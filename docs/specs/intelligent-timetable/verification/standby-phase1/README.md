# Weekly standby — phase 1 verification

**Date:** 2026-10-16. Scope: weekly allocation in the existing timetable only.

[Summary results](results.json): **124 backend/SQL tests and 5 Angular tests passed**. [Local migration](local-migration.json) confirms schema73 and unchanged pre-existing values across45 checked tables; [local read check](local-read-check.json) confirms the updated runtime can read the real test-school workspace without edits.

## Automated evidence

- `WeeklyStandbyTests`: teaching readiness, real shortened days, availability, physical capacity, manual conflicts, fair load/constrained periods, locks/exclusions/confirmed cover, stale/duplicate/invalid/cancelled requests, school scope/delegated permissions, reviewed persistence/history and replay rejection.
- `WeeklyStandbySqlTests`: guarded fresh `AlFalahStandbyTests_<guid>` LocalDB, all real migrations, preview without writes, exact reviewed apply, stable teaching/reserve identities and metadata, an independent SQL context invalidating a teacher revision, publication and scope checks. Original school/Drive data are not test destinations.
- `SchoolTimetableServiceTests`: regression plus weekly reserve settings/locks through regeneration, clone and restore. Existing generation/review/substitution/availability cases are included in the final focused run.
- Five focused Angular tests: exact reviewed request, preview without apply, manual teaching protection, locks/mode retention/undo, conflict retention, protected cover/exclusion and prerequisite blocking.
- [Browser results](browser-results.json): real Angular shell and real isolated SQL API, widths 390/768/1440/1920, 18 teachers and eight periods, contained grid scrolling and no page overflow/errors. The 1440px workflow verifies manual and fair preview/apply, dirty navigation cancellation, stale-save retention/reload, publication, personal teacher standby reads and actual weekly PDF bytes. Other widths perform read-only responsive/detail focus checks. No operational or provider writes are part of these browser tests.
- [Weekly timetable PDF](weekly-timetable.pdf) is generated from the existing renderer, not the future official daily register. PDF parsing confirms one page with extractable text.

Commands and complete local logs are under `.audit/standby-discovery/`; no private sessions or connection material are committed. `backend-final.txt`, `frontend-tests.txt`, `frontend-build.txt` and the TRX files record the actual check results. The frontend test command uses both `--ts-config tsconfig.standby-spec.json` and `--include "src/app/features/timetables/standby/*.spec.ts"`; omitting `--include` makes the existing runner discover excluded unrelated specs.

## Visual review

Workspace and focused-grid captures are provided for all four widths. Representative captures: [desktop grid](grid-1440.png), [phone grid](grid-390.png), [review](review-1440.png), [wide workspace](workspace-1920.png). Review led to a non-overlaying save footer, more compact configuration/metrics, balanced phone day controls, a narrower sticky phone identity column, explicit available counts and keyboard focus return from details. The matrix remains readable and scrolls horizontally instead of shrinking a whole week into phone width.

An expired private verification session and an unenrolled synthetic teacher signature initially blocked the harness. Fresh test sessions and an explicitly synthetic isolated signature fixture restored normal authorization; production authentication/signature rules were not bypassed. A raw enum assertion was corrected to accept the API's existing `Standby` string representation. These were test-harness corrections, not application changes.

## Build and acceptance limits

Development builds pass. Production build still fails on unchanged `visits-v2/visit-workspace/visit-workspace.component.css` (34.50kB) and `storage/storage-page.component.css` (39.34kB) exceeding the existing 32kB component budget. No budgets were relaxed. These pre-existing release blockers are outside the weekly standby change.

Owner appearance acceptance, screen-reader review and physical/printer acceptance are not claimed. Phase 2 dated coverage/teacher execution and phase 3 official QF71-B-2-1 PDF remain unimplemented. No email, Drive upload, production deployment or commit is performed by this phase.
