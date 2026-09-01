# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/leaves/leave-requests-approval-workflow/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Leave requests & approval workflow
- **Feature slug (folder under `plans/`):** `leaves`

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
Leave requests & approval workflow
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
Source: "Employee Managment System.pdf" (client spec), pages 1-2 — pasted verbatim.

Admins/Managers:
- Should receive a notification when an employee/Supervisor submits a leave request and
  take action "Accept/Reject/Request Delay to a specific date and stating a reason"
- Managers can submit a leave request and should be able to edit/delete it for Admins to
  take action on

Supervisors:
- Should receive a notification when an employee submits a leave request and take action
  "Accept/Reject/Request Delay to a specific date and stating a reason"
- Can submit a leave request and should be able to edit/delete it for Managers to take
  action on

Employees
- Can submit a leave request and should be able to edit/delete it for
  Managers/Supervisors/Admins to take action on
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
- [ ] LeaveRequest entity: requester (Employee), dates, reason, Status (Pending, Accepted,
      Rejected, DelayRequested), approver, decision reason, requested-delay-date (when
      action = Request Delay).
- [ ] POST /api/leaves — submit (Employee, Supervisor, or Manager can be a requester).
- [ ] PATCH /api/leaves/{id} / DELETE /api/leaves/{id} — only by the original requester,
      only while still Pending.
- [ ] Approver routing per the doc: Employee's request -> Manager/Supervisor/Admin can
      action it; Supervisor's request -> Manager (or Admin, per "Admin overrides any
      role"); Manager's request -> Admin.
- [ ] PATCH /api/leaves/{id}/accept, /reject, /request-delay (delay requires a target
      date + reason; reject requires a reason per the doc's "stating a reason").
- [ ] Emits the event the `notifications` story listens for on submit — this story only
      owns persistence + status transitions; delivery is the other story's job.
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
- **Depends on code areas or other stories:** `roles` (who-can-action-whose-request routing needs the role model). The `notifications` story depends on this one (it fires "when a leave request is submitted").

## Extra notes (optional)

- Source document: `Employee Managment System.pdf` (user's desktop), "Admins/Managers", "Supervisors", and "Employees" sections.
- Companion frontend story: `employee-management-web` repo, `.squad/stories/leaves/leave-requests-approval-workflow/intake.md`.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `.`. Primary language: `C#`.
- The same "edit/delete while pending" shape appears three times (Employee, Supervisor, Manager as requester) — implement once against "requester role" rather than three parallel code paths.

## Out of scope

- Notification delivery/storage (separate `notifications` story).
- Any leave-balance/accrual tracking — not mentioned in the source doc, don't invent it.
