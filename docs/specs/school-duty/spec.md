# Specification — School Dismissal Duty

**Status:** D1/D2 implemented under explicit owner authorization; C1–C17 confirmed. [D2 verification](verification/d2.md) covers signatures/report/mobile picker/shared lateness/System communication. D3 official PDFs/protected archive/recovery are implemented; [D3 verification](verification/d3.md) records independent evidence and pending external Drive/owner/physical acceptance.
**Feature label:** `المناوبة`. **Administrative owner in v1:** School Manager.

**Current navigation (C18):** the owner now requires School Duty as its own sidebar entry and `/school-duty` page. Earlier timetable-tab references below are historical UX; old duty URLs redirect with their retained context. Duty business scope and backend access are unchanged. See [navigation verification](verification/navigation.md).

## Purpose and boundaries

Assign one eligible teacher to whole-school dismissal supervision each operating day, distribute turns fairly, react to recorded teacher absence, capture the teacher's responsibility and daily observations, and retain signed official documents in school storage.

Duty is date-based dismissal supervision. Teaching lessons, lesson swaps, waiting periods and student morning attendance remain separate workflows. A duty swap changes duty dates only.

## Actors and visibility

| Actor | Access in the initial release |
|---|---|
| Actual active school manager | Configure rotation/exclusions, publish schedule, inspect all duty records, manually change assignments, resolve exceptions and retry archive operations in that school |
| Actual assigned teacher | See their upcoming duties, acknowledge their own responsibility letter, edit/submit their own daily report and read their own retained signed documents |
| Secretary | Continues to record staff attendance through existing authorization; receives no duty management solely from that role |
| Other staff / existing timetable or storage delegate | No duty management by inheritance; future feature assignment will explicitly grant it |

The duty roster can be visible to eligible school teachers using a schedule-only projection. Student observations, signatures and report content are available only to the actual report owner and authorized duty manager. Viewing the full timetable never grants access to reports. Backend ownership checks apply to every report, signature and PDF route.

## Functional requirements

### FR-01 — Workspace and context

Add `المناوبة` beside `الجدول الدراسي` inside the school timetable workspace. Keep `/timetable` usable for existing viewers and preserve personal views, editing, cloning, checkpoints, inline swaps and printing. Duty is school/year/term-scoped; opening a timetable clone does not create another duty schedule. A duty-only delegate must later be able to enter the duty tab without gaining timetable edit access.

### FR-02 — Rotation setup and publication

Duty settings contain an explicit teacher-pool selector. The manager chooses which active school teachers participate, removes excluded teachers without changing their lesson timetable, confirms the term/operating dates, sets an initial order and exclusions with effective dates/reasons, previews the distribution and publishes it. Published assignment changes notify affected teachers through existing in-app infrastructure if available, without adding a new channel dependency.

Confirmed fairness: each selected eligible teacher receives one turn in a cyclic ordered roster before the next cycle. Generation is deterministic for unchanged inputs. With unchanged eligibility and no exceptions, planned assignment counts differ by at most one. Display planned/completed/covered turns and pending compensation separately; absence does not count as completed duty. Teaching load does not weight distribution. Manual adjustments disclose their effect on fairness.

Regeneration affects explicitly selected future unlocked dates only, preserves original/actual identities, completed duty, signature snapshots and pending compensation, and previews its changes before publishing. Term renewal retains historical counts and outstanding obligations; it cannot silently cancel a pending exchange.

### FR-03 — Exclusions and closures

The manager can exclude a teacher from a date/range or the remainder of a term, and restore future eligibility. Exclusion from duty does not mean staff absence or exclusion from teaching. Holidays/closed days allocate no turn and do not advance the rotation. Exam days follow the explicit operating-day setting. Reject dates outside term/year bounds.

### FR-04 — Automatic absence exchange

Use existing school/user/date AttendanceRecord facts. Present is available; Absent/Excused is unavailable; no row is unknown. Saving or correcting attendance triggers durable reconciliation; page visits are not the only trigger.

For an unlocked duty day D with effective teacher A unavailable:

