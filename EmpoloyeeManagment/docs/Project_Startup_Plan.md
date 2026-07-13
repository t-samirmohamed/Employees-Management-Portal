# Employee Management API — .NET 10 Minimal API + Swagger + SQL Server

## Context

The `EmpoloyeeManagment` folder currently holds a bare `dotnet new web` scaffold (net10.0, just `app.MapGet("/", () => "Hello World!")`). The goal is to turn it into a real Employee Management backend: auth (signup/login/logout), employee listing/detail/activate-deactivate, employee statistics (gender/status/attendance), Swagger UI for testing, and a SQL Server database that stores both employee data and a log of every API call. This is deliberately v1 of a system the user plans to keep extending, so the structure favors clean, additive extension points over building out speculative features now.

Confirmed locally: .NET SDK 10.0.301 is installed, and SQL Server is available both as a running default-instance Windows service (`MSSQLSERVER`) and via SQL Server LocalDB.

**Decisions confirmed with the user:**
- Add a `POST /api/employees` create endpoint now (beyond the original list) so real data can be entered — no full update/delete yet, no DB seeding needed.
- "Sign out" must be a real server-side revocation (not just client-side token deletion), even though JWTs are normally stateless.

## Data model (EF Core Code-First, single `AppDbContext`)

- `ApplicationUser : IdentityUser` — stock ASP.NET Core Identity user (Id, Email, PasswordHash, SecurityStamp, ...). Kept decoupled from `Employee` for v1 (no requirement ties a login to a specific employee record yet); linking them is a natural later feature.
- `Employee`: `Id (int, PK)`, `FirstName`, `LastName`, `Email`, `Gender (enum)`, `Status (enum, default Active)`, `AttendanceStatus (enum, default InOffice)`, `CreatedAt (UTC)`.
- `ApiCallLog`: `Id (long, PK)`, `Timestamp (UTC)`, `Method`, `Path`, `QueryString`, `StatusCode`, `DurationMs`, `UserId (nullable)`, `IpAddress (nullable)`.
- Enums (matching the spec literally):
  - `Gender { M, F }`
  - `EmployeeStatus { Active, Inactive }`
  - `AttendanceStatus { InOffice, Absent, OnVacation, OutOfOffice }`
- `AppDbContext : IdentityDbContext<ApplicationUser>` adds `DbSet<Employee> Employees` and `DbSet<ApiCallLog> ApiCallLogs`.

## Auth design (JWT bearer + real sign-out)

- `builder.Services.AddIdentityCore<ApplicationUser>()` — **not** the full `AddIdentity<TUser,TRole>()`. This is a pure bearer-token API with no browser sign-in surface; `AddIdentity` wires up cookie auth as a side effect (401s get redirected to a login page instead of staying 401s), which fights a JWT-only API. Chain `.AddEntityFrameworkStores<AppDbContext>()` and `.AddDefaultTokenProviders()`. No roles for v1 (skip `.AddRoles<TRole>()` — add later if role-based authorization becomes a real need).
- `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)` as the sole auth scheme.
- `POST /api/auth/signup` — `UserManager.CreateAsync`, then auto-issues a JWT so the client is logged in immediately.
- `POST /api/auth/login` — validates credentials via `UserManager`/password check, issues a JWT.
- `POST /api/auth/logout` *(requires auth)* — calls `UserManager.UpdateSecurityStampAsync(user)`, rotating the user's Identity security stamp. This immediately invalidates every JWT previously issued to that user.
- Token contents: `sub`/`NameIdentifier` = user id, `email`, plus a custom `securityStamp` claim holding the stamp at issue time.
- `AddJwtBearer(...)` wires `JwtBearerEvents.OnTokenValidated` to re-fetch the user's *current* security stamp via `UserManager.GetSecurityStampAsync` and fail auth if it doesn't match the token's claim — this is what makes logout actually revoke access instead of waiting out the token's natural expiry. This is the same mechanism cookie-based Identity auth already uses internally (`SecurityStampValidator`), just applied manually to bearer JWTs — not a novel hack.
  - Trade-off, accepted for v1: this puts a DB read on every authenticated request, which removes some of JWT's usual statelessness benefit. Fine at this scale; if it ever matters, cache the live stamp per user in `IMemoryCache` with a short TTL — not built now.
  - Trade-off vs. a refresh-token/session table, one line: security-stamp rotation revokes *all* of a user's tokens at once (simple, matches "real server-side sign-out") but can't revoke a single device without logging the user out everywhere. Good enough for v1; a token table is a reasonable later feature if per-device revocation is ever needed.
