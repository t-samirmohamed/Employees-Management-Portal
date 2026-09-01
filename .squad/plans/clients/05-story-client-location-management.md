# Story 05 — Client & Location Management

## Prerequisites

- `roles` Story 01 completed: [`../roles/01-story-role-model-and-admin-user-management.md`](../roles/01-story-role-model-and-admin-user-management.md) — this story depends on the `Role` enum, the JWT role claim, and `AdminAuthorizationHandler` it introduced. Verified still in place: `EmpoloyeeManagment/Models/Enums.cs` lines 23–29 (`Role` enum), `EmpoloyeeManagment/Program.cs` lines 76–82 (`AddAuthorization` + handler registration).
- `tasks` Story 02 completed: [`../tasks/02-story-task-assignment-and-comments.md`](../tasks/02-story-task-assignment-and-comments.md) — this story reuses its `TaskAssigner` policy (`Program.cs` line 79, `Admin`/`Manager`/`Supervisor`) rather than inventing a new one, the same reuse the `visits` story already established for `POST /api/visits`.
- **Important — the actual codebase is ahead of `.squad/plans/visits/`'s tracked status.** `.squad/plans/visits/00-overview.md` and [`../visits/04-story-visit-tracking-and-status.md`](../visits/04-story-visit-tracking-and-status.md) both describe `Visit`/`Location`/`Client` as not existing yet and list `clients` as a "hard blocker, not yet planned." That is now stale: a full repo listing confirms `EmpoloyeeManagment/Models/Visit.cs` (10 lines), `EmpoloyeeManagment/Endpoints/VisitEndpoints.cs` (77 lines), and the EF migration `EmpoloyeeManagment/Migrations/20260901145826_AddVisits.cs` **already exist and are already committed** (`git log` shows this landed as part of the `20291f2` "Implement task management system" commit, ahead of a separate `visits` story ever having been executed). Concretely, today:
  - `Visit` has `Id`, `DateTime`, `ClientId` (plain unconstrained `int`), `LocationId` (plain unconstrained `int`), `CreatedAt`.
  - `VisitEndpoints.MapVisitEndpoints` wires up `GET /api/visits` and `GET /api/visits/{id}` only (role/row-scoped exactly like `TaskEndpoints`). `POST /api/visits` is explicitly **not** implemented — `VisitEndpoints.cs` lines 19–24 are a code comment: `// TODO(clients story): add POST "/" -> CreateVisitAsync (TaskAssigner policy) once Client/Location exist. ... See .squad/plans/visits/04-story-visit-tracking-and-status.md task 6 for the full design.`
  - `Data/AppDbContext.cs` already has `public DbSet<Visit> Visits => Set<Visit>();` (line 13) and a completed FK + unique filtered index from `TaskItem.RelatedVisitId` to `Visit` (lines 64–68). Migration `20260901145826_AddVisits.cs` is what created the `Visits` table and that FK/index — it does **not** touch `Client`/`Location` (they don't exist yet at that point in history).
  - `Data/AppDbContext.cs` lines 86–89 carry the exact TODO this story resolves: `// TODO(clients story): Visit.ClientId/LocationId are plain unconstrained ints until Client/Location exist — add a builder.Entity<Visit>() block with HasOne<Client>().HasForeignKey(v => v.ClientId) and HasOne<Location>().HasForeignKey(v => v.LocationId) (both DeleteBehavior.Restrict) once those entities land.`
  - `Dtos/Visits/VisitDtos.cs` already declares `CreateVisitRequest(DateTime DateTime, int ClientId, int LocationId, int AssigneeId, string? Name, string? Notes)` — pre-written for the future `visits` follow-up story, not touched here.
  - This means: **this story does not need to create `Visit` or wire up `POST /api/visits`.** It only needs `Client`/`Location` to exist so it can (a) complete the FK TODO above and (b) implement the `clients`-owned endpoints (list/detail/create/visit-stats). `POST /api/visits` remains a separate, still-open follow-up to `../visits/04-story-visit-tracking-and-status.md` — its own FK-configuration task (that story's task 2) is now redundant with this story's task 3; its remaining work is just the transactional `Visit`+`TaskItem` creation handler and the accepted-visit concurrency check.
- Cross-reference: [`../dashboards/00-overview.md`](../dashboards/00-overview.md) / [`../../stories/dashboards/activity-dashboards-stats/intake.md`](../../stories/dashboards/activity-dashboards-stats/intake.md) — that story's own acceptance criteria explicitly owns "Client visit stats + most-visited-clients ranking (aggregates over visits/clients data)". This story does **not** build a "most visited clients" ranking, to avoid building it twice.

---

## Story Goal

Introduce `Client` and `Location` as first-class entities, expose them to Admin/Manager (full visibility) and Supervisor (scoped to one assigned location) via list/detail/create endpoints, add a per-client visit-count endpoint, and give a Supervisor's "assigned location" — named but never built by any prior story — a real home on `Employee`. Concretely:

1. A `Client` entity exists with `Name`, `Email`, `Contact`. A `Location` entity exists with `Name`, `Email`, `Contact`, and `ClientId` (→ `Client`) — a client has many locations.
2. `Employee` gains `AssignedLocationId` (nullable, FK → `Location`) — the field the intake flagged as "needs a home" but that neither the `roles` story nor any other prior story added. It is settable only through `POST /api/users` (the only endpoint that can create a `Supervisor`-role user in the first place; plain `POST /api/employees` creates an unlinked, role-less `Employee` for which "assigned location" has no meaning — see Edge Cases).
3. `GET /api/clients` and `GET /api/clients/{id}` (Admin/Manager/Supervisor, via the existing `TaskAssigner` policy) return every client for Admin/Manager, and — for a Supervisor — only the one client that owns their `AssignedLocationId`. Detail includes the client's locations. Employees get `403` at the policy level; they are not mentioned anywhere in the intake's client-visibility rules.
4. `POST /api/clients` (Admin/Manager only — a new `ClientManager` policy, since the intake's Supervisor section only ever describes *viewing* clients, never creating them) creates a `Client` and zero or more nested `Location`s transactionally, mirroring the existing `Employee`+`ApplicationUser` (`AuthEndpoints.SignupAsync`) and `Client`-then-child (a `Visit`+`TaskItem`, per the `visits` story's still-pending design) save-parent-then-children pattern.
5. `GET /api/clients/{id}/visit-stats?range=month|3month|6month|year` (same visibility as detail) counts `Visit` rows for that client with `DateTime` on or after a computed cutoff, for the four literal range values named in the source spec.
6. `Data/AppDbContext.cs`'s pending `TODO(clients story)` (lines 86–89, see Prerequisites) is resolved: `Visit.ClientId`/`Visit.LocationId` become real, `Restrict`-delete foreign keys to `Client`/`Location`.

**Not in scope** (do not build these here):
- Editing or deleting a `Client`/`Location`, or adding a `Location` to an already-created `Client` after the fact. The acceptance criteria explicitly left the write surface for this undecided ("Exact write surface (edit/delete) to be confirmed during planning") — this story resolves that ambiguity by committing to **create-only**, matching this codebase's established minimal-write-surface convention (`Employee` has no update/delete either — see `CLAUDE.md`'s "Known v1 gaps"). Document as a gap, do not invent an update/delete endpoint.
- `POST /api/visits` and any Visit-creation logic — see Prerequisites; that remains `../visits/04-story-visit-tracking-and-status.md`'s job.
- "Most visited clients" ranking — owned by `dashboards` (see Prerequisites).
- An endpoint to change an existing user's `AssignedLocationId` after creation — no general user/employee-field-update endpoint exists anywhere in this codebase yet; inventing one here would be out of scope.
- Any validation that a `Location`'s assigned Supervisor role matches — `AssignedLocationId` is a bare nullable FK with no role-type check at the database level, consistent with how `Employee.UserId` also carries no role-type constraint.

---

## Context — Read These Files First

1. [`EmpoloyeeManagment/Models/Employee.cs`](../../../EmpoloyeeManagment/Models/Employee.cs) — whole file (14 lines). `AttendanceStatus` at line 12, `CreatedAt` at line 13 — `AssignedLocationId` (task 2) is inserted between them, matching this codebase's "CreatedAt always last" convention seen on every model (`TaskItem`, `TaskComment`, `Visit`).
2. [`EmpoloyeeManagment/Models/Visit.cs`](../../../EmpoloyeeManagment/Models/Visit.cs) — whole file (10 lines). `ClientId`/`LocationId` (lines 7–8) are the plain unconstrained `int`s that task 3 below wires to real FKs — this file's own code is **not** modified, only `AppDbContext`'s configuration of it.
3. [`EmpoloyeeManagment/Data/AppDbContext.cs`](../../../EmpoloyeeManagment/Data/AppDbContext.cs) — whole file (91 lines). `DbSet` declarations lines 9–13 (add `Clients`/`Locations` after line 13, task 3). The `Employee` block, lines 26–42 — `entity.HasOne<ApplicationUser>()...OnDelete(DeleteBehavior.SetNull);` at lines 38–41 is the FK pattern task 3's new `AssignedLocationId → Location` FK (added inside this same block) follows, except with `Restrict` (see task 3 for why). The TODO comment at lines 86–89 is replaced outright by task 3's `builder.Entity<Client>()`/`builder.Entity<Location>()`/`builder.Entity<Visit>()` blocks — it names the exact FK shape to add, verbatim.
4. [`EmpoloyeeManagment/Endpoints/VisitEndpoints.cs`](../../../EmpoloyeeManagment/Endpoints/VisitEndpoints.cs) — whole file (77 lines). The TODO at lines 19–24 remains **valid and unimplemented** after this story (it belongs to `visits`, not here) — do not touch this file. `IsPrivileged` (lines 66–67), `GetCallerEmployeeAsync` (lines 69–73) are the per-file-duplicated-helper convention (not a shared service) that this story's new `Endpoints/ClientEndpoints.cs` (task 8) also follows for its own, differently-scoped equivalents.
5. [`EmpoloyeeManagment/Migrations/20260901145826_AddVisits.cs`](../../../EmpoloyeeManagment/Migrations/20260901145826_AddVisits.cs) — whole file (61 lines). Confirms `Visits` (plain `ClientId`/`LocationId` int columns, no FK) and the `Tasks.RelatedVisitId` FK/unique index already exist in the database — this is the migration baseline this story's own migration (task 4) applies on top of. This is the **most recent** migration in the repo (confirmed via a direct directory listing, timestamped after `20260901141421_AddTasksAndComments` on the same day) — the Rollback target if this story's migration needs reverting.
6. [`EmpoloyeeManagment/Dtos/Users/UserDtos.cs`](../../../EmpoloyeeManagment/Dtos/Users/UserDtos.cs) (whole file, 11 lines) and [`Endpoints/UserEndpoints.cs`](../../../EmpoloyeeManagment/Endpoints/UserEndpoints.cs) (whole file, 89 lines) — `CreateUserRequest` (line 5), `EmployeeSummaryDto` (line 9) gain `AssignedLocationId` (task 7). `CreateUserAsync` (lines 48–87): the existence-check-before-transaction pattern task 7 adds mirrors this method's own `!result.Succeeded` early-return (lines 62–68) — fail fast before any write. `GetUsersAsync`'s projection (line 36) is the one line that must also change to surface the new field.
7. [`EmpoloyeeManagment/Dtos/Visits/VisitDtos.cs`](../../../EmpoloyeeManagment/Dtos/Visits/VisitDtos.cs) (whole file, 31 lines) and [`Dtos/Tasks/TaskDtos.cs`](../../../EmpoloyeeManagment/Dtos/Tasks/TaskDtos.cs) — the one-file-per-feature, one-record-per-shape DTO convention `Dtos/Clients/ClientDtos.cs` (task 6) must match.
8. [`EmpoloyeeManagment/Program.cs`](../../../EmpoloyeeManagment/Program.cs) — whole file (156 lines). `AddAuthorization` block, lines 76–81 — add a fourth policy (task 5). `MapXEndpoints()` calls, lines 149–154 (`app.MapVisitEndpoints();` is the last one, line 154) — add `app.MapClientEndpoints();` after it (task 9). No new `using` needed — `EmpoloyeeManagment.Endpoints` is already imported (line 5).
9. [`../tasks/02-story-task-assignment-and-comments.md`](../tasks/02-story-task-assignment-and-comments.md) — read in full as the structural/tonal precedent (per the planning process's "find a similar precedent" step); its role/row-scoping split (policy for "can you call this route at all," manual in-handler checks for "which rows") is exactly what this story's `ClientEndpoints.cs` (task 8) reuses. [`../visits/04-story-visit-tracking-and-status.md`](../visits/04-story-visit-tracking-and-status.md) is also useful for its `CreateVisitAsync` save-parent-then-children transactional shape (task 6 here follows the same shape for `Client`+`Location`) — but per Prerequisites, treat its file-state claims (e.g. "`Visit.cs` doesn't exist yet") as stale; verify current line numbers directly rather than trusting that document's line citations.
10. [`README.md`](../../../README.md) — whole file (89 lines). Features list (line 12, the Tasks bullet — add a Clients bullet after it); Project structure block (lines 25–37: line 28's `Data/AppDbContext.cs` comment, line 29's Models comment, and line 31's Endpoints comment are all already stale — missing `Tasks`/`TaskComments`/`Visits`/`TaskItem`/`TaskComment`/`Visit`/`VisitEndpoints` from a prior story's incomplete doc update; task 10 corrects these fully, not just adding this story's own new types, since it is already editing these exact lines); API table (lines 61–84, **and note it is missing `GET /api/visits`/`GET /api/visits/{id}` entirely** — another pre-existing gap folded into task 10 for the same reason); Roadmap paragraph (line 88).
11. [`CLAUDE.md`](../../../CLAUDE.md) — whole file (72 lines). Unlike `README.md`, this file's Architecture section is already current on `Visit`/`VisitEndpoints` (see line 26's Map*Endpoints list, line 40's Data model paragraph, and line 46's dedicated "Visits — read side shipped, creation blocked on `clients`" paragraph) — task 10 updates line 26 (add `ClientEndpoints`, the new `ClientManager` policy), line 40 (add `Client`/`Location`, `Employee.AssignedLocationId`), line 46 (the "blocked on `clients`" framing changes — `Client`/`Location` now exist, only the `POST /api/visits` handler itself remains unbuilt), the Project layout block (lines 54–67 — line 58's Models comment is missing `Visit` entirely, a pre-existing gap folded in here for the same reason as README's), and line 71 (Known v1 gaps — append this story's own deliberate gaps).

---

## Product rules (from story)

| Area | Current behavior | New behavior |
|---|---|---|
| `Client` / `Location` | Don't exist | New entities: `Client` (`Name`, `Email`, `Contact`); `Location` (`Name`, `Email`, `Contact`, FK `ClientId → Client`) |
| `Visit.ClientId` / `Visit.LocationId` | Plain unconstrained `int`s (`Visit`/`VisitEndpoints.cs` already exist; `POST /api/visits` still unimplemented) | FK-constrained to `Client`/`Location` (`Restrict`) |
| `Employee.AssignedLocationId` | Doesn't exist | New nullable FK → `Location`; settable only via `POST /api/users`'s new `AssignedLocationId` field |
| `GET /api/clients` | Doesn't exist | New — **Admin/Manager/Supervisor** (`TaskAssigner`); Admin/Manager see all clients; Supervisor sees only the client that owns their `AssignedLocationId` (or an empty list if unset) |
| `GET /api/clients/{id}` | Doesn't exist | New — same visibility rule; returns the client plus its locations; `404` for an out-of-scope Supervisor |
| `POST /api/clients` | Doesn't exist | New — **Admin/Manager only** (new `ClientManager` policy) — creates a `Client` + zero-or-more nested `Location`s transactionally |
| `GET /api/clients/{id}/visit-stats?range=` | Doesn't exist | New — same visibility as detail; `range` ∈ `{month, 3month, 6month, year}`; counts `Visit` rows for the client with `DateTime` ≥ a computed cutoff; `400` for any other `range` value |
| `POST /api/users` (`CreateUserRequest`) | No location concept | Gains optional `AssignedLocationId`; `404` if given but no such `Location` exists |
| `GET /api/users` (`EmployeeSummaryDto`) | No location concept | Gains `AssignedLocationId` so an Admin can see what a Supervisor was assigned |
| Client/Location edit/delete | N/A | Not built — acceptance criteria left this undecided; this story commits to create-only and documents the rest as a gap |

---

## Backend Tasks

`No frontend changes required in this repository` — the companion UI work is tracked in the separate `employee-management-web` repo per the intake's "Extra notes."

### 1 — Add the `Client` and `Location` models

**Create file: `EmpoloyeeManagment/Models/Client.cs`**

```csharp
namespace EmpoloyeeManagment.Models;

public class Client
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Contact { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

**Create file: `EmpoloyeeManagment/Models/Location.cs`**

```csharp
namespace EmpoloyeeManagment.Models;

public class Location
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Contact { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

Neither has a navigation property to the other — matches this codebase's established FK-by-id-only convention (`TaskItem.AssigneeId`, `Visit.ClientId`/`LocationId` — none of these have nav properties either).

### 2 — Add `Employee.AssignedLocationId`

**File: `EmpoloyeeManagment/Models/Employee.cs`**

Insert between `AttendanceStatus` (line 12) and `CreatedAt` (line 13):

```csharp
    public AttendanceStatus AttendanceStatus { get; set; } = AttendanceStatus.InOffice;
    public int? AssignedLocationId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
```

Nullable — most employees (and even most Supervisors, until an Admin sets it) have no assigned location. No role-type check is enforced at the database level; see Edge Cases.

### 3 — Register the new DbSets and EF configuration; resolve the `Visit` FK TODO

**File: `EmpoloyeeManagment/Data/AppDbContext.cs`**

Add two `DbSet`s after line 13 (`public DbSet<Visit> Visits => Set<Visit>();`):

```csharp
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Location> Locations => Set<Location>();
```

Inside the existing `builder.Entity<Employee>(...)` block (lines 26–42), add a new FK directly after the existing `HasOne<ApplicationUser>()` block (after line 41's `.OnDelete(DeleteBehavior.SetNull);`, before the block's closing `});`):

```csharp
            entity.HasOne<Location>()
                .WithMany()
                .HasForeignKey(e => e.AssignedLocationId)
                .OnDelete(DeleteBehavior.Restrict);
```

`Restrict`, not `SetNull` like `UserId` — an assigned `Location` should not silently vanish out from under a Supervisor; nothing deletes a `Location` in this story anyway (create-only), so this is precautionary for whenever a delete endpoint is eventually considered. No `entity.Property(...)` call is needed for `AssignedLocationId` itself — a bare nullable `int` needs no explicit configuration, same as `TaskItem.RelatedVisitId`.

Replace the `TODO(clients story)` comment block (lines 86–89) with:

```csharp
        builder.Entity<Client>(entity =>
        {
            entity.Property(c => c.Name).HasMaxLength(200).IsRequired();
            entity.Property(c => c.Email).HasMaxLength(256).IsRequired();
            entity.Property(c => c.Contact).HasMaxLength(100).IsRequired();
        });

        builder.Entity<Location>(entity =>
        {
            entity.Property(l => l.Name).HasMaxLength(200).IsRequired();
            entity.Property(l => l.Email).HasMaxLength(256).IsRequired();
            entity.Property(l => l.Contact).HasMaxLength(100).IsRequired();

            entity.HasOne<Client>()
                .WithMany()
                .HasForeignKey(l => l.ClientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

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

`Location.ClientId → Client` uses `Cascade` (a location is meaningless without its client, the same reasoning already applied to `TaskComment.TaskId → TaskItem`) — currently unreachable in practice since no delete endpoint exists for `Client`, but consistent with the rest of the model. `Visit`'s two FKs use `Restrict`, exactly as the TODO comment specified. `200`/`256`/`100` max-lengths mirror `Employee.FirstName`/`Employee.Email`'s existing conventions (`100`/`256`); `200` for `Name` (vs. `Employee`'s `100`) matches `TaskItem.Name`'s precedent for a business/entity name rather than a person's given name. `100` for `Contact` is this story's own judgment call — no existing field of this kind to match.

### 4 — Create the EF Core migration

From the repo root:

```bash
dotnet ef migrations add AddClientsAndLocations --project EmpoloyeeManagment
dotnet ef database update --project EmpoloyeeManagment
```

**This migration will be larger than its name suggests.** Because `Visit.ClientId`/`Visit.LocationId` were left unconstrained by `20260901145826_AddVisits.cs` specifically pending this story (see Prerequisites), the generated migration will contain, in addition to the two new tables:
- `AddColumn` for `Employees.AssignedLocationId` + its new FK.
- `AddForeignKey` for `Visits.ClientId → Clients` and `Visits.LocationId → Locations` (no column changes on `Visits` itself — the columns already exist).

Confirm the generated migration touches exactly `Clients` (new), `Locations` (new), `Employees` (new column + FK), and `Visits` (two new FKs) — if it reports changes to `Tasks`, `TaskComments`, or any `AspNet*` table, stop and investigate before applying.

### 5 — Add the `ClientManager` authorization policy

**File: `EmpoloyeeManagment/Program.cs`**

Extend the `AddAuthorization` block (lines 76–81) with a fourth policy:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(nameof(Role.Admin)));
    options.AddPolicy("TaskAssigner", policy => policy.RequireRole(nameof(Role.Admin), nameof(Role.Manager), nameof(Role.Supervisor)));
    options.AddPolicy("EmployeeOnly", policy => policy.RequireRole(nameof(Role.Employee)));
    options.AddPolicy("ClientManager", policy => policy.RequireRole(nameof(Role.Admin), nameof(Role.Manager)));
});
```

`ClientManager` (Admin/Manager, no Supervisor) is deliberately narrower than `TaskAssigner` — the intake's Supervisor section only ever describes *viewing* clients, never creating them.

### 6 — Create the Clients DTOs

**Create file: `EmpoloyeeManagment/Dtos/Clients/ClientDtos.cs`**

```csharp
namespace EmpoloyeeManagment.Dtos.Clients;

public record CreateLocationRequest(string Name, string Email, string Contact);

public record CreateClientRequest(string Name, string Email, string Contact, List<CreateLocationRequest>? Locations);

public record LocationDto(int Id, string Name, string Email, string Contact);

public record ClientListItemDto(int Id, string Name, string Email, string Contact);

public record ClientDetailDto(int Id, string Name, string Email, string Contact, DateTime CreatedAt, List<LocationDto> Locations);

public record ClientVisitStatsDto(int ClientId, string Range, int VisitCount);
```

No `using EmpoloyeeManagment.Models;` needed — unlike `TaskDtos.cs`/`VisitDtos.cs`, none of these records reference a `Models` enum or type. `CreateClientRequest.Locations` is nullable (a client can be created with zero locations) — the handler (task 8) treats a `null` the same as an empty list.

### 7 — Give Supervisors a home for their assigned location via `POST /api/users`

**File: `EmpoloyeeManagment/Dtos/Users/UserDtos.cs`**

Change line 5 and line 9:

```csharp
public record CreateUserRequest(string Email, string Password, string FirstName, string LastName, Gender Gender, Role Role, int? AssignedLocationId);

public record UserResponse(string Id, string Email, Role Role);

public record EmployeeSummaryDto(int Id, string FirstName, string LastName, EmployeeStatus Status, int? AssignedLocationId);
```

**File: `EmpoloyeeManagment/Endpoints/UserEndpoints.cs`**

Change `GetUsersAsync`'s projection at line 36 from:

```csharp
                    .Select(e => new EmployeeSummaryDto(e.Id, e.FirstName, e.LastName, e.Status))
```

to:

```csharp
                    .Select(e => new EmployeeSummaryDto(e.Id, e.FirstName, e.LastName, e.Status, e.AssignedLocationId))
```

Change `CreateUserAsync`'s signature (lines 48–51) and add a validation check before the transaction opens (before line 53):

```csharp
    private static async Task<Results<Created<UserResponse>, ValidationProblem, NotFound>> CreateUserAsync(
        CreateUserRequest request,
        UserManager<ApplicationUser> userManager,
        AppDbContext db)
    {
        if (request.AssignedLocationId is not null)
        {
            var locationExists = await db.Locations.AnyAsync(l => l.Id == request.AssignedLocationId);
            if (!locationExists) return TypedResults.NotFound();
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
```

Add `AssignedLocationId = request.AssignedLocationId,` inside the `Employee` object literal (lines 72–82), after `AttendanceStatus = AttendanceStatus.OutOfOffice,` (line 80) and before `CreatedAt = DateTime.UtcNow` (line 81).

The existence check runs **before** `BeginTransactionAsync()` — fail fast on a bad `AssignedLocationId` without ever creating the `ApplicationUser`, mirroring this same method's existing `!result.Succeeded` early-return (lines 62–68) for `UserManager.CreateAsync` failures. `db.Locations.AnyAsync` requires no new `using` — `Microsoft.EntityFrameworkCore` is already imported (line 6).

`AssignedLocationId` is added here — not to `Dtos/Employees/EmployeeDtos.cs` / `CreateEmployeeAsync` — because `POST /api/employees` creates an unlinked, role-less `Employee` (no `UserId`, confirmed by `EmployeeEndpoints.CreateEmployeeAsync`), and "Supervisor's assigned location" is only meaningful for an `Employee` linked to a `Supervisor`-role `ApplicationUser`. `POST /api/users` is the only endpoint in this codebase that can create such a user (self-signup always assigns `Employee`, per the `roles` story). No changes to `Endpoints/EmployeeEndpoints.cs` or `Dtos/Employees/EmployeeDtos.cs` are needed or made.

### 8 — Create `Endpoints/ClientEndpoints.cs`

**Create file: `EmpoloyeeManagment/Endpoints/ClientEndpoints.cs`**

```csharp
using System.Security.Claims;
using EmpoloyeeManagment.Data;
using EmpoloyeeManagment.Dtos.Clients;
using EmpoloyeeManagment.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace EmpoloyeeManagment.Endpoints;

public static class ClientEndpoints
{
    public static IEndpointRouteBuilder MapClientEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/clients").WithTags("Clients").RequireAuthorization("TaskAssigner");

        group.MapGet("/", GetClientsAsync);
        group.MapGet("/{id:int}", GetClientByIdAsync);
        group.MapPost("/", CreateClientAsync).RequireAuthorization("ClientManager");
        group.MapGet("/{id:int}/visit-stats", GetClientVisitStatsAsync);

        return app;
    }

    private static async Task<Ok<List<ClientListItemDto>>> GetClientsAsync(ClaimsPrincipal principal, AppDbContext db)
    {
        var query = db.Clients.AsNoTracking().AsQueryable();

        if (!HasFullClientAccess(principal))
        {
            var assignedClientId = await GetSupervisorAssignedClientIdAsync(principal, db);
            if (assignedClientId is null) return TypedResults.Ok(new List<ClientListItemDto>());
            query = query.Where(c => c.Id == assignedClientId);
        }

        var clients = await query
            .OrderBy(c => c.Name)
            .Select(c => new ClientListItemDto(c.Id, c.Name, c.Email, c.Contact))
            .ToListAsync();

        return TypedResults.Ok(clients);
    }

    private static async Task<Results<Ok<ClientDetailDto>, NotFound>> GetClientByIdAsync(int id, ClaimsPrincipal principal, AppDbContext db)
    {
        var client = await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (client is null) return TypedResults.NotFound();

        if (!await CanViewClientAsync(id, principal, db)) return TypedResults.NotFound();

        var locations = await db.Locations.AsNoTracking()
            .Where(l => l.ClientId == id)
            .Select(l => new LocationDto(l.Id, l.Name, l.Email, l.Contact))
            .ToListAsync();

        return TypedResults.Ok(new ClientDetailDto(client.Id, client.Name, client.Email, client.Contact, client.CreatedAt, locations));
    }

    private static async Task<Created<ClientDetailDto>> CreateClientAsync(CreateClientRequest request, AppDbContext db)
    {
        // Client is saved first to get its generated Id, then Locations are created
        // referencing it — the same save-parent-then-children shape as the (not yet
        // built) Visit+TaskItem creation designed in the visits story.
        await using var transaction = await db.Database.BeginTransactionAsync();

        var client = new Client
        {
            Name = request.Name,
            Email = request.Email,
            Contact = request.Contact,
            CreatedAt = DateTime.UtcNow
        };
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        var locations = (request.Locations ?? [])
            .Select(l => new Location
            {
                ClientId = client.Id,
                Name = l.Name,
                Email = l.Email,
                Contact = l.Contact,
                CreatedAt = DateTime.UtcNow
            })
            .ToList();

        db.Locations.AddRange(locations);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        var locationDtos = locations.Select(l => new LocationDto(l.Id, l.Name, l.Email, l.Contact)).ToList();
        var detail = new ClientDetailDto(client.Id, client.Name, client.Email, client.Contact, client.CreatedAt, locationDtos);

        return TypedResults.Created($"/api/clients/{client.Id}", detail);
    }

    private static async Task<Results<Ok<ClientVisitStatsDto>, NotFound, BadRequest<string>>> GetClientVisitStatsAsync(
        int id, string range, ClaimsPrincipal principal, AppDbContext db)
    {
        var client = await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (client is null) return TypedResults.NotFound();

        if (!await CanViewClientAsync(id, principal, db)) return TypedResults.NotFound();

        var cutoff = GetRangeCutoff(range);
        if (cutoff is null)
        {
            return TypedResults.BadRequest("range must be one of: month, 3month, 6month, year.");
        }

        var visitCount = await db.Visits.CountAsync(v => v.ClientId == id && v.DateTime >= cutoff);

        return TypedResults.Ok(new ClientVisitStatsDto(id, range, visitCount));
    }

    private static DateTime? GetRangeCutoff(string range) => range switch
    {
        "month" => DateTime.UtcNow.AddMonths(-1),
        "3month" => DateTime.UtcNow.AddMonths(-3),
        "6month" => DateTime.UtcNow.AddMonths(-6),
        "year" => DateTime.UtcNow.AddYears(-1),
        _ => null
    };

    private static bool HasFullClientAccess(ClaimsPrincipal principal) =>
        principal.IsInRole(nameof(Role.Admin)) || principal.IsInRole(nameof(Role.Manager));

    private static async Task<bool> CanViewClientAsync(int clientId, ClaimsPrincipal principal, AppDbContext db)
    {
        if (HasFullClientAccess(principal)) return true;
        var assignedClientId = await GetSupervisorAssignedClientIdAsync(principal, db);
        return assignedClientId == clientId;
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
}
```

`HasFullClientAccess` is intentionally a different helper from `TaskEndpoints`/`VisitEndpoints`'s `IsPrivileged` (Admin/Manager/**Supervisor**) — here Supervisor is explicitly *not* full access, it is the scoped case, so reusing the name `IsPrivileged` for a different role set would be misleading. `GetSupervisorAssignedClientIdAsync` is shared by all three handlers that need row-scoping (`GetClientsAsync`, `CanViewClientAsync` used by both `GetClientByIdAsync` and `GetClientVisitStatsAsync`) to avoid the duplicate two-query resolution logic. This does **not** cross a file boundary — it stays a private static method inside this one file, consistent with the codebase's no-cross-feature-service convention.

### 9 — Wire up the endpoint group

**File: `EmpoloyeeManagment/Program.cs`**

Add after line 154 (`app.MapVisitEndpoints();`):

```csharp
app.MapClientEndpoints();
```

### 10 — Update docs

**File: `README.md`**
- Add a bullet after line 12 (the Tasks bullet): `` **Clients & locations** — Admin/Manager list every client with its locations; Supervisor sees only the client that owns their assigned location. Admin/Manager create a client with zero or more nested locations in one call. Per-client visit counts for the last month/3 months/6 months/year. ``
- Line 28 (`Data/AppDbContext.cs` comment): update to `EF Core DbContext (Identity + Employees + ApiCallLogs + Tasks + TaskComments + Visits + Clients + Locations)`.
- Line 29 (Models comment): update to `Employee, ApplicationUser, ApiCallLog, TaskItem, TaskComment, Visit, Client, Location, enums (Gender, EmployeeStatus, AttendanceStatus, Role, TaskItemStatus)` — this also corrects the pre-existing omission of `TaskItem`/`TaskComment`/`Visit`/`TaskItemStatus` left over from an earlier story's incomplete doc update, since this line is already being edited for this story's own new types.
- Line 31 (Endpoints comment): update to `One static class per feature area (AuthEndpoints, EmployeeEndpoints, StatisticsEndpoints, UserEndpoints, TaskEndpoints, VisitEndpoints, ClientEndpoints)` — again folding in the pre-existing missing `VisitEndpoints`.
- API table (after line 84): add rows for `GET /api/clients`, `GET /api/clients/{id}`, `POST /api/clients`, `GET /api/clients/{id}/visit-stats`, and — since they are missing entirely — `GET /api/visits`, `GET /api/visits/{id}` (Employee: own only; Admin/Manager/Supervisor: all).
- Line 88 (Roadmap): note clients/locations have shipped (list/detail/create, Supervisor location-scoping, visit-count stats); note Client/Location edit/delete and adding a location to an existing client are still out of scope; note `POST /api/visits` is still not implemented (entities now exist, only the handler is missing).

**File: `CLAUDE.md`**
- Line 26 (Composition root paragraph): add `ClientEndpoints` to the `Map*Endpoints` list; add a clause noting `ClientEndpoints`'s `POST` uses the new `ClientManager` policy (Admin/Manager, deliberately excluding Supervisor) layered over the group's `TaskAssigner`.
- Line 40 (Data model paragraph): add `Client` (`Name`/`Email`/`Contact`) and `Location` (`Name`/`Email`/`Contact`, FK `ClientId → Client`, `Cascade`); update the `Employee` clause to mention `AssignedLocationId` (nullable FK → `Location`, set only via `POST /api/users`); update the `Visit` clause — `ClientId`/`LocationId` are now FK-constrained (`Restrict`), remove the "no FK yet" wording and the `TODO(clients story)` cross-reference (now resolved).
- Line 46 ("Visits — read side shipped, creation blocked on `clients`"): update — `Client`/`Location` now exist, so this is no longer blocked on missing entities; `POST /api/visits` is simply not implemented yet. Note that `../visits/04-story-visit-tracking-and-status.md` task 6's FK-configuration portion is now redundant with this story's task 3.
- Project layout (lines 54–67): line 57 (`Data/AppDbContext.cs` comment) → add `+ Visits + Clients + Locations`; line 58 (Models) → add `Visit` (missing pre-existing), `Client`, `Location`; line 59 (Dtos) → add `Visits` (missing pre-existing), `Clients`; line 60 (Endpoints) → add `VisitEndpoints` (missing pre-existing), `ClientEndpoints`.
- Line 71 (Known v1 gaps): append — Client/Location have no edit/delete endpoints and a location cannot be added to an already-created client; a Supervisor's `AssignedLocationId` cannot be changed after user creation (no user/employee field-update endpoint exists); "most visited clients" ranking is owned by the `dashboards` story, not duplicated here.

---

## Edge Cases & Failure Modes

- **Supervisor with no `AssignedLocationId`.** `GetSupervisorAssignedClientIdAsync` returns `null` if the caller has no linked `Employee`, or the `Employee` has a `null` `AssignedLocationId`. `GetClientsAsync` returns an empty list; `GetClientByIdAsync`/`GetClientVisitStatsAsync` return `404` for any `id` — matches this codebase's existing defensive style for an unresolvable caller (`TaskEndpoints.GetTasksAsync`'s identical `caller is null` → empty-list handling).
- **Employee role.** Blocked entirely at the `TaskAssigner` group-level policy (`403`) — the intake's client-visibility rules never mention Employees, so unlike Tasks (where an Employee sees their own), there is no row-level Employee case to handle in `ClientEndpoints.cs` at all.
- **`CreateClientRequest.Locations` omitted or `null`.** Treated as an empty list (`request.Locations ?? []`, task 8) — a `Client` with zero locations is a valid, expected state, not an error.
- **`POST /api/users` with a nonexistent `AssignedLocationId`.** Returns `404` *before* `BeginTransactionAsync()` runs (task 7) — no `ApplicationUser`/`Employee` is created, so there is no partial state to roll back.
- **`AssignedLocationId` set on a non-`Supervisor` user.** Nothing prevents this at the database level (task 2's FK has no role-type check, mirroring `Employee.UserId`'s own lack of one) — it is simply never consulted by `ClientEndpoints.cs`'s scoping logic for any role other than the "not `HasFullClientAccess`" branch, which in practice only Supervisors reach (Admin/Manager short-circuit first; Employee never reaches the handler).
- **No way to change `AssignedLocationId` after user creation, or to add a `Location` to an already-created `Client`.** Both are deliberate, documented v1 gaps (Story Goal, "Not in scope") — consistent with `Employee` having no general update endpoint anywhere in this codebase.
- **The generated migration is larger than "Clients + Locations."** See task 4 — it also carries the pending `Visits` FK wiring and the new `Employees` column. Confirm the migration's actual contents before applying; do not assume the migration only touches the two new tables.
- **`GET /api/clients/{id}/visit-stats` always returns `VisitCount: 0` immediately after this story ships.** Expected, not a bug: `POST /api/visits` still doesn't exist (see Prerequisites), so no `Visit` rows can exist yet in any environment that only uses this API's endpoints.
- **Visit-count semantics.** `GetClientVisitStatsAsync` counts every `Visit` row for the client with `DateTime >= cutoff`, regardless of the status of its linked `TaskItem` (a cancelled or rejected visit still counts), and with **no upper bound** on `DateTime` — a visit scheduled for next week counts toward "this month" the moment it is created, not just once it has actually occurred. This is the literal, simplest reading of "stats of visits for this month/3month/6month/year" from the source spec; an alternative reading (count only visits whose linked task reached `Done`) is equally plausible and not ruled out by the acceptance criteria — flagged here rather than silently decided, in case product feedback later prefers the `Done`-only interpretation.
- **`range` query parameter.** An unrecognized value (e.g. `week`) returns `400` via `GetRangeCutoff` returning `null` (task 8). Omitting `range` entirely is **not** custom-handled — minimal API's standard model binding treats a non-nullable `string` parameter with no default as required and auto-generates its own `400` for a missing one; do not add redundant handling for this case.
- **`Location.ClientId → Client` uses `Cascade` delete**, but no `DELETE /api/clients/{id}` endpoint exists anywhere in this story — currently unreachable in practice, documented for whenever a delete endpoint is eventually considered.
- **The `TODO(clients story)` comment in `Endpoints/VisitEndpoints.cs` (lines 19–24) is intentionally left in place.** Its precondition (`Client`/`Location` existing) is satisfied by this story, but the `POST /api/visits` handler it describes is not implemented here — the comment remains accurate about what is still missing and should not be deleted or "resolved" by this story.

---

## Test Plan

This repo has no automated test project yet (`CLAUDE.md`, line 22: "There are no automated tests in this repo yet; verification is manual via Swagger"). This story follows the same manual-Swagger-walkthrough pattern used by the `roles` and `tasks` stories.

1. **Migration applies cleanly** — `dotnet ef database update --project EmpoloyeeManagment`; confirm `Clients`/`Locations` tables exist, `Employees` has a new `AssignedLocationId` column + FK, `Visits` has two new FKs (`ClientId`, `LocationId`), and no other table changed.
2. **Create + role gating** — as Admin (or Manager), `POST /api/clients` with `Name`/`Email`/`Contact` and two nested `Locations`; confirm `201` with both locations present in the response, each with a real (non-zero) `Id`. Retry the same request as a Supervisor; confirm `403` (blocked by `ClientManager`). Retry as a plain Employee; confirm `403` (blocked by the group's `TaskAssigner`).
3. **List/detail as Admin/Manager** — `GET /api/clients` includes the new client; `GET /api/clients/{id}` returns it with both locations.
4. **Supervisor scoping — assigned** — `POST /api/users` a new Supervisor (`Role: "Supervisor"`, `AssignedLocationId` = one of the two location ids from step 2); log in as them. `GET /api/clients` returns exactly the one client that owns that location. `GET /api/clients/{id}` for a *different* client (create a second client first) returns `404`.
5. **Supervisor scoping — unassigned** — `POST /api/users` a second Supervisor with `AssignedLocationId` omitted (`null`); confirm `GET /api/clients` returns an empty list for them, and `GET /api/clients/{id}` for any client returns `404`.
6. **`POST /api/users` location validation** — `AssignedLocationId` set to a nonexistent id; confirm `404` and that no new row appears in `GET /api/users` (no orphan user was created).
7. **Visit stats** — `GET /api/clients/{id}/visit-stats?range=month` for the client from step 2; confirm `200`, `VisitCount: 0`. Repeat with `range=3month`, `6month`, `year`; confirm all `200`. Call with `range=week`; confirm `400`. Call with `range` omitted entirely; confirm `400`.
8. **Regression** — re-run the `tasks` (Story 02) and `visits` (list/detail) walkthroughs end to end to confirm nothing broke; confirm `ApiCallLogs` rows are written for the new `/api/clients/*` calls.

---

## Migration / Rollback

One additive EF Core migration (`AddClientsAndLocations`, task 4) — see task 4 and Edge Cases for why it is larger than its name implies: it adds `Clients`, `Locations`, a new `Employees.AssignedLocationId` column + FK, and the two `Visits` FKs that `20260901145826_AddVisits` deliberately left for this story. No other existing table or column changes.

**Half-applied migration.** If `dotnet ef database update` fails partway, re-running it is safe — EF Core migrations are idempotent per-migration, matching the `tasks`/`visits` stories' documented behavior. If it fails specifically on the `Visits` FK step because pre-existing `Visit` rows already carry `ClientId`/`LocationId` values with no matching `Client`/`Location` row — this should not occur in any environment that only used this API (`POST /api/visits` doesn't exist yet), but would need a manual data fix (delete or correct the offending `Visit` rows) before retrying if it somehow does.

**Rollback.** `dotnet ef database update 20260901145826_AddVisits --project EmpoloyeeManagment` reverts to the prior migration (dropping `Clients`/`Locations`, the `Employees.AssignedLocationId` column/FK, and the two `Visits` FKs). Then delete this story's migration files and revert the code changes (`Models/Client.cs`, `Models/Location.cs`, `Models/Employee.cs`, `Data/AppDbContext.cs`, `Program.cs`, `Dtos/Clients/ClientDtos.cs`, `Dtos/Users/UserDtos.cs`, `Endpoints/UserEndpoints.cs`, `Endpoints/ClientEndpoints.cs`).

---

## Verification Steps

1. **Backend builds:** `dotnet build` from the repo root (or `EmpoloyeeManagment/`).
2. **Database check:** `dotnet ef database update --project EmpoloyeeManagment` — confirm only the new migration from task 4 applies, with the contents described in task 4.
3. **Run:** `dotnet run --project EmpoloyeeManagment`, open `/swagger`, and execute the full Test Plan above (steps 1–8).
4. **Regression:** confirm `ApiCallLogs` rows are still being written for the new `/api/clients/*` calls (sanity check that `Middleware/ApiLoggingMiddleware.cs`, unmodified by this story, still wraps the new endpoints correctly).

---

## Done Criteria

- [ ] `Client`/`Location` models added; `Employee.AssignedLocationId` (nullable FK → `Location`) added.
- [ ] `Clients`/`Locations` DbSets and EF configuration added to `AppDbContext`; the pending `Visit.ClientId`/`Visit.LocationId` FK TODO resolved; one migration applied covering all four affected tables (see task 4).
- [ ] `ClientManager` authorization policy (Admin/Manager) registered alongside the reused `TaskAssigner` policy.
- [ ] `GET /api/clients`, `GET /api/clients/{id}` implemented with Admin/Manager-see-all, Supervisor-scoped-to-assigned-location visibility.
- [ ] `POST /api/clients` (Admin/Manager only) creates a `Client` + nested `Location`s transactionally.
- [ ] `GET /api/clients/{id}/visit-stats?range=month|3month|6month|year` implemented with the same visibility rule.
- [ ] `POST /api/users` accepts and validates an optional `AssignedLocationId`; `GET /api/users` surfaces it via `EmployeeSummaryDto`.
- [ ] `README.md` (Features, Project structure, API table, Roadmap) and `CLAUDE.md` (Architecture, Data model, Project layout, Known v1 gaps) updated, including the folded-in pre-existing `Visit`-related doc gaps noted in task 10.
- [ ] Full manual Test Plan (steps 1–8) passes against a local run.
