# Employee Management API

A .NET 10 minimal API backend for managing employees: authentication, employee records, activity statistics, a live Swagger UI, and a SQL Server database that also logs every API call made against it.

## Features

- **Auth** — signup, login, and a real sign-out (JWT bearer tokens; logout rotates the user's Identity security stamp, which immediately invalidates every token issued to that user, not just a client-side token drop). Email and username must be unique. Signing up creates both the login account *and* its linked employee record — as `Inactive` — in one atomic transaction; an admin activates them via the employee endpoints below.
- **Roles** — four fixed roles (`Admin`, `Manager`, `Supervisor`, `Employee`), seeded on startup. Every signup is assigned `Employee`; an Admin can create a user with any role. Admin overrides every role check app-wide. The JWT carries the caller's role as a claim.
- **Employees** — list (with optional gender/status/attendance filters), view details, create; activate/deactivate is Admin-only.
- **Users** — Admin-only: list every system user with role + linked employee summary, or create a new one with any role.
- **Statistics** — employee counts grouped by gender (M/F), status (Active/Inactive), and attendance status (In Office/Absent/On Vacation/Out of Office).
- **Tasks** — Admin/Manager/Supervisor assign and reassign tasks to employees; the assignee accepts or rejects (with a reason); a server-computed status (`New`/`InProgress`/`Rejected`/`Cancelled`/`Done`) tracks the lifecycle. An `AttendanceRequired` task that's `InProgress` automatically flips its assignee's attendance status to Out of Office. Comments can be added, updated, and deleted on a task by their author (or an Admin).
- **Clients & locations** — Admin/Manager list every client with its locations; Supervisor sees only the client that owns their assigned location. Admin/Manager create a client with zero or more nested locations in one call. Per-client visit counts for the last month/3 months/6 months/year.
- **Visits** — a `Visit` has a `DateTime`, `ClientId`, and `LocationId` (FK-constrained to `Client`/`Location`), with no independent status: it always reflects the `Status` of the `TaskItem` it drives. Admin/Manager/Supervisor create a visit (`POST /api/visits`), which atomically creates the `Visit` and its driving `TaskItem` (always `AttendanceRequired`); creating or accepting a visit is blocked if its assignee already has another visit-linked task that's `InProgress`. A task can optionally link to a `Visit` (`RelatedVisitId`); accepting or completing a visit-linked task enforces the same rules.
- **Dashboards & stats** — per-employee monthly activity rollups (tasks/visits by status, current attendance snapshot), company-wide task/visit status counts, a most-visited-clients ranking, and a per-supervisor team rollup (roster, open tasks, attendance breakdown); Admin/Manager see everything, a Supervisor is scoped to a "team" heuristically derived from who's been visited-assigned to their client (no real reporting-hierarchy model exists yet).
- **Leave requests** — Employees, Managers, and Supervisors submit a leave request (date range + reason); the original requester can edit or delete it while it's still `Pending`. Approval routing is role-based: an Employee's request can be actioned by a Supervisor, Manager, or Admin; a Supervisor's by a Manager or Admin; a Manager's by an Admin only (a caller can never action their own request or a same-rank peer's). Actions are accept, reject (reason required), or request a delay to a specific date (reason required); notification delivery on submission/decision is a separate feature (see below) not yet wired to this one.
- **Notifications** — a `Notification` per recipient (`RecipientUserId`, `Type`, `ReferenceId`, read/unread, timestamp); `GET /api/notifications` lists the caller's own (newest first), `PATCH /api/notifications/{id}/read` marks one read. Six triggers are wired: leave-request submitted (every employee whose role outranks the requester), accepted/rejected/delay-requested (the requester), and task/visit assignment (the assignee).
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
  Data/AppDbContext.cs                EF Core DbContext (Identity + Employees + ApiCallLogs + Tasks + TaskComments + Visits + Clients + Locations + Notifications + LeaveRequests)
  Models/                             Employee, ApplicationUser, ApiCallLog, TaskItem, TaskComment, Visit, Client, Location, Notification, LeaveRequest, enums (Gender, EmployeeStatus, AttendanceStatus, Role, TaskItemStatus, NotificationType, LeaveRequestStatus)
  Dtos/                               Request/response records, grouped by feature (Auth, Employees, Statistics, Users, Tasks, Visits, Clients, Notifications, Leaves)
  Endpoints/                          One static class per feature area (AuthEndpoints, EmployeeEndpoints, StatisticsEndpoints, UserEndpoints, TaskEndpoints, VisitEndpoints, ClientEndpoints, NotificationEndpoints, LeaveEndpoints)
  Authorization/AdminAuthorizationHandler.cs   Makes the Admin role bypass every role-based authorization check
  Services/                          ITokenService / TokenService — JWT issuance; IEmployeeAttendanceService / EmployeeAttendanceService — task-driven attendance recalculation; INotificationService / NotificationService — leave/task/visit notification creation
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

### Setting up the database on another machine / an external server

Two ways to create the schema on a SQL Server that isn't your local dev instance, depending on what's available on the target:

**A. The target machine (or your own machine, pointed at a remote connection string) has the .NET SDK + `dotnet-ef`:**
```
dotnet ef database update --connection "Server=<host>;Database=EmployeeManagementDb;User Id=<user>;Password=<password>;TrustServerCertificate=True;"
```
This applies all migrations directly — always in sync with the codebase, nothing to regenerate.

**B. The target is a bare SQL Server with no .NET tooling** (a DBA-run deploy, a managed SQL instance, CI/CD that just runs `.sql` files, etc.): run the pre-generated script at [`EmpoloyeeManagment/docs/database-setup.sql`](EmpoloyeeManagment/docs/database-setup.sql) against it — via SSMS ("Open File" → Execute), Azure Data Studio, or:
```
sqlcmd -S <host> -d EmployeeManagementDb -U <user> -P <password> -i EmpoloyeeManagment/docs/database-setup.sql
```
(Add `-C` if the server uses a self-signed/dev certificate.) The script is **idempotent** — generated with `dotnet ef migrations script --idempotent`, it checks `__EFMigrationsHistory` before each change, so re-running it (e.g. against a DB that already has some migrations applied) only applies what's missing. Target database must already exist (`CREATE DATABASE EmployeeManagementDb;` first) — the script creates tables, not the database itself.

**Whichever way you create the schema**, remember:
- The four roles (`Admin`/`Manager`/`Supervisor`/`Employee`) are **not** in the script — they're seeded in code at app startup (`Program.cs`), so they appear automatically the first time the API runs against the new database.
- **Regenerate the script whenever you add a migration** — it's a snapshot, not a live query: `dotnet ef migrations script --idempotent --output EmpoloyeeManagment/docs/database-setup.sql` (overwrites the file; re-run from the repo root).
- Point the app at the new database by updating `ConnectionStrings:DefaultConnection` for whichever environment you're deploying (`appsettings.Production.json`, an environment variable, or user-secrets — not by editing `appsettings.Development.json`).

## API overview

| Method | Route | Auth | Description |
|---|---|---|---|
| POST | `/api/auth/signup` | — | Create an account (role `Employee`) + linked (Inactive) employee record, returns a JWT |
| POST | `/api/auth/login` | — | Returns a JWT |
| POST | `/api/auth/logout` | ✔ | Revokes all of the user's outstanding tokens |
| GET | `/api/employees` | ✔ | List employees (optional `gender`, `status`, `attendanceStatus` filters) |
| GET | `/api/employees/{id}` | ✔ | Employee details |
| POST | `/api/employees` | ✔ | Create an employee |
| PATCH | `/api/employees/{id}/activate` | Admin | Set status to Active |
| PATCH | `/api/employees/{id}/deactivate` | Admin | Set status to Inactive |
| GET | `/api/statistics/employees` | ✔ | Counts by gender, status, and attendance status |
| GET | `/api/users` | Admin | List every system user with role + linked employee summary |
| POST | `/api/users` | Admin | Create a user + linked (Inactive) employee record with any role |
| GET | `/api/tasks` | ✔ | List tasks (Employee: own only; Admin/Manager/Supervisor: all) |
| GET | `/api/tasks/{id}` | ✔ | Task details with comments (same visibility rule) |
| POST | `/api/tasks` | Admin/Manager/Supervisor | Assign a new task |
| PATCH | `/api/tasks/{id}` | Admin/Manager/Supervisor | Reassign to a different employee, resets status to New |
| PATCH | `/api/tasks/{id}/accept` | Employee (own task) | Accept an assigned task |
| PATCH | `/api/tasks/{id}/reject` | Employee (own task) | Reject an assigned task, reason required |
| PATCH | `/api/tasks/{id}/cancel` | Admin/Manager/Supervisor | Cancel a task |
| PATCH | `/api/tasks/{id}/complete` | Admin/Manager/Supervisor | Mark a task done (only if it has no linked visit) |
| POST | `/api/tasks/{id}/comments` | ✔ | Add a comment (same visibility rule as task detail) |
| PATCH | `/api/tasks/{id}/comments/{commentId}` | ✔ | Update a comment (author or Admin) |
| DELETE | `/api/tasks/{id}/comments/{commentId}` | ✔ | Delete a comment (author or Admin) |
| GET | `/api/visits` | ✔ | List visits (Employee: own only; Admin/Manager/Supervisor: all) |
| GET | `/api/visits/{id}` | ✔ | Visit details, `Status` mirrors its linked task (same visibility rule) |
| POST | `/api/visits` | Admin/Manager/Supervisor | Create a visit + its driving task in one transaction |
| GET | `/api/clients` | Admin/Manager/Supervisor | List clients (Admin/Manager: all; Supervisor: only the client owning their assigned location) |
| GET | `/api/clients/{id}` | Admin/Manager/Supervisor | Client details with its locations (same visibility rule) |
| POST | `/api/clients` | Admin/Manager | Create a client with zero or more nested locations |
| GET | `/api/clients/{id}/visit-stats` | Admin/Manager/Supervisor | Visit count for the client (`range=month\|3month\|6month\|year`, same visibility rule) |
| GET | `/api/statistics/employees/{id}/monthly` | Admin/Manager/Supervisor | Per-employee monthly rollup: tasks/visits by status, current attendance snapshot (`year`, `month` required; Supervisor: team only) |
| GET | `/api/statistics/status` | Admin/Manager/Supervisor | Task and visit counts by status (Supervisor: team only) |
| GET | `/api/statistics/clients/most-visited` | Admin/Manager | Clients ranked by visit count (`range=month\|3month\|6month\|year`, optional `top`, default 10) |
| GET | `/api/statistics/supervisors` | Admin/Manager | Every supervisor with their team roster, open task count, and team attendance breakdown |
| GET | `/api/notifications` | ✔ | List the caller's own notifications, newest first |
| PATCH | `/api/notifications/{id}/read` | ✔ | Mark one of the caller's own notifications read |
| POST | `/api/leaves` | Employee/Manager/Supervisor | Submit a leave request (`Pending`) |
| GET | `/api/leaves` | ✔ | List leave requests (Employee: own only; Admin/Manager/Supervisor: all) |
| GET | `/api/leaves/{id}` | ✔ | Leave request details (same visibility rule) |
| PATCH | `/api/leaves/{id}` | Original requester only | Edit dates/reason, only while `Pending` |
| DELETE | `/api/leaves/{id}` | Original requester only | Delete, only while `Pending` |
| PATCH | `/api/leaves/{id}/accept` | Admin/Manager/Supervisor (rank > requester's) | Accept a pending leave request |
| PATCH | `/api/leaves/{id}/reject` | Admin/Manager/Supervisor (rank > requester's) | Reject, reason required |
| PATCH | `/api/leaves/{id}/request-delay` | Admin/Manager/Supervisor (rank > requester's) | Request a delay to a target date, reason required |

## Roadmap / not yet built

This is v1 — deliberately structured to extend cleanly. Role-based access control has shipped: four fixed roles (`Admin`/`Manager`/`Supervisor`/`Employee`), Admin overrides every role check, and Admin-only user management (`/api/users`). Task assignment, accept/reject, cancel/complete, and comments have shipped (`/api/tasks`), including a real FK from `RelatedVisitId` to `Visits` and a visit-aware concurrency check on accept. Visits have fully shipped: `GET /api/visits`, `GET /api/visits/{id}`, and `POST /api/visits` (creates the `Visit` and its driving task transactionally, enforcing the client/location/assignee checks and the accepted-visit concurrency rule), built on top of the `clients` feature's `Client`/`Location` entities (`GET`/`POST /api/clients`, per-client visit-count stats, and a Supervisor's `AssignedLocationId` on `Employee`). Known gaps still not in scope: employee update/delete, changing an existing user's role after creation, per-device token revocation (current sign-out revokes all of a user's sessions at once), task attachments, real reporting-hierarchy/team scoping for Supervisors (they currently see every task/visit, not just "their team's" — no team model exists yet), reassigning a visit-linked task to a new employee without re-checking the visit concurrency rule, editing/deleting a client or location (or adding a location to an already-created client), and changing a Supervisor's `AssignedLocationId` after user creation. Dashboards & stats have shipped for tasks, visits, and current attendance (including the most-visited-clients ranking and the per-supervisor team rollup); the per-employee monthly rollup still omits "Leaves" — `leaves` has since shipped, but integrating its data into the rollup is a deferred, non-breaking follow-up, not part of the `leaves` story itself — no attendance *history* table exists (attendance is reported as a current snapshot only), and a Supervisor's "team" for dashboards purposes is a heuristic derived from visit assignments under their assigned client, not a real reporting hierarchy. Notifications have shipped in full: storage, self-service reading (`Notification` entity, `GET /api/notifications`, `PATCH /api/notifications/{id}/read`), and every trigger (`NotificationType` has six members) — leave-request submitted/accepted/rejected/delay-requested, plus task and visit assignment — all wired into their respective endpoints (leave requests have shipped in full: submit/edit/delete while `Pending`, and role-ranked accept/reject/request-delay routing — Employee → Supervisor/Manager/Admin; Supervisor → Manager/Admin; Manager → Admin). No per-notification-type preferences or real-time push exist (still client-side polling) — not asked for.
