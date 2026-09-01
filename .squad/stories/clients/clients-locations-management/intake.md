# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/clients/clients-locations-management/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Clients & locations management
- **Feature slug (folder under `plans/`):** `clients`

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
Clients & locations management
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
Source: "Employee Managment System.pdf" (client spec), pages 1-2 — pasted verbatim.

Admins/Managers:
- Client visits stats and most visited clients
- All Clients List and their details

Supervisors:
- Assigned Client list based to the assigned location

Clients:
- Name/Email/Contact
- List of locations: Name/Email/Contact
- Stats of visits for this month/3month/6month/year
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
- [ ] New Client entity: Name, Email, Contact. New Location entity: Name, Email, Contact,
      belongs to one Client (a client has many locations).
- [ ] GET /api/clients (Admin/Manager: all clients; Supervisor: only clients with a location
      matching the supervisor's assigned location), GET /api/clients/{id} detail with its
      locations.
- [ ] POST /api/clients + nested location create. Exact write surface (edit/delete) to be
      confirmed during planning — the doc only explicitly states list/detail + stats.
- [ ] GET /api/clients/{id}/visit-stats?range=month|3month|6month|year — visit counts per
      client for the four ranges named in the doc. Depends on the `visits` story's data.
- [ ] "Most visited clients" ranking — likely belongs in the `dashboards` story instead of
      being duplicated here; cross-reference so it is built once.
- [ ] Supervisor's "assigned location" needs a home — likely a field added to the
      Supervisor's Employee row by the `roles` story; this story's client-list query
      filters on it.
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
- **Depends on code areas or other stories:** `roles` story (Supervisor role + "assigned location" concept must exist first), `Data/AppDbContext.cs` (new `DbSet<Client>`, `DbSet<Location>` + migration). The `visits` story depends on this one — a Visit references a Client and a Location.

## Extra notes (optional)

- Source document: `Employee Managment System.pdf` (user's desktop), "Admins/Managers", "Supervisors", and "Clients" sections.
- Companion frontend story: `employee-management-web` repo, `.squad/stories/clients/clients-locations-management/intake.md`.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `.`. Primary language: `C#`.
- Follow the existing pattern: one static `Map*Endpoints` extension class in `Endpoints/`, DTOs grouped under `Dtos/Clients/`, models in `Models/`.

## Out of scope

- Visit scheduling itself (separate `visits` story).
- "Most visited clients" ranking widget (belongs to `dashboards`).
