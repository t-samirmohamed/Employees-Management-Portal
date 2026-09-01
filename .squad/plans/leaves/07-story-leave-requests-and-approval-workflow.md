# Story 07 — Leave Requests & Approval Workflow

## Prerequisites

- Story 01 completed: [`../roles/01-story-role-model-and-admin-user-management.md`](../roles/01-story-role-model-and-admin-user-management.md) — this story depends directly on the `Role` enum, the JWT role claim, `AdminAuthorizationHandler`, and the `TaskAssigner` policy it introduced. Verified already implemented in the current codebase: `Models/Enums.cs` lines 23–29 (`Role` enum), `Program.cs` lines 76–83 (`AddAuthorization` with `AdminOnly`/`TaskAssigner`/`EmployeeOnly`/`ClientManager` policies + handler registration), `Authorization/AdminAuthorizationHandler.cs` (whole file).
- No other story is a hard code dependency. `LeaveRequest` (task 2) only references `Employee` (unconditionally present since baseline) — it has no relationship to `TaskItem`, `Visit`, `Client`, or `Location`. `tasks`/`visits`/`clients`/`statistics` are cited throughout this plan only as **structural precedent** for the row-level authorization patterns reused here, not as functional prerequisites; none of their files are modified by this story.
- **Coordination note for the (not-yet-planned) `notifications` story** — see [`../../stories/notifications/leave-request-notifications/intake.md`](../../stories/notifications/leave-request-notifications/intake.md): its own acceptance criteria says it will "create a notification for every valid approver per the routing rules from the `leaves` story" on `POST /api/leaves`. This story deliberately adds **no** event/hook/callback mechanism for that. Per this codebase's established convention — the dependent story modifies the producer in place rather than the producer pre-building a hook (this is exactly how the `visits` story modified `TaskEndpoints.CreateTaskAsync`/`AcceptTaskAsync`/`CompleteTaskAsync` in place, rather than the `tasks` story speculatively building one) — `notifications` will modify `CreateLeaveRequestAsync` (task 7 below) in place to call its own future notification-creation logic once it's planned. Do not build that hook here.

---

## Story Goal

Give Employees, Supervisors, and Managers a way to submit, edit, and delete their own leave requests, and give the appropriate higher-ranked role a way to accept, reject, or request a delay on a pending one — entirely role-routed, with no reporting-hierarchy/team model (none exists anywhere in this codebase). Concretely:

