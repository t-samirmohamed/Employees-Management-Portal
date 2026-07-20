# Employee Management API

A .NET 10 minimal API backend for managing employees: authentication, employee records, activity statistics, a live Swagger UI, and a SQL Server database that also logs every API call made against it.

## Features

- **Auth** — signup, login, and a real sign-out (JWT bearer tokens; logout rotates the user's Identity security stamp, which immediately invalidates every token issued to that user, not just a client-side token drop). Email and username must be unique. Signing up creates both the login account *and* its linked employee record — as `Inactive` — in one atomic transaction; an admin activates them via the employee endpoints below.
- **Employees** — list (with optional gender/status/attendance filters), view details, create, activate/deactivate.
- **Statistics** — employee counts grouped by gender (M/F), status (Active/Inactive), and attendance status (In Office/Absent/On Vacation/Out of Office).
- **API call logging** — every request (method, path, query, status code, duration, user id, IP) is recorded to a SQL table.
- **Swagger UI** at `/swagger`, with a working "Authorize" button for pasting in a JWT.

## Tech stack

- .NET 10, ASP.NET Core minimal APIs
- SQL Server + EF Core (Code-First migrations)
- ASP.NET Core Identity (`AddIdentityCore`) + JWT bearer authentication
- `Microsoft.AspNetCore.OpenApi` for OpenAPI generation + `Swashbuckle.AspNetCore.SwaggerUI` for the UI

## Project structure

```
EmpoloyeeManagment/
  Program.cs                          Composition root: DI, middleware pipeline, endpoint mapping
  Data/AppDbContext.cs                EF Core DbContext (Identity + Employees + ApiCallLogs)
  Models/                             Employee, ApplicationUser, ApiCallLog, enums (Gender, EmployeeStatus, AttendanceStatus)
  Dtos/                               Request/response records, grouped by feature (Auth, Employees, Statistics)
  Endpoints/                          One static class per feature area (AuthEndpoints, EmployeeEndpoints, StatisticsEndpoints)
  Services/                          ITokenService / TokenService — JWT issuance
  Middleware/ApiLoggingMiddleware.cs  Logs every request to ApiCallLogs
  OpenApi/BearerSecuritySchemeTransformer.cs   Adds the JWT bearer scheme to the generated OpenAPI doc
  Migrations/                         EF Core migrations
```

## Running it locally

**Prerequisites:** .NET 10 SDK, a local SQL Server instance (default instance or LocalDB).

1. Set the connection string in `appsettings.Development.json` (`ConnectionStrings:DefaultConnection`) — defaults to the local default SQL Server instance via Windows auth:
   ```
   Server=localhost;Database=EmployeeManagementDb;Trusted_Connection=True;TrustServerCertificate=True;
   ```
2. Apply migrations:
   ```
   dotnet ef database update
   ```
3. Run the API:
   ```
   dotnet run
   ```
4. Open `/swagger` (e.g. `http://localhost:5134/swagger` with the `http` launch profile).

**Note:** the JWT signing key in `appsettings.Development.json` is a locally-generated dev-only value. Move it to user-secrets or an environment variable before this project is ever pushed to source control or deployed anywhere.

## API overview

| Method | Route | Auth | Description |
|---|---|---|---|
| POST | `/api/auth/signup` | — | Create an account + linked (Inactive) employee record, returns a JWT |
| POST | `/api/auth/login` | — | Returns a JWT |
| POST | `/api/auth/logout` | ✔ | Revokes all of the user's outstanding tokens |
| GET | `/api/employees` | ✔ | List employees (optional `gender`, `status`, `attendanceStatus` filters) |
| GET | `/api/employees/{id}` | ✔ | Employee details |
| POST | `/api/employees` | ✔ | Create an employee |
| PATCH | `/api/employees/{id}/activate` | ✔ | Set status to Active |
| PATCH | `/api/employees/{id}/deactivate` | ✔ | Set status to Inactive |
| GET | `/api/statistics/employees` | ✔ | Counts by gender, status, and attendance status |

## Roadmap / not yet built

This is v1 — deliberately structured to extend cleanly. Known gaps not in scope yet: employee update/delete, role-based authorization, and per-device token revocation (current sign-out revokes all of a user's sessions at once).
