# Story 01 — Role Model & Admin User Management

## Prerequisites

None. This is the first story in the `roles` feature and the first planned story across the whole workspace — everything it depends on (JWT bearer auth, `AppDbContext : IdentityDbContext<ApplicationUser>`, the `Employee` model) is already implemented and documented in [auth/00-current-state.md](../auth/00-current-state.md), [employees/00-current-state.md](../employees/00-current-state.md), and [users/00-current-state.md](../users/00-current-state.md).

## Story Goal

Introduce the four fixed ASP.NET Identity roles (`Admin`, `Manager`, `Supervisor`, `Employee`), thread the caller's role into the JWT, enforce it via one reusable "Admin overrides everything" authorization rule, lock `PATCH /api/employees/{id}/activate` and `/deactivate` down to Admins, and give Admins a way to see and create system users. Concretely:

1. Every `ApplicationUser` has exactly one of the four roles at all times (new signups, admin-created users, and existing pre-story users via a one-time backfill).
2. The JWT issued at signup/login carries the user's role as a standard `ClaimTypes.Role` claim.
3. An `Admin` passes every role-based authorization check in the app, via a single reusable rule — not per-endpoint special-casing.
4. `PATCH /api/employees/{id}/activate` and `PATCH /api/employees/{id}/deactivate` become Admin-only. Every other `/api/employees` route (list, detail, create) is **unchanged** — still any authenticated user, per the "System permission on all actions and lists/details view" line in the source spec.
5. `GET /api/users` (Admin-only) lists every system user with their role and linked employee summary.
6. `POST /api/users` (Admin-only, new) lets an Admin create a user account with any of the four roles — this is the concrete mechanism behind the spec's "Admins... Can add any other roles," since no such admin-initiated-creation endpoint exists anywhere in the codebase today (`POST /api/employees` only ever creates an `Employee` row, never an `ApplicationUser`).