1. A `LeaveRequest` entity exists with `RequesterId` (→ `Employee`), `StartDate`, `EndDate`, `Reason`, a server-computed `Status` (`Pending`/`Accepted`/`Rejected`/`DelayRequested`), `ApproverEmployeeId` (nullable, → `Employee`, set only once actioned), `DecisionReason` (nullable), and `RequestedDelayDate` (nullable, set only by `request-delay`).
2. `POST /api/leaves` (a new `LeaveRequester` policy: Employee/Manager/Supervisor) creates a `Pending` request. `RequesterId` is always resolved from the authenticated caller's own linked `Employee` — never accepted as client input — mirroring how `TaskEndpoints.AddCommentAsync` resolves `AuthorEmployeeId` from the caller rather than the request body.
3. `PATCH /api/leaves/{id}` (edit) and `DELETE /api/leaves/{id}` are restricted to the original requester, and only while `Status` is still `Pending` — a non-owner gets `403` (not `404`), mirroring `TaskEndpoints.UpdateCommentAsync`/`DeleteCommentAsync`'s ownership check rather than `TaskEndpoints.GetTaskByIdAsync`'s view-hiding one (see Edge Cases for why).
4. `PATCH /api/leaves/{id}/accept`, `/reject`, `/request-delay` are gated at the route level by the **existing** `TaskAssigner` policy (Admin/Manager/Supervisor — the same set that can ever action any leave request), then by a new **row-level** check, `CanActionLeaveRequest`, that resolves the request's requester's own Identity role and requires the caller's role to strictly outrank it on a fixed linear scale: `Employee (0) < Supervisor (1) < Manager (2) < Admin (3)`. This directly encodes the source doc's routing table (Employee's request → Supervisor/Manager/Admin; Supervisor's → Manager/Admin; Manager's → Admin) as one comparison instead of a per-role special case, and — because the comparison is strict — blocks self-approval and same-rank peer-approval as an emergent property, with no separate check needed.
5. `reject` requires a non-empty `Reason`; `request-delay` requires a `TargetDate` and a non-empty `Reason`; `accept` requires neither — matching the intake's own reading of "Accept/Reject/Request Delay ... and stating a reason."
6. `GET /api/leaves` and `GET /api/leaves/{id}` are added beyond the intake's literal endpoint list: an approver cannot discover or review a request to act on it, and a requester cannot confirm what they submitted, without one — the same practical necessity that led every other feature in this codebase (`Tasks`, `Visits`, `Clients`, `Users`) to ship GET routes alongside their mutation endpoints. They use the same row-level visibility as `GET /api/tasks`/`GET /api/visits`: Employee sees/opens only their own; Admin/Manager/Supervisor see all (a deliberate superset, not team/location-scoped — see Edge Cases).

**Not in scope** (do not build these here):
- Notification delivery, storage, or any creation hook for it — separate, not-yet-planned `notifications` story; see Prerequisites' coordination note.
- Leave-balance/accrual tracking (days remaining, carry-over) — not mentioned anywhere in the source doc.
- Team/location-scoped visibility for `GET /api/leaves` (the way `ClientEndpoints`/`StatisticsEndpoints` scope a Supervisor to their `AssignedLocationId`) — the source doc's leave-approval routing is purely role-based, never location-based; this story does not invent scoping the doc doesn't describe.
- Integrating leave data into the already-shipped `dashboards` feature (`StatisticsEndpoints.GetEmployeeMonthlyActivityAsync`/`EmployeeMonthlyActivityDto`) — already flagged as a known, explicitly deferred follow-up by [`../dashboards/00-overview.md`](../dashboards/00-overview.md) line 15, not this story's job.
- Any further transition out of `DelayRequested` (re-actioning a delayed request, or a way to actually reschedule the leave) — the source doc's `Status` enum lists it as a fourth terminal value alongside `Accepted`/`Rejected` with no follow-up flow described; see Edge Cases.
- Query filters (status, date range) or pagination on `GET /api/leaves` beyond the required row-level scoping — not asked for; can be added later as a non-breaking, additive change, the same restraint the `tasks`/`visits` stories documented for their own list endpoints.

---

## Context — Read These Files First

1. [`EmpoloyeeManagment/Models/Enums.cs`](../../../EmpoloyeeManagment/Models/Enums.cs) — whole file (38 lines). `TaskItemStatus` (lines 31–38) is the append point for the new `LeaveRequestStatus` enum (task 1) — match its exact `public enum X { ... }` style, no explicit backing values.
2. [`EmpoloyeeManagment/Models/Employee.cs`](../../../EmpoloyeeManagment/Models/Employee.cs) — whole file (15 lines). The plain-class-with-auto-properties, no-navigation-property style the new `LeaveRequest` model (task 2) must match.
3. [`EmpoloyeeManagment/Models/TaskItem.cs`](../../../EmpoloyeeManagment/Models/TaskItem.cs) — whole file (15 lines). `RejectionReason` (line 13, nullable `string?`) is the closest existing precedent for `LeaveRequest.DecisionReason`'s shape.
4. [`EmpoloyeeManagment/Data/AppDbContext.cs`](../../../EmpoloyeeManagment/Data/AppDbContext.cs) — whole file (125 lines). `DbSet` declarations lines 9–15 (add `LeaveRequests` here, task 3). The `Employee` block's FK-to-`Location` (lines 45–48) and the `Visit` block's two FKs to two *different* entity types (lines 112–123, `Client` and `Location`) are the closest existing FK precedents — but note neither is "two FKs to the *same* entity type." `LeaveRequest.RequesterId` and `LeaveRequest.ApproverEmployeeId` (task 3) both point at `Employee` — the first time this codebase needs that shape. See Edge Cases for the exact fluent-API pattern and what to verify in the generated migration.
5. [`EmpoloyeeManagment/Program.cs`](../../../EmpoloyeeManagment/Program.cs) — whole file (159 lines). `AddAuthorization` block, lines 76–82 (add a `LeaveRequester` policy after line 81, task 5). Line 156 (`app.MapClientEndpoints();`) — add `app.MapLeaveEndpoints();` directly after it (task 8). No new service registration needed — this story adds no new service class. No new `using` needed — `EmpoloyeeManagment.Endpoints` is already imported (line 5).
6. [`EmpoloyeeManagment/Authorization/AdminAuthorizationHandler.cs`](../../../EmpoloyeeManagment/Authorization/AdminAuthorizationHandler.cs) — whole file (20 lines). Confirms this handler only intercepts **policy-based** `RequireAuthorization("...")` checks (it succeeds every pending `IAuthorizationRequirement` for an Admin) — it does **not** run for a manual, in-handler check like `CanActionLeaveRequest` (task 7). This is why that check must itself rank Admin as the highest rank rather than assuming the handler covers it — see Edge Cases for how it does, correctly, without a special case.
7. [`EmpoloyeeManagment/Endpoints/TaskEndpoints.cs`](../../../EmpoloyeeManagment/Endpoints/TaskEndpoints.cs) — whole file (312 lines). Specifically: `GetTasksAsync` (lines 32–49) and `GetTaskByIdAsync` (lines 51–64) — the `IsPrivileged`-scoped-list, `CanView*`-scoped-detail-`404` pattern `GetLeaveRequestsAsync`/`GetLeaveRequestByIdAsync` (task 7) reuse directly. `AcceptTaskAsync` (lines 128–156) and `RejectTaskAsync` (lines 158–182) — the check-ownership/authorization-**then**-check-status ordering, and the required-reason `400` pattern (`string.IsNullOrWhiteSpace(request.Reason)`, line 172), that `AcceptLeaveRequestAsync`/`RejectLeaveRequestAsync`/`RequestDelayAsync` (task 7) follow. `UpdateCommentAsync`/`DeleteCommentAsync` (lines 254–285) — the `TypedResults.Forbid()` (`403`) ownership-check pattern, applied **without** first re-checking whether the caller could otherwise view the parent resource — `UpdateLeaveRequestAsync`/`DeleteLeaveRequestAsync` (task 7) reuse this exact shape. `IsPrivileged`/`GetCallerEmployeeAsync` (lines 287–294) — helpers duplicated verbatim into the new `LeaveEndpoints.cs`, per this codebase's no-cross-feature-service convention (`CLAUDE.md` line 32 states it explicitly; Context item 12 below is the same convention applied to a different pair of files).
8. [`EmpoloyeeManagment/Endpoints/UserEndpoints.cs`](../../../EmpoloyeeManagment/Endpoints/UserEndpoints.cs) lines 22–46 (`GetUsersAsync`), specifically lines 30–33 (`RoleName = db.Roles.Where(r => db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == r.Id)).Select(r => r.Name).FirstOrDefault()`) — the correlated-subquery pattern for resolving a user's Identity role name via `IdentityDbContext<ApplicationUser>`'s `Roles`/`UserRoles` sets. `GetEmployeeRoleAsync` (task 7) adapts this exact shape to resolve one specific employee's role by id. Also note line 42 (`Enum.Parse<Role>(u.RoleName!)`) — the unguarded-parse precedent `GetEmployeeRoleAsync` follows (every seeded role name is a valid `Role` enum member; see `Program.cs` lines 108–130).
9. [`EmpoloyeeManagment/Endpoints/StatisticsEndpoints.cs`](../../../EmpoloyeeManagment/Endpoints/StatisticsEndpoints.cs) lines 153–163 (`GetSupervisorTeamStatisticsAsync`'s role-id-then-membership resolution — an alternate two-step shape of the same `Roles`/`UserRoles` idea) and lines 208–216 (`HasFullEmployeeStatsAccess`/`CanViewEmployeeStatsAsync`) — another Admin/Manager-vs-Supervisor row-level precedent, read for contrast with Context item 10.
10. [`EmpoloyeeManagment/Endpoints/ClientEndpoints.cs`](../../../EmpoloyeeManagment/Endpoints/ClientEndpoints.cs) lines 124–150 (`HasFullClientAccess`/`CanViewClientAsync`/`GetSupervisorAssignedClientIdAsync`) — read specifically to rule this shape **out**: `leaves` does not scope visibility by a Supervisor's assigned client/location the way `Clients`/`Statistics` do, since the source doc's leave-approval routing is purely role-based (Story Goal point 6, Edge Cases).
11. [`EmpoloyeeManagment/Dtos/Tasks/TaskDtos.cs`](../../../EmpoloyeeManagment/Dtos/Tasks/TaskDtos.cs) — whole file (45 lines). The one-file-per-feature, `Create<Entity>Request` / `<Entity>ListItemDto` (compact) / `<Entity>DetailDto` (complete) naming and shape convention the new `Dtos/Leaves/LeaveDtos.cs` (task 6) must match.
12. [`../tasks/02-story-task-assignment-and-comments.md`](../tasks/02-story-task-assignment-and-comments.md) and [`../visits/04-story-visit-tracking-and-status.md`](../visits/04-story-visit-tracking-and-status.md) — read in full as the structural and tonal precedent for this story (per the planning process's "find a similar precedent" step), and the source of the "duplicate small per-endpoint-file helpers rather than extract a shared service" convention this story continues.
13. [`../../stories/notifications/leave-request-notifications/intake.md`](../../stories/notifications/leave-request-notifications/intake.md) — read in full. Confirms what "emits the event" means in practice for this story (nothing — see Prerequisites) and confirms the future `notifications` story will need to replicate this story's approver-routing logic itself, not consume it as a shared service (same no-cross-feature-service convention).
14. [`README.md`](../../../README.md) lines 62–98 (API table) and line 102 (Roadmap paragraph), and [`CLAUDE.md`](../../../CLAUDE.md) — whole file (72 lines), specifically lines 26, 40, 46, 58–60, 71 — all need updating; see task 9.

---

## Product rules (from story)

| Area | Current behavior | New behavior |
|---|---|---|
| `LeaveRequest` | Doesn't exist | New entity: `RequesterId` (→ `Employee`), `StartDate`, `EndDate`, `Reason`, `Status` (`Pending`/`Accepted`/`Rejected`/`DelayRequested`), `ApproverEmployeeId` (nullable, → `Employee`), `DecisionReason` (nullable), `RequestedDelayDate` (nullable) |
| `POST /api/leaves` | Doesn't exist | New — **Employee/Manager/Supervisor** (new `LeaveRequester` policy) — `RequesterId` always resolved from the caller; creates in `Pending`; `400` if `EndDate < StartDate` |
| `GET /api/leaves`, `GET /api/leaves/{id}` | Don't exist | New — any authenticated user; Employee sees/opens only their own; Admin/Manager/Supervisor see all (same superset pattern as `Tasks`/`Visits`) |
| `PATCH /api/leaves/{id}`, `DELETE /api/leaves/{id}` | Don't exist | New — only the original requester, only while `Pending`; `403` (not `404`) otherwise; `400` if not `Pending` |
| `PATCH /api/leaves/{id}/accept` | Doesn't exist | New — **Admin/Manager/Supervisor** (`TaskAssigner` policy) at the route level, plus a row-level rank check (`CanActionLeaveRequest`): caller's role must strictly outrank the requester's resolved role; only from `Pending` |
| `PATCH /api/leaves/{id}/reject` | Doesn't exist | New — same gating as accept, plus a required non-empty `Reason` |
| `PATCH /api/leaves/{id}/request-delay` | Doesn't exist | New — same gating as accept, plus a required `TargetDate` and non-empty `Reason`; moves to `DelayRequested` (terminal — see Edge Cases) |

---

## Backend Tasks

`No frontend changes required in this repository` — the companion UI work is tracked in the separate `employee-management-web` repo per the intake's "Extra notes."

### 1 — Add the `LeaveRequestStatus` enum

**File: `EmpoloyeeManagment/Models/Enums.cs`**

Append after `TaskItemStatus` (after line 38, end of file), matching the existing style exactly:

```csharp
public enum LeaveRequestStatus
{
    Pending,
    Accepted,
    Rejected,
    DelayRequested
}
```

### 2 — Add the `LeaveRequest` model

**Create file: `EmpoloyeeManagment/Models/LeaveRequest.cs`**

```csharp
namespace EmpoloyeeManagment.Models;

public class LeaveRequest
{
    public int Id { get; set; }
    public int RequesterId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public LeaveRequestStatus Status { get; set; } = LeaveRequestStatus.Pending;
    public int? ApproverEmployeeId { get; set; }
    public string? DecisionReason { get; set; }
    public DateTime? RequestedDelayDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
```

No navigation properties to `Employee` — matches this codebase's established FK-by-id-only convention (`TaskItem.AssigneeId`, `Employee.UserId` — neither has a nav property either). `StartDate`/`EndDate` is this story's reading of the source doc's plural "dates" (the acceptance criteria lists "dates" alongside "reason, Status..." as separate entity fields) as a date range — the standard shape for a leave request; flagged here since the source doc never spells out field names.

### 3 — Register the `LeaveRequests` DbSet and EF configuration

**File: `EmpoloyeeManagment/Data/AppDbContext.cs`**

Add a `DbSet` after line 15 (`public DbSet<Location> Locations => Set<Location>();`):

```csharp
public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
```

Add a new configuration block inside `OnModelCreating`, directly after the `Visit` block (after line 123, before the closing `}` of `OnModelCreating` on line 124):

```csharp
builder.Entity<LeaveRequest>(entity =>
{
    entity.Property(l => l.Reason).HasMaxLength(1000).IsRequired();
    entity.Property(l => l.DecisionReason).HasMaxLength(1000);
    entity.Property(l => l.Status).HasConversion<string>().HasMaxLength(20);

    entity.HasOne<Employee>()
        .WithMany()
        .HasForeignKey(l => l.RequesterId)
        .OnDelete(DeleteBehavior.Restrict);

    entity.HasOne<Employee>()
        .WithMany()
        .HasForeignKey(l => l.ApproverEmployeeId)
        .OnDelete(DeleteBehavior.Restrict);
});
```

Two separate `HasOne<Employee>().WithMany().HasForeignKey(...)` calls, each naming a distinct FK property and neither declaring a navigation property — this is valid EF Core (each call defines an independent, unnamed relationship disambiguated solely by its FK property) but is the first time this codebase has needed two FKs to the *same* entity type from one dependent entity. `DeleteBehavior.Restrict` on both, matching the caution already implicit in every other FK to `Employee` in this codebase. `Reason`/`DecisionReason` both use `HasMaxLength(1000)`, matching `TaskItem.RejectionReason`'s precedent exactly (`Data/AppDbContext.cs` line 63). `StartDate`/`EndDate` get no explicit `Property()` call — plain `DateTime` columns need none, matching `TaskItem.DueDateTime`/`Visit.DateTime`.

### 4 — Create the EF Core migration

From the repo root:

```bash
dotnet ef migrations add AddLeaveRequests --project EmpoloyeeManagment
dotnet ef database update --project EmpoloyeeManagment
```

Confirm the generated migration only adds one new table (`LeaveRequests`) with **two independent** FK constraints to `Employees` (one for `RequesterId`, required; one for `ApproverEmployeeId`, nullable) — not a single merged constraint, and no changes to any other table. If it reports changes anywhere else, stop and investigate before applying.

### 5 — Add the `LeaveRequester` authorization policy

**File: `EmpoloyeeManagment/Program.cs`**

Extend the `AddAuthorization` block (lines 76–82), adding a new policy after line 81:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(nameof(Role.Admin)));
    options.AddPolicy("TaskAssigner", policy => policy.RequireRole(nameof(Role.Admin), nameof(Role.Manager), nameof(Role.Supervisor)));
    options.AddPolicy("EmployeeOnly", policy => policy.RequireRole(nameof(Role.Employee)));
    options.AddPolicy("ClientManager", policy => policy.RequireRole(nameof(Role.Admin), nameof(Role.Manager)));
    options.AddPolicy("LeaveRequester", policy => policy.RequireRole(nameof(Role.Employee), nameof(Role.Manager), nameof(Role.Supervisor)));
});
```

No new policy needed for accept/reject/request-delay — they reuse the existing `TaskAssigner` policy (Admin/Manager/Supervisor is exactly the set of roles that can ever action *any* leave request; the row-level `CanActionLeaveRequest` check in task 7 narrows further per request). `LeaveRequester` deliberately excludes Admin — matching the intake's literal "Employee, Supervisor, or Manager can be a requester" — though `AdminAuthorizationHandler` still lets an Admin call it anyway, the same accepted quirk already documented for `TaskAssigner`/`EmployeeOnly` (see Edge Cases).

### 6 — Create the Leaves DTOs

**Create file: `EmpoloyeeManagment/Dtos/Leaves/LeaveDtos.cs`**

```csharp
using EmpoloyeeManagment.Models;

