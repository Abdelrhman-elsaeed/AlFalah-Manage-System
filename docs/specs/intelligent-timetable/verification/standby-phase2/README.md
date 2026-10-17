# Daily coverage — phase 2 verification

Date: 2026-10-16. Scope: absence preparation, reviewed daily assignment and signed teacher execution.

## Automated checks

- `DailyCoverageTests`: standby-only eligibility, repeat reconciliation without approval, effective date ownership, exact request replay, separate retained signatures, correction counting, absence correction/absent substitute, stale previews/drafts, school/owner boundaries, competing needs and pinned manual choices, receipt before execution, inability/release, secretary attendance enqueue/no-op, worker lease/replay, delegated administration and reassignment during editing with retained old draft/receipt.
- `TimetableSubstitutionPhase8Tests`: existing pairs/co-teaching, structural swaps, dated cover, scope, candidate staleness, audit/version/outbox and deduplicated teacher notification checks. Former free-teacher cover fixtures now explicitly provide scheduled standby, recorded original absence and an active term. Structural-swap fixtures remain independent.
- `DailyCoverageSqlTests`: fresh guarded `AlFalahCoverageTests_<guid>` integrated LocalDB, all real migrations twice, preview without assignment, a constraint fault on the **second** ledger insert proving whole-batch rollback (including first assignment/version/outbox), replay-safe successful approval, an independent context advancing a revision, persisted receipt/draft/execution and retained signature bytes. No school or Drive destination is used for write tests.
- Eleven focused Angular tests cover weekly behavior plus teacher dirty-navigation/draft conflicts, explicit signing, lost-response request replay and exact reviewed manager approval/server-clock refresh, plus the existing instructor-dashboard compatibility test. Logs and TRX files are under `.audit/standby-phase2/`.

The final selection passes **333 backend/SQL checks and 11 Angular checks**. [Results](results.json), [local additive migration](local-migration.json) and [authenticated local reads](local-read-check.json) record schema74 and unchanged existing values across45 checked tables. The app runs at `http://localhost:4200` and API at `http://localhost:5264`. The broader regression run also exposed the existing hand-built SQLite messaging fixture missing `Users.ProtectedPassword`; the related source/query/fixture were not changed by this phase. Its failure is retained in `regression-tests.txt`, rather than described as a passing full suite. The final focused selection excludes that unrelated fixture. Other opt-in SQL suites are reported as skipped when their separate connection variables are unset.

## Browser and visual checks

[Authenticated browser results](browser-results.json) use the actual Angular shell and a real isolated SQL API. All coverage/assignment/draft/signature requests go to the synthetic database. Checks cover390/768/1440/1920 widths, contained ledger scrolling, manual choice plus fair preview/approval, denied unrelated-teacher/teacher-administrator HTTP requests, future acknowledgement with early execution disabled, saved historical draft reload, separate signed execution/history and dashboard tasks.

Representative captures: [manager ledger](manager-1440.png), [phone tasks](teacher-390.png), [candidate explanation](candidates-1440.png), [review](review-1440.png), [execution review](execution-review-1440.png), [retained execution](executed-1440.png). Visual review confirms platform typography/palette, distinct buttons without a green navigation strip, bounded rows and a separate receipt/draft/signing flow. Captures wait for dialog animation and unrelated synthetic-shell toasts to finish; they are not claimed as proof of unrelated storage/dashboard permission flows. Synthetic signatures are explicit test fixtures, not real staff signatures.

## Manual test route

1. Publish a valid timetable with weekly standby. As the authorized Secretary, record an original teacher's absence.
2. Open **التبديل والاحتياطي → التغطية اليومية**. The worker prepares needs; **تحديث احتياجات الغياب** offers explicit refresh. Inspect candidates and choose manual or fair allocation.
3. Preview, review warnings/reason, then approve. Confirm the actual dated lesson owner changes and the teacher receives the existing internal notification.
4. As the covering teacher, open **تكليفات الانتظار** from navigation/dashboard. Sign receipt, write/save the draft, then review and sign execution after the actual period end.
5. As administrator, inspect retained history. Correct absence or reopen execution with a reason; previous signed versions remain. An unrelated teacher and another school must be denied.

## Limits

Production build remains blocked by unchanged VisitsV2 CSS34.50kB and storage CSS39.34kB exceeding the existing32kB component budget; no budget was relaxed. Owner appearance, assistive technology and operational pilot acceptance are pending. Official QF71-B-2-1 PDF/Drive/physical printer acceptance is phase3 and is not claimed here. No provider upload, deployment or commit is performed.
