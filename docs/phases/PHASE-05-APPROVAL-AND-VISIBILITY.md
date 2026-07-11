# Phase 5 — Approval & Instructor Visibility

**Status:** COMPLETED ✅ (D-36 closed 2026-07-10; D-37 closed 2026-07-10) · **Last updated:** 2026-07-10

## Goal
School Manager approval workflow and controlled instructor visibility.

## Scope
### In
- School Manager approval (approve / edit directly / return to moderator for changes / reject)
- Instructor result visibility **only after approval**
- Report view tracking (ReportViewLog when instructor views)
- Reopen visit (store reason)
- Audit on changes
- D-36 close (2026-07-10) — backend hardening + frontend wiring so the
  instructor can never reach scores/analysis via the manager endpoint, and
  `ReportViewLog` rows are written on every successful instructor view.

### Out
- PDF reports (Phase 6), complaints (Phase 8)

## Rules
- Instructor cannot see the result until the School Manager approves.
- After approval, Instructor can view the result.
- When Instructor views the report, create a **ReportViewLog**.
- Visit can be reopened and edited even after approval; every reopen/edit is audited.
- D-36 (security): the gate that hides scores/analysis from a non-approved
  Instructor is enforced **server-side in `VisitService.GetByIdAsync`**, not
  in the UI. The dedicated `GET /api/v1/visits/{id}/report` endpoint is the
  only path that returns the full result to an Instructor — and it is the
  only path that writes a `ReportViewLog` row.

## State machine (Phase 5 — verbatim)

```
        submit         approve (SM)
Draft ─────────► PendingApproval ─────────► Approved
                       │                       │ reopen (SM, reason)
                       │ reject (SM, reason)   ▼
                       └─────────────────► RejectedForChanges
                              (creator edits)         Reopened
                                   │ resubmit           │ resubmit (creator) ── recomputes NEW snapshot
                                   ▼                    ▼
                              PendingApproval ◄──── PendingApproval
```

| From | To | Action | Who | Reason required |
|------|----|--------|-----|-----------------|
| Draft | PendingApproval | submit | Creator | No |
| PendingApproval | Approved | approve | School Manager (or SuperAdmin/MainManager) | No |
| PendingApproval | RejectedForChanges | reject | School Manager (or SuperAdmin/MainManager) | **Yes** |
| PendingApproval | Approved | direct-edit + approve | School Manager (or SuperAdmin/MainManager) | No |
| Approved | Reopened | reopen | School Manager (or SuperAdmin/MainManager) | **Yes** |
| Reopened | PendingApproval | resubmit | Creator | No (recomputes analysis on same RubricVersionId) |
| RejectedForChanges | PendingApproval | edit + resubmit | Creator | No |

Invalid transitions throw `InvalidOperationException` → HTTP 400 with Arabic
`ApiResponse` error.

## Approval rules

- Only the **School Manager** of the visit's school (or SuperAdmin/MainManager)
  can approve / reject / direct-edit / reopen.
- **Moderator** can edit + resubmit only when status = RejectedForChanges or Reopened.
- School-scope enforced via `SchoolScopeGuard`; cross-school access returns 403.
- ActiveSchoolId enforced — no cross-school approval.
- `Approved → Reopened → resubmit` recomputes a **NEW** `VisitAnalysis` snapshot on the
  **SAME** `RubricVersionId` (historical visits keep their original rubric version —
  no switch to a newer rubric version).

## Instructor visibility (the core of Phase 5)

- Instructor sees the visit's full result (scores + analysis snapshot: domain averages,
  overall, performance level, strengths/improvement/priority) **ONLY** when:
  - `status = Approved` AND
  - `visit.InstructorId == current user`.
- Not before approval, never for other instructors, never another school.
- When the Instructor opens the approved report, a `ReportViewLog` row is created
  (record every view; expose "first viewed / last viewed / count" via the
  view-status endpoint).
- Instructor cannot edit anything (enforced in backend, not just UI).

## Audit

Every change (approve / reject / edit-after-reject / reopen / direct-edit / resubmit)
writes an `AuditLog` row:
- `Action` (e.g. `Visit.Approve`, `Visit.Reject`, `Visit.Reopen`, `Visit.Edit`, `Visit.Resubmit`)
- `EntityName = "Visit"`
- `EntityId = Visit.Id`
- `OldValues` / `NewValues` JSON (status before/after + scores / approval metadata)
- `Reason`
- `UserId`
- `SchoolId`
- `CreatedAt`
- `IpAddress` (best-effort from `X-Forwarded-For` or `Connection.RemoteIpAddress`)

