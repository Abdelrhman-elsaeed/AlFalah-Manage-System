# Frontend Phase 1 — Foundation, Security, Localization, and Student Affairs Settings

## 1. Scope and source of truth

This phase defines frontend architecture and interaction contracts only. It does not prescribe React, Angular, Vue, HTML, or CSS implementation.

The authoritative backend sources are:

- Student Affairs routes under `/api/v1` in `AlFalah.Api/Controllers/StudentAffairs`.
- Authentication routes in `AuthController`.
- Wire contracts in `AlFalah.Application/StudentAffairs/DTOs`.
- Roles and permission grants in `RoleNames`, `PermissionNames`, and `DatabaseSeeder.GetRolePermissionMap()`.
- String enum names in `AlFalah.Domain/Enums/StudentAffairs`; ASP.NET uses `JsonStringEnumConverter`, so the wire format is the enum name, not its numeric value.

All screens in this kit are school-scoped. The frontend must never submit an authoritative `schoolId`, actor ID, reporter ID, reviewer ID, teacher ID, or security-guard ID unless a DTO explicitly contains that field. The JWT/current-user context supplies the active school and actor.

## 2. API transport contract

### 2.1 Base configuration

| Concern | Required frontend behavior |
|---|---|
| API base | Environment-owned origin plus `/api/v1`; never hard-code a production host. |
| JSON | Send `Content-Type: application/json; charset=utf-8` and `Accept: application/json`. Preserve Arabic as UTF-8. |
| Language | Send `Accept-Language: ar-SA` by default; allow `en-US` only when the user explicitly changes language. |
| Dates | Send `DateOnly` as `yyyy-MM-dd`. |
| Instants | Send `DateTimeOffset` as a complete ISO-8601 timestamp with offset, for example the school-local offset rather than a timezone-free value. |
| Times | Send `TimeOnly` as `HH:mm:ss`; accept `HH:mm:ss` from the API. |
| Enums | Send and receive string names such as `Absent`, `Requested`, `Visual`, and `GuardianTeacher`. |
| Paging | List queries use `page`/`pageNumber`, `pageSize` (1–100), `search`, `sortBy`, `sortDirection`; feature-specific filters are added per endpoint. |
| Cancellation | Abort superseded searches, route-leave requests, and stale dashboard refreshes. Do not abort a committed mutation merely because a modal closed. |
| Idempotency | Generate a UUID per user submission, retain it across timeout/retry, and replace it only when the user intentionally starts a new operation. |

### 2.2 Standard response envelope

JSON endpoints return camel-cased `ApiResponse<T>`:

| Field | Type | Client rule |
|---|---|---|
| `isSuccess` | boolean | The final business-success indicator. Inspect it even for HTTP 2xx. |
| `message` | string | Optional server summary; show only after localization/safe fallback. Never branch business logic on it. |
| `data` | `T` or null | Use only when `isSuccess=true`. |
| `errors` | string[] | Render as an Arabic error summary; do not expose stack traces or duplicate identical messages. |

Paged data is `ApiResponse<PagedResult<T>>`, where `data` contains `items`, `totalCount`, `page`, `pageSize`, `totalPages`, `hasNext`, and `hasPrevious`.

The transport adapter must normalize all successful HTTP responses into one of three frontend outcomes:

1. `success(data, message)` when HTTP is successful and `isSuccess=true`.
2. `businessFailure(errors, message)` when HTTP is successful but `isSuccess=false`.
3. `httpFailure(status, envelope/problem/body)` for non-2xx responses.

This second branch is mandatory because current Student Affairs handlers can return an unsuccessful `ApiResponse` through a controller's `200 OK` path.

### 2.3 Binary endpoints

Binary downloads do not use `ApiResponse<T>` on success. The client must inspect HTTP status and `Content-Type` before treating the body as a file.

