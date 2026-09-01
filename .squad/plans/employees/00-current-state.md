# Employees — current state

_Snapshot of what's implemented, not a story to execute. Read this before planning new employee work._

## Backend (`EmpoloyeeManagment`)

### Implemented
- `/api/employees` group (all `RequireAuthorization()`): `GET /` list, `GET /{id}` detail, `POST /` create, `PATCH /{id}/activate`, `PATCH /{id}/deactivate` (`Endpoints/EmployeeEndpoints.cs`).
- `GET /api/statistics/employees` — aggregate employee stats (`Endpoints/StatisticsEndpoints.cs`).
- `Employee` model: `Gender` (M/F), `EmployeeStatus` (Active/Inactive), `AttendanceStatus` (InOffice/Absent/OnVacation/OutOfOffice) — all serialized as strings.
- One-to-one link to `ApplicationUser` via nullable `Employee.UserId`, set at signup (see [[../users/00-current-state]]).

### Not implemented
- General update (edit) and delete — explicit v1 gap (see README roadmap).
- Role-based authorization on who can create/activate/deactivate.

## Frontend (`employee-management-web`)

### Implemented
- `/employees` dashboard route (`(dashboard)` route group, gated by `AuthGuard`).
- Hooks 1:1 with the backend endpoints (`src/features/employees/hooks/`): `use-employees` (list), `use-employee` (detail), `use-create-employee`, `use-activate-employee`, `use-deactivate-employee`.
- `/statistics` dashboard route backed by its own `src/features/statistics/` folder (separate frontend feature, not tracked under this squad feature).
- Domain enums (`src/shared/types/enums.ts`) intentionally mirror the backend's C# wire values, not display labels.

### Not implemented
- No edit or delete UI/hooks — matches the backend gap above.

## Key files
- Backend: `Endpoints/EmployeeEndpoints.cs`, `Endpoints/StatisticsEndpoints.cs`, `Models/Employee.cs`, `Dtos/Employees/`, `Dtos/Statistics/`
- Frontend: `src/features/employees/` (`hooks/`, `components/`, `schemas/`)