**Not in scope** (per the intake's own "Out of scope" section — do not build these here): a "change an existing user's role" endpoint, per-employee/per-resource permission overrides beyond the 4 roles, the actual clients/tasks/visits/leaves/notifications/dashboards endpoints, attendance-status-from-visit/task calculation, and any frontend work (a companion story exists in the separate `employee-management-web` repo).

---

## Context — Read These Files First

1. [`EmpoloyeeManagment/Program.cs`](../../../EmpoloyeeManagment/Program.cs) — the composition root you'll edit in four places: lines 22–28 (`AddIdentityCore<ApplicationUser>()` chain — no `.AddRoles<TRole>()` today), line 73 (`builder.Services.AddAuthorization();` — no policies today), line 83 (`builder.Services.AddScoped<ITokenService, TokenService>();` — register the new authorization handler near here), line 95 (`var app = builder.Build();` — insertion point for role seeding), and lines 114–116 (`app.MapAuthEndpoints(); app.MapEmployeeEndpoints(); app.MapStatisticsEndpoints();` — add `app.MapUserEndpoints();`). Also read lines 46–70 (`JwtBearerEvents.OnTokenValidated`) to see the existing security-stamp revocation pattern this story's role claim rides alongside, without modifying.
2. [`EmpoloyeeManagment/Endpoints/AuthEndpoints.cs`](../../../EmpoloyeeManagment/Endpoints/AuthEndpoints.cs) — `SignupAsync` (lines 24–67): the transaction pattern (`db.Database.BeginTransactionAsync()` at line 33, `UserManager.CreateAsync` at line 41, `db.Employees.Add(...)` at lines 50–60, commit at line 62) you'll extend with a role assignment, and mirror (not call into) for the new `POST /api/users`. `LoginAsync` (lines 69–83) needs the same one-line token-call change.
3. [`EmpoloyeeManagment/Endpoints/EmployeeEndpoints.cs`](../../../EmpoloyeeManagment/Endpoints/EmployeeEndpoints.cs) — lines 11–22 (group declaration, `RequireAuthorization()` with no policy at the group level) and lines 69–73 (`ActivateEmployeeAsync`/`DeactivateEmployeeAsync` route registrations you'll add `.RequireAuthorization("AdminOnly")` to).
4. [`EmpoloyeeManagment/Services/TokenService.cs`](../../../EmpoloyeeManagment/Services/TokenService.cs) and [`ITokenService.cs`](../../../EmpoloyeeManagment/Services/ITokenService.cs) — the whole `CreateToken` method (`TokenService.cs` lines 11–40): the fixed `claims` array (lines 19–26) that needs to become a growable list, and the `(ApplicationUser user, string securityStamp)` signature (both files) that needs a third `roles` parameter.
5. [`EmpoloyeeManagment/Models/Enums.cs`](../../../EmpoloyeeManagment/Models/Enums.cs) — the whole file (21 lines): `Gender`, `EmployeeStatus`, `AttendanceStatus` — the exact style (`public enum X { ... }`) the new `Role` enum must match, appended at the end.
6. [`EmpoloyeeManagment/Models/Employee.cs`](../../../EmpoloyeeManagment/Models/Employee.cs) and [`Data/AppDbContext.cs`](../../../EmpoloyeeManagment/Data/AppDbContext.cs) — `Employee.UserId` (`Employee.cs` line 6) is a **nullable** `string?` with no `.IsRequired()` in `AppDbContext.cs` (compare line 25, `UserId`, against lines 26–28, which do call `.IsRequired()`). This means `Employee` rows created via `POST /api/employees` (`EmployeeEndpoints.CreateEmployeeAsync`) have **no linked `ApplicationUser` and therefore no role** — see Edge Cases below. `AppDbContext.cs` line 7 (`IdentityDbContext<ApplicationUser>`) is also why the role schema already exists — see next item.
7. [`EmpoloyeeManagment/Migrations/AppDbContextModelSnapshot.cs`](../../../EmpoloyeeManagment/Migrations/AppDbContextModelSnapshot.cs) lines 189–301, and [`Migrations/20260713190916_InitialCreate.cs`](../../../EmpoloyeeManagment/Migrations/20260713190916_InitialCreate.cs) (`CreateTable` calls for `AspNetRoles` at line 35, `AspNetRoleClaims` at line 93, `AspNetUserRoles` at line 155) — **proof the role tables already physically exist in the database**, created by the very first migration, because `IdentityDbContext<ApplicationUser>` (single type parameter) always includes the full `IdentityRole`/`IdentityUserRole<string>`/`IdentityRoleClaim<string>` schema regardless of whether `.AddRoles<TRole>()` was ever called in DI. **This story needs zero new EF Core migrations** — confirm this before assuming otherwise.
8. [`EmpoloyeeManagment/docs/Project_Startup_Plan.md`](../../../EmpoloyeeManagment/docs/Project_Startup_Plan.md) line 26 — explicitly documents the original decision to skip roles ("No roles for v1... add later if role-based authorization becomes a real need") — this story is that "later." Also read lines 104–107 (`## Verification`) — the manual Swagger walkthrough pattern this story's Test Plan extends.
9. [`EmpoloyeeManagment/Dtos/Auth/AuthDtos.cs`](../../../EmpoloyeeManagment/Dtos/Auth/AuthDtos.cs) (9 lines) and [`Dtos/Employees/EmployeeDtos.cs`](../../../EmpoloyeeManagment/Dtos/Employees/EmployeeDtos.cs) (19 lines) — the one-record-per-shape, grouped-by-feature DTO convention the new `Dtos/Users/UserDtos.cs` must match.
10. [`EmpoloyeeManagment/obj/Debug/net10.0/EmpoloyeeManagment.GlobalUsings.g.cs`](../../../EmpoloyeeManagment/obj/Debug/net10.0/EmpoloyeeManagment.GlobalUsings.g.cs) — confirms `Microsoft.AspNetCore.Authorization` is **not** a global using (only `Builder`, `Hosting`, `Http`, `Routing`, `Configuration`, `DependencyInjection`, `Hosting`, `Logging` plus base `System.*` are). `Program.cs` and the new authorization handler file both need an explicit `using Microsoft.AspNetCore.Authorization;`.
11. Grep for `RequireAuthorization` across `EmpoloyeeManagment/Endpoints/` — confirms the only existing precedent for a **per-route** override on top of a group is `AuthEndpoints.cs` line 19 (`group.MapPost("/logout", LogoutAsync).RequireAuthorization();`); this story adds the first **named-policy** per-route override, on `EmployeeEndpoints.cs`.
12. [`README.md`](../../../README.md) lines 57–67 (API overview table) and line 71 (Roadmap gaps line, "...role-based authorization...") — both go stale once this ships; update as part of Done Criteria, along with the matching "Known v1 gaps" line in `CLAUDE.md`.

---

## Product rules (from story)

| Area | Current behavior | New behavior |
|---|---|---|
| Identity roles | None — `AddIdentityCore` has no `.AddRoles()`, `RoleManager<IdentityRole>` isn't in DI | 4 roles seeded on startup: `Admin`, `Manager`, `Supervisor`, `Employee` |
| `POST /api/auth/signup` | Creates `ApplicationUser` + `Employee` (`Inactive`) | Same, plus auto-assigns the `Employee` role |
| JWT claims | `sub`, `NameIdentifier`, `Email`, `securityStamp`, `Jti` | + one `ClaimTypes.Role` claim per role the user holds |
| `PATCH /api/employees/{id}/activate`, `/deactivate` | Any authenticated user | **Admin only** |
| `GET /api/employees`, `GET /api/employees/{id}`, `POST /api/employees` | Any authenticated user | **Unchanged** — still any authenticated user |
| `GET /api/users` | Doesn't exist | New — **Admin only** — lists all system users with role + linked employee summary |
| `POST /api/users` | Doesn't exist | New — **Admin only** — creates a user with any of the 4 roles |
| Pre-existing users (signed up before this story shipped) | No role | Backfilled to `Employee` on next startup; operator manually promotes one to `Admin` (see Migration / Rollback) |

---

## Backend Tasks

`No frontend changes required in this repository` — the companion UI work is tracked in the separate `employee-management-web` repo per the intake's "Extra notes."

### 1 — Add the `Role` enum

**File: `EmpoloyeeManagment/Models/Enums.cs`**

Append a fourth enum after `AttendanceStatus` (after line 21), matching the existing style exactly:

```csharp
public enum Role
{
    Admin,
    Manager,
    Supervisor,
    Employee
}
```

No `[HasConversion<string>]`-style EF configuration is needed for this enum — unlike `Gender`/`EmployeeStatus`/`AttendanceStatus`, it is never persisted as a column; it only appears in DTOs (serialized as a string automatically by the `JsonStringEnumConverter` already registered in `Program.cs` lines 85–88) and is converted to/from the Identity role-name strings (`nameof(Role.Admin)` ⇄ `IdentityRole.Name`) at the API boundary.

### 2 — Register ASP.NET Identity roles and seed them on startup

**File: `EmpoloyeeManagment/Program.cs`**

Add `.AddRoles<IdentityRole>()` to the existing `AddIdentityCore` chain (lines 22–28), **between** the `AddIdentityCore` options lambda and `.AddEntityFrameworkStores<AppDbContext>()` — order matters: `AddEntityFrameworkStores` inspects whichever role type was already configured via `AddRoles` to wire up `IRoleStore<IdentityRole>`.

```csharp
builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();
```

Immediately after `var app = builder.Build();` (line 95), before the `if (app.Environment.IsDevelopment())` block, seed the four roles and backfill any pre-existing user that has no role yet:

```csharp
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    foreach (var role in Enum.GetValues<Role>())
    {
        var roleName = role.ToString();
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }

    var existingUsers = await userManager.Users.ToListAsync();
    foreach (var user in existingUsers)
    {
        var roles = await userManager.GetRolesAsync(user);
        if (roles.Count == 0)
        {
            await userManager.AddToRoleAsync(user, nameof(Role.Employee));
        }
    }
}
```

This is idempotent (both loops check-before-write), so it is safe to run on every startup, not just the first one. No new EF Core migration — see Context item 7.

### 3 — Admin-bypass authorization policy

**Create file: `EmpoloyeeManagment/Authorization/AdminAuthorizationHandler.cs`**

New folder, mirroring the existing single-purpose-infra-folder convention already used by `OpenApi/BearerSecuritySchemeTransformer.cs`.

```csharp
using Microsoft.AspNetCore.Authorization;
using EmpoloyeeManagment.Models;

namespace EmpoloyeeManagment.Authorization;

public class AdminAuthorizationHandler : IAuthorizationHandler
{
    public Task HandleAsync(AuthorizationHandlerContext context)
    {
        if (context.User.IsInRole(nameof(Role.Admin)))
        {
            foreach (var requirement in context.PendingRequirements.ToList())
            {
                context.Succeed(requirement);
            }
        }

        return Task.CompletedTask;
    }
}
```

The `.ToList()` is required, not stylistic: `context.Succeed(...)` mutates `PendingRequirements`, so iterating the live collection directly risks a collection-modified-during-enumeration exception.

This one handler is what makes "Admin role overrides any other role" true app-wide without touching individual endpoints: it runs for **every** authorization evaluation, and for an Admin it succeeds whatever the pending requirement happens to be — `RolesAuthorizationRequirement("Admin")`, or a future `RolesAuthorizationRequirement("Manager")` from a sibling story — with no per-endpoint special-casing.

**File: `EmpoloyeeManagment/Program.cs`**

Add the two usings (top of file, alongside the existing ones at lines 1–12):

```csharp
using Microsoft.AspNetCore.Authorization;
using EmpoloyeeManagment.Authorization;
```

Replace the no-op `builder.Services.AddAuthorization();` (line 73) with a policy registration, and register the handler (near line 83's `AddScoped<ITokenService, TokenService>();`):

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(nameof(Role.Admin)));
});
builder.Services.AddSingleton<IAuthorizationHandler, AdminAuthorizationHandler>();
```

### 4 — Put the caller's role into the JWT

**File: `EmpoloyeeManagment/Services/ITokenService.cs`**

```csharp
(string Token, DateTime ExpiresAtUtc) CreateToken(ApplicationUser user, string securityStamp, IList<string> roles);
```

**File: `EmpoloyeeManagment/Services/TokenService.cs`**

Change the `claims` array (lines 19–26) to a `List<Claim>` so role claims can be appended, and add the `roles` parameter (line 11):

```csharp
public (string Token, DateTime ExpiresAtUtc) CreateToken(ApplicationUser user, string securityStamp, IList<string> roles)
{
    var jwtSection = configuration.GetSection("Jwt");
    var key = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
    var issuer = jwtSection["Issuer"];
    var audience = jwtSection["Audience"];
    var expiryMinutes = jwtSection.GetValue("ExpiryMinutes", 60);

    var claims = new List<Claim>
    {
        new(JwtRegisteredClaimNames.Sub, user.Id),
        new(ClaimTypes.NameIdentifier, user.Id),
        new(ClaimTypes.Email, user.Email ?? string.Empty),
        new("securityStamp", securityStamp),
        new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
    };
    claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

    var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
    var expires = DateTime.UtcNow.AddMinutes(expiryMinutes);

    var token = new JwtSecurityToken(
        issuer: issuer,
        audience: audience,
        claims: claims,
        expires: expires,
        signingCredentials: credentials);

    return (new JwtSecurityTokenHandler().WriteToken(token), expires);
}
```

`ClaimTypes.Role` is deliberate, not arbitrary: it is the default `RoleClaimType` both `JwtBearerOptions.TokenValidationParameters` and `ClaimsPrincipal.IsInRole` use, so `context.User.IsInRole(...)` (task 3) and `.RequireRole(...)` (the `AdminOnly` policy) work with **no extra configuration** in `Program.cs`'s `TokenValidationParameters` (lines 34–44).

**File: `EmpoloyeeManagment/Endpoints/AuthEndpoints.cs`**

In `SignupAsync`, after the `if (!result.Succeeded) { ... }` block (line 48) and before `db.Employees.Add(...)` (line 50), assign the default role — inside the same transaction, since `UserManager` writes go through the same scoped `AppDbContext` per the existing comment at lines 30–32:

```csharp
await userManager.AddToRoleAsync(user, nameof(Role.Employee));
```

Not checked for `.Succeeded` — deliberately: the `Employee` role is guaranteed to exist by the startup seeding in task 2, which runs before the app accepts any request, so this call cannot fail under normal operation. This mirrors the file's existing trust of `await db.SaveChangesAsync()` at line 61, which also isn't wrapped in error handling.

Then update both token-issuance call sites to fetch and pass roles — `SignupAsync` (around line 64):

```csharp
var securityStamp = await userManager.GetSecurityStampAsync(user);
var roles = await userManager.GetRolesAsync(user);
var (token, expiresAtUtc) = tokenService.CreateToken(user, securityStamp, roles);
```

and `LoginAsync` (around line 80), the identical two-line change:

```csharp
var securityStamp = await userManager.GetSecurityStampAsync(user);
var roles = await userManager.GetRolesAsync(user);
var (token, expiresAtUtc) = tokenService.CreateToken(user, securityStamp, roles);
```

Add `using EmpoloyeeManagment.Models;` if not already present (it is — line 4 already imports it for `ApplicationUser`, and `Role` lives in the same `EmpoloyeeManagment.Models` namespace per `Models/Enums.cs` line 1, so no new using is actually needed here — only `nameof(Role.Employee)` needs to resolve, which it already will).

### 5 — Restrict employee activate/deactivate to Admin

**File: `EmpoloyeeManagment/Endpoints/EmployeeEndpoints.cs`**

Change lines 18–19 from:

```csharp
group.MapPatch("/{id:int}/activate", ActivateEmployeeAsync);
group.MapPatch("/{id:int}/deactivate", DeactivateEmployeeAsync);
```

to:

```csharp
group.MapPatch("/{id:int}/activate", ActivateEmployeeAsync).RequireAuthorization("AdminOnly");
group.MapPatch("/{id:int}/deactivate", DeactivateEmployeeAsync).RequireAuthorization("AdminOnly");
```

The group-level `.RequireAuthorization()` (line 13, no policy name) still applies to every route in the group, including these two — ASP.NET Core combines multiple `RequireAuthorization` calls with AND semantics, so these two routes end up requiring **both** "authenticated" (from the group) **and** "AdminOnly" (from the route), while list/detail/create keep only the group's bare "authenticated" requirement. No change needed to `GetEmployeesAsync`, `GetEmployeeByIdAsync`, or `CreateEmployeeAsync`.

### 6 — Admin user-management endpoints

**Create file: `EmpoloyeeManagment/Dtos/Users/UserDtos.cs`**

Matches the existing per-feature DTO grouping (`Dtos/Auth/`, `Dtos/Employees/`); `CreateUserRequest` mirrors `SignupRequest` (`Dtos/Auth/AuthDtos.cs` line 5) plus `Role`:

```csharp
using EmpoloyeeManagment.Models;

