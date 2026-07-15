# 09 — Rubric & Evaluation

**Status:** Phase 3 IMPLEMENTED + seeded baseline (5 domains / 25 standards verbatim) + dynamic visit snapshots · **Last updated:** 2026-07-15

## Structure
- The seeded baseline has **5 domains** and **25 standards**.
- Rubric versions are dynamic: a visit snapshots **N** standards from the active version; create, update, progress, and submit derive N from that snapshot and never hard-code 25 (D-65 / locked D2).
- Score from **0 to 4**.

## Score labels (exact Arabic)
| Score | Label |
|-------|-------|
| 0 | غير مشاهد |
| 1 | يحتاج تحسين |
| 2 | متحقق جزئياً |
| 3 | متحقق بدرجة جيدة |
| 4 | متميز |

## Performance levels (exact Arabic)
| Level | Threshold |
|-------|-----------|
| متميز | >= 3.5 |
| جيد جداً | >= 3.0 |
| جيد | >= 2.5 |
| متحقق جزئياً | >= 2.0 |
| يحتاج تحسين | >= 1.0 |
| غير مشاهد | < 1.0 |

## Analysis rules
- **Domain average** = average of standard scores in that domain.
- **Overall score (current historical behavior before desktop-parity Phase 2)** = average of all scored standards. Locked decision D1 changes the current rubric-version behavior to equal-weight domain averages in desktop-parity Phase 2 without mutating historical snapshots.
- **Strengths** = domains with average **>= 3.0**.
- **Improvement areas** = domains with average **< 2.5**.
- **Priority standards** = individual standards with score **<= 1.5**.

## Rubric versioning (required in new system)
- Main Manager can edit standards/domains.
- Every edit creates a new **RubricVersion**.
- Existing visits keep their original **RubricVersionId**.
- Old reports remain historically accurate.
- All schools use the same active rubric version for now.

> Not implemented in Phase 1. Keep architecture ready.
See [10-IMPROVEMENT-PLANS-AND-FOLLOWUPS.md](10-IMPROVEMENT-PLANS-AND-FOLLOWUPS.md) for how
weak domains (avg < 2.5) and priority standards (score <= 1.5) feed improvement plans.

---

## Phase 3 implementation status

- **5 domains** (D1 بيئة التعلم / D2 التدريس والتعلم / D3 تنمية المهارات / D4 التقويم / D5 سلوك المتعلمين) and **25 standards** (distribution 6/4/6/3/6) are **seeded** verbatim by `DatabaseSeeder.SeedRubricAsync` as RubricVersion 1, IsActive=true.
- Score labels and performance-level thresholds above are **exposed verbatim** by `GET /api/v1/rubric/score-scale` (see [05-API-ENDPOINTS.md](05-API-ENDPOINTS.md) Phase 3 section). Phase 4 analysis MUST consume that endpoint — do not hardcode values elsewhere.
- Rubric is **GLOBAL** (one platform-wide active version), enforced at the DB level by filtered unique index `UX_RubricVersion_Active` on `RubricVersions(IsActive)` filtered on `IsActive=1 AND IsDeleted=0`. See **D-21** in [14-DECISIONS-AND-DEVIATIONS.md](14-DECISIONS-AND-DEVIATIONS.md).
- Main Manager edits via `POST /api/v1/rubric/versions` create a new version via **copy-on-write** (new rows for every domain/standard, the previous active version is deactivated). Historical visit rows (Phase 4) keep pointing at their original `RubricVersionId` so old reports remain accurate.
