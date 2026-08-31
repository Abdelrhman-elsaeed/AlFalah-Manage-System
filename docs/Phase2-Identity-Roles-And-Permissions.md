# Phase 2 — Identity, Roles, and Permissions

**Status:** Locked technical specification — no implementation authorized  
**Depends on:** Phase 1 academic and guardian relationships

## 1. Existing security model to preserve

The current platform uses ASP.NET Core Identity plus database-driven `ApplicationRole`, `Permission`, `RolePermission`, and `UserSchoolRole`. JWTs carry role, permission, and `active_school_id` claims. `SchoolScopeGuard` is the authoritative tenant gate.

The new module must extend this model; it must not introduce a second authentication system or authorize from role names alone.

Required changes to existing security infrastructure:

- Add the four role constants and seed rows described below.
- Add the roles to `CurrentUserService.IsSchoolScopedRole()`. Otherwise `SchoolScopeGuard` will fail closed for them.
- Keep `SuperAdmin` and `MainManager` as the only global roles.
- Guardian login must select/derive a school context and issue `active_school_id`. A guardian attached to students in two schools has two `GuardianProfile`/`UserSchoolRole` assignments and must select one school at login.
- A permission answers “may this role attempt this use case”; handlers still enforce student ownership, guardian links, timetable ownership, workflow state, and `SchoolId`.
- Identity roles remain platform roles, while `GuardianProfile`, `InstructorProfile`, and student-affairs assignment records carry domain-specific relationships.

## 2. New roles

### 2.1 `Guardian` — ولي الأمر

**Scope:** One selected school and only students linked through active `StudentGuardian` rows.

Can:

- View linked student summaries, daily absence, morning delay, session delay, approved/dispatchable student-affairs notices, recognitions, active entry permits, and summons.
- Upload PDF absence excuses for linked students when `CanSubmitExcuses=true`.
- Request/cancel a gate pass before review when `CanRequestGatePass=true`.
- View gate-pass decisions and execution status.
- Participate in student-specific messaging with teachers, Student Affairs Officers, and Social Workers.
- Read notifications and acknowledge summons.

Cannot:

- Browse any school student directory.
- Select a raw `StudentId` outside the “my students” endpoint and relationship check.
- Approve excuses, gate passes, referrals, or summons.
- View internal case notes, behavior details marked staff-confidential, or other guardians on the same student unless explicitly allowed.

### 2.2 `StudentAffairsOfficer` — وكيل شؤون الطلاب

**Scope:** One active school.

Can:

- Manage school students, enrollments, guardian links, and absence-excuse decisions. The Secretary owns routine daily attendance roster submission.
- Review morning/class delays, academic concerns, behavior incidents, and recognitions.
- Issue classroom-entry permits.
- Approve/reject gate passes and monitor guard acknowledgement/execution.
- Receive all threshold alerts, approve behavior/academic-concern guardian dispatches, review recalculation-affected automatic summons, and create/manual-route referrals.
- View and edit the school-wide `SchoolStudentAffairsSettings` thresholds through the dedicated audited permission.
- View recognition statistics and student-affairs dashboards.
- Message guardians without teacher office-hour restrictions.

Cannot:

- Operate in another school, even when a request contains another `SchoolId`.
- Execute physical exit at the gate.
- Mark a Social Worker case resolved or schedule a summons unless separately granted.
- Bypass the settings use case or edit immutable rule/trigger history.

### 2.3 `SocialWorker` — الموجه الطلابي / الأخصائي الاجتماعي

**Scope:** One active school. Multiple workers may exist; cases are assignable.

Can:

- View and accept referrals in the current school.
- Schedule guardian summons; transition Pending → Attended → UnderObservation → Improved.
- Record counseling, observations, recommendations, suspension/grade-deduction recommendations, and Child Rights Committee referral actions.
- Message relevant guardians and view the student timeline necessary for an assigned case.

Cannot:

- Approve a gate pass unless separately granted the exact permission.
- Execute an exit.
- Browse unrelated students merely because they share the school; access is assignment/permission constrained.
- Change raw attendance/incident facts to manipulate thresholds. Corrections use dedicated permissions and audit reasons.

