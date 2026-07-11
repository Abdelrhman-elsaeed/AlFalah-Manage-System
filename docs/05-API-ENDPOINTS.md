# 05 — API Endpoints

**Status:** Phase 6 Stage 1 (PDF Reports) added · **Last updated:** 2026-07-10

> **Base URL (dev):** `http://localhost:5264` · **Swagger:** `http://localhost:5264/swagger`

## Phase 1 endpoints
| Method | Path | Purpose | Auth |
|--------|------|---------|------|
| POST | `/api/v1/auth/school-login` | School user login (validates UserSchoolRole) | Anonymous |
| POST | `/api/v1/auth/main-manager-login` | Main Manager / Super Admin login (global) | Anonymous |
| POST | `/api/v1/auth/refresh` | Exchange/rotate refresh token for new JWT | Refresh token |
| POST | `/api/v1/auth/logout` | Revoke refresh token / end session | Authenticated |
| GET  | `/api/v1/auth/me` | Current user profile, roles, permissions, active school | Authenticated |
| POST | `/api/v1/auth/forgot-password` | Start password recovery (Identity token) | Anonymous |
| POST | `/api/v1/auth/reset-password` | Complete password reset | Anonymous (token) |
| GET  | `/api/v1/schools/for-login` | School lookup list for login dropdown | Anonymous |

> **Note:** the login-dropdown lookup is served by the existing route `GET /api/v1/auth/schools` (see **D-01** in [14-DECISIONS-AND-DEVIATIONS.md](14-DECISIONS-AND-DEVIATIONS.md)), functionally equivalent to the spec's `schools/for-login`. `forgot-password` returns the reset token in `data.resetToken` in **Development only**.

> All responses use the standard `ApiResponse<T>` shape (see
> [01-ARCHITECTURE.md](01-ARCHITECTURE.md)).

## Planned endpoints (future phases)
> High-level placeholders. Exact routes defined when each phase starts.

### Phase 2 — School & User Management ✅ DONE
See [phases/PHASE-02-SCHOOL-USER-MANAGEMENT.md](phases/PHASE-02-SCHOOL-USER-MANAGEMENT.md) for full contract.

| Method | Path | Purpose | Permissions |
|--------|------|---------|-------------|
| GET | `/api/v1/schools` | Paged schools list | `School.View` |
| GET | `/api/v1/schools/{id}` | School detail | `School.View` |
| POST | `/api/v1/schools` | Create school | `School.Create` |
| PUT | `/api/v1/schools/{id}` | Update school | `School.Edit` |
| DELETE | `/api/v1/schools/{id}` | Soft-delete school | `School.Delete` |
| POST | `/api/v1/schools/{id}/assign-manager` | Assign / replace School Manager | `School.Edit` |
| POST | `/api/v1/schools/{id}/activate` | Activate (blocked without manager) | `School.Edit` |
| POST | `/api/v1/schools/{id}/deactivate` | Deactivate | `School.Disable` |
| GET | `/api/v1/users` | Paged users list | `User.View` |
| GET | `/api/v1/users/{id}` | User detail | `User.View` |
| POST | `/api/v1/users` | Create user (SchoolManager/Moderator/Instructor) | `User.Create` |
| PUT | `/api/v1/users/{id}` | Update user | `User.Edit` |
| POST | `/api/v1/users/{id}/deactivate` | Soft-deactivate user | `User.Delete` |
| POST | `/api/v1/user-school-roles` | Assign user to school with role | `User.Edit` |
| DELETE | `/api/v1/user-school-roles/{id}` | Remove assignment | `User.Edit` |
| GET | `/api/v1/user-school-roles?schoolId=` | List assignments by school | `User.Edit` |

### Phase 3 — Rubric ✅ DONE
Rubric is **GLOBAL** — not school-scoped (see **D-21** in [14-DECISIONS-AND-DEVIATIONS.md](14-DECISIONS-AND-DEVIATIONS.md)).
All schools use the same active version. Only ONE active version at a time is allowed at the DB level
by the filtered unique index `UX_RubricVersion_Active` on `RubricVersions(IsActive)` filtered on
`IsActive=1 AND IsDeleted=0`. Editing creates a new version via copy-on-write (historical rows are never mutated).

