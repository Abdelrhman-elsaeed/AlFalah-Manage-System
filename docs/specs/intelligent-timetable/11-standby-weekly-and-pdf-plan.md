# Standby allocation and official-register PDF: master implementation plan

**Date:** 2026-10-16 · **C37 / D-129**
**Status:** Phase 1 weekly allocation delivered in C38; phases 2 and 3 remain planned. See [implementation](standby-periods/weekly-implementation.md) and [evidence](verification/standby-phase1/README.md).
**Execution index:** [Phases, dependencies and skill map](standby-periods/README.md).
**References:** [Current-system discovery](10-standby-periods-discovery.md), [UX direction](standby-periods/ux-direction.md), private source `C:\Users\abdelrhman\Desktop\المناوبة\New folder\3.jpeg` (`QF71-B-2-1`).

All planning prose is English. Preserve exact Arabic UI/form labels and add matching English locale entries during implementation. The owner explicitly prioritizes exceptional frontend quality aligned with the current platform.

## 1. Confirmed product flow

1. Generate/complete and validate the teaching timetable.
2. Enable a separate weekly standby step; the manager chooses **fair** or **manual** allocation.
3. Review teaching and standby in the existing timetable; save/version/publish through its existing workflow.
4. Secretary-recorded absence prepares dated coverage needs and suggestions.
5. The authorized administrator independently chooses **manual** or **fair** daily distribution, then approves assignments.
6. Notify assigned teachers, show actionable dashboard tasks, capture acknowledgement/execution under the agreed signature policy.
7. Export the source-faithful daily register PDF. Archive through existing storage only under an explicitly agreed policy.

Weekly and daily mode choices are independent. Standby is recurring readiness; daily coverage is the actual dated assignment. Only a teacher explicitly scheduled as standby in the required period can cover that period, in either daily mode.

## 2. Enablement after teaching completion

The standby step is unavailable before a complete, saved, hard-valid teaching timetable exists. Do not use “some lesson cells exist” or an unverified status flag as readiness evidence. Reuse the assignment/requirement, timing and review validation, bound to the exact timetable/setup/Bell revisions.

Show an actionable prerequisite message with a route to teaching generation/review. Successful generation alone is insufficient if required lessons remain missing or invalid. Existing completed imported/manual schedules need the same verified readiness path rather than a fabricated generation receipt.

Standby allocation never moves or replaces a teaching lesson to make room. Changes to teaching, timing, teacher availability or participation invalidate an old preview and require revalidation. Reuse the existing publication lifecycle; do not create a second operational timetable.

## 3. Natural capacity: required safety rules

Teacher capacity is derived from their actual timetable and availability, not a mandatory arbitrary daily/weekly quota form.

For teacher `t` and study day `d`:

- `P(d)`: valid effective Bell study periods, excluding breaks/out-of-school time.
- `E(t,d)`: periods in `P(d)` where the teacher is active/participating, available, and has no overlapping teaching or other applicable blocking commitment.
- Existing valid standby cells are members of `E` and consume capacity; they are not extra capacity.
- `S(t,d)`: all retained plus proposed standby cells.

Required invariants:

```text
S(t,d) is a subset of E(t,d)
standbyCount(t,d) <= eligibleFreePeriodCount(t,d)
additionalStandby(t,d) <= eligibleFreePeriodCount(t,d) - retainedStandbyCount(t,d)
weeklyStandbyCount(t) <= sum of eligibleFreePeriodCount(t,d) over actual study days
```

Count unique occupied periods correctly; paired lessons consume both periods and co-teaching does not create duplicate capacity. Use actual time intervals and immutable Bell period identities. Overlapping/invalid teaching or timing must be resolved rather than used to calculate misleading free capacity.

Examples:

| Actual day | Available new standby |
|---|---|
| Seven valid periods, five teaching periods, one other unavailable period, no standby yet | At most one |
| Every available period already occupied by teaching | Zero |
| Five-period day with two teaching periods and one existing standby, all otherwise available | At most two more; total standby at most three |
| Visiting teacher available for only three periods and teaching two of them | At most one; unavailable school periods are not free capacity |

These are physical maxima, **not allocation targets**. Do not fill every free slot. Do not derive a reserve quota from `MaximumWeeklyPeriods - teachingLoad`: that field limits teaching, not all school-day availability.

