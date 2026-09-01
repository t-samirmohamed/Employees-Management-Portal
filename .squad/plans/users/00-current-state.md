# Users — current state

_Snapshot of what's implemented, not a story to execute. Read this before planning new user-management work._

## Backend (`EmpoloyeeManagment`)

### Implemented
- `ApplicationUser : IdentityUser` (`Models/ApplicationUser.cs`) — stock ASP.NET Identity user, wired via `AddIdentityCore` (not full `AddIdentity`, so no cookie-auth surface — see [[../auth/00-current-state]]).
- Created only implicitly as part of `POST /api/auth/signup`; one-to-one with an `Employee` row via `Employee.UserId`.
- Security-stamp rotation on the user drives logout-wide token revocation (see [[../auth/00-current-state]]).

### Not implemented
- No standalone Users endpoints — no list/get/update/delete API distinct from the linked employee.
- No roles or per-user permissions.
- No admin flow to manage a user independently of its employee record.

## Frontend (`employee-management-web`)

### Implemented
- Nothing standalone — matches the backend: there's no Users API to build a `users` feature against. A user is only ever touched indirectly, via the `auth` (signup) and `employees` features.

### Not implemented
- Everything: no `users` feature folder or route exists.

## Key files
- Backend: `Models/ApplicationUser.cs`, `Data/AppDbContext.cs` (`IdentityDbContext<ApplicationUser>`), `Endpoints/AuthEndpoints.cs` (only place a user is created)
- Frontend: n/a