1. Search future effective duty assignments in date order within the term for the next eligible teacher B, excluding A, excluded/inactive users and users known unavailable on D. Validate published timetable membership/availability as applicable; lesson-period availability alone does not establish dismissal availability.
2. Preserve A as the original planned teacher on D and record B as the actual effective teacher, with absence reason and AttendanceRecord identity/version evidence.
3. Exchange A into B's selected future date E; retain E's original teacher and the compensation relationship. Persist both assignment changes and one exchange audit atomically.
4. If A is still absent on E, repeat against the current effective future schedule. Skip unusable candidates; do not always restart from the original printed roster.
5. Show both dates and the before/after teachers. Do not modify SchoolTimetableEntry or staff attendance.

Repeated handling of the same effective absence creates no duplicate exchange, obligation or notification. A changed attendance fact can prompt a new decision. If no candidate/future date exists, create an explicit manager exception. Never assign the same teacher twice on one day or invent completion credit.

Example: Sunday A=Mahmoud, Monday B=Ali, Tuesday C=Hassan. Mahmoud absent Sunday → Ali Sunday, Mahmoud Monday. Mahmoud absent Monday → Hassan Monday, Mahmoud Tuesday. Original roster, each exchange and actual execution remain reconstructable.

For attendance corrected to Present before acknowledgement/activity, automatically undo only the exact still-unmodified exchange pair if both dates remain unlocked. Otherwise surface manager review. A signed or completed day is never silently reassigned.

### FR-05 — Manual management

The duty manager can select a substitute, change a future date, exchange two future assignments, override an automatic choice or resolve unresolved compensation. The review shows both dates, availability/exclusion warnings, affected acknowledgement/report states and fairness impact. A reason is mandatory and the choice is protected from automatic overwrite.

A same-day emergency replacement after acknowledgement retains the original letter as historical, creates a new assignment revision and requires the replacement teacher's own acknowledgement. An existing draft is retained for inspection, not copied to another teacher as their authored report. After report submission, use a correction workflow rather than rewriting the completed assignment. An override cannot fake attendance, another person's signature or a completed report.

### FR-06 — Responsibility letter

Show school identity, academic year/term, day/date, actual teacher and manager identity, with the source obligation to supervise until the last student leaves. `اطلعت وأتعهد` is an explicit state-changing action. Merely fetching/previewing the letter does not sign it.

Use the authenticated teacher's saved handwritten signature. Freeze teacher display name, signature bytes, acknowledged-at timestamp, template version and assignment revision. A later account signature change cannot change the retained letter. Missing signature is an actionable setup state; draft/report progress remains available. Managers cannot acknowledge on behalf of teachers.

### FR-07 — Mobile daily report

The assigned teacher creates/updates a draft for that duty day and assignment revision. Include:

- Original planned teacher, actual teacher, `بدلاً عنه الأستاذ` when applicable, date/day, year/term, signatures and relevant notes.
- `أسماء الطلاب المتأخرين`: student, class, local time, action taken and optional notes.
- `أسماء الطلاب المخالفين`: student, class, local time, action taken and optional notes.
- General observations and required last-student departure time.

Use the mobile picker specified in [dismissal-lateness.md](dismissal-lateness.md): server search, grade/class filters, classroom-grouped results and active term enrollment with class auto-fill. Select existing StudentId/EnrollmentId for both student categories; typed student names/manual roster fallback are superseded by the owner amendment. An unavailable student produces a roster-resolution state. Duty grants minimal whole-school lookup, not student-profile, guardian or disciplinary access. Allow the same student in both categories; repeated dismissal-lateness entry reviews one existing fact.

The editor retains the exact source section order in [references.md](references.md): duty/original-substitute identity, late students, violating students, observations and handover/footer. Mobile cards stay within those sections. Confirmed lateness facts appear in student history and statistics before final report submission.

No observations is a valid report when the teacher explicitly confirms both categories have none. Do not treat zero lines as missing work automatically. Validate required fields and school-local times; reject departure before dismissal or before a recorded observation unless the duty manager records an explicit timing exception. Support overflow pages; do not enforce the paper's seven-line limit.

### FR-08 — Submission and correction

`توقيع وإرسال التقرير` validates current assignment, acknowledgement, signature and required last-departure time. Freeze all report fields and signature at submission. Commit submitted revision, audit and two distinct document archive intents as needed; Google failures do not roll back a submitted report.

