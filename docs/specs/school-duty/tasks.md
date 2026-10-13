# Tasks — Three Implementation Phases

**Status:** D1 implementation delivered (2026-10-13); verification evidence and known baseline limitations are in `verification/d1.md`. Final owner visual acceptance is pending. D2 implementation delivered; see `verification/d2.md`. D3 remains unstarted.

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

**Outcome:** The actual duty teacher acknowledges the letter, selects students through a premium mobile picker, confirms dismissal lateness with guardian notice/student history/manager statistics, and signs a complete report using the existing saved handwritten signature. The [amendment](dismissal-lateness.md) and [source references](references.md) are mandatory.

- [x] Record owner amendment C12–C16, inspect original image references and plan the mobile picker/shared lateness fact (documentation only, 2026-10-13).
- [x] Record C17: both guardian dashboard and existing Messages tab, authored by System; inspect current human-sender contracts and plan the additive seam (documentation only).
- [x] Prepare the English D2-only implementation handoff with the confirmed amendments, premium mobile frontend/visual gate, isolated verification and explicit D3 exclusion (preparation only).
- [x] Reopen `4.jpeg`/`5.jpeg` and map exact report sections/columns/signature positions before data/UI implementation; retain the source division in the mobile editor.

- [x] Add acknowledgement, report draft/retained revisions/lines and frozen signature/document snapshots with scoped relationships.
- [x] Build narrow whole-school student/enrollment/class lookup for the actual duty author, with server search, grade/class filters, grouped classrooms, pagination and stable duplicate-name identity; no typed-name fallback or contact/profile disclosure.
- [x] Implement the modern phone full-height/student-selection dialog and tablet/desktop layout from the amendment, with focus, scroll/keyboard handling, selected-student footer and preserved filter context.
- [x] Persist explicit dismissal-lateness confirmation, audit and durable guardian intent atomically; autosave does not notify. Validate effective recipients server-side, deduplicate/retry and show missing-guardian/delivery states. Do not assume external-channel delivery.
- [x] Create actual guardian-dashboard notice/content/deep link and one system-authored message in existing `/student-affairs/messages`; retain source-fact correlation, unread/receipt behavior and independent surface retry/read state without duplicate alerts.
- [x] Add trusted system-origin persistence/DTO/UI support to messaging; reject browser spoofing and avoid personal sender impersonation. Preserve human permissions/office-hour behavior and scoped participant isolation; system notices do not wait for human office hours.
- [x] Extend existing manager dashboard with dismissal-lateness counts and names/class drill-down, date/term/class filters and distinct-student versus incident totals.
- [x] Implement the real scoped dismissal-lateness student-record/timeline/profile projection; address the current empty timeline seam and preserve existing record authorization.
- [x] Link one stable fact/revision across report/dashboard/student history; implement audited correction/retraction and notice history without altering morning-delay or disciplinary metrics.
- [x] Build teacher own-duty upcoming/history, letter preview, acknowledgement, draft persistence, late/violation cards, observations and last departure review.
- [x] Capture saved signature on `اطلعت وأتعهد` and `توقيع وإرسال التقرير`; missing-signature settings/return flow.
- [x] Validate author/current assignment, selected student/enrollment/class, date/time and explicit empty categories; unavailable roster entries need resolution, not manual name entry. Preserve drafts on failure/conflict.
- [x] Implement idempotent submit, late-submit timestamps, manager-authorized retained correction and teacher re-signature.
- [x] Commit document archive intents with the signed snapshot; external worker delivery belongs to D3.
- [x] Handle same-day emergency reassignment after acknowledgement without moving signatures/authorship to the replacement teacher.

- [x] Complete isolated SQL/security/concurrency/dual-surface HTTP and affected backend/frontend regressions; retain real versus mocked evidence.
- [x] Capture/refine rendered principal workflows at 390px, 768px and 1440px, plus signature/conflict/correction/delivery states.
- [x] Update feature/project docs, phase tracker and decisions; stop after D2 without operational migration or D3 processing.
- [ ] Final owner/client visual acceptance and physical mobile keyboard review (pending owner review).

