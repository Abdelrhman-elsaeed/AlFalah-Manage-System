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
