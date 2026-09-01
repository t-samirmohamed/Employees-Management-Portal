# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/dashboards/activity-dashboards-stats/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Activity dashboards & stats
- **Feature slug (folder under `plans/`):** `dashboards`

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
Activity dashboards & stats
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
Source: "Employee Managment System.pdf" (client spec), page 1 — pasted verbatim.

Admins/Managers:
- Should be able to view an employee's monthly activity "Tasks - Visits -Attendance State
  - Leaves" for every employee in their details view
- Status/visits stats
- Client visits stats and most visited clients
- Should be able to view all supervisors and their teams and stats for all the employees
  /supervisors and all the open tasks/ employees attendance status, etc., categorized per
  team/suprvisor in their detailed views.

Supervisors:
- Status/visits stats
- Review an employee's monthly activity "Tasks - Errands- Attendance/Attendance State -
  Leaves" for every employee
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
- [ ] Extend the existing GET /api/statistics/employees group (or add siblings under it)
      with:
  - [ ] Per-employee monthly activity rollup: tasks, visits, attendance state history,
        leaves — for a given month, scoped to one employee (Admin/Manager: any employee;
        Supervisor: their team only).
  - [ ] Status/visit stats (counts by status).
  - [ ] Client visit stats + most-visited-clients ranking (aggregates over `visits`/
        `clients` data).
  - [ ] Supervisor + team rollup: per-supervisor team stats, open tasks, attendance
        status — categorized per team, for Admin/Manager's detail view.
- [ ] All of the above are read/aggregate-only endpoints — no new write surface in this
      story.
```

---

## Attachments

Place files in `attachments/` next to this `intake.md`, then list them here so the planner knows what to open.

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| None. | Source content pasted into Description above. |

---

## Dependencies

- **Blocked by / related ids:** None (tracker: none). Plan/implement this story last — it aggregates data owned by `clients`, `tasks`, `visits`, and `leaves`, so those should have stable shapes first.
- **Depends on code areas or other stories:** `Endpoints/StatisticsEndpoints.cs` (existing), plus `clients`, `tasks`, `visits`, `leaves`, `roles` (team/scope filtering).

## Extra notes (optional)

- Source document: `Employee Managment System.pdf` (user's desktop), "Admins/Managers" and "Supervisors" sections.
- Companion frontend story: `employee-management-web` repo, `.squad/stories/dashboards/activity-dashboards-stats/intake.md`.
- Current state: [employees current-state](../../../plans/employees/00-current-state.md) — `GET /api/statistics/employees` already exists as an aggregate-stats precedent to extend rather than replace.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `.`. Primary language: `C#`.
- Follow the existing `Dtos/Statistics/` + `Endpoints/StatisticsEndpoints.cs` pattern for new response shapes.

## Out of scope

- Any of the underlying entities/write endpoints (clients/tasks/visits/leaves) — this story is read-only aggregation on top of them.
