# Backend Service Templates

## Models (`FooModels.cs`)

```csharp
namespace PortfolioManager.Api.Models;

public record FooItem(int Id, string Name, decimal Value);

public record AddFooRequest(string Name, decimal Value);

public record UpdateFooRequest(string? Name, decimal? Value);
```

## Interface (`IFooService.cs`)

```csharp
namespace PortfolioManager.Api.Services;

public interface IFooService
{
    Task<IEnumerable<FooItem>> GetAllAsync(CancellationToken ct = default);
    Task<FooItem?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<FooItem> AddAsync(AddFooRequest request, CancellationToken ct = default);
    Task<FooItem?> UpdateAsync(int id, UpdateFooRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}
```

## Implementation (`FooService.cs`)

```csharp
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public class FooService(AppDbContext db) : IFooService
{
    public async Task<IEnumerable<FooItem>> GetAllAsync(CancellationToken ct = default)
        => await db.Foos.Select(f => new FooItem(f.Id, f.Name, f.Value)).ToListAsync(ct);

    public async Task<FooItem?> GetByIdAsync(int id, CancellationToken ct = default)
        => await db.Foos.Where(f => f.Id == id)
                        .Select(f => new FooItem(f.Id, f.Name, f.Value))
                        .FirstOrDefaultAsync(ct);

    public async Task<FooItem> AddAsync(AddFooRequest request, CancellationToken ct = default)
    {
        var entity = new Foo { Name = request.Name, Value = request.Value };
        db.Foos.Add(entity);
        await db.SaveChangesAsync(ct);
        return new FooItem(entity.Id, entity.Name, entity.Value);
    }

    public async Task<FooItem?> UpdateAsync(int id, UpdateFooRequest request, CancellationToken ct = default)
    {
        var entity = await db.Foos.FindAsync([id], ct);
        if (entity is null) return null;
        if (request.Name is not null) entity.Name = request.Name;
        if (request.Value is not null) entity.Value = request.Value.Value;
        await db.SaveChangesAsync(ct);
        return new FooItem(entity.Id, entity.Name, entity.Value);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.Foos.FindAsync([id], ct);
        if (entity is null) return false;
        db.Foos.Remove(entity);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
```

## Controller (`FooController.cs`)

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FooController(IFooService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await service.GetAllAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var item = await service.GetByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddFooRequest request, CancellationToken ct)
    {
        var item = await service.AddAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateFooRequest request, CancellationToken ct)
    {
        var item = await service.UpdateAsync(id, request, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => await service.DeleteAsync(id, ct) ? NoContent() : NotFound();
}
```

## `Program.cs` Registration

```csharp
// Scoped (per-request)
builder.Services.AddScoped<IFooService, FooService>();

// With HttpClient (for Yahoo Finance-style services)
builder.Services.AddHttpClient<IFooService, FooService>(client =>
{
    client.BaseAddress = new Uri("https://query1.finance.yahoo.com/");
    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
    client.Timeout = TimeSpan.FromSeconds(30);
});
```
