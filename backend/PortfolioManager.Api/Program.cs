using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

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
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Enter JWT token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ── Database ─────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── ASP.NET Core Identity ─────────────────────────────────────────────────────
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// ── JWT Bearer Authentication ──────────────────────────────────────────────────
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException(
        "Jwt:Secret is not configured. Run: dotnet user-secrets set \"Jwt:Secret\" \"<64-char-random>\"");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ClockSkew = TimeSpan.Zero
    };
});
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITokenService, TokenService>();

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
builder.Services.AddScoped<ICashService, CashService>();
builder.Services.AddScoped<IAllocationRiskService, AllocationRiskService>();
builder.Services.AddHttpClient<IOptionService, OptionService>(client =>
{
    client.BaseAddress = new Uri("https://query1.finance.yahoo.com/");
    client.DefaultRequestHeaders.Add("User-Agent", userAgent);
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddMemoryCache();          // used by ScannerController to cache scan results
builder.Services.AddHttpClient<IRsiScannerService, RsiScannerService>(client =>
{
    client.BaseAddress = new Uri("https://query1.finance.yahoo.com/");
    client.DefaultRequestHeaders.Add("User-Agent", userAgent);
    // Full TSX scan: ~17 batches × 1.5s = ~25s. Give generous timeout.
    client.Timeout = TimeSpan.FromSeconds(120);
});
builder.Services.AddScoped<ValueScreenerService>();
builder.Services.AddScoped<IStagedSignalService, StagedSignalService>();
// Singleton: persists/reads Value Screener results from DB
builder.Services.AddSingleton<ValueScreenerPersistenceService>();
// Background service: runs Value Screener at configured time (default 5 PM ET weekdays)
builder.Services.AddHostedService<ValueScreenerSchedulerService>();

// ── Rate Limiting (fixed window per IP — 200 req/min) ───────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 200,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ── CORS — localhost:4200 always allowed; production origin from config ───────
var allowedOrigins = new List<string> { "http://localhost:4200" };
var prodOrigin = builder.Configuration["CorsOrigin"];
if (!string.IsNullOrWhiteSpace(prodOrigin))
    allowedOrigins.Add(prodOrigin);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularDevPolicy", policy =>
        policy.WithOrigins([.. allowedOrigins])
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
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
    if (decimal.TryParse(section["EodOversoldRsiThreshold"], out var oversold) && oversold > 0)
        cfg.EodOversoldRsiThreshold = oversold;
    if (decimal.TryParse(section["EodOverboughtRsiThreshold"], out var overbought) && overbought > 0)
        cfg.EodOverboughtRsiThreshold = overbought;
    // Load persisted overrides (saved via PUT /api/scanner/eod-settings), takes priority over appsettings
    cfg.LoadFromFile();
    return cfg;
});

// ── Email Notification Services ───────────────────────────────────────────────
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailNotification"));
builder.Services.AddSingleton<NotificationRecipientsService>();
builder.Services.AddScoped<SectorIndustryService>();
builder.Services.AddSingleton<SignalNotificationTracker>();
// Singleton: all dependencies (IOptions, NotificationRecipientsService, SignalNotificationTracker, ILogger) are singletons
builder.Services.AddSingleton<EmailNotificationService>();
// Singleton: persists EOD CONFIRM signals to eod-signal-history.json for next-morning review
builder.Services.AddSingleton<EodSignalPersistenceService>();
// Background service: runs RSI scans every ScanIntervalSeconds, fires emails on new CONFIRMED signals
// regardless of which page is open in the frontend
builder.Services.AddHostedService<RsiAlertBackgroundService>();

// Portfolio value history: persists EOD portfolio value daily at 4:30 PM ET
builder.Services.AddScoped<IPortfolioValueHistoryService, PortfolioValueHistoryService>();
builder.Services.AddHostedService<PortfolioValueEodBackgroundService>();

// Portfolio beta calculation
builder.Services.AddScoped<IPortfolioBetaService, PortfolioBetaService>();

// RSI snapshot persistence and per-user preferences
builder.Services.AddScoped<IRsiSnapshotService, RsiSnapshotService>();
builder.Services.AddScoped<IUserPreferenceService, UserPreferenceService>();
builder.Services.AddScoped<IPortfolioSnapshotService, PortfolioSnapshotService>();
builder.Services.AddScoped<IWatchlistSnapshotService, WatchlistSnapshotService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

var app = builder.Build();

// ── Middleware Pipeline ───────────────────────────────────────────────────────
// Auto-apply EF migrations on every startup (ensures DailySignals and all tables exist)
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}
catch (Exception ex)
{
    var startupLog = app.Services.GetRequiredService<ILogger<Program>>();
    startupLog.LogError(ex, "EF migration failed on startup — some tables may be missing.");
}

// ── Seed roles (Admin, Trader, Viewer) ────────────────────────────────────────
try
{
    using var roleScope = app.Services.CreateScope();
    var roleManager = roleScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in new[] { "Admin", "Trader", "Viewer" })
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }
}
catch (Exception ex)
{
    var startupLog = app.Services.GetRequiredService<ILogger<Program>>();
    startupLog.LogError(ex, "Role seeding failed.");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRateLimiter();
app.UseCors("AngularDevPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

