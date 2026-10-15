# Clarifications and Decision Register

**Updated:** 2026-10-13. The owner explicitly authorized D1-D3 through the submitted phase prompts, recorded C1-C17 and relevant defaults. D3 implementation/independent verification is delivered; external Drive and owner/physical acceptance remain pending. Earlier phase-only statements below are historical.

## Confirmed decisions

| ID | Decision | Authority |
|---|---|---|
| C1 | One teacher supervises all school students at dismissal until the last student leaves | Client conversation |
| C2 | Exclude selected teachers from duty rotation | Client conversation |
| C3 | When A is absent, the next eligible scheduled teacher B covers A; A takes B's next scheduled duty date. Repeat if A remains absent | Owner answer on 2026-10-13 |
| C4 | The user authorized to manage duty can manually change assignment/exchange behavior | Owner answer on 2026-10-13 |
| C5 | `اطلعت وأتعهد` draws the teacher's existing saved handwritten signature into the responsibility letter | Owner answer on 2026-10-13 |
| C6 | The school manager alone manages duty initially; the assigned teacher completes their own workflow | Owner answer on 2026-10-13 |
| C7 | General feature-to-user permission distribution comes after duty is finished | Owner answer on 2026-10-13 |
| C8 | Archive the signed documents in duty storage with the actual teacher and duty date identified | Client conversation and owner request |
| C9 | Write the Spec Kit in English, communicate in Arabic, use a separate docs folder and few phases; agree before implementation | Owner request |
| C10 | Equal cyclic rotation, without teaching-load weights, over the teacher pool explicitly selected in duty settings. Managers can remove teachers from that pool | Owner follow-up answer on 2026-10-13 |
| C11 | Archive under school root / `المناوبات` / academic year / term / actual teacher / duty date, with separate responsibility-letter and daily-report PDFs | Owner follow-up answer on 2026-10-13 |
| C12 | Student identities are selected through a modern mobile dialog with search, filters and classroom grouping; no typed student-name fallback | Owner amendment on 2026-10-13 |
| C13 | Dismissal lateness triggers a notice to the selected student's guardian | Owner amendment on 2026-10-13 |
| C14 | Existing manager dashboard statistics show dismissal-lateness counts and which students were late | Owner amendment on 2026-10-13 |
| C15 | Dismissal lateness is recorded in the student's history | Owner amendment on 2026-10-13 |
| C16 | Official report divisions match the supplied images exactly; retain their directory as a mandatory reference and review during D3 | Owner amendment on 2026-10-13 |
| C17 | Dismissal-lateness notice appears in the guardian dashboard **and** as a message in the existing internal Messages tab, authored by the system rather than a person | Owner clarification on 2026-10-13 |
| C18 | School Duty has its own top-level sidebar entry and independent page, not an internal lesson-timetable tab. Preserve old duty links and existing school/author/manager scope | Owner navigation correction and screenshot on 2026-10-14 |

All five submitted clarification questions are answered. The former fairness and archive-path proposals are confirmed as C10 and C11; no answer to those questions remains outstanding.

## Proposed defaults for plan review

| ID | Proposal | Reason / consequence |
|---|---|---|
| P2 | Schedule operating weekdays from selected timetable timing, within AcademicTerm dates, with manager-entered holiday/exam closure ranges | The paper has holiday/exam rows. Exam days remain operating unless explicitly closed; no automatic school-calendar import is assumed |
| P4 | Teacher report submission explicitly applies the saved handwritten signature again. If missing, save the draft and direct the teacher to existing signature settings; signed submission/acknowledgement stays unavailable | Reuses the confirmed signature mechanism without inventing a signature |
| P5 | Submission is final and does not wait for manager approval; manager reviews the following day. Corrections create a retained revision with reason and teacher re-signature | Paper specifies handover, not an approval state; preserves automatic archival and historical fidelity |
| P6 (partly superseded by C13–C15) | Dismissal lateness now creates a shared student fact/history item, guardian notice and manager statistics. Violating-student observations still do not automatically create disciplinary incidents/penalties | Owner amendment expands dismissal-lateness integration only; no morning-delay or conduct-policy expansion |
| P7 | Known Absent and Excused staff are unavailable. Missing attendance means unknown; show this state. Automatic next-in-queue cover may remain provisional until attendance is saved, with a final check before acknowledgement | The current model has no attendance fact until a sheet is saved; unknown must not be presented as confirmed Present |
| P8 | An emergency manual reassignment may proceed for a known-absent/excluded teacher only through an explicit exception warning and mandatory reason. The manager cannot sign on that teacher's behalf or falsify attendance | Supports the owner's manual control with transparent accountability |

