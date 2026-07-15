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
