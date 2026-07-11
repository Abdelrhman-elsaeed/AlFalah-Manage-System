# 02 — Domain Model (Phase 1 Entities)

**Status:** Baseline + Phase 2 + Phase 3 + Phase 4 + Phase 5 entities · **Last updated:** 2026-07-10

> All entities below are **Phase 1** entities. Phase 2/3/4 additions are noted inline.
> Future entities are listed at the end.

## ApplicationUser (extends `IdentityUser`)
**Custom fields:**
- FirstName
- LastName
- FullName or computed DisplayName
- IsActive
- PreferredLanguage
- LastLoginAt
- CreatedAt
- UpdatedAt
- **IsDeleted, DeletedAt, DeletedByUserId** (Phase 2)

**Identity fields used:** Id, UserName, Email, PhoneNumber, PasswordHash, etc.

## ApplicationRole (extends `IdentityRole`)
**Fields:** Description, IsSystemRole, CreatedAt, UpdatedAt
**Roles to seed:** SuperAdmin, MainManager, SchoolManager, Moderator, Instructor

## Permission
**Fields:** Id, Name, Code, Description, Category, CreatedAt
**Example codes:** Schools.View, Schools.Create, Schools.Update, Schools.Delete,
Users.View, Users.Create, Users.Update, Users.Delete, Roles.Manage,
Permissions.Manage, Auth.Login, Audit.View
> More permissions will be added later.

## RolePermission
**Fields:** Id, RoleId, PermissionId, CreatedAt

## UserSchoolRole
**Fields:** Id, UserId, SchoolId, RoleId, IsActive, CreatedAt, CreatedByUserId (nullable),
**UpdatedAt, UpdatedByUserId, IsDeleted, DeletedAt, DeletedByUserId** (Phase 2)
**Rules:**
- Allows the same user to be assigned to multiple schools.
- Allows the same user to have a different role per school in the future.
- Required for school-login authorization.
- **Exactly one ACTIVE SchoolManager per school** (Phase 2 business rule). Assigning a new one deactivates the previous row.
- **Soft delete (Phase 2):** `IsDeleted=true` excludes the row from queries (global filter).

## School
**Fields:** Id, Name, Stage, City, LocationDetails, ManagerUserId (nullable initially),
LogoUrl, IsActive, CreatedAt, UpdatedAt, **IsDeleted**, **DeletedAt**, **DeletedByUserId**
**Rules:**
- Each school has exactly **one** School Manager (business validation).
- `ManagerUserId` is nullable at the DB level initially (create school first, assign manager later).
- **Business validation must prevent activating a school without a manager.**
- The same `Name` is allowed if `City`/`Location` is different.
- One stage only.
- **Soft delete (Phase 2):** `IsDeleted=true` hides the row from all queries via global query filter. Hard delete is not used in this layer.

## RefreshToken
**Fields:** Id, UserId, Token, ExpiresAt, IsRevoked, CreatedAt, RevokedAt

## AuditLog
**Fields:** Id, SchoolId (nullable), UserId, Action, EntityName, EntityId,
OldValues (JSON, nullable), NewValues (JSON, nullable), Reason (nullable),
CreatedAt, IpAddress (nullable), UserAgent (nullable)
> Phase 1 should create the table and log basic auth/admin events if possible.

## SchoolReportSettings
**Fields:** Id, SchoolId, ReportHeaderText, ReportFooterText, LogoUrl, PrimaryColor,
ShowModeratorSignature, ShowManagerSignature, ShowQrCode, CreatedAt, UpdatedAt
> No full report workflow in Phase 1.

## UserSignature
**Fields:** Id, UserId, SignatureImageUrl, SignatureDrawnData (optional), DisplayName, UpdatedAt
> No full signature UI required in Phase 1 (simple placeholder allowed).

---

# Future Entities (not implemented in Phase 1)

## FileAttachment
Id, SchoolId, EntityType, EntityId, FileName, OriginalFileName, ContentType,
FileSize, StoragePath, UploadedByUserId, UploadedAt, IsDeleted
Can later link to: Visit evidence, Complaint, Improvement Plan, Follow-up.

