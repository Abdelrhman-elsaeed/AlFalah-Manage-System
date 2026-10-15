# Tasks — Three Implementation Phases

**Status:** D1 implementation delivered (2026-10-13); verification evidence and known baseline limitations are in `verification/d1.md`. Final owner visual acceptance is pending. D2 implementation delivered; see `verification/d2.md`. D3 implementation, independent local verification and designated synthetic real Drive delivery/read/recovery are delivered. Owner visual/physical-device acceptance and operational activation remain pending. See `verification/d3.md` and `verification/workspace-amendment.md`.

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

- [x] Prepare the [English D3-only implementation handoff](prompts/d3-implementation.md), carrying D1/D2 contracts, exact PDF/source and premium UI gates, protected archive recovery, isolated verification and explicit provider/rollout boundaries (documentation only, 2026-10-13; runtime unstarted).
- [x] Implement official roster, letter and report renderers with source field fidelity, original/substitute identities, frozen assets, school headers, last departure and multi-page tables.
- [x] Reopen originals in `C:\Users\abdelrhman\Desktop\المناوبة\New folder`; use [references.md](references.md) to verify exact daily-report division/section/column order (`4.jpeg`), letter (`5.jpeg`) and roster (`1.jpeg`). Retain actual source/output visual comparison evidence; no generic replacement report.
- [x] Add duty archive folder/source identities and protection across new and legacy listing/download/mutation boundaries.
- [x] Provision the reviewed school/year/term/teacher/date hierarchy using stable identities and duplicate-name handling.
- [x] Implement durable processor, ID reservation, leases/retry/reconciliation, immutable artifact/version links and per-school readiness.
- [x] Add archive statuses, own/manager authenticated PDF reads and duty-manager retry; preserve submitted success during storage failure.
- [x] Verify actual generated PDFs visually and compare generated/local/downloaded/provider-double byte hashes. Real Drive byte equality remains part of the external gate below.
- [x] Inject disconnected Drive, denied destination, uncertain upload response, SQL failure after upload, worker restart/race, renamed/moved external artifact and same-name teacher cases.
- [x] Validate migrations on isolated SQL, meaningful relevant backend/frontend checks and existing timetable/attendance/storage regressions.
- [x] Demonstrate live external provider write/read/retry in explicitly designated D3_Test using fresh isolated SQL: 15 real provider checks, 10 protected HTTP checks, 6 real archive browser journeys/18 downloads. See [workspace amendment](verification/workspace-amendment.md).
- [ ] Final owner/client visual acceptance and physical-device keyboard review (pending).

- [x] Publish per-school readiness, safe activation and non-destructive rollback instructions; preserve snapshots/facts/artifacts/history.
- [x] Update feature/project documentation, phase tracker and D-110; stop after D3.
- [x] Subsequent owner-authorized local manual-test readiness: verified backup, nine pending migrations through D3, preservation/CHECKDB checks and API5264/Angular4200 startup. See [local operation](verification/local-manual-testing.md), D-111. External delivery and owner/physical acceptance remain pending.
- [x] Subsequent C18 owner UX correction: independent sidebar/page, legacy redirects/context, scoped/unsaved/signature guards and rendered teacher/manager navigation checks. See [navigation verification](verification/navigation.md), D-112.

**Verification:** [D3 delivery report](verification/d3.md): 21 D3 SQL, 96 focused Angular and 37 actual local HTTP checks; 15 PDF pages and rendered 390/768/1440 evidence. Expanded backend results, provider doubles versus real local communication, known baseline failures and external/owner/physical gates are explicit. No operational migration/activation or real Drive delivery is claimed.

**Acceptance:** AC-09–10, AC-13–20 and AC-27, plus complete end-to-end: publish → staff absence → date exchange → substitute acknowledgement → selected student → confirmed dismissal lateness → guardian notice/student history/manager statistics → report submit → archive → protected download. No duplicate documents/notices, overwritten signature, unprotected access or fabricated completion/delivery. Distinguish actual notification-channel results from mocks.

