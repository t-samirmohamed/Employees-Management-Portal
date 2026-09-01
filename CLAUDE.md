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

**Composition root**: `Program.cs` wires DI, the middleware pipeline, and endpoint mapping only — no business logic lives there. Each feature area's endpoints are mapped via a `Map*Endpoints(this IEndpointRouteBuilder)` extension method (`AuthEndpoints`, `EmployeeEndpoints`, `StatisticsEndpoints`, `UserEndpoints`, `TaskEndpoints`), each building its own `app.MapGroup("/api/...")` with `.RequireAuthorization()` applied once at the group level; some routes layer an additional named-policy `.RequireAuthorization("...")` on top of that at the route level — `EmployeeEndpoints`'s activate/deactivate use `AdminOnly`; `TaskEndpoints`'s assign/reassign/cancel/complete use `TaskAssigner` (Admin/Manager/Supervisor), and accept/reject use `EmployeeOnly` — see Role-based access control below. `TaskEndpoints` additionally does **row-level** (ownership-based) authorization inside its handlers — `GET /api/tasks`, `GET /api/tasks/{id}`, and the comment endpoints check `User.IsInRole(...)`/resolve the caller's linked `Employee` directly, since a policy can only gate an entire route, not filter which rows a caller may see or touch. An Employee may only see/act on tasks where they are the assignee; Admin/Manager/Supervisor see every task — there is no reporting-hierarchy/team model in this codebase, so "Supervisor sees their team's tasks" is implemented as "sees all tasks" (a documented superset, not a bug).

**Auth is `AddIdentityCore`, not `AddIdentity`** — deliberately, since this is a pure bearer-token API with no browser sign-in surface; the full `AddIdentity` wires up cookie auth as a side effect (401s become redirects), which fights JWT-only auth. `.AddRoles<IdentityRole>()` is chained on top for role support (see below) — that only adds role-related DI services (`RoleManager<IdentityRole>`, role store wiring), not `AddIdentity`'s cookie behavior.

**Real server-side sign-out via security-stamp rotation**: JWTs carry a custom `securityStamp` claim captured at issue time (see `Services/TokenService.cs`). `JwtBearerEvents.OnTokenValidated` in `Program.cs` re-fetches the user's *current* stamp via `UserManager` on every authenticated request and fails auth if it doesn't match — this is the same mechanism cookie-based Identity auth uses internally (`SecurityStampValidator`), applied manually to bearer tokens. `POST /api/auth/logout` calls `UserManager.UpdateSecurityStampAsync`, which immediately invalidates *every* outstanding token for that user (not per-device revocation — a known v1 limitation). This trades some of JWT's normal statelessness for a DB read per request; accepted as fine at this scale.

