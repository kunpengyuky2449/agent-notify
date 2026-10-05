# Agent Notify working rules

This repository owns the standalone Windows app, sender tools and public user documentation. Read README.md and docs/architecture.md before implementation. Keep source/tests/documentation independent of personal projects, absolute workstation paths and account credentials.

- Never commit/package live keys, passwords, device/session files, inboxes, personal notification text or machine-local maps. Releases use scripts/package.ps1's explicit allow-list.
- Run scripts/test.ps1 for code changes. Persistence, queue deletion, identity and retry changes need meaningful failure/recovery checks. Use dummy data for UI previews and inspect relevant UI changes.
- Keep Windows PowerShell 5.1 support; separate modern/runtime migrations from routine fixes. Update both README languages and installation guides for behavior changes.
- Do not claim cloud receipt, visible banners, gaming display or startup based only on offline tests. Record actual evidence and user confirmations separately.
- Login, purchases and credential entry happen locally by the user. Do not ask for secrets in chat or weaken DPAPI/ACLs. No implicit credential transfer or message authorization is granted by these rules.
- Preserve modified/unknown installation files and user data during upgrades/removal. Native banners depend on Windows settings; do not silently rewrite global Do Not Disturb policy.
- Maintain any local project_handoff records separately from public source; these records and machine maps are ignored. Commits, pushes, publishing and messages require authorization from the task.
