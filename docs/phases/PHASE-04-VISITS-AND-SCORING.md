# Phase 4 — Visits & Scoring

**Status:** COMPLETED ✅ + desktop-parity Phase 1 completed · **Last updated:** 2026-07-15

## Goal
Create and score classroom visits against the standards in the snapshotted rubric version, with analysis.

## Scope
### In
- Visit CRUD
- Draft
- Dynamic N-standard scoring (the seeded baseline remains 25)
- Submit for approval
- Score validation
- Analysis engine (snapshot, immutable in Phase 4)
- Visit permissions (View/Create/Edit/Delete + Submit + Approve + Reopen)
- School-scoping enforced in backend (D-24 carry-over)

### Out
- Approval visibility to instructor (Phase 5)
- Reopen / Reject-for-changes flow (Phase 5)
- Reports / PDF (Phase 6)
- Improvement Plans (Phase 7)
- Complaints (Phase 8)
- Real dashboards (Phase 9)

## Workflow (implemented)
1. Moderator or School Manager (with `ActiveSchoolId`) creates a visit via `POST /api/v1/visits`.
2. The visit **snapshots** the currently active `RubricVersionId` (D-21 — rubric is global).
3. One `VisitScore` row is pre-generated for every standard in that version (the seeded baseline distribution is 6/4/6/3/6).
4. The visit starts as `VisitStatus.Draft` (enum int = 1).
5. User fills visit details and partial scores; can save draft via `PUT /api/v1/visits/{id}`.
6. To submit, **all N standards in the visit snapshot must be scored** (`POST /api/v1/visits/{id}/submit`).
7. After submit, status becomes `VisitStatus.PendingApproval` (enum int = 3) and an **immutable analysis snapshot** is persisted (see Analysis engine below).
8. Submitted visits are **read-only** in Phase 4 — re-edit/reopen/approve are Phase 5.

## Business rules (enforced in backend)
- School-scoping via `SchoolScopeGuard` (D-24 carry-over): school-scoped callers can only read/mutate visits within their `ActiveSchoolId`; global admins bypass.
- Instructor must have an **active `UserSchoolRole`** in the visit's school with role `Instructor` (validated on create).
- Score range: **0..4 or null** (null only allowed before submit; submit requires all snapshotted rows to be non-null).
- Rubric snapshot is **immutable**: `RubricVersionId` cannot change after creation; if the rubric is later edited the historical visit stays bound to the version that was live at creation (D-21).
- Soft delete (`DELETE /api/v1/visits/{id}`) is Draft-only and cascades to scores/analysis.

## Enums (verbatim Arabic via i18n + extensions for English enum names)

### VisitStatus (`AlFalah.Domain.Enums.VisitStatus`)
| int | name | Used in Phase 4 |
|-----|------|------------------|
| 1 | Draft | ✅ |
| 2 | Submitted | reserved |
| 3 | PendingApproval | ✅ (set on submit) |
| 4 | Approved | Phase 5 |
| 5 | RejectedForChanges | Phase 5 |
| 6 | Reopened | Phase 5 |
| 7 | UnderReviewAfterComplaint | Phase 8 |
| 8 | Cancelled | reserved |

### VisitCategory (`AlFalah.Domain.Enums.VisitCategory`) — 9 Arabic values, verbatim from docs/11
See `AlFalah.Domain/Enums/VisitCategory.cs` → `VisitCategoryExtensions.ToArabicString()`.

### VisitSequence (`AlFalah.Domain.Enums.VisitSequence`) — 4 Arabic values
أولى / ثانية / ثالثة / متابعة (verbatim from docs/11).

## Entities

| Entity | Description |
|--------|-------------|
| `Visit` | SchoolId + InstructorId + CreatedByUserId + **RubricVersionId (snapshot)** + Category + Sequence + Status + VisitDate + Subject + GradeClass + LessonTitle + PresentCount + AbsentCount + Notes? + SubmittedAt? + audit + soft-delete |
| `VisitScore` | VisitId + RubricStandardId + Score? + EvidenceNote? + audit + soft-delete. Unique on (VisitId, RubricStandardId). |
| `VisitAnalysis` | VisitId (unique) + OverallScore(decimal(6,3)) + PerformanceLevelAr + StrengthsJson + ImprovementAreasJson + PriorityStandardsJson + ComputedAt + soft-delete |
| `VisitDomainAverage` | VisitAnalysisId + RubricDomainId + DomainCode + DomainNameAr + AverageScore(decimal(6,3)) + soft-delete |

All four entities carry `IsDeleted` / `DeletedAt` / `DeletedByUserId` and have a global soft-delete query filter.

## Analysis engine (docs/09 verbatim — implemented in `VisitService.ComputeAnalysis`)

Given 25 standards grouped by domain (distribution 6/4/6/3/6):
- **Domain average** = mean of that domain's standard scores (UNEVEN distribution respected; never a fixed /5 divisor).
- **Overall score** = mean of all 25 standard scores (`decimal(6,3)`).
- **Performance level** (highest → lowest, exclusive on the upper bound):
  - متميز ≥ 3.5
  - جيد جداً ≥ 3.0
  - جيد ≥ 2.5
  - متحقق جزئياً ≥ 2.0
  - يحتاج تحسين ≥ 1.0
  - غير مشاهد < 1.0
- **Strengths** = domains with average ≥ 3.0.
- **Improvement areas** = domains with average < 2.5.
- **Priority standards** = individual standards with score ≤ 1.