| Method | Path | Purpose | Permissions |
|--------|------|---------|-------------|
| GET | `/api/v1/rubric/active` | Full tree (domains + standards) of the currently active version | `Rubric.View` |
| GET | `/api/v1/rubric/versions` | Lightweight list of all versions (no inline standards) | `Rubric.View` |
| GET | `/api/v1/rubric/versions/{id}` | Full tree for a specific version | `Rubric.View` |
| POST | `/api/v1/rubric/versions` | Create new version from a complete tree (copy-on-write) | `Rubric.Manage` |
| POST | `/api/v1/rubric/versions/{id}/activate` | Activate a version, deactivate all others | `Rubric.Manage` |
| GET | `/api/v1/rubric/score-scale` | Global 0–4 score labels + performance-level thresholds (verbatim from [09-RUBRIC-AND-EVALUATION.md](09-RUBRIC-AND-EVALUATION.md)) | `Rubric.View` |

Score scale returned by `GET /api/v1/rubric/score-scale`:

```json
{
  "isSuccess": true,
  "data": {
    "scores": [
      { "score": 0, "labelAr": "غير مشاهد" },
      { "score": 1, "labelAr": "يحتاج تحسين" },
      { "score": 2, "labelAr": "متحقق جزئياً" },
      { "score": 3, "labelAr": "متحقق بدرجة جيدة" },
      { "score": 4, "labelAr": "متميز" }
    ],
    "performanceLevels": [
      { "labelAr": "متميز",          "minScore": 3.5, "isLessThan": false },
      { "labelAr": "جيد جداً",        "minScore": 3.0, "isLessThan": false },
      { "labelAr": "جيد",             "minScore": 2.5, "isLessThan": false },
      { "labelAr": "متحقق جزئياً",    "minScore": 2.0, "isLessThan": false },
      { "labelAr": "يحتاج تحسين",     "minScore": 1.0, "isLessThan": false },
      { "labelAr": "غير مشاهد",       "minScore": 1.0, "isLessThan": true  }
    ]
  }
}
```

Phase 4 (Visits & Scoring) MUST use this endpoint as the source of truth for labels and thresholds.

### Phase 4 — Visits & Scoring ✅ DONE
Visits are **school-scoped** (see **D-24**): school-scoped callers can only read/mutate visits
within their JWT `active_school_id`; global admins (SuperAdmin, MainManager) bypass. On create
the visit snapshots the currently active `RubricVersionId` (see **D-21**). Analysis matches
docs/09 verbatim and is computed and persisted ONCE on submit (immutable in Phase 4).

| Method | Path | Purpose | Permissions |
|--------|------|---------|-------------|
| GET    | `/api/v1/visits`            | Paged visits list (filter by `status`, `instructorId`, `visitCategory`, `fromDate`, `toDate`; school-scope auto-enforced) | `Visit.View` |
| GET    | `/api/v1/visits/{id}`        | Full detail + 25 scores + analysis (if submitted) | `Visit.View` |
| POST   | `/api/v1/visits`             | Create draft (snapshots active rubric; pre-generates 25 empty `VisitScore` rows) | `Visit.Create` |
| PUT    | `/api/v1/visits/{id}`        | Update visit meta + upsert 25 scores (Draft only) | `Visit.Edit` |
| POST   | `/api/v1/visits/{id}/submit` | Validate 25/25 → `Draft → PendingApproval` + persist analysis snapshot | `Visit.Edit` |
| DELETE | `/api/v1/visits/{id}`        | Soft delete (Draft only) | `Visit.Delete` |
| GET    | `/api/v1/visits/{id}/analysis` | Analysis snapshot only; 404 if not submitted | `Visit.View` |

**Seeded visit permissions** (`DatabaseSeeder.GetRolePermissionMap`):

| Permission | SuperAdmin | MainManager | SchoolManager | Moderator | Instructor |
|------------|:----------:|:-----------:|:-------------:|:---------:|:----------:|
| Visit.View       | ✅ | ✅ | ✅ | ✅ | ✅ |
| Visit.Create     | ✅ | ✅ | ✅ | ✅ | — |
| Visit.Edit       | ✅ | ✅ | ✅ | ✅ | — |
| Visit.Delete     | ✅ | ✅ | ✅ | ✅ | — |
| Visit.Submit     | ✅ | ✅ | ✅ | ✅ | — |
| Visit.Approve    | ✅ | ✅ | ✅ | — | — |
| Visit.Reopen     | ✅ | ✅ | ✅ | — | — |

