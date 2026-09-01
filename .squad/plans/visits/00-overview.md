# visits — plan overview

Entry point for the **visits** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Depends on |
|----|------|-------|------------|
| 04 | [04-story-visit-tracking-and-status.md](04-story-visit-tracking-and-status.md) | Visit Tracking & Status | [clients](../clients/00-overview.md) Story 05 (implemented), [tasks](../tasks/00-overview.md) Story 02 (implemented) |

## Dependency notes

Story 04 is fully implemented. It shipped in two passes because of its hard blocker on `clients`: `Visit.ClientId`/`Visit.LocationId` are core, non-nullable fields, and `Models/Client.cs`/`Models/Location.cs` didn't exist yet when this story was first executed — everything except `POST /api/visits` and the `Visit`→`Client`/`Location` FK configuration was built first (`Visit` model with plain unconstrained `int` FKs, the `Visits` DbSet, the `TaskItem.RelatedVisitId`→`Visit` FK + unique filtered index, `GET /api/visits`/`GET /api/visits/{id}`, and the `TaskEndpoints.cs` changes below), with the deferred pieces left as `TODO(clients story)` comments in `Data/AppDbContext.cs` and `Endpoints/VisitEndpoints.cs`. Once [`clients` Story 05](../clients/05-story-client-location-management.md) shipped `Client`/`Location` (independently, in parallel), those TODOs were completed exactly as designed — `CreateVisitAsync` and the `Visit`→`Client`/`Location` FK block — with no rework needed to the already-shipped parts.

Story 04 also builds directly on `tasks` Story 02 ([`../tasks/02-story-task-assignment-and-comments.md`](../tasks/02-story-task-assignment-and-comments.md)), which was already implemented before this story started — it adds the FK constraint onto `TaskItem.RelatedVisitId` that Story 02 deliberately left unconstrained, and relaxes `CompleteTaskAsync`'s block on completing visit-linked tasks. It does **not** modify `Services/IEmployeeAttendanceService` at all: visit-linked tasks are created with `AttendanceRequired = true`, so the existing attendance recalculation already covers "employee is on an active visit" for free — see Story 04's Story Goal for the reasoning.