| Endpoint | Success type | Error behavior |
|---|---|---|
| `POST /api/v1/student-attendance/noor/exports?weekStartsOn=…` | `.xlsx`; OpenXML content type | A `400` body is JSON `ApiResponse`, even if the request was configured for a blob. Parse the blob as UTF-8 JSON before showing an error. |
| `GET /api/v1/student-attendance/excuses/{excuseId}/attachments/{attachmentId}` | Authorized file stream, normally PDF | On error, do not download/preview the body; normalize the JSON/HTTP error. |

## 3. Authentication client and interceptors

### 3.1 Login/bootstrap endpoints

| Operation | Endpoint | Request DTO | Response DTO |
|---|---|---|---|
| School user login | `POST /api/v1/auth/school-login` | `SchoolLoginRequestDto { schoolId, username, password }` | `ApiResponse<AuthResponseDto>` |
| Refresh | `POST /api/v1/auth/refresh` | `RefreshTokenRequestDto { refreshToken }` | `ApiResponse<AuthResponseDto>` |
| Current identity | `GET /api/v1/auth/me` | none | `ApiResponse<CurrentUserDto>` |
| Logout | `POST /api/v1/auth/logout` | `RefreshTokenRequestDto` | `ApiResponse` |

`AuthResponseDto` supplies `accessToken`, `refreshToken`, both expiries, and `UserTokenInfoDto`. `CurrentUserDto` supplies the verified `roles`, `permissions`, `activeSchoolId`, `activeSchoolName`, and `preferredLanguage`.

Bootstrap sequence:

1. Restore the token session.
2. If the access token is valid, call `GET /api/v1/auth/me` before mounting protected routes.
3. If it is expired and a refresh token exists, perform one refresh, store the rotated response, then call `/auth/me`.
4. Build navigation and route access from `/auth/me`, not from decoded JWT claims alone.
5. If the current user has no `activeSchoolId`, block all school-scoped Student Affairs routes and show `لم يتم تحديد مدرسة نشطة`.

The existing backend returns the refresh token in JSON and accepts it in a JSON body; it does not expose an HttpOnly-cookie flow. Keep the access token in memory. If reload persistence is required, use session-scoped storage for the returned token pair, clear it on logout/tab-session end, apply a strict CSP, and never log either token. A future BFF/HttpOnly-cookie change would be a backend contract change.

### 3.2 Request interceptor

For protected calls, the interceptor must:

- Add `Authorization: Bearer <accessToken>`.
- Add `Accept-Language` from the active locale.
- Preserve caller-provided `Idempotency-Key` and multipart boundaries.
- Never add a client-chosen `SchoolId` header.
- Attach a correlation/request ID only as telemetry; it is not an idempotency key.
- Avoid serializing `undefined` query values as the literal string `undefined`.

### 3.3 Refresh/response interceptor

- On `401`, run a single-flight refresh so simultaneous requests share one refresh operation.
- Retry each failed request at most once after refresh.
- Never refresh on `403`, `400`, `409`, or an `isSuccess=false` envelope.
- If refresh fails, atomically clear auth state, cancel queued protected calls, and redirect to login with a safe return URL.
- Do not replay multipart uploads or mutations automatically unless the request carries the same retained idempotency key and the failure was definitively pre-commit. Prefer a user-visible retry for uploads.

## 4. Global error and conflict policy

### 4.1 `400 Bad Request` — validation/business rules

The current envelope exposes a flat `errors: string[]`, not a field-keyed validation dictionary. Therefore:

- Perform deterministic field validation locally using the DTO rules documented in each phase.
- Render server errors in a summary at the top of the form and move focus to that summary.
- Associate a server error with a field only when the endpoint contract makes the association unambiguous; otherwise do not guess.
- Keep the draft and user-selected file in place when the browser permits.
- Treat invalid workflow transitions as stale-state/business validation: refresh the affected record after the message is acknowledged.
- Never display raw exception text in production telemetry or user UI.

### 4.2 `403 Forbidden` — role, permission, tenant, or object scope

There are two distinct experiences:

- Route-level denial: replace the route with a full-page `ليس لديك صلاحية للوصول إلى هذه الصفحة`, include a return-to-dashboard action, and do not render protected data or mutation controls.
- Object-level denial after an allowed route loads: show an enumeration-safe inline state such as `لا يمكن عرض هذا السجل ضمن نطاق صلاحياتك`; do not reveal whether a student, guardian link, teacher assignment, or case exists elsewhere.