**Signup is transactional across two tables**: `AuthEndpoints.SignupAsync` opens a `db.Database.BeginTransactionAsync()` before calling `UserManager.CreateAsync` (Identity writes through the same scoped `AppDbContext`, so it participates in the transaction) and then inserts a linked `Employee` row (`Employee.UserId` → the new user's Id) with `Status = Inactive`. Both writes commit or roll back together — an admin later activates the employee via `PATCH /api/employees/{id}/activate`. `POST /api/users` (Admin-only, `Endpoints/UserEndpoints.cs`) follows the identical transactional shape for admin-initiated user creation, deliberately duplicated rather than shared with `SignupAsync` since the two live in different endpoint files and the codebase has no cross-feature service-extraction convention yet.

**Role-based access control**: four fixed `IdentityRole`s (`Admin`, `Manager`, `Supervisor`, `Employee`, see `Models/Enums.cs`'s `Role` enum) are seeded idempotently on startup (`Program.cs`, right after `builder.Build()`), which also backfills any pre-existing user with no role to `Employee`. Every signup is auto-assigned `Employee`; `POST /api/users` can assign any of the four. The JWT carries the user's role(s) as `ClaimTypes.Role` claims (`Services/TokenService.cs`) — the framework's default role claim type, so `.RequireRole(...)`/`User.IsInRole(...)` need no extra `TokenValidationParameters` configuration. `Authorization/AdminAuthorizationHandler.cs` is a single `IAuthorizationHandler` that succeeds *any* pending authorization requirement for a user in the `Admin` role, which is what makes Admin override every other role check app-wide — not a rule repeated per endpoint. A user's role claims are only as fresh as their JWT: changing a role in the DB doesn't affect an already-issued token until it expires or the user logs back in (logout alone doesn't refresh it).

**API call logging happens in `Middleware/ApiLoggingMiddleware`, registered first in the pipeline** (before CORS/auth), so it wraps and times literally every request including ones that fail auth. Because middleware unwinds inside-out, by the time its `finally` block runs, inner auth middleware has already populated `context.User`, so the log row can include the user id even though logging middleware runs "before" auth. The `SaveChangesAsync` for the log write is caught and logged via `ILogger`, never rethrown — a logging failure must never turn a successful business response into a 500. This is a plain synchronous per-request insert (no queue/batching) — treat that as an intentional v1 choice, not an oversight, if you touch this file.

**Swagger uses the .NET 9/10 native OpenAPI generator, not classic Swashbuckle `AddSwaggerGen`**: `Microsoft.AspNetCore.OpenApi`'s `AddOpenApi()`/`MapOpenApi()` generates `/openapi/v1.json`; `Swashbuckle.AspNetCore.SwaggerUI` (UI-only package) renders it at `/swagger`. `OpenApi/BearerSecuritySchemeTransformer.cs` is a custom `IOpenApiDocumentTransformer` that adds the JWT bearer scheme to the generated document — this is what makes the UI's "Authorize" button work, since Swashbuckle's old `AddSecurityDefinition`/`IOperationFilter` model doesn't apply once Microsoft's generator owns the document.

**Data model** (`Data/AppDbContext.cs : IdentityDbContext<ApplicationUser>`): `ApplicationUser : IdentityUser` (Identity's stock user), `Employee` (links to `ApplicationUser` via nullable `UserId`; `Gender`/`Status`/`AttendanceStatus` enums), `ApiCallLog`, `TaskItem` (assigned work item, FK `AssigneeId → Employee`), `TaskComment` (FK `TaskId → TaskItem`, `AuthorEmployeeId → Employee`). Enum values matter and are serialized as strings (`JsonStringEnumConverter` registered in `Program.cs`): `Gender { M, F }`, `EmployeeStatus { Active, Inactive }`, `AttendanceStatus { InOffice, Absent, OnVacation, OutOfOffice }`, `TaskItemStatus { New, InProgress, Rejected, Cancelled, Done }`. `Role { Admin, Manager, Supervisor, Employee }` is serialized the same way in DTOs but is **not** a persisted column anywhere — `IdentityDbContext<ApplicationUser>` already carries the full `AspNetRoles`/`AspNetUserRoles` schema regardless, so `Role` only exists at the API boundary and is converted to/from Identity's string role names (`nameof(Role.Admin)` ⇄ `IdentityRole.Name`).

**Tasks are named `TaskItem`/`TaskItemStatus`, never `Task`/`TaskStatus`** — `System.Threading.Tasks` is a project-wide global using (see `EmpoloyeeManagment.GlobalUsings.g.cs`), so `Task`/`TaskStatus` would collide with the BCL types every `async Task` method in this codebase already depends on.

**Task lifecycle and attendance**: `TaskEndpoints` (`Endpoints/TaskEndpoints.cs`) computes `TaskItem.Status` entirely server-side — it is never accepted as direct client input. `POST /api/tasks` creates `New`; `accept`/`reject` (Employee-only, own task) move `New` → `InProgress`/`Rejected`; `cancel` (from `New`/`InProgress`) and `complete` (from `InProgress`, only when `RelatedVisitId is null`) are Admin/Manager/Supervisor-only additions beyond the literal spec text, added because the status enum requires `Cancelled`/`Done` to be reachable states. Reassigning (`PATCH /api/tasks/{id}`) resets `Status` to `New`. `Services/IEmployeeAttendanceService` recalculates `Employee.AttendanceStatus` (`InOffice`⇄`OutOfOffice`) whenever an `AttendanceRequired` task starts/stops being `InProgress` for its assignee, but only while the employee's current status is already `InOffice`/`OutOfOffice` (an `Absent`/`OnVacation` status set by some other process is left untouched) — built as a shared service specifically so a future visits feature can extend the same recalculation instead of duplicating it. `TaskItem.RelatedVisitId` is a bare nullable `int` with no FK constraint and no existence validation — there is no `Visits` table yet.

**CORS**: allowed origins are config-driven (`Cors:AllowedOrigins` in `appsettings.*.json`, currently `http://localhost:3000` for a Next.js client) and applied via a named `"NextJsClient"` policy with `AllowAnyHeader().AllowAnyMethod()` but no `AllowCredentials()` — auth is a bearer token in the `Authorization` header, not cookies, so credentialed CORS isn't needed.

**Secrets**: the JWT signing key in `appsettings.Development.json` is a locally-generated dev-only value checked in for convenience — move it to user-secrets or an environment variable before any deployment or before this leaves local dev.

## Project layout

```
EmpoloyeeManagment/
  Program.cs                          Composition root: DI, middleware pipeline, endpoint mapping
  Data/AppDbContext.cs                EF Core DbContext (Identity + Employees + ApiCallLogs + Tasks + TaskComments)
  Models/                             Employee, ApplicationUser, ApiCallLog, TaskItem, TaskComment, enums (including Role, TaskItemStatus)
  Dtos/                               Request/response records, grouped by feature (Auth, Employees, Statistics, Users, Tasks)
  Endpoints/                          One static class per feature area (including UserEndpoints, TaskEndpoints)
  Authorization/AdminAuthorizationHandler.cs   Single IAuthorizationHandler that makes Admin bypass every role check
  Services/                           ITokenService / TokenService — JWT issuance; IEmployeeAttendanceService / EmployeeAttendanceService — task-driven attendance
  Middleware/ApiLoggingMiddleware.cs  Logs every request to ApiCallLogs
  OpenApi/BearerSecuritySchemeTransformer.cs   Adds the JWT bearer scheme to the generated OpenAPI doc
  Migrations/                         EF Core migrations
  docs/Project_Startup_Plan.md        Original design rationale — read this for the "why" behind the above
```

## Known v1 gaps (not oversights — see README roadmap)

Employee update/delete and per-device token revocation (logout currently revokes all of a user's sessions at once) are deliberately out of scope for now. Role-based access control shipped (four fixed roles, admin user management via `/api/users`); changing an existing user's role after creation is still out of scope. Task assignment, accept/reject, cancel/complete, and comments shipped (`/api/tasks`); task attachments, real reporting-hierarchy/team scoping for Supervisors (they see every task, not just "their team's" — no team model exists), and syncing task status from a linked visit are still out of scope.
