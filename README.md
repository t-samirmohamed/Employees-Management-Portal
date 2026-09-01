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
  Data/AppDbContext.cs                EF Core DbContext (Identity + Employees + ApiCallLogs + Tasks + TaskComments + Visits + Clients + Locations)
  Models/                             Employee, ApplicationUser, ApiCallLog, TaskItem, TaskComment, Visit, Client, Location, enums (Gender, EmployeeStatus, AttendanceStatus, Role, TaskItemStatus)
  Dtos/                               Request/response records, grouped by feature (Auth, Employees, Statistics, Users, Tasks, Visits, Clients)
  Endpoints/                          One static class per feature area (AuthEndpoints, EmployeeEndpoints, StatisticsEndpoints, UserEndpoints, TaskEndpoints, VisitEndpoints, ClientEndpoints)
  Authorization/AdminAuthorizationHandler.cs   Makes the Admin role bypass every role-based authorization check
  Services/                          ITokenService / TokenService — JWT issuance; IEmployeeAttendanceService / EmployeeAttendanceService — task-driven attendance recalculation
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

## Roadmap / not yet built

This is v1 — deliberately structured to extend cleanly. Role-based access control has shipped: four fixed roles (`Admin`/`Manager`/`Supervisor`/`Employee`), Admin overrides every role check, and Admin-only user management (`/api/users`). Task assignment, accept/reject, cancel/complete, and comments have shipped (`/api/tasks`), including a real FK from `RelatedVisitId` to `Visits` and a visit-aware concurrency check on accept. Visits have fully shipped: `GET /api/visits`, `GET /api/visits/{id}`, and `POST /api/visits` (creates the `Visit` and its driving task transactionally, enforcing the client/location/assignee checks and the accepted-visit concurrency rule), built on top of the `clients` feature's `Client`/`Location` entities (`GET`/`POST /api/clients`, per-client visit-count stats, and a Supervisor's `AssignedLocationId` on `Employee`). Known gaps still not in scope: employee update/delete, changing an existing user's role after creation, per-device token revocation (current sign-out revokes all of a user's sessions at once), task attachments, real reporting-hierarchy/team scoping for Supervisors (they currently see every task/visit, not just "their team's" — no team model exists yet), reassigning a visit-linked task to a new employee without re-checking the visit concurrency rule, editing/deleting a client or location (or adding a location to an already-created client), changing a Supervisor's `AssignedLocationId` after user creation, and a "most visited clients" ranking (planned for a future `dashboards` feature).