Submission is idempotent. Further edits require an authorized correction with reason, preserved prior version and teacher re-signature. Review by management is visible independently and does not fabricate manager signatures or delay initial archive by default. Show overdue submission using real timestamps.

### FR-09 — Official output and archive

Provide an official term duty roster, responsibility letter and daily report. [references.md](references.md) is the mandatory layout contract: reopen the originals in `C:\Users\abdelrhman\Desktop\المناوبة\New folder`, especially `4.jpeg` and `5.jpeg`, during D2 mapping and D3 rendering. Preserve the exact report divisions, order, labels, columns and signature positions; compare actual generated PDFs visually. Letter acknowledgement queues its signed PDF; report submission queues its signed report PDF. The roster is an export; automatic retention of each roster publication is optional and outside the initial minimum.

Letter and report archive under the approved school root / `المناوبات` / academic year / term / actual teacher / duty date hierarchy, and retain original/substitute context inside the documents. Suggested filenames:

```text
خطاب المناوبة - {actual teacher} - {yyyy-MM-dd}.pdf
تقرير المناوبة - {actual teacher} - {yyyy-MM-dd}.pdf
```

Names are display values, not identity keys. Distinguish duplicate names and revisions through stable internal identities and version metadata. Prevent path characters from escaping the selected school boundary. Two PDFs are separate artifacts, not a single overwritten upload.

Archive status is independent: Pending, Processing, Completed, RetryScheduled or NeedsAttention, with actionable configuration/connection states. Retry uses the frozen snapshot and reserved provider identity and does not duplicate documents. Authorized teacher reads use the duty boundary even if the file resides outside the teacher's ordinary grant.

### FR-10 — Dismissal lateness, guardian notice and student history

The actual duty teacher confirms a selected student's dismissal-lateness fact. Commit durable intents for **guardian-dashboard notice and a system-authored message in the existing internal Messages tab**, with audit; process/retry independently of report signing and PDF storage. The visible sender is `النظام` / `System`, never the recording teacher or another person. Reuse effective guardian relationships without exposing contacts. Missing recipients/delivery failures are visible exceptions; retries do not duplicate either surface. The channel is confirmed; WhatsApp/SMS is outside this requirement. Delivery/read states are distinct per surface and grounded in actual evidence. Only trusted server code may create system messages; ordinary send requests cannot spoof origin or recipients.

The same committed fact supplies the existing manager dashboard's `تأخر الانصراف` count and student-name drill-down and the student record/timeline. Provide date/term/class filters; distinguish distinct students from incidents across a range. Preserve school authorization and audited corrections/retractions. Keep dismissal lateness separate from morning delay, lesson delay, absence and disciplinary penalties. See [dismissal-lateness.md](dismissal-lateness.md) for confirmed scope, proposed execution defaults, interface design and transaction/verification requirements.

## Acceptance scenarios

