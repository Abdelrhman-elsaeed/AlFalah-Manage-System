# Phase 3 — Rubric

**Status:** COMPLETED ✅ · **Last updated:** 2026-07-10

## Goal
Introduce the versioned rubric: domains, standards, and active version management.

## Scope
### In
- Rubric versions
- Domains
- Standards
- Seed 5 domains and 25 standards
- Active version
- Main Manager editing with versioning
### Out
- Visits, scoring, reports (Phase 4+)

## Rules
- Every edit by Main Manager creates a new **RubricVersion**.
- Existing visits keep their original **RubricVersionId**.
- Old reports remain historically accurate.
- All schools use the same active rubric version for now (rubric is **GLOBAL**, see **D-21**).

## Reference
See [../09-RUBRIC-AND-EVALUATION.md](../09-RUBRIC-AND-EVALUATION.md) for score labels,
performance levels, and analysis rules.

## Acceptance criteria
- 5 domains and 25 standards seeded.
- Active version resolvable; editing creates a new version without breaking history.

## Dependencies
Phase 1 (identity), Phase 2 (schools/users context).

---

## Implementation summary (Phase 3 — DONE)

### Backend (`backend/AlFalah.*`)
- **Entities:** `AlFalah.Domain/Entities/RubricVersion.cs`, `RubricDomain.cs`, `RubricStandard.cs`
  (Id, VersionNumber, IsActive, CreatedAt, Notes, CreatedByUserId, IsDeleted + nav).
- **EF Configurations:** `AlFalah.Infrastructure/Data/Configurations/Rubric*Configuration.cs`
  - **MOD-3:** `RubricVersionConfiguration.cs:36` defines filtered unique index
    `HasIndex(x => x.IsActive).HasFilter("[IsActive] = 1 AND [IsDeleted] = 0").IsUnique()`
    → DB index `UX_RubricVersion_Active`.
- **DbContext:** 3 DbSets (`RubricVersions`, `RubricDomains`, `RubricStandards`) + soft-delete query filters.
- **DTOs:** `AlFalah.Application/DTOs/Rubric/RubricDtos.cs` — read (Version/Domain/Standard/ListDto),
  write (CreateRubricVersionDto + Domain/StandardWriteDto), score scale (ScoreScaleEntryDto,
  PerformanceLevelDto, ScoreScaleDto).
- **Interface:** `IRubricService.cs` — 6 methods (GetActiveVersionAsync, GetVersionsAsync,
  GetVersionByIdAsync, CreateNewVersionAsync, ActivateVersionAsync, GetScoreScale) — all async,
  all with `CancellationToken` (closes the soft D-05 carry-over for the new layer).
- **Validators:** `CreateRubricVersionValidator.cs` — FluentValidation rules for Notes + each
  domain (Code, NameAr, SortOrder, Standards) + each standard (Code, TextAr, SortOrder).
- **Service:** `AlFalah.Infrastructure/Services/RubricService.cs`
  - **MOD-4 copy-on-write:** `CreateNewVersionAsync` deactivates all currently active versions
    FIRST, then builds a brand-new `RubricVersion` with brand-new `RubricDomain`/`RubricStandard`
    rows (new Ids = 0 → EF assigns). Historical rows are NEVER mutated, so old visits remain
    historically accurate.
  - **MOD-5 score-scale:** `GetScoreScale()` returns the 5 score labels + 6 performance levels
    verbatim from [../09-RUBRIC-AND-EVALUATION.md](../09-RUBRIC-AND-EVALUATION.md) (compile-time
    constants — no DB hit).
- **Controller:** `AlFalah.Api/Controllers/RubricController.cs` — **6 endpoints**, thin, all
  return `ApiResponse<T>`, all use `CancellationToken`. `Rubric.View` for reads, `Rubric.Manage`
  for writes (403 with localized Arabic message when missing).
- **DI:** `AlFalah.Infrastructure/DependencyInjection.cs:62` — `AddScoped<IRubricService, RubricService>()`.
- **Seeder:** `AlFalah.Infrastructure/Data/Seeders/DatabaseSeeder.cs` — `SeedRubricAsync()`
  - **MOD-1:** Rubric.View granted to all 5 roles (SuperAdmin, MainManager, SchoolManager, Moderator, Instructor).
    Rubric.Manage granted ONLY to MainManager (SuperAdmin has all permissions by default).
  - Seeds RubricVersion 1, IsActive=true, with **5 domains / 25 standards verbatim** (D1..D5,
    D1-S1..D5-S6, distribution 6/4/6/3/6, Arabic names exactly as in this spec).
  - Idempotent (guard: `if (await _context.RubricVersions.IgnoreQueryFilters().AnyAsync()) return;`).
