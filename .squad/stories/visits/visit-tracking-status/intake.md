# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/visits/visit-tracking-status/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Visit tracking & status
- **Feature slug (folder under `plans/`):** `visits`

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
Visit tracking & status
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
Source: "Employee Managment System.pdf" (client spec), pages 1-2 — pasted verbatim.

Employees
- When an employee is out on a visit, and the visit is not done, the employee can't have
  another active / assigned visit at the same time "and the employee already accepted to
  take"

Visits
- DateTime
- Status "inherited from task"
- Client
- Location "URL" "filtered by selected client"

System user (context):
- Attendance status is calculated depending of the user is in a visit / or a task
  requiring attendance status
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
- [ ] Visit entity: DateTime, ClientId, LocationId (location must belong to the selected
      client), Status (read-only, mirrors the linked Task's status — no independently
      settable status field).
- [ ] Enforce the concurrency rule server-side: creating/accepting a visit for an employee
      fails if that employee already has another visit that is accepted and not yet Done.
- [ ] While an employee has an active accepted visit (or an active task with "attendance
      required status"), Employee.AttendanceStatus is computed/updated automatically —
      coordinate with the `tasks` story so this is one implementation, not two.
- [ ] GET /api/visits, GET /api/visits/{id}, POST /api/visits. Exact creation flow
      (Visit-first vs Task-first, given "Related Visit" lives on Task) to be confirmed
      during planning — the doc doesn't fully spell out ordering.
- [ ] Location filter: given a clientId, return only that client's locations (consumes
      the `clients` story's location data).
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
- **Depends on code areas or other stories:** `clients` (Client + Location must exist first), `tasks` (status inheritance direction — Visit status mirrors Task status), `Models/Employee.cs` `AttendanceStatus` enum (existing field this story starts computing, jointly with `tasks`).

## Extra notes (optional)

- Source document: `Employee Managment System.pdf` (user's desktop), "Employees" and "Visits" sections, plus the "System user" attendance-status bullet.
- Companion frontend story: `employee-management-web` repo, `.squad/stories/visits/visit-tracking-status/intake.md`.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `.`. Primary language: `C#`.
- "Location 'URL'" in the doc suggests a map/external-link URL field on `Location`, not a physical address — confirm during planning.

## Out of scope

- Task fields/comments/attachments (separate `tasks` story).
- Client/location CRUD itself (separate `clients` story) — this story only consumes them.
