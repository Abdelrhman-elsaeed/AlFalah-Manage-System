# Phase 08 — Lesson Swaps and Substitution

**Module:** Intelligent Timetable (الجدول الذكي)  
**Status:** Requirements draft — no implementation  
**Primary actor:** Authorized timetable editor

## Overview

This phase allows a user to move a selected lesson by swapping it with another lesson while preserving the timetable's hard constraints. The system classifies candidate cells as a safe direct swap, a possible swap with warnings, a three-way swap requiring a third lesson/teacher, or impossible.

Despite the folder's use of “substitution,” the supplied flow describes structural lesson swaps, not temporary teacher absence cover. Any future daily substitute-teacher workflow should be modeled separately unless the user confirms they are one feature.

## UI/UX Requirements

### Candidate selection

- Start from the general timetable preview and require the user to select a source lesson.
- Enter a dedicated swap-selection mode with a persistent legend and cancel action.
- Evaluate and mark candidate cells:
  - **Green — لا توجد تعارضات:** safe direct two-way swap.
  - **Yellow — توجد تنبيهات:** feasible only if the user accepts one or more soft-rule or policy warnings.
  - **Red — بحاجة إلى تبديل ثلاثي:** direct swap conflicts, but a feasible three-way cycle exists.
  - **No highlight/disabled:** swap is impossible; optionally expose the reason on hover/focus.
- Use labels/icons/patterns in addition to color.
- Preserve day navigation and search while in selection mode, but never lose the chosen source cell without confirmation.

### Direct-swap review

- Clicking a green or yellow candidate opens **تبديل الحصص**.
- Show clear “from” and “to” cards with classroom, subject, teacher(s), day, period, exact time, and location.
- Show all detected warnings, such as a teacher meeting/blocked slot or a missed subject-distribution preference.
- Display side-by-side before/after mini timetables for each affected teacher. Mark current cells and destination cells distinctly.
- Show an explicit success message such as **لا توجد مشاكل - جاهز للتبديل** when clean.
- **تأكيد التبديل** remains disabled until the proposal has been validated against the latest timetable revision.

### Three-way swap wizard

- Step 1, **مراجعة**, explains why the direct swap fails and shows both affected teacher schedules.
- **التالي** requests ranked feasible solutions involving a third occurrence/teacher.
- Step 2, **حل وتأكيد**, lists selectable proposals. Each proposal identifies the third subject, class, teacher, day, and period.
- Selecting a proposal reveals a complete before/after preview for all three movements and all resulting warnings.
- The UI explains the cycle in plain language; no participant's destination may be implicit.
- **تأكيد التبديل** executes the complete cycle once. **الرجوع** returns to review without losing the original source/target selection.

### Result and error handling

- On success, refresh affected grids, display confirmation, and make the new timetable revision visible.
- On concurrent modification, do not apply a stale proposal. Explain that the timetable changed, reload candidates, and require review again.
- A failed multi-step swap leaves every entry unchanged.

## Backend & Data Models

### Proposal/read models

`TimetableSwapProposal`

- Ephemeral proposal ID/token, school and timetable ID, source timetable revision.
- Kind: Direct, Warning, or ThreeWay.
- Ordered list of movements.
- Hard validation result, warnings, quality delta, and expiry time.
- Hash/signature or server-side storage so the client cannot alter validated movements.

`TimetableSwapMovement`

- Logical lesson occurrence ID.
- Source and destination day/period/location.
- Affected classroom, subject, and teacher member IDs.

### Audit model

`TimetableChangeTransaction` (or an extension of the existing version metadata)

- `Id`, `SchoolTimetableId`, before/after revision.
- Change kind `DirectSwap` or `ThreeWaySwap`.
- Actor, timestamp, optional reason/accepted-warning codes.
- Serialized movement/evidence summary linked to the resulting immutable `SchoolTimetableVersion`.

### Validation engine

Every candidate and confirmation must evaluate the same rules as Phase 07 against exact effective time windows:

- teacher, classroom, and location availability/collisions;
- teacher hard unavailability;
- study day, break, and valid period;
- class-subject allowed days and fixed slots;
- paired-block integrity;
- teacher assignment membership and allocated load;
- subject daily distribution and early preference;
- consecutive teaching and last-period fairness;
- co-teaching groups moved as one logical occurrence.

Confirmation repeats validation inside the transaction using the latest row/revision locks. Candidate classification from the browser is never authoritative.

### Atomic update and versioning

- Direct swap updates both logical lesson occurrences in one transaction.
- Three-way swap updates all three occurrences as one cycle in one transaction.
- If any movement fails, roll back the complete operation.
- Increment `SchoolTimetable.Revision`, update audit fields, and create an immutable `SchoolTimetableVersion` with a swap-specific change kind.
- Invalidate prior analysis runs and rerun affected rules (or the full analysis) for the new revision.
- If the timetable is published, define whether the swap immediately updates the live schedule or creates a draft requiring republish.

## Business Rules

1. A direct swap exchanges the complete logical lesson occurrences, including all co-teachers and required location—not just display text.
2. A green candidate has no hard violations or warnings after the exchange.
3. A yellow candidate has no hard violation but does have explicitly disclosed soft/policy warnings; confirmation records acceptance.
4. A red candidate cannot be exchanged directly but has at least one valid three-way cycle.
5. An unhighlighted candidate has no valid proposal and cannot be confirmed.
6. A three-way proposal moves three occurrences so each affected teacher, class, and room remains conflict-free.
7. Required subject counts do not change during a swap; only placement changes.
8. Individual subject distribution, paired blocks, early preference, availability, meetings/blocked slots, and location constraints must all be reevaluated.
9. Hard constraints can never be bypassed through the swap UI unless a separate explicit override policy is approved.
10. Proposal confirmation is revision-bound, atomic, idempotent, audited, and versioned.
11. After any successful swap, all current-context consumers use the new published/live revision and exact bell-schedule boundaries.
12. Users without timetable-management permission may view the timetable but cannot enter swap mode or confirm a proposal.

## ❓ Pending Questions for the User

1. Are swaps limited to periods within the same day, or may the engine propose cross-day direct and three-way swaps?
2. When a yellow proposal violates a soft rule, which roles may accept it, and must they enter a reason?
3. Can one period of a paired block be swapped independently, or must every swap move the entire paired block as one occurrence group?
4. For a published timetable, should a confirmed swap become live immediately and notify affected teachers, or create a draft revision that requires republishing?
