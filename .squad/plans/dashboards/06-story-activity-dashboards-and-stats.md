# Story 06 — Activity Dashboards & Stats

## Prerequisites

- `clients` Story 05 completed: [`../clients/05-story-client-location-management.md`](../clients/05-story-client-location-management.md) — verified already implemented (`Models/Client.cs`, `Models/Location.cs`, `Endpoints/ClientEndpoints.cs` all exist and match the story). This story's "most visited clients" ranking is explicitly called out as belonging here, not to `clients`, in [`../clients/00-overview.md`](../clients/00-overview.md) line 19.
- `tasks` Story 02 completed: [`../tasks/02-story-task-assignment-and-comments.md`](../tasks/02-story-task-assignment-and-comments.md) — verified already implemented (`Models/TaskItem.cs`, `Endpoints/TaskEndpoints.cs`).
- `visits` Story 04 completed: [`../visits/04-story-visit-tracking-and-status.md`](../visits/04-story-visit-tracking-and-status.md) — verified already implemented (`Models/Visit.cs`, `Endpoints/VisitEndpoints.cs`).
- `roles` Story 01 completed: [`../roles/01-story-role-model-and-admin-user-management.md`](../roles/01-story-role-model-and-admin-user-management.md) — verified already implemented (`Program.cs` lines 76–82: `AdminOnly`, `TaskAssigner`, `EmployeeOnly`, `ClientManager` policies; `Models/Enums.cs`'s `Role` enum).
- **`leaves` is only partially satisfied — this story ships around it, not on top of it.** [`../leaves/00-overview.md`](../leaves/00-overview.md) is still the empty stub ("_add rows as stories are planned_") — confirmed by a full `EmpoloyeeManagment/**/*.cs` listing showing no `Leave`/`LeaveRequest` model, no `Endpoints/LeaveEndpoints.cs`, and no `Dtos/Leaves/` anywhere in the codebase. Its intake ([`../../stories/leaves/leave-requests-approval-workflow/intake.md`](../../stories/leaves/leave-requests-approval-workflow/intake.md) lines 72–85) only *drafts* a `LeaveRequest` shape — it has not been turned into a `NN-story-*.md`, let alone implemented. This story's intake asks for "leaves" as one of four data sources in the per-employee monthly rollup, and for "Attendance/Attendance State - Leaves" in the Supervisor's employee review. Since there is no `LeaveRequest` entity or shape to write real code against yet (writing DTOs/queries against an unbuilt, unstoried entity would violate this plan's own anti-hallucination rules), **this story ships the other three data sources now (tasks, visits, current attendance snapshot) and explicitly leaves the "Leaves" column out**, documented as a follow-up in Edge Cases & Failure Modes and Done Criteria below. `Services/EmployeeAttendanceService.cs` line 14's own comment — "Absent/OnVacation (set by a future leaves feature) takes precedence over task-driven attendance" — is the codebase's own acknowledgment that `leaves` lands after this kind of work, not before it.
- **No reporting-hierarchy/team model exists on `Employee`.** Confirmed two ways: (1) `Models/Employee.cs` (read in full, 15 lines) has no `SupervisorId`/`TeamId`/`ManagerId` field — its only location-ish field is `AssignedLocationId`, described in `CLAUDE.md` line 40 as "only meaningful for a `Supervisor`-role user"; (2) [`../tasks/00-overview.md`](../tasks/00-overview.md) line 20 already states this explicitly in writing: "Neither story implements real Supervisor/team scoping (no `SupervisorId`/`TeamId` field exists anywhere on `Employee` today)." This story's Admin/Manager "supervisors and their teams" requirement cannot be built as a true reporting hierarchy — see Story Goal and Edge Cases for the heuristic used instead.

---

## Story Goal

Extend the existing `GET /api/statistics/employees` group with four new read-only, aggregate-only endpoints (no new write surface anywhere — matches the intake's "Out of scope" note verbatim), covering the acceptance criteria that are buildable against the current schema:

1. **`GET /api/statistics/employees/{id}/monthly`** — per-employee monthly rollup (tasks by status, visits by status, current attendance snapshot) for a given `year`/`month`. Admin/Manager: any employee. Supervisor: only employees on their "team" (see point 5). Employee role: no access (the intake's "Admins/Managers"/"Supervisors" sections never mention Employees consuming this feature).
2. **`GET /api/statistics/status`** — task and visit counts by status ("Status/visits stats" in both the Admins/Managers and Supervisors sections of the intake). Admin/Manager: company-wide. Supervisor: their team only.
3. **`GET /api/statistics/clients/most-visited`** — clients ranked by visit count over a trailing range, reusing the `range` convention `ClientEndpoints.GetClientVisitStatsAsync` already established. Admin/Manager only (the intake lists "Client visits stats and most visited clients" only under "Admins/Managers", never under "Supervisors"). This is the ranking `../clients/00-overview.md` line 19 explicitly deferred to this feature; the underlying per-client "Client visits stats" endpoint (`GET /api/clients/{id}/visit-stats`) already exists and needs no changes.
4. **`GET /api/statistics/supervisors`** — every Supervisor with their team roster, open task count, and team attendance-status breakdown ("view all supervisors and their teams and stats ... categorized per team/supervisor"). Admin/Manager only.
5. **The "team" heuristic.** Since no `SupervisorId`/`TeamId` field exists (Prerequisites), "team" is defined **only for this story** as: the distinct set of Employees who are the assignee of at least one visit-linked `TaskItem` whose `Visit.ClientId` equals the Supervisor's own `AssignedLocationId → Location.ClientId` — i.e., the same client `ClientEndpoints.GetSupervisorAssignedClientIdAsync` (lines 134–150) already scopes a Supervisor's client visibility to, extended one hop further to "who has visited that client's locations." This is a heuristic over existing data, not a real reporting hierarchy — see Edge Cases for its known false-negatives/false-positives.

**Not in scope for this story** (do not build these here):
- The "Leaves" column of the per-employee monthly rollup and of the Supervisor's employee-review criteria — blocked on the `leaves` feature (Prerequisites).
- Any new `LeaveRequest`-adjacent entity, table, or migration.
- An `Employee → Supervisor`/`Employee → Team` schema change (e.g. adding a `SupervisorId` column). The heuristic in point 5 deliberately reuses existing tables instead.
- An attendance *history* table. `Employee.AttendanceStatus` (`Models/Employee.cs` line 12) is a single current-value column with no audit log anywhere in `Data/AppDbContext.cs` — the monthly rollup reports the **current** snapshot plus a derived proxy (task 2's `AttendanceDrivingTaskCount`), not true "attendance on date X" history. See Edge Cases.
- Any change to `Endpoints/ClientEndpoints.cs`, `Endpoints/TaskEndpoints.cs`, `Endpoints/VisitEndpoints.cs`, or their underlying entities — this story only reads from them.
- A new authorization policy — `TaskAssigner` and `ClientManager` (`Program.cs` lines 78–81) already express exactly the two role combinations this story needs.

---

## Context — Read These Files First

1. [`EmpoloyeeManagment/Endpoints/StatisticsEndpoints.cs`](../../../EmpoloyeeManagment/Endpoints/StatisticsEndpoints.cs) — whole file (40 lines). This is the file the intake says to extend. `GetEmployeeStatisticsAsync` (lines 19–39) is the exact `GroupBy(...).Select(x => new { x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Key.ToString(), x => x.Count)` pattern (lines 23–26, 28–31, 33–36) every new query in task 2 reuses.
2. [`EmpoloyeeManagment/Dtos/Statistics/StatisticsDtos.cs`](../../../EmpoloyeeManagment/Dtos/Statistics/StatisticsDtos.cs) — whole file (7 lines), the one-record-per-shape convention task 1 extends.
3. [`EmpoloyeeManagment/Models/Employee.cs`](../../../EmpoloyeeManagment/Models/Employee.cs) — whole file (15 lines). Confirms no `SupervisorId`/`TeamId` field — the fact behind the "team" heuristic (Story Goal point 5).
4. [`EmpoloyeeManagment/Models/TaskItem.cs`](../../../EmpoloyeeManagment/Models/TaskItem.cs) (15 lines), [`Models/Visit.cs`](../../../EmpoloyeeManagment/Models/Visit.cs) (10 lines), [`Models/Client.cs`](../../../EmpoloyeeManagment/Models/Client.cs) (10 lines), [`Models/Location.cs`](../../../EmpoloyeeManagment/Models/Location.cs) (11 lines), [`Models/Enums.cs`](../../../EmpoloyeeManagment/Models/Enums.cs) (38 lines) — whole files, the exact field names/types (`AssigneeId`, `DueDateTime`, `RelatedVisitId`, `Visit.DateTime`/`ClientId`/`LocationId`, `TaskItemStatus`, `AttendanceStatus`) every new query in task 2 references.
5. [`EmpoloyeeManagment/Data/AppDbContext.cs`](../../../EmpoloyeeManagment/Data/AppDbContext.cs) — whole file (125 lines). `DbSet` declarations (lines 9–15) confirm the exact `db.Tasks`/`db.Visits`/`db.Clients`/`db.Locations`/`db.Employees` names task 2 queries. Lines 59–64 and line 36 confirm `TaskItem.Status` and `Employee.AttendanceStatus` are `HasConversion<string>()` columns, the same shape the existing (and proven-working) `GetEmployeeStatisticsAsync` already groups by.
6. [`EmpoloyeeManagment/Endpoints/ClientEndpoints.cs`](../../../EmpoloyeeManagment/Endpoints/ClientEndpoints.cs) — whole file (151 lines). `GetSupervisorAssignedClientIdAsync` (lines 134–150) is the exact Supervisor-scoping helper task 2 duplicates verbatim (per the codebase's documented no-cross-feature-service convention — see item 15 below). `GetRangeCutoff` (lines 115–122) is duplicated verbatim for the most-visited-clients endpoint. `HasFullClientAccess` (lines 124–125, `Admin || Manager`) is the exact shape `HasFullEmployeeStatsAccess` (task 2) matches.
7. [`EmpoloyeeManagment/Endpoints/TaskEndpoints.cs`](../../../EmpoloyeeManagment/Endpoints/TaskEndpoints.cs) — whole file (312 lines). `IsPrivileged`/`GetCallerEmployeeAsync`/`CanViewTaskAsync` (lines 287–301) are the row-level-scoping style task 2 follows. `CancelTaskAsync`'s status guard (line 190: `task.Status is not (TaskItemStatus.New or TaskItemStatus.InProgress)`) is this codebase's own definition of "still open" — reused for this story's `OpenTaskCount` (task 2).
8. [`EmpoloyeeManagment/Endpoints/VisitEndpoints.cs`](../../../EmpoloyeeManagment/Endpoints/VisitEndpoints.cs) — whole file (127 lines). `GetVisitsAsync`'s `.Join(db.Tasks.AsNoTracking(), v => v.Id, t => t.RelatedVisitId, (v, t) => ...)` (lines 25–26) is the exact Visit↔Task join pattern task 2 reuses repeatedly — already proven to translate correctly in this codebase. Line 98 (`DueDateTime = request.DateTime` inside `CreateVisitAsync`) confirms a visit-linked task's `DueDateTime` always equals its `Visit.DateTime` by construction — why task 2 can bucket a visit-linked task into a month using either field.
9. [`EmpoloyeeManagment/Endpoints/UserEndpoints.cs`](../../../EmpoloyeeManagment/Endpoints/UserEndpoints.cs) — whole file (95 lines). `GetUsersAsync` (lines 24–38), specifically `db.Roles.Where(r => db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == r.Id))`, is the exact `db.Roles`/`db.UserRoles` join this story's `GetSupervisorTeamStatisticsAsync` (task 2) reuses to find every Employee in the `Supervisor` role.
10. [`EmpoloyeeManagment/Program.cs`](../../../EmpoloyeeManagment/Program.cs) — whole file (158 lines). Policies at lines 78–81 (`TaskAssigner`, `ClientManager`) are reused as-is — **no new policy, no `Program.cs` change at all** (task 3): line 152 (`app.MapStatisticsEndpoints();`) already wires up the file this story extends.
11. [`EmpoloyeeManagment/Services/EmployeeAttendanceService.cs`](../../../EmpoloyeeManagment/Services/EmployeeAttendanceService.cs) — whole file (27 lines). Line 14's comment ("Absent/OnVacation (set by a future leaves feature) takes precedence over task-driven attendance") is the codebase's own acknowledgment that `Employee.AttendanceStatus` is a single current-value column with no history anywhere — the fact behind this story's "no attendance history" gap (Edge Cases).
12. [`../tasks/00-overview.md`](../tasks/00-overview.md) line 20 — already-written, citable confirmation that no team field exists and that "sees all" is this codebase's existing fallback where a team concept can't be scoped.
13. [`../clients/00-overview.md`](../clients/00-overview.md) line 19 — already-written, citable confirmation that "most visited clients" is this feature's job.
14. [`../leaves/00-overview.md`](../leaves/00-overview.md) (14 lines, still the stub) and [`../../stories/leaves/leave-requests-approval-workflow/intake.md`](../../stories/leaves/leave-requests-approval-workflow/intake.md) lines 72–85 — confirms `leaves` has no entity/endpoints/story yet, only a drafted-not-implemented `LeaveRequest` shape in its intake. The reason this story defers the "Leaves" column (Story Goal, Edge Cases).
15. [`../visits/04-story-visit-tracking-and-status.md`](../visits/04-story-visit-tracking-and-status.md) — read in full as this plan's structural/tonal precedent (the "find a similar precedent" step), and specifically as this codebase's precedent for how to document a partially-blocked story (its Prerequisites' `clients` hard-blocker, and its Context item 6 / this file's own CLAUDE.md line 32 citation for "duplicate small per-endpoint-file helpers instead of extracting a shared service").
16. [`README.md`](../../../README.md) — whole file (97 lines). Features list (lines 11, 13), API table (lines 63–93), Roadmap paragraph (line 97) — all updated by task 4.
17. [`CLAUDE.md`](../../../CLAUDE.md) lines 26 and 71 — the Architecture row-level-authorization paragraph and the Known v1 gaps paragraph — both updated by task 4.
18. [`EmpoloyeeManagment/Dtos/Employees/EmployeeDtos.cs`](../../../EmpoloyeeManagment/Dtos/Employees/EmployeeDtos.cs), [`Dtos/Tasks/TaskDtos.cs`](../../../EmpoloyeeManagment/Dtos/Tasks/TaskDtos.cs), [`Dtos/Clients/ClientDtos.cs`](../../../EmpoloyeeManagment/Dtos/Clients/ClientDtos.cs) — confirm the `FirstName`/`LastName`-separate (never a combined `Name`) and IDs-not-nested-objects DTO conventions task 1 follows (e.g. `TaskListItemDto.AssigneeId` is a bare `int`, never a nested `Employee`).

---

## Product rules (from story)

| Area | Current behavior | New behavior |
|---|---|---|
| `GET /api/statistics/employees/{id}/monthly` | Doesn't exist | New — tasks/visits by status for one employee in a given month, plus current attendance snapshot. Admin/Manager: any employee. Supervisor: team-only (404 if the employee isn't on their team). "Leaves" intentionally omitted — see Prerequisites. |
| `GET /api/statistics/status` | Doesn't exist | New — all-time task and visit counts by status. Admin/Manager: company-wide. Supervisor: team-only. |
| `GET /api/statistics/clients/most-visited` | Doesn't exist | New — clients ranked by visit count over `range` (`month`\|`3month`\|`6month`\|`year`, reusing `ClientEndpoints`'s convention), optional `top` (default 10, clamped 1–100). Admin/Manager only. |
| `GET /api/statistics/supervisors` | Doesn't exist | New — every Employee in the `Supervisor` role, their assigned client (if any), team roster, open task count, and team attendance-status breakdown. Admin/Manager only. |
| "Team" (Supervisor scoping) | No concept anywhere in the codebase | New heuristic, **this story only**: Employees who are the assignee of a visit-linked task whose `Visit.ClientId` matches the Supervisor's own `AssignedLocationId → Location.ClientId`. Not a real reporting hierarchy — see Edge Cases. |

---

## Backend Tasks

`No frontend changes required in this repository` — the companion UI work is tracked in the separate `employee-management-web` repo per the intake's "Extra notes."

### 1 — Extend the Statistics DTOs

**File: `EmpoloyeeManagment/Dtos/Statistics/StatisticsDtos.cs`**

Replace the whole file with:

```csharp
namespace EmpoloyeeManagment.Dtos.Statistics;

public record EmployeeStatisticsDto(
    int TotalEmployees,
    Dictionary<string, int> ByGender,
    Dictionary<string, int> ByStatus,
    Dictionary<string, int> ByAttendanceStatus);

public record EmployeeMonthlyActivityDto(
    int EmployeeId,
    int Year,
    int Month,
    int TotalTasks,
    Dictionary<string, int> TasksByStatus,
    int TotalVisits,
    Dictionary<string, int> VisitsByStatus,
    string CurrentAttendanceStatus,
    int AttendanceDrivingTaskCount);

public record StatusStatsDto(
    Dictionary<string, int> TasksByStatus,
    Dictionary<string, int> VisitsByStatus);

public record MostVisitedClientDto(int ClientId, string ClientName, int VisitCount);

public record SupervisorTeamStatsDto(
    int SupervisorEmployeeId,
    string SupervisorName,
    int? AssignedClientId,
    string? AssignedClientName,
    List<int> TeamEmployeeIds,
    int OpenTaskCount,
    Dictionary<string, int> TeamAttendanceStatusBreakdown);
```

`EmployeeStatisticsDto` is unchanged — reproduced verbatim so the file stays a complete, drop-in replacement. `AttendanceDrivingTaskCount` on `EmployeeMonthlyActivityDto` is the derived proxy described in Story Goal ("not in scope") and Edge Cases, standing in for true attendance history. `SupervisorTeamStatsDto.TeamEmployeeIds` is a bare `int` list (not nested `EmployeeListItemDto` objects) — matching `TaskListItemDto.AssigneeId`'s existing bare-id convention (Context item 18); the frontend can resolve names via the existing `GET /api/employees/{id}`.

### 2 — Extend `Endpoints/StatisticsEndpoints.cs`

**File: `EmpoloyeeManagment/Endpoints/StatisticsEndpoints.cs`**

Replace the whole file with:

```csharp
using System.Security.Claims;
using EmpoloyeeManagment.Data;
using EmpoloyeeManagment.Dtos.Statistics;
using EmpoloyeeManagment.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace EmpoloyeeManagment.Endpoints;

public static class StatisticsEndpoints
{
    public static IEndpointRouteBuilder MapStatisticsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/statistics").WithTags("Statistics").RequireAuthorization();

        group.MapGet("/employees", GetEmployeeStatisticsAsync);
        group.MapGet("/employees/{id:int}/monthly", GetEmployeeMonthlyActivityAsync).RequireAuthorization("TaskAssigner");
        group.MapGet("/status", GetStatusStatisticsAsync).RequireAuthorization("TaskAssigner");
        group.MapGet("/clients/most-visited", GetMostVisitedClientsAsync).RequireAuthorization("ClientManager");
        group.MapGet("/supervisors", GetSupervisorTeamStatisticsAsync).RequireAuthorization("ClientManager");

        return app;
    }

    private static async Task<Ok<EmployeeStatisticsDto>> GetEmployeeStatisticsAsync(AppDbContext db)
    {
        var total = await db.Employees.CountAsync();

        var byGender = await db.Employees
            .GroupBy(e => e.Gender)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key.ToString(), x => x.Count);

        var byStatus = await db.Employees
            .GroupBy(e => e.Status)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key.ToString(), x => x.Count);

        var byAttendanceStatus = await db.Employees
            .GroupBy(e => e.AttendanceStatus)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key.ToString(), x => x.Count);

        return TypedResults.Ok(new EmployeeStatisticsDto(total, byGender, byStatus, byAttendanceStatus));
    }

    private static async Task<Results<Ok<EmployeeMonthlyActivityDto>, NotFound, BadRequest<string>>> GetEmployeeMonthlyActivityAsync(
        int id, int year, int month, ClaimsPrincipal principal, AppDbContext db)
    {
        if (month < 1 || month > 12)
        {
            return TypedResults.BadRequest("month must be between 1 and 12.");
        }

        var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
        if (employee is null) return TypedResults.NotFound();

        if (!await CanViewEmployeeStatsAsync(id, principal, db)) return TypedResults.NotFound();

        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1);

        var taskQuery = db.Tasks.AsNoTracking()
            .Where(t => t.AssigneeId == id && t.DueDateTime >= monthStart && t.DueDateTime < monthEnd);

        var totalTasks = await taskQuery.CountAsync();
        var tasksByStatus = await taskQuery
            .GroupBy(t => t.Status)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key.ToString(), x => x.Count);
        var attendanceDrivingTaskCount = await taskQuery.CountAsync(t => t.AttendanceRequired);

        var visitTaskQuery = db.Visits.AsNoTracking()
            .Where(v => v.DateTime >= monthStart && v.DateTime < monthEnd)
            .Join(db.Tasks.AsNoTracking(), v => v.Id, t => t.RelatedVisitId, (v, t) => t)
            .Where(t => t.AssigneeId == id);

        var totalVisits = await visitTaskQuery.CountAsync();
        var visitsByStatus = await visitTaskQuery
            .GroupBy(t => t.Status)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key.ToString(), x => x.Count);

        var dto = new EmployeeMonthlyActivityDto(
            employee.Id, year, month,
            totalTasks, tasksByStatus,
            totalVisits, visitsByStatus,
            employee.AttendanceStatus.ToString(), attendanceDrivingTaskCount);

        return TypedResults.Ok(dto);
    }

    private static async Task<Ok<StatusStatsDto>> GetStatusStatisticsAsync(ClaimsPrincipal principal, AppDbContext db)
    {
        List<int>? teamEmployeeIds = HasFullEmployeeStatsAccess(principal)
            ? null
            : await GetSupervisorTeamEmployeeIdsAsync(principal, db);

        var taskQuery = db.Tasks.AsNoTracking().AsQueryable();
        var visitTaskQuery = db.Visits.AsNoTracking()
            .Join(db.Tasks.AsNoTracking(), v => v.Id, t => t.RelatedVisitId, (v, t) => t);

        if (teamEmployeeIds is not null)
        {
            taskQuery = taskQuery.Where(t => teamEmployeeIds.Contains(t.AssigneeId));
            visitTaskQuery = visitTaskQuery.Where(t => teamEmployeeIds.Contains(t.AssigneeId));
        }

        var tasksByStatus = await taskQuery
            .GroupBy(t => t.Status)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key.ToString(), x => x.Count);

        var visitsByStatus = await visitTaskQuery
            .GroupBy(t => t.Status)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key.ToString(), x => x.Count);

        return TypedResults.Ok(new StatusStatsDto(tasksByStatus, visitsByStatus));
    }

    private static async Task<Results<Ok<List<MostVisitedClientDto>>, BadRequest<string>>> GetMostVisitedClientsAsync(
        string range, int? top, AppDbContext db)
    {
        var cutoff = GetRangeCutoff(range);
        if (cutoff is null)
        {
            return TypedResults.BadRequest("range must be one of: month, 3month, 6month, year.");
        }

        var take = Math.Clamp(top ?? 10, 1, 100);

        var ranked = await db.Visits.AsNoTracking()
            .Where(v => v.DateTime >= cutoff)
            .GroupBy(v => v.ClientId)
            .Select(g => new { ClientId = g.Key, VisitCount = g.Count() })
            .OrderByDescending(x => x.VisitCount)
            .Take(take)
            .ToListAsync();

        var rankedClientIds = ranked.Select(r => r.ClientId).ToList();
        var clientNames = await db.Clients.AsNoTracking()
            .Where(c => rankedClientIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name);

        var result = ranked
            .Select(r => new MostVisitedClientDto(r.ClientId, clientNames.GetValueOrDefault(r.ClientId, "Unknown"), r.VisitCount))
            .ToList();

        return TypedResults.Ok(result);
    }

    private static async Task<Ok<List<SupervisorTeamStatsDto>>> GetSupervisorTeamStatisticsAsync(AppDbContext db)
    {
        var supervisorRoleId = await db.Roles
            .Where(r => r.Name == nameof(Role.Supervisor))
            .Select(r => r.Id)
            .FirstOrDefaultAsync();

        var supervisors = await db.Employees.AsNoTracking()
            .Where(e => e.UserId != null && db.UserRoles.Any(ur => ur.UserId == e.UserId && ur.RoleId == supervisorRoleId))
            .Select(e => new { e.Id, e.FirstName, e.LastName, e.AssignedLocationId })
            .ToListAsync();

        var locationToClient = await db.Locations.AsNoTracking().ToDictionaryAsync(l => l.Id, l => l.ClientId);
        var clientNames = await db.Clients.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name);

        // Every visit-linked task's assignee, keyed by the visit's ClientId — the "team" roster source (Story Goal point 5).
        var visitAssigneesByClient = await db.Visits.AsNoTracking()
            .Join(db.Tasks.AsNoTracking(), v => v.Id, t => t.RelatedVisitId, (v, t) => new { v.ClientId, t.AssigneeId })
            .ToListAsync();

        var openTaskCountsByAssignee = await db.Tasks.AsNoTracking()
            .Where(t => t.Status == TaskItemStatus.New || t.Status == TaskItemStatus.InProgress)
            .GroupBy(t => t.AssigneeId)
            .Select(g => new { AssigneeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.AssigneeId, x => x.Count);

        var attendanceByEmployee = await db.Employees.AsNoTracking().ToDictionaryAsync(e => e.Id, e => e.AttendanceStatus);

        var result = new List<SupervisorTeamStatsDto>();
        foreach (var supervisor in supervisors)
        {
            int? clientId = supervisor.AssignedLocationId is not null && locationToClient.TryGetValue(supervisor.AssignedLocationId.Value, out var cid)
                ? cid
                : null;
            var clientName = clientId is not null ? clientNames.GetValueOrDefault(clientId.Value) : null;

            var teamEmployeeIds = clientId is null
                ? []
                : visitAssigneesByClient.Where(x => x.ClientId == clientId).Select(x => x.AssigneeId).Distinct().ToList();

            var openTaskCount = teamEmployeeIds.Sum(eid => openTaskCountsByAssignee.GetValueOrDefault(eid));

            var attendanceBreakdown = teamEmployeeIds
                .Select(eid => attendanceByEmployee.GetValueOrDefault(eid))
                .GroupBy(status => status)
                .ToDictionary(g => g.Key.ToString(), g => g.Count());

            result.Add(new SupervisorTeamStatsDto(
                supervisor.Id, $"{supervisor.FirstName} {supervisor.LastName}",
                clientId, clientName, teamEmployeeIds, openTaskCount, attendanceBreakdown));
        }

        return TypedResults.Ok(result);
    }

    private static bool HasFullEmployeeStatsAccess(ClaimsPrincipal principal) =>
        principal.IsInRole(nameof(Role.Admin)) || principal.IsInRole(nameof(Role.Manager));

    private static async Task<bool> CanViewEmployeeStatsAsync(int employeeId, ClaimsPrincipal principal, AppDbContext db)
    {
        if (HasFullEmployeeStatsAccess(principal)) return true;
        var teamEmployeeIds = await GetSupervisorTeamEmployeeIdsAsync(principal, db);
        return teamEmployeeIds.Contains(employeeId);
    }

    private static async Task<List<int>> GetSupervisorTeamEmployeeIdsAsync(ClaimsPrincipal principal, AppDbContext db)
    {
        var supervisorClientId = await GetSupervisorAssignedClientIdAsync(principal, db);
        if (supervisorClientId is null) return [];

        return await db.Visits.AsNoTracking()
            .Where(v => v.ClientId == supervisorClientId)
            .Join(db.Tasks.AsNoTracking(), v => v.Id, t => t.RelatedVisitId, (v, t) => t.AssigneeId)
            .Distinct()
            .ToListAsync();
    }

    private static async Task<int?> GetSupervisorAssignedClientIdAsync(ClaimsPrincipal principal, AppDbContext db)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return null;

        var assignedLocationId = await db.Employees
            .Where(e => e.UserId == userId)
            .Select(e => e.AssignedLocationId)
            .FirstOrDefaultAsync();

        if (assignedLocationId is null) return null;

        return await db.Locations
            .Where(l => l.Id == assignedLocationId)
            .Select(l => (int?)l.ClientId)
            .FirstOrDefaultAsync();
    }

    private static DateTime? GetRangeCutoff(string range) => range switch
    {
        "month" => DateTime.UtcNow.AddMonths(-1),
        "3month" => DateTime.UtcNow.AddMonths(-3),
        "6month" => DateTime.UtcNow.AddMonths(-6),
        "year" => DateTime.UtcNow.AddYears(-1),
        _ => null
    };
}
```

`GetSupervisorAssignedClientIdAsync` and `GetRangeCutoff` are verbatim duplicates of `ClientEndpoints.cs` lines 134–150 and 115–122 respectively — intentional, per this codebase's documented no-cross-feature-service convention (Context item 6 / `CLAUDE.md` line 32). `GetMostVisitedClientsAsync` deliberately splits into two queries (rank-and-take, then a separate name lookup) rather than one combined `GroupBy().Join()` — the two-query form matches the `Contains`-against-a-materialized-list pattern already proven elsewhere in this file (`teamEmployeeIds.Contains(...)`) rather than relying on a more complex single-query EF Core translation. `GetSupervisorTeamStatisticsAsync` batches all its supporting data into a fixed number of queries up front (never scales with the number of supervisors) instead of querying per-supervisor in the loop — see Edge Cases for the accepted-at-this-scale reasoning.

### 3 — `Program.cs` and migrations

**No changes to `Program.cs`.** `app.MapStatisticsEndpoints();` (line 152) already wires up the extended file; no new policy is needed (task 2 reuses `TaskAssigner`/`ClientManager` verbatim); no new service is registered.

**No new EF Core migration.** This story adds no entity, column, or index — every new endpoint reads existing tables (`Employees`, `Tasks`, `Visits`, `Clients`, `Locations`, plus Identity's `AspNetRoles`/`AspNetUserRoles`) through the existing `AppDbContext` DbSets.

### 4 — Update docs

**File: `README.md`** — add four rows to the API table (after line 93, the existing `GET /api/clients/{id}/visit-stats` row):

```
| GET | `/api/statistics/employees/{id}/monthly` | Admin/Manager/Supervisor | Per-employee monthly rollup: tasks/visits by status, current attendance snapshot (`year`, `month` required; Supervisor: team only) |
| GET | `/api/statistics/status` | Admin/Manager/Supervisor | Task and visit counts by status (Supervisor: team only) |
| GET | `/api/statistics/clients/most-visited` | Admin/Manager | Clients ranked by visit count (`range=month\|3month\|6month\|year`, optional `top`, default 10) |
| GET | `/api/statistics/supervisors` | Admin/Manager | Every supervisor with their team roster, open task count, and team attendance breakdown |
```

Add a `Dashboards & stats` bullet to the Features list (after line 13, matching the existing `Clients & locations` bullet's style), summarizing the four new endpoints and the "team" heuristic in one sentence. Update the Roadmap paragraph (line 97): remove `"a 'most visited clients' ranking (planned for a future dashboards feature)"` (now shipped) from the Clients & locations clause, and add a new clause noting that the per-employee monthly rollup omits "Leaves" pending the `leaves` feature, and that "team" for Supervisor-scoped dashboards is a visit-derived heuristic, not a real reporting hierarchy.

**File: `CLAUDE.md`** — update line 26 (Architecture paragraph): after the existing `ClientEndpoints` row-level-scoping sentence, add that `StatisticsEndpoints` now does the same kind of row-level scoping for its Supervisor-facing routes, using the same "visit-linked task under the Supervisor's assigned client" heuristic as its new "team" concept (cross-reference this story). Update line 71 (Known v1 gaps): remove `"a 'most visited clients' ranking (planned for a future dashboards feature)"` from the Clients & locations sentence (now shipped), and append a new sentence for dashboards: per-employee monthly rollups shipped for tasks/visits/attendance-snapshot but not Leaves (pending the `leaves` feature); no attendance history table exists (current-snapshot only); "Supervisor's team" for dashboards is a heuristic derived from visit assignments under the Supervisor's assigned client, not a real reporting hierarchy.

---

## Edge Cases & Failure Modes

- **"Team" is a heuristic, not a real hierarchy — false negatives.** An Employee who has never been the assignee of a visit-linked task appears on **no** Supervisor's team roster and is invisible to `GetEmployeeMonthlyActivityAsync`/`GetStatusStatisticsAsync` for every Supervisor, even if they have plenty of non-visit `TaskItem`s. This follows directly from the heuristic's definition (Story Goal point 5) — there is no other Employee↔Client/Location association anywhere in the schema to fall back on.
- **"Team" false positives / overlap.** An Employee who has done visits for multiple different clients over time (serially — the existing accepted-visit concurrency rule only blocks *concurrent* visits, `TaskEndpoints.cs` line 142) appears on multiple Supervisors' team rosters simultaneously. Two Supervisors assigned to different `Location`s of the *same* `Client` see the identical team roster (the heuristic keys on `Client`, not `Location`). Both are accepted consequences of reusing existing data instead of a real hierarchy — documented here rather than silently gapped, per this plan's own anti-hallucination rules.
- **A Supervisor viewing their own monthly activity.** `CanViewEmployeeStatsAsync` (task 2) only grants access when the target employee is on the caller's "team" — a Supervisor is not automatically on their own team unless they themselves happen to be the assignee of a visit-linked task under their own assigned client. The intake's Supervisor section ("Review an employee's monthly activity ... for every employee") reads as reviewing *others*, so this is left as-is rather than special-cased.
- **No attendance history table.** `Employee.AttendanceStatus` (`Models/Employee.cs` line 12) is a single current-value column, overwritten in place by `EmployeeAttendanceService.RecalculateAsync` (`Services/EmployeeAttendanceService.cs` lines 9–26) — there is no log of what it was during a past month. `GetEmployeeMonthlyActivityAsync`'s `CurrentAttendanceStatus` is therefore **today's** status, not "status during the requested month," and `AttendanceDrivingTaskCount` (count of that month's `AttendanceRequired` tasks/visits for the employee) is offered as the closest available proxy for "how much attendance-driving activity happened that month" — explicitly not the same thing as a status history. Do not present `CurrentAttendanceStatus` to a user as if it were month-specific.
- **`AttendanceStatus` can already be `Absent`/`OnVacation` before `leaves` ships.** `EmployeeEndpoints.CreateEmployeeAsync`'s `CreateEmployeeRequest.AttendanceStatus` (`Dtos/Employees/EmployeeDtos.cs` line 10) accepts any `AttendanceStatus` value at creation time, and `UserEndpoints.CreateUserAsync` hardcodes `AttendanceStatus.OutOfOffice` (`Endpoints/UserEndpoints.cs` line 86) — so `GetSupervisorTeamStatisticsAsync`'s attendance breakdown (task 2) may legitimately show all four enum values today, not just `InOffice`/`OutOfOffice`, even though no ongoing workflow currently drives `Absent`/`OnVacation`.
- **Leaves entirely absent.** No `LeaveRequest` entity exists (Prerequisites) — `EmployeeMonthlyActivityDto` has no `Leaves` field. Adding one later is a non-breaking, purely additive change (new JSON property), so this is safe to defer rather than stub out with a fake/always-empty field now.
- **`month` out of range** (`< 1` or `> 12`) on `GetEmployeeMonthlyActivityAsync` → `400`. **`year` is not validated** — an absurd year (e.g. `1900` or `3000`) is accepted and simply yields a rollup with all-zero counts, since no rows fall in that month; this matches the codebase's existing preference for not validating scenarios that can't cause harm (`TaskItem.DueDateTime`/`Visit.DateTime` aren't range-validated anywhere else either — `../visits/04-story-visit-tracking-and-status.md` Edge Cases, "not validated as being in the future").
- **Nonexistent `id` on `GetEmployeeMonthlyActivityAsync`** → `404` (existence check before the visibility check, matching `ClientEndpoints.GetClientByIdAsync`'s two-step shape).
- **An employee that exists but is outside the caller Supervisor's team** → `404`, not `403` — matches `ClientEndpoints`/`TaskEndpoints`'s established convention of hiding out-of-scope records behind `NotFound` rather than revealing their existence via `Forbid`.
- **`range` invalid on `GetMostVisitedClientsAsync`** → `400` (identical contract to `ClientEndpoints.GetClientVisitStatsAsync`'s existing `range` validation).
- **Ties in the most-visited ranking.** `OrderByDescending(x => x.VisitCount)` has no secondary sort key — tied clients' relative order is whatever the SQL Server provider returns, which is not guaranteed stable across calls. Acceptable for a v1 ranking display; not treated as a bug.
- **A client with zero visits in the requested `range`** is simply absent from the `most-visited` list (not zero-padded) — consistent with a "ranking," not a "report of every client."
- **`GetSupervisorTeamStatisticsAsync` performance.** The handler issues a fixed, small number of queries (roles, supervisors, all locations, all clients, all visit↔task assignee pairs, task-status counts, employee attendance) regardless of how many supervisors exist, then does all per-supervisor grouping in memory — but that in-memory work is still `O(supervisors × team size)`. Acceptable at this project's current scale, matching the precedent already accepted for `Middleware/ApiLoggingMiddleware`'s plain synchronous per-request insert (`CLAUDE.md` line 36, "accepted as fine at this scale"); revisit if the employee/visit volume grows significantly.
- **A Supervisor-role Employee with no `AssignedLocationId`.** Still appears in `GetSupervisorTeamStatisticsAsync`'s result list (not filtered out) with `AssignedClientId: null`, `AssignedClientName: null`, `TeamEmployeeIds: []`, `OpenTaskCount: 0`, `TeamAttendanceStatusBreakdown: {}` — this lets Admin/Manager see which supervisors still need a location assigned, rather than silently hiding them.
- **Employee role is fully blocked from all four new routes.** `TaskAssigner` and `ClientManager` (`Program.cs` lines 78–81) both exclude the `Employee` role — a plain Employee gets `403` on every new route, since the intake never lists Employees as a consumer of this feature (only "Admins/Managers" and "Supervisors" sections exist in the source spec).
- **`GetEmployeeMonthlyActivityAsync`'s visit-month filter uses `Visit.DateTime`, not `TaskItem.DueDateTime`, for the visit-side query** — safe only because `VisitEndpoints.CreateVisitAsync` always sets the driving task's `DueDateTime` equal to the visit's `DateTime` (`Endpoints/VisitEndpoints.cs` line 98) and nothing in this codebase can edit a task's `DueDateTime` after creation (no such field is settable via `ReassignTaskAsync`). If a future story ever allows editing `TaskItem.DueDateTime` independently of its `Visit.DateTime`, this equivalence would break and the two queries would need reconciling.

---

## Test Plan

This repo has no automated test project yet (`CLAUDE.md` line 22). This story follows the same manual-Swagger-walkthrough pattern as the `visits`/`clients` stories.

1. **Setup** — as Admin: create a `Client` with one `Location` (`POST /api/clients`); create a Supervisor-role user assigned to that location (`POST /api/users`, `AssignedLocationId` set); create two more plain Employees. Create a mix of plain tasks (`POST /api/tasks`) and visits (`POST /api/visits`) against one of the plain Employees, spanning at least two different calendar months (vary `DueDateTime`/`DateTime`), with at least one task/visit accepted (`InProgress`) and one completed (`Done`).
2. **Per-employee monthly rollup, Admin** — `GET /api/statistics/employees/{id}/monthly?year=&month=` for the employee with visits, for the month containing the seeded data; confirm `TotalTasks`/`TasksByStatus`/`TotalVisits`/`VisitsByStatus` match what was seeded, and `CurrentAttendanceStatus` matches `GET /api/employees/{id}`.
3. **Per-employee monthly rollup, Supervisor scoping** — as the seeded Supervisor, call the same endpoint for the visited employee (on their team via the visit) → `200`. Call it for the third Employee (never visited, not on any team) → `404`. Call it as a plain Employee → `403`.
4. **Invalid month** — `GET /api/statistics/employees/{id}/monthly?year=2026&month=13` → `400`.
5. **Nonexistent employee** — `GET /api/statistics/employees/999999/monthly?year=2026&month=1` → `404`.
6. **Status stats scoping** — `GET /api/statistics/status` as Admin; confirm counts include all seeded tasks/visits. As the Supervisor; confirm counts include only the visited employee's tasks/visits (fewer than or equal to Admin's view). As a plain Employee → `403`.
7. **Most-visited clients** — as Admin, `GET /api/statistics/clients/most-visited?range=month`; confirm the seeded client appears with the correct `VisitCount`. Retry with `range=bogus` → `400`. Retry as the Supervisor → `403`.
8. **Supervisors + teams** — as Admin, `GET /api/statistics/supervisors`; confirm the seeded Supervisor appears with `AssignedClientId` matching the seeded client, `TeamEmployeeIds` containing the visited employee's id, `OpenTaskCount` matching their New/InProgress task count, and `TeamAttendanceStatusBreakdown` matching their current status. Create a second Supervisor-role user with no `AssignedLocationId`; confirm they appear with `AssignedClientId: null` and an empty team. Retry the whole call as Manager → `200`; as the Supervisor themselves → `403`.
9. **Regression** — `GET /api/statistics/employees` (unmodified route) still returns the same shape as before this story.
10. **Regression** — confirm `ApiCallLogs` rows are still written for the four new `/api/statistics/*` routes (sanity check that `Middleware/ApiLoggingMiddleware.cs`, unmodified by this story, still wraps them correctly).

---

## Verification Steps

1. **Backend builds:** `dotnet build` from the repo root (or `EmpoloyeeManagment/`).
2. **Run:** `dotnet run --project EmpoloyeeManagment`, open `/swagger`, and execute the full Test Plan above (steps 1–10).
3. **Regression:** re-run a quick pass of the existing `GET /api/statistics/employees`, `GET /api/clients/{id}/visit-stats`, and task/visit accept/complete flows to confirm nothing this story touched (row-level helpers duplicated, not shared) broke the originals in `ClientEndpoints.cs`/`TaskEndpoints.cs`/`VisitEndpoints.cs` (this story does not modify those files, but re-verify since they're the data source for every new query).

---

## Done Criteria

- [ ] `Dtos/Statistics/StatisticsDtos.cs` extended with `EmployeeMonthlyActivityDto`, `StatusStatsDto`, `MostVisitedClientDto`, `SupervisorTeamStatsDto`.
- [ ] `GET /api/statistics/employees/{id}/monthly` implemented: tasks/visits by status for the given month, current attendance snapshot; Admin/Manager any employee, Supervisor team-only (404 otherwise), `month` validated to 1–12.
- [ ] `GET /api/statistics/status` implemented: task/visit counts by status, scoped the same way (Admin/Manager company-wide, Supervisor team-only).
- [ ] `GET /api/statistics/clients/most-visited` implemented: ranked by visit count over `range`, Admin/Manager only, reusing the existing `range` convention.
- [ ] `GET /api/statistics/supervisors` implemented: every Supervisor-role Employee with assigned client, team roster, open task count, team attendance breakdown; Admin/Manager only.
- [ ] No `Program.cs` changes, no new authorization policy, no new EF Core migration.
- [ ] `README.md` API table/Features/Roadmap and `CLAUDE.md` (Architecture, Known v1 gaps) updated.
- [ ] "Leaves" explicitly **not** included in the monthly rollup — documented as deferred pending the `leaves` feature, not silently dropped.
- [ ] Full manual Test Plan (steps 1–10) passes against a local run.

**STOP HERE. Report to the user and wait for confirmation before proceeding to a future Leaves-integration follow-up story (only plannable once the `leaves` feature itself has a real `NN-story-*.md` and shipped `LeaveRequest` entity).**