## Acceptance criteria

- Instructor visibility gated by approval. ✅
- Reopen requires a reason and is audited. ✅
- Approve / Reject / Reopen return Arabic 400 with descriptive message on invalid state. ✅
- Approve sets `Approved` + approver + timestamp; Instructor then sees full result. ✅
- Reject stores reason, reopens editing for creator, requires 25/25 re-submit. ✅
- Reopen (Approved → Reopened) requires reason, audits, and resubmit recomputes a new
  snapshot on the same RubricVersionId. ✅
- `ReportViewLog` created on Instructor view; view-status visible to manager/moderator. ✅
- School-scoping: no cross-school approve/reject/reopen/view (403). ✅
- Global admin (SuperAdmin / MainManager) bypass; no D-24 / D-28 regression. ✅
- Every approve / reject / edit / reopen / resubmit writes an `AuditLog` row. ✅
- All new endpoints in Swagger (`http://localhost:5264/swagger`). ✅
- Permissions enforced (401 / 403). ✅
- Frontend flows work end-to-end (SM approves → Instructor sees result → view logged). ✅
- No D-19 regression (ar/en key parity). ✅
- D-36 close (2026-07-10) — backend `GetByIdAsync` rejects instructor cross-record access (403) and strips scores/analysis when status != Approved (safe pending payload); frontend `visit-detail` calls `/report` for instructors and `getById` for managers/moderators/admins. ✅
- D-36 close — view-status increments as instructors view reports (live-verified: 0 → 2 → 5 across test runs). ✅
- D-37 close (2026-07-10) — backend `VisitService` enforces Moderator own-visits-only: list query filters `Where(v => v.CreatedByUserId == currentUserId)` for Moderator-only callers; detail / analysis / view-status endpoints call `EnsureModeratorCanAccessCreatedByVisit` and return 403 Arabic when the visit was created by another user. School Manager / Super Admin / Main Manager / Instructor behaviour unchanged (no D-24/D-28/D-36 regression). Live-verified with two moderators in the same school. ✅

## D-36 close verification (2026-07-10)

| # | Scenario | Endpoint | Caller | Expected | Observed |
|---|---|---|---|---|---|
| 1 | Own Approved visit | `GET /api/v1/visits/5/report` | Instructor `nasser_unicode_test` | 200 + 25 scores + analysis + 1 new `ReportViewLog` row | **PASS** — `scores=25, analysis.overallScore=4.0`, `ReportViewLogs` +1 (with `IpAddress=::1`, `ViewedAt=UTC`) |
| 2a | Own non-approved (Draft) | `GET /api/v1/visits/3` | Instructor | 200 + `scores=0, analysis=null` | **PASS** |
| 2b | Another instructor's visit | `GET /api/v1/visits/4` | Instructor | 403 Arabic | **PASS** — "لا تملك صلاحية الوصول إلى تقارير المعلمين الآخرين" |
| 2c | Cross-school visit | `GET /api/v1/visits/2` | Instructor (school 1) | 404 | **PASS** — "الزيارة غير موجودة" (school-scope guard) |
| 2d | Own Approved via manager endpoint | `GET /api/v1/visits/5` | Instructor | 200 + full detail, but **no log** | **PASS** — `scores=25, analysis=yes`, `ReportViewLogs` delta = 0 (manager endpoint must NOT log) |
| 3a | Non-approved via `/report` | `GET /api/v1/visits/3/report` | Instructor | blocked | **PASS** — 403 |
| 3b | Other instructor via `/report` | `GET /api/v1/visits/4/report` | Instructor | 403 | **PASS** |
| 3c | Cross-school via `/report` | `GET /api/v1/visits/2/report` | Instructor | blocked | **PASS** — 404 |
| 3d | Own Approved via `/report` | `GET /api/v1/visits/5/report` | Instructor | 200 + log +1 | **PASS** |
| 4a | SchoolManager own-school Approved | `GET /api/v1/visits/5` | SchoolManager | 200 + full detail | **PASS** (no regression) |
| 4b | Moderator own-school Approved (own visit) | `GET /api/v1/visits/5` | Moderator (visit creator) | 200 + full detail | **PASS** — D-37: only if `Visit.CreatedByUserId == currentUserId` |
| 4c | SuperAdmin Approved | `GET /api/v1/visits/5` | SuperAdmin | 200 + full detail | **PASS** |
| 4d | SchoolManager cross-school | `GET /api/v1/visits/2` | SchoolManager | blocked | **PASS** — 404 (D-24/D-28 intact) |
| 4e | SchoolManager own-school Rejected | `GET /api/v1/visits/1` | SchoolManager | 200 + full detail (manager always sees everything) | **PASS** |
| 5 | View-status increments | `GET /api/v1/visits/5/view-status` | SchoolManager / Moderator (visit creator) | `viewCount` increments per instructor view | **PASS** — 0 → 2 → 5 across test runs |

