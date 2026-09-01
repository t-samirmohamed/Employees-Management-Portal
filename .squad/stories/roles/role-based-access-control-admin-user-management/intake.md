# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/roles/role-based-access-control-admin-user-management/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Role-based access control & admin user management
- **Feature slug (folder under `plans/`):** `roles`

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
Role-based access control & admin user management
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
Source: "Employee Managment System.pdf" (client spec), page 1 — "System user" and "Admins" sections, pasted verbatim.

System user:
- System permission on all actions and lists/details view
- Admin role overrides any other role
- Attendance status is calculated depending of the user is in a visit / or a task
  requiring attendance status

Admins
- View all system users
- Can add any other roles
- Is the only user permitted to activate/deactivate employees/supervisors

(The rest of the spec repeatedly refers to four roles — Admin, Manager, Supervisor,
Employee — each with different capabilities. Those per-role capabilities are broken out
into the other sibling stories in this workspace: clients, tasks, visits, leaves,
notifications, dashboards. This story is only the role model + admin user management
itself, which every other story depends on.)
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
- [ ] Add ASP.NET Identity roles: Admin, Manager, Supervisor, Employee (RoleManager<IdentityRole>,
      seeded on startup). Today AddIdentityCore has no roles at all.
- [ ] Signup / user-creation assigns exactly one of the four roles; admin-initiated creation can
      set any role.
- [ ] JWT includes the user's role as a claim (alongside the existing custom securityStamp claim
      issued by Services/TokenService.cs); endpoint groups enforce it via authorization policies.
- [ ] Admin bypasses every role check app-wide ("Admin role overrides any other role") — implement
      as one policy rule, not per-endpoint special-casing.
- [ ] New GET /api/users (Admin-only) — lists all system users with role + linked employee summary.
- [ ] Restrict PATCH /api/employees/{id}/activate and /deactivate to Admin only (today: any
      authenticated caller, per Endpoints/EmployeeEndpoints.cs).
- [ ] Decide the Supervisor/Manager data shape: an Employee row plus a role claim, not a separate
      table — confirm before other stories build role-scoped queries on top of it.
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
- **Depends on code areas or other stories:** `Endpoints/AuthEndpoints.cs`, `Endpoints/EmployeeEndpoints.cs`, `Program.cs` (JWT bearer setup), `Models/ApplicationUser.cs`. Foundational — every other story in this batch (clients, tasks, visits, leaves, notifications, dashboards) assumes roles exist. Implement this one first.

## Extra notes (optional)

- Source document: `Employee Managment System.pdf` (user's desktop).
- Companion frontend story: `employee-management-web` repo, `.squad/stories/roles/role-based-access-control-admin-user-management/intake.md`.
- Current state before this story: [auth](../../../plans/auth/00-current-state.md), [employees](../../../plans/employees/00-current-state.md), [users](../../../plans/users/00-current-state.md) — none has a role concept today.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `.`. Primary language: `C#`.
- Extend `AddIdentityCore<ApplicationUser>()` in `Program.cs` with `.AddRoles<IdentityRole>()`; use `UserManager.AddToRoleAsync`.
- `TokenService` (`Services/TokenService.cs`) already stamps a custom claim on the JWT — add the role claim the same way.

## Out of scope

- Per-employee/per-resource permission overrides beyond the 4 fixed roles.
- The actual clients/tasks/visits/leaves endpoints — this story only establishes the role model and retrofits existing auth/employees/users endpoints.
- Frontend UI for any of this (separate companion story in `employee-management-web`).
