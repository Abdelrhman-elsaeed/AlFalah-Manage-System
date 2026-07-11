# 03 — Roles & Permissions

**Status:** Baseline · **Last updated:** 2026-07-10

> Roles are **database-driven**. Do **not** hardcode role logic using only enums.
> Required model: ApplicationUser, ApplicationRole, Permission, RolePermission, UserSchoolRole.
> `UserSchoolRole` is required because a Moderator can work in more than one school,
> the same user may have different roles per school in the future, and school context
> must be selected at login for school users.

## Roles
- Super Admin / Developer
- Main Manager / مدير المدارس العام
- School Manager / مدير المدرسة
- Moderator / مشرف
- Instructor / معلم

---

## 1) Super Admin / Developer
**Scope:** Whole platform. **Login:** global login endpoint.
**Permissions:**
- Manage all users
- Manage Main Manager
- Manage all schools
- Manage roles
- Manage permissions
- View audit logs
- View error logs
- Manage system settings
- Access all modules if needed
- Full platform support

## 2) Main Manager / مدير المدارس العام
**Scope:** All schools. Separate login page & endpoint. **No** school selection.
**Permissions:**
- View all schools
- Add school
- Edit school
- Disable/delete school
- Add School Manager
- View reports across all schools
- View performance of all teachers
- Export Excel/PDF
- Manage permissions
- Manage global rubric/standards
- Manage global settings if needed
- View professional dashboard for all schools
- View aggregated improvement plan analytics

**Dashboard:** number of schools, school managers, moderators, instructors, visits;
aggregated reports; comparison between schools.
**Filters:** Academic year, Semester, School, Subject, Stage, Moderator.
**Restriction (critical):** Main Manager must **NOT** see teacher complaint/review
request details. Complaints are visible only to: School Manager, related Moderator,
the Instructor who submitted it, and Super Admin (for support if needed).

## 3) School Manager / مدير المدرسة
**Scope:** One school only.
**Rules:** must always be a real `ApplicationUser`; each school has exactly one;
`School.ManagerUserId` nullable initially; validation prevents activating a school without a manager.
**Permissions (inside his school):**
- Manage teachers/instructors; add/edit; disable/delete per soft-delete policy
- Manage moderators inside school
- Create visits
- Evaluate instructors directly
- Review moderator evaluations
- Edit evaluations
- Approve evaluations before they appear to instructors
- See all evaluations in the school
- Filter evaluations by status
- See complaints/review requests in the school
- Reopen visits
- Create improvement plans; manage them
- Add follow-ups
- Export reports
- Manage school report settings
- Manage own signature
- View school dashboard

**Dashboard:** number of instructors, number of moderators, visits this month,
instructors needing improvement, evaluations pending approval, subject performance,
complaints/review requests, Draft/Pending/Approved/Reopened visits, moderator
performance inside the school.

## 4) Moderator / مشرف
**Scope:** Selected school context; can be assigned to multiple schools.
**Login:** selects school before login; backend validates assignment; selected
school becomes `ActiveSchoolId` in token/session. In MVP, switching school
requires logout/login again.
**Permissions:**
- See all teachers/instructors in selected school
- See all subjects, grades, stages inside selected school
- Create visit; save draft; complete evaluation
- Edit old evaluations
- Delete visit if allowed by workflow
- Create improvement plans; add follow-ups
- Upload evidence attachments
- Print/export report
- See complaints only for reports/evaluations created by this Moderator

**Restrictions:**
- Evaluation result does not appear to Instructor until School Manager approves it.
- Must not see complaints created against other moderators' reports.
- Must not see private visit activity of other moderators.
- Dashboard should only include his own work where relevant.
- **Visibility scoping (enforced in the backend — see deviation D-37)**: a
  Moderator sees ONLY visits he created himself
  (`Visit.CreatedByUserId == currentUserId`) within his `ActiveSchoolId`.
  He must NOT see visits created by another Moderator or by the School
  Manager. The list endpoint (`GET /api/v1/visits`) filters the query
  with `Where(v => v.CreatedByUserId == currentUserId)` for Moderator-only
  callers (added after the school-scope guard); the detail endpoints
  (`GET /visits/{id}`, `GET /visits/{id}/analysis`, `GET /visits/{id}/view-status`)
  each call `EnsureModeratorCanAccessCreatedByVisit(visit)` and return 403
  Arabic ("لا تملك صلاحية الوصول إلى زيارات المشرفين الآخرين في مدرستك.")
  when the visit was created by someone else. School Manager / Super Admin /
  Main Manager / Instructor behaviour is unchanged — global admins bypass
  the filter (`IsGlobalAdmin() = true`); School Manager keeps full-school
  visibility; Instructor path is unchanged (D-36 still controls score
  visibility). No UI-only filtering relied upon for security.

**Dashboard:** all instructors in selected school (for visit creation), today's
visits, draft visits, open improvement plans, evaluations pending approval, average
performance of instructors evaluated by this Moderator, complaints related only to
this Moderator's own reports.

## 5) Instructor / معلم
**Scope:** Own account only. Must have `ApplicationUser`; `InstructorProfile` links
to `ApplicationUser` and `SchoolId`.
**Can view:** own evaluation results, detailed report, numeric score, performance
level, strengths, weaknesses, improvement plans, follow-ups, comparison between
visits, moderator notes, moderator name, attachments/reports, latest evaluation,
performance development.
**Can do:** view report; download approved PDF if allowed; submit complaint/review
request; when Instructor opens a report, system records that the report was viewed.
**Restrictions:** cannot edit evaluations; cannot see other instructors; cannot see
other teachers' data.
**Dashboard:** latest evaluation, performance trend, strengths, improvement points,
improvement plans, follow-ups, attachments/reports, report view status, request
review/complaint button.

---

## Seed permission codes (Phase 1)
| Code | Category |
|------|----------|
| Schools.View | Schools |
| Schools.Create | Schools |
| Schools.Update | Schools |
| Schools.Delete | Schools |
| Users.View | Users |
| Users.Create | Users |
| Users.Update | Users |
| Users.Delete | Users |
| Roles.Manage | Roles |
| Permissions.Manage | Permissions |
| Auth.Login | Auth |
| Audit.View | Audit |
> More permissions will be added later.
