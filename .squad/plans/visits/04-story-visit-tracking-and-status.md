# Story 04 — Visit Tracking & Status

## Prerequisites

- **Hard blocker — `clients` must be planned and implemented first.** As of this writing, `Models/Client.cs` and `Models/Location.cs` do not exist anywhere in the codebase (confirmed by a full `EmpoloyeeManagment/**/*.cs` listing — no `Client`/`Location` file exists — and [`../clients/00-overview.md`](../clients/00-overview.md) is still the empty stub: "_add rows as stories are planned_"). Unlike `TaskItem.RelatedVisitId` (an optional nullable placeholder Story 02 deliberately left unconstrained until this story landed), `Visit.ClientId`/`Visit.LocationId` are core, non-nullable fields of the `Visit` entity itself — there is no "stub it and backfill later" path here: the code in this plan will not even **compile** until `Client`/`Location` types exist. Do not attempt to execute the tasks below until `clients` has shipped entities matching its own acceptance criteria ([`../../stories/clients/clients-locations-management/intake.md`](../../stories/clients/clients-locations-management/intake.md) line 68: "New Client entity: Name, Email, Contact. New Location entity: Name, Email, Contact, belongs to one Client"), with at minimum an `Id` (int PK) on `Client` and an `Id` + `ClientId` (FK to `Client`) on `Location`. This plan only ever references `Client.Id` / `Location.Id` / `Location.ClientId` — if the shipped `clients` story names these fields differently, adjust task 2's FK configuration accordingly; nothing else here depends on `Client`/`Location`'s exact shape.
- Story 02 completed: [`../tasks/02-story-task-assignment-and-comments.md`](../tasks/02-story-task-assignment-and-comments.md) — verified already implemented in the current codebase. This story modifies three of its files in place: `Models/TaskItem.cs` (whole file, 15 lines — `RelatedVisitId` at line 9 is currently a bare nullable `int`), `Data/AppDbContext.cs` (the `builder.Entity<TaskItem>(...)` block, lines 51–62), and `Endpoints/TaskEndpoints.cs` (`CreateTaskAsync` lines 66–87, `AcceptTaskAsync` lines 119–142, `CompleteTaskAsync` lines 193–218). It does **not** touch `Services/IEmployeeAttendanceService.cs`, `EmployeeAttendanceService.cs`, or `Dtos/Tasks/TaskDtos.cs` — see Story Goal point 2 for why.
- Per the `roles` story's precedent ([`../roles/00-overview.md`](../roles/00-overview.md)), this plan assumes the `Admin`/`Manager`/`Supervisor`/`Employee` roles, the JWT role claim, `AdminAuthorizationHandler`, and the `TaskAssigner`/`EmployeeOnly` authorization policies already exist — they do (`Program.cs` lines 76–82).

---

## Story Goal

Give Admin/Manager/Supervisor a way to schedule a Visit (a Client, a Location belonging to that Client, and a date/time) for an Employee, reusing the existing Task lifecycle (Story 02) for accept/reject/cancel/complete instead of building a parallel one. Concretely:

1. A `Visit` entity exists with `DateTime`, `ClientId` (→ `Client`), `LocationId` (→ `Location`), and **no independently-settable `Status` field** — `Status` is always read live from the `TaskItem` it drives (point 2).
2. `POST /api/visits` (Admin/Manager/Supervisor, the existing `TaskAssigner` policy) creates a `Visit` **and** a linked `TaskItem` (`RelatedVisitId` → the new visit) in one transaction, mirroring the `Employee`+`ApplicationUser` transaction already established by `AuthEndpoints.SignupAsync` / `UserEndpoints.CreateUserAsync`. The linked task is always created with `AttendanceRequired = true` — a visit inherently takes the employee out of office the moment it's accepted, independent of whatever `AttendanceRequired` means for a generic task. This is a deliberate design choice: it means `Services/IEmployeeAttendanceService` needs **zero** changes to satisfy the acceptance criteria's "coordinate ... so this is one implementation, not two" — the existing `AttendanceRequired && Status == InProgress` query already covers "employee is on an active visit" for free.
3. The employee accepts/rejects a visit exactly like any other task, via the **existing** `PATCH /api/tasks/{id}/accept` / `/reject` endpoints — there are no separate visit-specific accept/reject routes. `AcceptTaskAsync` gains one new check: a task whose `RelatedVisitId` is set cannot be accepted if its assignee already has another visit-linked task that is `InProgress` (accepted-and-not-yet-`Done`).
4. `CompleteTaskAsync`'s existing block on completing any task with `RelatedVisitId is not null` (added by Story 02 specifically as a placeholder for this story — see its Edge Cases) is removed. Completing a visit-linked task via the existing `PATCH /api/tasks/{id}/complete` **is** how a Visit reaches its "done" state, since `Visit.Status` is nothing more than a live read of its task's `Status`.
5. `GET /api/visits` / `GET /api/visits/{id}` apply the same row-level visibility rule as `GET /api/tasks` / `GET /api/tasks/{id}` (Employee: only visits whose linked task is assigned to them; Admin/Manager/Supervisor: all). This is a deliberate consistency choice, not literal acceptance-criteria text — leaving Visits unscoped while Tasks are scoped would be an inconsistent privacy hole, since a Visit is a thin wrapper around a Task.
6. The concurrency rule ("an employee can't have another accepted, not-yet-done visit") is enforced at both entry points the acceptance criteria names: visit creation (`POST /api/visits`, checking the target assignee) and visit acceptance (`PATCH /api/tasks/{id}/accept`, when the task is visit-linked). Both checks are necessary — see Edge Cases for the race they each close.
7. The pre-existing, generic `POST /api/tasks` endpoint gains an existence check on `RelatedVisitId` (Story 02 explicitly deferred this to this story) plus a check that the target visit isn't already linked to another task, so a foreseeable conflict returns a clean `400` instead of surfacing the new unique-index violation (task 2) as a raw `500`.