The 2026-10-13 implementation request supplies the required plan agreement for D1. P2, P7, P8 and D1 correction/protection/compensation policies are authorized defaults; do not ask for reconfirmation. P4–P6 describe D2 behavior and remain unimplemented; P6 must be read with C13–C15, not as a prohibition on student-history/guardian integration. If a default changes, update the corresponding spec, contracts and tasks together.

## D2 amendment defaults and channel decision

See [dismissal-lateness.md](dismissal-lateness.md) and [references.md](references.md). Confirmed requirements C12–C17 need no reconfirmation. Proposed engineering defaults are explicit confirmation before notification (independent of report submission), one active lateness fact per student/duty date, durable deduplication/retry, established effective guardian-recipient policy and retained corrections. This amendment is planned; it does not start D2/D3 implementation.

**Channel resolved by C17:** both guardian-dashboard notice and existing internal Messages, with visible/authenticated system provenance (`النظام` / `System`). No external messaging provider is required. Current human-sender contracts need a narrow additive system-origin path; do not impersonate a teacher or permit browsers to forge it. Independent delivery/read states, deduplication and correction links are specified in the amendment. There is still no approved arbitrary dismissal-delay grace period.

The owner requested an English next-phase prompt. [D2 implementation prompt](prompts/d2-implementation.md) is prepared; submitting it authorizes D2 and its documented relevant defaults without reconfirming C1–C17. Prompt preparation itself does not start implementation. D3 remains excluded, with stop/report after D2.

## D1 implementation decisions

- Read operating weekdays and timezone from the published non-clone timetable timing when available. With no published timing, explicitly present Sunday–Thursday and Africa/Cairo as editable setup defaults; do not fabricate dismissal availability from free lesson periods.
- Keep selected teachers, effective exclusion ranges and operating dates in validated typed settings JSON. Published settings snapshots are append-only history; participant identity/order and closure ranges also have normalized scoped records.
- Regeneration uses an explicit future range. Preserve past, locked, protected and exchange-related assignments; a conflicting new closure/exclusion produces manager review. Cancel ordinary removed operating dates without deleting original identities/history.
- At term end, an eligible cover with no usable future date creates an unresolved obligation. It appears in later-term manager context and can be resolved to a real future assignment with reason/review/protection. Renewal never erases it.
- D1 completion counts remain zero because execution reports belong to D2. Past dates and absence do not confer completion credit.
- Teacher identity/history must survive later profile transfers. SQL validates school/profile/user scope when selecting participants or creating/changing an effective assignment, rather than binding historical records to a profile's mutable current SchoolId. Ineligible selected teachers remain visible for explicit pool removal; they cannot be selected as new replacements.

## Edge policies included for review

- No eligible teacher: visible `تحتاج تدخل المدير`; never choose an absent/excluded teacher silently.
- Cover candidate with no future duty date in the current term: mark compensation unresolved; manager resolves a concrete future date or carries an explicit obligation forward. Do not fabricate an out-of-term assignment.
- Attendance corrected after acknowledgement or report activity: preserve the signed snapshot; flag manager review instead of silently transferring the signed document.
- Manual assignment/exchange has a persistent protection marker, so automatic reconciliation does not immediately undo it. Removing protection is a separate audited action.
- Exclusions and eligibility changes affect future unlocked days from an explicit effective date; history survives.
- Late submission is allowed with an “overdue” indicator; never backdate the acknowledgement/submission timestamp to the duty date.

## D2 implemented decision record

The submitted D2 prompt explicitly authorized C1-C17 and their relevant defaults without reconfirmation. D2 is implemented; [verification](verification/d2.md) records actual scope and evidence. Historical D1 completion-zero and amendment/prompt-preparation statements above apply to their original phase, not the current D2 result: completion credit now requires a signed report, and selected-student facts/System communication are delivered.

D-108 records the retained typed draft JSON/generic immutable Letter/Report documents, stable lateness/revision records, per-surface results, nullable constrained System sender, scoped SQL triggers and emergency draft history. Embedded/local logo bytes are frozen; remote/unavailable logo state is retained explicitly. No arbitrary lateness grace period, fake departure, external provider, new student-record grant or D3 processor was introduced. Operational data/configuration remain unchanged; owner visual acceptance is pending and work stops after D2.

