# Phase 8 — Complaints

**Status:** Not started · **Last updated:** 2026-07-10

## Goal
Instructor complaint/review-request workflow with strict visibility rules.

## Scope
### In
- Instructor complaint after viewing an approved report
- School Manager visibility (all complaints in his school)
- Related Moderator visibility (only complaints linked to visits he created)
- Hide complaint details from Main Manager
- Super Admin support access
- Complaint can trigger visit reopen (store reason) + re-evaluation
- Audit log
### Out
- Dashboards (Phase 9)

## Visibility rules
- School Manager sees all complaints in his school.
- Related Moderator sees only complaints linked to visits created by him.
- Instructor sees own complaints.
- **Main Manager does NOT see complaint details.**
- Super Admin can see for support if needed.

## Enum
ComplaintStatus — see [../11-CONSTANTS-AND-ENUMS.md](../11-CONSTANTS-AND-ENUMS.md).

## Acceptance criteria
- Visibility enforced in backend; Main Manager blocked from complaint details.
- Reopen from complaint stores a reason and is audited.

## Dependencies
Phase 5 (approval/visibility), Phase 6 (reports).
