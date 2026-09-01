# dashboards — plan overview

Entry point for the **dashboards** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 06 | [06-story-activity-dashboards-and-stats.md](06-story-activity-dashboards-and-stats.md) | Activity Dashboards & Stats | — | [clients](../clients/00-overview.md) Story 05 (implemented), [tasks](../tasks/00-overview.md) Story 02 (implemented), [visits](../visits/00-overview.md) Story 04 (implemented), [roles](../roles/00-overview.md) Story 01 (implemented) |

## Dependency notes

Story 06 builds entirely on already-implemented features — `clients` Story 05, `tasks` Story 02, `visits` Story 04, and `roles` Story 01 are all shipped in the current codebase, so every endpoint it adds can be built and run today. It extends `Endpoints/StatisticsEndpoints.cs`/`Dtos/Statistics/StatisticsDtos.cs` in place (per the intake's own instruction) rather than creating a new feature file, and reuses the existing `TaskAssigner`/`ClientManager` authorization policies (`Program.cs` lines 78–81) rather than inventing new ones.

**`leaves` is a partial, deferred dependency.** [`../leaves/00-overview.md`](../leaves/00-overview.md) is still the empty stub — no `LeaveRequest` entity, no endpoints, and no `NN-story-*.md` exists yet for it (only its intake, which drafts but does not implement a `LeaveRequest` shape). The dashboards intake's acceptance criteria ask for "leaves" as one of four data sources in the per-employee monthly rollup, and for "Attendance/Attendance State - Leaves" in the Supervisor's employee review. Story 06 ships the other three sources (tasks, visits, current-attendance snapshot) now and explicitly documents the missing "Leaves" column as a follow-up (see Story 06's Prerequisites, Edge Cases & Failure Modes, and Done Criteria) — adding it later is a non-breaking, additive change to `EmployeeMonthlyActivityDto` once `leaves` ships and its `LeaveRequest` shape is actually implemented. This mirrors how [`../visits/00-overview.md`](../visits/00-overview.md) documents Story 04's own historical split around its `clients` blocker.

No real reporting-hierarchy/team model exists on `Employee` (confirmed: [`../tasks/00-overview.md`](../tasks/00-overview.md) line 20, and `Models/Employee.cs` has no `SupervisorId`/`TeamId` field). Story 06's "supervisor + team" endpoints therefore define "team" as a heuristic — employees who are the assignee of a visit-linked task for the client the Supervisor is assigned to via their own `AssignedLocationId` (the same scoping [`../clients/05-story-client-location-management.md`](../clients/05-story-client-location-management.md) already uses for Supervisor visibility) — not a true reporting hierarchy. See Story 06's Story Goal (point 5) and Edge Cases for the full reasoning and its known limitations (false negatives/positives in team membership).