## D3 prompt preparation - 2026-10-13

The owner requested the next English implementation prompt. [D3 handoff](prompts/d3-implementation.md) applies C1-C17 and relevant documented defaults to official PDFs, protected archive delivery/recovery and final verification, preserving D2's actual immutable contracts and unavailable-branding state. No new business decision or runtime authorization is inferred from preparing the prompt. Submitting it authorizes its scoped implementation; live provider checks additionally require an explicitly designated isolated synthetic destination. Operational migration/deployment, unrelated provider writes, real-parent test traffic and later features remain excluded. D3 is unstarted and final owner acceptance remains pending. See D-109.

## D3 authorization and applied defaults - 2026-10-13

The submitted D3 implementation request explicitly authorizes recorded C1-C17, the dismissal-lateness amendment and approved archive hierarchy. No recorded decision was reconfirmed and no new owner decision was invented. Earlier D1/D2-only authorization and prompt-preparation statements remain historical.

Implemented defaults keep global duty external processing and every school's duty enablement false until safe activation; local PDF preparation/access remains independent. Signed historical branding state and school-local timing are retained. Corrections require D2 manager reason plus actual teacher re-signature and retain prior bytes. Only letter/report have archive obligations; roster export binds a plan revision. Fake-provider verification is labelled TestVerified and cannot satisfy actual archive acceptance.

[D3 verification](verification/d3.md) records delivered independent checks, safe enablement/rollback and unresolved boundaries: an explicitly designated synthetic external destination/connection, owner visual review and physical-device keyboard review. General provider access alone does not designate a school archive destination. No operational migration/deployment, root/grant/credential change or later feature occurred.

## Subsequent local operational authorization - 2026-10-14

The owner explicitly authorized applying necessary migrations and running the project for manual testing. The configured local Development database migration/startup is completed and verified; this supersedes the earlier original-database restriction for that local scope. It does not designate an external test destination or authorize production/external archive activation. Recorded C1-C17 remain unchanged. See [local operation](verification/local-manual-testing.md), D-111; owner visual/physical acceptance remains pending.

## C18 navigation correction - 2026-10-14

The owner explicitly requested an independent sidebar entry/page after seeing duty inside the timetable. Canonical entry is `/school-duty`; lesson timetable stays `/timetable`, while retained `?tab=duty` links redirect with date/execution/fragment context. This supersedes earlier UI placement, not C1-C17 business rules or backend scope. Existing duty capabilities are reused; no general delegation, migration or external archive activation is added. [Evidence](verification/navigation.md), D-112.
## C19 settings amendment — 2026-10-14

The owner requests all active school teachers included by default, explicit permanent or dated exclusions with quick actions, timetable-derived study weekdays without manual duty weekday selection, and equal-height calendar inputs/buttons. This supersedes C10's opt-in pool UX and the editable fallback weekday default. Permanent means the current duty plan until restored; it does not alter attendance, lessons, historical assignments or other terms. Existing selected pools remain exclusions when reviewed, not silently republished. New teachers join the next reviewed configuration. Dates, regeneration bounds, closures, rotation order, preview/publication and execution protections remain. Study days use the current valid published timetable revision, otherwise the latest configured year/semester setup's active attached timing revision; without either, setup explains the required timetable calendar. A timing change invalidates an earlier preview. See D-113 and [verification](verification/settings-amendment.md).
## C20/C21 workflow organization and error feedback — 2026-10-14

The owner requests a main School Duty sidebar category with ordered separate child pages, replacing the crowded combined workspace. Managers get setup, schedule, execution, documents/archive, fairness/compensation and change history; teachers get schedule, own execution and documents. Setup separates teachers/exclusions, timetable calendar and publication preview. Execution separates letter, daily report and concise final review; signed PDF cards/history move to documents. Existing actual-author/school/manager checks and immutable contracts remain. One retained parent context preserves in-feature settings/picker/draft context and old links retain date/execution identity.

Errors in the duty workflow must appear in a centered modal requiring an explicit OK acknowledgement, with readable localized text, keyboard focus and retained input. Persistent informational archive states remain status cards rather than errors. This supersedes earlier primary inline/toast error presentation for duty; unrelated product feedback is unchanged. No backend/schema/provider activation or later feature is authorized by this UI amendment. D-114; implemented and verified in [workspace amendment](verification/workspace-amendment.md).

