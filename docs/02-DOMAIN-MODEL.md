# 02 — Domain Model (Phase 1 Entities)

**Status:** Baseline + Phase 2 + Phase 3 + Phase 4 + Phase 5 entities · **Last updated:** 2026-07-15

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
**Fields:** Id, Name, Stage, SchoolLocationId (nullable for migrated legacy rows),
City (legacy display value synchronized from the selected location), LocationDetails, ManagerUserId (nullable initially),
LogoUrl, IsActive, CreatedAt, UpdatedAt, **IsDeleted**, **DeletedAt**, **DeletedByUserId**
**Rules:**
- Each school has exactly **one** School Manager (business validation).
- `ManagerUserId` is nullable at the DB level initially (create school first, assign manager later).
- **Business validation must prevent activating a school without a manager.**
- The same `Name` is allowed if `City`/`Location` is different.
- One stage only.
- **Soft delete (Phase 2):** `IsDeleted=true` hides the row from all queries via global query filter. Hard delete is not used in this layer.

## SchoolLocation
**Fields:** Id, NameAr, NameEn, RegionNameAr, RegionNameEn, Latitude, Longitude,
IsActive, CreatedAt, UpdatedAt, IsDeleted, DeletedAt, DeletedByUserId
**Rules:**
- A location is reusable by many schools; schools select it from the managed catalog instead of relying on a hardcoded city-to-coordinate map.
- Main Manager and Super Admin may add locations. All authenticated users may read active locations for school forms.
- Coordinates are constrained to Saudi Arabia and stored at `decimal(9,6)` precision.
- New and updated schools require an active `SchoolLocation`; `School.City` remains synchronized for compatibility with existing filters and exports.

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

## InstructorProfile / InstructorClass (Teacher profile enhancement)
`InstructorProfile` remains the teacher profile for an `ApplicationUser` with an
active `Instructor` `UserSchoolRole`; no parallel teacher table exists.

**InstructorProfile additions:** `Stage` (`SchoolStage`, nullable for legacy
rows; required by new teacher create/edit flows) and the existing
`SubjectSpecialization` / `EmployeeNumber` fields are completed by the UI and
DTO contracts. `SchoolId` is the teacher's active school. Moving a teacher to
another school updates both this profile and the active Instructor assignment,
with `SchoolScopeGuard` enforcing the caller's scope.

**InstructorClass (migration `AddTeacherProfileClasses`):** Id,
InstructorProfileId, ClassLabel (max 50, Arabic_CI_AS), SortOrder, CreatedAt,
UpdatedAt, IsDeleted, DeletedAt, DeletedByUserId.

**Rules:**
- One active class label per profile (filtered unique index on
  `(InstructorProfileId, ClassLabel)` where `IsDeleted = 0`).
- Class rows are soft-deleted when removed from the teacher's submitted list.
- Subject is one value (`SubjectSpecialization`); classes are many
  `InstructorClass` rows, which avoids CSV parsing and preserves an additive,
  queryable model for visit-form auto-fill.
- Teacher create/edit writes the teacher identity/profile fields only. Class
  labels are edited by an authorized manager in the teacher profile or by the
  Instructor through their self-only account settings endpoint.
- A Visit continues to snapshot its chosen `Subject` and `GradeClass`; teacher
  profile changes do not rewrite historical visits.
- Teacher longitudinal progress is derived from persisted `VisitDomainAverage`
  snapshots for the caller's in-scope **Approved** visits. Radar axes come from
  the active rubric domains dynamically. When at least two visits are available,
  the API compares the chronologically earliest and latest visit and returns
  `latest - earliest` per domain; domains missing from either historical
  snapshot are reported as unavailable rather than as a zero score.

