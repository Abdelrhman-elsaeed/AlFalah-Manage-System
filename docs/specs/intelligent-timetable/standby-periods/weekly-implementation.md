# Weekly standby implementation — phase 1

**Delivery:** C38 / D-130, 2026-10-16. Phase 1 only; daily coverage modernization and the official register remain phases 2 and 3.

## User workflow

Open **الجدول الذكي → حصص الانتظار** at `/intelligent-timetable/standby`, then **التوزيع الأسبوعي**. C41 / D-133 replaced the original embedded `/timetable` tab; C44 / D-136 now hosts weekly planning and **التغطية اليومية** together while preserving their existing components and APIs. The unified command surface replaces the repeated headings/detached context panels. Visited panels remain mounted so local choices and undo survive switching; in-flight operations and dirty navigation remain guarded. Timetable identity, school/year/term, publication, history, teacher views and printing remain authoritative; no second timetable model or allocation engine. [Current host and verification](../verification/standby-hub/README.md).

The workbench reads the complete saved teaching timetable and runs the shared hard validator. Missing assignments, teaching requirements, invalid timings or stale timing validation block allocation and explain the prerequisite. **فتح الجدول الدراسي** provides the correction path. Imported/manual teaching can qualify; a generation receipt alone is insufficient.

All active setup teachers whose current instructor profile still belongs to this school participate by default. The manager can exclude individuals without changing teaching assignments. Default demand is one reserve for each period containing teaching, zero otherwise; the global number and individual period overrides are visible and editable. This is an implementation default, not a statutory teacher quota.

Manual mode permits only eligible free cells. A manual selection starts locked. Locks can be released explicitly; switching modes retains choices. Fair mode prepares a proposal without writing, retains locks and finds feasible candidates across the whole week. **مراجعة وحفظ التوزيع** opens a review dialog showing additions/removals and totals; the final confirmation saves that exact preview. Edits or mode changes invalidate the accepted preview. Initial unallocated demand is explained neutrally; a reviewed shortfall receives accurate shortage guidance. Undo restores the previous local operation. Search, day focus and contained scrolling keep a large teacher list from extending the page indefinitely. Teacher/period details expose load, capacity and availability reasons; opening/closing details manages keyboard focus.

Saving creates a draft revision and audit/version in the existing timetable. C43 / D-135 adds explicit **حفظ ونشر للمعلمين** as the primary reviewed confirmation, applying the exact accepted proposal then publishing the returned saved revision through the existing endpoint. **حفظ الانتظار كمسودة** remains a secondary choice. A clean saved draft can be published directly using **نشر المحفوظ للمعلمين**. Publication failure preserves the draft and offers publication-only retry; it never repeats apply. Clones remain draft-only. Publication covers the whole saved timetable and retains existing backend validation/concurrency/event rules. Existing school and personal teacher reads and timetable PDF then include the persisted **منتظر** cells; dated cover assignments and their notifications still require daily approval.

The shared header exposes **سجل الانتظار PDF**, opening the existing register in the daily panel (`/intelligent-timetable/standby?tab=daily&register=true`). The old substitution/register deep link redirects here. Choose the date to issue/view/preview/download QF71-B-2-1. This dated absence/signature register is separate from the weekly timetable PDF on **الجدول الدراسي**. [Publication/PDF follow-up evidence](../verification/standby-publish/README.md).

C42 / D-134 makes **تراجع خطوة** explicit in the toolbar, footer and review dialog. A changed fair proposal is one undo step, restoring prior settings/placements and manual locks without applying to the server. Identical preview output does not add an empty step. Continue editing keeps the proposal; Undo restores the previous state. This is local pre-save history, cleared by successful save/reload. Lock explanations clarify retained teacher/day/period, manual default locking and unlocking without removal. [Verification](../verification/standby-undo/README.md).

## Capacity and fair distribution

- Actual study-day Bell periods and immutable availability references define capacity. Shortened days, unavailable/visiting periods, breaks and overlapping windows never create extra periods.
- Teaching is immutable input to this allocator. No teaching lesson is shifted, deleted or reassigned to make standby fit.
- One teacher can hold at most one commitment in a real period. Existing reserves consume capacity. Remaining capacity shown to the manager subtracts current reserves.
- Demand is a coverage target, not a requirement to occupy every free period. Unfilled demand is explicit and does not block otherwise hard-valid teaching publication.
- Integral minimum-cost maximum flow maximizes feasible coverage, including reassignment of an earlier candidate when another period has only one candidate. Marginal costs balance combined teaching/reserve load relative to available periods, then daily workload, with a small retention preference. Results are deterministic for identical inputs. Cancellation is honored.
- Manual and fair modes share the same server eligibility and final hard validator. Unknown/cross-school/duplicate/non-study/unavailable placements are rejected.

## Persistence, authorization and lifecycle

`GET /api/v1/timetables/{id}/standby` returns readiness, settings, actual periods, teacher capacity, cells and current placements. `POST .../standby/preview` computes a read-only proposal. `PUT .../standby` accepts a reviewed request plus its fingerprint.

Thin controller → application service/engine → scoped repository → EF/SQL is retained. School managers, global administrators, timetable-management permission holders and explicitly delegated timetable moderators may manage it; instructor/guardian roles are excluded. Every repository read is school-scoped.

Preview fingerprints include the normalized result, timetable/setup/Bell/template revisions, teacher/requirement/assignment revisions and protected future coverage cells. Apply recomputes the proposal in a serializable transaction, rejects stale/tampered fingerprints, updates reserve rows in place and retains every teaching row ID. Replaying an old revision conflicts rather than creating another version.

Additive migration `20261016164904_AddWeeklyStandbyAllocation` adds timetable settings JSON and reserve lock/source metadata. Existing null-source standby is interpreted as locked Manual, preserving legacy intent without rewriting old records. Retained standby rows keep their IDs. Snapshots, clone, restore, generic saves and repair simulation preserve settings/provenance; teaching regeneration reserves existing standby time and carries those rows into the new draft. Regeneration never silently drops a reserve.

The old full-entry replacement path recreated teaching IDs. It now refuses replacement/regeneration/restore while present/future confirmed daily coverage references the timetable, requiring coverage review first. The weekly apply path is safe because it preserves those IDs and protects the associated reserve against removal/unlocking. Office-hour reconciliation still follows the existing publication event/outbox path.

GET repairs stale saved participation/demand references after teacher/timing changes and presents a review notice; request validation remains strict. Invalid old reserve cells are exposed for explicit removal/review, including legacy references that are no longer visible as active teachers. No storage, Drive, signature policy or daily assignment rule is changed in this phase.

## Verification and boundary

See [phase 1 evidence](../verification/standby-phase1/README.md). The three-phase package remains the delivery boundary. Phase 2 implements standby-only dated cover, absence proposals/approval, manual/fair daily allocation and teacher execution. Phase 3 implements the photographed register and final integrated acceptance. Owner visual acceptance and assistive-technology/physical-print checks are not replaced by automated evidence.
