# Phase 10 — Hardening

**Status:** Not started · **Last updated:** 2026-07-10

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
