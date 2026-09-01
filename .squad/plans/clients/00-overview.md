# clients — plan overview

Entry point for the **clients** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Depends on |
|----|------|-------|------------|
| 05 | [05-story-client-location-management.md](05-story-client-location-management.md) | Client & Location Management | [roles](../roles/00-overview.md) |

## Dependency notes

Story 05 is the only story currently planned in `clients/`. It depends on `roles` Story 01 (already implemented) for the `Admin`/`Manager`/`Supervisor`/`Employee` roles, and reuses the `TaskAssigner` policy introduced by `tasks` Story 02 (also already implemented) rather than inventing a new one.

It also completes the `TODO(clients story)` already left in `Data/AppDbContext.cs` and `Endpoints/VisitEndpoints.cs` by the already-implemented `Visit`/`VisitEndpoints.cs`/`20260901145826_AddVisits` migration — see Story 05's own Prerequisites for the full account of what already exists ahead of [`../visits/00-overview.md`](../visits/00-overview.md)'s stale "not yet implemented, hard-blocked on clients" status.

`POST /api/visits` itself remains unimplemented after Story 05 ships — see [`../visits/04-story-visit-tracking-and-status.md`](../visits/04-story-visit-tracking-and-status.md), whose `Client`/`Location` prerequisite this story satisfies (that story's own FK-configuration task becomes redundant with Story 05's task 3; its remaining work is just the transactional `Visit`+`TaskItem` creation handler and the accepted-visit concurrency check).

"Most visited clients" ranking is explicitly owned by [`../dashboards/00-overview.md`](../dashboards/00-overview.md) per its own intake — not duplicated here.
