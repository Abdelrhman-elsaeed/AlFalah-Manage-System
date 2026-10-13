# School Duty — Feature Phase Tracker

**Status:** D1 IMPLEMENTED / VERIFIED — final owner visual acceptance pending; D2/D3 not started.  
**Updated:** 2026-10-13  
**Spec Kit:** [School Duty](../specs/school-duty/README.md)

## Authorized work completed

Reviewed five supplied images and the client conversation, inspected existing timetable/staff attendance/signature/student/storage code, and created English research, specification, clarification, UX, plan and task files in a dedicated folder. The owner confirmed repeated next-date exchange with manual override, application of the existing handwritten signature, manager-first operation with general permission assignment deferred, equal rotation over explicitly selected teachers, and school/year/term/actual-teacher/date archival with separate letter/report PDFs.

## Implementation phases

| Phase | Deliverable | Status |
|---|---|---|
| D1 | Rotation, exclusions, manager workspace and automatic/manual absence exchange | IMPLEMENTED; isolated verification complete; owner visual review pending |
| D2 | Teacher mobile acknowledgement/report, saved signatures and retained snapshots | NOT STARTED |
| D3 | Official PDFs, protected school storage, recovery and final acceptance | NOT STARTED |

The owner's 2026-10-13 implementation request authorizes D1 using the confirmed decisions and documented defaults relevant to D1. Implementation and isolated SQL verification are authorized; production deployment, data resets, D2/D3 and general feature assignment are excluded.

## Verification of planning work

Source images visually inspected; conversation read; integration claims traced to repository files; supplied-source SHA-256 fingerprints recorded. Document links, requirement coverage and phase consistency are checked before handoff. No runtime tests are needed for this documentation-only change; no feature behavior is claimed as implemented.

## Implementation handoff — 2026-10-13

Prepared [the D1 prompt](../specs/school-duty/prompts/d1-implementation.md) at the owner's request for a new chat. Submitting that prompt authorizes D1 and its documented relevant defaults; preparing it has not started implementation in this chat. Exceptionally impressive frontend quality is an explicit acceptance priority. Static waiting-period/cover findings and the recommended later official-register extension are recorded in research; D1–D3 scope is unchanged.

## D1 delivery — 2026-10-13

Implemented the authorized D1 application/schema/UI work. See [verification and visual evidence](../specs/school-duty/verification/d1.md). Final affected backend verification passes 278 checks, including 22 duty tests (16 real SQL tests and six in-memory workflow/rotation tests). Real local HTTP/JWT/SQL/hosted-worker smoke passes 11 checks; 69 targeted Angular regressions pass. Rendered Angular browser fixtures exercise 390px, 768px and 1440px layouts, principal manager states and teacher confidentiality presentation; client visual acceptance remains pending.

Four non-destructive D1 migrations were applied only to fresh `AlFalahDutyTests_*` LocalDB databases. Original operational data, settings, credentials and key rings were preserved. No production deployment, D2 signatures/reports, D3 PDF/archive, general delegation or official waiting-register work was performed.

Known baseline limits: the broad backend suite has two failures reproduced at HEAD `15037a3`; unrelated storage/visits CSS exceed existing production budgets, and unrelated storage specs block the broad Angular test configuration. The targeted Angular configuration preserves those files and passes. These are documented, not silently changed to make D1 appear fully green.