**Stop/report:** Mark complete only when feature acceptance is demonstrated or explicitly record remaining external/owner acceptance. General feature assignment remains deferred.

## Later — Feature assignment, separately scoped

After duty acceptance, specify the manager's `توزيع الصلاحيات` tab, selectable feature capabilities, active school-user grants/revocation, audit and server enforcement. Start from the completed duty capability boundary. This is not an additional duty implementation phase and has no tasks silently started here.

## C19 owner settings amendment ? 2026-10-14

- [x] Default active teachers, explicit permanent/date exclusions, quick exclusion/restoration and retained rotation order.
- [x] Server-derived timetable study days; remove weekday selection, retain operating/regeneration/closure dates; calendar readiness/stale-preview protection.
- [x] Match calendar input/trigger heights, Arabic/English labels and mobile action layout.
- [x] Verify isolated SQL, focused Angular, actual 390/768/1440 rendering and running local project; record evidence: 72 backend / 114 Angular, current UI + D1/D2/D3 journeys and preservation/readiness.
- [ ] Owner visual and physical-device acceptance (remains pending).

## C20–C23 owner workflow amendment — 2026-10-14

- [x] Ordered School Duty sidebar child pages, retained in-feature context and focused three-stage setup/execution.
- [x] Explicit centered error OK with language/focus/input retention; refine picker/PDF/permission recovery.
- [x] Full-width nearest-own-date/today alert, academic-term totals and ten-day teacher/date cards with all three filters.
- [x] Backend actual-author and frozen-school-time future guards; receipt replay after replacement, retained historical artifacts and overdue completion.
- [x] Automatic current-day/current-term edit without picker, archive-only browsing/history, archive rename, equal desktop categories and full-width review without document footer.
- [x] Duty-specific storage readiness/enable/pause; narrow local callback fix and owner-completed OAuth connection.
- [x] 75 backend / 138 Angular, current UI and D1/D2/D3 three-width regressions, real designated Drive and authenticated native-download/hash checks; source/PDF and preservation evidence.
- [x] Update specification, plan, UX, phase/readme change logs and decisions; no new migration, production deployment or later feature.
- [ ] Final owner/client visual approval and physical-device/OS keyboard/assistive-technology review.
- [ ] Owner-selected operational school activation/rollout (original flags remain off).

[Current acceptance evidence and safe activation/rollback](verification/workspace-amendment.md).

## C24 administrator amendment — 2026-10-14

- [x] Separate settings steps from the content panel; use individual visible buttons without a continuous green strip and refine all administrative duty pages at390/768/1440.
- [x] Paginate/search teacher management and rotation order, preserving exclusion/restoration/exchange actions; paginate fairness and change history.
- [x] Add a school-scoped SQL-paginated paired letter/report archive with filters, retained corrections, authenticated previews/downloads/retry and unavailable/denied states.
- [x] Add optional published school-local dismissal time with timetable default, preserved timing on protected assignments and immutable prior PDFs.
- [x] Show today's effective duty teacher and preceding seven dates in the general dashboard of an actual Duty.Manage administrator, independent of primary manager identity; preserve author signing and school isolation.
- [x] Fix reload warnings for untouched settings; protect changed settings/server saves/unsaved teacher drafts.
- [x] Verify78 backend checks (70 real SQL),141 Angular, scalable105-teacher/103-archive browser fixtures, ordinary teacher regression,14 actual protected HTTP reads and18 native downloads against retained real Drive artifacts. See [administrator evidence](verification/admin-amendment.md).
- [x] Keep local API5264/Angular4200 running, retain68 operational migrations and disabled original school activation; update D-116 and docs. Stop after School Duty.
- [ ] Final owner appearance and physical-device/OS keyboard acceptance remain pending.

- [x] C24 owner follow-up: move duty summary to the general dashboard page end after all header/tools/content; verify actual browser DOM order and390/768/1440, authorized officer/ordinary-teacher isolation.

## C25 — explicit School Duty delegation

