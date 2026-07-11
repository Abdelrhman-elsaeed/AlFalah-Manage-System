# Phase 4 — Visits & Scoring

**Status:** COMPLETED ✅ · **Last updated:** 2026-07-10

## Goal
Create and score classroom visits against the 25 standards, with analysis.

## Scope
### In
- Visit CRUD
- Draft
- 25-standard scoring
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
3. 25 `VisitScore` rows are pre-generated from that version's standards (distribution 6/4/6/3/6).
4. The visit starts as `VisitStatus.Draft` (enum int = 1).
5. User fills visit details and partial scores; can save draft via `PUT /api/v1/visits/{id}`.
6. To submit, **all 25 standards must be scored** (`POST /api/v1/visits/{id}/submit`).
7. After submit, status becomes `VisitStatus.PendingApproval` (enum int = 3) and an **immutable analysis snapshot** is persisted (see Analysis engine below).
8. Submitted visits are **read-only** in Phase 4 — re-edit/reopen/approve are Phase 5.

## Business rules (enforced in backend)
- School-scoping via `SchoolScopeGuard` (D-24 carry-over): school-scoped callers can only read/mutate visits within their `ActiveSchoolId`; global admins bypass.
- Instructor must have an **active `UserSchoolRole`** in the visit's school with role `Instructor` (validated on create).
- Score range: **0..4 or null** (null only allowed in Draft; submit requires all 25 to be non-null).
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
| `Visit` | SchoolId + InstructorId + CreatedByUserId + **RubricVersionId (snapshot)** + Category + Sequence + Status + VisitDate + Subject? + GradeClass? + Notes? + SubmittedAt? + audit + soft-delete |
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
| GET    | `/api/v1/visits/{id}` | Visit.View | Full detail + 25 scores + analysis if submitted |
| POST   | `/api/v1/visits` | Visit.Create | Create draft (snapshots active rubric, generates 25 empty score rows) |
| PUT    | `/api/v1/visits/{id}` | Visit.Edit | Update visit meta + upsert 25 scores (Draft only) |
| POST   | `/api/v1/visits/{id}/submit` | Visit.Edit | Validate 25/25 → PendingApproval + persist analysis snapshot |
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
  - Form: instructor dropdown (same-school list, `appendTo="body"`), category + sequence dropdowns, visit date, subject, grade/class, notes.
  - Scoring grid: 25 standards grouped by 5 domains (D1=6, D2=4, D3=6, D4=3, D5=6). Each row has a 0..4 score dropdown with Arabic labels (per docs/09) and an optional evidence note.
  - Live progress: "تم تقييم X من 25" / "اكتمل التقييم".
  - "حفظ مسودة" + "إرسال للاعتماد" (the latter disabled until 25/25).
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

- ✅ Visit create snapshots `RubricVersionId` and generates exactly 25 empty score rows.
- ✅ Draft saves partial scores (PUT accepts any subset of the 25 with score ∈ [0,4] or null).
- ✅ Submit blocked until 25/25 with Arabic message "تبقى N من 25 معياراً بدون درجة.".
- ✅ Submit transitions Draft → PendingApproval (status = 3) and persists the analysis snapshot.
- ✅ Analysis matches docs/09 exactly (hand-verified sample — see deviation D-26 worked example).
- ✅ School-scoping: school-scoped callers get 403 on cross-school access; `?schoolId=999` is silently coerced to `ActiveSchoolId`.
- ✅ Submitted visit is read-only in Phase 4 (PUT after submit → Arabic error).
- ✅ All 7 endpoints in Swagger (`http://localhost:5264/swagger`).
- ✅ Permissions enforced (401 unauthenticated, 403 missing permission).
- ✅ Frontend: prod build green, scoring grid works, 25/25 gate works, detail shows analysis in Saudi light RTL, no D-19 regression (ar/en = 228/228 leaf keys, zero duplicates).