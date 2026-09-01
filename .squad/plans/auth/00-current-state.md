# Auth — current state

_Snapshot of what's implemented, not a story to execute. Read this before planning new auth work._

## Backend (`EmpoloyeeManagment`)

### Implemented
- `POST /api/auth/signup` — transactional: creates the `ApplicationUser` (Identity) and a linked `Employee` row (`Status = Inactive`) in one DB transaction (`Endpoints/AuthEndpoints.cs`).
- `POST /api/auth/login` — issues a JWT carrying a custom `securityStamp` claim (`Services/TokenService.cs`).
- `POST /api/auth/logout` (authorized) — rotates the user's Identity security stamp via `UserManager.UpdateSecurityStampAsync`, immediately invalidating every outstanding JWT for that user.
- Real server-side revocation: `JwtBearerEvents.OnTokenValidated` (`Program.cs`) re-checks the live security stamp on every authenticated request, not just token expiry.
- Swagger "Authorize" button wired via `OpenApi/BearerSecuritySchemeTransformer.cs`.

### Not implemented
- Per-device/session revocation — logout kills *all* sessions for the user at once.
- Password reset, email confirmation, 2FA.
- Role-based authorization (no ASP.NET Identity roles in use anywhere).

## Frontend (`employee-management-web`)

### Implemented
- `/login` and `/signup` pages under the `(auth)` route group (`src/features/auth/`).
- `use-login`, `use-signup`, `use-logout` hooks (`src/features/auth/hooks/`) calling the three backend endpoints above.
- Token + expiry kept in `localStorage` (`lib/token-storage.ts`), self-expiring past `expiresAtUtc` — no server sessions/cookies.
- `AuthProvider` hydrates the session from storage on mount; `AuthGuard` wraps the `(dashboard)` layout and redirects unauthenticated users client-side.
- Global `auth:session-expired` window event, dispatched by the API client on a 401, forces logout + redirect to `/login`.
- Logout is local-first and unconditional — the server `/api/auth/logout` call is fire-and-forget and never blocks signing out.
- Signup field errors are mapped from ASP.NET Identity error *codes* (e.g. `PasswordTooShort`) via `lib/map-server-errors.ts`.

### Not implemented
- Password reset, email confirmation, 2FA (no backend support to call yet).
- Any per-device session UI.

## Key files
- Backend: `Endpoints/AuthEndpoints.cs`, `Services/TokenService.cs`, `Dtos/Auth/`, `Program.cs` (JWT bearer + security stamp wiring)
- Frontend: `src/features/auth/` (`lib/token-storage.ts`, `lib/auth-context.tsx`, `components/auth-guard.tsx`, `hooks/`)