## Notification
Id, SchoolId (nullable), UserId, Title, Message, Type, RelatedEntityType,
RelatedEntityId, IsRead, ReadAt, CreatedAt

## ImprovementPlan (Phase 7) — implemented

**Fields:**
- Id, SchoolId, InstructorId (the evaluated teacher — string), VisitId, DomainId (nullable, FK to RubricDomain)
- Goal (required, ≤ 2000 chars, Arabic_CI_AS)
- Actions (required, ≤ 4000 chars, Arabic_CI_AS)
- StartDate (required, DateTimeOffset), EndDate (required, DateTimeOffset)
- SuccessIndicators (required, ≤ 2000 chars, Arabic_CI_AS)
- Status (PlanStatus enum: Active / Completed / Cancelled; default Active on create)
- CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId
- IsDeleted, DeletedAt, DeletedByUserId (soft delete)

**Rules (verbatim from docs/10):**
- Multiple plans per visit/domain allowed (no uniqueness constraint)
- Editable in any status (active / completed / cancelled)
- Default StartDate = today, EndDate = today + 2 months
- NO EndDate>=StartDate enforcement (only non-blocking warning)
- Suggestions (WeakDomainSuggestionDto) prefill Goal/Actions/SuccessIndicators/DomainId + dates; user saves manually
- 5 Arabic templates per weak-domain name verbatim from docs/10 (بيئة التعلم / التدريس والتعلم / تنمية المهارات / التقويم / سلوك المتعلمين) + fallback template
- Soft delete: rows survive with `IsDeleted=true`; on plan soft-delete the service cascades soft-delete to its follow-ups (no hard delete)

## PlanFollowUp (Phase 7) — implemented

**Fields:**
- Id, ImprovementPlanId (FK), FollowDate (DateTimeOffset, default today on create)
- ProgressNote (required, ≤ 2000 chars, Arabic_CI_AS)
- EvidenceNote (optional, ≤ 2000 chars, Arabic_CI_AS)
- ProgressScore (optional, 0..100; if null the row is excluded from latest-progress and chart)
- CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId
- IsDeleted, DeletedAt, DeletedByUserId (soft delete)

**Rules (verbatim from docs/10):**
- FollowDate ordered DESC for list display
- Latest progress = first scored follow-up in FollowDate DESC order
- Color thresholds: ≥75 → success (green), ≥50&<75 → warning (gold), <50 → danger (red)
- Chart data: FollowDate ASC (chronological), only rows with non-null ProgressScore
- Chart appears only if ≥2 scored follow-ups exist
- Editable / deletable regardless of parent plan status

For the verbatim Arabic suggestion templates and the full old-system logic, see
[10-IMPROVEMENT-PLANS-AND-FOLLOWUPS.md](10-IMPROVEMENT-PLANS-AND-FOLLOWUPS.md).

## RubricVersion / Domain / Standard (Phase 3)
See [09-RUBRIC-AND-EVALUATION.md](09-RUBRIC-AND-EVALUATION.md). Rubric versioning:
Main Manager edits create a new `RubricVersion`; existing visits keep their
original `RubricVersionId` so old reports remain historically accurate.

