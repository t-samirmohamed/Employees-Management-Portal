# Story 02 — Task Assignment Workflow & Comments

## Prerequisites

- Story 01 completed: [`../roles/01-story-role-model-and-admin-user-management.md`](../roles/01-story-role-model-and-admin-user-management.md) — this story depends directly on the `Role` enum, the JWT role claim, the `AdminOnly` policy, and `AdminAuthorizationHandler` it introduced. Verified already implemented in the current codebase: `Models/Enums.cs` lines 23–29 (`Role` enum), `Program.cs` lines 76–80 (`AddAuthorization` + handler registration), `Authorization/AdminAuthorizationHandler.cs` (whole file).
- Background reading: [`../employees/00-current-state.md`](../employees/00-current-state.md) and [`../auth/00-current-state.md`](../auth/00-current-state.md) — the existing `Employee`/`ApplicationUser` shape this story builds on.
- Coordination note for the (not-yet-planned) `visits` story — see [`../../stories/visits/visit-tracking-status/intake.md`](../../stories/visits/visit-tracking-status/intake.md): that story will (a) add the `Visits` table and the FK constraint onto `TaskItem.RelatedVisitId` that this story deliberately omits, and (b) extend `Services/IEmployeeAttendanceService` (introduced by this story) to also account for an active accepted Visit. Do not duplicate the attendance-recalculation logic when planning `visits` — call the service this story adds.
- Story 03 (`03-story-task-attachments.md`, in this same folder) depends on this story — it adds routes to the `Endpoints/TaskEndpoints.cs` file this story creates.

---

## Story Goal

Give Supervisors (and Managers/Admins) a way to assign and reassign Tasks to Employees, give Employees a way to accept or reject their own assigned Tasks (with a reason on reject), and make the Task's `Status` a fully server-computed field driven by those actions. Concretely:

1. A `TaskItem` entity exists with `Name`, `DueDateTime`, `AssigneeId` (→ `Employee`), `RelatedVisitId` (nullable, unconstrained placeholder for the future `visits` story), `Notes`, `AttendanceRequired`, and a server-computed `Status`.
2. `POST /api/tasks` (Admin/Manager/Supervisor) creates a Task in `New` status.
3. `PATCH /api/tasks/{id}` (Admin/Manager/Supervisor) reassigns a Task to a different Employee and resets it to `New` — the new assignee has not accepted anything yet.
4. `PATCH /api/tasks/{id}/accept` and `PATCH /api/tasks/{id}/reject` (Employee-only, and only the task's own assignee) move a `New` task to `InProgress` or `Rejected`; reject requires a non-empty reason string.
5. `PATCH /api/tasks/{id}/cancel` and `PATCH /api/tasks/{id}/complete` (Admin/Manager/Supervisor) are new, minimal endpoints added because the acceptance criteria's own `Status` enum requires `Cancelled` and `Done` to be reachable states, but the acceptance criteria's endpoint list only spells out create/reassign/accept/reject — mirrors how Story 01 added `POST /api/users` as "the concrete mechanism behind" a capability the spec implied but didn't give an endpoint for (see that story's task 6 commentary).
6. `GET /api/tasks` and `GET /api/tasks/{id}` are role-scoped: an Employee sees/can open only Tasks where they are the assignee; Admin/Manager/Supervisor see all Tasks. See Edge Cases for why "their team's" becomes "all" for those three roles.
7. Comments (`POST`/`PATCH`/`DELETE /api/tasks/{id}/comments...`) are visible to anyone who can already view the task; only a comment's own author (or an Admin) can update or delete it.
8. A new shared `Services/IEmployeeAttendanceService` recalculates `Employee.AttendanceStatus` (`InOffice` ⇄ `OutOfOffice`) whenever an `AttendanceRequired` task starts or stops being `InProgress` for its assignee — built once, here, so the `visits` story can call the same service instead of re-implementing the rule (per that story's own acceptance criteria: "coordinate ... rather than built twice").

**Not in scope for this story** (do not build these here):
- The `Visits` entity/endpoints and the FK constraint on `RelatedVisitId` — separate `visits` story; this story only adds the bare nullable column (see Edge Cases).
- Attachments — separate `03-story-task-attachments.md` in this folder.
- A general "edit task" endpoint. `PATCH /api/tasks/{id}` is specifically **reassignment** (changes only `AssigneeId`, per the acceptance criteria's literal wording), not a full field editor — `Name`/`DueDateTime`/`Notes`/`AttendanceRequired` are set at creation only. This matches the codebase's existing preference for minimal update surfaces (compare `Employee`'s deliberately-absent general update, noted in `CLAUDE.md`'s "Known v1 gaps", line 65).
- Real reporting-hierarchy/team modeling for Supervisors. No `SupervisorId`/`ManagerId`/`TeamId` field exists anywhere on `Employee` today (confirmed: `Models/Employee.cs` has no such field, and a repo-wide search for `SupervisorId|ManagerId|TeamId|ReportsTo` finds nothing). Inventing one is out of scope for this story's acceptance criteria, which only lists Task fields. See Edge Cases for the concrete, deliberately broader interpretation used instead.
- Query filters (`status`, date range) on `GET /api/tasks` beyond the required role-based row scoping — not asked for in the acceptance criteria; can be added later as a non-breaking, additive change.

---

## Context — Read These Files First

1. [`EmpoloyeeManagment/obj/Debug/net10.0/EmpoloyeeManagment.GlobalUsings.g.cs`](../../../EmpoloyeeManagment/obj/Debug/net10.0/EmpoloyeeManagment.GlobalUsings.g.cs) line 17 — `global using System.Threading.Tasks;` is a **project-wide global using**. **Do not name the new entity `Task` or the new enum `TaskStatus`** — both collide with BCL types (`System.Threading.Tasks.Task`, `System.Threading.Tasks.TaskStatus`) that are already in scope, unqualified, in every file of this project (every `async Task`/`Task<T>` signature depends on this). This story uses `TaskItem` and `TaskItemStatus` throughout specifically to avoid this. Confirmed no `TaskItem`/`TaskComment` symbol exists anywhere yet (repo-wide search came back empty).
2. [`EmpoloyeeManagment/Models/Enums.cs`](../../../EmpoloyeeManagment/Models/Enums.cs) — whole file (29 lines). The exact `public enum X { ... }` style (no `[Flags]`, no explicit backing values) the new `TaskItemStatus` enum (task 1) must match, appended after `Role` (line 29).
3. [`EmpoloyeeManagment/Models/Employee.cs`](../../../EmpoloyeeManagment/Models/Employee.cs) — whole file (14 lines). The plain-class-with-auto-properties style (no navigation properties, defaults inline via `= ...`) that `TaskItem`/`TaskComment` (tasks 2–3) must match.
4. [`EmpoloyeeManagment/Data/AppDbContext.cs`](../../../EmpoloyeeManagment/Data/AppDbContext.cs) — whole file (49 lines). `DbSet` declarations at lines 9–10 (add two more here). The `Employee` configuration block, lines 23–39, is the exact FK pattern to replicate for `TaskItem.AssigneeId → Employee`: `entity.HasOne<Employee>().WithMany().HasForeignKey(...).OnDelete(...)` with **no navigation property on either side** (`Employee` has no `ICollection<TaskItem>` — this codebase never adds nav properties for its FKs, e.g. `Employee.UserId → ApplicationUser` at lines 35–38 has none either). The `ApiCallLog` block, lines 41–47, shows the convention for `.HasMaxLength(...)` on every bounded string column and leaving unbounded ones (compare `ApiCallLog.QueryString`, which has no `.HasMaxLength()` call at all) as plain `nvarchar(max)`.
5. [`EmpoloyeeManagment/Migrations/AppDbContextModelSnapshot.cs`](../../../EmpoloyeeManagment/Migrations/AppDbContextModelSnapshot.cs) lines 135–187 — the current persisted shape of `Employee` (confirms `Id` is `int` identity, exactly the type `TaskItem.AssigneeId` must match). Also confirms (this whole file) that the most recent migration is `20260718091538_LinkEmployeeToUserAndUniqueEmail` (see the `Migrations/` directory listing) — the Rollback target if this story's migration needs to be reverted.
6. [`EmpoloyeeManagment/Endpoints/EmployeeEndpoints.cs`](../../../EmpoloyeeManagment/Endpoints/EmployeeEndpoints.cs) — whole file (95 lines). Lines 11–22: the `MapGroup(...).WithTags(...).RequireAuthorization()` group setup, then per-route `.RequireAuthorization("AdminOnly")` layered **on top of** the group's bare requirement for `activate`/`deactivate` (lines 18–19) — this is the exact pattern `TaskEndpoints` (task 9) reuses for `TaskAssigner`/`EmployeeOnly`. Lines 75–84 (`SetStatusAsync`) and 86–94 (`ToDetailDto`) are the shared-private-helper convention this story's `ToDetailDto`/`GetCallerEmployeeAsync`/`CanViewTaskAsync` helpers follow.
7. [`EmpoloyeeManagment/Endpoints/AuthEndpoints.cs`](../../../EmpoloyeeManagment/Endpoints/AuthEndpoints.cs) lines 89–102 (`LogoutAsync`) — the only existing precedent for a `ClaimsPrincipal principal` parameter bound directly in a minimal-API handler and read via `principal.FindFirstValue(ClaimTypes.NameIdentifier)` (line 93). This story's `GetCallerEmployeeAsync` helper (task 8) reuses this exact call to resolve the caller's `ApplicationUser.Id`, then joins to `Employee.UserId` to get their `Employee.Id`. Line 1 (`using System.Security.Claims;`) is required in the new `TaskEndpoints.cs` too — it is **not** a global using (confirmed absent from `GlobalUsings.g.cs`).
8. [`EmpoloyeeManagment/Authorization/AdminAuthorizationHandler.cs`](../../../EmpoloyeeManagment/Authorization/AdminAuthorizationHandler.cs) — whole file (20 lines). This handler makes an Admin succeed **any pending `IAuthorizationRequirement`** — i.e. any `RequireAuthorization("...")` policy check. It does **not** run for manual, in-handler ownership checks like `comment.AuthorEmployeeId != caller.Id` (task 8's `UpdateCommentAsync`/`DeleteCommentAsync`) — those never go through `IAuthorizationHandler` at all. Where this story wants Admin to override a manual check, it must say so explicitly in the handler body (`|| principal.IsInRole(nameof(Role.Admin))`) — see Edge Cases.
9. [`EmpoloyeeManagment/Program.cs`](../../../EmpoloyeeManagment/Program.cs) — whole file (152 lines). Line 9 (`using EmpoloyeeManagment.Services;`) and line 5 (`using EmpoloyeeManagment.Endpoints;`) already cover the new files this story adds — no new `using` lines needed in `Program.cs`. Lines 76–80 (`AddAuthorization`) — extend with two more `AddPolicy` calls (task 7). Line 90 (`AddScoped<ITokenService, TokenService>();`) — add the new service registration directly after it (task 7). Lines 146–149 — add `app.MapTaskEndpoints();` after line 149 (task 10).
10. [`EmpoloyeeManagment/Services/ITokenService.cs`](../../../EmpoloyeeManagment/Services/ITokenService.cs) and [`TokenService.cs`](../../../EmpoloyeeManagment/Services/TokenService.cs) — whole files. The interface-plus-primary-constructor-class pair convention (`public class TokenService(IConfiguration configuration) : ITokenService`) that `IEmployeeAttendanceService`/`EmployeeAttendanceService` (task 5) must match.
11. [`EmpoloyeeManagment/Dtos/Employees/EmployeeDtos.cs`](../../../EmpoloyeeManagment/Dtos/Employees/EmployeeDtos.cs) (29 lines) and [`Dtos/Users/UserDtos.cs`](../../../EmpoloyeeManagment/Dtos/Users/UserDtos.cs) (11 lines) — the one-file-per-feature, one-record-per-shape DTO convention `Dtos/Tasks/TaskDtos.cs` (task 6) must match.
12. Grep for `RequireRole` and `policy\.` in `EmpoloyeeManagment/` — confirms the only existing policy today is `AdminOnly` (`Program.cs` line 78, `policy.RequireRole(nameof(Role.Admin))`), a single-role policy. `RequireRole` accepts multiple role names as an OR condition — this story's `TaskAssigner` policy (task 7) is the first policy in this codebase to use that.
13. [`../roles/01-story-role-model-and-admin-user-management.md`](../roles/01-story-role-model-and-admin-user-management.md) — read in full as the structural and tonal precedent for this story (per the planning process's "find a similar precedent" step). Note in particular how its task 6 justifies adding `POST /api/users` beyond the literal spec text — this story's tasks 9 (`cancel`/`complete` endpoints) follow the identical justification pattern.
14. [`README.md`](../../../README.md) lines 58–76 (API table + Roadmap line) and [`CLAUDE.md`](../../../CLAUDE.md) lines 63–65 ("Known v1 gaps") — both need updating; see task 11.

---

## Product rules (from story)

| Area | Current behavior | New behavior |
|---|---|---|
| `TaskItem` | Doesn't exist | New entity: `Name`, `DueDateTime`, `AssigneeId`, `RelatedVisitId` (nullable, no FK yet), `Notes`, `AttendanceRequired`, `Status` (computed), `RejectionReason` (nullable) |
| `POST /api/tasks` | Doesn't exist | New — **Admin/Manager/Supervisor** (`TaskAssigner` policy) — creates a Task in `New` status |
| `PATCH /api/tasks/{id}` | Doesn't exist | New — **Admin/Manager/Supervisor** — reassigns `AssigneeId`, resets `Status` to `New`, clears `RejectionReason`; blocked if current status is `Done`/`Cancelled` |
| `PATCH /api/tasks/{id}/accept`, `/reject` | Don't exist | New — **Employee-only**, and only the task's own assignee; only valid from `New`. Reject requires a non-empty `Reason`, stored on `TaskItem.RejectionReason` |
| `PATCH /api/tasks/{id}/cancel` | Doesn't exist | New — **Admin/Manager/Supervisor** — valid from `New` or `InProgress` only |
| `PATCH /api/tasks/{id}/complete` | Doesn't exist | New — **Admin/Manager/Supervisor** — valid from `InProgress` only, and only when `RelatedVisitId is null` (visit-linked tasks will get `Done` from the `visits` story instead — see Edge Cases) |
| `GET /api/tasks`, `GET /api/tasks/{id}` | Don't exist | New — any authenticated user; Employee sees/opens only their own (`AssigneeId` resolves to their `Employee.Id`); Admin/Manager/Supervisor see all |
| `POST/PATCH/DELETE /api/tasks/{id}/comments...` | Don't exist | New — anyone who can view the task may add a comment; only the comment's author (or Admin) may update/delete it |
| `Employee.AttendanceStatus` | Set once at creation/signup, never recomputed | Recomputed to `OutOfOffice`/`InOffice` by `IEmployeeAttendanceService` whenever an `AttendanceRequired` task starts/stops being `InProgress` for its assignee — but only while the employee's current status is already `InOffice` or `OutOfOffice` (an `Absent`/`OnVacation` status set by some other process is left untouched) |

---

## Backend Tasks

`No frontend changes required in this repository` — the companion UI work is tracked in the separate `employee-management-web` repo per the intake's "Extra notes."

### 1 — Add the `TaskItemStatus` enum

**File: `EmpoloyeeManagment/Models/Enums.cs`**

Append after `Role` (after line 29), matching the existing style exactly:

```csharp
public enum TaskItemStatus
{
    New,
    InProgress,
    Rejected,
    Cancelled,
    Done
}
```

Named `TaskItemStatus`, not `TaskStatus` — see Context item 1. Serialized as a string automatically by the `JsonStringEnumConverter` already registered in `Program.cs` (lines 92–95), same as every other enum in this codebase.

### 2 — Add the `TaskItem` model

**Create file: `EmpoloyeeManagment/Models/TaskItem.cs`**

```csharp
namespace EmpoloyeeManagment.Models;

public class TaskItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime DueDateTime { get; set; }
    public int AssigneeId { get; set; }
    public int? RelatedVisitId { get; set; }
    public string? Notes { get; set; }
    public bool AttendanceRequired { get; set; }
    public TaskItemStatus Status { get; set; } = TaskItemStatus.New;
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

`RelatedVisitId` is a bare nullable `int` with **no navigation property and, per task 4, no FK constraint** — the `Visits` table doesn't exist yet (it's a separate, not-yet-planned story). No existence validation is performed on it anywhere in this story either — see Edge Cases.

### 3 — Add the `TaskComment` model

**Create file: `EmpoloyeeManagment/Models/TaskComment.cs`**

```csharp
namespace EmpoloyeeManagment.Models;

public class TaskComment
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public int AuthorEmployeeId { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
```

### 4 — Register the new DbSets and EF configuration

**File: `EmpoloyeeManagment/Data/AppDbContext.cs`**

Add two DbSets after line 10:

```csharp
public DbSet<TaskItem> Tasks => Set<TaskItem>();
public DbSet<TaskComment> TaskComments => Set<TaskComment>();
```

Add two new configuration blocks inside `OnModelCreating`, after the existing `builder.Entity<ApiCallLog>(...)` block (after line 47, before the closing brace on line 48):

```csharp
builder.Entity<TaskItem>(entity =>
{
    entity.Property(t => t.Name).HasMaxLength(200).IsRequired();
    entity.Property(t => t.Notes).HasMaxLength(2000);
    entity.Property(t => t.RejectionReason).HasMaxLength(1000);
    entity.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);

    entity.HasOne<Employee>()
        .WithMany()
        .HasForeignKey(t => t.AssigneeId)
        .OnDelete(DeleteBehavior.Restrict);
});

builder.Entity<TaskComment>(entity =>
{
    entity.Property(c => c.Text).IsRequired();

    entity.HasOne<TaskItem>()
        .WithMany()
        .HasForeignKey(c => c.TaskId)
        .OnDelete(DeleteBehavior.Cascade);

    entity.HasOne<Employee>()
        .WithMany()
        .HasForeignKey(c => c.AuthorEmployeeId)
        .OnDelete(DeleteBehavior.Restrict);
});
```

`DeleteBehavior.Restrict` on both FKs to `Employee` (matching the caution already implicit in `Employee` having no delete endpoint at all). `DeleteBehavior.Cascade` on `TaskComment.TaskId` — deleting a task should take its comments with it. `TaskComment.Text` has no `.HasMaxLength()` — left as unbounded `nvarchar(max)`, matching the `ApiCallLog.QueryString` precedent (Context item 4) for genuinely free-text fields. `RelatedVisitId` gets **no** `entity.Property(...)` call at all — a nullable `int` needs no explicit configuration, same as every other plain nullable value-typed column in this codebase.

### 5 — Create the EF Core migration

From the repo root:

```bash
dotnet ef migrations add AddTasksAndComments --project EmpoloyeeManagment
dotnet ef database update --project EmpoloyeeManagment
```

Confirm the generated migration only adds two new tables (`Tasks`, `TaskComments`) and touches nothing else — if it reports changes to `Employees`, `AspNetUsers`, or `ApiCallLogs`, stop and investigate before applying.

### 6 — Add the shared `IEmployeeAttendanceService`

**Create file: `EmpoloyeeManagment/Services/IEmployeeAttendanceService.cs`**

```csharp
namespace EmpoloyeeManagment.Services;

public interface IEmployeeAttendanceService
{
    Task RecalculateAsync(int employeeId);
}
```

**Create file: `EmpoloyeeManagment/Services/EmployeeAttendanceService.cs`**

```csharp
using EmpoloyeeManagment.Data;
using EmpoloyeeManagment.Models;
using Microsoft.EntityFrameworkCore;

namespace EmpoloyeeManagment.Services;

public class EmployeeAttendanceService(AppDbContext db) : IEmployeeAttendanceService
{
    public async Task RecalculateAsync(int employeeId)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId);
        if (employee is null) return;

        // Absent/OnVacation (set by a future leaves feature) takes precedence over
        // task-driven attendance; only ever toggle between InOffice and OutOfOffice.
        if (employee.AttendanceStatus != AttendanceStatus.InOffice && employee.AttendanceStatus != AttendanceStatus.OutOfOffice)
        {
            return;
        }

        var hasActiveAttendanceTask = await db.Tasks
            .AnyAsync(t => t.AssigneeId == employeeId && t.AttendanceRequired && t.Status == TaskItemStatus.InProgress);

        employee.AttendanceStatus = hasActiveAttendanceTask ? AttendanceStatus.OutOfOffice : AttendanceStatus.InOffice;
        await db.SaveChangesAsync();
    }
}
```

`RecalculateAsync` re-derives the status from scratch (queries for *any* currently-active attendance-required task) rather than blindly toggling — required so that completing one of two simultaneous attendance-required tasks doesn't incorrectly flip the employee back to `InOffice` while the other is still active. The `visits` story extends this same `AnyAsync` check to also cover an active accepted Visit — do not add a second, parallel recalculation path when that story is planned.

### 7 — Add authorization policies and register the new service

**File: `EmpoloyeeManagment/Program.cs`**

Extend the `AddAuthorization` block (lines 76–80):

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(nameof(Role.Admin)));
    options.AddPolicy("TaskAssigner", policy => policy.RequireRole(nameof(Role.Admin), nameof(Role.Manager), nameof(Role.Supervisor)));
    options.AddPolicy("EmployeeOnly", policy => policy.RequireRole(nameof(Role.Employee)));
});
builder.Services.AddSingleton<IAuthorizationHandler, AdminAuthorizationHandler>();
```

Add the service registration directly after line 90:

```csharp
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IEmployeeAttendanceService, EmployeeAttendanceService>();
```

No new `using` needed — `EmpoloyeeManagment.Services` is already imported at line 9.

`TaskAssigner` includes `Admin` explicitly even though `AdminAuthorizationHandler` would bypass it anyway — harmless redundancy, kept for readability. `EmployeeOnly` is literally what the acceptance criteria calls the accept/reject routes; note in Edge Cases that Admin can still call them, via the pre-existing app-wide bypass, not anything new here.

### 8 — Create the Tasks DTOs

**Create file: `EmpoloyeeManagment/Dtos/Tasks/TaskDtos.cs`**

```csharp
using EmpoloyeeManagment.Models;

namespace EmpoloyeeManagment.Dtos.Tasks;

public record CreateTaskRequest(
    string Name,
    DateTime DueDateTime,
    int AssigneeId,
    int? RelatedVisitId,
    string? Notes,
    bool AttendanceRequired);

public record ReassignTaskRequest(int NewAssigneeId);

public record RejectTaskRequest(string Reason);

public record AddCommentRequest(string Text);

public record UpdateCommentRequest(string Text);

public record TaskListItemDto(
    int Id,
    string Name,
    DateTime DueDateTime,
    int AssigneeId,
    int? RelatedVisitId,
    TaskItemStatus Status,
    bool AttendanceRequired);

public record TaskDetailDto(
    int Id,
    string Name,
    DateTime DueDateTime,
    int AssigneeId,
    int? RelatedVisitId,
    string? Notes,
    bool AttendanceRequired,
    TaskItemStatus Status,
    string? RejectionReason,
    DateTime CreatedAt);

public record TaskCommentDto(int Id, int AuthorEmployeeId, string Text, DateTime CreatedAt, DateTime? UpdatedAt);

public record TaskDetailResponse(TaskDetailDto Task, List<TaskCommentDto> Comments);
```

`TaskDetailResponse` wraps the base `TaskDetailDto` with its comments — only `GET /api/tasks/{id}` returns this shape; every mutation endpoint (create/reassign/accept/reject/cancel/complete) returns the plain `TaskDetailDto`, since threading a fresh comments query through every one of those handlers just to populate a field the caller isn't asking about would be unnecessary work. Story 03 extends `TaskDetailResponse` with an `Attachments` list — the name is deliberately not `TaskWithCommentsDto` so that extension isn't a rename.

### 9 — Create `Endpoints/TaskEndpoints.cs`

**Create file: `EmpoloyeeManagment/Endpoints/TaskEndpoints.cs`**

```csharp
using System.Security.Claims;
using EmpoloyeeManagment.Data;
using EmpoloyeeManagment.Dtos.Tasks;
using EmpoloyeeManagment.Models;
using EmpoloyeeManagment.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace EmpoloyeeManagment.Endpoints;

public static class TaskEndpoints
{
    public static IEndpointRouteBuilder MapTaskEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tasks").WithTags("Tasks").RequireAuthorization();

        group.MapGet("/", GetTasksAsync);
        group.MapGet("/{id:int}", GetTaskByIdAsync);
        group.MapPost("/", CreateTaskAsync).RequireAuthorization("TaskAssigner");
        group.MapPatch("/{id:int}", ReassignTaskAsync).RequireAuthorization("TaskAssigner");
        group.MapPatch("/{id:int}/accept", AcceptTaskAsync).RequireAuthorization("EmployeeOnly");
        group.MapPatch("/{id:int}/reject", RejectTaskAsync).RequireAuthorization("EmployeeOnly");
        group.MapPatch("/{id:int}/cancel", CancelTaskAsync).RequireAuthorization("TaskAssigner");
        group.MapPatch("/{id:int}/complete", CompleteTaskAsync).RequireAuthorization("TaskAssigner");
        group.MapPost("/{id:int}/comments", AddCommentAsync);
        group.MapPatch("/{id:int}/comments/{commentId:int}", UpdateCommentAsync);
        group.MapDelete("/{id:int}/comments/{commentId:int}", DeleteCommentAsync);

        return app;
    }

    private static async Task<Ok<List<TaskListItemDto>>> GetTasksAsync(ClaimsPrincipal principal, AppDbContext db)
    {
        var query = db.Tasks.AsNoTracking().AsQueryable();

        if (!IsPrivileged(principal))
        {
            var caller = await GetCallerEmployeeAsync(principal, db);
            if (caller is null) return TypedResults.Ok(new List<TaskListItemDto>());
            query = query.Where(t => t.AssigneeId == caller.Id);
        }

        var tasks = await query
            .OrderBy(t => t.DueDateTime)
            .Select(t => new TaskListItemDto(t.Id, t.Name, t.DueDateTime, t.AssigneeId, t.RelatedVisitId, t.Status, t.AttendanceRequired))
            .ToListAsync();

        return TypedResults.Ok(tasks);
    }

    private static async Task<Results<Ok<TaskDetailResponse>, NotFound>> GetTaskByIdAsync(int id, ClaimsPrincipal principal, AppDbContext db)
    {
        var task = await db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return TypedResults.NotFound();
        if (!await CanViewTaskAsync(task, principal, db)) return TypedResults.NotFound();

        var comments = await db.TaskComments.AsNoTracking()
            .Where(c => c.TaskId == id)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new TaskCommentDto(c.Id, c.AuthorEmployeeId, c.Text, c.CreatedAt, c.UpdatedAt))
            .ToListAsync();

        return TypedResults.Ok(new TaskDetailResponse(ToDetailDto(task), comments));
    }

    private static async Task<Results<Created<TaskDetailDto>, NotFound>> CreateTaskAsync(CreateTaskRequest request, AppDbContext db)
    {
        var assigneeExists = await db.Employees.AnyAsync(e => e.Id == request.AssigneeId);
        if (!assigneeExists) return TypedResults.NotFound();

        var task = new TaskItem
        {
            Name = request.Name,
            DueDateTime = request.DueDateTime,
            AssigneeId = request.AssigneeId,
            RelatedVisitId = request.RelatedVisitId,
            Notes = request.Notes,
            AttendanceRequired = request.AttendanceRequired,
            Status = TaskItemStatus.New,
            CreatedAt = DateTime.UtcNow
        };

        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        return TypedResults.Created($"/api/tasks/{task.Id}", ToDetailDto(task));
    }

    private static async Task<Results<Ok<TaskDetailDto>, NotFound, BadRequest<string>>> ReassignTaskAsync(
        int id, ReassignTaskRequest request, AppDbContext db, IEmployeeAttendanceService attendanceService)
    {
        var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return TypedResults.NotFound();

        if (task.Status is TaskItemStatus.Done or TaskItemStatus.Cancelled)
        {
            return TypedResults.BadRequest($"Cannot reassign a task with status {task.Status}.");
        }

        var assigneeExists = await db.Employees.AnyAsync(e => e.Id == request.NewAssigneeId);
        if (!assigneeExists) return TypedResults.NotFound();

        var previousAssigneeId = task.AssigneeId;
        var wasInProgress = task.Status == TaskItemStatus.InProgress;

        task.AssigneeId = request.NewAssigneeId;
        task.Status = TaskItemStatus.New;
        task.RejectionReason = null;
        await db.SaveChangesAsync();

        if (wasInProgress && task.AttendanceRequired)
        {
            await attendanceService.RecalculateAsync(previousAssigneeId);
        }

        return TypedResults.Ok(ToDetailDto(task));
    }

    private static async Task<Results<Ok<TaskDetailDto>, NotFound, BadRequest<string>>> AcceptTaskAsync(
        int id, ClaimsPrincipal principal, AppDbContext db, IEmployeeAttendanceService attendanceService)
    {
        var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return TypedResults.NotFound();

        var caller = await GetCallerEmployeeAsync(principal, db);
        if (caller is null || task.AssigneeId != caller.Id) return TypedResults.NotFound();

        if (task.Status != TaskItemStatus.New)
        {
            return TypedResults.BadRequest($"Cannot accept a task with status {task.Status}.");
        }

        task.Status = TaskItemStatus.InProgress;
        await db.SaveChangesAsync();

        if (task.AttendanceRequired)
        {
            await attendanceService.RecalculateAsync(task.AssigneeId);
        }

        return TypedResults.Ok(ToDetailDto(task));
    }

    private static async Task<Results<Ok<TaskDetailDto>, NotFound, BadRequest<string>>> RejectTaskAsync(
        int id, RejectTaskRequest request, ClaimsPrincipal principal, AppDbContext db)
    {
        var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return TypedResults.NotFound();

        var caller = await GetCallerEmployeeAsync(principal, db);
        if (caller is null || task.AssigneeId != caller.Id) return TypedResults.NotFound();

        if (task.Status != TaskItemStatus.New)
        {
            return TypedResults.BadRequest($"Cannot reject a task with status {task.Status}.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return TypedResults.BadRequest("A reason is required to reject a task.");
        }

        task.Status = TaskItemStatus.Rejected;
        task.RejectionReason = request.Reason;
        await db.SaveChangesAsync();

        return TypedResults.Ok(ToDetailDto(task));
    }

    private static async Task<Results<Ok<TaskDetailDto>, NotFound, BadRequest<string>>> CancelTaskAsync(
        int id, AppDbContext db, IEmployeeAttendanceService attendanceService)
    {
        var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return TypedResults.NotFound();

        if (task.Status is not (TaskItemStatus.New or TaskItemStatus.InProgress))
        {
            return TypedResults.BadRequest($"Cannot cancel a task with status {task.Status}.");
        }

        var wasInProgress = task.Status == TaskItemStatus.InProgress;
        task.Status = TaskItemStatus.Cancelled;
        await db.SaveChangesAsync();

        if (wasInProgress && task.AttendanceRequired)
        {
            await attendanceService.RecalculateAsync(task.AssigneeId);
        }

        return TypedResults.Ok(ToDetailDto(task));
    }

    private static async Task<Results<Ok<TaskDetailDto>, NotFound, BadRequest<string>>> CompleteTaskAsync(
        int id, AppDbContext db, IEmployeeAttendanceService attendanceService)
    {
        var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return TypedResults.NotFound();

        if (task.RelatedVisitId is not null)
        {
            return TypedResults.BadRequest("This task's status is derived from its linked visit and cannot be completed manually.");
        }

        if (task.Status != TaskItemStatus.InProgress)
        {
            return TypedResults.BadRequest($"Cannot complete a task with status {task.Status}.");
        }

        task.Status = TaskItemStatus.Done;
        await db.SaveChangesAsync();

        if (task.AttendanceRequired)
        {
            await attendanceService.RecalculateAsync(task.AssigneeId);
        }

        return TypedResults.Ok(ToDetailDto(task));
    }

    private static async Task<Results<Created<TaskCommentDto>, NotFound, BadRequest<string>>> AddCommentAsync(
        int id, AddCommentRequest request, ClaimsPrincipal principal, AppDbContext db)
    {
        var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return TypedResults.NotFound();
        if (!await CanViewTaskAsync(task, principal, db)) return TypedResults.NotFound();

        if (string.IsNullOrWhiteSpace(request.Text)) return TypedResults.BadRequest("Comment text is required.");

        var caller = await GetCallerEmployeeAsync(principal, db);
        if (caller is null) return TypedResults.NotFound();

        var comment = new TaskComment
        {
            TaskId = id,
            AuthorEmployeeId = caller.Id,
            Text = request.Text,
            CreatedAt = DateTime.UtcNow
        };
        db.TaskComments.Add(comment);
        await db.SaveChangesAsync();

        return TypedResults.Created($"/api/tasks/{id}/comments/{comment.Id}", ToCommentDto(comment));
    }

    private static async Task<Results<Ok<TaskCommentDto>, NotFound, ForbidHttpResult>> UpdateCommentAsync(
        int id, int commentId, UpdateCommentRequest request, ClaimsPrincipal principal, AppDbContext db)
    {
        var comment = await db.TaskComments.FirstOrDefaultAsync(c => c.Id == commentId && c.TaskId == id);
        if (comment is null) return TypedResults.NotFound();

        var caller = await GetCallerEmployeeAsync(principal, db);
        var isAuthor = caller is not null && comment.AuthorEmployeeId == caller.Id;
        if (!isAuthor && !principal.IsInRole(nameof(Role.Admin))) return TypedResults.Forbid();

        comment.Text = request.Text;
        comment.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return TypedResults.Ok(ToCommentDto(comment));
    }

    private static async Task<Results<NoContent, NotFound, ForbidHttpResult>> DeleteCommentAsync(
        int id, int commentId, ClaimsPrincipal principal, AppDbContext db)
    {
        var comment = await db.TaskComments.FirstOrDefaultAsync(c => c.Id == commentId && c.TaskId == id);
        if (comment is null) return TypedResults.NotFound();

        var caller = await GetCallerEmployeeAsync(principal, db);
        var isAuthor = caller is not null && comment.AuthorEmployeeId == caller.Id;
        if (!isAuthor && !principal.IsInRole(nameof(Role.Admin))) return TypedResults.Forbid();

        db.TaskComments.Remove(comment);
        await db.SaveChangesAsync();

        return TypedResults.NoContent();
    }

    private static bool IsPrivileged(ClaimsPrincipal principal) =>
        principal.IsInRole(nameof(Role.Admin)) || principal.IsInRole(nameof(Role.Manager)) || principal.IsInRole(nameof(Role.Supervisor));

    private static async Task<Employee?> GetCallerEmployeeAsync(ClaimsPrincipal principal, AppDbContext db)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return userId is null ? null : await db.Employees.FirstOrDefaultAsync(e => e.UserId == userId);
    }

    private static async Task<bool> CanViewTaskAsync(TaskItem task, ClaimsPrincipal principal, AppDbContext db)
    {
        if (IsPrivileged(principal)) return true;
        var caller = await GetCallerEmployeeAsync(principal, db);
        return caller is not null && task.AssigneeId == caller.Id;
    }

    private static TaskDetailDto ToDetailDto(TaskItem task) => new(
        task.Id, task.Name, task.DueDateTime, task.AssigneeId, task.RelatedVisitId,
        task.Notes, task.AttendanceRequired, task.Status, task.RejectionReason, task.CreatedAt);

    private static TaskCommentDto ToCommentDto(TaskComment comment) => new(
        comment.Id, comment.AuthorEmployeeId, comment.Text, comment.CreatedAt, comment.UpdatedAt);
}
```

Two points worth flagging explicitly since they're easy to get wrong:
- `IsPrivileged`/`CanViewTaskAsync` are **manual, in-handler** checks — the first time this codebase does row-level (ownership-based) authorization, as opposed to the route-level `RequireAuthorization("...")` policy gating used everywhere else. They are deliberately not expressed as policies because a policy can only gate an entire route, not filter rows within one.
- `UpdateCommentAsync`/`DeleteCommentAsync`'s `principal.IsInRole(nameof(Role.Admin))` check is *not* redundant with `AdminAuthorizationHandler` — see Context item 8. Without this explicit check, an Admin would get `403` trying to moderate someone else's comment, breaking the "Admin overrides everything" expectation the rest of the app establishes.

### 10 — Wire up the endpoint group

**File: `EmpoloyeeManagment/Program.cs`**

Add after line 149 (`app.MapUserEndpoints();`):

```csharp
app.MapTaskEndpoints();
```

### 11 — Update docs

**File: `README.md`** — add rows to the API overview table (lines 60–72) for all eleven new routes, and update the Roadmap paragraph (line 76) to mention task assignment/comments have shipped.

**File: `CLAUDE.md`** — update the "Known v1 gaps" paragraph (lines 63–65) to note that task assignment and comments have shipped, and that team-scoped (vs. all-tasks) Supervisor visibility remains a gap pending a reporting-hierarchy model (see Edge Cases below).

---

## Edge Cases & Failure Modes

- **"Supervisor sees their team's tasks" has no team to scope by.** No `SupervisorId`/`ManagerId`/`TeamId` column exists anywhere on `Employee` (confirmed by reading `Models/Employee.cs` in full and a repo-wide grep for those names). This story implements it as Admin/Manager/Supervisor seeing **every** task, Employee seeing only their own — a strict superset of "their team's," so introducing real team scoping later only needs to *narrow* an existing filter, not restructure one. Flagged in `CLAUDE.md`'s gaps list (task 11) so it isn't mistaken for an oversight.
- **`RelatedVisitId` is unvalidated.** Since `Visits` doesn't exist yet, `CreateTaskAsync` accepts and stores any `int?` value for `RelatedVisitId` with no existence check and no FK constraint (`Data/AppDbContext.cs`, task 4). A caller can set it to a nonexistent id today; nothing breaks (it's just an unconstrained int), but nothing validates it either. The `visits` story must add both the FK constraint and a real existence check when it introduces the `Visits` table.
- **`Done` is unreachable for a task with `RelatedVisitId` set.** `CompleteTaskAsync` explicitly rejects (`400`) any task where `RelatedVisitId is not null` (task 9) — such a task can only reach `Done` once the `visits` story ships and starts syncing `Status` from the linked Visit. Until then, a visit-linked task can go `New` → `InProgress` → (stuck), which is expected, not a bug — there is no requirement in this story's acceptance criteria for a workaround.
- **Reassigning a task that was `InProgress`.** `ReassignTaskAsync` must recalculate the **previous** assignee's attendance (not the new one) if the task was `InProgress` and `AttendanceRequired` — the task stops being their active work the moment it's reassigned, even though its `Status` resets to `New` rather than anything terminal. The new assignee's attendance is untouched until they call `accept`. Implemented via the `previousAssigneeId`/`wasInProgress` locals captured *before* `task.AssigneeId` is overwritten (task 9) — capturing them after would recalculate the wrong employee.
- **`AdminAuthorizationHandler` does not cover manual ownership checks.** See Context item 8 — `UpdateCommentAsync`/`DeleteCommentAsync` need an explicit `principal.IsInRole(nameof(Role.Admin))` check; the route-level `EmployeeOnly`/`TaskAssigner` policies do not need this since those go through the real policy pipeline the handler intercepts.
- **Employee-only routes are, in practice, Employee-**or**-Admin.** `AcceptTaskAsync`/`RejectTaskAsync` require the `EmployeeOnly` policy, but `AdminAuthorizationHandler` (pre-existing, app-wide) lets an Admin through regardless of which policy is named. This is consistent with every other role check in the app and is not a new gap.
- **Caller has no linked `Employee` row.** `GetCallerEmployeeAsync` returns `null` if `Employee.UserId` has no match for the caller's id. Every code path that creates an `ApplicationUser` today (`SignupAsync`, `UserEndpoints.CreateUserAsync`) also creates a linked `Employee` in the same transaction, so this should not occur in practice — but `GetTasksAsync` (empty list), `GetTaskByIdAsync`/`AcceptTaskAsync`/`RejectTaskAsync`/`AddCommentAsync` (`404`) all handle it defensively rather than throwing.
- **Simultaneous attendance-required tasks.** `EmployeeAttendanceService.RecalculateAsync` re-queries for *any* active attendance-required task rather than toggling a flag — necessary so that finishing one of two concurrently `InProgress` attendance-required tasks doesn't prematurely flip the employee back to `InOffice` while the other is still active (task 6).
- **Task-driven attendance never overrides `Absent`/`OnVacation`.** `RecalculateAsync` no-ops if the employee's current `AttendanceStatus` isn't already `InOffice` or `OutOfOffice` (task 6) — a status set by some other process (e.g. a future `leaves` story) takes precedence. That future story will need its own rule for what happens when the override ends; not this story's concern.
- **Rejecting/cancelling with an empty reason.** `RejectTaskAsync` returns `400` for a null/whitespace-only `Reason` (task 9) — minimal API record binding does not itself enforce non-empty strings, so this is an explicit check, not framework behavior.
- **Reassigning a `Rejected` task.** Allowed (only `Done`/`Cancelled` block reassignment, task 9) — a rejected task can be handed to someone else and re-enters `New`, clearing the old `RejectionReason`.

---

## Test Plan

This repo has no automated test project yet (`CLAUDE.md`, line 22: "There are no automated tests in this repo yet; verification is manual via Swagger"). This story follows the same manual-Swagger-walkthrough pattern Story 01 used (see that story's own Test Plan) rather than introducing a new, unestablished testing convention.

1. **Migration applies cleanly** — `dotnet ef database update --project EmpoloyeeManagment`; confirm `Tasks` and `TaskComments` tables exist and no other table changed.
2. **Create + role gating** — as a Supervisor (or Admin), `POST /api/tasks` with a valid `AssigneeId`; confirm `201` and `Status: "New"`. Retry as a plain Employee; confirm `403`.
3. **List scoping** — as the assignee Employee, `GET /api/tasks`; confirm only their own task(s) appear. As a Supervisor/Manager/Admin, confirm all tasks appear, including ones assigned to other employees.
4. **Detail scoping** — as a *different* Employee (not the assignee), `GET /api/tasks/{id}` for the task from step 2; confirm `404`.
5. **Accept** — as the assignee, `PATCH /api/tasks/{id}/accept`; confirm `200` and `Status: "InProgress"`. If `AttendanceRequired: true` was set at creation, `GET /api/employees/{id}` for that employee and confirm `AttendanceStatus: "OutOfOffice"`.
6. **Reject without reason** — create a second task, as its assignee call `PATCH /api/tasks/{id}/reject` with an empty `Reason`; confirm `400`. Retry with a non-empty reason; confirm `200`, `Status: "Rejected"`, and the reason round-trips on `GET /api/tasks/{id}`.
7. **Reassign resets state** — reassign the `InProgress` task from step 5 to a different employee (`PATCH /api/tasks/{id}`); confirm `200`, `Status: "New"`, and (if `AttendanceRequired`) that the *original* assignee's `AttendanceStatus` reverts to `InOffice`.
8. **Cancel / complete transitions** — confirm `PATCH /api/tasks/{id}/cancel` succeeds from `New`/`InProgress` and fails (`400`) from `Done`/`Cancelled`/`Rejected`; confirm `PATCH /api/tasks/{id}/complete` succeeds only from `InProgress` with `RelatedVisitId` null, and fails (`400`) when `RelatedVisitId` is set.
9. **Comments** — `POST /api/tasks/{id}/comments` as the assignee and as the assigning Supervisor; confirm both succeed. `PATCH`/`DELETE` a comment as a *different* employee who isn't its author; confirm `403`. Retry as an Admin; confirm it succeeds despite not being the author.
10. **Regression** — re-run the Story 01 walkthrough (signup → login → Authorize → employees → users → logout) end to end to confirm nothing existing broke.

---

## Migration / Rollback

One additive EF Core migration (`AddTasksAndComments`, task 5) adding the `Tasks` and `TaskComments` tables — no changes to any existing table or column.

**Half-applied migration.** If `dotnet ef database update` fails partway, re-running it is safe — EF Core migrations are idempotent per-migration; no manual cleanup needed.

**Rollback.** `dotnet ef database update 20260718091538_LinkEmployeeToUserAndUniqueEmail --project EmpoloyeeManagment` reverts the schema to the prior migration (dropping `Tasks`/`TaskComments`), then delete the migration files and revert the code changes (`Program.cs`, `Data/AppDbContext.cs`, the new `Models/`, `Services/`, `Dtos/Tasks/`, `Endpoints/TaskEndpoints.cs` files).

---

## Verification Steps

1. **Backend builds:** `dotnet build` from the repo root (or `EmpoloyeeManagment/`).
2. **Database check:** `dotnet ef database update --project EmpoloyeeManagment` — confirm only the new migration from task 5 applies.
3. **Run:** `dotnet run --project EmpoloyeeManagment`, open `/swagger`, and execute the full Test Plan above (steps 1–10).
4. **Regression:** confirm `ApiCallLogs` rows are still being written for the new `/api/tasks/*` calls (sanity check that `Middleware/ApiLoggingMiddleware.cs`, unmodified by this story, still wraps the new endpoints correctly).

---

## Done Criteria

- [ ] `TaskItemStatus` enum added to `Models/Enums.cs`; `TaskItem`/`TaskComment` models added — neither named `Task`/`TaskStatus`.
- [ ] `Tasks`/`TaskComments` DbSets and EF configuration added to `AppDbContext`; one additive migration applied.
- [ ] `IEmployeeAttendanceService`/`EmployeeAttendanceService` added and registered; recalculates `InOffice`⇄`OutOfOffice` for `AttendanceRequired` tasks without overriding `Absent`/`OnVacation`.
- [ ] `TaskAssigner` and `EmployeeOnly` authorization policies registered.
- [ ] `POST /api/tasks`, `PATCH /api/tasks/{id}` (reassign), `GET /api/tasks`, `GET /api/tasks/{id}` implemented with the role/row-scoping described above.
- [ ] `PATCH /api/tasks/{id}/accept`, `/reject` (Employee-only, own task, reason required on reject), `/cancel`, `/complete` (both Admin/Manager/Supervisor) implemented with the state-transition rules in Product rules.
- [ ] `POST/PATCH/DELETE /api/tasks/{id}/comments...` implemented; only the author or an Admin can update/delete.
- [ ] `README.md` API table/Roadmap and `CLAUDE.md`'s "Known v1 gaps" updated.
- [ ] Full manual Test Plan (steps 1–10) passes against a local run.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 03.**