`Visit.View` is granted to **Instructor** in Phase 4 so they can see that a visit exists;
**seeing scores/analysis** is Phase 5 (instructor visibility of results).

**Analysis snapshot shape** (verbatim from docs/09):

```json
{
  "isSuccess": true,
  "data": {
    "id": 1, "visitId": 1,
    "overallScore": 3.6,
    "performanceLevelAr": "متميز",
    "strengths":         [ { "domainCode": "D1", "domainNameAr": "بيئة التعلم",      "averageScore": 4.0 } ],
    "improvementAreas":  [ { "domainCode": "D5", "domainNameAr": "سلوك المتعلمين",  "averageScore": 2.333 } ],
    "priorityStandards": [ { "domainCode": "D5", "standardCode": "D5-S4", "standardTextAr": "...", "score": 0 } ],
    "domainAverages":    [
      { "domainCode": "D1", "domainNameAr": "بيئة التعلم",       "averageScore": 4.0 },
      { "domainCode": "D2", "domainNameAr": "التدريس والتعلم",   "averageScore": 4.0 },
      { "domainCode": "D3", "domainNameAr": "تنمية المهارات",    "averageScore": 4.0 },
      { "domainCode": "D4", "domainNameAr": "التقويم",            "averageScore": 4.0 },
      { "domainCode": "D5", "domainNameAr": "سلوك المتعلمين",    "averageScore": 2.333 }
    ],
    "computedAt": "2026-07-10T14:47:25+00:00"
  }
}
```

### Phase 5 — Approval & Visibility ✅ DONE

State machine (enforced in `VisitService` — invalid transitions return Arabic 400):

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

- **Visit approval flow** (School Manager / SuperAdmin / MainManager only — school-scoped via `SchoolScopeGuard`):
  - `POST /api/v1/visits/{id}/approve` — `PendingApproval → Approved`. Sets `ApprovedByUserId` + `ApprovedAt`.
  - `POST /api/v1/visits/{id}/reject` body `{ "reason": "..." }` — `PendingApproval → RejectedForChanges`. `reason` is **required**.
  - `POST /api/v1/visits/{id}/reopen` body `{ "reason": "..." }` — `Approved → Reopened`. `reason` is **required**. Resubmit recomputes a NEW `VisitAnalysis` snapshot on the SAME `RubricVersionId`.
- **Direct edit path**: `PUT /api/v1/visits/{id}` while `PendingApproval` is allowed ONLY for the visit's School Manager / SuperAdmin / MainManager; Moderators cannot edit at this stage. After direct edit the SM calls `/approve`.
- **Instructor result visibility** (gated in service by `Status == Approved` AND `Visit.InstructorId == current user`):
  - `GET /api/v1/visits/{id}/report` — full result (visit meta + 25 scores + analysis snapshot). On success records a `ReportViewLog` row. Returns 403 otherwise.
- **Report view status** (manager / moderator):
  - `GET /api/v1/visits/{id}/view-status` — `{ hasBeenViewed, firstViewedAt, lastViewedAt, viewCount }`. Aggregated over all `ReportViewLog` rows for the visit.
- **Audit**: every approve / reject / direct-edit / reopen / edit-after-reject / edit-after-reopen / resubmit-after-reopen writes an `AuditLog` row (`Action`, `EntityName="Visit"`, `EntityId`, `OldValues`/`NewValues` JSON, `Reason`, `UserId`, `SchoolId`, `CreatedAt`, `IpAddress`).