Do not retry. Refresh `/api/v1/auth/me` once only if permissions may have changed during the session; if the permission is still absent, keep the denial state.

### 4.3 `409 Conflict` — `RowVersion`, roster revision, or idempotency conflict

For every mutation carrying `rowVersion` or `rosterRevision`:

1. Freeze the submit control and preserve the user's draft separately from the stale server DTO.
2. Show `تم تعديل السجل بواسطة مستخدم آخر`.
3. Re-fetch the authoritative detail/list endpoint.
4. Present the new server state and a concise summary of fields/actions that changed.
5. Require the user to review and explicitly submit again with the new `rowVersion`; never silently overwrite or auto-retry.

For idempotent creates, first re-fetch the relevant list/detail. If the server returns the existing successful resource, treat it as success and do not create a second operation.

Compatibility note: the locked API specification maps concurrency to HTTP `409`, but several current handlers return `200` with `isSuccess=false` and a concurrency error string. The client normalizer must route those unsuccessful envelopes to the same conflict experience until the API consistently emits `409`; this compatibility mapping belongs in one transport adapter, not individual screens.

### 4.4 Other statuses

| Status | UI behavior |
|---|---|
| `401` | Refresh once, then sign out if unsuccessful. |
| `404` | Enumeration-safe not-found state; remove stale row after list refresh. |
| `413` | `حجم الملف أكبر من الحد المسموح`; preserve other fields. |
| `415` | `نوع الملف غير مدعوم`; specify the accepted PDF or XLSX type. |
| `429` | Show retry countdown from `Retry-After`; do not parallel-spam. |
| `500` | Stable Arabic fallback and incident/request ID; no technical details. |

## 5. RBAC router and navigation configuration

Permissions returned by `/api/v1/auth/me` are authoritative. Role checks are an additional UI constraint where handlers require an exact role. Hidden navigation is not security; every route guard must evaluate permissions again.

| Frontend route | Arabic navigation label | Role(s) allowed | Required permission(s) | Primary backend endpoint |
|---|---|---|---|---|
| `/student-affairs/teacher` | أولوية المعلم | `Instructor` | `TeacherQuickAction.View` | `GET /api/v1/teacher/student-affairs/current-context` |
| `/student-affairs/security` | بوابة الأمن | `SecurityGuard` | `StudentAffairsDashboard.Security` | `GET /api/v1/student-affairs/dashboard/security` |
| `/student-affairs/guardian` | أبنائي | `Guardian` | `StudentAffairsDashboard.Guardian` | `GET /api/v1/student-affairs/dashboard/guardian` |
| `/student-affairs/oversight` | مؤشرات شؤون الطلاب | `SchoolManager` | `StudentAffairsDashboard.SchoolOversight` | `GET /api/v1/student-affairs/dashboard/school-oversight` |
| `/student-affairs/attendance/sheet` | رصد الغياب | `Secretary` | `Attendance.ManageStudents` | `GET/PUT /api/v1/student-attendance/sheet` |
| `/student-affairs/biometrics/zajel` | استيراد زاجل | `Secretary`, `StudentAffairsOfficer` | `Biometric.Import` | `POST /api/v1/student-affairs/biometrics/zajel/import` |
| `/student-affairs/attendance/excuses` | مراجعة الأعذار | `StudentAffairsOfficer` | `Attendance.ReviewExcuse` | `GET /api/v1/student-attendance/records`; review endpoints |
| `/student-affairs/noor-export` | تصدير نور | `StudentAffairsOfficer` | `Noor.Export` | `POST /api/v1/student-attendance/noor/exports` |
| `/student-affairs/gate-passes` | طلبات الاستئذان | `StudentAffairsOfficer` | `GatePass.View` plus approve/reject permission | `GET /api/v1/gate-passes` |
| `/student-affairs/gate-passes/mine` | استئذانات أبنائي | `Guardian` | `GatePass.ViewOwn` | `GET /api/v1/gate-passes/mine` |
| `/student-affairs/gate-passes/security` | تنفيذ الخروج | `SecurityGuard` | `GatePass.Execute` | `GET /api/v1/gate-passes/security-queue` |
| `/student-affairs/cases` | الإحالات والحالات | `SocialWorker` | `Referral.View` | `GET /api/v1/referrals` |
| `/student-affairs/summons` | استدعاءات أولياء الأمور | `SocialWorker` | `Summon.View` | `GET /api/v1/summons` |
| `/student-affairs/notification-approvals` | اعتماد إشعارات أولياء الأمور | `StudentAffairsOfficer` | either dispatch approval permission | `GET /api/v1/notifications/pending-dispatch` |
| `/student-affairs/messages` | الرسائل | `Guardian`, `Instructor`, `StudentAffairsOfficer`, `SocialWorker` | `Messaging.ViewOwn` | `GET /api/v1/conversations` |
| `/student-affairs/office-hours` | الساعات المكتبية | `Instructor` | `OfficeHours.ManageOwn` | `GET /api/v1/office-hours/me` |
| `/student-affairs/office-hours/teachers/:id` | ساعات المعلم المكتبية | `Guardian`, `SchoolManager` | `OfficeHours.View`; Manager also needs `OfficeHours.ManageSchool` to edit | `GET /api/v1/office-hours/teachers/{instructorId}` |
| `/student-affairs/settings` | إعدادات شؤون الطلاب | `StudentAffairsOfficer`, `SchoolManager` | `StudentAffairsSettings.View` | `GET /api/v1/student-affairs/settings` |

