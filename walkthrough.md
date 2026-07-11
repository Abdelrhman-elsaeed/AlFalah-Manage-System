# Walkthrough — Al-Falah Schools Evaluation System

**Status:** Living document · **Last updated:** 2026-07-11

This file is a phase-by-phase narrative summary of what was actually built,
the acceptance evidence, and the worked examples. It is the human-readable
companion to `docs/` (the formal spec kit) and `docs/README.md` change-log.

---

## Phase 7 — Improvement Plans & Follow-ups (2026-07-11)

**Goal.** Reproduce the old desktop system's improvement-plan and follow-up
logic exactly: weak-domain suggestion chips, plan CRUD, follow-up CRUD,
latest progress, progress chart, role-aware visibility.

### Backend — implemented (0 errors / 0 warnings)

- **Entities** (`AlFalah.Domain/Entities`)
  - `ImprovementPlan.cs` — SchoolId / InstructorId / VisitId / DomainId? /
    Goal / Actions / StartDate / EndDate / SuccessIndicators / Status
    (Active / Completed / Cancelled) / audit fields / soft-delete fields /
    navigation to School, Visit, ApplicationUser, RubricDomain.
  - `PlanFollowUp.cs` — ImprovementPlanId / FollowDate / ProgressNote /
    EvidenceNote? / ProgressScore? (0..100) / audit / soft-delete.
- **EF configurations** (`AlFalah.Infrastructure/Data/Configurations`)
  - `ImprovementPlanConfiguration.cs` — Arabic_CI_AS collation on Goal /
    Actions / SuccessIndicators; composite index `(SchoolId, Status)` plus
    indexes on SchoolId / InstructorId / VisitId / DomainId / Status /
    IsDeleted; cascade follow-ups only on hard delete.
  - `PlanFollowUpConfiguration.cs` — Arabic_CI_AS on ProgressNote /
    EvidenceNote; indexes on ImprovementPlanId / FollowDate / IsDeleted.
- **DbContext** (`AlFalahDbContext.cs`)
  - DbSets `ImprovementPlans`, `PlanFollowUps`.
  - Global soft-delete query filters for both.
  - Auto-`UpdatedAt` on SaveChanges for both entity types.
- **DTOs** (`AlFalah.Application/DTOs/ImprovementPlanDtos.cs`) — 10 DTOs:
  ImprovementPlanDto, CreatePlanRequestDto, UpdatePlanRequestDto,
  PlanFollowUpDto, CreateFollowUpRequestDto, UpdateFollowUpRequestDto,
  ChartPointDto, PlanProgressDto, WeakDomainSuggestionDto.
- **Validators** (`AlFalah.Application/Validators/ImprovementPlans/ImprovementPlanValidators.cs`)
  — 4 FluentValidation validators. **No** `EndDate >= StartDate` rule (warning only per docs/10).
- **Service** (`AlFalah.Infrastructure/Services/ImprovementPlanService.cs`)
  - 5 **verbatim Arabic suggestion templates** (بيئة التعلم / التدريس والتعلم
    / تنمية المهارات / التقويم / سلوك المتعلمين) + fallback template —
    byte-for-byte against `docs/10` §Suggestion templates.
  - Latest progress = first scored follow-up in FollowDate DESC order.
  - Color thresholds: ≥75 success, ≥50&<75 warning, <50 danger.
  - Chart: FollowDate ASC, only non-null ProgressScore rows, returned
    only if ≥2 scored follow-ups exist.
  - Soft delete (Plan + FollowUp) preserves rows in DB; plan soft-delete
    cascades soft-delete to its follow-ups for consistency.
- **Controller** (`AlFalah.Api/Controllers/ImprovementPlansController.cs`)
  — 10 endpoints, thin, `ApiResponse<T>`, async + `CancellationToken`,
  all `[Authorize]`, gated by `PermissionNames.Plan.*`.
- **DI** (`DependencyInjection.cs:77`) — `IImprovementPlanService → ImprovementPlanService`.
- **Seeder** (`DatabaseSeeder.cs`) — `Plan.View/Create/Edit/Delete` granted
  to SchoolManager + Moderator + SuperAdmin + MainManager (full); Instructor
  gets `Plan.View` only (D-36/D-37 enforced in service).
- **Migration** `Phase7ImprovementPlans` (20260711131958) — applied; 2 tables,
  7 indexes, 6 FKs.

### Frontend — implemented (ng build --prod green)

- **Models** (`core/models/improvement-plan.models.ts`) — all interfaces,
  nullable progressScore (`number | null`), nullable evidenceNote
  (`string | null`).
- **Service** (`core/services/improvement-plans.service.ts`) — 10 methods
  matching the controller endpoints.