**Not in scope for this story** (do not build these here):
- `Client`/`Location` CRUD, and any "locations filtered by client" listing endpoint — owned by the `clients` story per its own intake's "Out of scope" section. This story only **validates** that a given `LocationId` belongs to the given `ClientId` when creating a Visit; it does not expose a way to list a client's locations.
- Reassigning a visit. The existing `PATCH /api/tasks/{id}` (`ReassignTaskAsync`) already works mechanically on a visit-linked task (it only touches `AssigneeId`/`Status`/`RejectionReason`), but this story does **not** add the concurrency check there — the acceptance criteria only names "creating/accepting" as enforcement points. See Edge Cases for the resulting gap.
- Any new Visit-specific accept/reject/cancel endpoints — deliberately reuses `TaskEndpoints`'s existing ones (point 3).
- Changes to `Services/IEmployeeAttendanceService` / `EmployeeAttendanceService` — see point 2 for why none are needed.

---

## Context — Read These Files First

1. Repo-wide file listing (`EmpoloyeeManagment/**/*.cs`) confirms no `Client.cs`, `Location.cs`, or `Visit.cs` exists yet anywhere under `Models/` — the fact behind the Prerequisites blocker above.
2. [`EmpoloyeeManagment/Models/TaskItem.cs`](../../../EmpoloyeeManagment/Models/TaskItem.cs) — whole file (15 lines). `RelatedVisitId` (line 9) is the existing bare nullable `int` this story attaches a real FK to (task 2).
3. [`EmpoloyeeManagment/Models/Employee.cs`](../../../EmpoloyeeManagment/Models/Employee.cs) — whole file (14 lines). The plain-class-with-auto-properties style (no navigation properties) the new `Visit` model (task 1) must match.
4. [`EmpoloyeeManagment/Data/AppDbContext.cs`](../../../EmpoloyeeManagment/Data/AppDbContext.cs) — whole file (79 lines). `DbSet` declarations lines 9–12 (add `Visits` here, task 2). The `Employee` block's unique-filtered index at line 36 (`entity.HasIndex(e => e.UserId).IsUnique().HasFilter("[UserId] IS NOT NULL");`) is the exact pattern this story reuses on `TaskItem.RelatedVisitId` (task 2) to enforce one task per visit at the database level. The `TaskItem` block, lines 51–62, is modified in place (task 2) to add the `Visit` FK. The `TaskComment` block, lines 64–77, is the closest existing precedent for a new entity block with two `HasOne<T>().WithMany().HasForeignKey(...)` FKs — the new `Visit` block (task 2) needs two: `Client`, `Location`.
5. [`EmpoloyeeManagment/Endpoints/TaskEndpoints.cs`](../../../EmpoloyeeManagment/Endpoints/TaskEndpoints.cs) — whole file (300 lines). This story modifies three handlers in place:
   - `CreateTaskAsync`, lines 66–87 — add the `RelatedVisitId` existence + already-linked checks (task 5).
   - `AcceptTaskAsync`, lines 119–142 — add the visit concurrency check (task 5).
   - `CompleteTaskAsync`, lines 193–218 — delete the `if (task.RelatedVisitId is not null) { return TypedResults.BadRequest(...) }` block at lines 199–202 outright (task 5).
   Also read `IsPrivileged` (lines 278–279), `GetCallerEmployeeAsync` (lines 281–285), and `CanViewTaskAsync` (lines 287–292) — the new `Endpoints/VisitEndpoints.cs` (task 6) duplicates the first two rather than sharing them, per the codebase's established convention (Context item 6) of duplicating small per-endpoint-file helpers instead of extracting a shared service across feature files.