Optional stricter school/teacher workload preferences may reduce capacity, never increase physical capacity. They are advanced controls, not mandatory invented numbers needed to enter the workflow. Weekly eligibility is recurring availability; today's absence does not silently become a term-long exclusion.

## 4. Coverage demand and realistic allocation

Demand is distinct from capacity. The school needs a clear desired number of standby teachers per active period, with an inspectable general setting and optional period overrides. Do not infer a statutory number, a full-day reserve quota or a target of all free teachers from the owner's “natural limits” instruction. The demand default remains a product-policy detail to settle in phase 1; it must be visible, editable and never silently created.

The working surface shows, for each period, **required / allocated / available**. If only one teacher is eligible and demand is two, show a shortage of one. A shortage cannot justify moving a lesson, exceeding availability or marking an ineligible teacher standby.

Do not add standby to breaks or non-teaching times merely to improve counts. Recognize date-specific calendar changes again during daily coverage rather than flattening one exceptional date into the recurring week.

## 5. Weekly manual mode

The manager selects an eligible free teacher/day/period cell in the existing timetable and marks it **منتظر**. Teaching cells remain protected from replacement. Show why a period cannot be selected, with a keyboard-accessible explanation.

A local inspector shows teaching, existing standby and remaining eligible capacity for that day/week. Support clear remove/undo and explicit manual locks. Switching to fair mode produces a reviewable proposal and preserves valid locks; it does not overwrite manual work silently.

Check the same server-side capacity, overlap, school, revision and period rules used in fair allocation. Manual is a method of choosing, not an override.

## 6. Weekly fair mode

Fairness is constrained by real capacity and coverage demand. Equal standby counts alone can overload a teacher who already has more teaching.

Proposed objective order:

1. Satisfy period coverage demand under all hard constraints, honoring valid manual locks.
2. Balance combined planned teaching/standby occupancy relative to each participating teacher's available school periods.
3. Reduce avoidable standby concentration on one day and long uninterrupted commitments; preserve actual Bell breaks.
4. Minimize unnecessary changes to valid existing placements and use deterministic, documented tie-breaking.

The exact ordering/score must be inspectable in phase 1 examples before it is implemented; do not claim one teacher deserves a precise numeric quota without agreed rules. Show each teacher's teaching load, planned standby, available capacity and the reason for a proposed placement.

A capacity-constrained optimizer, such as min-cost max-flow, can model teacher-week, teacher-day and eligible-period edges after manual locks. Fairness penalties use marginal costs rather than a one-pass greedy choice that strands the last candidate for a constrained period. If an objective cannot be represented by that solver, validate/extend it explicitly and report search limits honestly.

Revalidate the entire proposal with the common timetable rules. Report unmet demand precisely. Whether a reserve shortage blocks publication or allows an explicit warning remains a policy decision; no hidden publication restriction is invented here.

Weekly fairness balances planned readiness. Daily fairness balances actual cover assignments among scheduled standby teachers. Their counters must remain separate; daily fairness must also consider already-confirmed pending assignments so a teacher is not repeatedly selected before earlier work is marked executed.

## 7. Save, publication and regeneration

- Standby is persisted in the current timetable entry/version model and shown in school/teacher views and timetable print output.
- Preserve provenance/manual locks deliberately; existing cells cannot be classified as manual versus generated from their appearance alone.
- Compose teaching plus standby in a validated proposal before saving. Fix the current regeneration behavior that replaces all entries with `Lesson` output.
- Regeneration must display impact and preserve valid locks. A conflicting lock requires an explicit resolution, not silent removal.
- Editing/regenerating teaching invalidates dependent standby previews. Recalculate safely without changing retained signed daily history.
- Review confirmed future coverage when publication, restore or structural swaps change relevant lessons/period identities. Never lose a coverage task because an `EntryId` changed.
- Preserve office-hour and effective-ownership reconciliation.
- Keep the current weekly teaching maximum meaningful. Review the existing daily-cover simulation that may incorrectly make one dated cover look like a permanent extra weekly lesson; do not “fix” this by silently raising a teacher's teaching limit.

## 8. Daily coverage and teacher experience

Absence prepares needs/proposals, not final assignments. The administrator chooses manual or fair distribution; both use scheduled eligible standby teachers only and rerun eligibility on approval. Multiple needs cannot allocate one teacher to simultaneous classrooms. Paired-block policy is preserved.

An approved assignment has one effective source of truth reused by current-lesson, roster and permit workflows. Add execution data as a linked register, not a second substitute system.