## Visit (Phase 4 + Phase 5 approval fields)
**Fields:** Id, SchoolId, InstructorId (the evaluated teacher's user id), CreatedByUserId
(Moderator/SchoolManager who created it), RubricVersionId (SNAPSHOT — the active version at creation),
VisitCategory (enum), VisitSequence (enum), Status (VisitStatus, default Draft),
VisitDate, Subject (required for new visits), GradeClass (required for new visits),
LessonTitle (required for new visits, nullable only for legacy rows), PresentCount (required, >= 0),
AbsentCount (optional input, stored as 0 when omitted), Notes (nullable), SubmittedAt (nullable),
**ApprovedByUserId (nullable), ApprovedAt (nullable), RejectionReason (nullable), ReopenReason (nullable),
ReopenedByUserId (nullable), ReopenedAt (nullable) (Phase 5)**,
CreatedAt, UpdatedAt, IsDeleted, DeletedAt, DeletedByUserId
**Rules:**
- Always owned by one school; the `InstructorId` must have an active `UserSchoolRole` in `SchoolId` with role = Instructor.
- `RubricVersionId` is set at create time and is **immutable** thereafter — historical accuracy.
- On create, one `VisitScore` row per standard in the snapshotted rubric version is pre-generated (scores null). The count is dynamic (D-65), never hard-coded to 25.
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
- Unique on `(VisitId, RubricStandardId)` — exactly one row per standard in the visit's rubric snapshot.
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

## Intelligent Timetable Phase 02 — timings (2026-09-06)

`BellScheduleTemplate` belongs to one school, academic year and semester. A `TimetableSetupProfile` selects one template. Each save appends a complete `BellScheduleRevision` carrying the name and school timezone; its `BellScheduleDay` children own default/day definitions and `BellPeriod` children own ordered time-only boundaries and optional labels. Day 0 is the default; 1–7 are Saturday through Friday.

Periods are positive contiguous integer sequences with flexible duration/count, no overlap and no midnight crossing. Holidays have no effective periods; study days inherit defaults or own independent overrides. `SchoolTimetable.BellScheduleRevisionId` pins historical operational times. Changes reset dependent setup readiness and mark timetables for revalidation; explicit publication validates entries before changing the pinned revision. Version snapshots embed labels and times. Teacher context and gate-pass lookups share school-local, half-open period resolution and return no lesson in gaps.

See [Phase 02 blueprint](specs/intelligent-timetable/02-timings.md) for the full contract.
# Classroom Visits V2 additions (2026-09-21)

`Visit.ExperienceVersion` and `Visit.ScoringRuleSetVersion` select legacy versus prototype behavior without rewriting historical records. V2 adds `ClassroomPeriod` plus evaluator name/role snapshots. `VisitAnalysis.RuleSetVersion` and `OverallPercentage`, and `VisitDomainAverage.PercentageScore`, preserve exact V2 results alongside the compatible `/4` values used by existing charts.

- `RubricIndicator` is a versioned child of `RubricStandard`; V2 freezes 66 rows beneath 25 standards.
- `VisitObservedIndicator` is the soft-deletable join between a visit score and an observed rubric indicator, unique per active `(VisitScoreId, RubricIndicatorId)`.
- `VisitTreatmentSnapshot` is visit-owned and persists generated or manually edited goal/actions/success-indicator text. It replaces the standalone improvement-plan UI only for V2; existing plan/follow-up records remain retained.

The additive migration is `20260920233213_ClassroomVisitsV2AdditiveSchema`. No V1 column/table is dropped or renamed.

## School File Storage S1 (2026-10-03)

Implemented `StorageFolder`, `StoredFile`, `StoredFileVersion`, `EvidenceRequirement`, `EvidenceLink`, `EvidenceReviewDecision`, `StorageDelegation`, `VisitArchiveOperation`, `VisitArchiveArtifact`, `PrototypeImportBatch`, and `PrototypeImportRow` under `Entities/Storage`. Every readable record has explicit SchoolId; only requirement templates may have nullable school/year. File identity, immutable version identity, independent link status, and append-only decisions are separate facts. School/year requirements retain OriginalTaskId and stable Code; prototype domain/standard codes are separate from classroom-visit rubric identities.

Teacher identities remain InstructorProfile; no new teacher table or replacement of TeacherDriveFolder. Historical files retain their original SchoolId after a transfer, while access resolves active membership/profile/grant on every request. LegacySubmissionId is unique and immutable provenance retains review, deletion, missing, upload and timestamp fields. Incomplete task/year context is NeedsLink, with no invented link/decision. Unknown byte hashes and uploading users stay nullable; the old ledger's existence is not live availability.

Archive and import entities are schema preparation only. See [S1 data/verification contract](specs/school-file-storage/verification/README.md) for actual FK/index shapes and rollback. S2–S6 workflows are not implemented.

## School File Storage S2 (2026-10-03)

`StorageOperation` records SchoolId/actor/request key/fingerprint/action/status, authorized destination/owner, pre-generated provider identity, upload metadata/hash, optional legacy reservation/submission and resulting file/version IDs, safe error code/timestamps/rowversion. Pending precedes provider write; Completed requires a committed file/version for Upload; uncertain results are NeedsAttention, definitive rejection Failed. Scope-composite FKs and unique keys prevent cross-school or duplicate results.

S2 uploads create one StoredFile and immutable StoredFileVersion with NeedsLink and no EvidenceLink for standalone files. Legacy task uploads retain the original submission contract and PendingReview matrix through the shared service. Historical/Approved/multi-version facts protect mutations; source version identity/DriveFileName stays immutable on display-name changes. S3 links/reviews/change requests, S4 readiness, S5 archive and S6 imports remain unimplemented. [S2 domain and lifecycle contracts](specs/school-file-storage/verification/s2-library-and-uploads.md).

## School File Storage S3 (2026-10-04)

`EvidenceRequirement` is initialized idempotently per school/year with eleven stable STD codes in four domains, plus one identity mapping for each existing active EvidenceTask (OriginalTaskId and unchanged Code). Task semantic domain/standard assignments are explicit configuration, never guessed. Importance, responsible member/role and AnyApprovedLink/MinimumApprovedLinks policy are editable with rowversion; tasks are not duplicated. `EvidenceLink` is the active file/requirement/year/teacher association and pins its current VersionId. Draft → PendingReview → Approved/Rejected; Rejected → Resubmitted → Approved/Rejected. Review requires a current authorized manager/delegate; rejection requires a reason. `EvidenceReviewDecision` snapshots version, actor/name, time and note, independently for each link.

`FileChangeRequest` retains immutable original version/requester/kind/reason and policy intent, optional candidate version, Pending/Approved/Rejected status and rowversion. `FileChangeDecision` is append-only with reviewer/name/note/time, one terminal decision per request. ReplaceBeforeReview is allowed only before any approval history and applies a completed candidate without approving evidence; otherwise an authorized review decision is required. Accepted replacements preserve all versions/decisions and move every active link to the candidate with PendingReview. Delete means withdrawal with bytes/history retained, never provider trash in this workflow.

StoredFile adds immutable-once-set SharedWriterProvenanceJson/Fingerprint beside untouched original legacy provenance. StorageOperation.ChangeRequestId binds at most one reserved candidate upload to a request, using the S2 same-ID reconciliation. Legacy ReviewStatus for mapped files is a frozen compatibility baseline, not an independent decision writer. Matrix/counts/export use active links with an approval for the same current, available, permitted version; minimum policy applies and fulfilled requirements are distinct. File totals describe available library assets, link/requirement totals describe the selected year; each is displayed separately. Details: [S3 report](specs/school-file-storage/verification/s3-evidence-and-review.md). S4–S6 remain unimplemented; both flags OFF.
