# School Duty — Feature Phase Tracker

**Status:** D1/D2 IMPLEMENTED / VERIFIED — final owner visual acceptance pending; D3 not started.
**Updated:** 2026-10-13  
**Spec Kit:** [School Duty](../specs/school-duty/README.md)

## Authorized work completed

Reviewed five supplied images and the client conversation, inspected existing timetable/staff attendance/signature/student/storage code, and created English research, specification, clarification, UX, plan and task files in a dedicated folder. The owner confirmed repeated next-date exchange with manual override, application of the existing handwritten signature, manager-first operation with general permission assignment deferred, equal rotation over explicitly selected teachers, and school/year/term/actual-teacher/date archival with separate letter/report PDFs.

## Implementation phases

| Phase | Deliverable | Status |
|---|---|---|
| D1 | Rotation, exclusions, manager workspace and automatic/manual absence exchange | IMPLEMENTED; isolated verification complete; owner visual review pending |
| D2 | Teacher acknowledgement/report/signatures; mobile student picker, dismissal-lateness guardian notice/student history/existing manager statistics | IMPLEMENTED / VERIFIED; 361 backend, 86 Angular, 20 real HTTP checks; owner visual review pending |
| D3 | Source-faithful official PDFs, protected school storage, recovery and final acceptance | NOT STARTED; exact report division/reference gate confirmed |

The owner's 2026-10-13 implementation request authorizes D1 using the confirmed decisions and documented defaults relevant to D1. Implementation and isolated SQL verification are authorized; production deployment, data resets, D2/D3 and general feature assignment are excluded.

## Verification of planning work

Source images visually inspected; conversation read; integration claims traced to repository files; supplied-source SHA-256 fingerprints recorded. Document links, requirement coverage and phase consistency are checked before handoff. No runtime tests are needed for this documentation-only change; no feature behavior is claimed as implemented.

## Implementation handoff — 2026-10-13

Prepared [the D1 prompt](../specs/school-duty/prompts/d1-implementation.md) at the owner's request for a new chat. Submitting that prompt authorizes D1 and its documented relevant defaults; preparing it has not started implementation in this chat. Exceptionally impressive frontend quality is an explicit acceptance priority. Static waiting-period/cover findings and the recommended later official-register extension are recorded in research; D1–D3 scope is unchanged.

## D1 delivery — 2026-10-13

Implemented the authorized D1 application/schema/UI work. See [verification and visual evidence](../specs/school-duty/verification/d1.md). Final affected backend verification passes 278 checks, including 22 duty tests (16 real SQL tests and six in-memory workflow/rotation tests). Real local HTTP/JWT/SQL/hosted-worker smoke passes 11 checks; 69 targeted Angular regressions pass. Rendered Angular browser fixtures exercise 390px, 768px and 1440px layouts, principal manager states and teacher confidentiality presentation; client visual acceptance remains pending.

Four non-destructive D1 migrations were applied only to fresh `AlFalahDutyTests_*` LocalDB databases. Original operational data, settings, credentials and key rings were preserved. No production deployment, D2 signatures/reports, D3 PDF/archive, general delegation or official waiting-register work was performed.

Known baseline limits: the broad backend suite has two failures reproduced at HEAD `15037a3`; unrelated storage/visits CSS exceed existing production budgets, and unrelated storage specs block the broad Angular test configuration. The targeted Angular configuration preserves those files and passes. These are documented, not silently changed to make D1 appear fully green.

## D2/D3 scope amendment — 2026-10-13

Owner confirmed a premium mobile selected-student picker with search/filters/classroom grouping, dismissal-lateness guardian notices, recording in the student history and existing manager statistics with counts/names. D2 includes durability, deduplication, correction and school/recipient authorization checks. No typed-name fallback; dismissal lateness stays separate from morning delay/disciplinary penalties. C17 now confirms both guardian-dashboard notice and a system-authored message in existing internal Messages; no external channel/provider is needed.

The official report must preserve the exact supplied image divisions. Reopened all five originals and verified hashes. [References](../specs/school-duty/references.md) retains `C:\Users\abdelrhman\Desktop\المناوبة\New folder` paths and section mapping; D2 verifies fields/section order, D3 compares actual PDFs with `4.jpeg`/`5.jpeg` (roster `1.jpeg`). See [amendment](../specs/school-duty/dismissal-lateness.md) and C12–C16 / D-105.

This turn updates planning documentation only. D1 application/tests/schema remain unchanged; D2/D3 runtime implementation, notifications and new screens have not started. Original private images are preserved outside the repository. Verification consists of source inspection, hash comparison, scoped integration research and document/link consistency checks; no new runtime or live delivery claim.

### Guardian delivery clarification — C17 / D-106

Confirmed by the owner on 2026-10-13: guardian dashboard notice plus message in the existing Messages tab, from System rather than a person. D2 tasks include additive trusted system provenance (current messages require a human sender), content/deep link, independent delivery/read results, no duplicate surface alerts, prompt system dispatch and preserved human messaging rules. Planning only; no actual message was sent and D2 remains unstarted.

## D2 implementation handoff — 2026-10-13

Prepared the [English D2-only prompt](../specs/school-duty/prompts/d2-implementation.md) at the owner's request. Includes C1–C17 and relevant defaults, source-image mapping, saved-signature/report ownership, mobile student picker, shared dismissal-lateness facts, System-authored guardian dashboard/internal Messages, student history and existing manager statistics. Requires premium frontend composition and actual interaction/capture/refinement at 390px, 768px and 1440px, meaningful regression/security/concurrency/delivery tests and isolated SQL/synthetic guardian verification. Official PDFs/archive processing, real-parent test traffic, operational migration/deployment and D3 work are excluded.

Submitting the prompt authorizes D2 execution with a stop after D2; preparing it is documentation only. D1 remains delivered and unchanged; D2/D3 implementation remains unstarted. Checked prompt coverage and document links; no runtime checks or deliveries are claimed for this handoff.

## D2 delivery - 2026-10-13

The submitted D2 prompt explicitly authorized C1-C17 and relevant documented defaults. Implemented actual-teacher letter acknowledgement/server draft/review/saved-signature submission, immutable snapshots/history and local pending D3 archive intents. Delivered the premium enrolled-student picker, atomic shared dismissal-lateness fact/revisions/audit/intents, System-authored guardian dashboard/internal Messages with independent delivery/read/retry, existing manager statistics and actual student timeline/profile. Reviewed emergency reassignment retains earlier signed and unsigned history and requires replacement acknowledgement; D1 attendance/regeneration respects execution locks.

See [D2 verification and captures](../specs/school-duty/verification/d2.md): 361 affected backend checks (including 21 new D2 checks), 86 focused Angular checks, 20 actual isolated HTTP/JWT/SQL/hosted-worker checks and 11 rerun D1 HTTP regressions. Actual Angular rendered journeys/captures at 390/768/1440 use mocked synthetic API fixtures. No real-parent test traffic or external provider was used. Final owner visual review and physical mobile keyboard acceptance remain pending.

The additive D2 migration was tested only on fresh guarded LocalDB databases, with existing-database rejection before migration and sentinel preservation/reapplication. Operational data/settings/credentials/keys are unchanged. Existing baseline SQLite/broad Angular/production CSS failures remain documented and budgets unchanged. D3 official PDFs/archive processing, general delegation, waiting-register extensions, operational migration and production deployment remain outside this execution. **Stopped after D2.** Earlier planning/handoff sections above are historical records.
