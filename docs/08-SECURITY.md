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

## Storage S1 authorization boundary (2026-10-03)

Storage is default deny: actual manager or unrevoked, currently effective direct delegate within the selected active school; Instructor only own teacher files and the current grant. Database membership/user/school activity and role permissions are rechecked in Application services per request, including stale tokens, transfers and revocation. No global-role shortcut, old Instructor permission inheritance, public WebUrl authorization, or chained delegation. Manager grant/revoke additionally reads School.ManagerUserId inside a Serializable transaction; audit writes commit atomically with delegation.

File authorization proves the current Drive item remains within the current teacher/school root through the existing TeacherDriveFolderGuard after SQL identity/ownership validation. Raw provider IDs occur only in internal contracts, not the delegation API. Visit-archive content fails closed until the independent visit-visibility policy is wired in S5. S1 exposes no new file content/list/export routes and keeps administrative endpoints OFF by default. Existing teacher endpoints/guards are unchanged.

Backfill has no HTTP, Google token/credential-decryption or local student-attachment dependency. It retains encrypted Google settings unchanged; S0's live credential problem remains unresolved and blocks live validation/activation. See [evidence and constraints](specs/school-file-storage/verification/README.md).