The snapshot (`VisitAnalysis` + `VisitDomainAverage[]`) is computed and persisted **once on submit**, then immutable in Phase 4. Phase 5 introduces reopen/approve which may rebuild the snapshot.

## API endpoints (thin controllers, services in `VisitService`, DTOs + FluentValidation)

| Method | Path | Permission | Purpose |
|--------|------|------------|---------|
| GET    | `/api/v1/visits` | Visit.View | Paged list (school-scoped, filter by status/instructor/category/date) |
| GET    | `/api/v1/visits/{id}` | Visit.View | Full detail + dynamic snapshot scores + analysis if submitted |
| POST   | `/api/v1/visits` | Visit.Create | Create draft (snapshots active rubric, generates N empty score rows) |
| PUT    | `/api/v1/visits/{id}` | Visit.Edit | Update visit meta + upsert all snapshot scores |
| POST   | `/api/v1/visits/{id}/submit` | Visit.Edit | Validate N/N → PendingApproval + persist analysis snapshot |
| DELETE | `/api/v1/visits/{id}` | Visit.Delete | Soft delete (Draft only) |
| GET    | `/api/v1/visits/{id}/analysis` | Visit.View | Snapshot only; 404 if not submitted |

## Permissions (granted by seeder `DatabaseSeeder.GetRolePermissionMap`)

| Permission | SuperAdmin | MainManager | SchoolManager | Moderator | Instructor |
|------------|:----------:|:-----------:|:-------------:|:---------:|:----------:|
| Visit.View       | ✅ | ✅ | ✅ | ✅ | ✅ |
| Visit.Create     | ✅ | ✅ | ✅ | ✅ | — |
| Visit.Edit       | ✅ | ✅ | ✅ | ✅ | — |
| Visit.Delete     | ✅ | ✅ | ✅ | ✅ | — |
| Visit.Submit     | ✅ | ✅ | ✅ | ✅ | — |
| Visit.Approve    | ✅ | ✅ | ✅ | — | — |
| Visit.Reopen     | ✅ | ✅ | ✅ | — | — |

`Visit.View` is granted to **Instructor** so they can see that a visit exists, but the **result visibility** (seeing scores / analysis) is Phase 5.

## Frontend (Angular 17 standalone + PrimeNG, Saudi light theme, RTL, ngx-translate)

- **Visits list** — `features/visits/visits-list` (p-table with filters: status / category / from-date / to-date; progress pill "X/25"; "زيارة جديدة" button permission-gated by Visit.Create; row action to view detail; soft-delete button only for Drafts).
- **Visit create / edit** — `features/visits/visit-form`:
  - Form: locked teacher when entered from profile; scoped teaching auto-fill; required subject, grade/class, lesson title, and present count; optional absent count/default 0 and supervisor notes; PrimeNG calendar for date.
  - Scoring grid: dynamic snapshot standards grouped by domain. A persistent 0–4 legend uses the exact Arabic labels and semantic colors; evidence is always inline; each domain shows scored/total in addition to overall N/N.
  - "حفظ مسودة" + "إرسال للاعتماد" (the latter disabled until N/N).
  - Read-only banner when the visit is submitted.
- **Visit detail** — `features/visit-detail`:
  - Meta header (instructor, category, sequence, date, subject, class, status pill).
  - Rubric-version snapshot hint.
  - Read-only scoring grid (with low/mid/high color cues on per-standard scores).
  - **Analysis card** (only if submitted): overall score (big number), performance-level tag, per-domain averages grid, strengths / improvements / priority standards lists with semantic colors (brand-green / gold / danger).
- i18n: full `VISITS.*` namespace + `RUBRIC.SCORE_LABEL_0..4` (merged into existing namespaces, no duplicate top-level keys — **D-19 safe**).
- New sidebar item `NAV.VISITS` (icon `pi pi-clipboard`) added to `ShellComponent.adminItems`, permission-filtered (`Visit.View`).
- Routes: `/visits`, `/visits/new`, `/visits/:id`, `/visits/:id/edit` — all gated by `permissionGuard`.

## Dependencies
Phase 3 (rubric — reused for snapshot), Phase 2 (users/schools, school-scoping via `SchoolScopeGuard`).

## Acceptance (all PASS)

- ✅ Visit create snapshots `RubricVersionId` and generates exactly one empty score row per snapshot standard.
- ✅ Draft saves the full dynamic score-row set with score ∈ [0,4] or null.
- ✅ Submit blocked until N/N with an Arabic message derived from the snapshot count.
- ✅ Submit transitions Draft → PendingApproval (status = 3) and persists the analysis snapshot.
- ✅ Analysis matches docs/09 exactly (hand-verified sample — see deviation D-26 worked example).
- ✅ School-scoping: school-scoped callers get 403 on cross-school access; `?schoolId=999` is silently coerced to `ActiveSchoolId`.
- ✅ Submitted visit is read-only in Phase 4 (PUT after submit → Arabic error).
- ✅ All 7 endpoints in Swagger (`http://localhost:5264/swagger`).
- ✅ Permissions enforced (401 unauthenticated, 403 missing permission).
- ✅ Desktop-parity Phase 1: lesson/attendance fields round-trip through create/update/detail/instructor report/PDF; two-standard EF regression proves dynamic behavior; score legend, inline evidence, per-domain progress, teacher locking, and graceful teaching auto-fill fallback are implemented.
- ✅ Phase 1 gate: Release build 0 warnings/errors; 88/88 backend tests; production frontend build; Arabic/English parity 634/634 with no duplicate top-level keys.
