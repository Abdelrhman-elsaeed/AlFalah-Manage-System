# Phase 1: weekly standby allocation and manager UI

**State:** Delivered on 2026-10-16 (C38 / D-130). Automated backend/SQL, Angular and actual responsive browser verification complete. Owner appearance/assistive-technology acceptance is not claimed.
**Implementation:** [Behavior and design](../weekly-implementation.md), [verification](../../verification/standby-phase1/README.md).
**Depends on:** Completed discovery and the existing teaching timetable.
**Skills:** dotnet-layered-api-architect and frontend-design; domain-modeling for unresolved rules; tdd for critical behavioral/integration checks. Use prototype only if an uncertain interaction benefits from a throwaway review.

## Deliver

A professional manager workbench that becomes available after teaching completion, supports manual/fair weekly standby, and saves/publishes standby in the existing timetable.

## Work

Implementation defaults for this phase: one standby teacher per period containing teaching, with visible editable global demand and per-period overrides; zero demand for otherwise empty periods unless explicitly overridden. All active same-school setup teachers participate unless explicitly excluded. Physical availability is the hard cap. Reserve shortages are shown as warnings and do not invent a new blocker for publishing hard-valid teaching. Fair placement balances combined teaching/standby occupancy against available periods, spreads daily load and preserves manual locks. Editing standby saves a draft through the existing publication lifecycle. Dated-coverage workflows and signature policy remain phase 2.

1. Refine the [UX direction](../ux-direction.md) while resolving reserve demand/default, fair-scoring explanation, participation exceptions and shortage/publication policy.
2. Verify a complete, saved, hard-valid teaching timetable on the server, bound to timetable/setup/Bell revisions. Present an actionable prerequisite state.
3. Derive daily/weekly physical capacity from actual eligible free periods. Existing standby consumes capacity; teaching, unavailability and breaks do not create capacity. No mandatory arbitrary per-teacher quota inputs.
4. Provide manual eligible-cell placement, remove/undo and explicit locks. Fair allocation preserves locks, addresses constrained periods and balances teaching/standby burden against availability.
5. Show required/allocated/available reserve, reasons and shortages in the timetable workbench. Review changes before applying; switching modes must preserve work.
6. Extend existing configuration/contracts/provenance only as needed, using thin controllers, service rules and scoped repository transactions.
7. Save/version/publish teaching plus standby in the existing model. Protect standby and manual locks during regeneration, restore and structural edits; retain future dated-cover safety and office-hour reconciliation.
8. Test and refine the actual frontend at 390/768/1440/1920px. A prototype is not a substitute for production UI review.

## Acceptance and tests

- No standby allocation before teaching readiness; stale proposals cannot save.
- Real shortened/unavailable/visiting/full teaching days produce correct capacity. Existing standby consumes capacity; no duplicate or overlapping commitment.
- Manual and fair share all hard constraints. A constrained-period scenario does not lose its only candidate through naive greedy allocation.
- Shortages are explicit; teaching is not moved and capacity is not raised silently.
- Reloaded school/teacher views and timetable printing show persisted standby.
- Regeneration/publication/concurrency/school-scope behavior passes meaningful backend/frontend checks and real isolated SQL verification where persistence changes.
- Actual captures/interactions satisfy the UX direction. Any additive migration is verified in isolation before authorized operational application.

## Exit and boundary

Deliver the complete weekly slice with evidence and update the spec kit. Daily absence assignment, execution/signatures and the official register PDF belong to phases 2–3. Do not change teaching quotas to hide dated-coverage validation problems.