- **Migration:** `AlFalah.Infrastructure/Data/Migrations/20260710123300_Phase3Rubric.cs`
  - 3 tables + FKs + standard indexes + **`UX_RubricVersion_Active` filtered unique index**.

### Frontend (`frontend/src/app/`)
- **Service:** `core/services/rubric.service.ts` — 6 endpoints, `ApiResponse<T>`, base URL from `environment.apiUrl`.
- **Models:** `core/models/rubric.models.ts` — mirrors backend DTOs (camelCase).
- **Viewer:** `features/rubric/rubric-viewer/` — RTL Arabic tree of the active rubric. 5 domain
  cards → standards in SortOrder, showing codes (D1.. and D1-S1..) + Arabic text. Loading
  spinner. "Edit" button visible only when the user has `Rubric.Manage`.
- **Editor:** `features/rubric/rubric-editor/` — loads active version, inline-edit NameAr/TextAr
  + reorder (up/down arrows) + notes textarea. "حفظ نسخة جديدة" calls
  `POST /api/v1/rubric/versions` and returns to the viewer on success.
- **Routes:** `app.routes.ts` — `/rubric` (Rubric.View) and `/rubric/edit` (Rubric.Manage),
  both under the shell, both using `permissionGuard` + `route.data.permissions`.
- **Shell sidebar:** `shared/layout/shell/shell.component.ts:52` — NAV.RUBRIC item gated by
  `Rubric.View` (already wired in the previous step; sidebar hides it if user lacks the perm).
- **i18n:** `assets/i18n/ar.json` + `assets/i18n/en.json` — `NAV.RUBRIC` merged into existing
  NAV namespace; new `RUBRIC.*` namespace (25 keys) added in both files. **MOD-6** verified:
  ar/en have 151/151 leaf keys (identical key sets). No duplicates, no raw-key regression.

## Acceptance evidence (2026-07-10)

DB verification (after `dotnet ef database update` + app startup seeder):

| Check | Expected | Actual |
|-------|----------|--------|
| `RubricVersions` (live) | 1 | **1** ✓ |
| `RubricDomains` (live) | 5 | **5** ✓ |
| `RubricStandards` (live) | 25 | **25** ✓ |
| Distribution | 6/4/6/3/6 | **6/4/6/3/6** ✓ |
| Codes | D1..D5 / D1-S1..D5-S6 | **D1..D5 / D1-S1..D5-S6** ✓ |
| Active version count | 1 | **1** ✓ |
| `UX_RubricVersion_Active` index exists, unique, filter `IsActive=1 AND IsDeleted=0` | yes | **yes** ✓ |
| `UX_RubricVersion_Active` blocks a 2nd active at DB level | yes | **yes** (Msg 2601 on direct UPDATE) ✓ |
| Copy-on-write creates new rows, v1 rows unchanged | yes | **yes** (DomainId 1→6, StandardId 1→26 for D1-S1; v1 rows preserved) ✓ |
| 6 endpoints in `/swagger` | yes | **6** ✓ |
| `GET /rubric/active` without token | 401 | **401** ✓ |
| `GET /rubric/active` with school_manager token (Rubric.View) | 200 | **200** ✓ |
| `POST /rubric/versions` with school_manager token (no Rubric.Manage) | 403 | **403** ✓ |
| `GET /rubric/score-scale` labels + thresholds match docs/09 exactly | yes | **yes** ✓ (5 scores + 6 levels verbatim) |

Frontend verification:

| Check | Expected | Actual |
|-------|----------|--------|
| `core/models/rubric.models.ts` exists, mirrors backend | yes | **yes** ✓ |
| `core/services/rubric.service.ts` exists, 6 methods | yes | **yes** ✓ |
| `features/rubric/rubric-viewer` exists, RTL Arabic, spinner | yes | **yes** ✓ |
| `features/rubric/rubric-editor` exists, inline-edit + reorder + notes | yes | **yes** ✓ |
| `/rubric` route gated by `Rubric.View` | yes | **yes** ✓ |
| `/rubric/edit` route gated by `Rubric.Manage` | yes | **yes** ✓ |
| `environment.ts` points to `http://localhost:5264` | yes | **yes** ✓ |
| `ar.json` / `en.json` strict JSON, identical key sets, RUBRIC.* merged (MOD-6) | yes | **yes** (151/151 leaf keys, zero duplicates) ✓ |
| Login + Phase 2 pages still render (no raw-key regression) | yes | verified (translation keys intact, login flow unchanged) ✓ |
| Prod build green | yes | **yes** (see STEP 5 acceptance below) ✓ |