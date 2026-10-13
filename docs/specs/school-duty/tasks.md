# Tasks — Three Implementation Phases

**Status:** D1 implementation delivered (2026-10-13); verification evidence and known baseline limitations are in `verification/d1.md`. Final owner visual acceptance is pending. D2/D3 remain unstarted.

## Before D1

- [x] Inspect all supplied sources and current integration code.
- [x] Confirm repeated date exchange, manual management, existing saved signature, manager-first operation, equal selected-teacher rotation and the two-document archive hierarchy.
- [x] Prepare English feature specification, UX, plan and tasks.
- [x] Owner authorized D1 and its documented relevant defaults through the implementation request on 2026-10-13; no reconfirmation required.
- [x] Refresh working-tree baseline and read current integration files before implementation; retain unrelated user work.

## D1 — Rotation, manager workspace and attendance integration

**Outcome:** The manager can publish a fair term rotation, exclude teachers, see today's duty, and manage automatic/manual date exchanges.

- [x] Add scoped duty plan/participants/closures/assignments/change history/reconciliation records, constraints, additive migrations and permission catalog.
- [x] Implement the explicit teacher-pool settings, deterministic equal rotation, preview/delta, publication, future regeneration and compensation retention across renewal.
- [x] Implement known absence/Excused rules, unknown-state display, next eligible future assignment selection, repeated exchange and explicit no-candidate/end-of-term exceptions.
- [x] Integrate durable attendance reconciliation in the attendance-save transaction; verify corrections, repeat saves and worker retry.
- [x] Implement manager manual replacement/exchange, compulsory reason, protected choices and revision conflict handling.
- [x] Add timetable/duty workspace tabs and manager RTL today/week/setup/details UI. Preserve all existing timetable features and teacher routes.
- [x] Implement schedule-only teacher projection and manager-only confidential day details.

- [x] Render and exercise D1 principal states at 390px, 768px and 1440px; inspect/refine captures, long names, spacing, wrapping, keyboard access and overflow.
- [ ] Final owner/client visual acceptance (deliberately pending review).

**Acceptance:** AC-01–08, AC-16–19 applicable to D1. Verify full cycles, closure/exclusion behavior, current effective queue, same-teacher skip, repeated absence, no eligible substitute, unknown attendance, corrected attendance, manual protection and concurrent two-date changes.

Use meaningful service tests for these decisions and real SQL integration tests for unique dates, school boundaries, atomic rollback, outbox persistence and concurrency. Regression-check attendance access/save, timetable personal view, publication, clones and inline swaps. Capture desktop/mobile manager screens and record owner visual feedback.

**Stop/report:** Update this checklist, the feature README, phase tracker and project change log. Do not claim report/signature/archive delivery at D1.

## D2 — Teacher execution, report and saved signatures

**Outcome:** The actual duty teacher acknowledges the letter and signs a complete mobile daily report using the existing saved handwritten signature.

- [ ] Add acknowledgement, report draft/retained revisions/lines and frozen signature/document snapshots with scoped relationships.
- [ ] Build narrow student/enrollment/class lookup without exposing unrelated Student Affairs information.
- [ ] Build teacher own-duty upcoming/history, letter preview, acknowledgement, draft persistence, late/violation cards, observations and last departure review.
- [ ] Capture saved signature on `اطلعت وأتعهد` and `توقيع وإرسال التقرير`; missing-signature settings/return flow.
- [ ] Validate author/current assignment, date/time, explicit empty categories and manual roster fallback; preserve drafts on failure/conflict.
- [ ] Implement idempotent submit, late-submit timestamps, manager-authorized retained correction and teacher re-signature.
- [ ] Commit document archive intents with the signed snapshot; external worker delivery belongs to D3.
- [ ] Handle same-day emergency reassignment after acknowledgement without moving signatures/authorship to the replacement teacher.

**Acceptance:** AC-07, AC-09–13, AC-16 and AC-20. Verify substitution signature ownership, later account signature change, two concurrent draft edits, stale assignment, seven-row overflow, no observations, departure validation, lost submit response and cross-school/student/report ID attempts.

Use workflow/backend tests plus focused desktop/mobile browser journeys with real supported RTL controls. Verify teacher deep links through the actual role/permission guard. Capture key states and record visual feedback.

**Stop/report:** Document locally submitted/signed status separately from unfinished external archive delivery. Update the same memory/checklists.

## D3 — Official PDFs, storage and final acceptance

**Outcome:** Letter acknowledgement and report submission produce retained official PDFs in the duty archive, with recoverable external delivery and protected access.

- [ ] Implement official roster, letter and report renderers with source field fidelity, original/substitute identities, frozen assets, school headers, last departure and multi-page tables.
- [ ] Add duty archive folder/source identities and protection across new and legacy listing/download/mutation boundaries.
- [ ] Provision the reviewed school/year/term/teacher/date hierarchy using stable identities and duplicate-name handling.
- [ ] Implement durable processor, ID reservation, leases/retry/reconciliation, immutable artifact/version links and per-school readiness.
- [ ] Add archive statuses, own/manager authenticated PDF reads and duty-manager retry; preserve submitted success during storage failure.
- [ ] Verify actual generated PDFs visually and compare archived byte hashes with generated output.
- [ ] Inject disconnected Drive, denied destination, uncertain upload response, SQL failure after upload, worker restart/race, renamed/moved external artifact and same-name teacher cases.
- [ ] Validate migrations on isolated SQL, meaningful relevant backend/frontend checks and existing timetable/attendance/storage regressions.
- [ ] Demonstrate real configured-school write/read/retry behavior when authorized and available; distinguish live results from fakes/mocks.
- [ ] Review manager and teacher UI with the owner, record acceptance/limits and publish activation/rollback instructions.

**Acceptance:** AC-09–10 and AC-13–20, plus complete end-to-end: publish → staff absence → date exchange → substitute acknowledgement → mobile report → submit → archive → protected download. No duplicate documents, overwritten signature, unprotected library access or fabricated completion.

**Stop/report:** Mark complete only when feature acceptance is demonstrated or explicitly record remaining external/owner acceptance. General feature assignment remains deferred.

## Later — Feature assignment, separately scoped

After duty acceptance, specify the manager's `توزيع الصلاحيات` tab, selectable feature capabilities, active school-user grants/revocation, audit and server enforcement. Start from the completed duty capability boundary. This is not an additional duty implementation phase and has no tasks silently started here.
