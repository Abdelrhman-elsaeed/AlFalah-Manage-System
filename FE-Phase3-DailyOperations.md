# Frontend Phase 3 — Daily Attendance, Zajel Import, Noor Export, and Absence Excuses

## 1. Secretary attendance sheet — `رصد الغياب اليومي`

### 1.1 Access and contracts

Frontend route: `/student-affairs/attendance/sheet`  
Role: exact `Secretary`  
Permissions: `Attendance.ViewStudents` to load and `Attendance.ManageStudents` to submit

| Purpose | Endpoint | Request | Response |
|---|---|---|---|
| Classroom selector | `GET /api/v1/classrooms?academicYearId=&academicTermId=&pageSize=100` | `ClassroomListQuery` | `PagedResult<ClassroomDto>` |
| Load sheet | `GET /api/v1/student-attendance/sheet?date={yyyy-MM-dd}&classroomId={id}` | query parameters | `StudentAttendanceSheetDto` |
| Save/upsert full sheet | `PUT /api/v1/student-attendance/sheet` with `Idempotency-Key` | `SubmitAbsentRosterRequestDto` | Updated `StudentAttendanceSheetDto` |

The handler also requires the exact `Secretary` role even if another role is later granted `Attendance.ManageStudents`. The router must not offer this sheet to an Officer or Manager based on permission alone.

### 1.2 Load sequence

1. Default date to today's school-local date; allow only dates permitted by school policy.
2. Load the classroom catalog. The actual query property is `academicTermId`, not `termId`.
3. After class/date selection, fetch the sheet.
4. Replace the complete view model from `StudentAttendanceSheetDto`:
   - `date`.
   - `classroom: ClassroomSummaryDto`.
   - opaque `rosterRevision`.
   - `isSaved`.
   - `rows: StudentAttendanceSheetRowDto[]`.

Never construct a roster from a previously cached student search; `rows` is the authoritative active-enrollment roster for that class/date.

### 1.3 Core checkbox semantics

The selection control means **Absent**, not Present:

- Column label: `غائب؟`.
- Checked → include `student.id` in `absentStudentIds`.
- Unchecked → do not include the ID; the server marks that roster student `Present`.
- An empty `absentStudentIds` list is a meaningful complete submission: every actively enrolled student becomes Present.

For an existing saved sheet, initialize checks from row `status == Absent`. Display `AbsentExcused` as `غائب بعذر` with a locked/review indicator. The absent-ID payload cannot express “preserve AbsentExcused”; the current save handler would otherwise turn that row into either Absent or Present. If any row is already `AbsentExcused`, block Secretary full-sheet resubmission and direct the user to an Officer correction/review flow until the backend provides preservation semantics. Corrections requiring a protected override belong to `PATCH /api/v1/student-attendance/{attendanceId}` and are not part of the baseline Secretary flow.

### 1.4 Roster row design

Each row uses `StudentAttendanceSheetRowDto`:

- Student name/photo/number/class from `student`.
- Absent checkbox.
- Existing `status` and optional `excuseStatus` (`Pending`, `Accepted`, `Rejected`).
- Recorded metadata from `recordedBy` and `recordedAt` when present.
- `penaltyEligibleAbsenceBadge` count/severity/next threshold.
- Hidden transport fields `attendanceId` and `rowVersion`; the sheet write itself uses `rosterRevision`, not row versions.

Provide search within the already-loaded roster, but submission must include checked IDs across all filtered and unfiltered rows. Show a sticky summary: `إجمالي الطلاب`, `الغائبون المحددون`, and `سيُسجل الباقون حضورًا`.

### 1.5 Submission

Send `SubmitAbsentRosterRequestDto`:

| Field | Source |
|---|---|
| `date` | Loaded sheet date. |
| `classroomId` | Loaded `classroom.id`. |
| `absentStudentIds` | Unique positive IDs of all checked roster rows. |
| `rosterRevision` | Exact opaque value returned by GET. |

Generate one `Idempotency-Key` for the save attempt and retain it across network retries. Do not regenerate it when the first response is unknown.

Confirmation copy must be explicit: `سيتم تسجيل {N} طالبًا غائبًا، وتسجيل بقية طلاب الفصل حاضرين.` For `N=0`: `سيتم تسجيل جميع طلاب الفصل حاضرين.`

After success, replace rows/revision with the response DTO, clear dirty state, and announce `تم حفظ كشف الحضور`. On conflict, preserve checked IDs, refetch the sheet, compare the new roster, remove IDs no longer present only after showing them to the Secretary, and require reconfirmation.

## 2. Zajel biometric import — `استيراد سجل زاجل`

### 2.1 Access and endpoint

Frontend route: `/student-affairs/biometrics/zajel`  
Roles: `Secretary`, `StudentAffairsOfficer`  
Permission: `Biometric.Import`

