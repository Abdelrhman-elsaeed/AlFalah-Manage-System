# 08 — Security & Authorization

**Status:** Baseline · **Last updated:** 2026-07-10

## Critical rule
Every **school-scoped query must enforce `SchoolId` filtering in the backend**.
Do **not** rely only on Angular filtering.

## Role access matrix
| Role | Access |
|------|--------|
| School Manager | Only his school |
| Moderator | Only selected `ActiveSchoolId`; later only his own private records where required |
| Instructor | Only own records |
| Main Manager | Global access, **but cannot see complaint details** |
| Super Admin | Full access |

## JWT must include
- UserId
- Username
- Roles
- Permissions
- ActiveSchoolId (if school login)
- PreferredLanguage

## Backend validation checklist (on login / token use)
- [ ] User is active.
- [ ] User role is active.
- [ ] `UserSchoolRole` is active.
- [ ] School is active.
- [ ] User is assigned to the selected school.

## Credentials & secrets
- Use ASP.NET Core Identity; passwords **hashed**.
- **No** hardcoded real credentials.
- Do **not** store secrets in code.
- Development credentials documented only (dev), never used for production.
- Inactive users cannot login.
- User not assigned to selected school cannot login.