Settings are editable only when the actor is `StudentAffairsOfficer` and has `StudentAffairsSettings.Manage`. The seeded `SchoolManager` is read-only on this screen.

## 6. Arabic, RTL, and domain language

### 6.1 Global RTL rules

- Arabic is the default locale: document language `ar-SA`, direction RTL.
- Navigation begins at the right; the primary action is at the visual inline end appropriate to RTL.
- Use logical start/end spacing and alignment. Do not mirror meaningful direction icons such as clocks or download symbols; do mirror navigation arrows.
- Keep student numbers, national IDs, file names, row versions, ISO dates, and technical IDs in isolated LTR spans so punctuation does not reorder.
- Format displayed dates/times with the school timezone returned by the endpoint. Use Gregorian school dates unless product policy explicitly requests Hijri; API values remain ISO/Gregorian.
- Tables read right-to-left: identity and status columns begin on the right, row actions remain consistently at the left edge.
- All status meaning must include text/icon, not color alone. Minimum touch target is 44×44 logical pixels; guard/teacher priority actions should be larger.

### 6.2 Canonical Arabic terms

| Domain term | Arabic label |
|---|---|
| Student Affairs | شؤون الطلاب |
| Student Affairs Officer | وكيل شؤون الطلاب |
| Social Worker | الموجه الطلابي / الأخصائي الاجتماعي |
| Guardian | ولي الأمر |
| Security Guard | حارس الأمن |
| Instructor | المعلم |
| Secretary | سكرتير المدرسة |
| School Manager | مدير المدرسة |
| Attendance / Present / Absent | الحضور / حاضر / غائب |
| Excused absence | غياب بعذر |
| Morning arrival delay | تأخر صباحي |
| Session delay | تأخر عن الحصة |
| Academic concern | ملاحظة أكاديمية |
| Behavior incident | مخالفة سلوكية |
| Recognition | إشادة وتميّز |
| Classroom entry permit | تصريح دخول الفصل |
| Gate pass | استئذان خروج |
| Referral | إحالة طلابية |
| Guardian summons | استدعاء ولي الأمر |
| Under observation | تحت الملاحظة |
| Improved | تحسّن |
| Office hours | الساعات المكتبية |
| Pending approval | بانتظار الاعتماد |
| Row changed | تم تحديث السجل من مستخدم آخر |

## 7. School Student Affairs Settings screen

### 7.1 Screen contract and lifecycle

