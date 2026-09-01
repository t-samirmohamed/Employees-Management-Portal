# notifications — plan overview

Entry point for the **notifications** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Depends on |
|----|------|-------|------------|
| 07 | [07-story-leave-request-notifications.md](07-story-leave-request-notifications.md) | Leave Request Notifications | [roles](../roles/00-overview.md) Story 01 (implemented); [leaves](../leaves/00-overview.md) (partial — see Dependency notes) |

## Dependency notes

Story 07 ships the `Notification` entity, `GET /api/notifications`, `PATCH /api/notifications/{id}/read`, and a reusable `INotificationService.NotifyLeaveRequestSubmittedAsync` primitive — all buildable today, with no compile-time dependency on `leaves` (`Notification.ReferenceId` is a bare, unconstrained `int`, deliberately mirroring how `TaskItem.RelatedVisitId` started before the `visits` story attached a real FK to it).

**`leaves` is a partial, deferred dependency** — the same shape [`../dashboards/00-overview.md`](../dashboards/00-overview.md) already documents for its own `leaves` dependency. [`../leaves/00-overview.md`](../leaves/00-overview.md) is still the empty stub (no `NN-story-*.md`, no entity, no endpoints) — only its intake drafts, but does not implement, a `LeaveRequest` shape and an approver-routing rule. The one piece of this feature's acceptance criteria Story 07 cannot complete — wiring the notification trigger into `POST /api/leaves` on submit — is blocked until `leaves` ships that endpoint. See Story 07's own Prerequisites, Story Goal, and Done Criteria for the full account.