| Method | Path | Purpose | Permissions |
|--------|------|---------|-------------|
| GET    | `/api/v1/visits`            | Paged visits list (filter by `status`, `instructorId`, `visitCategory`, `fromDate`, `toDate`; school-scope auto-enforced) | `Visit.View` |
| GET    | `/api/v1/visits/{id}`        | Full detail + 25 scores + analysis (if submitted) | `Visit.View` |
| POST   | `/api/v1/visits`             | Create draft (snapshots active rubric; pre-generates 25 empty `VisitScore` rows) | `Visit.Create` |
| PUT    | `/api/v1/visits/{id}`        | Update visit meta + upsert 25 scores. Allowed when Draft (Phase 4) **OR** RejectedForChanges (creator) **OR** Reopened (creator) **OR** PendingApproval (SM direct-edit only) | `Visit.Edit` |
| POST   | `/api/v1/visits/{id}/submit` | Validate 25/25 → `Draft → PendingApproval` OR `Reopened → PendingApproval` + persist / recompute analysis snapshot | `Visit.Edit` |
| DELETE | `/api/v1/visits/{id}`        | Soft delete (Draft only) | `Visit.Delete` |
| GET    | `/api/v1/visits/{id}/analysis` | Analysis snapshot only; 404 if not submitted | `Visit.View` |
| POST   | `/api/v1/visits/{id}/approve` | `PendingApproval → Approved`. Sets `ApprovedByUserId` + `ApprovedAt`. | `Visit.Approve` |
| POST   | `/api/v1/visits/{id}/reject`  | `PendingApproval → RejectedForChanges`. `reason` required (≤ 1000 chars). | `Visit.Approve` |
| POST   | `/api/v1/visits/{id}/reopen`  | `Approved → Reopened`. `reason` required (≤ 1000 chars). Resubmit recomputes a NEW snapshot. | `Visit.Reopen` |
| GET    | `/api/v1/visits/{id}/report`  | **Instructor-only**. Full result (scores + analysis). Status MUST be Approved AND InstructorId MUST equal current user. Records a `ReportViewLog`. Returns 403 otherwise. | `Visit.View` (instructor) — data-driven gate |
| GET    | `/api/v1/visits/{id}/view-status` | Manager / moderator aggregated view status (`hasBeenViewed`, `firstViewedAt`, `lastViewedAt`, `viewCount`). | `Visit.View` |

**Seeded visit permissions** (`DatabaseSeeder.GetRolePermissionMap`):

| Permission | SuperAdmin | MainManager | SchoolManager | Moderator | Instructor |
|------------|:----------:|:-----------:|:-------------:|:---------:|:----------:|
| Visit.View       | ✅ | ✅ | ✅ | ✅ | ✅ |
| Visit.Create     | ✅ | ✅ | ✅ | ✅ | — |
| Visit.Edit       | ✅ | ✅ | ✅ | ✅ | — |
| Visit.Delete     | ✅ | ✅ | ✅ | ✅ | — |
| Visit.Submit     | ✅ | ✅ | ✅ | ✅ | — |
| Visit.Approve    | ✅ | ✅ | ✅ | — | — |
| Visit.Reopen     | ✅ | ✅ | ✅ | — | — |

`Visit.View` is granted to **Instructor** in Phase 4 so they can see that a visit exists;
**seeing scores/analysis** is gated by `GET /api/v1/visits/{id}/report` which enforces
`Status == Approved` AND `Visit.InstructorId == current user` (Phase 5).

**State machine invalid-transition responses** (Arabic 400 with descriptive message):
- `Draft → Approved/Rejected/Reopened`     — "لا يمكن [الإجراء] في حالتها الحالية. يجب أن تكون ..."
- `Approved → Approved/Rejected`            — "لا يمكن ... يجب أن تكون معتمدة."
- `RejectedForChanges → Approved`          — "لا يمكن ... يجب أن تكون بانتظار الاعتماد." (must re-submit first)
- `Reopened → Approved`                     — "لا يمكن ... يجب أن تكون بانتظار الاعتماد." (must re-submit first)

### Phase 6 — Reports ✅ STAGE 1 + STAGE 2 DONE

**Stage 1 — server-side Arabic PDF (data only).**
**Stage 2 — official/branding layer on top of Stage 1** (school logo, school
branding, real Moderator + Manager signatures, informational QR code). Same
endpoint, same `application/pdf` response, same data-driven gate (D-24 /
D-28 / D-36 / D-37). Archive + export endpoints are deferred to a later
prompt. See [phases/PHASE-06-REPORTS.md](phases/PHASE-06-REPORTS.md) for the
full Stage 1 + Stage 2 spec (Arabic font embedding, snapshot fidelity
rules, content layout, branding layer, image-safety rules, fallback matrix).

