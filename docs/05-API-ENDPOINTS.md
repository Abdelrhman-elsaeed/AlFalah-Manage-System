# 05 — API Endpoints

**Status:** Phase 6 Stage 1 (PDF Reports) added · **Last updated:** 2026-07-15

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

### Classroom Visits V2 — global default

All JSON endpoints use `ApiResponse<T>`; CSV/PDF endpoints return binary files. Every operation is backend school-scoped. Moderator lists/details are creator-only, and Instructor access is own + Approved only. V2 uses the frozen rubric version 2 (5 domains, 25 standards, 66 indicators) and persists rule-set-2 analyses and treatment snapshots.

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/v2/visits/availability` | Resolve global/per-school feature availability. |
| GET | `/api/v2/visits/observation-card` | Frozen V2 rubric, indicators, default score 1, and labels. |
| POST | `/api/v2/visits` | Create school-scoped draft with 25 default score rows. |
| PUT | `/api/v2/visits/{id}` | Atomically update metadata, scores, evidence, and observed indicators. |
| POST | `/api/v2/visits/{id}/finalize` | Persist the exact V2 analysis/treatments and enter the retained approval workflow. |
| GET | `/api/v2/visits` | Scoped, searched, filtered, paged archive plus evaluator filter values. |
| GET | `/api/v2/visits/{id}` | Authorized detail/report; Instructor reads record `ReportViewLog`. |
| DELETE | `/api/v2/visits/{id}` | Individual soft delete with child soft-delete behavior. |
| POST | `/api/v2/visits/{id}/approve` | Transactional V2 manager approval with audit fields. |
| POST | `/api/v2/visits/{id}/reject` | Transactional V2 reason-required rejection with audit fields. |
| POST | `/api/v2/visits/{id}/reopen` | Transactional V2 reason-required reopen with audit fields. |
| PUT | `/api/v2/visits/{id}/treatment-recommendations` | Persist visit-owned treatment-plan edits. |
| GET | `/api/v2/visits/dashboard` | SQL-side V2 KPIs, domain averages, and top/bottom standards. |
| GET | `/api/v2/visits/export/csv` | Exact 22-column UTF-8 BOM CSV. |
| GET | `/api/v2/visits/export/zip` | Scoped bulk ZIP of V2 PDFs; batch projections/assets avoid N+1 and do not call V1. |
| GET | `/api/v2/visits/{id}/report/pdf` | Arabic QuestPDF report with school branding and persisted signature fallbacks. |

JSON backup/import and whole-archive deletion are intentionally absent.

After the 2026-09-22 cutover, `/api/v1/visits` is a historical reader restricted to `ExperienceVersion.Legacy`. Every V1 create/update/submit/approve/reject/reopen/delete request returns `410 Gone` with a safe `ApiResponse` and a warning log. The V1 mutation descriptions below are retained only as historical design documentation and are no longer executable.

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
| GET    | `/api/v1/visits/my-approved-reports` | Instructor-only own + Approved report list. | Instructor role; server-scoped |

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

Instructor has no generic supervisor visit permission. The Phase 5 dedicated
report feed and report endpoint enforce own + Approved visibility in the backend.

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
  - `GET /api/v1/visits/my-approved-reports` — paged list of the caller's own
    Approved reports only; filter parameters cannot widen it.
  - `GET /api/v1/visits/{id}/report` — full result (visit meta + 25 scores + analysis snapshot). On success records a `ReportViewLog` row. Returns 403 otherwise.
- **Supervisor surface exclusion:** Instructor-only callers receive 403 from the
  generic list/detail/analysis/view-status/ZIP-export endpoints, even if a stale
  token still carries a former `Visit.View` claim.
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

Instructor uses `GET /api/v1/visits/my-approved-reports` and
`GET /api/v1/visits/{id}/report`; both enforce `Status == Approved` AND
`Visit.InstructorId == current user` in the backend. Generic supervisor visit
endpoints reject an Instructor-only caller with 403.

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

### Phase 8 — Complaints ✅ IMPLEMENTED

All operations use `ApiResponse<T>`, async service methods, `CancellationToken`,
and service-level scope checks. Main Manager and Moderator receive 403 on every
complaint operation even if a stale permission reaches the controller.

| Method | Route | Permission / scope | Description |
|---|---|---|---|
| POST | `/api/v1/visits/{visitId}/complaints` | `Complaint.Create`; Instructor-own, Approved, viewed report | Submit complaint/review request after `ReportViewLog` exists. |
| GET | `/api/v1/complaints` | `Complaint.View`; School Manager active school / SuperAdmin support | List scoped complaints; optional status filter. |
| GET | `/api/v1/complaints/{id}` | Same scope | Get complaint details. |
| PUT | `/api/v1/complaints/{id}/status` | `Complaint.Manage`; School Manager active school / SuperAdmin | Apply the complaint status state machine. |
| POST | `/api/v1/complaints/{id}/reopen-visit` | `Complaint.Manage` + `Visit.Reopen` | Reuse the visit reopen workflow and persist the linked reason/audit. |
| DELETE | `/api/v1/complaints/{id}` | `Complaint.Delete`; School Manager / SuperAdmin | Soft-delete the complaint. |

### Teacher Teaching Profile Enhancement (additive)

> This completed enhancement is independent of Phase 8 and does not alter its
> complaint scope.

Teacher create/edit continues to use `POST /api/v1/users` and
`PUT /api/v1/users/{id}` with `Role = Instructor`. The extended request and
detail shapes carry `FullName`, `EmployeeNumber`, `SchoolId`, `Subject`,
`Stage`, `PhoneNumber`, and `Email`. Class labels are intentionally excluded
from this form and are maintained in the teacher profile or self-only account
settings. `SchoolManager` is forced to their `ActiveSchoolId`; global roles
may choose a school. All school changes and reads are enforced server-side
through `SchoolScopeGuard`.

| Method | Route | Permission / scope | Description |
|---|---|---|---|
| GET | `/api/v1/teachers` | `Instructor.View`, school-scoped | Teacher-only directory. Moderator receives only active Instructors in `ActiveSchoolId`; this does not expose `/users`. |
| GET | `/api/v1/teachers/{userId}` | `Instructor.View`, school-scoped | Teacher profile header. Cross-school teacher is 403. |
| GET | `/api/v1/teachers/{userId}/visits` | `Visit.View`, school-scoped + D-37 | Moderator sees only visits they created for that teacher. |
| GET | `/api/v1/teachers/{userId}/progress` | `Visit.View`, school-scoped + D-37 | Approved-visit radar data on dynamic active-rubric axes plus chronological first-to-latest domain deltas; Moderator receives only visits they created. |

| Method | Route | Permission / scope | Description |
|---|---|---|---|
| GET | `/api/v1/account/teaching` | Authenticated Instructor, self-only | Current teacher's subject, stage, and class labels. |
| PUT | `/api/v1/account/teaching` | Authenticated Instructor, self-only | Save own subject and class labels; never accepts another user id. |
| GET | `/api/v1/teachers/{userId}/teaching` | `Visit.Create`, school-scoped | Teaching payload for manager edit and visit-form auto-fill. Any visit creator may read an in-scope Instructor's subject/classes; cross-school remains 403. |
| PUT | `/api/v1/teachers/{userId}/teaching` | `User.Edit`, school-scoped | Save class labels from the manager-facing teacher profile (and the teaching payload when needed) for an in-scope teacher. Cross-school is 403. |

The visit form calls the scoped teacher GET after choosing an Instructor. It
sets `Visit.Subject` from `SubjectSpecialization` as read-only when present and
offers only that teacher's `InstructorClass` labels for `Visit.GradeClass`.
When either value is missing, free-text entry remains available; no visit or
scoring workflow is blocked or changed.

### Phase 9 — Dashboards & Exports ✅ IMPLEMENTED

All dashboard reads return `ApiResponse<T>` and all queries are scoped in
`DashboardService`. File exports return their native binary response.

| Method | Route | Scope |
|---|---|---|
| GET | `/api/v1/dashboard/main-manager` | Global Main Manager/Super Admin metrics; no complaint data |
| GET | `/api/v1/dashboard/school-manager` | Caller `ActiveSchoolId`; school complaint count allowed |
| GET | `/api/v1/dashboard/moderator` | Caller school + `CreatedByUserId == currentUserId`; no complaints (D-37/D-75) |
| GET | `/api/v1/dashboard/instructor` | Current Instructor + Approved visits in token `ActiveSchoolId`; cross-school filter is coerced (D-24/D-36) |
| GET | `/api/v1/dashboard/export/excel?role=` | Same role service/scope as the selected dashboard; `.xlsx` |
| GET | `/api/v1/dashboard/export/pdf?role=` | Same role service/scope as the selected dashboard; PDF |

Supported narrowing parameters are `academicYear`, `semester`, `schoolId`,
`subject`, `stage`, `moderatorUserId`, `fromDate`, and `toDate` where applicable.
School-scoped callers cannot widen scope with query parameters.

## Intelligent Timetable Phase 02 — timings (2026-09-06)

All timing endpoints are school-scoped through the authenticated active school and return `ApiResponse<T>`. Read requires timetable view access; mutations require `Timetable.Manage` or the existing explicit editor grant, with Instructor/Guardian exclusions. Validation, forbidden, missing-resource and stale-revision responses use HTTP 400, 403, 404 and 409.

| Method | Route | Contract |
|---|---|---|
| GET | `/api/v1/intelligent-timetable/timings?academicYearId=…&semester=…` | Latest active templates with default/day periods and selected setup IDs |
| POST | `/api/v1/intelligent-timetable/timings` | Create template plus initial timing revision |
| PUT | `/api/v1/intelligent-timetable/timings/{id}` | Append timing revision; body includes expected `revision` |
| PUT | `/api/v1/intelligent-timetable/timings/profiles/{profileId}/selection` | `{ templateId, profileRevision }` selects the scoped template |

Save bodies include `academicYearId`, `semester`, `name`, `revision`, `schoolTimeZoneId`, `defaultPeriods`, and all seven `days`. Periods contain `sequence`, optional `displayLabel`, `startLocalTime`, and `endLocalTime` (unambiguous time-only strings). Days contain `day`, `isStudyDay`, `usesDefaultSchedule`, and `periods`; inherited/holiday days carry empty period arrays.

Timetable settings overview now includes `bellSchedule`. `POST /api/v1/timetables` accepts optional `timetableSetupProfileId`; specify it when the scope has more than one selected timing template. Timetable responses include the pinned `bellSchedule` and `timingsRequireRevalidation`. Existing save/publication/restore/export endpoints use per-day timing definitions. Teacher context and gate-pass resolution use the published revision and the requested instant in its school timezone.

## School File Storage S1 — delegation (2026-10-03)

Authenticated, selected-school routes; actual manager plus live DB Storage.Delegate and membership required. All success bodies use `ApiResponse<T>`. `SchoolFileStorage.AdministrationEnabled=false` by default returns 404 for this workspace. S1 exposes no new file list/content/export/upload API.

| Method | Route | Body / result |
|---|---|---|
| GET | `/api/v1/storage/delegations` | Array of delegation DTOs for the manager's active school, including expired/revoked history |
| POST | `/api/v1/storage/delegations` | `{ granteeUserId, startsAt, expiresAt?, reason }`; creates direct delegation and returns its DTO |
| DELETE | `/api/v1/storage/delegations/{id:int}` | `{ reason, rowVersion }`; revokes and returns the retained DTO; repeated revoke preserves the first revocation |

DTO: `id`, `granteeUserId`, `grantedByManagerUserId`, `startsAt`, `expiresAt`, `revokedAt`, `reason`, `revocationReason`, `revokedByManagerUserId`, `rowVersion` (base64 SQL rowversion). No Drive/provider identifiers or credentials. UTC/offset dates; reason required and <=1000 chars. StartsAt may be future or at most five minutes before server time; ExpiresAt must be strictly later. Grantee must be active and assigned to this school, distinct from manager. Overlapping nonrevoked intervals are rejected; contiguous intervals are allowed.

Failures use the existing ApiResponse middleware: 400 invalid input, 401 unauthenticated, 403 manager/school/identity denial, 404 disabled workspace or missing in-school ID, 409 stale rowversion, overlapping/duplicate grant or concurrency conflict. A delegate cannot list/manage delegations, including with copied claims. [S1 verification and rollout](specs/school-file-storage/verification/README.md).

## School File Storage S2 library API (2026-10-03)

Implemented `/api/v1/storage/context`, folder list/create/move/native-Drive-discovery, school/own file list/upload, file details/content/name/delete, and operation reconciliation. Exact query/body/multipart/header/status contracts are recorded in [S2 HTTP contracts](specs/school-file-storage/verification/s2-library-and-uploads.md#عقود-http-المنفذة). JSON uses ApiResponse; content is an authorized stream. ReadModelEnabled defaults OFF; old teacher/matrix paths remain available. No S3 link/review/version-change, S4 reporting, S5 archive or S6 import endpoints were activated.

## School File Storage S3 evidence API (2026-10-04)

Thin StorageEvidenceController → Application services → IEvidenceRepository/IStorageRepository → SQL. All JSON is ApiResponse<T>; content is an authenticated stream. Selected active school is server-side; no client school selector/body can enlarge scope. ReadModelEnabled=false keeps the workspace unavailable (404), including new matrix/export readers. The exact DTOs/statuses and retained-history policy are in [S3 HTTP contracts](specs/school-file-storage/verification/s3-evidence-and-review.md).

| Method | Route beneath `/api/v1/storage` | Contract |
|---|---|---|
| GET | `/academic-years`, `/evidence-teachers`, `/requirement-catalog?academicYearId=&search=` | Authorized catalog/year/teacher choices; school teacher choices require school view; S4 reserves GET requirements for paginated school evaluation |
| POST | `/requirements/initialize?academicYearId=` | Idempotent exact task mappings and 4/11 catalog |
| PATCH | `/requirements/{id}` | Domain/standard, importance, responsibleUserId/role, fulfillmentPolicy, minimumApprovedLinks, rowVersion |
| POST | `/files/{id}/links` | requirementId, academicYearId, teacherId?; teacher is server-derived for owned teacher files |
| GET | `/files/{id}/links`, `/me/files/{id}/links`, `/requirements/{id}/links` | Independent badges/rowversions/decision history; own path enforces ownership |
| GET | `/review-queue`, `/evidence-counts` | School/year requirement/teacher/standard/status filters; queue SQL pages25 by default; counts has own selector |
| POST | `/links/{id}/submit`, `/links/{id}/review` | rowVersion; review adds numeric decision Approved=3/Rejected=4 and note |
| POST / GET | `/files/{id}/change-requests` | Create kind Replace/Delete, required reason, file rowVersion, replaceBeforeReview?; GET retained requests/decisions |
| POST | `/change-requests/{id}/version` | S2 streamed multipart file + declared length, Idempotency-Key; new immutable candidate version of same asset |
| POST | `/change-requests/{id}/review` | approve boolean, note, change rowVersion; rejection reason required |
| GET | `/change-queue`, `/files/{id}/history`, `/files/{id}/versions/{version}/content` | Pending/Approved/Rejected change queue, withdrawn-inclusive authorized history, scoped historical bytes |

400 invalid context/body/reason, 403 scope/permission/ownership/delegation denial, 404 disabled/missing ID or unavailable bytes, 409 duplicate/race/stale rowversion, 503 provider/SQL upload uncertainty. Candidate upload success follows S2 operation response and reconciliation; a successful upload never approves a link. Existing legacy matrix review and Drive rename/delete reject mapped files even when flags are OFF; unmapped legacy records retain existing behavior.

## School File Storage S4 evaluation API (2026-10-04)

StorageReadinessController → IReadinessService/ReadinessExportService → IReadinessRepository → SQL. School and permissions are inferred from the current active server context and live database membership/delegation. JSON remains ApiResponse<T>; successful exports return authenticated no-store/nosniff file bytes. ReadModelEnabled=false closes all S4 routes (404). Teachers cannot query school evaluation or exports without a separately valid direct delegation.

| Method | Route beneath `/api/v1/storage` | Contract |
|---|---|---|
| GET | `/templates`, `/templates/{version}` | Immutable versions and sanitized 4/11/36/145 snapshot |
| GET | `/evaluation-members` | Current active school members for explicit assignments |
| POST | `/self-evaluation/initialize` | academicYearId, templateVersion=1; repeatable 36 mandatory rows; no files/approvals/tasks created |
| GET | `/requirements`, `/gaps`, `/digital-index` | StoragePage<T>, stable order and bounded paging; gaps means mandatory unfulfilled rows |
| GET | `/readiness` | School/year/version, template hash/name, rounding, calculation time/filters, overall + 4 domains + 11 standards; numerator/denominator/nullable percentage/state and independent counts |
| PATCH | `/requirements/{id}/follow-up` | responsibleUserId?, responsibleRole?, importance, isMandatory, policy, minimumApprovedLinks, dueDate?, followUpStatus, note?, reason, rowVersion |
| GET | `/requirements/{id}/follow-up-history` | Last 100 append-only revisions; all revisions retained in SQL |
| GET / POST | `/manual-evaluations` | GET academicYearId/templateVersion; POST scopeCode, judgment (can be empty when value supplied), value?, reason, rowVersion? plus school year/version |
| GET | `/manual-evaluations/{id}/history` | Last 100 append-only revision snapshots; school-scoped |
| GET | `/exports/{csv\|excel\|pdf}` | Same filter contract; comprehensive metrics/requirements/gaps/manual/index report; actual CSV UTF-8 BOM, Excel RTL, embedded-Amiri PDF |

Filter contract: academicYearId required, templateVersion=1, domainCode?, standardCode?, responsibleUserId?, importance? (Normal=1/Important=2/Critical=3), search? (<=200), trackerOnly=false; these define the structural calculation scope. status?, criticalOnly=false, hideCompleted=false and gapsOnly=false affect displayed/exported rows only. page=1, pageSize=25 (1..100). Statuses: Fulfilled, Unfulfilled, NoFile, Unavailable, AwaitingReview, Rejected, InsufficientApprovedLinks. Requirement rows include SourceKey/hash/path/action, candidates labelled for human content review, gapReasons and an action URL retaining the academic year.

GET `/requirements` changes the previous S3 array contract to a paginated school evaluation contract. GET `/requirement-catalog` preserves the S3 linking catalog and own-teacher permissions; Angular consumers and mocked regression tests moved with it. Existing S3 mutation/link routes remain. Curated source metadata cannot be overwritten through the old catalog PATCH. Manual/follow-up mutations require Storage.ManageSchool, a required reason and rowversion, with atomic audit; 400 validation/export cap, 403 current access denial, 404 disabled/not found, 409 concurrency and 503 unavailable provider remain safe ApiResponse errors. Export rows/files are capped at 5000 and live verification at 10000 distinct files; no export job system.

## School File Storage S5 archive contracts (2026-10-05)

Authenticated, rate-limited `/api/v1/storage/visits`, JSON ApiResponse<T>, server ActiveSchoolId only. Defaults remain disabled; V2 school feature must also be enabled for reads.

| Method | Route | Contract |
|---|---|---|
| GET | `/api/v1/storage/visits` | From/To UTC, TeacherId?, Status?, Page=1/PageSize=25 (max100); SQL filtered, descending latest approval time then VisitId; administrative list requires current Storage.ViewArchive and Visit.View. |
| GET | `/api/v1/storage/visits/teachers` | Distinct in-school archive teachers, sorted, capped1000; same list gate. |
| GET | `/api/v1/storage/visits/{visitId}/archive` | VisitArchiveDto with revisions/status/times/attempts/safe error/current/version history; teacher sees only own current Approved approval, no management data. |
| POST | `/api/v1/storage/visits/{visitId}/archive/retry` | `{approvalRevision,recreateMissing=false,reason?}`; current ViewArchive/RetryArchive/Visit.View, 202. Missing recreation requires true/nonempty reason<=1000; reserved/Processing/Pending or available Completed return409. |
| GET | `/api/v1/storage/visits/{visitId}/archive/{revision}/content?versionId` | Authorized PDF stream; same visit/school/root/identity/hash validation. Old revisions/versions restricted to authorized management; no Google IDs/public URL. private/no-store/nosniff/sandbox. |

No archive bytes/details/history are available through generic storage/files, evidence, digital index or readiness exports. Official V2 PDF/ZIP uses the frozen snapshot for new approvals even when archival waits; legacy approvals without snapshot retain existing V2 rendering. [Complete S5 contracts, security, tests and limitations](specs/school-file-storage/verification/s5-approved-visit-pdf-archive.md).

Existing teacher-drive items/content/breadcrumb also deny archived IDs and filter them from items lists, including external relocation into the teacher's grant or stripped properties. No legacy endpoint or response contract changes; archive content must use its visit-authorized API.

## School Drive setup: account-folder browser (2026-10-05, D-99)

`GET /api/v1/school-google-drive/folders` accepts optional `ParentItemId`, `PageToken` and `Search`; it derives the school from authenticated `ActiveSchoolId` and revalidates live manager/global-admin assignment in SQL. It returns `ApiResponse<SchoolDriveFolderPage>` with current folder identity/name, account-root and selectability flags, breadcrumbs, file/folder metadata and next page token. Page size is 50; search stays within the current folder. Response caching is `private, no-store`; existing teacher-drive rate limiting applies.

`PUT /api/v1/school-google-drive` now supports an inactive connection draft with empty root ID/name, enabling OAuth consent before root selection. Settings add `hasStoredOAuthClientSecret` as a boolean only. On root change or activation, the server validates real Drive folder metadata, write capability, other schools' roots and retained teacher/library/archive boundaries; name and shared drive ID come from validated metadata. Missing account consent is HTTP 400, forbidden scope/manager is 403, missing folder 404 and changed setup connection 409. Account browsing requires stored credentials but does not activate normal Drive access.

The setup reader performs metadata/list GETs only. No new migrations or automatic storage rollout are introduced. [Contracts, verification and local pilot](specs/school-file-storage/verification/drive-settings-and-pilot.md).
