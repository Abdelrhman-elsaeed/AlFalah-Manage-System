# Phase 2: daily coverage and teacher execution

**State:** Delivered locally (C39 / D-131), 2026-10-16. 333 focused backend/SQL and 11 Angular checks pass; real isolated SQL/authenticated responsive browser evidence is linked below. Owner appearance/operational acceptance remains pending.
**Depends on:** Phase 1 published standby.
**Skills:** dotnet-layered-api-architect, frontend-design, domain-modeling for transitions/authority; tdd for business, authorization and durable integration behavior.

## Deliver

A complete absence-to-execution journey: prepare needs, approve manual/fair assignments, notify the actual teacher and retain acknowledgement/execution under the agreed signature policy.

## Work

Implementation decisions: the owner confirms separate signed acknowledgement and signed execution. Reuse current timetable management/delegated-editor authority. Attendance currently records full-day Present/Absent/Excused facts; Absent and Excused create needs, with no fabricated partial-day times. Fair distribution considers retained actual execution periods during the academic term plus pending approved work and current proposals; deterministic ties use teacher identity. Corrected absence or an absent substitute requests administrator review without erasing assignments or signed history. Execution submission follows the actual period end; authenticated teacher acknowledgement is available before execution. Corrections retain previous signed versions. No automatic deputy signature or PDF/archive operation is introduced.

1. Resolve daily fair counters/tie-breaking, waiting-period administration/delegation, acknowledgement versus execution signing, deputy authority, correction/closure and partial-day absence policy as needed.
2. Reuse Secretary attendance facts with a separate durable coverage reconciliation flow. Repeated/corrected absence cannot duplicate needs or silently erase completed work.
3. Resolve the effective published timetable, term, Bell calendar and school-local date. Collect actual absent-teacher lesson members/blocks using structured classroom/subject identity.
4. Reuse the current candidate engine/change ledger, enforcing scheduled-standby-only eligibility at proposal and approval across current coverage entry points.
5. Provide independent daily manual/fair selection, review and administrator approval. A free teacher without scheduled standby is ineligible; simultaneous needs cannot double-book a teacher.
6. Preserve original/effective ownership, paired/co-teaching rules, idempotency, stale checks, audit/version/outbox behavior and effective student/roster/permit integration. Resolve dated-cover versus permanent weekly-load validation explicitly.
7. Add concise teacher dashboard tasks and existing internal notifications with date, real time, class, subject, state and a direct details action.
8. Implement agreed acknowledgement/execution, narrative/notes and genuine signature snapshots, including draft recovery, review/submission, inability to execute and correction/reassignment history.
9. Enforce exact assignment ownership and school/role boundaries. Notification delivery is not execution; a draft is not submission.
10. Refine the compact daily ledger and teacher experience at all required responsive widths.

## Acceptance and tests

- Absence prepares suggestions; no assignment takes effect before administrator approval.
- Both modes reject free-but-not-standby teachers, including direct HTTP attempts. Missing candidates leave visible uncovered needs.
- Test repeated events, corrected absence, absent substitute, competing administrators, expired/stale proposals, paired/co-taught lessons and publication changes.
- Test teacher versus unrelated user, reassignment during editing, saved-signature changes, lost submit response, long narrative, unavailable signatures and retained corrections.
- Verify real isolated SQL atomicity/durability, authenticated HTTP authorization/effective ownership, and actual notification/dashboard/browser behavior using synthetic accounts.
- Existing lesson-context, student actions, permits, messaging and office-hour workflows retain their boundaries.

## Exit and boundary

Management can distinguish needed, assigned, acknowledged and executed work. The actual teacher can find and complete the agreed action, and retained snapshots are ready for phase 3. No second substitute or messaging system; no official PDF/Drive success claim before phase 3 proves the artifact.

Delivered behavior: [implementation](../daily-implementation.md). Actual tests, captures, manual route and unchanged production build/unrelated regression limits: [evidence](../../verification/standby-phase2/README.md). Stop at this phase boundary; official QF71-B-2-1 remains phase 3.
