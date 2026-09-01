# Plans index

One row per feature folder under `.squad/plans/`. `NN` continues as a global execution sequence across all features when `naming.globalSequence` is `true` in `config.yaml`.

Each `00-current-state.md` now spans both repos: this API (`EmpoloyeeManagment`) and its Next.js client (`employee-management-web`, sibling repo, separate git history).

| Feature | Overview | NN range |
|---------|----------|----------|
| [auth](auth/00-current-state.md) | API: signup/login/logout, JWT + security-stamp revocation. Web: login/signup pages, localStorage session, auto-logout on 401 | 00 (baseline) |
| [employees](employees/00-current-state.md) | API: CRUD-lite (list/get/create/activate/deactivate) + statistics. Web: dashboard wired 1:1 to those endpoints | 00 (baseline) |
| [users](users/00-current-state.md) | API: Identity `ApplicationUser`, no standalone API yet. Web: no standalone feature either | 00 (baseline) |
| [logs](logs/00-current-state.md) | API: write-only API call logging middleware, no read endpoint. Web: nothing to show yet | 00 (baseline) |

## Planned — not yet implemented

Broken out from `Employee Managment System.pdf` (client spec, full system). Each row has a ready intake; `plans/<slug>/00-overview.md` stays an empty stub until `squad new-plan` is run against it. Suggested build order follows the "Depends on" column.

| Feature | Intake | Depends on |
|---------|--------|------------|
| [roles](roles/00-overview.md) | [Role model (Admin/Manager/Supervisor/Employee) + admin user management](../stories/roles/role-based-access-control-admin-user-management/intake.md) | — (foundational, build first) |
| [clients](clients/00-overview.md) | [Clients + locations, visit stats](../stories/clients/clients-locations-management/intake.md) | roles |
| [tasks](tasks/00-overview.md) | [Task assignment, accept/reject, comments, attachments](../stories/tasks/task-management-assignment-workflow/intake.md) | roles |
| [visits](visits/00-overview.md) | [Visit tracking, status inherited from task, concurrency rule](../stories/visits/visit-tracking-status/intake.md) | clients, tasks |
| [leaves](leaves/00-overview.md) | [Leave request submit/edit/delete + role-scoped approval](../stories/leaves/leave-requests-approval-workflow/intake.md) | roles |
| [notifications](notifications/00-overview.md) | [Notify approver on leave-request submission](../stories/notifications/leave-request-notifications/intake.md) | leaves |
| [dashboards](dashboards/00-overview.md) | [Monthly activity rollups, team/client stats](../stories/dashboards/activity-dashboards-stats/intake.md) | clients, tasks, visits, leaves |