- Config-driven signing key/issuer/audience/expiry under a `Jwt` section in `appsettings.json` (local dev key generated now; noted as something to move to secrets/environment for anything beyond local dev).
- `ITokenService`/`TokenService` encapsulates JWT creation so the three auth endpoints stay thin.

## Endpoints (minimal APIs, grouped via `MapGroup` extension methods)

- `Endpoints/AuthEndpoints.cs` → `/api/auth`: `POST /signup`, `POST /login`, `POST /logout` (auth required).
- `Endpoints/EmployeeEndpoints.cs` → `/api/employees` (all require auth):
  - `GET /` — list, with optional `gender`/`status`/`attendanceStatus` filter query params.
  - `GET /{id}` — details, 404 if missing.
  - `POST /` — create (the newly-agreed addition).
  - `PATCH /{id}/activate`, `PATCH /{id}/deactivate`.
- `Endpoints/StatisticsEndpoints.cs` → `/api/statistics` (auth required): `GET /employees` returns one DTO with counts grouped by gender, by status, and by attendance status (three small `GroupBy`/`Count` queries), plus a total. One combined endpoint since a dashboard would want all three together; splitting into three endpoints later is a trivial change if needed.
- Each file is a static class with a `Map...Endpoints(this IEndpointRouteBuilder app)` extension, called from `Program.cs`. Each builds its group via `app.MapGroup("/api/...")`, chains `.WithTags(...)` for clean Swagger grouping and `.RequireAuthorization()` once at the group level (instead of decorating every route) where auth is required — keeps `Program.cs` itself tiny and keeps the door open for more endpoint groups later.

## API call logging middleware

- `Middleware/ApiLoggingMiddleware.cs`, registered as the *first* middleware in the pipeline (before auth) so it captures literally every request — including ones that fail auth or never reach an endpoint — and times the whole thing. Convention-based middleware (constructor takes only `RequestDelegate next`); gets a scoped `AppDbContext` via an extra `InvokeAsync(HttpContext, AppDbContext)` parameter rather than the constructor, which is the current documented .NET 10 pattern (middleware itself is constructed once at startup, so a scoped dependency has to come in through `InvokeAsync`, resolved per-request from that request's DI scope).
- Times the request with a `Stopwatch`, wraps `await next(context)` in `try/finally`. Because middleware unwinds inside-out, `context.User` is already populated by the time the `finally` block runs (auth middleware, nested inside, has already set it) even though logging is first in the pipeline — so it records method/path/query/status/duration/user id (if authenticated)/IP and saves one `ApiCallLog` row via `SaveChangesAsync`. Never touches `context.Response.Body`, so response streaming is unaffected.
- The logging `SaveChangesAsync` itself is wrapped so a failure there is caught and logged (via `ILogger`), never rethrown — a logging hiccup must not turn a successful business response into a 500.
- Plain synchronous per-request insert for v1 (no background queue/batching) — confirmed this is reasonable at this scale, not premature optimization. A `Channel<T>` + background consumer is worth it later only once log writes measurably add latency; adding that now would trade a simple failure mode (occasional slow insert) for harder ones (dropped rows on crash, silent consumer death).

## Swagger

.NET 9/10 dropped Swashbuckle from the default templates in favor of the built-in `Microsoft.AspNetCore.OpenApi` document generator — classic full Swashbuckle (`AddSwaggerGen`) still technically works on net10.0, but it's no longer the direction the SDK is heading, and it's an extra third-party dependency for something the framework now does natively. Going with the current idiomatic hybrid instead, which still gets the literal classic Swagger UI that was asked for (as opposed to newer alternatives like Scalar, which is a different-looking UI):

- `Microsoft.AspNetCore.OpenApi`'s `AddOpenApi()` generates the OpenAPI document; `app.MapOpenApi()` exposes it at `/openapi/v1.json`. No SwaggerGen involved.
- `Swashbuckle.AspNetCore.SwaggerUI` (the UI-only package, zero dependency on SwaggerGen) supplies the actual Swagger UI page, pointed at that generated `/openapi/v1.json`.
- One custom `IOpenApiDocumentTransformer` (e.g. `BearerSecuritySchemeTransformer`) registered via `AddOpenApi(options => options.AddDocumentTransformer<...>())` adds the JWT bearer security scheme + a global security requirement to the generated document — this is what makes the UI's "Authorize" button appear and actually attach the token to try-it-out calls (Swashbuckle's old `AddSecurityDefinition`/`IOperationFilter` model doesn't apply once Microsoft's generator owns the document).
- Both `MapOpenApi()`/`UseSwaggerUI()` enabled in Development (standard default; trivial to widen later). `UseSwaggerUI` configured with `RoutePrefix = "swagger"` so the UI stays at the conventional `/swagger` path.
- Minimal API handlers already populate most OpenAPI metadata automatically in .NET 9/10 (types via `TypedResults`/`Results<T1,T2>`), so `.WithSummary()`/`.WithDescription()`/`.WithTags()` cover documentation needs — no need to sprinkle `.WithOpenApi()` everywhere.

