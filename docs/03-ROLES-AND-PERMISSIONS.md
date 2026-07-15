# 03 — Roles & Permissions

**Status:** Baseline · **Last updated:** 2026-07-15

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
request details. Complaint management is visible only to the School Manager and
Super Admin support; the Instructor may submit from his own viewed approved report.
Moderators have no complaint access anywhere (D-75).

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

**Teacher access policy (D-83):** the first permission above is implemented by
the narrow `Instructor.View` permission, not `User.View`. A Moderator may open
`المعلمون` and an in-scope teacher profile only when the teacher has an active
Instructor assignment in the token's `ActiveSchoolId`; another school's teacher
returns 403. The Moderator receives no user-directory, create, edit, deactivate,
or delete permission. The profile's `زيارة جديدة` action reuses `Visit.Create`
with that in-scope teacher preselected.

**Restrictions:**
- Evaluation result does not appear to Instructor until School Manager approves it.
- **Must not access complaints anywhere**: no complaint route, sidebar item,
  dashboard count/content, Excel sheet, PDF section, or complaint API access
  (D-75). School Manager handles school complaints.
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
performance of instructors evaluated by this Moderator. No complaint widget or
complaint data is exposed.

## 5) Instructor / معلم
**Scope:** Own account only. Must have `ApplicationUser`; `InstructorProfile` links
to `ApplicationUser` and `SchoolId`.
**Can view:** only his own **Approved** evaluation reports and their approved PDF:
numeric score, performance level, strengths, weaknesses, moderator notes/name, and
the attached report. The dedicated report feed is server-filtered by
`Visit.InstructorId == currentUserId` **and** `Status == Approved`; query
parameters can never broaden that scope.
**Can do:** view the read-only approved report; download its approved PDF; submit
a complaint/review request from that report only after the successful report read
has recorded the view.
**Navigation:** exactly **الرئيسية** (Instructor dashboard), **تقاريري**
(own approved reports), and **إعدادات الحساب**. Complaint submission is an action
inside the approved report, not a sidebar entry. The Instructor never sees supervisor **الزيارات**,
its filters/export/actions, or **أداة التقييم**, teachers, users, assignments,
schools, or supervisor complaints.
**Restrictions:** cannot edit evaluations; cannot see other instructors or any
non-approved visit. The generic supervisor visit endpoints (list, detail,
analysis, view-status, and ZIP export) return 403 for an Instructor-only caller;
the Instructor must use the dedicated report feed and report endpoint.
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
