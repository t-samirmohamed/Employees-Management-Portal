# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/notifications/leave-request-notifications/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Leave request notifications
- **Feature slug (folder under `plans/`):** `notifications`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `` *(used in filenames and plan tables; fill manually if empty)*
- **Work item type:** ``
- **Status:** ``
- **Assignee:** ``
- **Labels:** ``

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Leave request notifications
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
Source: "Employee Managment System.pdf" (client spec), page 1 — pasted verbatim.

Admins/Managers:
- Should receive a notification when an employee/Supervisor submits a leave request and
  take action "Accept/Reject/Request Delay to a specific date and stating a reason"

Supervisors:
- Should receive a notification when an employee submits a leave request and take action
  "Accept/Reject/Request Delay to a specific date and stating a reason"

(The document does not mention any other notification trigger anywhere — task/visit
assignment notifications are not specified.)
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
- [ ] Notification entity: recipient (user), type, payload/reference (leave request id),
      read/unread state, created timestamp.
- [ ] On POST /api/leaves (submit), create a notification for every valid approver per the
      routing rules from the `leaves` story. Don't add task/visit notifications
      speculatively — not in the source doc.
- [ ] GET /api/notifications (current user's own), PATCH /api/notifications/{id}/read.
- [ ] Delivery mechanism (poll vs push/SignalR) is unspecified in the source doc — default
      to a pollable GET endpoint unless planning decides real-time is warranted.
```

---

## Attachments

Place files in `attachments/` next to this `intake.md`, then list them here so the planner knows what to open.

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| None. | Source content pasted into Description above. |

---

## Dependencies

- **Blocked by / related ids:** None (tracker: none).
- **Depends on code areas or other stories:** `leaves` story (this listens for its submit event), `roles` (recipient resolution).

## Extra notes (optional)

- Source document: `Employee Managment System.pdf` (user's desktop), "Admins/Managers" and "Supervisors" sections. The doc only ties notifications to leave-request submission.
- Companion frontend story: `employee-management-web` repo, `.squad/stories/notifications/leave-request-notifications/intake.md`.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `.`. Primary language: `C#`.
- `Middleware/ApiLoggingMiddleware.cs` is the existing example of a cross-cutting write that catches/logs its own failures rather than rethrowing — notification creation on leave submit should follow the same "never break the main request" principle.

## Out of scope

- Email/SMS/push delivery — not mentioned in the source doc; in-app only unless a later story asks for it.
- Notifications for anything other than leave-request submission.