### 2.4 `SecurityGuard` — حارس الأمن / حارس المدرسة

**Scope:** One active school and today's approved gate queue only.

Can:

- View minimum necessary gate-pass information: student identity/photo, classroom, approved exit window, guardian-entered pickup name/hint, officer approval, and status.
- Acknowledge that an approved pass was seen.
- Mark `Exited` only when the student physically leaves and record the actual exit timestamp.
- Record visual/manual/screenshot verification method and a concise verification note; no pre-registered delegate lookup is required.
- Flag a mismatch or execution problem without seeing confidential attendance/behavior/case data.

Cannot:

- Create, approve, reject, edit, or delete gate passes.
- Browse student profiles, guardian contact history, summons, incidents, or messages.
- Mark `Exited` before acknowledgement or after expiry without an authorized override.

## 3. Existing roles affected

| Existing role | Student Affairs extension |
|---|---|
| `SuperAdmin` | Receives all new permissions through the existing canonical all-permissions map; global support access remains audited |
| `MainManager` | Aggregated cross-school dashboards and configuration where granted; no routine student case-detail access by default |
| `SchoolManager` | Aggregate school-level oversight, role assignment, and exceptional gate-pass override; dashboard access never includes confidential Social Worker case notes or counseling/session details |
| `Secretary` | Owns roster-based daily student attendance submission; no excuse approval, behavior/summons/gate approval, or blocked Noor export permission by default |
| `Moderator` | No Student Affairs access by default; this existing “classroom evaluation supervisor” is not the Social Worker |
| `Instructor` | Teacher top-priority dashboard, session delay/academic concern/behavior/recognition recording, current-student lookup, entry/gate notification acknowledgement, own office hours and guardian messaging |

## 4. Permission naming rules

- Permission codes are stable English strings in `PermissionNames.cs` and database rows.
- Use singular module prefixes consistent with the current codebase, while retaining the requested business examples exactly where they are already clear (`GatePass.Approve`, `Attendance.ManageStudents`).
- Avoid broad `StudentAffairs.Manage`; every mutation has a dedicated code.
- Controllers may perform an early permission check for a fast 403, but the use-case handler repeats all authoritative checks.

## 5. Permissions to add to `PermissionNames.cs`

### 5.1 Student and guardian administration

| Constant intent | Permission code | Meaning |
|---|---|---|
| View students | `Student.View` | School-scoped list/detail access |
| Create students | `Student.Create` | Create a student master row |
| Edit students | `Student.Edit` | Edit non-workflow profile fields |
| Archive students | `Student.Archive` | Soft-delete/deactivate with reason |
| Manage enrollment | `Student.EnrollmentManage` | Enroll, transfer, withdraw |
| View guardians | `Guardian.View` | View guardians related to an authorized student |
| Manage guardians | `Guardian.Manage` | Create/edit school guardian profiles |
| Link guardians | `Guardian.LinkStudent` | Create/revoke student-guardian authority |
| View own linked students | `Guardian.ViewLinkedStudents` | Guardian-only relationship-scoped read |

### 5.2 Student attendance and excuses

| Permission code | Meaning |
|---|---|
| `Attendance.ViewStudents` | View student attendance sheets/timelines |
| `Attendance.ManageStudents` | Record/correct the daily student sheet |
| `Attendance.SubmitExcuse` | Guardian submits excuse for linked student |
| `Attendance.ReviewExcuse` | Accept/reject excuse |
| `Attendance.OverrideCorrection` | Correct locked/historical attendance with reason |
| `MorningDelay.View` | View school/student morning delays |
| `MorningDelay.ManageReason` | Add/review delay reason; does not edit raw biometric punch |

The existing `Attendance.View` and `Attendance.Manage` remain staff-attendance permissions and are not repurposed.

### 5.3 Teacher observations and recognition

