using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace PortfolioManager.Api.Services;

/// <summary>
/// Generates and validates the local-only shared secret used by the Windows Scheduled Task's trigger
/// script to call POST /api/automation/trigger — deliberately NOT an Admin JWT/password. The secret is
/// protected with DPAPI (CurrentUser scope) and stored only in %LOCALAPPDATA%, outside the repo, so it
/// can never be committed to source control, never appears in appsettings/logs, and only the same
/// Windows user/machine that generated it can decrypt it (the trigger script decrypts the same file
/// via the same DPAPI API from PowerShell).
/// </summary>
public interface IAutomationSecretStore
{
    /// <summary>Generates a new random secret, DPAPI-protects it, and overwrites the stored file. Never
    /// returns the plaintext value to the caller — the trigger script reads the protected file directly.</summary>
    Task GenerateAndStoreAsync(CancellationToken ct = default);

    /// <summary>True if a secret has been generated (setup/rotate has run at least once).</summary>
    bool Exists();

    /// <summary>Constant-time comparison against the stored (decrypted) secret. Never logs the value.</summary>
    Task<bool> ValidateAsync(string? headerValue, CancellationToken ct = default);
}

[SupportedOSPlatform("windows")]
public sealed class AutomationSecretStore(ILogger<AutomationSecretStore> logger) : IAutomationSecretStore
{
    private static readonly string SecretFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PortfolioManager",
        "automation-secret.protected");

    public async Task GenerateAndStoreAsync(CancellationToken ct = default)
    {
        var secretBytes = RandomNumberGenerator.GetBytes(32);
        var protectedBytes = ProtectedData.Protect(secretBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);

        var dir = Path.GetDirectoryName(SecretFilePath)!;
        Directory.CreateDirectory(dir);
        await File.WriteAllBytesAsync(SecretFilePath, protectedBytes, ct);
        logger.LogInformation("[AutomationSecretStore] Automation trigger secret (re)generated.");
    }

    public bool Exists() => File.Exists(SecretFilePath);

    public async Task<bool> ValidateAsync(string? headerValue, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(headerValue) || !File.Exists(SecretFilePath)) return false;

        try
        {
            var protectedBytes = await File.ReadAllBytesAsync(SecretFilePath, ct);
            var secretBytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            var expected = Convert.ToBase64String(secretBytes);

            var expectedBytes = System.Text.Encoding.UTF8.GetBytes(expected);
            var actualBytes = System.Text.Encoding.UTF8.GetBytes(headerValue);
            if (expectedBytes.Length != actualBytes.Length) return false;
            return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
        }
        catch (Exception ex)
        {
            // Never log headerValue or the decrypted secret.
            logger.LogWarning(ex, "[AutomationSecretStore] Failed to validate automation trigger secret.");
            return false;
        }
    }
}