Teacher dashboard tasks show the actual local date, period/time, classroom, subject and assignment state, with a direct action to details. Use existing durable notifications; notification receipt does not equal acknowledgement or execution. Only the actual assigned teacher can record their action. Resolve signature timing, deputy authority and correction/closure transitions during phase 2 before implementing the dependent actions.

## 9. Official PDF: confirmed source-fidelity requirement

The required artifact reproduces **سجل توزيع حصص الانتظار**, form **QF71-B-2-1**, from `3.jpeg`. It is independent of weekly timetable PDF export.

Preserve portrait page structure, school/year/term header, title/form identifier, absent-teacher/day/date group headings, source instruction, column order/widths/borders, signature positions and footer. Rebuild it as print-quality Arabic text/tables with embedded licensed fonts and actual school/record data.

| Source position | Digital content |
|---|---|
| Page header | Approved school identity/assets, academic year/term, title and form code |
| Group heading | Absent teacher, day/date and exact source acknowledgement instruction |
| الحصة | Actual Bell period sequence |
| الفصل / المادة | Retained original lesson identity/display snapshots |
| اسم المعلم المنتظر | Approved covering teacher for that date |
| ما تم تنفيذه في حصة الانتظار | Actual saved execution narrative; blank until recorded |
| التوقيع | Genuine authorized action/signature snapshot; never copied from the sample or manufactured |
| ملاحظات | Retained notes without truncation |
| Deputy/footer area | Actual responsible person's name and genuine signature when the agreed workflow provides one |

The short form preserves three groups and eight printed rows. Unused areas stay blank; do not duplicate absent teachers to fill the page. More periods/teachers or long text require readable continuation pages with repeated headings, not cropped text or illegibly small type. Eight rows are paper capacity, not a data limit.

Expose preview/download on the daily register. A pre-execution export must not imply completed execution or invent signatures. Final issued versions retain frozen bytes/snapshots; later corrections create retained revisions. Previewed and downloaded bytes must match. Local download works when Drive is unavailable; no new parallel storage system or implicit duty-archive flag reuse.

Reuse the existing Arabic renderer, preview and download infrastructure. Verify actual A4/100% PDF pages against the source, then obtain owner visual/physical-print acceptance when available. HTML screenshots and green builds do not establish source fidelity.

## 10. Delivery phases and skills

The owner requested exactly three implementation phases. Design and tests are integrated into delivery, not standalone phases. Detailed scope and acceptance live in the linked files.

| Phase | Deliverable | Principal skills | Status |
|---|---|---|---|
| [1](standby-periods/phases/01-weekly-standby.md) | Weekly manual/fair standby after completed teaching; premium manager UI, natural capacity, persistence/publication/regeneration and tests | dotnet-layered-api-architect, frontend-design; domain-modeling for rules; tdd | Not started |
| [2](standby-periods/phases/02-daily-coverage-and-execution.md) | Absence needs and approval; daily manual/fair cover; teacher dashboard, notification, acknowledgement/execution, signatures/corrections and tests | dotnet-layered-api-architect, frontend-design; domain-modeling for transitions; tdd | Not started |
| [3](standby-periods/phases/03-pdf-and-final-verification.md) | Source-faithful PDF, revisions, preview/download, agreed storage integration, final integrated verification and manual-test handoff | dotnet-layered-api-architect, frontend-design; tdd; diagnosing-bugs for reproduced failures | Not started |

Read the relevant skill when executing its work. Design refinement and a prototype, only if useful, belong inside phase 1; use the prototype skill for that work. Each phase includes its own tests and actual visual checks; phase 3 also verifies the assembled workflow. No additional design/testing delivery phases. Existing authorization governs execution; do not add repetitive approval rituals for already-authorized work.

## 11. Open decisions and current evidence

Still settle coverage demand/default, optional stricter preferences, exact fairness tie-breaking, shortage/publication behavior, signature/deputy workflow, responsible-role delegation, partial-day absence and archive policy.

Physical limits now derive from the timetable; mandatory arbitrary per-teacher quotas and a daily-only interpretation of the manual/fair choice are superseded.

Discovery has existing baseline evidence: [36 selected tests](verification/standby-discovery/results.json), using EF InMemory. It is completed background work, not a fourth implementation phase. There is no new allocator, source-faithful PDF, interactive standby prototype, live SQL/browser acceptance, migration or provider write yet.
