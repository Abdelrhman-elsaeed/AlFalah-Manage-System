# 11 — Constants & Enums

**Status:** Baseline · **Last updated:** 2026-07-10

> Create enums/constants for future use, but do not fully implement workflows yet.
> Roles are **database-driven** — do **not** depend only on a `UserRole` enum for authorization.

## SchoolStage
- Primary
- Intermediate
- Secondary

## VisitStatus
- Draft
- Submitted
- PendingApproval
- Approved
- RejectedForChanges
- Reopened
- UnderReviewAfterComplaint
- Cancelled

## PlanStatus
- active
- completed
- cancelled

## ComplaintStatus
- Open
- InReview
- Resolved
- Rejected
- Closed

## Visit Category (Arabic)
- استطلاعية / توجيهية
- زيارة صفية أو دورية
- زيارة تبادلية
- زيارة التثبيت / الترسيم للمعلمين الجدد
- زيارة المتابعة والدعم
- زيارة مفاجئة / تفتيشية
- زيارة طارئة
- زيارة التحقق / متابعة قانونية
- زيارة اللجان المركزية

## Visit Sequence (Arabic)
- أولى
- ثانية
- ثالثة
- متابعة

## Score labels & performance levels
See [09-RUBRIC-AND-EVALUATION.md](09-RUBRIC-AND-EVALUATION.md) for the exact Arabic
score labels (0–4) and performance-level thresholds.

## School File Storage S1 (2026-10-03)

`StorageFolderKind`: Teacher=1, SchoolLibrary=2, VisitArchive=3. `StoredFileSourceKind`: TeacherUpload=1, SchoolUpload=2, VisitArchive=3, HistoricalImport=4. `StoredFileAvailability`: Unverified=1, Available=2, Missing=3, Deleted=4, UploadIncomplete=5; legacy Completed is imported as Unverified rather than claiming live Drive existence.

`EvidenceLinkStatus`: Draft=1, PendingReview=2, Approved=3, Rejected=4, Resubmitted=5. Decisions preserve existing `EvidenceReviewStatus` values Approved=3/Rejected=4. `EvidenceFulfillmentPolicy`: AnyApprovedLink=1, MinimumApprovedLinks=2. `EvidenceImportance`: Normal=1, Important=2, Critical=3.

Schema-only preparation: `VisitArchiveStatus` Pending=1/Processing=2/Completed=3/RetryScheduled=4/NeedsAttention=5; `PrototypeImportStatus` Preview=1/ReferenceOnly=2/Committed=3/Exception=4. Their workers/workflows remain S5/S6. The eight `Storage.*` PermissionNames are seeded DB facts; these enums do not grant access.
