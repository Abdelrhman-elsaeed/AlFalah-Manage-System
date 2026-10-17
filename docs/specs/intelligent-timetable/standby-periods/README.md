# Standby periods: three-phase delivery plan

**Date:** 2026-10-16 · **C37 / D-129**
**Status:** All three phases delivered locally (C38/C39/C40). Owner visual/operational and physical-print acceptance remains pending; see [phase 3 evidence and handoff](../verification/standby-phase3/README.md).
**Current weekly entry:** **الجدول الذكي → حصص الانتظار** (`/intelligent-timetable/standby`). C41 replaces the embedded timetable tab and clarifies reviewed saving/unallocated guidance; see [follow-up evidence](../verification/standby-save-navigation/README.md).
**Current undo:** C42 adds clearly labelled pre-save **تراجع خطوة** inside review and beside the grid/save controls; identical preview output does not add a history step. [Lock semantics and verification](../verification/standby-undo/README.md).
**Current publication/PDF:** C43 adds explicit **حفظ ونشر للمعلمين**, optional draft save and publication-only retry. **سجل الانتظار PDF** opens the existing dated daily register from the weekly header. [Workflow and verification](../verification/standby-publish/README.md).
**Current unified page:** C44 brings **التوزيع الأسبوعي** and **التغطية اليومية** into the same **حصص الانتظار** page. The shared command header connects actions, context/status and summaries; swaps remain separate. [Navigation, visual review and verification](../verification/standby-hub/README.md).
**Current PDF period:** C45 adds **يوم واحد** / **أسبوع كامل** in the existing register dialog. Select any date in weekly mode for a retained Sunday–Saturday PDF with seven dated daily sections and continuous pagination. Daily/weekly history is separate; existing daily bytes remain unchanged. [Implementation and verification](../verification/standby-week/README.md).

## References

- [Discovery and confirmed owner decisions](../10-standby-periods-discovery.md).
- [Allocation rules and official PDF master plan](../11-standby-weekly-and-pdf-plan.md).
- [Frontend direction and visual acceptance](ux-direction.md).

Planning prose is English. Exact Arabic UI labels and source-form text stay Arabic.

## Three phases

| Phase | What gets delivered | Skills | State |
|---|---|---|---|
| [1: Weekly standby](phases/01-weekly-standby.md) | Professional manager UI; manual/fair placement after teaching completion; natural capacity; save, publication and regeneration safety | dotnet-layered-api-architect, frontend-design; domain-modeling for rule decisions; tdd for critical behavior/integration checks | Delivered; [implementation](weekly-implementation.md) / [evidence](../verification/standby-phase1/README.md) |
| [2: Daily coverage and teacher execution](phases/02-daily-coverage-and-execution.md) | Absence needs/proposals; manual/fair assignment and approval; teacher dashboard/notifications, acknowledgement, execution and signatures | dotnet-layered-api-architect, frontend-design; domain-modeling for transitions; tdd | Delivered locally; [implementation](daily-implementation.md) / [evidence](../verification/standby-phase2/README.md) |
| [3: Official PDF and final verification](phases/03-pdf-and-final-verification.md) | Source-faithful QF71-B-2-1, preview/download and retained revisions; integrated acceptance and manual-test handoff | dotnet-layered-api-architect, frontend-design; tdd for artifact/integration behavior; diagnosing-bugs for reproduced layout/concurrency failures | Delivered locally; [implementation](register-implementation.md) / [evidence](../verification/standby-phase3/README.md) |

Design refinement, rule resolution and any useful prototype happen **inside phase 1**, not in a separate delivery phase. Use the prototype skill only where a throwaway interaction would resolve uncertainty. Every phase includes its own tests and actual UI review; phase 3 adds final integrated verification.

## Shared rules

- Reuse current timetable, attendance, effective ownership, notifications, signatures and storage; no parallel systems.
- Weekly and daily fair/manual choices are independent; manual never bypasses hard constraints.
- Enable weekly standby after complete valid teaching. Derive physical limits from actual eligible periods; no mandatory invented teacher quotas.
- Allocate required reserve demand rather than filling every free period. Explain shortages.
- Preserve manual decisions, concurrency, historical execution and genuine signatures.
- Follow the premium platform-aligned [UX direction](ux-direction.md), with actual responsive captures/refinement.
- PDF fidelity to source `3.jpeg` is required. Software, visual and physical-print acceptance are distinct evidence.
- Read applicable skills when performing the corresponding work. Keep existing Controller -> Service -> Repository -> Database, ApiResponse and school-scope conventions.
- Resolve remaining decisions in the phase that needs them. Existing user authorization governs execution; documentation reorganization does not start implementation or authorize production rollout.
- Update the spec kit with actual phase results and remaining work.

## Existing baseline

[36 selected existing tests](../verification/standby-discovery/results.json) passed using EF InMemory during discovery. They do not prove the new allocator, standby-only coverage policy, teacher register or official PDF is implemented.

The former six-phase split is superseded by this three-phase plan. Its requirements are merged into these phases, including frontend quality and verification.