| Permission code | Meaning |
|---|---|
| `SessionDelay.View` | View authorized class/student session delays |
| `SessionDelay.Create` | Teacher records a delay in the current lesson |
| `SessionDelay.Correct` | Authorized audited correction |
| `AcademicConcern.View` | View authorized academic concerns |
| `AcademicConcern.Create` | Teacher creates concern |
| `AcademicConcern.Manage` | Officer reviews/corrects/routes concern |
| `Behavior.View` | View permitted behavior data |
| `Behavior.Create` | Record an incident |
| `Behavior.Manage` | Correct/classify/route incident |
| `Recognition.View` | View recognitions |
| `Recognition.Create` | Record recognition |
| `Recognition.Manage` | Correct/archive recognition |
| `Recognition.ViewStatistics` | Weekly/monthly/term statistics |
| `TeacherQuickAction.View` | Access current timetable/class quick-action payload |

### 5.4 Classroom-entry permits

| Permission code | Meaning |
|---|---|
| `ClassroomEntryPermit.View` | View permit within role scope |
| `ClassroomEntryPermit.Issue` | Officer issues permit |
| `ClassroomEntryPermit.Acknowledge` | Current teacher acknowledges |
| `ClassroomEntryPermit.Revoke` | Officer revokes active permit |

### 5.5 Gate passes

| Permission code | Meaning |
|---|---|
| `GatePass.View` | Staff view in-scope passes |
| `GatePass.ViewOwn` | Guardian views requests for linked student |
| `GatePass.Request` | Guardian submits a request |
| `GatePass.CancelOwn` | Guardian cancels own unreviewed request |
| `GatePass.Approve` | Student Affairs Officer approves |
| `GatePass.Reject` | Student Affairs Officer rejects with reason |
| `GatePass.AcknowledgeTeacher` | Current teacher acknowledges release notice |
| `GatePass.AcknowledgeSecurity` | Guard acknowledges approved pass |
| `GatePass.Execute` | Guard records actual exit |
| `GatePass.Override` | School Manager resolves exceptional/expired case with audit reason |
| `GatePass.ViewAudit` | View transition and delivery audit |

### 5.6 Referrals, cases, and summons

| Permission code | Meaning |
|---|---|
| `Referral.View` | View in-scope/assigned referrals |
| `Referral.Create` | Manual referral |
| `Referral.Assign` | Assign/reassign Social Worker |
| `Referral.Manage` | Update case and actions |
| `Referral.ViewConfidential` | View sensitive case notes |
| `Summon.View` | View in-scope summons |
| `Summon.Create` | Create manual summons |
| `Summon.Schedule` | Set/reschedule appointment and send guardian notice |
| `Summon.MarkAttended` | Pending → Attended |
| `Summon.StartObservation` | Attended → UnderObservation |
| `Summon.MarkImproved` | UnderObservation → Improved |
| `Summon.ViewHistory` | View immutable transition history |
| `Summon.ReviewAutomationImpact` | Officer acknowledges/retains an unresolved automatic summons flagged after source-count recalculation and records a mandatory rationale; it does not invent a new summons state |

### 5.7 Messaging and office hours

| Permission code | Meaning |
|---|---|
| `Messaging.ViewOwn` | View threads where current user is participant |
| `Messaging.Send` | Send under participant/office-hour rules |
| `Messaging.StartGuardianTeacher` | Guardian starts a linked-student teacher thread |
| `Messaging.StartGuardianAdministration` | Guardian starts Officer/Social Worker thread |
| `Messaging.CloseThread` | Authorized participant closes thread |
| `Messaging.ViewAudit` | School-authorized audit without message mutation |
| `OfficeHours.View` | View teacher office hours |
| `OfficeHours.ManageOwn` | Teacher selects eligible own hours |
| `OfficeHours.ManageSchool` | Manager override/configuration |

### 5.8 Settings, automation, notifications, and dashboards

