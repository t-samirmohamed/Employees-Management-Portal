# Story 07 — Leave Request Notifications

## Prerequisites

- `roles` Story 01 completed: [`../roles/01-story-role-model-and-admin-user-management.md`](../roles/01-story-role-model-and-admin-user-management.md) — verified already implemented (`Program.cs` lines 76–82: `AdminOnly`, `TaskAssigner`, `EmployeeOnly`, `ClientManager` policies; `Models/Enums.cs` lines 23–29's `Role` enum). This story's two new routes need **no new policy** — both are plain `.RequireAuthorization()` (any authenticated user acting on their own rows), matching the shape already used by `TaskEndpoints`'s and `VisitEndpoints`'s groups.
- **`leaves` has no story file or entity yet — a partial blocker, not a hard one.** [`../leaves/00-overview.md`](../leaves/00-overview.md) is still the empty stub ("_add rows as stories are planned_"), confirmed by a full `EmpoloyeeManagment/**/*.cs` listing showing no `LeaveRequest`/`Leave` model, no `Endpoints/LeaveEndpoints.cs`, and no `Dtos/Leaves/` anywhere in the codebase — the same fact [`../dashboards/06-story-activity-dashboards-and-stats.md`](../dashboards/06-story-activity-dashboards-and-stats.md)'s Prerequisites and [`../visits/04-story-visit-tracking-and-status.md`](../visits/04-story-visit-tracking-and-status.md)'s Prerequisites already document for their own dependencies on `leaves`/`clients`. Unlike `visits`→`clients` (a **hard**, compile-time blocker, because `Visit.ClientId`/`LocationId` are non-nullable FKs to types that didn't exist yet), this story's `Notification` entity references the leave request only via a bare, unconstrained `int ReferenceId` — deliberately mirroring how `TaskItem.RelatedVisitId` (`Models/TaskItem.cs` line 9) started as a bare nullable `int` in the `tasks` story before the `visits` story attached a real FK to it later ([`../visits/04-story-visit-tracking-and-status.md`](../visits/04-story-visit-tracking-and-status.md) Context item 2). That means the `Notification` entity, its migration, and both new endpoints have **no compile-time or data dependency on `leaves`** and can be built and shipped today. Only one thing in this plan is genuinely blocked: wiring `INotificationService.NotifyLeaveRequestSubmittedAsync` into an actual `POST /api/leaves` handler — that route does not exist yet. See Story Goal point 5, "Not in scope," and Done Criteria for exactly what ships now versus what is only prepared for.
- The `leaves` story's own intake ([`../../stories/leaves/leave-requests-approval-workflow/intake.md`](../../stories/leaves/leave-requests-approval-workflow/intake.md) lines 78–80) already drafts the approver-routing rule this story's trigger will eventually need: "Employee's request -> Manager/Supervisor/Admin can action it; Supervisor's request -> Manager (or Admin, per 'Admin overrides any role'); Manager's request -> Admin." That rule is restated in Story Goal point 5 purely for the future executor's benefit — it is owned and must be finalized by `leaves`, not decided here.

---

## Story Goal

Give every user a way to see and dismiss their own leave-request notifications, and give the codebase a reusable, fire-and-forget primitive that the (not-yet-built) `leaves` story will call once a leave request is submitted. Concretely:

1. A `Notification` entity: `RecipientUserId` (the notified `ApplicationUser.Id`), `Type` (a new `NotificationType` enum — one member today, `LeaveRequestSubmitted`, matching the intake's own "out of scope: notifications for anything other than leave-request submission"), `ReferenceId` (the referenced leave request's id — a bare, unconstrained `int` for now; see Prerequisites for why), `IsRead`, `CreatedAt`.
2. `GET /api/notifications` — any authenticated user, no new policy — lists only the caller's own notifications, newest first.
3. `PATCH /api/notifications/{id}/read` — any authenticated user — marks one of the caller's own notifications read; returns `404` (not `403`) for someone else's notification or a nonexistent one, matching this codebase's established convention of hiding out-of-scope records behind `NotFound` rather than revealing their existence (already used by `ClientEndpoints`/`TaskEndpoints`/`StatisticsEndpoints`).
4. `Services/INotificationService` / `NotificationService` provides `NotifyLeaveRequestSubmittedAsync(int leaveRequestId, IEnumerable<string> recipientUserIds)` — a primitive that writes one `Notification` row per recipient, catching and logging (never rethrowing) any failure. This exactly mirrors `Middleware/ApiLoggingMiddleware`'s "a side-effect write must never turn a successful business response into a 500" shape (`CLAUDE.md` line 36) — the intake's own Technical Hints name this file as the pattern to follow.
5. This story deliberately does **not** implement or decide the approver-routing rule (who is a valid approver for whose leave request). That logic belongs to `leaves` (its intake already drafts it: Employee's request → Manager/Supervisor/Admin; Supervisor's request → Manager/Admin; Manager's request → Admin). This story only ships the primitive that `leaves`' submit handler will call once it has resolved recipient user ids itself — see "Not in scope" below and Edge Cases for the query pattern it should reuse.
6. Delivery is poll-only (a plain `GET`), per the intake's own explicit default ("Delivery mechanism ... default to a pollable GET endpoint unless planning decides real-time is warranted"). Nothing in this codebase uses SignalR, a message queue, or any push mechanism today (confirmed: no such package reference or pattern anywhere in `EmpoloyeeManagment/**/*.cs`), so introducing one here would be new infrastructure the intake never asked for.

**Not in scope for this story** (do not build these here):
- Wiring `NotifyLeaveRequestSubmittedAsync` into an actual `POST /api/leaves` submit handler — that route does not exist yet (Prerequisites). This is the one piece of the intake's acceptance criteria this story cannot complete; it is prepared for, not implemented.
- The approver-routing rule itself (point 5) — owned by `leaves`.
- Any endpoint to create a notification directly (no `POST /api/notifications`). The intake never asks for user-created notifications — every row is system-generated via `INotificationService`.
- An unread-count endpoint, pagination, or an `unreadOnly` filter on `GET /api/notifications` — none are in the acceptance criteria, and no existing list endpoint in this codebase paginates either (`TaskEndpoints.GetTasksAsync`, `VisitEndpoints.GetVisitsAsync`).
- Email/SMS/push delivery, and notifications for anything other than leave-request submission — both explicitly out of scope per the intake.
- Any change to `Models/Employee.cs`, `TaskEndpoints.cs`, `VisitEndpoints.cs`, `ClientEndpoints.cs`, or their underlying entities — this story only adds a new, independent feature area.

---

## Context — Read These Files First

1. [`../leaves/00-overview.md`](../leaves/00-overview.md) (14 lines, still the stub) and [`../../stories/leaves/leave-requests-approval-workflow/intake.md`](../../stories/leaves/leave-requests-approval-workflow/intake.md) lines 72–85 and 99–102 — confirms `leaves` has no entity/endpoints/story yet, only a drafted-not-implemented `LeaveRequest` shape and approver-routing rule. The reason this story ships a "primitive, not a wired trigger" (Prerequisites, Story Goal).
2. [`../../plans/notifications/00-overview.md`](00-overview.md) — still the stub; this is the first story filed under `notifications/`.
3. [`EmpoloyeeManagment/Models/Enums.cs`](../../../EmpoloyeeManagment/Models/Enums.cs) — whole file (38 lines). `Role` (lines 23–29) and `TaskItemStatus` (lines 31–38) are the exact bare-enum style `NotificationType` (task 1) follows; every enum here is later given `HasConversion<string>()` in `AppDbContext`, never stored as its numeric value.
4. [`EmpoloyeeManagment/Models/TaskItem.cs`](../../../EmpoloyeeManagment/Models/TaskItem.cs) line 9 (`RelatedVisitId`) and [`../visits/04-story-visit-tracking-and-status.md`](../visits/04-story-visit-tracking-and-status.md) Context item 2 — the precedent for a bare, unconstrained `int` reference that gets a real FK only once its target entity exists. `Notification.ReferenceId` (task 2) deliberately follows the same shape.
5. [`EmpoloyeeManagment/Models/ApplicationUser.cs`](../../../EmpoloyeeManagment/Models/ApplicationUser.cs) — whole file (7 lines): `ApplicationUser : IdentityUser` with no custom fields, confirming its `Id` (and therefore `Notification.RecipientUserId`, task 2) is a plain `string`.
6. [`EmpoloyeeManagment/Models/ApiCallLog.cs`](../../../EmpoloyeeManagment/Models/ApiCallLog.cs) line 12 (`UserId`) — the closest existing precedent for a field that stores a raw Identity user id string, though there it's nullable and unconstrained (no FK); contrast with `Notification.RecipientUserId`, which is required and FK-constrained (task 3) because a notification always has exactly one owner.
7. [`EmpoloyeeManagment/Data/AppDbContext.cs`](../../../EmpoloyeeManagment/Data/AppDbContext.cs) — whole file (125 lines). `DbSet` declarations lines 9–15 (add `Notifications` after line 15, task 3). The `Employee` block lines 28–49 (specifically line 30 `HasMaxLength(450)` and lines 40–43's `HasOne<ApplicationUser>().WithMany().HasForeignKey(...)`) is the exact FK-to-`ApplicationUser` shape task 3 reuses. The `Visit` block, lines 112–123, is where the new `Notification` configuration block is inserted directly after (task 3), before line 124's closing `}` of `OnModelCreating`.
8. [`EmpoloyeeManagment/Endpoints/TaskEndpoints.cs`](../../../EmpoloyeeManagment/Endpoints/TaskEndpoints.cs) — whole file (312 lines). `GetTasksAsync` (lines 32–49) and `GetCallerEmployeeAsync`/`CanViewTaskAsync` (lines 290–301) are the row-level "caller sees only their own" pattern this story's endpoints simplify — `Notification` scopes directly on `RecipientUserId`, so no `Employee` lookup is needed at all (task 7).
9. [`EmpoloyeeManagment/Endpoints/VisitEndpoints.cs`](../../../EmpoloyeeManagment/Endpoints/VisitEndpoints.cs) — whole file (128 lines). A second precedent for the same row-level list pattern (`GetVisitsAsync`, lines 23–41) and confirmation that no list endpoint in this codebase paginates.
10. [`EmpoloyeeManagment/Endpoints/UserEndpoints.cs`](../../../EmpoloyeeManagment/Endpoints/UserEndpoints.cs) lines 22–46 (`GetUsersAsync`), specifically lines 30–33 (`db.Roles.Where(r => db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == r.Id))...`) — one of two existing precedents for "resolve every user's role," the query shape `leaves`' future recipient-resolution should reuse (Edge Cases).
11. [`EmpoloyeeManagment/Endpoints/StatisticsEndpoints.cs`](../../../EmpoloyeeManagment/Endpoints/StatisticsEndpoints.cs) lines 153–161 (inside `GetSupervisorTeamStatisticsAsync`) — the second, more direct precedent: resolve a **specific** role's `roleId` first (`db.Roles.Where(r => r.Name == nameof(Role.Supervisor)).Select(r => r.Id).FirstOrDefaultAsync()`), then filter `db.UserRoles` by it. This is the pattern to point to for "get every `ApplicationUser.Id` in role X" (Edge Cases) — simpler than item 10's per-user join since it only needs one role at a time.
12. [`EmpoloyeeManagment/Authorization/AdminAuthorizationHandler.cs`](../../../EmpoloyeeManagment/Authorization/AdminAuthorizationHandler.cs) — whole file (20 lines). Confirms Admin-bypass is purely an **authorization-time** mechanism (it succeeds pending authorization requirements) — it does **not** make an Admin user show up in a `db.UserRoles` query for the `Manager` or `Supervisor` role. This is the reason a future recipient-resolution query must explicitly union in the `Admin` role's users as its own group, never assume the bypass covers it (Edge Cases).
13. [`EmpoloyeeManagment/Middleware/ApiLoggingMiddleware.cs`](../../../EmpoloyeeManagment/Middleware/ApiLoggingMiddleware.cs) — whole file (43 lines). Lines 21–39's `try { ... } catch (Exception ex) { logger.LogError(...); }` (never rethrown) is the exact shape `NotificationService.NotifyLeaveRequestSubmittedAsync` (task 6) copies — named directly in this story's own intake Technical Hints.
14. [`EmpoloyeeManagment/Services/IEmployeeAttendanceService.cs`](../../../EmpoloyeeManagment/Services/IEmployeeAttendanceService.cs) (7 lines) and [`EmployeeAttendanceService.cs`](../../../EmpoloyeeManagment/Services/EmployeeAttendanceService.cs) (28 lines) — the precedent for a small, constructor-injected (primary constructor), cross-feature service registered `AddScoped` in `Program.cs` and called from a different feature's endpoint file. `INotificationService`/`NotificationService` (task 6) follows the identical shape — this is the one case in this codebase where a shared *service* (not a duplicated per-file helper) is the established convention, because the logic (writing rows tied to a side effect) is meant to be called from a different feature entirely, exactly like `IEmployeeAttendanceService` is called from both `TaskEndpoints` and `VisitEndpoints`.
15. [`EmpoloyeeManagment/Dtos/Tasks/TaskDtos.cs`](../../../EmpoloyeeManagment/Dtos/Tasks/TaskDtos.cs) (45 lines), [`Dtos/Visits/VisitDtos.cs`](../../../EmpoloyeeManagment/Dtos/Visits/VisitDtos.cs) (31 lines), [`Dtos/Users/UserDtos.cs`](../../../EmpoloyeeManagment/Dtos/Users/UserDtos.cs) (11 lines) — confirm the one-file-per-feature, one-record-per-shape DTO convention `Dtos/Notifications/NotificationDtos.cs` (task 5) matches.
16. [`EmpoloyeeManagment/Program.cs`](../../../EmpoloyeeManagment/Program.cs) — whole file (158 lines). Line 94 (`builder.Services.AddScoped<IEmployeeAttendanceService, EmployeeAttendanceService>();`) — add the new service registration directly after (task 8). Line 156 (`app.MapClientEndpoints();`) — add `app.MapNotificationEndpoints();` directly after (task 8). Lines 5 and 9 (`using EmpoloyeeManagment.Endpoints;` / `using EmpoloyeeManagment.Services;`) already cover the new files — no new `using` needed. No new authorization policy is added anywhere (lines 76–82 unchanged).
17. Migrations directory listing (`EmpoloyeeManagment/Migrations/*.cs`) — confirms the current latest migration is `20260901152214_AddVisitClientLocationForeignKeys`; this story's own migration (task 4) applies directly after it, and has no ordering dependency on whatever `leaves` eventually adds (no shared FK).
18. [`README.md`](../../../README.md) (102 lines) and [`CLAUDE.md`](../../../CLAUDE.md) (72 lines) — the doc files task 9 updates; exact line numbers are given inline in task 9.

---

## Product rules (from story)

| Area | Current behavior | New behavior |
|---|---|---|
| `Notification` entity | Doesn't exist | New: `RecipientUserId`, `Type` (`NotificationType`, one member: `LeaveRequestSubmitted`), `ReferenceId` (bare `int`, no FK yet), `IsRead`, `CreatedAt` |
| `GET /api/notifications` | Doesn't exist | New — any authenticated user; lists only the caller's own notifications, newest first |
| `PATCH /api/notifications/{id}/read` | Doesn't exist | New — any authenticated user; marks one of the caller's own notifications read; `404` for someone else's or a nonexistent one |
| `INotificationService.NotifyLeaveRequestSubmittedAsync` | Doesn't exist | New — creates one notification per recipient id passed in; never throws (catches + logs). **Not called from anywhere yet** — `POST /api/leaves` doesn't exist |
| `POST /api/leaves` → notify approvers | N/A (`leaves` unplanned) | Deferred — once `leaves` ships its submit endpoint, that handler resolves approver user ids per its own routing rule and calls the primitive above |

---

## Backend Tasks

`No backend changes required outside the files listed below.` The companion frontend work is tracked separately (`employee-management-web` repo, per the intake's "Extra notes").

### 1 — Add the `NotificationType` enum

**File: `EmpoloyeeManagment/Models/Enums.cs`**

Append after the closing `}` of `TaskItemStatus` (line 38):

```csharp

public enum NotificationType
{
    LeaveRequestSubmitted
}
```

One member today, matching the intake's own "out of scope: notifications for anything other than leave-request submission." Adding a second member later (e.g. if a future story adds another trigger) is a non-breaking, additive change — same reasoning `../dashboards/06-story-activity-dashboards-and-stats.md` uses for deferring its own "Leaves" column.

### 2 — Add the `Notification` model

**Create file: `EmpoloyeeManagment/Models/Notification.cs`**

```csharp
namespace EmpoloyeeManagment.Models;

public class Notification
{
    public int Id { get; set; }
    public string RecipientUserId { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public int ReferenceId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

No navigation properties — matches this codebase's established convention of FK-by-id-only (`TaskItem.AssigneeId`, `Employee.UserId`, `Visit.ClientId` — none of them have a nav property either).

### 3 — Register the `Notifications` DbSet and EF configuration

**File: `EmpoloyeeManagment/Data/AppDbContext.cs`**

Add a new `DbSet` after line 15 (`public DbSet<Location> Locations => Set<Location>();`):

```csharp
public DbSet<Notification> Notifications => Set<Notification>();
```

Add a new configuration block directly after the `Visit` block (after line 123, before line 124's closing `}` of `OnModelCreating`):

```csharp
builder.Entity<Notification>(entity =>
{
    entity.Property(n => n.RecipientUserId).HasMaxLength(450).IsRequired();
    entity.Property(n => n.Type).HasConversion<string>().HasMaxLength(50);

    entity.HasOne<ApplicationUser>()
        .WithMany()
        .HasForeignKey(n => n.RecipientUserId)
        .OnDelete(DeleteBehavior.Restrict);
});
```

`HasMaxLength(450)` matches `Employee.UserId` (line 30) and `ApiCallLog.UserId` (line 55) — the standard Identity key length already used twice in this file. `HasMaxLength(50)` on `Type` gives headroom beyond the current longest member name (`LeaveRequestSubmitted`, 22 characters) for any future addition, matching the same `HasConversion<string>()` treatment every other enum column gets. `DeleteBehavior.Restrict` matches the dominant convention for a required reference to an owner row (`TaskItem.AssigneeId`, `Visit.ClientId`/`LocationId`) — no `DELETE /api/users` endpoint exists anywhere in this codebase, so this FK is not exercised in practice today either way, but `Restrict` is the safer, more consistent default.

### 4 — Create the EF Core migration

From the repo root:

```bash
dotnet ef migrations add AddNotifications --project EmpoloyeeManagment
dotnet ef database update --project EmpoloyeeManagment
```

Confirm the generated migration only adds the `Notifications` table (with its FK to `AspNetUsers` and no other table touched). This migration has no ordering dependency on `leaves` — `ReferenceId` is a bare `int`, not an FK, so it applies cleanly regardless of whether `leaves`'s own migration lands before or after it.

### 5 — Create the Notification DTOs

**Create file: `EmpoloyeeManagment/Dtos/Notifications/NotificationDtos.cs`**

```csharp
using EmpoloyeeManagment.Models;

namespace EmpoloyeeManagment.Dtos.Notifications;

public record NotificationDto(int Id, NotificationType Type, int ReferenceId, bool IsRead, DateTime CreatedAt);
```

No `RecipientUserId` field on the DTO — `GET /api/notifications` is always self-scoped (there is no "view another user's notifications" mode in the intake), so the caller already knows who they are; this matches the "don't add fields beyond what's needed" pattern the rest of this codebase follows.

### 6 — Create `Services/INotificationService.cs` and `NotificationService.cs`

**Create file: `EmpoloyeeManagment/Services/INotificationService.cs`**

```csharp
namespace EmpoloyeeManagment.Services;

public interface INotificationService
{
    Task NotifyLeaveRequestSubmittedAsync(int leaveRequestId, IEnumerable<string> recipientUserIds);
}
```

**Create file: `EmpoloyeeManagment/Services/NotificationService.cs`**

```csharp
using EmpoloyeeManagment.Data;
using EmpoloyeeManagment.Models;

namespace EmpoloyeeManagment.Services;

public class NotificationService(AppDbContext db, ILogger<NotificationService> logger) : INotificationService
{
    public async Task NotifyLeaveRequestSubmittedAsync(int leaveRequestId, IEnumerable<string> recipientUserIds)
    {
        try
        {
            var now = DateTime.UtcNow;
            var notifications = recipientUserIds
                .Distinct()
                .Select(userId => new Notification
                {
                    RecipientUserId = userId,
                    Type = NotificationType.LeaveRequestSubmitted,
                    ReferenceId = leaveRequestId,
                    IsRead = false,
                    CreatedAt = now
                });

            db.Notifications.AddRange(notifications);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create leave-request-submitted notifications for leave request {LeaveRequestId}", leaveRequestId);
        }
    }
}
```

`.Distinct()` defensively guards against the caller passing the same user id twice (e.g. an overlapping role-resolution query) — cheap, and avoids duplicate rows for one recipient. The try/catch never rethrows, matching `ApiLoggingMiddleware`'s precedent exactly (Context item 13): whatever calls this (eventually, `leaves`'s submit handler) must be able to complete its own response even if notification creation fails.

### 7 — Create `Endpoints/NotificationEndpoints.cs`

**Create file: `EmpoloyeeManagment/Endpoints/NotificationEndpoints.cs`**

```csharp
using System.Security.Claims;
using EmpoloyeeManagment.Data;
using EmpoloyeeManagment.Dtos.Notifications;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace EmpoloyeeManagment.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications").WithTags("Notifications").RequireAuthorization();

        group.MapGet("/", GetNotificationsAsync);
        group.MapPatch("/{id:int}/read", MarkNotificationReadAsync);

        return app;
    }

    private static async Task<Ok<List<NotificationDto>>> GetNotificationsAsync(ClaimsPrincipal principal, AppDbContext db)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return TypedResults.Ok(new List<NotificationDto>());

        var notifications = await db.Notifications.AsNoTracking()
            .Where(n => n.RecipientUserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationDto(n.Id, n.Type, n.ReferenceId, n.IsRead, n.CreatedAt))
            .ToListAsync();

        return TypedResults.Ok(notifications);
    }

    private static async Task<Results<Ok<NotificationDto>, NotFound>> MarkNotificationReadAsync(
        int id, ClaimsPrincipal principal, AppDbContext db)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id);
        if (notification is null || userId is null || notification.RecipientUserId != userId)
        {
            return TypedResults.NotFound();
        }

        notification.IsRead = true;
        await db.SaveChangesAsync();

        return TypedResults.Ok(new NotificationDto(notification.Id, notification.Type, notification.ReferenceId, notification.IsRead, notification.CreatedAt));
    }
}
```

No `GetCallerEmployeeAsync`-style helper is needed — unlike `TaskEndpoints`/`VisitEndpoints`, `Notification` scopes directly on the raw `RecipientUserId`, so there is no `Employee` lookup at all. The `404`-for-non-owner shape in `MarkNotificationReadAsync` matches `TaskEndpoints`/`ClientEndpoints`'s established convention (Context item 8) of hiding out-of-scope rows behind `NotFound`.

### 8 — Wire up DI and the endpoint group

**File: `EmpoloyeeManagment/Program.cs`**

Add after line 94 (`builder.Services.AddScoped<IEmployeeAttendanceService, EmployeeAttendanceService>();`):

```csharp
builder.Services.AddScoped<INotificationService, NotificationService>();
```

Add after line 156 (`app.MapClientEndpoints();`):

```csharp
app.MapNotificationEndpoints();
```

No new `using`, no new authorization policy — both are already covered (Context item 16).

### 9 — Update docs

**File: `README.md`** — add a new Features bullet after line 15 (the existing "Dashboards & stats" bullet), before line 16 ("API call logging"):

```
- **Notifications** — a `Notification` per recipient (`RecipientUserId`, `Type`, `ReferenceId`, read/unread, timestamp); `GET /api/notifications` lists the caller's own (newest first), `PATCH /api/notifications/{id}/read` marks one read. The trigger — notifying every valid approver when a leave request is submitted — is prepared (`INotificationService.NotifyLeaveRequestSubmittedAsync`) but not yet wired in, since `POST /api/leaves` doesn't exist yet.
```

Add two rows to the API table after line 98 (the existing `GET /api/statistics/supervisors` row):

```
| GET | `/api/notifications` | ✔ | List the caller's own notifications, newest first |
| PATCH | `/api/notifications/{id}/read` | ✔ | Mark one of the caller's own notifications read |
```

Update the "Project structure" block: line 31 (`Data/AppDbContext.cs` row) — append `+ Notifications`; line 32 (`Models/` row) — append `, Notification` to the entity list and `, NotificationType` to the enum list; line 33 (`Dtos/` row) — append `, Notifications`; line 34 (`Endpoints/` row) — append `, NotificationEndpoints`; line 36 (`Services/` row) — append `; INotificationService / NotificationService — leave-request notification creation`.

Update the Roadmap paragraph (line 102): append — "Notifications have shipped for storage and self-service reading (`Notification` entity, `GET /api/notifications`, `PATCH /api/notifications/{id}/read`, and a reusable `INotificationService.NotifyLeaveRequestSubmittedAsync` primitive); the actual trigger on leave-request submission is prepared but not wired in, since it depends on the `leaves` feature's `POST /api/leaves` endpoint, which doesn't exist yet."

**File: `CLAUDE.md`** —

- Line 26 (Architecture, `Map*Endpoints` list): append `, NotificationEndpoints` to the parenthetical list of endpoint classes; add one sentence noting `NotificationEndpoints` is the first feature to scope rows directly on the caller's raw Identity user id (`RecipientUserId`) rather than resolving an `Employee` first.
- Insert a new bolded paragraph directly after line 46 (the "Visits — fully shipped" paragraph), before line 48 (CORS): `**Notifications — storage and reading shipped; the submission trigger is not yet wired in**: ...` describing the `Notification` entity, the two new routes, and that `INotificationService.NotifyLeaveRequestSubmittedAsync` exists but is called from nowhere yet pending `leaves`'s `POST /api/leaves`.
- Line 40 (Data model paragraph): append ", `Notification` (`RecipientUserId` → `ApplicationUser`, `Restrict` delete; `Type` enum; `ReferenceId` a bare unconstrained int — a future leave request id, following the same deferred-FK shape `TaskItem.RelatedVisitId` used before `visits` added its FK)" to the entity list, and add `NotificationType { LeaveRequestSubmitted }` to the "enum values matter" sentence.
- Lines 58, 59, 60, 62 (Project layout): same four additions as the README block above (`Notification`/`NotificationType` to Models; `Notifications` to the Dtos feature list; `NotificationEndpoints` to the Endpoints list; `INotificationService`/`NotificationService` to Services).
- Line 71 (Known v1 gaps): append — "Notifications have shipped for storage and self-service reading (entity, `GET`/`PATCH .../read`); the leave-request-submission trigger itself (`INotificationService.NotifyLeaveRequestSubmittedAsync`) is implemented but not called from anywhere yet, since it depends on the `leaves` feature's `POST /api/leaves`, which doesn't exist yet (no entity, no endpoints, only a stub plan) — see `.squad/plans/notifications/07-story-leave-request-notifications.md`."

---

## Edge Cases & Failure Modes

- **Marking someone else's notification read, or a nonexistent id.** `MarkNotificationReadAsync` (task 7) returns `404` for both — never `403` — matching `ClientEndpoints`/`TaskEndpoints`'s established convention of hiding out-of-scope records behind `NotFound` (Context item 8).
- **Re-marking an already-read notification.** Idempotent: `IsRead = true` is simply set again, `200 Ok` returned — no guard against "already read," matching this codebase's existing preference for not validating scenarios that can't cause harm (e.g. `../dashboards/06-story-activity-dashboards-and-stats.md` Edge Cases, `year` not being range-validated).
- **A caller with zero notifications.** `GET /api/notifications` returns an empty list with `200`, not an error — matches `TaskEndpoints.GetTasksAsync`/`VisitEndpoints.GetVisitsAsync`'s existing "no rows" shape.
- **`NotifyLeaveRequestSubmittedAsync` failing (DB error, etc.).** Caught and logged via `ILogger`, never rethrown (task 6) — a notification-creation failure must never turn a successful leave-request submission into a `500`, mirroring `ApiLoggingMiddleware`'s exact precedent (Context item 13) named directly in this story's own intake.
- **`NotifyLeaveRequestSubmittedAsync` given an empty `recipientUserIds`.** `AddRange` over an empty sequence plus `SaveChangesAsync()` is a safe no-op — no special-casing needed.
- **One invalid/nonexistent recipient user id inside a batch.** Because all rows in one call share a single `SaveChangesAsync()`, one bad `RecipientUserId` (violating the FK, task 3) fails the **entire** batch — every other, valid recipient in that call also silently gets no notification (caught and logged, not surfaced). This is accepted rather than guarded against: recipient ids will always come from a live `db.UserRoles` query (Context items 10–11), not user-supplied input, so an invalid id here would only occur if a role-holding user were deleted in the narrow window between that query and this call — and no `DELETE /api/users` endpoint exists anywhere in this codebase today. Not adding per-row isolation here matches the "don't add error handling for scenarios that can't happen" principle already applied throughout this codebase.
- **Admin as an implicit approver.** `AdminAuthorizationHandler` (Context item 12) only bypasses **authorization checks** — it does not add Admin-role users to any `db.UserRoles`-based query. Whichever future `leaves` handler resolves "Manager/Supervisor/Admin can action this" must run three explicit role lookups (or one `WHERE RoleId IN (...)`), not assume the Admin bypass already covers it. Documented here since it is the single most likely mistake for whoever wires the trigger in later.
- **A recipient with no linked `Employee` row.** Cannot happen for a real signed-up account (`AuthEndpoints.SignupAsync`/`UserEndpoints.CreateUserAsync` both create the `ApplicationUser` + `Employee` pair transactionally, per `CLAUDE.md` line 32), but is also irrelevant here regardless — `Notification` keys directly on `ApplicationUser.Id`, with no `Employee` dependency anywhere in this feature.
- **`NotificationType` has exactly one member.** Deliberate, not an oversight — the intake explicitly scopes this story to leave-request-submission notifications only (Story Goal point 1, "Not in scope").
- **Migration ordering.** `AddNotifications` (task 4) has no FK to any `leaves`-owned type, so it has no ordering dependency on whatever migration `leaves` eventually adds — unlike `visits`'s hard compile-time dependency on `clients` (Context item 4).

---

## Test Plan

This repo has no automated test project yet (`CLAUDE.md` line 22). This story follows the same manual-Swagger-walkthrough pattern the `visits`/`clients`/`dashboards` stories used — with one addition: because no endpoint creates a `Notification` yet (by design — see Story Goal point 5), one row must be seeded directly via SQL to exercise the two new endpoints.

1. **Migration applies cleanly** — `dotnet ef database update --project EmpoloyeeManagment`; confirm a new `Notifications` table exists (with a FK to `AspNetUsers`) and no other table changed.
2. **Seed a test notification** — as Admin, `GET /api/users` and copy the `Id` of any test account (`UserListItemDto.Id`). Using a SQL client against the `DefaultConnection` database:
   ```sql
   INSERT INTO Notifications (RecipientUserId, Type, ReferenceId, IsRead, CreatedAt)
   VALUES ('<copied Id>', 'LeaveRequestSubmitted', 1, 0, GETUTCDATE());
   ```
3. **List, as the recipient** — log in as that account, `GET /api/notifications`; confirm the seeded row appears with `Type: "LeaveRequestSubmitted"`, `ReferenceId: 1`, `IsRead: false`.
4. **List, as a different user** — log in as any other account, `GET /api/notifications`; confirm an empty list (the seeded row does not leak across users).
5. **Mark read, as the recipient** — `PATCH /api/notifications/{id}/read` (the id from step 3); confirm `200`, `IsRead: true`. Re-run `GET /api/notifications` and confirm it persisted.
6. **Mark read, as a non-recipient** — attempt the same `PATCH` as the "different user" from step 4; confirm `404`.
7. **Mark read, nonexistent id** — `PATCH /api/notifications/999999/read`; confirm `404`.
8. **Idempotent re-read** — repeat step 5's `PATCH` a second time on the same (already-read) notification; confirm `200`, no error.
9. **Regression** — confirm `ApiCallLogs` rows are still written for the two new `/api/notifications/*` routes (sanity check that `Middleware/ApiLoggingMiddleware.cs`, unmodified by this story, still wraps them correctly).
10. **Deferred — not part of this story's pass.** The actual "submit a leave request → every valid approver gets notified" flow cannot be exercised until `leaves` ships `POST /api/leaves` and its handler calls `INotificationService.NotifyLeaveRequestSubmittedAsync`. Re-test this story's endpoints as part of that future story's own Test Plan once that wiring exists.

---

## Migration / Rollback

One additive EF Core migration (`AddNotifications`, task 4) adding only the `Notifications` table and its FK to `AspNetUsers` — no existing table or column changes. Applies directly after `20260901152214_AddVisitClientLocationForeignKeys` (the current latest migration); no ordering dependency on `leaves`.

**Half-applied migration.** If `dotnet ef database update` fails partway, re-running it is safe — EF Core migrations are idempotent per-migration, matching Story 02/04's documented behavior.

**Rollback.** `dotnet ef database update 20260901152214_AddVisitClientLocationForeignKeys --project EmpoloyeeManagment`, then delete the `AddNotifications` migration files, `Models/Notification.cs`, the `NotificationType` enum block from `Models/Enums.cs`, `Dtos/Notifications/NotificationDtos.cs`, `Endpoints/NotificationEndpoints.cs`, `Services/INotificationService.cs`, `Services/NotificationService.cs`, and revert the `Program.cs` (task 8) and `Data/AppDbContext.cs` (task 3) additions.

---

## Verification Steps

1. **Backend builds:** `dotnet build` from the repo root (or `EmpoloyeeManagment/`) — succeeds immediately; this story has no unshipped dependency (unlike `visits`).
2. **Database check:** `dotnet ef database update --project EmpoloyeeManagment` — confirm only `AddNotifications` applies.
3. **Run:** `dotnet run --project EmpoloyeeManagment`, open `/swagger`, and execute the full Test Plan above (steps 1–9; step 10 is explicitly deferred).
4. **Regression:** re-run a quick pass of `GET /api/tasks`, `GET /api/visits`, and `GET /api/clients` to confirm nothing this story touched (no shared files were modified beyond `Program.cs`/`AppDbContext.cs` additions) broke the originals.

---

## Done Criteria

- [ ] `NotificationType` enum added to `Models/Enums.cs`; `Notification` model added (`Models/Notification.cs`).
- [ ] `Notifications` DbSet and EF configuration added to `AppDbContext` (FK to `ApplicationUser`, `Restrict` delete); one additive migration (`AddNotifications`) applied after the current latest.
- [ ] `Dtos/Notifications/NotificationDtos.cs` added, matching the existing DTO conventions.
- [ ] `INotificationService`/`NotificationService` added, registered in DI (`Program.cs`), and never throws from `NotifyLeaveRequestSubmittedAsync` (catches + logs).
- [ ] `GET /api/notifications` (caller's own, newest first) and `PATCH /api/notifications/{id}/read` (`404` for non-owner/nonexistent) implemented; no new authorization policy.
- [ ] `README.md` (Features, API table, Project structure, Roadmap) and `CLAUDE.md` (Architecture, Data model, Project layout, Known v1 gaps) updated.
- [ ] Full manual Test Plan (steps 1–9) passes against a local run.
- [ ] Explicitly **not** done, and documented as such rather than silently dropped: wiring `NotifyLeaveRequestSubmittedAsync` into `POST /api/leaves` — blocked on the `leaves` story shipping that endpoint first (Prerequisites, Story Goal, Test Plan step 10).

**STOP HERE. Report to the user and wait for confirmation before proceeding to a future leaves-integration follow-up story (only plannable once the `leaves` feature itself has a real `NN-story-*.md` and a shipped `POST /api/leaves` endpoint to wire `INotificationService.NotifyLeaveRequestSubmittedAsync` into).**

---

## Addendum — the deferred trigger wiring landed (Done Criteria's last unchecked item, now done)

The blocker this story stopped for — `leaves` shipping `POST /api/leaves` — has long since resolved (see `.squad/plans/leaves/07-story-leave-requests-and-approval-workflow.md`), but the trigger itself sat unwired for a while after that (documented as a known v1 gap in `CLAUDE.md`). It has now been wired, plus extended well beyond this story's original single-trigger scope, during the Next.js frontend repo's own `notifications` initiative: that repo's plan (`employee-management-web/.squad/plans/notifications/07-story-leave-request-notifications.md`, Addendum section) needed real notifications to build and verify its UI against, which the user explicitly approved as a one-time exception letting that initiative extend this backend (otherwise treated as a read-only API reference for that work).

**What changed, beyond this story's original scope:**
- `NotificationType` (`Models/Enums.cs`) grew from the one member this story shipped (`LeaveRequestSubmitted`) to six: `+ LeaveRequestAccepted, LeaveRequestRejected, LeaveRequestDelayRequested, TaskAssigned, VisitAssigned`.
- `INotificationService`/`NotificationService` gained one method per new type, all funneling through a new shared private `CreateNotificationsAsync(type, referenceId, recipientUserIds)` — this story's original method (task 6) generalized rather than duplicated six times; the try/catch/log-never-rethrow shape (this story's Edge Cases, Context item 13) is unchanged.
- `LeaveEndpoints.CreateLeaveRequestAsync` now calls `NotifyLeaveRequestSubmittedAsync` — the exact wiring this story deferred (Not-in-Scope bullet 1, Done Criteria's last item, Test Plan step 10). Recipients are resolved by a new `GetEligibleApproverUserIdsAsync` helper in `LeaveEndpoints.cs` itself (not this story's `NotificationService`): every employee whose resolved role outranks the requester's, via the same `AspNetRoles`/`AspNetUserRoles` join and rank table `CanActionLeaveRequest` already uses for authorization — this sidesteps exactly the "Admin as an implicit approver" trap this story's own Edge Cases flagged in advance (`AdminAuthorizationHandler`'s bypass doesn't reach a `db.UserRoles` query; the recipient-resolution helper does its own explicit role lookups, never assumes the bypass covers it).
- `LeaveEndpoints.AcceptLeaveRequestAsync`/`RejectLeaveRequestAsync`/`RequestDelayAsync` each now notify the requester with the matching new type.
- `TaskEndpoints.CreateTaskAsync`/`ReassignTaskAsync` notify the (new) assignee with `TaskAssigned` — outside this story's original scope entirely (this story only ever covered leave-request notifications).
- `VisitEndpoints.CreateVisitAsync` notifies the assignee with `VisitAssigned`, deliberately not also firing `TaskAssigned` for the visit's auto-created driving task.
- `Notification.ReferenceId` stays a bare, unconstrained `int` (this story's original design, Data model) — now meaning a `LeaveRequest.Id`, `TaskItem.Id`, or `Visit.Id` depending on `Type`, still with no real FK.
- Verified live: a fresh employee signup, three leave requests (accept/reject/request-delay) actioned by an existing Manager account, a task creation + reassignment, and a visit creation, each checked against the `Notifications` table directly via `sqlcmd` for the expected recipient(s) and type — including confirming the rank-based fan-out (a Manager's own submission notified only Admins, correctly excluding Supervisors/Managers) and that reassigning a task notifies the *new* assignee, not the old one.
- `README.md`/`CLAUDE.md` updated accordingly (the Known v1 gaps entry this story's own instructions added is now stale and has been corrected).

This story's original Done Criteria, Test Plan, and Edge Cases above remain accurate as a record of what was built *at the time* — they are left unedited; this addendum is the record of what changed afterward, not a rewrite of the original story.