6. [`CLAUDE.md`](../../../CLAUDE.md) line 32 — states the codebase's explicit precedent for this exact situation: `POST /api/users` "follows the identical transactional shape [as `SignupAsync`], deliberately duplicated rather than shared ... since the two live in different endpoint files and the codebase has no cross-feature service-extraction convention yet." This story's `HasActiveAcceptedVisitAsync` helper (tasks 5 and 6) follows the same rule — duplicated in both `TaskEndpoints.cs` and the new `VisitEndpoints.cs` rather than extracted to a shared service.
7. [`EmpoloyeeManagment/Endpoints/AuthEndpoints.cs`](../../../EmpoloyeeManagment/Endpoints/AuthEndpoints.cs) lines 24–70 (`SignupAsync`) — the transactional-two-writes pattern (`await using var transaction = await db.Database.BeginTransactionAsync(); ... await db.SaveChangesAsync(); await transaction.CommitAsync();`) that `CreateVisitAsync` (task 6) replicates for its `Visit` + `TaskItem` writes.
8. [`EmpoloyeeManagment/Services/EmployeeAttendanceService.cs`](../../../EmpoloyeeManagment/Services/EmployeeAttendanceService.cs) — whole file (28 lines). `RecalculateAsync`'s query at lines 21–22 (`t.AssigneeId == employeeId && t.AttendanceRequired && t.Status == TaskItemStatus.InProgress`) has no awareness of `RelatedVisitId` at all. **This file is not modified by this story** — forcing `AttendanceRequired = true` on every visit-linked task (task 6) makes this existing query already correct for visits; re-verify this reasoning before assuming a change is needed here.
9. [`EmpoloyeeManagment/Dtos/Tasks/TaskDtos.cs`](../../../EmpoloyeeManagment/Dtos/Tasks/TaskDtos.cs) (whole file, 45 lines) and [`Dtos/Users/UserDtos.cs`](../../../EmpoloyeeManagment/Dtos/Users/UserDtos.cs) (11 lines) — the one-file-per-feature, one-record-per-shape DTO convention `Dtos/Visits/VisitDtos.cs` (task 4) must match.
10. [`EmpoloyeeManagment/Program.cs`](../../../EmpoloyeeManagment/Program.cs) — whole file (155 lines). Line 5 (`using EmpoloyeeManagment.Endpoints;`) already covers the new `VisitEndpoints.cs` file — no new `using` needed. Line 153 (`app.MapTaskEndpoints();`) — add `app.MapVisitEndpoints();` directly after it (task 7). No new service registration is needed — task 6 adds no new service class.
11. [`EmpoloyeeManagment/Migrations/AppDbContextModelSnapshot.cs`](../../../EmpoloyeeManagment/Migrations/AppDbContextModelSnapshot.cs) lines 222–268 (`TaskItem`) — confirms `RelatedVisitId` (lines 255–256) has no FK today, and lines 224–228 confirm `Id` on every entity in this codebase is `int` identity (`ValueGeneratedOnAdd()` + `UseIdentityColumn`) — the type `Visit.Id`/`Visit.ClientId`/`Visit.LocationId` must match. Also confirms the most recent migration is `20260901141421_AddTasksAndComments` (see the `Migrations/` directory listing) — this story's own migration (task 3) applies after it, and after whatever migration `clients` adds in between.
12. [`../tasks/02-story-task-assignment-and-comments.md`](../tasks/02-story-task-assignment-and-comments.md) — read in full as the structural and tonal precedent for this story (per the planning process's "find a similar precedent" step). Its Edge Cases section (search for "RelatedVisitId") is what commits this story to adding the FK, the existence check, and resolving how `Done` becomes reachable.
13. [`README.md`](../../../README.md) line 12 (Tasks feature bullet) and lines 60–88 (API table + Roadmap paragraph), and [`CLAUDE.md`](../../../CLAUDE.md) lines 26, 40, 44, 53–65, 69 — all need updating; see task 8.

---

## Product rules (from story)

| Area | Current behavior | New behavior |
|---|---|---|
| `Visit` | Doesn't exist | New entity: `DateTime`, `ClientId`, `LocationId`. No stored `Status` — always read live from its linked `TaskItem.Status` |
| `POST /api/visits` | Doesn't exist | New — **Admin/Manager/Supervisor** (`TaskAssigner` policy) — creates a `Visit` + a linked `TaskItem` (`RelatedVisitId`, `AttendanceRequired = true`, `Status = New`) in one transaction |
| `GET /api/visits`, `GET /api/visits/{id}` | Don't exist | New — any authenticated user; Employee sees/opens only visits whose linked task is assigned to them; Admin/Manager/Supervisor see all |
| `PATCH /api/tasks/{id}/accept` (visit-linked task) | N/A (no visits) | Now also rejects (`400`) if the assignee already has another visit-linked task that is `InProgress` |
| `PATCH /api/tasks/{id}/complete` (visit-linked task) | Always `400` ("derived from its linked visit") | Now succeeds exactly like a normal task (only from `InProgress`) — this **is** how a Visit reaches its "done" state |
| `POST /api/tasks` with `RelatedVisitId` set | Accepted with no existence check | Now `404` if the visit doesn't exist, `400` if it's already linked to another task |
| `Employee.AttendanceStatus` | Recomputed only from `AttendanceRequired` tasks | Unchanged — visit-linked tasks are created with `AttendanceRequired = true`, so they already flow through the same computation |

---

## Backend Tasks

`No frontend changes required in this repository` — the companion UI work is tracked in the separate `employee-management-web` repo per the intake's "Extra notes."

### 1 — Add the `Visit` model

**Create file: `EmpoloyeeManagment/Models/Visit.cs`**

```csharp
namespace EmpoloyeeManagment.Models;

public class Visit
{
    public int Id { get; set; }
    public DateTime DateTime { get; set; }
    public int ClientId { get; set; }
    public int LocationId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

No navigation properties to `Client`/`Location`/`TaskItem` — matches this codebase's established convention of FK-by-id-only (`TaskItem.AssigneeId`, `Employee.UserId` — neither has a nav property either).

### 2 — Register the `Visits` DbSet and EF configuration

**File: `EmpoloyeeManagment/Data/AppDbContext.cs`**

Add a fifth `DbSet` after line 12:

```csharp
public DbSet<Visit> Visits => Set<Visit>();
```

Replace the existing `builder.Entity<TaskItem>(...)` block (lines 51–62) with:

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

    entity.HasIndex(t => t.RelatedVisitId).IsUnique().HasFilter("[RelatedVisitId] IS NOT NULL");
    entity.HasOne<Visit>()
        .WithMany()
        .HasForeignKey(t => t.RelatedVisitId)
        .OnDelete(DeleteBehavior.Restrict);
});
```

Add a new configuration block directly after the `TaskComment` block (after line 77, before the closing `}` of `OnModelCreating` on line 78):

```csharp
builder.Entity<Visit>(entity =>
{
    entity.HasOne<Client>()
        .WithMany()
        .HasForeignKey(v => v.ClientId)
        .OnDelete(DeleteBehavior.Restrict);

    entity.HasOne<Location>()
        .WithMany()
        .HasForeignKey(v => v.LocationId)
        .OnDelete(DeleteBehavior.Restrict);
});
```

The unique filtered index on `TaskItem.RelatedVisitId` guarantees at most one task can ever point at a given visit — the DB-level enforcement of the 1:1 relationship, mirroring `Employee.UserId`'s existing unique-filtered index (line 36). `DeleteBehavior.Restrict` on all three new/changed FKs matches the caution already implicit in `Employee`/`TaskItem` having no delete endpoints. `Client`/`Location` resolve via the existing `using EmpoloyeeManagment.Models;` at line 1 — no new `using` needed once `clients` has added them to that namespace.

### 3 — Create the EF Core migration

From the repo root, **after** `clients`'s own migration has already been applied:

```bash
dotnet ef migrations add AddVisits --project EmpoloyeeManagment
dotnet ef database update --project EmpoloyeeManagment
```

Confirm the generated migration only adds the `Visits` table and the new FK + unique filtered index on `Tasks.RelatedVisitId` — if it also tries to create `Clients`/`Locations`, `clients`'s migration was not actually applied first; stop and investigate before applying.

### 4 — Create the Visits DTOs

**Create file: `EmpoloyeeManagment/Dtos/Visits/VisitDtos.cs`**

```csharp
using EmpoloyeeManagment.Models;

namespace EmpoloyeeManagment.Dtos.Visits;

public record CreateVisitRequest(
    DateTime DateTime,
    int ClientId,
    int LocationId,
    int AssigneeId,
    string? Name,
    string? Notes);

public record VisitListItemDto(
    int Id,
    DateTime DateTime,
    int ClientId,
    int LocationId,
    int AssigneeId,
    TaskItemStatus Status,
    int TaskId);

public record VisitDetailDto(
    int Id,
    DateTime DateTime,
    int ClientId,
    int LocationId,
    int AssigneeId,
    TaskItemStatus Status,
    int TaskId,
    DateTime CreatedAt);
```

`Name`/`Notes` on `CreateVisitRequest` are optional and map onto the auto-created `TaskItem.Name`/`Notes` (task 6) — `Name` defaults to an auto-generated string when omitted, since `TaskItem.Name` is `IsRequired()` but nothing in the intake defines a "visit name" concept independent of the task it drives.

### 5 — Modify `Endpoints/TaskEndpoints.cs`

**File: `EmpoloyeeManagment/Endpoints/TaskEndpoints.cs`**

Replace `CreateTaskAsync` (lines 66–87) with:

```csharp
private static async Task<Results<Created<TaskDetailDto>, NotFound, BadRequest<string>>> CreateTaskAsync(CreateTaskRequest request, AppDbContext db)
{
    var assigneeExists = await db.Employees.AnyAsync(e => e.Id == request.AssigneeId);
    if (!assigneeExists) return TypedResults.NotFound();

    if (request.RelatedVisitId is not null)
    {
        var visitExists = await db.Visits.AnyAsync(v => v.Id == request.RelatedVisitId);
        if (!visitExists) return TypedResults.NotFound();

        var alreadyLinked = await db.Tasks.AnyAsync(t => t.RelatedVisitId == request.RelatedVisitId);
        if (alreadyLinked) return TypedResults.BadRequest("This visit is already linked to another task.");
    }

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
```

Replace `AcceptTaskAsync` (lines 119–142) with:

```csharp
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

    if (task.RelatedVisitId is not null && await HasActiveAcceptedVisitAsync(task.AssigneeId, db))
    {
        return TypedResults.BadRequest("This employee already has another accepted visit that isn't done yet.");
    }

    task.Status = TaskItemStatus.InProgress;
    await db.SaveChangesAsync();

    if (task.AttendanceRequired)
    {
        await attendanceService.RecalculateAsync(task.AssigneeId);
    }

    return TypedResults.Ok(ToDetailDto(task));
}
```

Replace `CompleteTaskAsync` (lines 193–218) with:

```csharp
private static async Task<Results<Ok<TaskDetailDto>, NotFound, BadRequest<string>>> CompleteTaskAsync(
    int id, AppDbContext db, IEmployeeAttendanceService attendanceService)
{
    var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id);
    if (task is null) return TypedResults.NotFound();

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
```

Add a new private helper directly after `CanViewTaskAsync` (after line 292, before `ToDetailDto`):

```csharp
private static Task<bool> HasActiveAcceptedVisitAsync(int employeeId, AppDbContext db) =>
    db.Tasks.AnyAsync(t => t.AssigneeId == employeeId && t.RelatedVisitId != null && t.Status == TaskItemStatus.InProgress);
```

No changes to the `MapTaskEndpoints` route table (lines 17–27) — the handler signatures' new `BadRequest<string>` result type is inferred automatically by minimal APIs from the method group, same as every other route here.

### 6 — Create `Endpoints/VisitEndpoints.cs`

**Create file: `EmpoloyeeManagment/Endpoints/VisitEndpoints.cs`**

```csharp
using System.Security.Claims;
using EmpoloyeeManagment.Data;
using EmpoloyeeManagment.Dtos.Visits;
using EmpoloyeeManagment.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace EmpoloyeeManagment.Endpoints;

public static class VisitEndpoints
{
    public static IEndpointRouteBuilder MapVisitEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/visits").WithTags("Visits").RequireAuthorization();

        group.MapGet("/", GetVisitsAsync);
        group.MapGet("/{id:int}", GetVisitByIdAsync);
        group.MapPost("/", CreateVisitAsync).RequireAuthorization("TaskAssigner");

        return app;
    }

    private static async Task<Ok<List<VisitListItemDto>>> GetVisitsAsync(ClaimsPrincipal principal, AppDbContext db)
    {
        var query = db.Visits.AsNoTracking()
            .Join(db.Tasks.AsNoTracking(), v => v.Id, t => t.RelatedVisitId, (v, t) => new { Visit = v, Task = t });

        if (!IsPrivileged(principal))
        {
            var caller = await GetCallerEmployeeAsync(principal, db);
            if (caller is null) return TypedResults.Ok(new List<VisitListItemDto>());
            query = query.Where(x => x.Task.AssigneeId == caller.Id);
        }

        var visits = await query
            .OrderBy(x => x.Visit.DateTime)
            .Select(x => new VisitListItemDto(x.Visit.Id, x.Visit.DateTime, x.Visit.ClientId, x.Visit.LocationId, x.Task.AssigneeId, x.Task.Status, x.Task.Id))
            .ToListAsync();

        return TypedResults.Ok(visits);
    }

    private static async Task<Results<Ok<VisitDetailDto>, NotFound>> GetVisitByIdAsync(int id, ClaimsPrincipal principal, AppDbContext db)
    {
        var visit = await db.Visits.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id);
        if (visit is null) return TypedResults.NotFound();

        var task = await db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.RelatedVisitId == id);
        if (task is null) return TypedResults.NotFound();

        if (!IsPrivileged(principal))
        {
            var caller = await GetCallerEmployeeAsync(principal, db);
            if (caller is null || task.AssigneeId != caller.Id) return TypedResults.NotFound();
        }

        return TypedResults.Ok(ToDetailDto(visit, task));
    }

    private static async Task<Results<Created<VisitDetailDto>, NotFound, BadRequest<string>>> CreateVisitAsync(
        CreateVisitRequest request, AppDbContext db)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == request.ClientId);
        if (client is null) return TypedResults.NotFound();

        var location = await db.Locations.FirstOrDefaultAsync(l => l.Id == request.LocationId);
        if (location is null) return TypedResults.NotFound();
        if (location.ClientId != request.ClientId)
        {
            return TypedResults.BadRequest("The selected location does not belong to the selected client.");
        }

        var assigneeExists = await db.Employees.AnyAsync(e => e.Id == request.AssigneeId);
        if (!assigneeExists) return TypedResults.NotFound();

        if (await HasActiveAcceptedVisitAsync(request.AssigneeId, db))
        {
            return TypedResults.BadRequest("This employee already has another accepted visit that isn't done yet.");
        }

        // A Visit and the Task that drives it are created together, or not at all —
        // mirrors the Employee+ApplicationUser transaction in AuthEndpoints.SignupAsync.
        await using var transaction = await db.Database.BeginTransactionAsync();

        var visit = new Visit
        {
            DateTime = request.DateTime,
            ClientId = request.ClientId,
            LocationId = request.LocationId,
            CreatedAt = DateTime.UtcNow
        };
        db.Visits.Add(visit);
        await db.SaveChangesAsync();

        var task = new TaskItem
        {
            Name = string.IsNullOrWhiteSpace(request.Name) ? $"Visit on {request.DateTime:yyyy-MM-dd HH:mm}" : request.Name,
            DueDateTime = request.DateTime,
            AssigneeId = request.AssigneeId,
            RelatedVisitId = visit.Id,
            Notes = request.Notes,
            AttendanceRequired = true,
            Status = TaskItemStatus.New,
            CreatedAt = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return TypedResults.Created($"/api/visits/{visit.Id}", ToDetailDto(visit, task));
    }

    private static bool IsPrivileged(ClaimsPrincipal principal) =>
        principal.IsInRole(nameof(Role.Admin)) || principal.IsInRole(nameof(Role.Manager)) || principal.IsInRole(nameof(Role.Supervisor));

    private static async Task<Employee?> GetCallerEmployeeAsync(ClaimsPrincipal principal, AppDbContext db)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return userId is null ? null : await db.Employees.FirstOrDefaultAsync(e => e.UserId == userId);
    }

    private static Task<bool> HasActiveAcceptedVisitAsync(int employeeId, AppDbContext db) =>
        db.Tasks.AnyAsync(t => t.AssigneeId == employeeId && t.RelatedVisitId != null && t.Status == TaskItemStatus.InProgress);

    private static VisitDetailDto ToDetailDto(Visit visit, TaskItem task) => new(
        visit.Id, visit.DateTime, visit.ClientId, visit.LocationId, task.AssigneeId, task.Status, task.Id, visit.CreatedAt);
}
```

`db.Clients`/`db.Locations` are `clients`-story DbSets this file consumes but does not own (Prerequisites). `IsPrivileged`/`GetCallerEmployeeAsync` are verbatim duplicates of `TaskEndpoints`'s private helpers of the same name (Context item 5) — intentional, per the codebase's documented no-cross-feature-service convention (Context item 6), not an oversight.

### 7 — Wire up the endpoint group

**File: `EmpoloyeeManagment/Program.cs`**

Add after line 153 (`app.MapTaskEndpoints();`):

```csharp
app.MapVisitEndpoints();
```

### 8 — Update docs

**File: `README.md`** — add a `Visits` bullet to the Features list (after line 12, matching the existing Tasks bullet's style) and add rows to the API table (after line 84) for `GET /api/visits`, `GET /api/visits/{id}`, `POST /api/visits`; update the Roadmap paragraph (line 88) to note that visit tracking has shipped and that `RelatedVisitId` is now FK-constrained.

**File: `CLAUDE.md`** — update line 26 (add `VisitEndpoints` to the `Map*Endpoints` list), line 40 (add `Visit` to the Data model paragraph, FK `ClientId → Client`, `LocationId → Location`), line 44 (note that `RelatedVisitId` is now FK-constrained and `complete` no longer blocks visit-linked tasks), lines 53–65 (Project layout — add `Models/Visit.cs`, `Dtos/Visits/`, `Endpoints/VisitEndpoints.cs` to the relevant lines), and line 69 (Known v1 gaps — remove "syncing task status from a linked visit" from the gap list; note real reporting-hierarchy/team scoping for Supervisors still applies equally to visits, since it reuses the same row-level check).

---

## Edge Cases & Failure Modes

- **`clients` not implemented yet.** This is the Prerequisites blocker, not a runtime edge case — restated here for visibility: this story's code does not compile without `Client`/`Location` existing.
- **Location belongs to a different client.** `CreateVisitAsync` (task 6) returns `404` if the `LocationId` doesn't exist at all, but `400` if it exists and belongs to some *other* client — these are deliberately different status codes for different failure shapes.
- **Two `New` visits, then a race at accept-time.** Creating two visits for the same employee both succeeds while neither is yet accepted (the create-time check in `CreateVisitAsync` only blocks when the employee already has an *accepted* visit). Accepting the first succeeds; accepting the second must then be blocked by `AcceptTaskAsync`'s new check (task 5) — this is exactly why the concurrency rule needs **both** enforcement points, not just one at creation time.
- **Reassigning a visit-linked task bypasses the concurrency check for the new assignee.** `ReassignTaskAsync` (unmodified by this story) lets any visit-linked task be reassigned to a different employee without checking whether *that* employee already has an accepted, not-done visit. This is a deliberate, documented gap — the acceptance criteria only names "creating/accepting" as enforcement points (Story Goal, "Not in scope"). A future story would need to add the same `HasActiveAcceptedVisitAsync` check there if this gap needs closing.
- **`RelatedVisitId` set on the generic `POST /api/tasks`.** Now checked for existence (`404`) and for not already being linked to another task (`400`, task 5) — without the second check, a second task pointing at an already-linked visit would hit the new unique filtered index (task 2) and surface as an unhandled `500` instead of a clean `400`.
- **`AttendanceRequired` is entirely server-controlled for visit-linked tasks.** `CreateVisitRequest` has no `AttendanceRequired` field at all — `CreateVisitAsync` (task 6) always sets `true`. This is safe precisely because there is no way to edit a task's fields after creation in this codebase (Story 02's `PATCH /api/tasks/{id}` only changes `AssigneeId`/`Status`/`RejectionReason` — see `CLAUDE.md` line 44) — nothing can ever flip a visit-task's `AttendanceRequired` back to `false`.
- **Accepting a visit while `Absent`/`OnVacation`.** Unchanged existing behavior (Context item 8): `EmployeeAttendanceService.RecalculateAsync` no-ops unless the employee's current `AttendanceStatus` is already `InOffice`/`OutOfOffice`. A visit accepted while an employee is `Absent`/`OnVacation` does not force them `OutOfOffice` — inherited for free, not a new rule.
- **`Visit.DateTime` is not validated as being in the future.** No date-range validation exists anywhere else in this codebase (`TaskItem.DueDateTime` isn't validated either) — deliberately not introducing one here for consistency.
- **A `Visit` with no matching `TaskItem`.** Should be unreachable in practice — there is no `DELETE` endpoint for tasks anywhere in this codebase, and the unique filtered index (task 2) guarantees at most one task per visit — but `GetVisitByIdAsync`/`GetVisitsAsync` (task 6) handle a missing task defensively (`404` / excluded from the list via the inner `Join`) rather than throwing, matching `TaskEndpoints.GetCallerEmployeeAsync`'s existing defensive-null style (Context item 5).
- **Migration ordering.** This story's migration (task 3) must run strictly after `clients`'s own migration. Because the FK types are resolved at **compile time** (`entity.HasOne<Client>()`), there is no partial-apply risk in practice — attempting to build before `clients` ships fails the build outright, well before any migration command would run.

---

## Test Plan

This repo has no automated test project yet (`CLAUDE.md` line 22). This story follows the same manual-Swagger-walkthrough pattern Story 02 used.

1. **Migration applies cleanly** — `dotnet ef database update --project EmpoloyeeManagment` (after `clients`'s own migration is already applied); confirm `Visits` exists, `Tasks.RelatedVisitId` now has a FK + unique filtered index, and no other table changed.
2. **Create + role gating** — as a Supervisor (or Admin), `POST /api/visits` with a valid `ClientId`/`LocationId`/`AssigneeId`; confirm `201` and the response's `Status` is `"New"`. Retry as a plain Employee; confirm `403`.
3. **Location/client mismatch and not-found** — `POST /api/visits` with a `LocationId` that belongs to a *different* client than the given `ClientId`; confirm `400`. Retry with a nonexistent `ClientId`, then a nonexistent `LocationId`, then a nonexistent `AssigneeId`; confirm `404` for each.
4. **List/detail scoping** — as the visit's assignee Employee, `GET /api/visits`; confirm only their own visit(s) appear. As a different Employee, `GET /api/visits/{id}` for that visit; confirm `404`. As Admin/Manager/Supervisor, confirm all visits appear.
5. **Accept via the existing task endpoint** — `PATCH /api/tasks/{taskId}/accept` (the `TaskId` from step 2's response) as the assignee; confirm `200`, `Status: "InProgress"`. `GET /api/employees/{id}` for that employee and confirm `AttendanceStatus: "OutOfOffice"`. `GET /api/visits/{visitId}` and confirm `Status: "InProgress"` there too.
6. **Concurrency — two `New` visits, then a blocked second accept** — before accepting Visit A, create a second visit (Visit B) for the same assignee (both `New`, both succeed — the create-time check only blocks against an already-*accepted* visit). Now accept Visit A's task; confirm `200`, `Status: "InProgress"`. Attempt to accept Visit B's task; confirm `400`.
7. **Concurrency — blocked at create** — with Visit A still `InProgress`, `POST /api/visits` a third visit for the same assignee; confirm `400`.
8. **Complete via the existing task endpoint** — `PATCH /api/tasks/{taskId}/complete` for Visit A's task; confirm `200`, `Status: "Done"`. `GET /api/visits/{visitA}` confirms `Status: "Done"`. `GET /api/employees/{id}` confirms `AttendanceStatus` reverts to `"InOffice"` (assuming no other attendance-driving task/visit is active). Now accepting Visit B's task (blocked in step 6) succeeds.
9. **Generic task creation with `RelatedVisitId`** — `POST /api/tasks` with `RelatedVisitId` set to a nonexistent id; confirm `404`. Retry with `RelatedVisitId` set to Visit A's id (already linked to its own task); confirm `400`.
10. **Regression** — re-run Story 02's full task walkthrough (create/reassign/accept/reject/cancel/complete/comments) end to end to confirm nothing existing broke.

---

## Migration / Rollback

One additive EF Core migration (`AddVisits`, task 3) adding the `Visits` table and a FK + unique filtered index on the existing `Tasks.RelatedVisitId` column — no other existing table or column changes. Must be applied strictly after `clients`'s own migration (which creates `Clients`/`Locations`).

**Half-applied migration.** If `dotnet ef database update` fails partway, re-running it is safe — EF Core migrations are idempotent per-migration; no manual cleanup needed (same as Story 02's documented behavior).

**Rollback.** Roll back to the migration `clients` shipped immediately before this one — its exact name is not yet known (check the `Migrations/` directory for the migration immediately preceding `AddVisits` once `clients` has shipped): `dotnet ef database update <that migration name> --project EmpoloyeeManagment`. Then delete the `AddVisits` migration files, delete `Models/Visit.cs`, `Dtos/Visits/VisitDtos.cs`, `Endpoints/VisitEndpoints.cs`, revert `Program.cs` (task 7) and `Data/AppDbContext.cs` (task 2), and revert `Endpoints/TaskEndpoints.cs`'s three modified methods (task 5) back to their Story 02 shape (reproduced in full in that story's own file).

---

## Verification Steps

1. **Backend builds:** `dotnet build` from the repo root (or `EmpoloyeeManagment/`) — only succeeds once `clients` has shipped `Client`/`Location`.
2. **Database check:** `dotnet ef database update --project EmpoloyeeManagment` — confirm only `AddVisits` (and, if not already applied, `clients`'s migration before it) apply.
3. **Run:** `dotnet run --project EmpoloyeeManagment`, open `/swagger`, and execute the full Test Plan above (steps 1–10).
4. **Regression:** confirm `ApiCallLogs` rows are still being written for the new `/api/visits/*` calls (sanity check that `Middleware/ApiLoggingMiddleware.cs`, unmodified by this story, still wraps the new endpoints correctly).

---

## Done Criteria

- [ ] `Visit` model added (`Models/Visit.cs`); `Visits` DbSet and EF configuration added to `AppDbContext`, including the FK from `TaskItem.RelatedVisitId` to `Visit` and its unique filtered index; one additive migration applied after `clients`'s own migration.
- [ ] `Dtos/Visits/VisitDtos.cs` added, matching the existing DTO conventions.
- [ ] `POST /api/visits` (Admin/Manager/Supervisor) creates a `Visit` + linked `TaskItem` (`AttendanceRequired = true`) transactionally, validating client/location/assignee existence, location-belongs-to-client, and the accepted-visit concurrency rule.
- [ ] `GET /api/visits`, `GET /api/visits/{id}` implemented with the same row-level scoping as the equivalent task endpoints.
- [ ] `PATCH /api/tasks/{id}/accept` enforces the same concurrency rule for visit-linked tasks; `PATCH /api/tasks/{id}/complete` no longer blocks visit-linked tasks.
- [ ] `POST /api/tasks` validates `RelatedVisitId` existence and not-already-linked.
- [ ] `Services/IEmployeeAttendanceService`/`EmployeeAttendanceService` left unmodified — visit attendance works via `AttendanceRequired = true` alone.
- [ ] `README.md` API table/Roadmap and `CLAUDE.md` (Architecture, Data model, Project layout, Known v1 gaps) updated.
- [ ] Full manual Test Plan (steps 1–10) passes against a local run.