| ID | Given / when | Required result |
|---|---|---|
| AC-01 | Ten eligible teachers and twenty operating days | Two planned turns each; repeat generation is identical |
| AC-02 | A teacher excluded or a date closed | No assignment to that teacher/date; closure consumes no turn |
| AC-03 | Mahmoud absent, Ali next with future date E | Ali covers today; Mahmoud takes E; original names and one audit retained |
| AC-04 | Mahmoud remains absent on E | Repeat against current effective queue without duplicate compensation |
| AC-05 | Next candidate absent/excluded/inactive | Skip with reason; no usable candidate produces manager exception |
| AC-06 | Secretary saves the same attendance twice, or worker retries | One effective exchange and one notification set |
| AC-07 | Attendance corrected after a signed letter | No silent reassignment; manager review with historical letter retained |
| AC-08 | Manager manually changes the pair | Reason, before/after dates and protection retained; automation respects choice |
| AC-09 | Teacher acknowledges with saved signature, later edits account signature | Original letter remains byte/snapshot faithful to acknowledgement |
| AC-10 | Original teacher absent and substitute submits | Original absence is shown; substitute's own signature appears; absent teacher not signed |
| AC-11 | Teacher uses phone, enters over seven student lines, no violations | Accessible cards, explicit no-violations state, all rows in paginated PDF |
| AC-12 | No stored signature | Draft saves; acknowledgement/submission gives setup guidance; no fake signature |
| AC-13 | Double submit or lost response | Same committed report revision and archive intent returned |
| AC-14 | Drive succeeds but persistence/response fails | Recover exact reserved upload identity; no second logical artifact |
| AC-15 | Drive disconnected or disabled | Report stays submitted, PDF can be generated from frozen data, archive visibly awaits configuration |
| AC-16 | Another teacher/school forges report, student or file ID | Backend denies access with no report/signature/provider metadata leak |
| AC-17 | Timetable clone/regeneration/lesson swap | Existing lesson workflows pass regression; no duplicate duty schedule |
| AC-18 | Concurrent manual change and automatic reconciliation | One valid revision wins; loser gets conflict/retries current state; no half-exchange |
| AC-19 | Compensation has no remaining date in term | Unresolved obligation is visible and retained through renewal |
| AC-20 | Signed report corrected | Prior report/bytes retained; correction reason and re-signature link the new revision |
| AC-21 | Teacher searches/selects students on a phone | Modern scoped picker with grade/class filters, classroom groups, pagination, duplicate-name disambiguation and class auto-fill; no free-text student identity |
| AC-22 | Teacher confirms lateness, then retries or submits the report | One active student/date fact and one initial notice per eligible recipient; durable intent independent of final signing/archive |
| AC-23 | Guardian absent/inactive/unlinked or delivery unavailable | Retain fact with actionable status/retry; no wrong recipient, invented delivery or leaked contacts |
| AC-24 | Manager opens statistics before report submission | Correct count plus names/class/date drill-down; no morning-delay count changes |
| AC-25 | Student record opened; fact later corrected/retracted | Scoped dismissal-lateness history and current aggregates agree with report/dashboard; retained audit and notice history |
| AC-26 | Duplicate names or forged other-school student/class/guardian IDs | Stable identity selection and server validation; no cross-school disclosure |
| AC-27 | D3 renders actual report/letter/roster | Sources reopened; exact report/table order and fields, correct signatures, long-name/overflow handling and last departure verified visually |
| AC-28 | Guardian views dashboard and Messages after confirmed lateness | Actual dashboard notice with student/date and deep link, plus one message labeled `النظام` / `System`; no personal sender or manual Send action |
| AC-29 | Only one surface succeeds, or guardian reads one surface | Retry only missing delivery; no duplicate alert/message and no invented shared read status; preserve correction history on both surfaces |
| AC-30 | Browser attempts system origin or wrong-recipient thread; delivery occurs outside human office hours | Server rejects spoofing/leaks; trusted system notice delivers promptly without changing existing human conversation rules |

## Completion criteria

The feature is complete after D1–D3 pass business/security/failure verification, official PDFs are visually checked against supplied forms, mobile/desktop RTL flows meet `ux.md`, and a configured school demonstrates the actual archive write/read/retry path. Record unavailable live validation explicitly; mocks alone do not establish live storage acceptance.

## D2 acceptance record

Delivered D2 covers the execution portions of AC-07, AC-09-13, AC-16, AC-20-26 and AC-28-30, with the source-data mapping portion of AC-27. Read [verification/d2.md](verification/d2.md) for real SQL/HTTP evidence, independently mocked browser presentation and baseline limitations. AC-11's actual paginated PDF, AC-14-15's external archive/PDF behavior and AC-27's rendered official-source comparison remain D3. Local submission does not claim PDF/archive completion or final client visual acceptance. Operational migration/real-parent testing/deployment were not performed.

## D3 implemented contract and acceptance evidence - 2026-10-13

The submitted D3 request authorizes C1-C17 and the approved hierarchy. All official form sections, columns and signature ownership are implemented from the inspected originals, with actual Arabic shaping, licensed embedded fonts and composed continuation pages. Signed letters/reports use immutable D2 snapshots and actual author's frozen signature; unavailable historical branding remains unavailable. Frozen PDF bytes/hash/provenance and renderer version survive profile/fact changes and retained correction/re-signature. Roster export freezes a plan revision and creates no archive obligation.

Genuine protected duty folder/source identities and scoped ledger versions implement the two-file hierarchy. Durable operation identity, reserved provider IDs, leases, bounded retry/reconciliation and verified completion preserve uncertain writes and SQL rollback. Readiness disabled/disconnected preserves local signed access; managers may retry the missing result without storage administration or repeated facts/notices. Actual-author/current-school or authorized-manager checks apply to every revision; emergency replacement keeps displaced signatures in history.