## D-37 close verification (2026-07-10)

Two moderators seeded in school 1: `moderator_1` (existing) and `moderator_2`
(created via `POST /api/v1/users` as `SchoolManager`). Three visits used:
visit 1005 (MOD1-created, approved), visit 1006 (SM-created, approved),
visit 1007 (MOD2-created, draft).

| # | Scenario | Endpoint | Caller | Expected | Observed |
|---|---|---|---|---|---|
| 1a | MOD1 list | `GET /api/v1/visits` | MOD1 | only MOD1 visits | **PASS** — `[5, 1005]` (excludes 1006 SM-created + 1007 MOD2-created) |
| 1b | MOD2 list | `GET /api/v1/visits` | MOD2 | only MOD2 visits | **PASS** — `[1007]` (excludes 5, 1005 MOD1 + 1006 SM) |
| 1c | MOD2 GET MOD1's visit | `GET /api/v1/visits/1005` | MOD2 | 403 | **PASS** — "لا تملك صلاحية الوصول إلى زيارات المشرفين الآخرين في مدرستك." |
| 1d | MOD2 GET own visit | `GET /api/v1/visits/1007` | MOD2 | 200 + full detail | **PASS** |
| 2a | MOD1 GET SM-created visit | `GET /api/v1/visits/1006` | MOD1 | 403 | **PASS** |
| 2b | MOD2 GET SM-created visit | `GET /api/v1/visits/1006` | MOD2 | 403 | **PASS** |
| 2c | MOD1 GET SM visit analysis | `GET /api/v1/visits/1006/analysis` | MOD1 | 403 | **PASS** |
| 2d | MOD1 GET SM visit view-status | `GET /api/v1/visits/1006/view-status` | MOD1 | 403 | **PASS** |
| 3  | SM list | `GET /api/v1/visits` | SM | all visits in school (own + all moderators') | **PASS** — `[1, 3, 4, 5, 1005, 1006]` (totalCount=6) |
| 3b | SM GET MOD1 visit 1005 | `GET /api/v1/visits/1005` | SM | 200 + full detail | **PASS** |
| 3c | SM GET MOD2 visit 1007 | `GET /api/v1/visits/1007` | SM | 200 + full detail | **PASS** |
| 4  | SA list | `GET /api/v1/visits` | SA | all visible visits across schools | **PASS** — totalCount=7 (matches SQL count); visible ids `[1, 3, 4, 5, 1005, 1006]` (visit 2 belongs to soft-deleted school 9 → pre-existing soft-delete behaviour, NOT a D-37 regression) |
| 4b | SA per-visit GET | `GET /api/v1/visits/{1,3,4,5,1005,1006}` | SA | 200 each | **PASS** |
| 5a | Instructor own approved | `GET /api/v1/visits/5/report` | Instructor | 200 + 25 scores + log +1 | **PASS** (D-36 intact) |
| 5b | Instructor own draft | `GET /api/v1/visits/3` | Instructor | 200 with `scores=0, analysis=null` | **PASS** |
| 5c | Instructor other | `GET /api/v1/visits/4` | Instructor | 403 | **PASS** |
| 5d | Instructor /report draft | `GET /api/v1/visits/3/report` | Instructor | 403 | **PASS** |
| 6a | MOD1 cross-school | `GET /api/v1/visits/2` | MOD1 | 404 | **PASS** (D-24 intact) |
| 6b | SM cross-school | `GET /api/v1/visits/2` | SM | 404 | **PASS** (D-24/D-28 intact) |
| 6c | SM list excludes cross-school | `GET /api/v1/visits` | SM | excludes visit 2 | **PASS** |
| 7  | i18n ar/en parity | leaf-key comparison | — | 274/274, no missing | **PASS** |

i18n parity unchanged at **274/274 leaf keys** (no D-19 regression). No new
migration (no schema change). `dotnet build` 0/0; `ng build --prod` green.

## Dependencies
Phase 4 (visits & scoring).