# Al-Falah Spec Kit — Agent Constitution

**Status:** Living · **Last updated:** 2026-07-10

## Core rule: the spec kit IS the memory
Every AI agent working on this project MUST:
1. **Read `docs/` first** before acting.
2. Treat the spec kit as the **single source of truth**.
3. **Never** shorten, skip, or assume requirements.
4. Keep **Arabic strings verbatim** (labels, templates, role names).
5. **Stop after each phase** and report what was done.

## Memory-update rule (MANDATORY)
After ANY change to scope, entities, roles, endpoints, or workflow — or after
finishing a task/phase — the agent MUST update the spec kit **before finishing**:
- (a) Update the relevant `docs/` file(s).
- (b) Append a row to the **Change-log** in `docs/README.md` with date + summary.
- (c) Log any deviation in `docs/14-DECISIONS-AND-DEVIATIONS.md`.
- (d) Update the matching phase file status in `docs/phases/`.

> A task is **NOT done** until the spec kit reflects it.

## Prompt template hook
Every future IDE prompt should begin with:

> "Read `docs/` spec kit + `.spec/constitution.md` first. This is the single
> source of truth. At the end, update the spec kit (README change-log + relevant
> docs + deviations log) to reflect what you did."

## Non-negotiables
- Layered **modular monolith**; **no microservices**.
- **Thin controllers**; **no EF queries in controllers**.
- **`ApiResponse<T>`** everywhere.
- **School-scoped queries enforced in the backend** (never trust frontend filtering).
- **Database-driven roles/permissions** (not enum-only).
- **Arabic-primary + RTL** from day one.
- **Do not implement future phases early.**
