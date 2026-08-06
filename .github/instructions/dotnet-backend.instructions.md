---
applyTo: "**/*.cs"
description: "Portfolio Manager .NET 8 backend conventions. Use when adding controllers, services, models, or EF migrations."
---

# Backend Conventions

## Authorization

- **All controllers require `[Authorize]`** unless explicitly public.
- Role-restricted actions use `[Authorize(Roles = "Admin")]` or `[Authorize(Roles = "Admin,Trader")]`.
- Public auth endpoints (login, refresh, setup) use `[AllowAnonymous]`.

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]                          // ← required on every controller
public class FooController(IFooService service) : ControllerBase { }
```

## Async Methods

- Every `async` controller action and service method **must** accept `CancellationToken ct` and pass it to all async calls.
- Do **not** use `Task.Run` inside services — use `async/await` throughout.

```csharp
[HttpGet]
public async Task<IActionResult> GetAll(CancellationToken ct)
    => Ok(await service.GetAllAsync(ct));
```

## Service Pattern

Always interface + implementation. Register in `Program.cs`:

```csharp
builder.Services.AddScoped<IFooService, FooService>();      // request-scoped
builder.Services.AddSingleton<FooCacheService>();           // app-lifetime cache
builder.Services.AddHttpClient<IFooService, FooService>(...); // Yahoo Finance clients
```

## Enums

Always serialize as strings — global `JsonStringEnumConverter` in `Program.cs` covers it.
New C# enum **must** have a matching TypeScript string union in `frontend/.../portfolio.models.ts`.

## Yahoo Finance

- `YahooFinanceService`: uses **absolute URLs** — no `BaseAddress` on its `HttpClient`
- `YahooCrumbService`: singleton, caches crumb ~1 hr — never bypass or reset manually
- Scanner batch limit: **5 symbols + 300 ms delay** — do not remove throttling

## EF Core

- Migrations live in `Data/Migrations/` — **never edit generated files**
- After any schema change: `dotnet ef migrations add <Name>` then `dotnet ef database update`
- `AppDbContext` in `Data/AppDbContext.cs`

## Key Files

| File                           | Purpose                                           |
| ------------------------------ | ------------------------------------------------- |
| `Program.cs`                   | Service registration, CORS, middleware pipeline   |
| `appsettings.json`             | Connection string, scanner EOD window config      |
| `notification-recipients.json` | Email targets (not in DB)                         |
| `ScannerRuntimeConfig.cs`      | Override EOD window for tests                     |
| `AuthController.cs`            | Login / refresh / logout / setup (AllowAnonymous) |