namespace EmpoloyeeManagment.Dtos.Leaves;

public record CreateLeaveRequestRequest(DateTime StartDate, DateTime EndDate, string Reason);

public record UpdateLeaveRequestRequest(DateTime StartDate, DateTime EndDate, string Reason);

public record RejectLeaveRequestRequest(string Reason);

public record RequestDelayRequest(DateTime TargetDate, string Reason);

public record LeaveRequestListItemDto(
    int Id,
    int RequesterId,
    DateTime StartDate,
    DateTime EndDate,
    LeaveRequestStatus Status,
    int? ApproverEmployeeId);

public record LeaveRequestDetailDto(
    int Id,
    int RequesterId,
    DateTime StartDate,
    DateTime EndDate,
    string Reason,
    LeaveRequestStatus Status,
    int? ApproverEmployeeId,
    string? DecisionReason,
    DateTime? RequestedDelayDate,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
```

`CreateLeaveRequestRequest`/`UpdateLeaveRequestRequest`/`RejectLeaveRequestRequest` follow the codebase's `Create<Entity>Request` convention exactly (compare `CreateTaskRequest`, `RejectTaskRequest` in `Dtos/Tasks/TaskDtos.cs`) — the entity here is itself named `LeaveRequest`, so the doubled "Request" (e.g. `CreateLeaveRequestRequest`) is intentional, not a typo. `LeaveRequestListItemDto` omits `Reason`/`DecisionReason`/`RequestedDelayDate`/timestamps, matching `TaskListItemDto`'s precedent of a compact list shape versus a complete `TaskDetailDto`.

### 7 — Create `Endpoints/LeaveEndpoints.cs`

**Create file: `EmpoloyeeManagment/Endpoints/LeaveEndpoints.cs`**

```csharp
using System.Security.Claims;
using EmpoloyeeManagment.Data;
using EmpoloyeeManagment.Dtos.Leaves;
using EmpoloyeeManagment.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace EmpoloyeeManagment.Endpoints;

public static class LeaveEndpoints
{
    public static IEndpointRouteBuilder MapLeaveEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/leaves").WithTags("Leaves").RequireAuthorization();

        group.MapGet("/", GetLeaveRequestsAsync);
        group.MapGet("/{id:int}", GetLeaveRequestByIdAsync);
        group.MapPost("/", CreateLeaveRequestAsync).RequireAuthorization("LeaveRequester");
        group.MapPatch("/{id:int}", UpdateLeaveRequestAsync);
        group.MapDelete("/{id:int}", DeleteLeaveRequestAsync);
        group.MapPatch("/{id:int}/accept", AcceptLeaveRequestAsync).RequireAuthorization("TaskAssigner");
        group.MapPatch("/{id:int}/reject", RejectLeaveRequestAsync).RequireAuthorization("TaskAssigner");
        group.MapPatch("/{id:int}/request-delay", RequestDelayAsync).RequireAuthorization("TaskAssigner");

        return app;
    }

    private static async Task<Ok<List<LeaveRequestListItemDto>>> GetLeaveRequestsAsync(ClaimsPrincipal principal, AppDbContext db)
    {
        var query = db.LeaveRequests.AsNoTracking().AsQueryable();

        if (!IsPrivileged(principal))
        {
            var caller = await GetCallerEmployeeAsync(principal, db);
            if (caller is null) return TypedResults.Ok(new List<LeaveRequestListItemDto>());
            query = query.Where(l => l.RequesterId == caller.Id);
        }

        var leaveRequests = await query
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new LeaveRequestListItemDto(l.Id, l.RequesterId, l.StartDate, l.EndDate, l.Status, l.ApproverEmployeeId))
            .ToListAsync();

        return TypedResults.Ok(leaveRequests);
    }

    private static async Task<Results<Ok<LeaveRequestDetailDto>, NotFound>> GetLeaveRequestByIdAsync(int id, ClaimsPrincipal principal, AppDbContext db)
    {
        var leaveRequest = await db.LeaveRequests.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id);
        if (leaveRequest is null) return TypedResults.NotFound();

        if (!IsPrivileged(principal))
        {
            var caller = await GetCallerEmployeeAsync(principal, db);
            if (caller is null || leaveRequest.RequesterId != caller.Id) return TypedResults.NotFound();
        }

        return TypedResults.Ok(ToDetailDto(leaveRequest));
    }

    private static async Task<Results<Created<LeaveRequestDetailDto>, NotFound, BadRequest<string>>> CreateLeaveRequestAsync(
        CreateLeaveRequestRequest request, ClaimsPrincipal principal, AppDbContext db)
    {
        var caller = await GetCallerEmployeeAsync(principal, db);
        if (caller is null) return TypedResults.NotFound();

        if (request.EndDate < request.StartDate)
        {
            return TypedResults.BadRequest("EndDate cannot be before StartDate.");
        }

        var leaveRequest = new LeaveRequest
        {
            RequesterId = caller.Id,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Reason = request.Reason,
            Status = LeaveRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        db.LeaveRequests.Add(leaveRequest);
        await db.SaveChangesAsync();

        return TypedResults.Created($"/api/leaves/{leaveRequest.Id}", ToDetailDto(leaveRequest));
    }

    private static async Task<Results<Ok<LeaveRequestDetailDto>, NotFound, BadRequest<string>, ForbidHttpResult>> UpdateLeaveRequestAsync(
        int id, UpdateLeaveRequestRequest request, ClaimsPrincipal principal, AppDbContext db)
    {
        var leaveRequest = await db.LeaveRequests.FirstOrDefaultAsync(l => l.Id == id);
        if (leaveRequest is null) return TypedResults.NotFound();

        var caller = await GetCallerEmployeeAsync(principal, db);
        if (caller is null || leaveRequest.RequesterId != caller.Id) return TypedResults.Forbid();

        if (leaveRequest.Status != LeaveRequestStatus.Pending)
        {
            return TypedResults.BadRequest($"Cannot edit a leave request with status {leaveRequest.Status}.");
        }

        if (request.EndDate < request.StartDate)
        {
            return TypedResults.BadRequest("EndDate cannot be before StartDate.");
        }

        leaveRequest.StartDate = request.StartDate;
        leaveRequest.EndDate = request.EndDate;
        leaveRequest.Reason = request.Reason;
        leaveRequest.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return TypedResults.Ok(ToDetailDto(leaveRequest));
    }

    private static async Task<Results<NoContent, NotFound, BadRequest<string>, ForbidHttpResult>> DeleteLeaveRequestAsync(
        int id, ClaimsPrincipal principal, AppDbContext db)
    {
        var leaveRequest = await db.LeaveRequests.FirstOrDefaultAsync(l => l.Id == id);
        if (leaveRequest is null) return TypedResults.NotFound();

        var caller = await GetCallerEmployeeAsync(principal, db);
        if (caller is null || leaveRequest.RequesterId != caller.Id) return TypedResults.Forbid();

        if (leaveRequest.Status != LeaveRequestStatus.Pending)
        {
            return TypedResults.BadRequest($"Cannot delete a leave request with status {leaveRequest.Status}.");
        }

        db.LeaveRequests.Remove(leaveRequest);
        await db.SaveChangesAsync();

        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<LeaveRequestDetailDto>, NotFound, BadRequest<string>, ForbidHttpResult>> AcceptLeaveRequestAsync(
        int id, ClaimsPrincipal principal, AppDbContext db)
    {
        var leaveRequest = await db.LeaveRequests.FirstOrDefaultAsync(l => l.Id == id);
        if (leaveRequest is null) return TypedResults.NotFound();

        var requesterRole = await GetEmployeeRoleAsync(leaveRequest.RequesterId, db);
        if (requesterRole is null) return TypedResults.NotFound();
        if (!CanActionLeaveRequest(principal, requesterRole.Value)) return TypedResults.Forbid();

        if (leaveRequest.Status != LeaveRequestStatus.Pending)
        {
            return TypedResults.BadRequest($"Cannot action a leave request with status {leaveRequest.Status}.");
        }

        var caller = await GetCallerEmployeeAsync(principal, db);
        if (caller is null) return TypedResults.NotFound();

        leaveRequest.Status = LeaveRequestStatus.Accepted;
        leaveRequest.ApproverEmployeeId = caller.Id;
        await db.SaveChangesAsync();

        return TypedResults.Ok(ToDetailDto(leaveRequest));
    }

    private static async Task<Results<Ok<LeaveRequestDetailDto>, NotFound, BadRequest<string>, ForbidHttpResult>> RejectLeaveRequestAsync(
        int id, RejectLeaveRequestRequest request, ClaimsPrincipal principal, AppDbContext db)
    {
        var leaveRequest = await db.LeaveRequests.FirstOrDefaultAsync(l => l.Id == id);
        if (leaveRequest is null) return TypedResults.NotFound();

        var requesterRole = await GetEmployeeRoleAsync(leaveRequest.RequesterId, db);
        if (requesterRole is null) return TypedResults.NotFound();
        if (!CanActionLeaveRequest(principal, requesterRole.Value)) return TypedResults.Forbid();

        if (leaveRequest.Status != LeaveRequestStatus.Pending)
        {
            return TypedResults.BadRequest($"Cannot action a leave request with status {leaveRequest.Status}.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return TypedResults.BadRequest("A reason is required to reject a leave request.");
        }

        var caller = await GetCallerEmployeeAsync(principal, db);
        if (caller is null) return TypedResults.NotFound();

        leaveRequest.Status = LeaveRequestStatus.Rejected;
        leaveRequest.ApproverEmployeeId = caller.Id;
        leaveRequest.DecisionReason = request.Reason;
        await db.SaveChangesAsync();

        return TypedResults.Ok(ToDetailDto(leaveRequest));
    }

    private static async Task<Results<Ok<LeaveRequestDetailDto>, NotFound, BadRequest<string>, ForbidHttpResult>> RequestDelayAsync(
        int id, RequestDelayRequest request, ClaimsPrincipal principal, AppDbContext db)
    {
        var leaveRequest = await db.LeaveRequests.FirstOrDefaultAsync(l => l.Id == id);
        if (leaveRequest is null) return TypedResults.NotFound();

        var requesterRole = await GetEmployeeRoleAsync(leaveRequest.RequesterId, db);
        if (requesterRole is null) return TypedResults.NotFound();
        if (!CanActionLeaveRequest(principal, requesterRole.Value)) return TypedResults.Forbid();

        if (leaveRequest.Status != LeaveRequestStatus.Pending)
        {
            return TypedResults.BadRequest($"Cannot action a leave request with status {leaveRequest.Status}.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return TypedResults.BadRequest("A reason is required to request a delay.");
        }

        var caller = await GetCallerEmployeeAsync(principal, db);
        if (caller is null) return TypedResults.NotFound();

        leaveRequest.Status = LeaveRequestStatus.DelayRequested;
        leaveRequest.ApproverEmployeeId = caller.Id;
        leaveRequest.DecisionReason = request.Reason;
        leaveRequest.RequestedDelayDate = request.TargetDate;
        await db.SaveChangesAsync();

        return TypedResults.Ok(ToDetailDto(leaveRequest));
    }

    private static bool IsPrivileged(ClaimsPrincipal principal) =>
        principal.IsInRole(nameof(Role.Admin)) || principal.IsInRole(nameof(Role.Manager)) || principal.IsInRole(nameof(Role.Supervisor));

    private static async Task<Employee?> GetCallerEmployeeAsync(ClaimsPrincipal principal, AppDbContext db)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return userId is null ? null : await db.Employees.FirstOrDefaultAsync(e => e.UserId == userId);
    }

    private static async Task<Role?> GetEmployeeRoleAsync(int employeeId, AppDbContext db)
    {
        var roleName = await db.Employees
            .Where(e => e.Id == employeeId && e.UserId != null)
            .Select(e => db.Roles
                .Where(r => db.UserRoles.Any(ur => ur.UserId == e.UserId && ur.RoleId == r.Id))
                .Select(r => r.Name)
                .FirstOrDefault())
            .FirstOrDefaultAsync();

        return roleName is null ? null : Enum.Parse<Role>(roleName);
    }

    // Strict linear seniority used for approver routing: Employee < Supervisor < Manager < Admin.
    // Do NOT use (int)role here — the enum's declared order in Models/Enums.cs
    // (Admin=0, Manager=1, Supervisor=2, Employee=3) is the exact reverse of seniority rank.
    private static int GetRoleRank(Role role) => role switch
    {
        Role.Employee => 0,
        Role.Supervisor => 1,
        Role.Manager => 2,
        Role.Admin => 3,
        _ => 0
    };

    private static int GetCallerRoleRank(ClaimsPrincipal principal)
    {
        if (principal.IsInRole(nameof(Role.Admin))) return 3;
        if (principal.IsInRole(nameof(Role.Manager))) return 2;
        if (principal.IsInRole(nameof(Role.Supervisor))) return 1;
        return 0;
    }

    private static bool CanActionLeaveRequest(ClaimsPrincipal principal, Role requesterRole) =>
        GetCallerRoleRank(principal) > GetRoleRank(requesterRole);

    private static LeaveRequestDetailDto ToDetailDto(LeaveRequest l) => new(
        l.Id, l.RequesterId, l.StartDate, l.EndDate, l.Reason, l.Status,
        l.ApproverEmployeeId, l.DecisionReason, l.RequestedDelayDate, l.CreatedAt, l.UpdatedAt);
}
```

Three points worth flagging explicitly:
- `IsPrivileged`/`GetCallerEmployeeAsync` are verbatim duplicates of `TaskEndpoints`'s private helpers of the same name (Context item 7) — intentional, per this codebase's no-cross-feature-service convention, not an oversight.
- `GetEmployeeRoleAsync`'s nested subquery (`db.Roles.Where(...).Select(...).FirstOrDefault()` inside an outer `.Select()`) is the same correlated-subquery shape `UserEndpoints.GetUsersAsync` already uses successfully (Context item 8) — adapted to filter by one `employeeId` instead of projecting for every user.
- `CanActionLeaveRequest`/`GetCallerRoleRank` do **not** need an explicit `principal.IsInRole(nameof(Role.Admin))` bypass the way `TaskEndpoints.UpdateCommentAsync`/`DeleteCommentAsync` do (Context item 7) — Admin is modeled as rank 3, the ceiling, so it naturally outranks every valid requester role (0–2) without a special case. See Edge Cases for why this must stay a real rank comparison, not be "simplified" into a bypass.

### 8 — Wire up the endpoint group

**File: `EmpoloyeeManagment/Program.cs`**

Add after line 156 (`app.MapClientEndpoints();`):

```csharp
app.MapLeaveEndpoints();
```

### 9 — Update docs

**File: `README.md`** — add a new Features bullet after line 14 (the Visits bullet), before line 15 (Dashboards): a short description of submit/edit/delete-while-pending and the role-ranked approval routing, noting notification delivery is a separate not-yet-built feature. Add eight new rows to the API overview table after line 98 (the last existing row) for `POST /api/leaves`, `GET /api/leaves`, `GET /api/leaves/{id}`, `PATCH /api/leaves/{id}`, `DELETE /api/leaves/{id}`, `PATCH /api/leaves/{id}/accept`, `/reject`, `/request-delay`. Update the Roadmap paragraph (line 102) to note leave requests have shipped, that notification delivery remains a separate not-yet-planned feature, and that the per-employee monthly rollup still doesn't include leave data pending a `dashboards` follow-up. Also update the Project structure block (lines 31–34): `Data/AppDbContext.cs` line's feature list, `Models/` line, `Dtos/` line, `Endpoints/` line — each gains a `LeaveRequest`/`Leaves`/`LeaveEndpoints` mention.

**File: `CLAUDE.md`** — extend the Architecture paragraph at line 26 with a sentence on the new `LeaveRequester` policy and the `CanActionLeaveRequest` row-level rank check (naming it explicitly as *not* a team/location scope, unlike `ClientEndpoints`/`StatisticsEndpoints`). Add `LeaveRequest` (with its two `Employee` FKs and `Status` values) to the Data model paragraph at line 40. Insert a new bolded paragraph after line 46 (the Visits paragraph), before line 48 (CORS), titled "**Leave requests — fully shipped**", describing `LeaveEndpoints`, the `LeaveRequester`/`TaskAssigner` policy split, the rank-based routing, the requester-only-while-`Pending` edit/delete rule, and the deliberate absence of any notification hook. Update the Project layout block (lines 58–60): `Models/` line gains `LeaveRequest`; `Dtos/` line gains `Leaves`; `Endpoints/` line gains `LeaveEndpoints`. Revise the Known v1 gaps paragraph (line 71) to replace the "leaves feature doesn't exist yet" clause with a note that leave requests have shipped, that notification delivery is still out of scope, and that the dashboards monthly rollup still omits leave data pending its own follow-up (leave the rest of that paragraph — the attendance-history-table gap and the Supervisor "team" heuristic — unchanged).

**File: `.squad/plans/leaves/00-overview.md`** — fill in the Stories table (this story, `07`, depends on `roles` Story 01) and the Dependency notes, matching the format already used by [`../tasks/00-overview.md`](../tasks/00-overview.md) and [`../dashboards/00-overview.md`](../dashboards/00-overview.md) (both use a `NN | File | Title | Depends on` table with no separate "Tracker id" column, since this project's tracker type is `none`).

---

## Edge Cases & Failure Modes

- **Approver routing is a strict linear rank, not a per-role lookup table.** `GetRoleRank(Role)` (task 7) encodes `Employee=0 < Supervisor=1 < Manager=2 < Admin=3`, and `CanActionLeaveRequest` requires the caller's rank to be *strictly greater* than the requester's. **Do not cast `(int)role`** — the enum's declared order in `Models/Enums.cs` (`Admin, Manager, Supervisor, Employee` → default ordinal values 0,1,2,3) is the exact reverse of seniority rank; casting would silently invert every routing decision (e.g. it would let an Employee action a Manager's request, and block Admin from actioning anything). The explicit `switch` in task 7 is required.
- **Self-approval and peer-approval are blocked for free.** Because the rank check requires strict inequality, a caller can never action a request from someone at their own rank (Manager-vs-Manager, Supervisor-vs-Supervisor) or their own request — this falls out of the rank model without a separate "is this my own request" check. Cover this explicitly in the Test Plan.
- **`AdminAuthorizationHandler` does not need an explicit bypass here, and should not get one.** Unlike `TaskEndpoints.UpdateCommentAsync`/`DeleteCommentAsync` (which need an explicit `principal.IsInRole(nameof(Role.Admin))` because the handler only intercepts policy-based checks, not manual ones — `TaskEndpoints.cs` lines 262/279), `CanActionLeaveRequest` naturally admits Admin because rank 3 is higher than every valid requester rank (0–2). This only holds because Admin is modeled as a real participant in the rank order, not a bypass wildcard — do not "simplify" this into an `IsInRole(Admin) ||` short-circuit; that would be redundant with, and easier to get subtly wrong than, the rank check already in place.
- **An Admin as requester has no possible approver.** Rank(Admin) = 3 is the ceiling; if `LeaveRequest.RequesterId` ever resolves to an Admin-role employee — possible only because `AdminAuthorizationHandler` lets an Admin bypass the `LeaveRequester` policy on `POST /api/leaves`, the same accepted quirk already documented for `TaskAssigner`/`EmployeeOnly` in `.squad/plans/tasks/02-story-task-assignment-and-comments.md` Edge Cases ("Employee-only routes are, in practice, Employee-or-Admin") — that request can never be accepted/rejected/delayed by anyone (every rank comparison fails). It can still be edited/deleted by its own requester while `Pending`, since that check never goes through rank. Not a bug to fix here — the source doc never describes Admin as a requester.
- **First two-FKs-to-the-same-entity-type relationship in this codebase.** Every prior multi-FK entity (`TaskComment` → `TaskItem`+`Employee`, `Visit` → `Client`+`Location`) points at two *different* principal types. `LeaveRequest.RequesterId` and `LeaveRequest.ApproverEmployeeId` (task 3) both point at `Employee`. The two separate `HasOne<Employee>().WithMany().HasForeignKey(...)` calls (each naming a distinct FK property, neither declaring a navigation property) is valid, standard EF Core — but since there's no precedent for this exact shape in this codebase, double-check the generated migration (task 4) creates **two** independent FK constraints, not one merged constraint or a build error.
- **`ApproverEmployeeId`/`DecisionReason`/`RequestedDelayDate` are all null until an action is taken.** `CreateLeaveRequestAsync` (task 7) never sets them. Confirm `GET /api/leaves/{id}` on a fresh `Pending` request returns `null` for all three, not an empty string or a default date.
- **`DelayRequested` is a terminal status with no further transition.** The acceptance criteria's `Status` enum lists it alongside `Accepted`/`Rejected` as one of four values, with no "accept/reject the delay" follow-up endpoint described anywhere in the source doc. Once a request is `DelayRequested`, this story provides no way to move it anywhere else — not even back to `Pending`, and it can no longer be edited/deleted (that requires `Pending`). Whether the employee needs to submit a brand-new `POST /api/leaves` for the delayed date is not answered by the source doc — flagged as an open product question, not resolved here.
- **Edit/delete ownership check returns `403`, not `404`, for a non-owner — including one who could never have discovered the id via `GET`.** Mirrors `TaskEndpoints.UpdateCommentAsync`/`DeleteCommentAsync` (lines 254–285), which check authorship without first re-checking whether the caller could otherwise view the parent resource. A non-privileged Employee attempting `PATCH /api/leaves/{id}` for another employee's request (an id their own scoped `GET /api/leaves` would never surface) gets the same `403` a Manager would get editing a different employee's request — a minor, deliberate, precedent-matching information-disclosure tradeoff (confirms the id exists), not a new one introduced by this story.
- **Caller has no linked `Employee` row.** Same defensive handling as `TaskEndpoints.GetCallerEmployeeAsync` (lines 290–294) — should not occur in practice (every `ApplicationUser`-creating code path also creates a linked `Employee`), but every handler that calls it treats a `null` result as `404`.
- **`EndDate` before `StartDate`.** Rejected with `400` on both create and edit (task 7) — the only new input-shape validation this story adds beyond required-reason checks. Not a validation this codebase has needed before (`Visit.DateTime`/`TaskItem.DueDateTime` are single dates, not ranges) — necessary here since `LeaveRequest` is the first entity in this codebase with a date range.
- **Requester's role cannot be resolved (`GetEmployeeRoleAsync` returns `null`).** Should not occur — every `Employee` with a non-null `UserId` has exactly one Identity role assigned at creation (`AuthEndpoints.SignupAsync`/`UserEndpoints.CreateUserAsync`, both calling `AddToRoleAsync`) — but `AcceptLeaveRequestAsync`/`RejectLeaveRequestAsync`/`RequestDelayAsync` (task 7) treat it as `404`, the same defensive style as the other null checks above.
- **Multiple roles on one user.** `GetEmployeeRoleAsync`'s `.FirstOrDefault()` (task 7) picks an arbitrary one if a user somehow holds more than one Identity role — the same pre-existing limitation as `UserEndpoints.GetUsersAsync` (line 33), not a new gap introduced here.
- **No team/location scoping for `GET /api/leaves`.** Unlike `ClientEndpoints`/`StatisticsEndpoints` (scoped to a Supervisor's assigned client via `AssignedLocationId`), this story uses the same all-or-own superset visibility as `TaskEndpoints`/`VisitEndpoints`, since the source doc's leave routing is purely role-based, never location-based. Documented here so it isn't mistaken for an inconsistency with Clients/Statistics.

---

## Test Plan

This repo has no automated test project yet (`CLAUDE.md` line 22: "There are no automated tests in this repo yet; verification is manual via Swagger"). This story follows the same manual-Swagger-walkthrough pattern the `tasks`/`visits` stories used.

1. **Migration applies cleanly** — `dotnet ef database update --project EmpoloyeeManagment`; confirm `LeaveRequests` exists with two FK constraints to `Employees`, and no other table changed.
2. **Submit + role gating** — as an Employee, `POST /api/leaves` with valid `StartDate`/`EndDate`/`Reason`; confirm `201`, `Status: "Pending"`, and `RequesterId` equal to the caller's own employee id. Repeat as a Supervisor and as a Manager; confirm `201` for both.
3. **Date validation** — `POST /api/leaves` with `EndDate` before `StartDate`; confirm `400`.
4. **List/detail scoping** — as the requester Employee from step 2, `GET /api/leaves` shows only their own request. As a *different*, non-privileged Employee, `GET /api/leaves` is empty and `GET /api/leaves/{id}` for step 2's request returns `404`. As Admin/Manager/Supervisor, `GET /api/leaves` shows all three requests from step 2.
5. **Edit/delete ownership** — as the requester, `PATCH /api/leaves/{id}` with new dates/reason; confirm `200` and the fields round-trip on a follow-up `GET`. As a *different* employee — including an Admin — attempt the same `PATCH`; confirm `403`. Repeat the `403` check for `DELETE`.
6. **Approver routing — Employee's request.** Create a fresh Employee-submitted request. `PATCH /api/leaves/{id}/accept` as a Supervisor; confirm `200`. Repeat with a second fresh Employee request, actioned by a Manager; confirm `200`. Repeat with a third, actioned by an Admin; confirm `200`.
7. **Approver routing — Supervisor's request.** As a Supervisor, submit a request. Attempt `accept` as a *different* Supervisor; confirm `403`. Attempt as a Manager; confirm `200`. Repeat with a fresh Supervisor-submitted request, actioned by Admin; confirm `200`.
8. **Approver routing — Manager's request.** As a Manager, submit a request (Request A). Attempt `accept` as a Supervisor; confirm `403`. Attempt as a *different* Manager; confirm `403`. Leave Request A `Pending` — do not accept it yet (step 9 needs it still pending).
9. **Self-action blocked** — using Request A from step 8 (still `Pending`), attempt `PATCH /api/leaves/{id}/accept` as the *same* Manager who submitted it; confirm `403`. Then, as Admin, accept Request A; confirm `200` (completing step 8's routing check).
10. **Reject requires a reason** — from a valid approver, `PATCH /api/leaves/{id}/reject` with an empty `Reason` on a fresh `Pending` request; confirm `400`. Retry with a non-empty reason; confirm `200`, `Status: "Rejected"`, `DecisionReason` round-trips, `ApproverEmployeeId` set to the actor.
11. **Request-delay requires a target date and reason** — omit `Reason` on a fresh `Pending` request; confirm `400`. Retry with both `TargetDate` and `Reason`; confirm `200`, `Status: "DelayRequested"`, and `RequestedDelayDate`/`DecisionReason` round-trip.
12. **Already-actioned request** — attempt `accept`, `reject`, and `request-delay` again on any request from steps 6–11 that is no longer `Pending`; confirm `400` for each. Attempt `PATCH`/`DELETE` (edit/delete) on the same; confirm `400`.
13. **Regression** — re-run the `roles` (Story 01) and `tasks` (Story 02) walkthroughs end to end to confirm nothing existing broke; confirm `ApiCallLogs` rows are written for the new `/api/leaves/*` calls.

---

## Migration / Rollback

One additive EF Core migration (`AddLeaveRequests`, task 4) adding the `LeaveRequests` table with two FK constraints to `Employees` (`RequesterId` required, `ApproverEmployeeId` nullable) — no changes to any existing table or column.

**Half-applied migration.** If `dotnet ef database update` fails partway, re-running it is safe — EF Core migrations are idempotent per-migration; no manual cleanup needed (same as every prior story in this codebase).

**Rollback.** `dotnet ef database update 20260901152214_AddVisitClientLocationForeignKeys --project EmpoloyeeManagment` reverts the schema to the prior migration (dropping `LeaveRequests`), then delete the migration files and revert the code changes: `Program.cs`'s `LeaveRequester` policy (task 5) and `MapLeaveEndpoints()` call (task 8), `Data/AppDbContext.cs`'s `LeaveRequests` `DbSet` and configuration block (task 3), `Models/Enums.cs`'s `LeaveRequestStatus` enum (task 1), and the new `Models/LeaveRequest.cs`, `Dtos/Leaves/LeaveDtos.cs`, `Endpoints/LeaveEndpoints.cs` files.

---

## Verification Steps

1. **Backend builds:** `dotnet build` from the repo root (or `EmpoloyeeManagment/`).
2. **Database check:** `dotnet ef database update --project EmpoloyeeManagment` — confirm only the new `AddLeaveRequests` migration applies.
3. **Run:** `dotnet run --project EmpoloyeeManagment`, open `/swagger`, and execute the full Test Plan above (steps 1–13).
4. **Regression:** confirm `ApiCallLogs` rows are still being written for the new `/api/leaves/*` calls (sanity check that `Middleware/ApiLoggingMiddleware.cs`, unmodified by this story, still wraps the new endpoints correctly).

---

## Done Criteria

- [ ] `LeaveRequestStatus` enum added to `Models/Enums.cs` (`Pending`/`Accepted`/`Rejected`/`DelayRequested`).
- [ ] `LeaveRequest` model added (`RequesterId`, `StartDate`, `EndDate`, `Reason`, `Status`, `ApproverEmployeeId`, `DecisionReason`, `RequestedDelayDate`, `CreatedAt`, `UpdatedAt`); `LeaveRequests` DbSet and EF configuration added to `AppDbContext` with two independent FKs to `Employee`; one additive migration applied.
- [ ] `LeaveRequester` authorization policy (Employee/Manager/Supervisor) registered; accept/reject/request-delay reuse the existing `TaskAssigner` policy.
- [ ] `POST /api/leaves` creates a `Pending` request with `RequesterId` resolved server-side from the caller, never client input; rejects `EndDate < StartDate` with `400`.
- [ ] `GET /api/leaves`, `GET /api/leaves/{id}` implemented with Employee-sees-own / Admin-Manager-Supervisor-see-all scoping.
- [ ] `PATCH /api/leaves/{id}`, `DELETE /api/leaves/{id}` implemented — only the original requester, only while `Pending`, `403` otherwise.
- [ ] `PATCH /api/leaves/{id}/accept`, `/reject`, `/request-delay` implemented with the rank-based approver routing (`Employee < Supervisor < Manager < Admin`, strict inequality); reject requires a reason, request-delay requires a target date + reason.
- [ ] `README.md` API table/Roadmap/Project structure and `CLAUDE.md` (Architecture, Data model, new Leave requests paragraph, Project layout, Known v1 gaps) updated.
- [ ] `.squad/plans/leaves/00-overview.md` updated with this story's row and dependency notes.
- [ ] Full manual Test Plan (steps 1–13) passes against a local run.
