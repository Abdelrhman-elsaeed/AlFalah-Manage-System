# Phase 10 — Hardening

**Status:** IN PROGRESS · **Last updated:** 2026-07-15

## Goal
Production readiness: tests, security review, performance, file validation, audit
coverage, and deployment setup.

## Scope
### In
- Tests
- Security review
- Performance
- File validation
- Audit coverage
- Deployment setup

## FileAttachment security
- Validate file types.
- Max size configurable.
- No public direct file access without authorization.
- Store locally in MVP or use an abstraction for S3 / Azure Blob later.
- Fields (future): Id, SchoolId, EntityType, EntityId, FileName, OriginalFileName,
  ContentType, FileSize, StoragePath, UploadedByUserId, UploadedAt, IsDeleted.

## Notifications readiness
- Prefer **in-app notifications first**; email later.
- Examples: evaluation pending approval, evaluation approved, instructor viewed
  report, new complaint submitted, visit reopened, improvement plan created,
  follow-up added, plan end date approaching.
- Fields (future): Id, SchoolId (nullable), UserId, Title, Message, Type,
  RelatedEntityType, RelatedEntityId, IsRead, ReadAt, CreatedAt.

## Acceptance criteria
- Security review passed; audit coverage across reopen/edit/approve/complaint.
- File validation enforced; deployment documented.

## Dependencies
All previous phases.

## 2026-07-15 gap-completion evidence

- The nullable analysis navigation in `ImprovementPlanService` is explicitly
  annotated for the EF `Include` chain (`v.Analysis!`), while the service still
  handles the legitimate null-analysis case immediately after loading. The full
  solution now builds with 0 warnings and 0 errors.
- `VisibilityGateTests` now calls the real public `VisitService.GetByIdAsync`
  surface as an Instructor and proves D-36 rejects it before any data access.
- The same suite calls the real public `ComplaintService.ListAsync` surface as
  both a Moderator and a Moderator+SchoolManager and proves D-75 always rejects
  the caller before any data access. SuperAdmin remains the explicit support
  exception.
- The backend suite passes 70/70 tests. These items complete the ordered GAP 4;
  the broader Phase 10 production-readiness acceptance criteria remain in progress.

## 2026-07-15 desktop-parity Phase 0 evidence

- P0.1 removes client-writable plan `InstructorId`; plan `SchoolId`,
  `InstructorId`, and `VisitId` are derived from the loaded visit. Optional
  `DomainId` must belong to the visit's snapshotted rubric version or the API
  returns an Arabic 400 `ApiResponse`. No migration was required.
- P0.2 adds EF-backed service integration tests for visit detail, instructor
  report, PDF payload authorization, plan CRUD, follow-up CRUD, teacher profile,
  cross-school scope, approval visibility, Moderator own-created scope, and the
  D-53/D-75 complaint hard blocks.
- P0.3 confirms the Moderator teacher policy as read-only within
  `ActiveSchoolId`. The canonical seed map grants `Instructor.View` but not
  `User.View/Edit/Delete` or complaint permissions. Teacher endpoints, route,
  and navigation consume `Instructor.View`; `/users` now has explicit permission
  gates so it cannot become a bypass for teacher edit/deactivation.
- Verification: backend Release build 0 warnings/errors; 87/87 backend tests;
  frontend production build green; Arabic/English parity 623/623. D-24, D-28,
  D-36, D-37, D-53, D-65, and D-75 remain unchanged. Phase 1 was not started.