Endpoint: `POST /api/v1/student-affairs/biometrics/zajel/import`  
Content type: `multipart/form-data`  
Form field: `file`  
Response: `ApiResponse<BiometricImportResultDto>`

### 2.2 Drop zone and preflight

- Accept one `.xlsx` workbook only.
- Reject zero-byte files locally.
- Maximum controller request size is 20 MiB; show the limit before selection.
- Display file name, size, and replacement action before upload.
- Do not interpret the workbook client-side as authoritative. The server owns Arabic-digit normalization, timezone conversion, enrollment matching, duplicate detection, and delay creation.
- Upload must not be silently replayed by the auth interceptor. If authentication refresh is needed, refresh first and ask the user to retry the retained file.

The workbook reader expects a worksheet containing the Arabic Zajel header row and these columns:

- `رقم الهوية`.
- `تاريخ ووقت الحضور`.
- `حالة الحضور`.

Accepted date-time forms include Excel date values and recognized `yyyy-MM-dd HH:mm:ss` / `dd/MM/yyyy HH:mm:ss` forms. A row marked `متأخر` or whose school-local time exceeds arrival cutoff plus grace becomes a delay. The frontend may show these requirements as help text but must not duplicate the importer logic.

### 2.3 Processing and result presentation

On success render all fields from `BiometricImportResultDto`:

| Field | Arabic metric |
|---|---|
| `totalRows` | إجمالي الصفوف المقروءة |
| `importedDelays` | حالات التأخر المسجلة |
| `skippedOnTimeRows` | صفوف حضور في الوقت |
| `duplicateRows` | صفوف مكررة/مسجلة مسبقًا |
| `unmatchedRows` | صفوف لم تطابق طالبًا نشطًا |

Render `issues: BiometricImportIssueDto[]` in a downloadable/copyable table with Excel row number, localized code, and safe message. Known codes:

- `MissingNationalId` → `رقم الهوية مفقود`.
- `StudentNotFound` → `لا يوجد طالب نشط مطابق`.
- `EnrollmentNotFound` → `لا يوجد تسجيل نشط في تاريخ البصمة`.

Issues are partial-result feedback: successfully imported delays remain successful even when other rows are unmatched. Do not label the whole import failed when `isSuccess=true`.

On `400`, show missing header, invalid date, empty workbook, wrong extension, or missing Student Affairs cutoff settings from the response. No rows/result view should be fabricated.

## 3. Noor weekly export — `تصدير أعذار الغياب إلى نور`

### 3.1 Access and endpoint

Frontend route: `/student-affairs/noor-export`  
Role: `StudentAffairsOfficer`  
Permission: `Noor.Export`

Endpoint: `POST /api/v1/student-attendance/noor/exports?weekStartsOn={yyyy-MM-dd}` with `Idempotency-Key`  
Success: `.xlsx`, content type `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`

Response headers:

- `X-Noor-Batch-Id`.
- `X-Noor-Row-Count`.

The file name is server-provided through `Content-Disposition`; expected fallback is `noor-absence-corrections-{weekStartsOn}.xlsx`.

### 3.2 Form and behavior

- Field: `بداية الأسبوع`, a Gregorian date.
- Show computed inclusive range through `weekStartsOn + 6 days`.
- Explain that export contains accepted/excused absence corrections (`AbsentExcused`) only.
- Generate one idempotency key per selected week/export intent. A repeated click or timeout retry uses the same key and should return the existing batch/file.
- No preview count endpoint exists. Do not imply a pre-generation count; the authoritative count arrives in `X-Noor-Row-Count` after generation.

On success:

1. Verify content type is XLSX.
2. Read batch ID and row count headers.
3. Save with the server filename.
4. Show `تم إنشاء ملف نور — {rowCount} سجل` and batch ID for support/audit.
5. If row count is zero, still allow the valid generated workbook and label it clearly as empty.

On error, parse the JSON envelope even though the HTTP client requested a blob. A known blocking error is accepted absence rows whose students have no national ID; show the count and direct the Officer to student administration. Do not download an error blob as `.xlsx`.

## 4. Guardian absence excuse upload — `رفع عذر غياب`

### 4.1 Access and discovery

Role: exact `Guardian`  
Permission: `Attendance.SubmitExcuse` plus active guardian link with `canSubmitExcuses=true`

Use only linked students from:

- `GET /api/v1/guardian/students` → `GuardianStudentDto[]`.
- `GET /api/v1/student-attendance/students/{studentId}?academicTermId=…` → `StudentAttendanceHistoryDto`.

Eligible rows are attendance records with `status=Absent`. Do not offer upload for `Present` or `AbsentExcused`. If an existing `excuseStatus` is Pending/Accepted, show its status instead of a new-upload button. The server remains authoritative and rechecks relationship validity on the attendance date.

### 4.2 Multipart submission

Endpoint: `POST /api/v1/student-attendance/{attendanceId}/excuses`  
Headers: `Idempotency-Key`  
Content type: `multipart/form-data`  
Success: `202 Accepted` with `ApiResponse<AbsenceExcuseDto>`

