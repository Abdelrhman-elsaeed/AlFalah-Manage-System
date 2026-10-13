# Clarifications and Decision Register

**Updated:** 2026-10-13. The owner explicitly authorized D1 and its relevant documented defaults in the implementation request. D2/D3 remain outside that authorization.

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

All five submitted clarification questions are answered. The former fairness and archive-path proposals are confirmed as C10 and C11; no answer to those questions remains outstanding.

## Proposed defaults for plan review

| ID | Proposal | Reason / consequence |
|---|---|---|
| P2 | Schedule operating weekdays from selected timetable timing, within AcademicTerm dates, with manager-entered holiday/exam closure ranges | The paper has holiday/exam rows. Exam days remain operating unless explicitly closed; no automatic school-calendar import is assumed |
| P4 | Teacher report submission explicitly applies the saved handwritten signature again. If missing, save the draft and direct the teacher to existing signature settings; signed submission/acknowledgement stays unavailable | Reuses the confirmed signature mechanism without inventing a signature |
| P5 | Submission is final and does not wait for manager approval; manager reviews the following day. Corrections create a retained revision with reason and teacher re-signature | Paper specifies handover, not an approval state; preserves automatic archival and historical fidelity |
| P6 | Late and violating students are recorded in duty only; no automatic Student Affairs incident/penalty | The duty paper does not specify escalation, severity or conduct policy |
| P7 | Known Absent and Excused staff are unavailable. Missing attendance means unknown; show this state. Automatic next-in-queue cover may remain provisional until attendance is saved, with a final check before acknowledgement | The current model has no attendance fact until a sheet is saved; unknown must not be presented as confirmed Present |
| P8 | An emergency manual reassignment may proceed for a known-absent/excluded teacher only through an explicit exception warning and mandatory reason. The manager cannot sign on that teacher's behalf or falsify attendance | Supports the owner's manual control with transparent accountability |

The 2026-10-13 implementation request supplies the required plan agreement for D1. P2, P7, P8 and D1 correction/protection/compensation policies are authorized defaults; do not ask for reconfirmation. P4–P6 describe D2 behavior and remain unimplemented. If a default changes, update the corresponding spec, contracts and tasks together.

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
