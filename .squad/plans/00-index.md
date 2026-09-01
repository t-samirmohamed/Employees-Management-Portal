# Plans index

One row per feature folder under `.squad/plans/`. `NN` continues as a global execution sequence across all features when `naming.globalSequence` is `true` in `config.yaml`.

Each `00-current-state.md` now spans both repos: this API (`EmpoloyeeManagment`) and its Next.js client (`employee-management-web`, sibling repo, separate git history).

| Feature | Overview | NN range |
|---------|----------|----------|
| [auth](auth/00-current-state.md) | API: signup/login/logout, JWT + security-stamp revocation. Web: login/signup pages, localStorage session, auto-logout on 401 | 00 (baseline) |
| [employees](employees/00-current-state.md) | API: CRUD-lite (list/get/create/activate/deactivate) + statistics. Web: dashboard wired 1:1 to those endpoints | 00 (baseline) |
| [users](users/00-current-state.md) | API: Identity `ApplicationUser`, no standalone API yet. Web: no standalone feature either | 00 (baseline) |
| [logs](logs/00-current-state.md) | API: write-only API call logging middleware, no read endpoint. Web: nothing to show yet | 00 (baseline) |
