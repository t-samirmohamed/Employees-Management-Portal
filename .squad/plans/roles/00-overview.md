# roles — plan overview

Entry point for the **roles** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Depends on |
|----|------|-------|------------|
| 01 | [01-story-role-model-and-admin-user-management.md](01-story-role-model-and-admin-user-management.md) | Role Model & Admin User Management | — |

## Dependency notes

Story 01 is foundational — every sibling feature ([clients](../clients/00-overview.md), [tasks](../tasks/00-overview.md), [visits](../visits/00-overview.md), [leaves](../leaves/00-overview.md), [notifications](../notifications/00-overview.md), [dashboards](../dashboards/00-overview.md)) assumes the `Admin`/`Manager`/`Supervisor`/`Employee` roles, the JWT role claim, and the `AdminOnly` authorization policy established here already exist — see `.squad/plans/00-index.md`'s "Depends on" column.

No further story is currently planned in `roles/`. Per the intake, per-role capability rules for other domains (who can approve a leave, who can assign a task, etc.) belong in those sibling feature folders, not here.