## Visit (Phase 4 + Phase 5 approval fields)
**Fields:** Id, SchoolId, InstructorId (the evaluated teacher's user id), CreatedByUserId
(Moderator/SchoolManager who created it), RubricVersionId (SNAPSHOT — the active version at creation),
VisitCategory (enum), VisitSequence (enum), Status (VisitStatus, default Draft),
VisitDate, Subject (nullable), GradeClass (nullable), Notes (nullable), SubmittedAt (nullable),
**ApprovedByUserId (nullable), ApprovedAt (nullable), RejectionReason (nullable), ReopenReason (nullable),
ReopenedByUserId (nullable), ReopenedAt (nullable) (Phase 5)**,
CreatedAt, UpdatedAt, IsDeleted, DeletedAt, DeletedByUserId
**Rules:**
- Always owned by one school; the `InstructorId` must have an active `UserSchoolRole` in `SchoolId` with role = Instructor.
- `RubricVersionId` is set at create time and is **immutable** thereafter — historical accuracy.
- On create, exactly 25 `VisitScore` rows are pre-generated from the snapshot's standards (scores null).
- Phase 5 state machine:
  - `Draft → PendingApproval` on submit (creates VisitAnalysis).
  - `PendingApproval → Approved` on SM approve; `ApprovedByUserId` + `ApprovedAt` set.
  - `PendingApproval → RejectedForChanges` on SM reject; `RejectionReason` required.
  - `Approved → Reopened` on SM reopen; `ReopenReason` + `ReopenedByUserId` + `ReopenedAt` set.
  - `Reopened → PendingApproval` on resubmit; recomputes a NEW `VisitAnalysis` on the SAME `RubricVersionId`.
- Visit is editable in: Draft (Phase 4), RejectedForChanges (creator), Reopened (creator),
  PendingApproval (School Manager direct-edit path only).
- Soft delete only; `IsDeleted=true` hides the row from all queries via global query filter.

## VisitScore (Phase 4)
**Fields:** Id, VisitId, RubricStandardId, Score (int 0..4, nullable), EvidenceNote (nullable),
CreatedAt, UpdatedAt, IsDeleted, DeletedAt, DeletedByUserId
**Rules:**
- Unique on `(VisitId, RubricStandardId)` — exactly 25 rows per visit.
- Score ∈ [0..4]; null only allowed while visit is Draft.
- Soft delete cascades from `Visit` via the service.
- Phase 5: scores become mutable again when status = RejectedForChanges or Reopened (creator
  edits), or PendingApproval (SM direct-edit).

## VisitAnalysis (Phase 4 — recomputable in Phase 5)
**Fields:** Id, VisitId (unique 1:1), OverallScore (decimal(6,3)),
PerformanceLevelAr (verbatim from docs/09 thresholds), StrengthsJson (JSON array),
ImprovementAreasJson (JSON array), PriorityStandardsJson (JSON array),
ComputedAt, IsDeleted, DeletedAt, DeletedByUserId
**Rules:**
- Created on first submit.
- Phase 5: REPLACED in place on Approved → Reopened → resubmit. The 1:1
  invariant with `Visit` is preserved (`UX_VisitAnalysis_Visit` is unique on
  `VisitId`). The recompute always uses the visit's SNAPSHOTTED `RubricVersionId`
  (never the active version) so historical visits stay bound to the rubric
  that was in effect when they were created.
- Thresholds/labels follow [09-RUBRIC-AND-EVALUATION.md](09-RUBRIC-AND-EVALUATION.md) exactly.

## VisitDomainAverage (Phase 4, per-domain rows of the snapshot)
**Fields:** Id, VisitAnalysisId, RubricDomainId, DomainCode (e.g. "D1"),
DomainNameAr (snapshot of the rubric domain name, e.g. "بيئة التعلم"),
AverageScore (decimal(6,3)), IsDeleted, DeletedAt, DeletedByUserId
**Rules:**
- One row per rubric domain in the visit's snapshot (5 rows per analysis).
- Carries the snapshot of domain code + Arabic name so the snapshot stays readable even
  after the rubric is later edited.
- Phase 5: replaced together with the VisitAnalysis on reopen → resubmit.

## ReportViewLog (Phase 5 — NEW)
**Fields:** Id (long, identity), VisitId, InstructorUserId, ViewedAt, IpAddress (nullable),
IsDeleted, DeletedAt, DeletedByUserId
**Rules:**
- One row per view (record every view; expose "first viewed / last viewed / count" via
  the manager / moderator view-status endpoint).
- A row is only inserted when the current user is the visit's Instructor AND the visit
  is `Status == Approved` (both checks enforced in `VisitService.GetInstructorReportAsync`).
- Soft-delete cascades from `Visit` via FK.
