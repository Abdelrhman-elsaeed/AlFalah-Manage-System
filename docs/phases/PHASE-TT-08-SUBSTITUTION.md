# Intelligent Timetable — Phase 8

> **2026-09-16 modernization:** an additive inline swap experience is implemented in the main timetable grid. The existing dashboard and route remain supported. Same-day remains the compatibility default; whole-timetable cross-day search is opt-in. A cross-day candidate touching any confirmed future daily cover is always red and never moves or cancels that cover automatically. See [the inline swap experience specification](../specs/intelligent-timetable/09-inline-swap-experience.md).

The daily dashboard is available at `/intelligent-timetable/substitutions`, with an entry point from lesson cells in the review grid.

## Operations and policy

- **Daily cover:** replace an absent member for all periods of the selected lesson block on one school-local date. Permanent assignment membership and quota are retained; operational availability, collisions and maximum load use the replacement teacher. A scheduled standby period can be used for cover. Teachers recorded absent or excused are disqualified.
- **Structural swap:** exchange whole lesson occurrences within the same weekday by default, or across timetable days when `WholeTimetable` is explicitly selected. Feasible three-way cycles are offered separately with explicit before/after cards. Class, subject, room and co-teachers remain attached to their lesson. Previously confirmed future daily cover is revalidated, and the cross-day policy above is a hard conflict.
- **Green / yellow / red:** safe / warnings / hard conflicts. Red cannot execute. Yellow requires a global administrator, school manager, or secretary with timetable-management permission, and a trimmed reason of 1–1000 characters.
- **Paired periods:** selecting either half selects the entire block. Because legacy rows have no occurrence identifier, adjacent periods for a requirement with paired blocks are grouped conservatively; this may select an adjacent run larger than two periods. Co-teaching members move together in structural swaps; absence cover replaces the selected member throughout the block.
- **Live behavior:** a published timetable remains published. Its revision advances immediately. Teacher current context, rosters, teacher classroom actions and gate-pass teacher lookup use date-specific effective ownership. Weekly structural edits are visible through the existing timetable reads.

## API

All routes require authentication and an active school; confirmation and proposals require timetable management. Instructor/guardian roles are excluded from the management dashboard. Teacher operational views retain their own authorization.

| Method | Route under `/api/v1/intelligent-timetable/substitutions` | Purpose |
|---|---|---|
| GET | `/` | School-scoped timetable choices |
| GET | `/{id}?date=YYYY-MM-DD` | Effective daily lessons and audit history |
| GET | `/{id}/candidates?date=YYYY-MM-DD&sourceEntryId=1&mode=Substitution` | Teacher candidates; use `mode=Swap` for structural proposals |
| GET | `/{id}/inline-candidates?date=YYYY-MM-DD&sourceEntryId=1&scope=SameDay` | Grid projection for inline structural swap; use `scope=WholeTimetable` for cross-day search |
| POST | `/{id}/execute` | Confirm a server-generated proposal |

Confirmation supplies `requestId`, `revision`, `date`, `sourceEntryId`, `mode`, `proposalId`, `expiresAt`, optional `scope` (default `SameDay`), and optional `overrideReason`. Movements are regenerated on the server with the same scope; browser-supplied movements or colors are never trusted. Proposals expire after five minutes. Reusing a successful request is idempotent; expired or changed proposals return 409 and require review again. Historical days are read-only.

## Persistence and notifications

`AddTimetableSubstitutions` creates immutable `TimetableSubstitutions` and `TimetableSubstitutionMovements`, with school-scoped foreign keys, requester/approver, before/after revisions, reason, warnings and a version link. `AddCrossDaySwapDestination` adds nullable `ToDay` and backfills it from the original `Day`; `Day` remains the source day for compatibility. The original migration changes classroom and room indexes to nonunique indexes because co-teachers share one logical occurrence. Teacher-slot uniqueness remains enforced by SQL; logical class/room collisions are checked by the shared validator inside serializable transactions.

All movements, revision changes, immutable version, audit and outbox event are saved atomically. Structural swaps also create a fresh Phase 7 analysis. `TeacherTimetableChangedEvent` is consumed by the existing outbox worker to create deduplicated in-app notifications for every affected teacher, with the worker's existing retries. No email or external delivery channel is added.

## Verification

- Backend unit/integration tests: `dotnet test backend/AlFalah.slnx -c Release`.
- Backend build: `dotnet build backend/AlFalah.slnx -c Release`.
- Angular production build: `npm run build` from `frontend`.
- Browser interaction checks: run the Angular dev server, then `node frontend/scripts/test-timetable-substitutions.cjs`. Set `PLAYWRIGHT_MODULE` to an existing Playwright installation if it is outside the repository. This checks UI behavior against mocked API contracts, including paired previews, red disabling, yellow reasons, stale recovery and mobile overflow; server validation is covered separately by the .NET tests.

The solution now includes the test project so solution-level `dotnet test` actually runs the suite. Existing unrelated working-tree edits are retained.

Latest automated verification on 2026-09-16: 477 backend tests passed, including 23 Phase 8 tests; 84 Angular tests and the Angular production build passed. EF reports no pending model changes after `AddCrossDaySwapDestination`. The existing legacy browser harness was not rerun in this workspace because Playwright is not installed; its previous scenarios remain covered by backend/frontend regression tests. Existing unrelated nullable/CSS warnings remain.

The updated local API still needs to be started from the IDE (`http` launch profile). Automatic approval review blocked the agent's API startup attempt with “blocked by policy”; live authenticated HTTP verification was therefore not completed. UI browser checks use mocked API responses, while backend behavior is exercised by the .NET suite.