Route: `/student-affairs/settings`.

| Operation | Endpoint | Permission | Request | Response |
|---|---|---|---|---|
| Load effective settings | `GET /api/v1/student-affairs/settings` | `StudentAffairsSettings.View` | none | `ApiResponse<SchoolStudentAffairsSettingsDto>` |
| Create customization | `POST /api/v1/student-affairs/settings` | `StudentAffairsSettings.Manage` + Officer role | `CreateStudentAffairsSettingsRequestDto` | `201 ApiResponse<SchoolStudentAffairsSettingsDto>` |
| Update customization | `PUT /api/v1/student-affairs/settings` | same | `UpdateStudentAffairsSettingsRequestDto` | Updated DTO |
| Reset to defaults | `DELETE /api/v1/student-affairs/settings` | same | `ResetStudentAffairsSettingsRequestDto` | Effective defaults DTO |
| Audit history | `GET /api/v1/student-affairs/settings/history?pageNumber=&pageSize=` | `StudentAffairsSettings.View` | `StudentAffairsPageQuery` | `PagedResult<StudentAffairsSettingsHistoryDto>` |

Use `id` and `usesLockedDefaults` to choose the write mode:

- `id=null` or `usesLockedDefaults=true`: present defaults and use `POST` when the Officer saves a school customization.
- A custom active row: use `PUT`, always echoing the latest `rowVersion`.
- Reset is destructive to the customization but not to the effective policy; it restores locked defaults and returns a new effective DTO.

### 7.2 Form sections and schema

#### A. Arrival bell/cutoff — `جرس الحضور والتأخر الصباحي`

The settings DTO does not contain a full multi-period bell schedule. It contains only the arrival cutoff and grace used by Zajel/morning-delay logic. Do not invent period rows in this form; published lesson periods belong to the school timetable module.

| Arabic field | DTO field | Control semantics | Validation |
|---|---|---|---|
| وقت احتساب التأخر | `arrivalCutoffLocalTime` | School-local time input, seconds hidden unless returned non-zero | Required `TimeOnly`; submit as `HH:mm:ss`. |
| فترة السماح بالدقائق | `arrivalGraceMinutes` | Integer stepper | Required integer, `>= 0`. Effective cutoff is cutoff + grace. |
| حد التأخر الصباحي للفصل | `morningDelayThresholdPerTerm` | Integer stepper | Positive integer; locked default `10`. |

Show a calculated read-only sentence: `يُحتسب التأخر بعد الساعة {cutoff + grace}`. This is explanatory only; the API still receives the original cutoff and grace fields.

#### B. Observation thresholds — `حدود الملاحظات والتصاريح`

| Arabic field | DTO field | Validation/default |
|---|---|---|
| مضاعف المخالفات السلوكية | `behaviorIncidentMultiplePerTerm` | Positive integer; default `10`; explain triggers occur at 10, 20, 30… |
| حد الملاحظات الأكاديمية | `academicConcernThresholdPerTerm` | Positive integer; default `3`. |
| حد تكرار تصاريح دخول الفصل | `classroomEntryPermitThresholdPerTerm` | Positive integer; default `5`. |
| سياسة احتساب السلوك | `behaviorCountabilityPolicy` | Required backend policy code, ASCII, maximum 100 characters. Render as a controlled select populated from an approved frontend configuration; current known code is `all-upheld`. Never allow arbitrary localized text to be sent as the code. |

#### C. Absence escalation — `مستويات تصعيد الغياب`

| Level | Arabic label | DTO field | Locked default |
|---|---|---|---:|
| 1 | تنبيه مرئي | `absenceVisualAlertThresholdPerTerm` | 3 |
| 2 | إحالة واستدعاء | `absenceReferralThresholdPerTerm` | 5 |
| 3 | توصية لجنة حقوق الطفل | `absenceChildRightsThresholdPerTerm` | 10 |

All values must be positive integers and satisfy `visual < referral < childRights`. Show them as an ordered three-step ladder and announce ordering errors beside the whole group. Explain that only penalty-eligible `Absent` days count; `AbsentExcused` is always excluded and is not configurable.

