# Account access, school people and signature enrollment

The people sidebar lists school managers, teachers, moderators, secretaries, student affairs officers, social workers (student counselors), security guards and guardians. Staff and guardian accounts can be created through the existing user workflow. Guardian creation already creates the required guardian profile; linking children remains part of student affairs. Forms preserve the person's role in the selected school when editing an account assigned to multiple schools.

`/account/signature` contains the signature editor. `/account/settings` contains password changes and existing instructor teaching settings. Both links are available to every authenticated role.

## First signature

All roles except `SchoolManager` must save a signature before entering the authenticated application. A full-page welcome appears before the application shell and resumes the original destination after successful saving. The explanation accurately describes use within platform reports and workflows. A previously saved signature skips enrollment; the signature page remains available for later changes.

The route guard checks `GET /api/v1/account/signature/requirement`. The API also enforces enrollment: unsigned authenticated users receive HTTP 403 with `X-Signature-Required: true` on protected API operations. Authentication and signature endpoints remain accessible. A failed or blank save does not unlock the application. Signature saving validates the PNG data URL, size, image header and dimensions; the editor also rejects an empty canvas.

## Managed password disclosure

Role tables and the teachers table display a password column only to school managers and global administrators with `User.Edit`. Passwords start masked. Clicking the eye sends `POST /api/v1/users/{id}/password/reveal`; the response is non-cacheable. The visible secret is kept only in component memory and hidden after twenty seconds or when the component changes or is destroyed.

Identity keeps its standard password hash. `ManagedPasswordHasher` also saves a separate Data Protection encrypted copy whenever Identity creates, changes or resets a password. The encrypted payload is bound to the user ID and current hash. It is excluded from JSON entity serialization and ordinary account DTOs. It uses the existing persistent Data Protection key directory outside `wwwroot`; those keys must be retained with the application to decrypt managed passwords across restarts.

Existing hash-only passwords cannot be recovered. These accounts show an unavailable message until the password is changed or set through Identity. No migration resets a password or manufactures a replacement. A missing key, modified ciphertext or mismatched hash also returns unavailable.

School managers must have an active manager assignment to their selected active school, and targets must have active assignments in that school. Other roles and cross-school requests are rejected. Only a super administrator can reveal a global administrator's password. Every permitted reveal attempt records actor, target, school and availability in the audit log, without recording a password, ciphertext or hash.

Migration `AddManagedPasswordDisclosure` adds only the nullable `Users.ProtectedPassword` column. It does not modify existing accounts or signatures.

The local migration has been applied to `AlFalahDb`. Run `./restart-backend.ps1` from the repository root to activate the new API. The script now builds Release by default so it can build while the previous Debug API is running. It verifies that the listening process belongs to this application before restarting it, and keeps startup seeding disabled. Automatic execution of the restart was rejected by command policy in this session.

## Validation

- Targeted backend tests cover encrypted hashing, Identity create/change/reset, role and school boundaries, audit secrecy, legacy passwords, staff validators and API signature enforcement.
- The Angular development build succeeds.
- `node frontend/scripts/check-account-access.mjs` checks the built application using isolated API fixtures: all role tables, new account creation, correct school role while editing, reveal/hide and legacy behavior, separate account pages, enrollment save failures and success, reload behavior and mobile overflow. It writes screenshots under `frontend/test-results/account-*.png` and never writes to the live database.
- `node frontend/scripts/check-manager-dashboard.mjs` checks the existing dashboard layout and confirms the removed analytics stay absent.
