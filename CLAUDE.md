# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A .NET 10 minimal API backend for managing employees: JWT auth (signup/login/logout with real server-side revocation), employee records, activity statistics, Swagger UI, and a SQL Server database that also logs every API call made against it. Single-project solution — no separate test project exists yet.

## Commands

```
dotnet build                                                        # from repo root or EmpoloyeeManagment/
dotnet run --project EmpoloyeeManagment                             # run the API (from repo root)
dotnet ef migrations add <Name> --project EmpoloyeeManagment         # add a migration
dotnet ef database update --project EmpoloyeeManagment               # apply migrations
```

Run from inside `EmpoloyeeManagment/` and drop `--project EmpoloyeeManagment`. The global `dotnet-ef` tool version must match the `Microsoft.EntityFrameworkCore.Design`/`Tools` package version (currently 10.0.9) — `dotnet tool update -g dotnet-ef` if migrations commands fail with a version mismatch.

Prerequisites: .NET 10 SDK, a local SQL Server instance (default instance or LocalDB) with the connection string in `appsettings.Development.json` (`ConnectionStrings:DefaultConnection`). After `dotnet run`, open `/swagger` (e.g. `http://localhost:5134/swagger` with the `http` launch profile) — use the "Authorize" button to paste a JWT obtained from `/api/auth/login` or `/api/auth/signup`.

There are no automated tests in this repo yet; verification is manual via Swagger (see `EmpoloyeeManagment/docs/Project_Startup_Plan.md` for the full walkthrough: signup → login → Authorize → create employee → list/detail → activate/deactivate → statistics → logout → confirm the old JWT now gets 401).

## Architecture

**Composition root**: `Program.cs` wires DI, the middleware pipeline, and endpoint mapping only — no business logic lives there. Each feature area's endpoints are mapped via a `Map*Endpoints(this IEndpointRouteBuilder)` extension method (`AuthEndpoints`, `EmployeeEndpoints`, `StatisticsEndpoints`), each building its own `app.MapGroup("/api/...")` with `.RequireAuthorization()` applied once at the group level rather than per-route.

**Auth is `AddIdentityCore`, not `AddIdentity`** — deliberately, since this is a pure bearer-token API with no browser sign-in surface; the full `AddIdentity` wires up cookie auth as a side effect (401s become redirects), which fights JWT-only auth. There are no ASP.NET Identity roles in play.

**Real server-side sign-out via security-stamp rotation**: JWTs carry a custom `securityStamp` claim captured at issue time (see `Services/TokenService.cs`). `JwtBearerEvents.OnTokenValidated` in `Program.cs` re-fetches the user's *current* stamp via `UserManager` on every authenticated request and fails auth if it doesn't match — this is the same mechanism cookie-based Identity auth uses internally (`SecurityStampValidator`), applied manually to bearer tokens. `POST /api/auth/logout` calls `UserManager.UpdateSecurityStampAsync`, which immediately invalidates *every* outstanding token for that user (not per-device revocation — a known v1 limitation). This trades some of JWT's normal statelessness for a DB read per request; accepted as fine at this scale.

**Signup is transactional across two tables**: `AuthEndpoints.SignupAsync` opens a `db.Database.BeginTransactionAsync()` before calling `UserManager.CreateAsync` (Identity writes through the same scoped `AppDbContext`, so it participates in the transaction) and then inserts a linked `Employee` row (`Employee.UserId` → the new user's Id) with `Status = Inactive`. Both writes commit or roll back together — an admin later activates the employee via `PATCH /api/employees/{id}/activate`.

**API call logging happens in `Middleware/ApiLoggingMiddleware`, registered first in the pipeline** (before CORS/auth), so it wraps and times literally every request including ones that fail auth. Because middleware unwinds inside-out, by the time its `finally` block runs, inner auth middleware has already populated `context.User`, so the log row can include the user id even though logging middleware runs "before" auth. The `SaveChangesAsync` for the log write is caught and logged via `ILogger`, never rethrown — a logging failure must never turn a successful business response into a 500. This is a plain synchronous per-request insert (no queue/batching) — treat that as an intentional v1 choice, not an oversight, if you touch this file.

**Swagger uses the .NET 9/10 native OpenAPI generator, not classic Swashbuckle `AddSwaggerGen`**: `Microsoft.AspNetCore.OpenApi`'s `AddOpenApi()`/`MapOpenApi()` generates `/openapi/v1.json`; `Swashbuckle.AspNetCore.SwaggerUI` (UI-only package) renders it at `/swagger`. `OpenApi/BearerSecuritySchemeTransformer.cs` is a custom `IOpenApiDocumentTransformer` that adds the JWT bearer scheme to the generated document — this is what makes the UI's "Authorize" button work, since Swashbuckle's old `AddSecurityDefinition`/`IOperationFilter` model doesn't apply once Microsoft's generator owns the document.

**Data model** (`Data/AppDbContext.cs : IdentityDbContext<ApplicationUser>`): `ApplicationUser : IdentityUser` (Identity's stock user), `Employee` (links to `ApplicationUser` via nullable `UserId`; `Gender`/`Status`/`AttendanceStatus` enums), `ApiCallLog`. Enum values matter and are serialized as strings (`JsonStringEnumConverter` registered in `Program.cs`): `Gender { M, F }`, `EmployeeStatus { Active, Inactive }`, `AttendanceStatus { InOffice, Absent, OnVacation, OutOfOffice }`.

**CORS**: allowed origins are config-driven (`Cors:AllowedOrigins` in `appsettings.*.json`, currently `http://localhost:3000` for a Next.js client) and applied via a named `"NextJsClient"` policy with `AllowAnyHeader().AllowAnyMethod()` but no `AllowCredentials()` — auth is a bearer token in the `Authorization` header, not cookies, so credentialed CORS isn't needed.

**Secrets**: the JWT signing key in `appsettings.Development.json` is a locally-generated dev-only value checked in for convenience — move it to user-secrets or an environment variable before any deployment or before this leaves local dev.

## Project layout

```
EmpoloyeeManagment/
  Program.cs                          Composition root: DI, middleware pipeline, endpoint mapping
  Data/AppDbContext.cs                EF Core DbContext (Identity + Employees + ApiCallLogs)
  Models/                             Employee, ApplicationUser, ApiCallLog, enums
  Dtos/                               Request/response records, grouped by feature (Auth, Employees, Statistics)
  Endpoints/                          One static class per feature area
  Services/                           ITokenService / TokenService — JWT issuance
  Middleware/ApiLoggingMiddleware.cs  Logs every request to ApiCallLogs
  OpenApi/BearerSecuritySchemeTransformer.cs   Adds the JWT bearer scheme to the generated OpenAPI doc
  Migrations/                         EF Core migrations
  docs/Project_Startup_Plan.md        Original design rationale — read this for the "why" behind the above
```

## Known v1 gaps (not oversights — see README roadmap)

Employee update/delete, role-based authorization, and per-device token revocation (logout currently revokes all of a user's sessions at once) are deliberately out of scope for now.
