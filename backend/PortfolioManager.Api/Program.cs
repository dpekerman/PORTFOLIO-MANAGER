using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// ── Controllers + Swagger ────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        // Serialize enums as strings ("Oversold"/"Overbought") so Angular TypeScript
        // string-union types match without manual mapping.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Portfolio Manager API", Version = "v1" });
});

// ── Database ─────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Yahoo Finance HTTP Clients ───────────────────────────────────────────────
// YahooFinanceService makes absolute URL calls to both query1 and query2, so NO BaseAddress.
const string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

// Crumb service: singleton that caches the Yahoo Finance session crumb (~1 hour TTL)
builder.Services.AddSingleton<YahooCrumbService>();

builder.Services.AddHttpClient<IMarketDataProvider, YahooFinanceService>(client =>
{
    // No BaseAddress — service uses absolute URLs to query1 (chart/search) and query2 (v7/v10 + crumb)
    client.DefaultRequestHeaders.Add("User-Agent", userAgent);
    client.DefaultRequestHeaders.Add("Accept", "*/*");
    client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
    client.Timeout = TimeSpan.FromSeconds(15);
});

// ── Application Services ──────────────────────────────────────────────────────
builder.Services.AddScoped<IPortfolioService, PortfolioService>();
builder.Services.AddScoped<IWatchlistService, WatchlistService>();
builder.Services.AddMemoryCache();          // used by ScannerController to cache scan results
builder.Services.AddHttpClient<IRsiScannerService, RsiScannerService>(client =>
{
    client.BaseAddress = new Uri("https://query1.finance.yahoo.com/");
    client.DefaultRequestHeaders.Add("User-Agent", userAgent);
    // Full TSX scan: ~17 batches × 1.5s = ~25s. Give generous timeout.
    client.Timeout = TimeSpan.FromSeconds(120);
});
builder.Services.AddScoped<ValueScreenerService>();

// ── CORS (allow Angular dev server) ──────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularDevPolicy", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// ── Scanner Runtime Config (singleton — EOD window overridable at runtime) ────
builder.Services.AddSingleton<ScannerRuntimeConfig>(sp =>
{
    var cfg = new ScannerRuntimeConfig();
    var section = builder.Configuration.GetSection("ScannerSettings");
    if (!string.IsNullOrWhiteSpace(section["EodWindowStart"]))
        cfg.EodWindowStart = section["EodWindowStart"]!;
    if (!string.IsNullOrWhiteSpace(section["EodWindowEnd"]))
        cfg.EodWindowEnd = section["EodWindowEnd"]!;
    if (bool.TryParse(section["EodWindowEnabled"], out var enabled))
        cfg.EodWindowEnabled = enabled;
    return cfg;
});

// ── Email Notification Services ───────────────────────────────────────────────
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailNotification"));
builder.Services.AddSingleton<NotificationRecipientsService>();
builder.Services.AddSingleton<SectorIndustryService>();
builder.Services.AddSingleton<SignalNotificationTracker>();
// Singleton: all dependencies (IOptions, NotificationRecipientsService, SignalNotificationTracker, ILogger) are singletons
builder.Services.AddSingleton<EmailNotificationService>();
// Background service: runs RSI scans every ScanIntervalSeconds, fires emails on new CONFIRMED signals
// regardless of which page is open in the frontend
builder.Services.AddHostedService<RsiAlertBackgroundService>();

var app = builder.Build();

// ── Middleware Pipeline ───────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Auto-apply EF migrations on startup in development
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.UseCors("AngularDevPolicy");
app.UseAuthorization();
app.MapControllers();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