namespace EmpoloyeeManagment.Dtos.Users;

public record CreateUserRequest(string Email, string Password, string FirstName, string LastName, Gender Gender, Role Role);

public record UserResponse(string Id, string Email, Role Role);

public record EmployeeSummaryDto(int Id, string FirstName, string LastName, EmployeeStatus Status);

public record UserListItemDto(string Id, string Email, Role Role, EmployeeSummaryDto? Employee);
```

**Create file: `EmpoloyeeManagment/Endpoints/UserEndpoints.cs`**

Follows the exact `Map*Endpoints(this IEndpointRouteBuilder)` shape used by `AuthEndpoints`/`EmployeeEndpoints`/`StatisticsEndpoints`. Both routes are Admin-only at the group level (`GET /api/users` per the acceptance criteria directly; `POST /api/users` because "admin-initiated creation" is definitionally Admin-only):

```csharp
using EmpoloyeeManagment.Data;
using EmpoloyeeManagment.Dtos.Users;
using EmpoloyeeManagment.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EmpoloyeeManagment.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").WithTags("Users").RequireAuthorization("AdminOnly");

        group.MapGet("/", GetUsersAsync);
        group.MapPost("/", CreateUserAsync);

        return app;
    }

    private static async Task<Ok<List<UserListItemDto>>> GetUsersAsync(AppDbContext db)
    {
        var raw = await db.Users
            .AsNoTracking()
            .Select(u => new
            {
                u.Id,
                Email = u.Email ?? string.Empty,
                RoleName = db.Roles
                    .Where(r => db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == r.Id))
                    .Select(r => r.Name)
                    .FirstOrDefault(),
                Employee = db.Employees
                    .Where(e => e.UserId == u.Id)
                    .Select(e => new EmployeeSummaryDto(e.Id, e.FirstName, e.LastName, e.Status))
                    .FirstOrDefault()
            })
            .ToListAsync();

        var users = raw
            .Select(u => new UserListItemDto(u.Id, u.Email, Enum.Parse<Role>(u.RoleName!), u.Employee))
            .ToList();

        return TypedResults.Ok(users);
    }

    private static async Task<Results<Created<UserResponse>, ValidationProblem>> CreateUserAsync(
        CreateUserRequest request,
        UserManager<ApplicationUser> userManager,
        AppDbContext db)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = result.Errors
                .GroupBy(e => e.Code)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
            return TypedResults.ValidationProblem(errors);
        }

        await userManager.AddToRoleAsync(user, request.Role.ToString());

        db.Employees.Add(new Employee
        {
            UserId = user.Id,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Gender = request.Gender,
            Status = EmployeeStatus.Inactive,
            AttendanceStatus = AttendanceStatus.OutOfOffice,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return TypedResults.Created($"/api/users/{user.Id}", new UserResponse(user.Id, user.Email ?? string.Empty, request.Role));
    }
}
```

`Status = EmployeeStatus.Inactive` / `AttendanceStatus.OutOfOffice` deliberately mirrors `SignupAsync` (`AuthEndpoints.cs` lines 57–58), not `CreateEmployeeAsync`'s `Active`/`InOffice` default (`EmployeeEndpoints.cs` line 58–59): an admin-created user follows the same "created → Inactive → explicitly activated" lifecycle as a self-service signup, since both flows create a linked `ApplicationUser` + `Employee` pair together. `CreateEmployeeAsync` is a different, employee-only flow with no linked user and is intentionally left alone (task 5).

`Enum.Parse<Role>(u.RoleName!)` runs **after** `ToListAsync()`, on the already-materialized `raw` list — not inside the `db.Users.Select(...)` query. `Enum.Parse` cannot be translated into SQL by EF Core; calling it inside the query expression throws `InvalidOperationException` at request time. The query itself only ever projects the plain string `RoleName`, which EF Core can translate via a correlated subquery.

The `GET /api/users/{id}` route implied by the `Created` Location header is **not** part of this story's scope (only the list endpoint was requested) — the header is still valid, just not yet resolvable to a handler.

**File: `EmpoloyeeManagment/Program.cs`**

Add the call alongside the other three (lines 114–116):

```csharp
app.MapUserEndpoints();
```

---

## Edge Cases & Failure Modes

- **Pre-existing users with no role.** Any `ApplicationUser` created before this story ships has zero role rows. The startup backfill (task 2) assigns all of them `Employee` — the safe, lowest-privilege default. This means **no user can call an Admin-only endpoint immediately after this story ships** until one is manually promoted — see Migration / Rollback for the exact SQL. This is expected, not a bug: the acceptance criteria doesn't ask for a "first user becomes Admin" bootstrap, and inventing one would be unrequested scope.
- **`Employee` rows with no linked `ApplicationUser`.** `Employee.UserId` is nullable (`Employee.cs` line 6, `AppDbContext.cs` line 25 — no `.IsRequired()`), and `CreateEmployeeAsync` (`EmployeeEndpoints.cs` lines 50–67) never sets it. Such an `Employee` has no role and will never appear in `GET /api/users` (which enumerates `db.Users`, not `db.Employees`). This is expected and directly informs the intake's "Decide the Supervisor/Manager data shape" question: a `Manager`/`Supervisor` is only meaningful as a role on an `ApplicationUser` with a linked `Employee` — an `Employee` created via `POST /api/employees` alone is not a system user of any role until (a future story) links it to one.
- **Stale role claims mid-session.** No "change an existing user's role" endpoint exists in this story. Even so, if a role were ever changed directly in the DB, an already-issued JWT keeps its old `ClaimTypes.Role` claim until natural expiry (`Jwt:ExpiryMinutes` = 60, `appsettings.Development.json` line 15) or the user logs out and back in — logout (`AuthEndpoints.LogoutAsync`) rotates the security stamp but does not itself force a role refresh. This is the same latency trade-off the existing security-stamp revocation design already accepts (see `Program.cs` lines 46–70 and CLAUDE.md's Auth section) — not a new gap introduced here.
- **`AdminAuthorizationHandler` iterating `PendingRequirements`.** Must snapshot via `.ToList()` before calling `context.Succeed(...)` in the loop (task 3) — `Succeed()` mutates the live `PendingRequirements` collection, so iterating it directly risks a collection-modified-during-enumeration failure.
- **`Enum.Parse<Role>` inside an EF Core query.** Covered in task 6 — must happen after materialization (`ToListAsync()`), never inside `db.Users.Select(...)`.
- **Duplicate email on `POST /api/users`.** Hits the same `UserManager.CreateAsync` uniqueness check (`options.User.RequireUniqueEmail = true`, `Program.cs` line 25) as `SignupAsync` already does — returns a `ValidationProblem` with Identity's standard `DuplicateEmail`/`DuplicateUserName` error codes, same as `AuthEndpoints.cs` lines 42–48. No new handling required.
- **Fresh database, zero users.** On a brand-new DB with migrations applied but no signups yet, the backfill loop in task 2 is a no-op (empty `existingUsers`). The manual Admin-promotion SQL (Migration / Rollback) must be run **after** at least one signup exists, not before.

---

## Test Plan

This repo has no automated test project yet (see `CLAUDE.md` — "There are no automated tests in this repo yet; verification is manual via Swagger"). This story follows and extends the existing manual walkthrough documented in `docs/Project_Startup_Plan.md`'s `## Verification` section (lines 104–107) rather than introducing a new, unestablished testing pattern.

