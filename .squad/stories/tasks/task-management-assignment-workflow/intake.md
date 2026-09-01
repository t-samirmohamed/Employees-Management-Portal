s# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/tasks/task-management-assignment-workflow/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Task management & assignment workflow
- **Feature slug (folder under `plans/`):** `tasks`

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
Task management & assignment workflow
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
Source: "Employee Managment System.pdf" (client spec), pages 1-2 — pasted verbatim.

Supervisors:
- Assign/Reassign Tasks/Visits in a calendar, list, and kanban views, and their details

Employees
- View their assigned Tasks/Visits in a calendar, list, and kanban views and take action
  "Accept/ Reject Stating Reason"
- View their list of tasks/visits and details

Tasks
- Name / Due DateTime(Visit day) / Assignee / Related Visit "optional"/ Attachments/Notes
  / Status (Computed "new - in progress once accepted - rejected - cancelled - done")
  (linked to visit status) / attendance required status
- Comments (Add / Delete / Update)
- Delete attachment
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
- [ ] Task entity: Name, DueDateTime, AssigneeId (-> Employee), RelatedVisitId (nullable,
      -> Visit), Notes, AttendanceRequired (bool), computed Status enum: New, InProgress,
      Rejected, Cancelled, Done (server-computed, derives from acceptance + linked visit
      status per "(linked to visit status)").
- [ ] POST /api/tasks (Supervisor: assign), PATCH /api/tasks/{id} reassign, GET /api/tasks
      (role-filtered — Supervisor sees their team's, Employee sees only their own),
      GET /api/tasks/{id} detail.
- [ ] PATCH /api/tasks/{id}/accept and /reject (Employee-only; reject requires a reason
      string).
- [ ] Attachments: upload + DELETE /api/tasks/{id}/attachments/{attachmentId}. Storage
      approach (disk vs blob) to be confirmed during planning — not specified in the source.
- [ ] Comments: POST/DELETE/PATCH /api/tasks/{id}/comments (add/delete/update, per doc).
- [ ] Status-computation and the "attendance required" flag's effect on
      Employee.AttendanceStatus should be implemented once, coordinated with the `visits`
      story rather than built twice.
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
- **Depends on code areas or other stories:** `roles` (Supervisor/Employee role checks, team scoping), `Models/Employee.cs` (existing `AttendanceStatus` enum — this story starts actually computing it). Tightly coupled to `visits` — a Task's status is "linked to visit status"; plan `tasks` and `visits` together even though they are separate intake files.

## Extra notes (optional)

- Source document: `Employee Managment System.pdf` (user's desktop), "Supervisors", "Employees", and "Tasks" sections.
- Companion frontend story: `employee-management-web` repo, `.squad/stories/tasks/task-management-assignment-workflow/intake.md`.
- Current state: `Models/Employee.cs` already has an `AttendanceStatus` enum (`InOffice, Absent, OnVacation, OutOfOffice`) per [employees current-state](../../../plans/employees/00-current-state.md), but nothing computes it yet — this story (with `visits`) is what starts driving it.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `.`. Primary language: `C#`.
- The global `JsonStringEnumConverter` registered in `Program.cs` already serializes enums as strings — new Task-status enum will follow the same convention as `Gender`/`EmployeeStatus`/`AttendanceStatus` automatically.

## Out of scope

- Visit entity fields/endpoints themselves (separate `visits` story) — this story only owns the optional `RelatedVisitId` FK on Task.
- Calendar/list/kanban rendering (frontend story).
- Leave requests (separate `leaves` story, even though both are "request -> approve/reject" shaped).