| Permission code | Meaning |
|---|---|
| `StudentAffairsSettings.View` | View the effective school Student Affairs thresholds |
| `StudentAffairsSettings.Manage` | Student Affairs Officer creates/updates the school-wide settings with version and audit reason |
| `Automation.View` | View active rules and trigger history |
| `Automation.Retry` | Retry failed outbox action |
| `Notification.ViewOwn` | Read own in-app notifications |
| `Notification.ApproveDispatch` | Approve behavior-incident or academic-concern guardian dispatch |
| `Notification.SuppressDispatch` | Suppress a pending behavior/academic dispatch with mandatory reason |
| `Notification.ViewDelivery` | View delivery/read status |
| `StudentAffairsDashboard.Teacher` | Top-priority teacher view |
| `StudentAffairsDashboard.Officer` | Officer operational dashboard |
| `StudentAffairsDashboard.SocialWorker` | Assigned cases/summons dashboard |
| `StudentAffairsDashboard.Security` | Today's approved gate queue |
| `StudentAffairsDashboard.Guardian` | Linked-student summary |
| `StudentAffairsDashboard.SchoolOversight` | School Manager aggregate oversight |

## 6. Canonical role-permission seed map

This is the baseline. `DatabaseSeeder.SyncRolePermissionsAsync` remains a two-way canonical sync, so removing a permission from this map intentionally revokes stale database grants.

Legend: **F** full set for module, **R** read, **O** own/linked/assigned only, **—** none by default.

| Capability | SuperAdmin | MainManager | SchoolManager | StudentAffairsOfficer | SocialWorker | SecurityGuard | Secretary | Instructor | Guardian |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Student master/enrollment | F | aggregate/R by explicit need | F | F | assigned R | — | roster-minimum R | current-class minimal R | linked O |
| Guardian links | F | — | F | F | assigned R | — | — | — | own O |
| Student attendance | F | aggregate | aggregate + audited exception only | review excuses/read | assigned R | — | manage roster | current-class R only if granted | linked O + submit excuse |
| Delays/behavior/recognition | F | aggregate | aggregate only | manage/approve dispatch | assigned R/manage case | — | — | create/current-class | linked/approved-dispatch R |
| Entry permit | F | — | F | issue/revoke | R | — | — | acknowledge | linked R |
| Gate pass | F | aggregate | F + override | approve/reject | relevant R | acknowledge/execute | — | acknowledge | request/own R |
| Referral/summon | F | aggregate | aggregate status only | create/assign/read/review impact | F on assigned | — | — | source-status R only | own notice/status |
| Messaging | F/audit | — | school audit/config | participant/admin | participant/admin | — | — | participant + office-hours | participant |
| Settings/rule outcomes | F | aggregate | settings R/aggregate | settings manage + rule outcomes | assigned outcome R | — | — | — | — |

Important default assignments:

- `Instructor`: `TeacherQuickAction.View`, `SessionDelay.Create/View`, `AcademicConcern.Create`, `Behavior.Create`, `Recognition.Create`, entry/gate teacher acknowledgement, `OfficeHours.ManageOwn`, messaging participant permissions, teacher dashboard.
- `Secretary`: `Attendance.ViewStudents` and `Attendance.ManageStudents` in addition to existing staff attendance permissions. The request contains only absent IDs; the handler creates the complete classroom/date sheet.
- `Guardian`: only own/linked permissions; it never receives general `Student.View`, `GatePass.View`, or `Summon.View`.
- `SecurityGuard`: only `GatePass.View`, `GatePass.AcknowledgeSecurity`, `GatePass.Execute`, and the security dashboard. Its query handler always projects a minimal DTO.
- `SocialWorker`: referral/summon/case permissions and relevant read-only student timeline. Confidential notes require `Referral.ViewConfidential`.
- `MainManager`: cross-school aggregate data by default. Detailed student cases require an explicit exceptional permission; global scope alone is not sufficient product authorization.
- `SchoolManager`: `StudentAffairsDashboard.SchoolOversight` returns class/school aggregates only. Do not seed `Referral.ViewConfidential`; case notes, counseling notes, session details, guardian conversations, and attached evidence are excluded even when the manager can see case counts/statuses.
- `StudentAffairsOfficer`: seed `StudentAffairsSettings.View/Manage`, `Notification.ApproveDispatch/SuppressDispatch`, and `Summon.ReviewAutomationImpact`.