1. **Role seeding** — start the app fresh, then query `SELECT Name FROM AspNetRoles` directly (SSMS or `sqlcmd`) and confirm exactly `Admin`, `Manager`, `Supervisor`, `Employee` exist.
2. **Signup assigns `Employee`** — `POST /api/auth/signup` in Swagger, decode the returned JWT (e.g. jwt.io or any JWT decoder) and confirm a `role` claim with value `Employee` is present alongside the existing `securityStamp` claim.
3. **Non-admin blocked from admin routes** — using that signup's token, call `GET /api/users` and `PATCH /api/employees/{id}/activate` in Swagger; expect `403 Forbidden` on both.
4. **Admin bypass** — after manually promoting a user to `Admin` (Migration / Rollback SQL), log in as that user, call `GET /api/users` and confirm it returns a list including the current user with `Role: "Admin"` and (if linked) an `Employee` summary.
5. **Admin-only activate/deactivate** — as the Admin, call `PATCH /api/employees/{id}/activate` and confirm `200`; as a non-admin (from step 3's token), confirm the same call now returns `403` where it previously returned `200` before this story.
6. **`POST /api/users` end to end** — as the Admin, `POST /api/users` with `Role: "Manager"`, confirm `201 Created` with a `Location` header; then `GET /api/users` and confirm the new user appears with `Role: "Manager"` and `Employee.Status: "Inactive"`.
7. **Regression** — re-run the full pre-existing walkthrough from `docs/Project_Startup_Plan.md` end to end (signup → login → Authorize → create employee → list/detail → activate/deactivate as Admin → statistics → logout → confirm the old JWT gets `401`) to confirm nothing existing broke.

---

## Migration / Rollback

No EF Core schema migration is added or required — the `AspNetRoles`/`AspNetUserRoles`/`AspNetRoleClaims` tables already exist from `20260713190916_InitialCreate.cs` (Context item 7). This story is pure DI wiring, endpoint code, and one-time data seeding.

**Bootstrapping the first Admin.** After deploying this story and confirming at least one `AspNetUsers` row exists (sign up once if needed), run once against the target database:

```sql
DELETE ur FROM AspNetUserRoles ur
JOIN AspNetUsers u ON u.Id = ur.UserId
WHERE u.Email = 'YOUR_ADMIN_EMAIL';

INSERT INTO AspNetUserRoles (UserId, RoleId)
SELECT u.Id, r.Id
FROM AspNetUsers u, AspNetRoles r
WHERE u.Email = 'YOUR_ADMIN_EMAIL' AND r.Name = 'Admin';
```

The `DELETE` first removes the `Employee` role the startup backfill will have already assigned that user, keeping "exactly one role per user" true after the manual promotion.

**Half-applied startup state.** Both seeding loops in task 2 are check-before-write (`RoleExistsAsync`, `GetRolesAsync(...).Count == 0`), so a crash mid-loop is safe — the next restart only creates/assigns whatever is still missing, with no manual cleanup needed.

**Rollback.** Revert the code changes (`Program.cs`, the two endpoint files, `TokenService`/`ITokenService`, the two new files). The seeded role rows and any `AspNetUserRoles` assignments can be left in place (nothing references them once the code is reverted) or removed with `DELETE FROM AspNetUserRoles; DELETE FROM AspNetRoles;` for a fully clean revert.

---

## Verification Steps

1. **Backend builds:** `dotnet build` from the repo root (or `EmpoloyeeManagment/`).
2. **Database check:** `dotnet ef database update --project EmpoloyeeManagment` — expect no pending migration to apply. If EF Core reports a pending model change here, stop and investigate before proceeding — none is expected, since the role schema is already fully present (see Migration / Rollback).
3. **Run:** `dotnet run --project EmpoloyeeManagment`, open `/swagger`, and execute the full Test Plan above (steps 1–7).
4. **Regression:** confirm the existing statistics and API-logging middleware still function unaffected — check `ApiCallLogs` rows exist for the calls just made (`Middleware/ApiLoggingMiddleware.cs` is unmodified by this story, but is a good sanity check that the pipeline ordering in `Program.cs` wasn't disturbed by the new usings/registrations).

---

## Done Criteria

- [ ] `Role` enum (`Admin`, `Manager`, `Supervisor`, `Employee`) added to `Models/Enums.cs`.
- [ ] `RoleManager<IdentityRole>` registered via `.AddRoles<IdentityRole>()`; all four roles seeded on startup, idempotently.
- [ ] Every new signup is assigned exactly the `Employee` role; admin-initiated creation (`POST /api/users`) can assign any of the four.
- [ ] Existing pre-story users are backfilled to `Employee` on next startup.
- [ ] JWTs issued by `POST /api/auth/signup` and `POST /api/auth/login` carry a `ClaimTypes.Role` claim matching the user's role.
- [ ] `AdminAuthorizationHandler` registered as `IAuthorizationHandler`; an `Admin` passes the `AdminOnly` policy and any future role-scoped policy, via this one handler — no per-endpoint special-casing.
- [ ] `PATCH /api/employees/{id}/activate` and `/deactivate` require the `AdminOnly` policy; `GET /api/employees`, `GET /api/employees/{id}`, `POST /api/employees` remain unchanged (any authenticated user).
- [ ] `GET /api/users` (Admin-only) returns every system user with role + linked employee summary.
- [ ] `POST /api/users` (Admin-only) creates a user + linked `Employee` with the specified role.
- [ ] No new EF Core migration was added.
- [ ] `README.md`'s API overview table (lines 57–67) and Roadmap line (line 71), and `CLAUDE.md`'s "Known v1 gaps" line, updated to drop "role-based authorization" from the gaps list and document the new endpoints.
- [ ] Full manual Test Plan (steps 1–7) passes against a local run.