[D3 verification](verification/d3.md) records AC-09-10, AC-13-20 and AC-27 evidence, the complete publication/absence/substitute/lateness/internal-message/report/document journey, source-to-output comparisons and isolation/recovery checks. Independent local implementation acceptance is delivered; actual external Drive, final owner visual and physical-device acceptance remain pending. No later feature or operational rollout is authorized by these results.

## C19 settings amendment (2026-10-14)

All active school teachers participate by default in new configuration. Managers record permanent (this plan until restored) or dated exclusions, retain rotation ordering, and review before publishing. Study weekdays derive from the school/year/semester timetable calendar, with no independent weekday picker or guessed fallback; operating/regeneration ranges and closures remain. Historical selected pools convert omissions to explicit exclusions only when reviewed; new active teachers join the next review. Past/manual/exchanged/locked execution and signed artifacts remain protected. See C19, D-113 and [verification](verification/settings-amendment.md).

## C20–C23 current workflow and acceptance amendment (2026-10-14)

Independent ordered School Duty pages replace the combined timetable workspace. Main edit selects school today/current term automatically; archive alone selects historical duty/date/term. Schedule uses a full-width nearest-own-duty hero, prominent current-day alert, academic-term totals and ten-day teacher/date card pages with own/all/teacher filters. Execution/review fills available width, late/violating desktop categories share equal columns, and PDF cards live only in `أرشيف المناوبات`. Explicit centered error OK retains input and focus. Actual-author ownership and frozen-school-time future rejection apply on the backend; past overdue completion and reasoned corrections preserve retained revisions.

Manager storage administration offers separately scoped readiness/enable/pause, with actual root probing. Owner completed OAuth reconnection and designated D3_Test. Real delivery/uncertainty/SQL-rollback recovery and protected local/archived hash-identical reads passed on fresh isolated SQL in that destination. Original school flags remain off, no later feature/production rollout occurred, and final owner visual/physical acceptance stays pending. [Reproducible evidence](verification/workspace-amendment.md), C20–C23, D-114–115 supersede earlier UI/provider-gate statements for current implementation.

## C24 administrator amendment — 2026-10-14

Implemented the owner's administrator-only refinement; the approved ordinary teacher workspace stays intact. Setup navigation is outside its content card, with separate visible buttons and a highlighted active button; the continuous green bar was explicitly rejected in the follow-up. Teacher management and rotation order show ten entries per page with search and existing permanent/date exclusion, restore and move actions. Fairness and change-history pages also show ten entries, avoiding an unbounded administrative page. Calendar and preview layouts use the available width, bounded lists and readable RTL spacing. Refreshing untouched setup/search/stage navigation no longer triggers a browser unsaved-changes prompt; actual changed settings, pending operations and unsaved teacher drafts retain protection.

The administrative archive uses a SQL-paginated index with ten teacher/date/revision groups per page: responsibility letter on the RTL right, daily report on the left, independent truthful availability/delivery states, authenticated preview/download/retry and retained history. Teacher/date/term filters are school scoped; changing filters resets the page. Unsigned future duties are omitted by default, while retained signed future artifacts remain readable. Explicit date filters can inspect future assignments. Rows select the newest letter/report for their assignment/actual-author/revision; earlier correction documents remain in history. The query loads at most twenty latest documents/artifacts for a page rather than all signed history. Listing uses the last committed verification state, without a provider request for every row; existing archive reconciliation and explicit archived downloads verify the provider. Access denial clears historical/preview data.

DutySettings now has nullable DismissalAt in its existing versioned JSON. Null retains the final timetable-period default. An explicit school-local time is reviewed/published using the existing revision/digest lock and regeneration range. Append-only SettingsTimingPreserved events freeze earlier base times for past, signed/locked, manual/exchanged or range-protected assignments when the plan setting or calendar revision changes. An explicit daily timing exception still wins. Timing preservation is not an exception that bypasses lateness validation. Existing frozen signed snapshots/PDF bytes are not rewritten; later reports freeze their actual resolved time. This is additive JSON/history behavior, with no schema migration.

