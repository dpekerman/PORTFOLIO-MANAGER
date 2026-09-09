using Microsoft.Data.SqlClient;

namespace PortfolioManager.Api.Services;

public record DatabaseBackupResult(bool Ran, string? FilePath, string Message);

/// <summary>
/// Creates a full SQL Server backup once per Eastern-Time calendar day into
/// D:\PORTFOLIO-MANAGER-SQL-BACKUP-ALL\DAYLY_BACKUP\&lt;yyyy-MM-dd&gt;\&lt;db&gt;.bak (defaults,
/// overridable via DatabaseBackup:BasePath), alongside a snapshot of the latest SQL scripts
/// (database/SQL) copied into a SQL-Scripts subfolder so each dated folder is a self-contained
/// emergency-recovery kit. The scheduled background timer is idempotent (a backup already present
/// for today is left alone); the manual "Backup Now" action always produces a new file, adding a
/// timestamp suffix if today's default file already exists.
/// </summary>
public interface IDatabaseBackupService
{
    Task<DatabaseBackupResult> RunBackupIfNeededAsync(CancellationToken ct);

    /// <summary>On-demand "Backup Now": if today's backup already exists, adds a NEW timestamped
    /// file alongside it instead of skipping — every manual click is preserved.</summary>
    Task<DatabaseBackupResult> RunManualBackupAsync(CancellationToken ct);
}

public sealed class DatabaseBackupService(
    IConfiguration configuration,
    ILogger<DatabaseBackupService> logger) : IDatabaseBackupService
{
    public Task<DatabaseBackupResult> RunBackupIfNeededAsync(CancellationToken ct) => RunAsync(forceNew: false, ct);

    public Task<DatabaseBackupResult> RunManualBackupAsync(CancellationToken ct) => RunAsync(forceNew: true, ct);

    private async Task<DatabaseBackupResult> RunAsync(bool forceNew, CancellationToken ct)
    {
        var tz = MarketHoursGate.GetEasternTimeZone();
        if (tz is null)
            return new DatabaseBackupResult(false, null, "Eastern time zone unavailable on this machine.");

        // Anchored to Eastern Time (not Windows local time) so "15:00" still means 3 PM Toronto even
        // if the machine's local timezone changes while traveling - same pattern used everywhere else
        // in this app's automation (see AutomationRuntimeConfig / EodAutomationOrchestratorService).
        var nowEt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var todayFolder = Path.Combine(BasePath, nowEt.ToString("yyyy-MM-dd"));
        Directory.CreateDirectory(todayFolder);
        // Snapshot the current SQL scripts (schema + seed) alongside the .bak file every time this
        // folder is touched, so a single dated folder is a self-contained emergency-recovery kit.
        var scriptsCopied = CopySqlScripts(todayFolder);

        var dbName = GetDatabaseName();
        var defaultFile = Path.Combine(todayFolder, $"{dbName}.bak");

        string backupFile;
        if (!File.Exists(defaultFile))
        {
            backupFile = defaultFile;
        }
        else if (!forceNew)
        {
            return new DatabaseBackupResult(false, defaultFile, $"Backup for {nowEt:yyyy-MM-dd} already exists.");
        }
        else
        {
            // Manual "Backup Now" clicked and today's backup already exists — create an additional
            // timestamped file rather than skipping or overwriting today's existing backup.
            backupFile = Path.Combine(todayFolder, $"{dbName}_{nowEt:HHmmss}.bak");
        }

        var connStr = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        // No COMPRESSION - not supported on SQL Server Express (confirmed: Msg 1844).
        cmd.CommandTimeout = 300;
        cmd.CommandText = $"BACKUP DATABASE [{dbName}] TO DISK = @path WITH INIT";
        cmd.Parameters.AddWithValue("@path", backupFile);
        await cmd.ExecuteNonQueryAsync(ct);

        logger.LogInformation("[DatabaseBackup] Backed up '{Db}' to {Path}.", dbName, backupFile);
        var scriptsNote = scriptsCopied > 0 ? $" (+{scriptsCopied} SQL scripts snapshotted)" : "";
        return new DatabaseBackupResult(true, backupFile, $"Backed up '{dbName}' to {backupFile}.{scriptsNote}");
    }

    /// <summary>Copies the latest *.sql scripts into &lt;todayFolder&gt;\SQL-Scripts. Best-effort —
    /// never fails the actual database backup if the scripts folder is missing or locked.</summary>
    private int CopySqlScripts(string todayFolder)
    {
        try
        {
            if (!Directory.Exists(SqlScriptsSourceDir)) return 0;
            var destDir = Path.Combine(todayFolder, "SQL-Scripts");
            Directory.CreateDirectory(destDir);
            var files = Directory.GetFiles(SqlScriptsSourceDir, "*.sql", SearchOption.TopDirectoryOnly);
            foreach (var file in files)
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true);
            return files.Length;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[DatabaseBackup] Failed to snapshot SQL scripts; backup continues without it.");
            return 0;
        }
    }

    private string BasePath =>
        configuration["DatabaseBackup:BasePath"] is { Length: > 0 } cfgPath
            ? cfgPath
            : @"D:\PORTFOLIO-MANAGER-SQL-BACKUP-ALL\DAYLY_BACKUP";

    // database/SQL holds the authoritative, up-to-date create/seed scripts (see SCRIPTS folder
    // README) — 5 levels up from bin/Debug/net8.0 reaches the repo root, same pattern as
    // AutomationController.DefaultScriptsDir.
    private string SqlScriptsSourceDir =>
        configuration["DatabaseBackup:SqlScriptsSourceDir"] is { Length: > 0 } cfgPath
            ? cfgPath
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "database", "SQL"));

    private string GetDatabaseName()
    {
        var connStr = configuration.GetConnectionString("DefaultConnection") ?? "";
        var builder = new SqlConnectionStringBuilder(connStr);
        return string.IsNullOrWhiteSpace(builder.InitialCatalog) ? "PortfolioManagerLocal" : builder.InitialCatalog;
    }
}

/// <summary>Polls every 5 minutes and backs up once the configured Eastern-Time trigger (default
/// 14:30 ET, run well before the 15:15 ET EOD automation trigger) is reached each day. Runs every
/// day (not just trading days) since it protects all app data, not only market data.</summary>
public sealed class DatabaseBackupBackgroundService(
    IDatabaseBackupService backupService,
    IConfiguration configuration,
    ILogger<DatabaseBackupBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("[DatabaseBackup] Background service starting.");
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var tz = MarketHoursGate.GetEasternTimeZone();
                if (tz is not null)
                {
                    var nowEt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
                    var triggerTimeEt = configuration["DatabaseBackup:BackupTimeEt"] is { Length: > 0 } cfg
                        ? cfg : "14:30";
                    if (TimeSpan.TryParse(triggerTimeEt, out var triggerTime) && nowEt.TimeOfDay >= triggerTime)
                    {
                        var result = await backupService.RunBackupIfNeededAsync(stoppingToken);
                        if (result.Ran)
                            logger.LogInformation("[DatabaseBackup] {Message}", result.Message);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "[DatabaseBackup] Check failed."); }

            try { await Task.Delay(PollInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
