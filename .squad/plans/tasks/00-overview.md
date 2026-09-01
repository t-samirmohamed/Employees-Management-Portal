# tasks — plan overview

Entry point for the **tasks** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Depends on |
|----|------|-------|------------|
| 02 | [02-story-task-assignment-and-comments.md](02-story-task-assignment-and-comments.md) | Task Assignment Workflow & Comments | [roles](../roles/00-overview.md) |
| 03 | [03-story-task-attachments.md](03-story-task-attachments.md) | Task Attachments | Story 02 |

## Dependency notes

Story 02 is foundational for this feature: it adds the `TaskItem`/`TaskComment` entities, the `TaskItemStatus` enum, the full assignment/accept/reject/cancel/complete lifecycle, role-scoped visibility, and the shared `Services/IEmployeeAttendanceService`. Story 03 only adds file attachments on top of the `Endpoints/TaskEndpoints.cs`/`Data/AppDbContext.cs`/`Dtos/Tasks/TaskDtos.cs` files Story 02 creates — it cannot be implemented first.

Both stories deliberately leave `TaskItem.RelatedVisitId` as an unconstrained nullable column with no FK and no existence validation, and leave `Done` unreachable for any task with `RelatedVisitId` set — the not-yet-planned `visits` feature (see [`../visits/00-overview.md`](../visits/00-overview.md) and its [intake](../../stories/visits/visit-tracking-status/intake.md)) owns adding the `Visits` table, the FK constraint, and syncing visit-linked tasks to `Done`. `visits` must also call Story 02's `Services/IEmployeeAttendanceService.RecalculateAsync` for visit-driven attendance rather than re-implementing the InOffice/OutOfOffice computation — see that service's Edge Cases in `02-story-task-assignment-and-comments.md`.

Per the roles story's precedent ([`../roles/00-overview.md`](../roles/00-overview.md)), Story 02 assumes the `Admin`/`Manager`/`Supervisor`/`Employee` roles, the JWT role claim, and `AdminAuthorizationHandler` already exist — they do, and are already implemented in the current codebase.

Neither story implements real Supervisor/team scoping (no `SupervisorId`/`TeamId` field exists anywhere on `Employee` today) — Story 02 documents this explicitly as Admin/Manager/Supervisor seeing all tasks rather than a scoped subset; see that story's Edge Cases.