## 7. Object-level authorization rules

Permission checks are necessary but never sufficient.

1. **Tenant:** call `ResolveAllowedSchoolId` for reads and `EnsureCanMutateSchoolAsync` for writes.
2. **Guardian ownership:** require an active, in-date `StudentGuardian` row with the capability needed for the action.
3. **Teacher current-class scope:** prove active `InstructorProfile`, published timetable assignment, classroom, day, and period. An override must be separately permitted and audited.
4. **Social Worker scope:** detailed case data requires assignment to the current user or a school-level assignment permission.
5. **Security minimum disclosure:** only approved/acknowledged passes in the active execution window, and only the fields required at the gate.
6. **Workflow state:** a permission does not allow an invalid transition.
7. **Notification policy:** only `Notification.ApproveDispatch` may approve a guardian message that is configured for human review.
8. **Files:** excuse-attachment downloads repeat the related aggregate authorization; a storage key is never treated as a bearer token.
9. **Global roles:** cross-school access remains explicitly filtered. Main Manager receives aggregates rather than confidential row-level details unless product policy grants it.

## 8. Login and JWT implications

- School login must allow the new school-scoped roles.
- JWT includes all existing claims plus role/permissions and `active_school_id`; do not add student IDs or large guardian relationship lists to the token because relationships can be revoked during token lifetime.
- Guardian `GET /me/students` resolves relationships from the database on every request or a short-lived cache with immediate invalidation.
- Security Guard sessions should have short access-token lifetime and no refresh on shared kiosk devices unless device management is approved.
- Role assignment requires an active `UserSchoolRole`, active school, active user, and any required domain profile.

## 9. Seed and rollout procedure

1. Add role constants and permission constants.
2. Add idempotent role seed entries with exact Arabic/English descriptions.
3. Add permission metadata grouped by module.
4. Extend the canonical role-permission map.
5. Extend `IsSchoolScopedRole()` and school-login allowed role validation before issuing any new-role tokens.
6. Create domain profiles/assignments before enabling login.
7. Run two-way permission sync and force token renewal so stale permission claims cannot survive.
8. Add regression tests for every role, cross-school coercion/denial, guardian sibling/non-linked access, current-teacher scope, case assignment, and guard minimum projection.

## 10. Security acceptance tests

- A guardian linked to Student A receives 403/404-safe denial for Student B, including by changing route IDs.
- A guardian with children in two schools cannot read school 2 while the token has school 1 active.
- An Instructor cannot record a quick action for another classroom/period.
- A Security Guard cannot approve a requested pass or view an incident/summons.
- A Social Worker cannot read another worker's confidential case unless assigned/authorized.
- A Student Affairs Officer cannot mutate a different school by body/query manipulation.
- A stale or guessed storage key cannot download an excuse or form artifact.
- A Main Manager aggregate endpoint contains no confidential notes, guardian identifiers, or uploaded excuse documents.
- A School Manager aggregate endpoint contains no Social Worker case notes, counseling/session details, guardian conversations, or evidence attachments.
- Soft-deleted student/guardian links are excluded from all normal authorization checks.
- `IgnoreQueryFilters()` never broadens school scope.

## 11. Locked authorization decisions

- The Secretary records the daily student roster; the Student Affairs Officer reviews excuses and owns school-wide Student Affairs settings.
- The School Manager dashboard is aggregate-only and cannot expose confidential Social Worker case notes or session details.
- Daily absence, morning delay, and session delay notifications bypass Officer approval. Behavior and academic-concern guardian notifications require `Notification.ApproveDispatch`.
- Gate pickup authorization is not modeled as a delegate role/profile. Security performs and records manual/visual/screenshot verification.
- Biometric, Noor-export, and 14-quality-form permissions are not seeded while those features are `[BLOCKED - PENDING CLIENT INPUT]`.
- No `Student` Identity role is included because student login was not requested.
