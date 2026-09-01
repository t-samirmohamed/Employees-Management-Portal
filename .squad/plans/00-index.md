# Plans index

One row per feature folder under `.squad/plans/`. `NN` continues as a global execution sequence across all features when `naming.globalSequence` is `true` in `config.yaml`.

The baseline four rows link to a `00-current-state.md` snapshot spanning both repos (this API and its `employee-management-web` Next.js client, sibling repo). The seven rows below them came out of the `Employee Managment System.pdf` intake batch — **backend (`EmpoloyeeManagment`) implementation is now complete for all seven**; each links to its `00-overview.md` plan entry point instead of a current-state doc. The frontend companion work is still untouched (`employee-management-web/src/features/` only has `auth`, `employees`, `statistics`) — tracked in that repo's own `.squad/plans/00-index.md`, not duplicated here.

| Feature | Overview | NN range |
|---------|----------|----------|
| [auth](auth/00-current-state.md) | API: signup/login/logout, JWT + security-stamp revocation. Web: login/signup pages, localStorage session, auto-logout on 401 | 00 (baseline) |
| [employees](employees/00-current-state.md) | API: CRUD-lite (list/get/create/activate/deactivate) + statistics. Web: dashboard wired 1:1 to those endpoints | 00 (baseline) |
| [users](users/00-current-state.md) | API: Identity `ApplicationUser`, no standalone API yet. Web: no standalone feature either | 00 (baseline) |
| [logs](logs/00-current-state.md) | API: write-only API call logging middleware, no read endpoint. Web: nothing to show yet | 00 (baseline) |
| [roles](roles/00-overview.md) | API: 4 fixed Identity roles (Admin/Manager/Supervisor/Employee), JWT role claim, Admin-overrides-all policy, `GET /api/users`, admin-only employee activate/deactivate. Web: not started. | 01 |
| [tasks](tasks/00-overview.md) | API: assign/reassign, accept/reject, cancel/complete, comments shipped (`/api/tasks`). **Attachments (story 03) planned but not implemented** — no `Attachment` code in the project yet. Web: not started. | 02–03 |
| [visits](visits/00-overview.md) | API: `GET`/`POST /api/visits`, status read live from the linked task, FK-constrained client/location, accepted-visit concurrency check shared with tasks. Web: not started. | 04 |
| [clients](clients/00-overview.md) | API: `GET`/`POST /api/clients` + locations, per-client visit-count stats, Supervisor scoped to their `AssignedLocationId`. Web: not started. | 05 |
| [dashboards](dashboards/00-overview.md) | API: employee monthly rollup, status stats, most-visited-clients, supervisor/team stats — 4 read endpoints under `/api/statistics`. Web: not started. | 06 |
| [leaves](leaves/00-overview.md) | API: submit/list/detail/edit/delete + accept/reject/request-delay, rank-based approver routing (`Employee < Supervisor < Manager < Admin`). Web: not started. | 07 |
| [notifications](notifications/00-overview.md) | API: `GET /api/notifications` + mark-read shipped. **Submission trigger not wired** — `INotificationService.NotifyLeaveRequestSubmittedAsync` exists but `LeaveEndpoints` never calls it. Web: not started. | 07 |

Two known gaps inside an otherwise-shipped batch (see `CLAUDE.md` "Known v1 gaps" for the full account): `tasks` story 03 (attachments) wasn't implemented, and `notifications` never got wired to fire on leave submission. Everything else above is shipped as planned.