## Database / connection string

- SQL Server (EF Core `Microsoft.EntityFrameworkCore.SqlServer`), Code-First migrations (`dotnet ef migrations add InitialCreate`, `dotnet ef database update`).
- Since a default-instance SQL Server service is already running locally, `appsettings.Development.json` gets:
  `"ConnectionStrings:DefaultConnection": "Server=localhost;Database=EmployeeManagementDb;Trusted_Connection=True;TrustServerCertificate=True;"` (Windows auth, no password to manage). LocalDB (`Server=(localdb)\mssqllocaldb;...`) noted as a drop-in fallback if preferred instead.
- Same database holds both `Employees` and `ApiCallLogs` tables (plus Identity's `AspNetUsers` etc.) — one connection string, one DB, as asked.

## Project structure

```
EmpoloyeeManagment/
  Program.cs                      (wiring only: services, middleware, Map*Endpoints calls)
  Data/AppDbContext.cs
  Models/ (ApplicationUser.cs, Employee.cs, ApiCallLog.cs, Enums.cs)
  Dtos/ (Auth/, Employees/, Statistics/ — request/response records)
  Endpoints/ (AuthEndpoints.cs, EmployeeEndpoints.cs, StatisticsEndpoints.cs)
  Services/ (ITokenService.cs, TokenService.cs)
  Middleware/ApiLoggingMiddleware.cs
  OpenApi/BearerSecuritySchemeTransformer.cs
  Migrations/ (generated)
```

## NuGet packages to add

All pinned to the identical `10.0.9` patch (mixing patch versions across these is a classic source of runtime type-load errors) except Swashbuckle's UI package, which ships on its own cadence:

- `Microsoft.AspNetCore.OpenApi` — 10.0.9
- `Swashbuckle.AspNetCore.SwaggerUI` — 10.2.3 (UI-only, no SwaggerGen dependency)
- `Microsoft.AspNetCore.Identity.EntityFrameworkCore` — 10.0.9
- `Microsoft.AspNetCore.Authentication.JwtBearer` — 10.0.9
- `Microsoft.EntityFrameworkCore.SqlServer` — 10.0.9
- `Microsoft.EntityFrameworkCore.Tools` — 10.0.9
- `Microsoft.EntityFrameworkCore.Design` — 10.0.9

Setup notes:
- Don't separately pin `Microsoft.IdentityModel.*`/`System.IdentityModel.Tokens.Jwt` — let NuGet resolve those transitively via the JwtBearer package to avoid version-mismatch token-validation errors.
- Run `dotnet tool update -g dotnet-ef` (or install if missing) — the global `dotnet-ef` CLI tool must match the `10.0.x` `Design`/`Tools` version, and dev machines often have a stale 8.x/9.x global install.

## Verification

1. `dotnet build` — compiles clean.
2. `dotnet ef database update` — creates `EmployeeManagementDb` locally; spot-check tables (`Employees`, `ApiCallLogs`, `AspNetUsers`) exist.
3. `dotnet run`, open `/swagger`, and walk the real flow: signup → login → Authorize with the returned JWT → create an employee → list → view details → activate/deactivate → check statistics reflect it → logout → retry the same old JWT on an authorized endpoint and confirm it now gets `401` (proves sign-out really revokes) → spot-check `ApiCallLogs` rows exist for the calls just made.