| Method | Path | Purpose | Permissions |
|--------|------|---------|-------------|
| GET | `/api/v1/visits/{id}/report/pdf` | **Server-side Arabic PDF download** for an APPROVED visit (snapshot-driven; embedded Amiri font; RTL). **Stage 2 adds** the official/branding layer: school logo (or initials fallback), `SchoolReportSettings` header/footer text + primary color + flags, real Moderator + Manager signatures from `UserSignature` (or printed-name + dashed-line fallback), and an informational QR code (when `SchoolReportSettings.ShowQrCode = true`) encoding a compact reference (visit id + school id + short hash — NO scores / NO PII). Every external asset has a safe PDF fallback — a missing logo / signature / QR NEVER crashes the report. Returns `application/pdf` bytes (NOT ApiResponse). Records a `ReportViewLog`. | data-driven gate — See PHASE-06 §"Stage 1 — Endpoint" / §"Stage 2 — Endpoint" |

**Authorization (data-driven, no permission gate at the controller)** — mirrors
the existing `/report` endpoint pattern:

| Caller | Outcome |
|--------|---------|
| Status != Approved | `400` Arabic `لا يمكن إنشاء تقرير PDF لزيارة غير معتمدة.` |
| Visit not found / soft-deleted | `404` Arabic `الزيارة غير موجودة.` |
| Unauthenticated | `401` |
| Instructor — own approved visit | `200` + PDF + `ReportViewLog` written |
| Instructor — other instructor's visit | `403` Arabic |
| School Manager — visit in HIS school | `200` + PDF |
| School Manager — cross-school | `403` Arabic (UnauthorizedSchoolAccessException) |
| Moderator — visit HE created | `200` + PDF |
| Moderator — cross-moderator | `403` Arabic (D-37: `لا تملك صلاحية الوصول إلى زيارات المشرفين الآخرين في مدرستك.`) |
| SuperAdmin / MainManager | `200` + PDF (global) |

**Stage 2 (NOT STARTED, separate prompt)** — school logo, real
`UserSignature` image, QR code, branding polish, `ReportArchive` entity +
listing endpoint.

### Phase 7 — Improvement Plans & Follow-ups ✅ IMPLEMENTED

All endpoints under `/api/v1`, all `ApiResponse<T>`, all async + `CancellationToken`, all `[Authorize]`.
All write endpoints gate the **service** behind `SchoolScopeGuard` + Moderator own-only (D-37) + Instructor blocked (D-36).

| Method | Route | Permission | Description |
|---|---|---|---|
| GET | `/visits/{visitId}/improvement-plans` | `Plan.View` | List all plans (incl. follow-ups) for a visit |
| GET | `/visits/{visitId}/weak-domains-suggestions` | `Plan.View` | Weak-domain suggestions (avg < 2.5) with verbatim Arabic prefilled templates |
| GET | `/improvement-plans/{id}` | `Plan.View` | Get a single plan by id |
| POST | `/improvement-plans` | `Plan.Create` | Create plan (default status = active, soft-delete false) |
| PUT | `/improvement-plans/{id}` | `Plan.Edit` | Update plan fields (editable in any status) |
| DELETE | `/improvement-plans/{id}` | `Plan.Delete` | Soft-delete plan (cascades soft-delete to its follow-ups; rows survive in DB) |
| POST | `/improvement-plans/{id}/follow-ups` | `Plan.Edit` | Add follow-up (ProgressNote required; ProgressScore optional 0..100; EvidenceNote optional) |
| PUT | `/follow-ups/{id}` | `Plan.Edit` | Update follow-up |
| DELETE | `/follow-ups/{id}` | `Plan.Delete` | Soft-delete follow-up |
| GET | `/improvement-plans/{id}/progress` | `Plan.View` | Latest progress (score + color) + chronological chart data (only if ≥2 scored follow-ups) |

**Visibility matrix (cross-school → 403 / not-found → 404):**
| Role | View | Create | Edit | Delete |
|---|---|---|---|---|
| SuperAdmin / MainManager | ✅ all | ✅ | ✅ | ✅ |
| SchoolManager | ✅ own school | ✅ | ✅ | ✅ |
| Moderator (creator) | ✅ own visits | ✅ | ✅ | ✅ |
| Moderator (NOT creator) | ❌ 403 | ❌ 403 | ❌ 403 | ❌ 403 |
| Instructor (own approved visit) | ✅ view-only | ❌ 403 | ❌ 403 | ❌ 403 |

### Phase 8 — Complaints
- Instructor complaint, visibility rules, reopen from complaint

### Phase 9 — Dashboards & Exports
- Role dashboards, Excel/PDF exports, filters