## C22 schedule, ownership and archive activation — 2026-10-14

Owner requests a full-width nearest-own-duty hero with actual date and prominent school-today duty/no-duty alert; academic-term naming; teacher/date cards ten days per page; own/all/specific-teacher filters; `تحرير المناوبة` naming; equal desktop late/violation columns; functional letter preview/download; duty-specific manager storage enable/pause and real Drive verification. Application still requires actual teacher ownership. Future execution is rejected using frozen school-local timing, while past overdue submission and earlier signed PDFs are retained. Receipt replay returns live permissions after emergency replacement. See D-115 and [evidence](verification/workspace-amendment.md).

## C23 current-day editing, archive separation and designated provider test — 2026-10-14

Owner explicitly removes the execution date picker: edit automatically uses the current school day/current term; date/term browsing is reserved for archive. Execution/review fills the schedule width and has no bottom documents section. `مستنداتي` becomes `أرشيف المناوبات`. Historical details default read-only; explicit current-owner overdue completion and approved correction preserve prior behavior without changing the main today entry.

Owner requested the local Google redirect mismatch fix, completed OAuth consent manually, and explicitly designated a **new D3_Test folder inside the current Al-Falah E2E Test School root**, with isolated test SQL. This authorizes bounded synthetic external write/read/recovery there, not original school activation or arbitrary real-school destinations. Only local public OAuth callbacks changed; existing credentials were renewed by the owner's Google consent. Real verification passed with two PDFs, one reserved hierarchy and no existing data deletion. Original per-school duty flags remain off; owner/physical appearance acceptance remains pending. D-115, [evidence](verification/workspace-amendment.md).

## C24 administration layout, scalable archive and dismissal/dashboard amendment

The owner requests administration-only layout refinement: separate prominent settings step navigation outside the content panel; rebalance every administrative duty page; compact paginated teacher management retaining existing permanent/date exclusion and exchange actions. The approved ordinary teacher view remains unchanged. Administration archive becomes one paginated table with responsibility letters on the RTL right and daily reports on the left, independent availability/actions and retained revision history.

Duty administrators may configure a dismissal time in the term plan; absent an override, the final timetable period remains authoritative. Preview/publication applies the choice to eligible regenerated dates, while retained/locked dates preserve their prior timing and signed documents/shared facts remain immutable. Day-specific reasoned timing exceptions still take priority. No schema migration or current-school data mutation is required for implementation.

The general account dashboard shows today's actual effective duty teacher and the preceding seven calendar days with explicit dates, for authorized duty administrators. Duty administration uses live active school membership plus the database Duty.Manage capability rather than the recorded primary-manager identity. Existing permission administration is reused; no grants are created, no generic delegation UI or general storage right is introduced. Backend scope/revocation and actual-teacher signing remain enforced. Owner visual/physical acceptance remains pending.

C24 follow-up: remove the continuous green navigation strip; distinguish individual step buttons only. Untouched setup must not cause a reload warning. Compare the initial normalized draft to actual settings changes, preserving unsaved-work protection. No migration or additional provider writes were performed or needed for this amendment; the previously designated isolated D3_Test boundary remains unchanged.


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


## C28 — Owner-confirmed draft departure explanation (2026-10-15)

The owner requested a centered, acknowledged explanation only when leaving actual report work as an unsubmitted draft, with clear completion steps and an OK button; random load/refresh notices are explicitly unwanted. Continue report is provided as a second useful action. This changes no signing, timing, ownership or automatic-archive rule. The actual 2026-10-15 missing Report was independently diagnosed as an unsubmitted draft, with zero signed Reports/archive intents. [Evidence](verification/draft-exit.md).

## C29 — Owner-confirmed source1 roster layout (2026-10-15)

The owner's supplied1.jpeg is the current official roster layout reference. Reopen it visually and verify its recorded fingerprint; reproduce organization and column/signature positions using current frozen school/plan values, never photographed personal data. This replaces the older sparse four-week pagination decision: ordinary18-week output fits one A4 page with nine weeks per side, with whole-week continuation when content requires it.

A layout upgrade receives a separate roster renderer identity and retained artifact. Use the earliest existing snapshot, retain every older artifact, and keep the signed letter/report renderer identity unchanged. Version-aware SQL uniqueness is required; the narrowly additive local index migration is covered by the owner's existing local manual-test migration authorization. No new roster archive obligation or external upload is authorized by this request. [Evidence](verification/roster-source.md).