- **Routes** (`app.routes.ts`) — `/visits/:visitId/improvement-plans` and
  `/improvement-plans/:id` both `permissionGuard` with `Plan.View`.
- **plan-list** (`features/improvement-plans/plan-list`) — weak-domain
  chips with prefilled verbatim templates + DomainId + dates on click;
  plans `p-table` with status tags + row actions; non-blocking warning if
  `EndDate < StartDate` (save NOT blocked); status filter dropdown.
- **plan-detail** (`features/improvement-plans/plan-detail`) — plan info
  card; follow-ups list ordered by FollowDate DESC with add/edit/delete;
  latest-progress badge colored by 75/50 thresholds; **chronological line
  `p-chart`** shown only if ≥2 scored follow-ups.
- **visit-detail** — "خطط التحسين" button (NEW `canViewPlans()` =
  `Plan.View`) navigates to `/visits/{id}/improvement-plans`.
- **i18n** — `PLANS.*` + `FOLLOWUPS.*` merged into `ar.json` + `en.json`.
  D-19 parity preserved (16/16 top-level keys, no duplicates).

### Acceptance

| Item | Result |
|---|---|
| Backend `dotnet build AlFalah.slnx` | 0 Warning(s), 0 Error(s) |
| Frontend `npm run build` | green (15.7s, ~1015 kB initial; only 3 unrelated PrimeFlex library CSS warnings) |
| 5 Arabic suggestion templates verbatim | PASS (byte-for-byte vs docs/10) |
| Multiple plans per visit/domain allowed | PASS (no uniqueness constraint) |
| Editable in any status | PASS (no status gate in UpdatePlanAsync) |
| Follow-ups addable in any status | PASS (no status gate in AddFollowUpAsync) |
| NO EndDate>=StartDate block | PASS (no validator rule; UI warning only) |
| Plan deletion is SOFT | PASS (rows survive with IsDeleted=true) |
| Follow-ups not hard-deleted on plan delete | PASS (cascade soft-delete, no hard delete) |
| Latest progress ≥75 green / ≥50 gold / <50 red | PASS |
| Chart chronological (FollowDate ASC) | PASS |
| Chart appears only if ≥2 scored follow-ups | PASS |
| Routes guarded by `permissionGuard` | PASS (`Plan.View` on both routes) |
| visit-detail button permission-filtered | PASS (`canViewPlans()` gate added) |
| Instructor view-only, own + approved only (D-36) | PASS (service enforces) |
| Moderator only plans for visits HE created (D-37) | PASS (service enforces) |
| SchoolManager only plans in his school (D-24/D-28) | PASS (`SchoolScopeGuard` enforces) |
| Global admin bypass | PASS (`IsGlobalAdmin()` short-circuits) |
| i18n ar/en parity | PASS (16/16 top-level keys, no duplicates) |
| Migration additive only | PASS (no existing migration altered, no seeded-data change) |

### Worked example — end-to-end

1. **Visit with a weak domain** — Visit #5 has domain بيئة التعلم average < 2.5.
2. **plan-list page loads** — the chip button
   `D1 - بيئة التعلم (متوسط: 2.20)` appears in the suggestions card.
3. **Click the chip** — dialog opens prefilled with the **verbatim Arabic template**:
   - Goal: `تحسين جودة بيئة التعلم وجعلها أكثر إثراءً وفاعلية للمتعلمين`
   - Actions: 4 bullets `- مراجعة توزيع المقاعد وترتيب الغرفة الصفية` etc.
   - SuccessIndicators: `ارتفاع متوسط درجات نطاق بيئة التعلم إلى 3.0 أو أعلى في الزيارة القادمة`
   - DomainId: `1` (بيئة التعلم)
   - StartDate: today
   - EndDate: today + 2 months
4. **Click "حفظ"** — POST `/api/v1/improvement-plans` → 201 with the full plan DTO.
5. **Add follow-up #1** — ProgressScore=60, today → POST `/api/v1/improvement-plans/{id}/follow-ups` → 201.
6. **Add follow-up #2** — ProgressScore=80, 7 days later → 201.
7. **GET /api/v1/improvement-plans/{id}/progress** →
   `{ latestProgressScore: 80, latestProgressColor: "success", chartData: [{60, day1}, {80, day8}] }`.
8. **UI** — latest-progress badge renders GREEN ("ممتاز / مكتمل"), the chronological
   line chart appears with 2 points.

### Deviations

See `docs/14-DECISIONS-AND-DEVIATIONS.md` entries **D-42** (cascade soft-delete on
plan soft-delete), **D-43** (404/403 mapping via global middleware, not per-action
try/catch), **D-44** (visit-detail `canViewPlans()` gate added), **D-45**
(`visitRequest: any` polymorphic observable — minimal acceptable cast).
