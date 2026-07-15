# Phase 8 — Complaints

**Status:** COMPLETED ✅ · **Last updated:** 2026-07-15

## Goal
Instructor complaint/review-request workflow with strict visibility rules.

## Scope
### In
- Instructor complaint after viewing an approved report
- School Manager visibility (all complaints in his school)
- Moderator hard block: no complaint route, API read/write, dashboard, or export data (D-75)
- Hide complaint details from Main Manager
- Super Admin support access
- Complaint can trigger visit reopen (store reason) + re-evaluation
- Audit log
### Out
- Dashboards (Phase 9)

## Visibility rules
- School Manager sees all complaints in his school.
- Moderator receives 403 for every complaint service operation, even if a stale
  permission is present; Super Admin support is the only multi-role exception.
- Instructor sees own complaints.
- **Main Manager does NOT see complaint details.**
- Super Admin can see for support if needed.

## Enum
ComplaintStatus — see [../11-CONSTANTS-AND-ENUMS.md](../11-CONSTANTS-AND-ENUMS.md).

## Acceptance criteria
- Visibility enforced in backend; Main Manager and Moderator blocked from complaint details.
- Reopen from complaint stores a reason and is audited.
- Instructor submission is available only from his own Approved report after
  `ReportViewLog` has been written by the report endpoint.
- School Manager sees and handles all complaints in the active school.

## Dependencies
Phase 5 (approval/visibility), Phase 6 (reports).

## 2026-07-15 completion evidence
- The approved-report UI calls `GET /api/v1/visits/{id}/report`; only a successful
  response can expose the review-request dialog, so the server-written view log
  necessarily exists before submission.
- `ComplaintService.CreateAsync` independently checks Instructor role, own visit,
  Approved state, view log, and school scope.
- School Manager uses the scoped `/complaints` management route. Main Manager and
  Moderator have neither route nor navigation and are hard-blocked in the service.
- Moderator's canonical seed permissions no longer include `Complaint.View`.