The general account dashboard, outside the School Duty workspace, shows today's effective teacher and the preceding seven calendar dates with an explicit range. It is shown for an active school account with live database Duty.Manage, regardless of primary manager identity/role label. The backend checks active user/current-school membership and live database permission before and after reads. Signing still requires the actual effective teacher; teacher-only viewers cannot read the administrative archive/dashboard. Existing permission administration is reused; no grants, delegation UI or general storage rights are created. The dashboard refreshes while visible and clears stale data when school context changes or access is revoked.

[Actual verification, principal captures, reproducible commands and remaining gates](verification/admin-amendment.md), D-116. No operational migration, school activation, new provider write, credential/root/grant change or later feature. Rollback disables new duty processing while retaining all facts/snapshots/artifacts/history. Owner final appearance and physical-device/keyboard acceptance remain pending.


C24 owner placement follow-up (2026-10-14): the duty summary belongs at the **end of the general account dashboard**, after its header, tools and entire routed content. The shell renders it after router-outlet/routed component with top spacing, preserving all existing duty scope/role checks. Actual browser checks passed at390/768/1440 for the manager, plus the authorized officer and ordinary-teacher isolation. [Placement results](verification/dashboard-footer-screens/browser-results.json). No backend, migration, configuration or provider write change.

## C25 — School Duty administrator delegation and dashboard width (2026-10-14)

The owner explicitly authorizes **School Duty only** delegation, superseding its earlier deferral for this feature. General feature delegation remains outside scope. The summary stays at the general dashboard page end and now matches the existing dashboard's1440px maximum width and centered margins, including1920px screens.

A manager-only /school-duty/permissions page groups account selection by role, prioritizing moderator grouping while allowing every active role in the current school. Existing usernames disambiguate duplicate display names. The manager grants/revokes duty administration; delegated accounts receive all six administrative duty pages, actual signed-document/archive access and their general dashboard summary. Only actual school managers with active current-school membership and live Duty.Manage may delegate. Delegates cannot grant/revoke others, administer general storage or sign for a different assigned teacher. Role-inherited management is shown separately and cannot be revoked through this page. Inactive/out-of-school targets and stale mutations are rejected; explicit revoke confirmation, failures/selection retention, ten-row admin paging and audit history are provided.

Reuse existing UserSchoolRoles/RolePermissions/AuditLogs. A fixed SchoolDutyAdministrator role has exactly Duty.View and Duty.Manage, provisioned transactionally only on the first explicit authorized grant, protected by a shared role lock. No global UserRoles assignment/base role change, schema/migration, credential/root/grant change or automatic operational grant occurs. Per-school serialized transactions plus the existing unique membership index prevent duplicate grants/audits; the retained UpdatedAt version prevents stale regrant/revoke overwrites. Audit and membership commit/rollback together. A grant alone is insufficient after ordinary school membership/user/role deactivation. Auth/me derives Duty permissions from live active school membership without changing the account's primary role or other permission families; page refresh exposes the new sidebar/dashboard. API authorization/revocation is immediate even for an old token.

[Verification](verification/delegation-amendment.md):82 backend (74 real SQL),146 Angular,16 actual isolated authenticated HTTP checks, actual UI at390/768/1440/1920 and existing teacher/workspace regression. Backend remains Controller → Application service → scoped Infrastructure repository. No general permission-framework refactor, waiting-register or external messaging feature. Owner appearance/physical acceptance remains pending. Stop after this authorized School Duty amendment.

## C26 — Archive controls, school-root navigation and library presentation (2026-10-14)

The owner explicitly requested this School Duty/storage follow-up. The two existing local letters had already reached verified archive completion; the manager's SQL-paged table retained its old presentation because it had no refresh timer. It now refreshes while visible, excluding busy preview/history actions and stopping after denied access. Enablement is presented **only in Storage administration**, with independent on/off switches for duty and visits. Duty-only delegates retain document/retry access without acquiring general storage administration. Signing/submission remains independent of external delivery.

The running local process had `SchoolFileStorage:ArchiveProcessingEnabled=false` from isolated D3 verification, despite the owner's per-school visit flag being true. This was a process stop, not an OAuth re-login side effect. The process now explicitly enables both archive processors, preserving owner-selected school flags: school18 duty/visits ON, school1 OFF. Startup initialization stays false and the operational schema stays at68 migrations. No credential/key/root/grant change or new migration.