**Verification:** [D2 delivery report](verification/d2.md). 361 affected backend checks, 86 focused Angular checks, 20 actual isolated HTTP checks and retained mocked-API rendered browser evidence. No real-parent test traffic. Known unrelated baseline failures remain documented.

**Acceptance:** AC-07, AC-09–13, AC-16, AC-20–26 and AC-27's source mapping. Verify substitution signature ownership, later account signature change, two concurrent draft edits, stale assignment, seven-row overflow, no observations, departure validation, lost submit response and cross-school/student/report ID attempts. Verify concurrent lateness confirmation/atomic rollback/retry, recipient isolation/expiry/missing guardian, no repeated initial notices, consistent counts/history before report submission and after correction, and unchanged morning-delay counters. Inspect/capture picker search/filter/class browsing, long/duplicate names, virtual keyboard and notice states at 390/768/1440px; client visual acceptance remains pending.

Also verify AC-28–30: actual guardian-dashboard notice and Messages list/chat with `النظام` / `System`, one-surface failure/recovery, distinct unread/read state, correction linkage, denied system-origin forgery/other-recipient threads and unchanged human conversation behavior. Capture both guardian surfaces; distinguish real local persistence/browser results from mocks.

Use workflow/backend tests plus focused desktop/mobile browser journeys with real supported RTL controls. Verify teacher deep links through the actual role/permission guard. Capture key states and record visual feedback.

**Stop/report:** Document locally submitted/signed status separately from unfinished external archive delivery. Update the same memory/checklists.

## D3 — Official PDFs, storage and final acceptance

**Outcome:** Letter acknowledgement and report submission produce retained official PDFs in the duty archive, with recoverable external delivery and protected access.

- [ ] Implement official roster, letter and report renderers with source field fidelity, original/substitute identities, frozen assets, school headers, last departure and multi-page tables.
- [ ] Reopen originals in `C:\Users\abdelrhman\Desktop\المناوبة\New folder`; use [references.md](references.md) to verify exact daily-report division/section/column order (`4.jpeg`), letter (`5.jpeg`) and roster (`1.jpeg`). Retain actual source/output visual comparison evidence; no generic replacement report.
- [ ] Add duty archive folder/source identities and protection across new and legacy listing/download/mutation boundaries.
- [ ] Provision the reviewed school/year/term/teacher/date hierarchy using stable identities and duplicate-name handling.
- [ ] Implement durable processor, ID reservation, leases/retry/reconciliation, immutable artifact/version links and per-school readiness.
- [ ] Add archive statuses, own/manager authenticated PDF reads and duty-manager retry; preserve submitted success during storage failure.
- [ ] Verify actual generated PDFs visually and compare archived byte hashes with generated output.
- [ ] Inject disconnected Drive, denied destination, uncertain upload response, SQL failure after upload, worker restart/race, renamed/moved external artifact and same-name teacher cases.
- [ ] Validate migrations on isolated SQL, meaningful relevant backend/frontend checks and existing timetable/attendance/storage regressions.
- [ ] Demonstrate real configured-school write/read/retry behavior when authorized and available; distinguish live results from fakes/mocks.
- [ ] Review manager and teacher UI with the owner, record acceptance/limits and publish activation/rollback instructions.

**Acceptance:** AC-09–10, AC-13–20 and AC-27, plus complete end-to-end: publish → staff absence → date exchange → substitute acknowledgement → selected student → confirmed dismissal lateness → guardian notice/student history/manager statistics → report submit → archive → protected download. No duplicate documents/notices, overwritten signature, unprotected access or fabricated completion/delivery. Distinguish actual notification-channel results from mocks.

**Stop/report:** Mark complete only when feature acceptance is demonstrated or explicitly record remaining external/owner acceptance. General feature assignment remains deferred.

## Later — Feature assignment, separately scoped

After duty acceptance, specify the manager's `توزيع الصلاحيات` tab, selectable feature capabilities, active school-user grants/revocation, audit and server enforcement. Start from the completed duty capability boundary. This is not an additional duty implementation phase and has no tasks silently started here.
