# Phase 7 — Improvement Plans & Follow-ups

**Status:** COMPLETED ✅ · **Last updated:** 2026-07-15

## Goal
Reproduce the old desktop system's improvement-plan and follow-up logic exactly.

## Scope
### In
- Suggestion chips (auto from weak domains)
- Plan CRUD
- Follow-up CRUD
- Latest progress
- Progress chart
- Permissions
### Out
- Complaints (Phase 8)

## Key definitions
- Weak domain: domain average **< 2.5**.
- Priority standard: score **<= 1.5**.
- Default dates: StartDate = today, EndDate = today + 2 months.

## Reference (must follow exactly)
See [../10-IMPROVEMENT-PLANS-AND-FOLLOWUPS.md](../10-IMPROVEMENT-PLANS-AND-FOLLOWUPS.md)
for the full field lists, validation, the five Arabic suggestion templates, the
fallback template, and follow-up logic (progress colors 75/50, chart rules).

## Acceptance criteria
- Suggestion fills Goal/Actions/SuccessIndicators/DomainId/dates; user saves manually.
- Old behaviors preserved (multiple plans per visit/domain, edit regardless of status, etc.).

## Dependencies
Phase 4/5 (visit analysis & approval), Phase 3 (domains).

## Acceptance evidence (2026-07-11)

### Backend (✅ 0 errors / 0 warnings)
- 2 entities: `ImprovementPlan`, `PlanFollowUp` (Arabic_CI_AS on text cols, soft-delete + audit fields, navigations)
- 2 EF configurations with composite indexes (incl. `IX_ImprovementPlans_SchoolId_Status`)
- DbSets + global soft-delete query filters + auto-UpdatedAt on SaveChanges (in `AlFalahDbContext`)
- 10 DTOs in `backend/AlFalah.Application/DTOs/ImprovementPlanDtos.cs`
- `IImprovementPlanService` + `ImprovementPlanService` with **5 verbatim Arabic templates** + fallback (verified byte-for-byte against docs/10 §Suggestion templates)
- 4 FluentValidation validators (no EndDate>=StartDate rule, only non-blocking warning)
- `ImprovementPlansController` with **10 endpoints**, all thin, `ApiResponse<T>`, async + CT
- DI registration (`IImprovementPlanService → ImprovementPlanService` scoped)
- Seeder grants `Plan.View/Create/Edit/Delete`: SchoolManager + Moderator + SuperAdmin + MainManager = full; Instructor = `Plan.View` only (D-36/D-37 enforced in service)
- Migration `Phase7ImprovementPlans` (20260711131958) **applied** — 2 tables, 7 indexes, 6 FKs

### Frontend (✅ ng build --prod green)
- `core/models/improvement-plan.models.ts` (all interfaces, nullable progressScore / evidenceNote handled)
- `core/services/improvement-plans.service.ts` (10 methods matching controller)
- `app.routes.ts` — `/visits/:visitId/improvement-plans` and `/improvement-plans/:id` both `permissionGuard` with `Plan.View`
- `plan-list.component` — weak-domain chips prefill Goal/Actions/SuccessIndicators/DomainId + StartDate=today + EndDate=today+2months on click; plans `p-table` with status tags + row actions; **non-blocking warning** if `EndDate < StartDate` (save NOT blocked); status filter dropdown
- `plan-detail.component` — plan info card; follow-ups list ordered by FollowDate DESC with add/edit/delete; latest-progress badge colored by thresholds (≥75 green, ≥50&<75 gold, <50 red); **chronological line `p-chart`** shown only if ≥2 scored follow-ups
- `visit-detail.component` — "خطط التحسين" button (permission-filtered via `canViewPlans()` = `Plan.View`) navigates to `/visits/{id}/improvement-plans`
- i18n: `PLANS.*` + `FOLLOWUPS.*` merged into `ar.json` + `en.json` — D-19 parity preserved (16/16 top-level keys, no duplicates)

### Visibility matrix (verified by code inspection against `GlobalExceptionMiddleware`)
| Role | Create | Edit | Delete | View own | View other-in-school | View cross-school |
|---|---|---|---|---|---|---|
| SuperAdmin / MainManager | 200 | 200 | 200 | n/a | n/a | 200 |
| SchoolManager | 201 | 200 | 200 | 200 | 200 | 403 |
| Moderator (creator) | 201 | 200 | 200 | 200 | 403 | 403 |
| Moderator (NOT creator) | 403 | 403 | 403 | 403 | 403 | 403 |
| Instructor | 403 | 403 | 403 | 200 (own approved only) | 403 | 403 |

Error mapping: `UnauthorizedSchoolAccessException` → **HTTP 403**, `KeyNotFoundException` → **HTTP 404**, missing-permission → **HTTP 403** with Arabic ApiResponse.

### Worked example (end-to-end)
1. Visit has weak domain بيئة التعلم (avg < 2.5) — chip appears in plan-list header.
2. Click chip → dialog opens prefilled with the **verbatim Arabic template** + `DomainId` + `StartDate=today` + `EndDate=today+2 months`.
3. User clicks "حفظ" → POST /api/v1/improvement-plans → 201 Created with full plan DTO.
4. Add follow-up #1 with `ProgressScore=60` (today) → POST .../follow-ups → 201.
5. Add follow-up #2 with `ProgressScore=80` (later) → 201.
6. GET /api/v1/improvement-plans/{id}/progress → `latestProgressScore=80`, `latestProgressColor=success` (≥75), `chartData` = 2 points in chronological FollowDate ASC order.
7. UI renders the latest-progress badge GREEN ("ممتاز / مكتمل") and the chronological line chart.

### Old behavior preserved
- ✅ Multiple plans per visit/domain (no uniqueness on `VisitId + DomainId`)
- ✅ Editable in any status (active / completed / cancelled)
- ✅ Follow-ups addable in any plan status
- ✅ NO EndDate>=StartDate block (warning only — per docs/10 §Plan validation)
- ✅ Plan soft-delete: rows survive in DB with `IsDeleted=true`; on plan soft-delete the service cascades soft-delete to its follow-ups for consistency (no hard delete)

### Build evidence
- `dotnet build AlFalah.slnx` → Build succeeded, 0 Warning(s), 0 Error(s) (Time Elapsed ~5–7s)
- `cd frontend && npm run build` → Application bundle generation complete (15.7s); only 3 unrelated PrimeFlex library CSS warnings (organizationchart selector parser)

## Whole-app UI completion verification (2026-07-15)

- Plan and follow-up templates contain no hard-coded user-facing Arabic copy;
  all labels, status text, dialogs, confirmations, validation, and toast messages
  resolve through Arabic/English i18n.
- Direct dropdowns were replaced by `app-clearable-select`, and all date fields
  use PrimeNG calendars. Buttons, tags, and page styling consume the shared Saudi
  theme tokens and preserve RTL.
- D-70 verified: plan create/update closes the dialog and reloads plans; delete
  reloads plans. D-71 verified: follow-up create/update closes the dialog and
  reloads plan/follow-ups/progress; delete reloads the same aggregate.
- Static repository scans find no native selects, no direct feature-level
  `p-dropdown`, and no native date inputs. D-19 parity is 623/623 leaf keys with
  no duplicate top-level keys and no missing literal translation keys.
