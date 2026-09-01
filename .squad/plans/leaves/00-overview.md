# leaves — plan overview

Entry point for the **leaves** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Depends on |
|----|------|-------|------------|
| 07 | [07-story-leave-requests-and-approval-workflow.md](07-story-leave-requests-and-approval-workflow.md) | Leave Requests & Approval Workflow | [roles](../roles/00-overview.md) Story 01 (implemented) |

## Dependency notes

Story 07 depends only on `roles` Story 01 — the `Role` enum, the JWT role claim, `AdminAuthorizationHandler`, and the `TaskAssigner` policy it reuses for the accept/reject/request-delay routes (`Program.cs` lines 76–83) — already shipped, so this story can be built and run today. It does not touch `tasks`/`visits`/`clients`/`dashboards` code at all; those features are cited in the story only as structural precedent for its row-level authorization patterns (scoped-list/detail, ownership checks, role-membership resolution).

**`notifications` depends on this story.** [`../notifications/00-overview.md`](../notifications/00-overview.md) is still the empty stub — its own intake explicitly waits on this story's `LeaveRequest` shape and approver-routing rules to know who to notify on submission. Story 07 deliberately adds no event/hook mechanism for that; per this codebase's established convention, `notifications` will modify `LeaveEndpoints.CreateLeaveRequestAsync` in place once it's planned, the same way `visits` modified `TaskEndpoints` in place rather than `tasks` pre-building a hook.

Integrating leave data into the already-shipped `dashboards` feature's per-employee monthly rollup (`StatisticsEndpoints.GetEmployeeMonthlyActivityAsync`) is a known, explicitly out-of-scope follow-up — see [`../dashboards/00-overview.md`](../dashboards/00-overview.md) line 15, which already anticipated this gap before `leaves` was planned.
