# 04 — Auth & Login Flows

**Status:** Baseline · **Last updated:** 2026-07-10

There are **two** login experiences.

## A) School User Login
**Used by:** School Manager, Moderator, Instructor.

**Flow:**
1. User selects **school** first.
2. User enters username / password.
3. Backend validates username / password.
4. Backend validates user assignment to the selected school through `UserSchoolRole`.
5. If not assigned to the selected school → **reject** login.
6. If valid → return **JWT + refresh token**.

**Token must include:**
- UserId
- Username
- Roles
- Permissions
- ActiveSchoolId
- PreferredLanguage

**Notes:**
- Moderator sees only schools assigned to him in the school dropdown.
- In MVP, school switch requires **logout/login**.

**Endpoints:**
- `POST /api/v1/auth/school-login`
- `GET /api/v1/schools/for-login` (school lookup for login dropdown)

## B) Main Manager Login
**Used by:** Main Manager, Super Admin.

**Flow:**
1. **No** school selection.
2. Username / password only.
3. Token has **global scope**.
4. User can access global dashboards.

**Endpoint:**
- `POST /api/v1/auth/main-manager-login`

## Other auth endpoints
- `POST /api/v1/auth/refresh`
- `POST /api/v1/auth/logout`
- `GET  /api/v1/auth/me`
- `POST /api/v1/auth/forgot-password`
- `POST /api/v1/auth/reset-password`

## Auth rules
- Use **ASP.NET Core Identity**.
- Passwords must be **hashed**.
- **No** hardcoded credentials.
- Use **JWT + Refresh Tokens**.
- Email exists for password recovery.

See [08-SECURITY.md](08-SECURITY.md) for the backend validation checklist.