- [x] Match dashboard width/margins at390/768/1440/1920 while retaining page-end placement.
- [x] Manager-only grouped school-account picker; grant/revoke, duplicate-name usernames, retained selection, explicit confirmation, paging and accessible RTL/English states.
- [x] Six administrative duty pages/dashboard/documents for a delegate, with actual-author signing and school scope preserved; no recursive delegation or general storage rights.
- [x] Reuse existing scoped memberships/fixed two-permission role/audit with serialized idempotent/versioned transactions, SQL uniqueness/rollback and live identity/revocation; no schema migration or automatic operational grants.
- [x]82 backend (74 SQL),146 Angular,16 actual isolated HTTP checks and rendered four-width delegation/workspace regression; latest local Release API5264/Angular4200 ready. [Evidence](verification/delegation-amendment.md), D-117.
- [ ] Owner final appearance and physical-device acceptance remain pending.

## C26 — Storage/archive follow-up

- [x] Diagnose retained pending presentation versus actual delivery; refresh the administrative archive without re-signing or extra notices.
- [x] Single Storage administration location with independent duty/visit on/off switches and truthful process/school states.
- [x] Restore owner-requested local Visit processing, preserving owner-selected school enablement, existing credentials/roots/grants and68 migrations.
- [x] Shared protected duty root in library setup; reserved-ID/lost-response recovery and idempotent reapplication on fresh isolated SQL.
- [x] Verified school-root branch navigation and switchable premium library map with accurate registered counts, search/collapse/fullscreen/English/mobile access.
- [x] Real isolated D3_Test delivery/recovery, frozen-byte equality, current authenticated browser downloads and measured before/after latency.
- [x] Appropriate backend/frontend/legacy regressions and actual rendered captures; document genuine baseline failures without relaxing budgets.
- [ ] Owner final visual acceptance and physical-device/keyboard acceptance. See [C26 evidence](../school-file-storage/verification/storage-duty-amendment.md).


## C27 / D-119 — Native school storage and confirmed removal (2026-10-15)

The owner's latest storage correction supersedes C26's feature shortcuts. Files has one School files entry and browses actual Drive folders/files inside the verified configured school root, including native/unindexed items. The committee map opens storage folders. The actual current school manager can review linked evidence/subtree impact, explicitly trash files/folders and recover recorded deletions. Rename/move remain protected for signed/system identities. Existing teacher/delegate scope remains enforced; duty delegation does not grant general storage administration.

Durable operations, expiring actor/school/root-bound confirmations, SQL locking, provider reconciliation and atomic evidence/legacy task updates preserve audit and review history. Removing a signed external copy never deletes or rewrites its snapshot/artifact/version identity/completed history; it records intentional deletion, retains local PDF access and excludes automatic recreation. Explicit recovery restores recorded descendants parent first and verifies names, parents and known hashes. Withdrawn evidence approvals are not automatically restored. Native managed signed reads reject altered bytes before streaming.

The owner explicitly authorized clearing the existing Al-Falah E2E Test School root, including D3_Test. All five original branches were trashed through real manager HTTP operations and the empty root verified. Fresh guarded isolated SQL then provisioned all four branches and delivered five real files through acknowledgement, signed report submission, visit approval, library upload and teacher upload. Sixteen live checks passed without messages. Test artifacts are cleaned through their retained isolated ledger, registered history is preserved and D3_Test remains trashed. No new migration, original credential/key/root/grant change, data reset or production deployment. Owner visual and physical-device acceptance remain pending. See the C27 verification report for current final recovery receipts, commands, exact test results and baseline limitations.

- [x] C27: native folder browsing and protected source/school/author isolation.
- [x] C27: explicit linked-impact deletion, durable uncertainty/SQL rollback/race recovery and retained signing evidence.
- [x] C27: actual empty-root setup and five real automatic/storage uploads in isolated SQL.
- [x] C27: responsive mocked presentation plus actual authenticated native previews/downloads; actual PDF pages inspected.
- [ ] Owner visual acceptance and physical phone/OS-keyboard review.