#### D. Audit fields

- Create has no audit-reason property.
- Update requires non-empty `auditReason` and latest `rowVersion`.
- Reset requires non-empty `reason` and latest `rowVersion`.
- `effectiveVersion`, `effectiveFrom`, and `rowVersion` are read-only metadata and must never be editable.

### 7.3 Read-only Manager experience

For `SchoolManager`, show all effective values, `usesLockedDefaults`, `effectiveVersion`, `effectiveFrom`, and the history tab. Hide Save/Reset controls because the seeded role lacks `StudentAffairsSettings.Manage`. Do not show disabled controls that imply the Manager can request a write.

### 7.4 History tab

Each `StudentAffairsSettingsHistoryDto` row shows:

- `version` and `effectiveFrom`.
- Actor from `actor.displayName` and `actor.roleSnapshot`.
- `reason`.
- A compact comparison of that row's nested `settings` against the immediately previous version.

History is paged through `GET /api/v1/student-affairs/settings/history`; do not reconstruct audit history from current values.

### 7.5 Save/reset behavior

- Disable submit while a request is active and prevent double submission.
- After success, replace the entire local settings model with returned `SchoolStudentAffairsSettingsDto`, including its new `rowVersion` and `effectiveVersion`.
- Warn that settings changes can recalculate active-term metrics and flag existing summons for review; this is informational, not a client-side automation.
- Reset requires an explicit confirmation dialog naming the locked defaults and a typed reason. On conflict, close neither form nor dialog until the latest settings have been fetched and shown.

## 8. Phase 1 acceptance criteria

- Every protected route is derived from `/auth/me` roles and permissions.
- A user cannot see another role's navigation, and direct URL entry produces the same denial.
- Arabic is the default, the shell is RTL, and LTR identifiers remain readable.
- 2xx unsuccessful envelopes are not mistaken for success.
- `400`, `403`, and `409` have distinct user experiences and drafts survive recoverable failures.
- Settings create/update/reset use the correct DTO and latest row version.
- Absence settings cannot be submitted unless strictly ordered.
- The form does not misrepresent arrival cutoff settings as a complete timetable/bell schedule.

## 9. Backend integration readiness notes discovered during contract audit

These notes do not change the frontend contracts above; they identify places where the checked-in runtime must be reconciled before UI integration testing:

1. The current Student Affairs controller directory contains 128 `[Http*]` actions, not 126. OpenAPI should be treated as the release-time count/source of truth.
2. The locked backend API document assigns row-version/idempotency conflicts to HTTP `409`, but current controllers/handlers commonly return HTTP `200` with `isSuccess=false`; `GlobalExceptionMiddleware` has no concurrency-to-409 mapping. The compatibility normalization in section 4.3 is therefore required until backend status mapping is unified.
3. A repository search finds DTO/Controller contracts but no discoverable `IRequestHandler` implementations for several UI-critical request families, including settings, dashboards, teacher current context, recognitions, referrals, conversations, and office hours. The solution compiles because MediatR handler availability is a runtime concern; smoke-test these routes before declaring a frontend environment ready.
4. `ApiResponse<T>` supplies only flat string errors, with no stable error code or field-keyed validation map. Frontend business branching should remain status/DTO/state based; server error strings are presentation fallbacks only.
5. Officer assignment needs a scoped Social Worker lookup, and Guardian teacher-thread creation needs a linked-student teacher/instructor-profile lookup. Neither is present in the audited Student Affairs contracts, so those selectors are explicitly blocked rather than populated from an unsafe general directory.
6. `UpdateMyOfficeHoursRequestDto` accepts one `rowVersion`, while its GET endpoints return lists of per-slot row versions. Phase 5 defines a fail-closed consistency rule until an aggregate configuration version is exposed.
7. The current excuse-review handler updates the excuse and attendance `excuseStatus`, but does not directly set attendance `status=AbsentExcused` at review time, while the locked workflow and Noor query require that status. The UI must re-fetch and display actual state; backend integration tests should verify the intended transition before Noor rollout.