Library setup includes the protected duty root through the same `DutyArchiveDriveService.EnsureRootAsync` identity, session lock, provider ID reservation and missing/moved-folder rules used by the worker. It never detaches/rebinds signed history through generic recovery consent. Year/term/actual-author/date descendants remain document-driven; no guessed year or teacher directories. Reapplication reuses the root and never enables either archive implicitly.

The Files entry shows School Library, Teacher Files, School Duty and Visit Archive under the verified authorized root. Each enters its existing scoped workflow. Duty and visit documents retain their distinct authorization/mutation boundaries; arbitrary Drive-only folders and the designated `D3_Test` verification area are not adopted into the operational library or counted as evidence. Deep library/review/file/year links are preserved; overview Library shortcuts now target the actual library folder directly.

A switchable committee presentation includes root branches, colored connected library sections, nested folder links, retained-ledger file counts including deeper descendants, search, collapse, keyboard focus, English labels, reduced motion and native fullscreen. Canonical template sections can be described as domains/standards; arbitrary existing folders are described as library sections/subfolders. No invented approvals/readiness percentages or sample personal data. The existing teacher storage/duty workspace is retained.

Provider metadata returned by a live list is reused only within the scoped request. Mutation outcomes, including lost responses, invalidate the changed ID. New request/verification boundaries reset the cache; connectivity checks stay fresh. An internal verified-metadata streaming seam removes redundant content metadata reads while legacy adapters keep their safe default. Scoped SQL grouping counts library files, excludes both archive source kinds and bounds tree depth/size. All new API responses retain ApiResponse<T>, thin controllers, Application authorization and Infrastructure queries.

See [storage/archive verification](../school-file-storage/verification/storage-duty-amendment.md), current read-only performance/readiness, bounded real Drive recovery and actual/synthetic browser captures. Owner appearance and physical-device review remain pending. No production deployment, destructive history removal, waiting-register feature, external messaging or general feature delegation.


## C27 / D-119 — Native school storage and confirmed removal (2026-10-15)

The owner's latest storage correction supersedes C26's feature shortcuts. Files has one School files entry and browses actual Drive folders/files inside the verified configured school root, including native/unindexed items. The committee map opens storage folders. The actual current school manager can review linked evidence/subtree impact, explicitly trash files/folders and recover recorded deletions. Rename/move remain protected for signed/system identities. Existing teacher/delegate scope remains enforced; duty delegation does not grant general storage administration.

Durable operations, expiring actor/school/root-bound confirmations, SQL locking, provider reconciliation and atomic evidence/legacy task updates preserve audit and review history. Removing a signed external copy never deletes or rewrites its snapshot/artifact/version identity/completed history; it records intentional deletion, retains local PDF access and excludes automatic recreation. Explicit recovery restores recorded descendants parent first and verifies names, parents and known hashes. Withdrawn evidence approvals are not automatically restored. Native managed signed reads reject altered bytes before streaming.

The owner explicitly authorized clearing the existing Al-Falah E2E Test School root, including D3_Test. All five original branches were trashed through real manager HTTP operations and the empty root verified. Fresh guarded isolated SQL then provisioned all four branches and delivered five real files through acknowledgement, signed report submission, visit approval, library upload and teacher upload. Sixteen live checks passed without messages. Test artifacts are cleaned through their retained isolated ledger, registered history is preserved and D3_Test remains trashed. No new migration, original credential/key/root/grant change, data reset or production deployment. Owner visual and physical-device acceptance remain pending. See the C27 verification report for current final recovery receipts, commands, exact test results and baseline limitations.


## C28 — Intentional unsubmitted-report departure (2026-10-15)

An editable acknowledged report worked on in the current session must explain its unsubmitted state before application-controlled departure, even after autosave. Save dirty content first; failed/conflicted saves retain input and block departure. A centered Arabic/English notice gives completion steps, Continue report and OK-to-leave-as-draft. No notice on initial/view-only load, autosave itself, manager/historical inspection or after submission. Never sign/submit/notify implicitly. Native browser closing retains its existing unsaved-cache protection; it cannot host a custom awaited HTML dialog. [Diagnosis and verification](verification/draft-exit.md).
