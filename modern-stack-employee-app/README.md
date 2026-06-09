# Modern Stack Employee App

Example project for interview preparation:

- Angular 21 frontend
- .NET 10 Web API backend
- Entity Framework Core
- SQL Server integration
- Swagger
- Azure DevOps YAML pipeline

## Open in VS Code

```bash
code modern-stack-employee-app
```

## SQL Server Setup

Run this script in SQL Server Management Studio or Azure Data Studio:

```text
database/create-database.sql
```

This creates:

- EmployeeDb
- dbo.Employees
- sample employee row

## Backend Setup

```bash
cd backend/EmployeeApp.Api
dotnet restore
dotnet run
```

Swagger should be available at:

```text
https://localhost:5001/swagger
```

If your API runs on a different port, update the Angular service URL in:

```text
frontend/employee-client/src/app/features/employees/employee.service.ts
```

## Angular Setup

```bash
cd frontend/employee-client
npm install
npm start
```

Angular should run at:

```text
http://localhost:4200
```

## Interview Talking Points

### Architecture

Explain that the app uses Angular for SPA UI, .NET Web API for backend services, EF Core for SQL Server persistence, and Azure DevOps for CI/CD.

### Backend

Key points:

- Controllers expose REST endpoints.
- Services contain business logic.
- DbContext handles SQL Server access.
- DTOs protect the API contract.
- Swagger documents and tests endpoints.
- CORS allows Angular to call the API.

### Frontend

Key points:

- Standalone Angular components.
- Signals for local state.
- Reactive forms for validation.
- HttpClient service for API calls.
- Functional HTTP interceptor for global API error handling.
- `@if` and `@for` modern Angular template syntax.

### SQL Server

Key points:

- Table has a primary key.
- Email has a unique constraint.
- Salary uses decimal(18,2).
- CreatedAt uses UTC time.
- In real projects, add indexes for search and reporting.

### CI/CD

Pipeline stages:

1. Restore dependencies.
2. Build .NET API.
3. Build Angular client.
4. Publish artifacts.
5. Deploy to Dev.
6. Add approval gates before QA/Production.

## Popular Interview Questions

### 1. How would you design this application?

I would separate the frontend, backend, and database layers. Angular handles the user interface and API calls. .NET exposes REST APIs and applies business rules. SQL Server stores relational data. CI/CD automates build, test, and deployment.

### 2. Why use DTOs?

DTOs prevent exposing database entities directly through the API. They also allow the API contract to evolve independently from the database model.

### 3. Why use services instead of putting logic in controllers?

Controllers should be thin. Services improve maintainability, testability, and separation of concerns.

### 4. How does Angular call the API?

Angular uses HttpClient through a dedicated service. This keeps API logic reusable and prevents components from becoming too complex.

### 5. How do you handle environment-specific URLs?

Use Angular environment configuration or runtime configuration. For .NET, use appsettings files, environment variables, or Azure App Configuration.

### 6. How do you deploy database changes?

For small and medium teams, EF Core migrations are common. In enterprise environments, DACPAC or DBA-reviewed scripts may be preferred.

### 7. What should be in a production pipeline?

Build, unit tests, linting, security scan, artifact publishing, deployment to lower environments, smoke testing, approvals, and production deployment.

### 8. How would you improve this sample for production?

Add JWT authentication, global exception middleware, Serilog, validation with FluentValidation, automated tests, paging/sorting, Docker, Key Vault, and Application Insights.
