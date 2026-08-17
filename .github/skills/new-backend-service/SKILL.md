---
name: new-backend-service
description: "Scaffold a new .NET 8 backend service for Portfolio Manager. Use when adding a new API endpoint, controller, service interface/implementation, or background service. Generates interface, implementation, controller, and Program.cs registration."
argument-hint: "service name (e.g. 'Dividend', 'TaxReport')"
---

# New Backend Service

Scaffolds a complete .NET 8 service following the interface/implementation pattern used in this project.

## When to Use

- Adding a new API endpoint group
- Creating a new domain service (CRUD, calculation, data fetch)
- User asks to "add a backend service", "create an endpoint", "add an API for X"

## Files to Create

Given service name `Foo`:

```
backend/PortfolioManager.Api/
├── Controllers/FooController.cs
├── Services/IFooService.cs          (interface)
├── Services/FooService.cs           (implementation)
└── Models/FooModels.cs              (request/response DTOs)
```

## Templates

See [./assets/templates.md](./assets/templates.md) for copy-paste file templates.

## Steps

1. Define request/response model types in `Models/FooModels.cs`
2. Create `Services/IFooService.cs` with the interface
3. Create `Services/FooService.cs` implementing the interface
4. Create `Controllers/FooController.cs`
5. Register in `Program.cs`:
   ```csharp
   builder.Services.AddScoped<IFooService, FooService>();
   ```
6. Add any matching TypeScript types to `frontend/.../portfolio.models.ts`

## Rules

- Always use interface + implementation (`IFooService` / `FooService`)
- Enums are serialized as strings globally — use `enum` normally in C#
- Inject via constructor (standard .NET DI — not Angular's `inject()`)
- New C# enum **requires** a matching TypeScript string union in `portfolio.models.ts`
- For Yahoo Finance HTTP work: register a named `HttpClient` in `Program.cs`, not inside service
- Background/timer services: implement `IHostedService` / inherit `BackgroundService`