Multipart fields:

| Field | Type/rule |
|---|---|
| `excuseType` | `Medical`, `Family`, `Official`, or `Other`; required enum string. Arabic: طبي، عائلي، رسمي، أخرى. |
| `notes` | Optional guardian note. |
| `attachment` | Exactly one PDF, `.pdf`, MIME `application/pdf`, size > 0 and <= 10 MiB. |

Preflight must validate extension, MIME when available, and size. Keep the original filename visible but never render it as HTML. A valid submit creates a `Pending` excuse; the attendance remains unapproved until Officer review.

Result view uses `AbsenceExcuseDto`: status, guardian summary, submission time, review metadata when present, attachments, and `rowVersion`. Attachment links must use the returned authorized `downloadUrl` or the explicit download endpoint; never expose a storage key.

### 4.3 Excuse history/download

| Purpose | Endpoint | Response |
|---|---|---|
| Excuses for attendance row | `GET /api/v1/student-attendance/{attendanceId}/excuses` | `AbsenceExcuseDto[]` |
| Download PDF | `GET /api/v1/student-attendance/excuses/{excuseId}/attachments/{attachmentId}` | Authorized file stream |

Preview in a sandboxed PDF viewer or download. Revalidate access each time; do not permanently cache a confidential PDF in a service worker.

## 5. Officer excuse review — `مراجعة الأعذار`

### 5.1 Queue composition

Role: exact `StudentAffairsOfficer`  
Permissions: `Attendance.ViewStudents`, `Attendance.ReviewExcuse`

There is no dedicated `GET pending excuses` endpoint. Build the queue from the implemented contracts:

1. `GET /api/v1/student-attendance/records?excuseStatus=Pending&pageNumber=&pageSize=&fromDate=&toDate=&classroomId=&studentId=` → `PagedResult<StudentAttendanceRecordDto>`.
2. For a row opened or entering the visible viewport, `GET /api/v1/student-attendance/{attendanceId}/excuses` → select Pending `AbsenceExcuseDto` entries.

Avoid unbounded N+1 calls: load excuse details on expansion with a small concurrency limit and cache per attendance ID until mutation. The record DTO exposes an aggregate `excuseStatus` but not the excuse ID, so the second call is required by the current API.

Queue columns: student, class, attendance date, submitted-at, excuse type, attachment count, current status, and actions. Do not show Guardian notes in the compact row if the screen could be shoulder-surfed; show them in the authorized review drawer.

### 5.2 Review drawer

- Load/preview authorized attachment through the download endpoint.
- Display submitter guardian relationship, notes, timestamps, and status.
- Keep the latest excuse `rowVersion` in memory.
- Disable actions for any non-Pending excuse.

Approve:

- Endpoint: `POST /api/v1/student-attendance/excuses/{excuseId}/accept`.
- Request: `ReviewAbsenceExcuseRequestDto { reviewNote, rowVersion }`.
- `reviewNote` is optional by DTO.
- Response: updated `AbsenceExcuseDto`.

Reject:

- Endpoint: `POST /api/v1/student-attendance/excuses/{excuseId}/reject`.
- Request: `RejectAbsenceExcuseRequestDto { rejectionReason, rowVersion }`.
- `rejectionReason` is required and trimmed.
- Response: updated `AbsenceExcuseDto`.

After either success, remove the item from the Pending queue and update cached attendance data. Acceptance is intended to produce the `AbsentExcused` attendance outcome and penalty recalculation; the UI must re-fetch the attendance record/history and display the actual returned state rather than locally forcing it.

On `409` or unsuccessful concurrency envelope, keep the typed review note/reason, refetch excuses for the attendance ID, and show the winning decision. Never offer a second decision if the latest status is no longer Pending.

## 6. Daily-operations RBAC summary

| Component | Guardian | Secretary | Student Affairs Officer | Other listed roles |
|---|---:|---:|---:|---:|
| Attendance roster view/save | No | Yes | No baseline write; no sheet route | No |
| Zajel import | No | Yes | Yes | No |
| Noor export | No | No | Yes | No |
| Upload excuse | Yes, linked/capable only | No | No | No |
| Review excuse | No | No | Yes | No |
| Download excuse PDF | Related Guardian | No baseline need | Yes through attendance view/review | Only if relevant permission/object scope permits |

## 7. Phase 3 acceptance criteria

- Checking a roster row always means Absent; unchecked rows are submitted implicitly as Present.
- The full authoritative absent-ID list survives filters and the sheet's opaque `rosterRevision` is echoed.
- Zajel accepts one `.xlsx` up to 20 MiB and renders row-level server issues.
- Noor handles binary success and JSON failure without corrupt downloads.
- Guardian upload is one PDF up to 10 MiB with the exact multipart field names and a retained idempotency key.
- Officer review uses the latest excuse row version and cannot decide an already-decided excuse.
