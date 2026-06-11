using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Controllers + Swagger ────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Portfolio Manager API", Version = "v1" });
});

// ── Database ─────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Finnhub HTTP Client ───────────────────────────────────────────────────────
builder.Services.AddHttpClient<IFinnhubService, FinnhubService>(client =>
{
    client.BaseAddress = new Uri("https://finnhub.io/api/v1/");
    var apiKey = builder.Configuration["Finnhub:ApiKey"] ?? string.Empty;
    if (!string.IsNullOrWhiteSpace(apiKey))
    {
        client.DefaultRequestHeaders.Add("X-Finnhub-Token", apiKey);
    }
    client.Timeout = TimeSpan.FromSeconds(10);
});

// ── Application Services ──────────────────────────────────────────────────────
builder.Services.AddScoped<IPortfolioService, PortfolioService>();
builder.Services.AddMemoryCache();          // used by ScannerController to cache scan results
builder.Services.AddHttpClient<IRsiScannerService, RsiScannerService>(client =>
{
    client.BaseAddress = new Uri("https://finnhub.io/api/v1/");
    var apiKey = builder.Configuration["Finnhub:ApiKey"] ?? string.Empty;
    if (!string.IsNullOrWhiteSpace(apiKey))
        client.DefaultRequestHeaders.Add("X-Finnhub-Token", apiKey);
    // Live scan can take ~60s for 50 symbols; set generous timeout
    client.Timeout = TimeSpan.FromSeconds(120);
});

// ── CORS (allow Angular dev server) ──────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularDevPolicy", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

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
