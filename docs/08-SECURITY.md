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

## Storage S2 content and mutations (2026-10-03)

New file/folder endpoints enforce current DB scope/permission/ownership/delegation and live Drive root/ancestor checks. Teacher clients supply internal folder/file IDs only. Public library roots cannot overlap teacher grants; grant changes cannot cover library/archive roots. Missing/trashed/out-of-root items cannot return bytes or restore availability; transient connection failure never marks a file missing. Protected history/Approved legacy decisions forbid rename/delete and containing-folder moves. Visit archives remain default deny until S5.

Uploads validate a normalized safe name, allowed extension, actual stream length <=250 MiB, signature/OOXML/no VBA, server-derived MIME and SHA256 via bounded temporary spool. Durable school/actor/key fingerprint + pre-generated Drive ID prevents a second upload after lost response/SQL failure; explicit reconciliation verifies saved metadata without automatic re-upload. These checks do not constitute antivirus scanning. Authenticated content has no-store/nosniff/sandbox; browser previews cap at20 MiB and revoke blob URLs. No public provider URL authorizes access. [S2 security/verification/limitations](specs/school-file-storage/verification/s2-library-and-uploads.md); flags remain OFF and credentials/keys untouched.

## Storage S3 review and retained history (2026-10-04)

Catalog/file/link/change operations use the same live selected-school boundary; historical reads additionally prove exact file/version scope and the current owner grant. Past authorization never authorizes a new request after delegation/grant revocation or teacher transfer. VisitArchive/HistoricalImport sources remain denied. TeacherId cannot override the asset's owner; school assets cannot become another teacher's own-file access. Reviews are independent and require a fresh link rowversion, rejection reason and authorized reviewer. Append-only DB decisions/audit and immutable request/version/provenance triggers protect against alternate writers.

Only active approved links with an approval for their current, available, authorized version count. Withdrawn/missing/trashed/out-of-root/inactive-owner/revoked-grant files stop counting; Drive transport failure fails closed without fabricating Missing. Approved file replacement requires a decided request, retains original bytes and previous decisions, then resets all active links to PendingReview. Before first approval, direct policy application still records its request/decision and cannot approve links; it rechecks approval history on completion. Candidate bytes use the original authorized teacher root even when the requester is the manager. Legacy review/rename/delete cannot bypass mapped-file history protection regardless of the flag. Both flags remain false and Google secrets/keys unchanged. [S3 evidence and rollback](specs/school-file-storage/verification/s3-evidence-and-review.md).

## S5 archive authorization and worker gates (2026-10-05)

Archive reads derive the school from authenticated ActiveSchoolId and recheck live membership/permissions/delegation. Management requires Storage.ViewArchive plus Visit.View; retry additionally requires Storage.RetryArchive. Delegation revocation/expiry takes effect on every request and after provider I/O. Moderator remains own-created visit scoped and cannot use administrative list; Instructor only own current Approved visit with Storage.ViewOwn, no colleague/old revision/retry/list. No platform-role shortcut or client school/root/provider ID. Stream delivery rechecks authorization/current revision/current root after I/O; returned DTOs contain only internal visit/version IDs and safe state/times.

Storage generic metadata/content/versions and EvidenceLink paths explicitly reject VisitArchive; library/readiness/index/export queries exclude it. Archival grants no evidence approval/readiness. Protected archive folder must remain below current school root and must not overlap any teacher grant in either direction. Live metadata, identity properties, length/MIME and actual SHA256 are required for success/download. Missing/trash/outside root fail closed; transport errors do not fabricate missing or trigger blind uploads.

Legacy teacher-drive list/details/content/breadcrumb also exclude/deny archive identities after an external move into the teacher grant, even if provider properties are stripped. The shared folder guard uses repository-provided reserved operation/folder/all-version IDs plus private archive metadata; list IDs are looked up in one SQL union query. Pure ancestry used for grant validation remains independent, so protected-folder overlap is still detectable. This protection is independent of storage flags.

Library native discovery uses the same deny-only provider identity predicate before indexing folders or exposing Managed/Unindexed files. Known archive IDs remain forbidden across externally moved school boundaries; the lookup reveals no school/report records, while all positive archive reads remain school/visit scoped.

All four SchoolFileStorage flags default false, including ArchiveWorkerEnabled and ArchiveExternalWritesEnabled. Snapshot capture in the approval transaction survives flagsOFF after schema application; no PDF generation/Google I/O until all worker gates are true. Google image URLs and redirects are blocked during capture. Stable reserved IDs and SQL session exclusion fence expired leases and uncertain provider success. Missing recreation needs authorized action/reason and retains original bytes/history. Safe logs/audits omit OAuth/provider responses/PDF; SQL/Drive identifiers never enter public URLs. [Evidence, limits and explicit activation/rollback](specs/school-file-storage/verification/s5-approved-visit-pdf-archive.md).

## S6 import security — D-100

Each import action requires current school membership/active DB role and Storage.ManageSchool, with direct delegation checked live; source names never grant membership. Foreign batch/row/requirement/file/member identifiers are scoped in the backend. Disabled context discloses only the actual school manager's minimal status, without storage-schema access. Content reauthorizes after provider I/O and audits successful reads; revocation during upload retains the reserved object while denying links/content. Inert source limits and CSV formula protection apply. SHA256 dedup is school-scoped and only reuses current available SchoolUpload assets; archive/historical protected types remain excluded. No provider IDs/public Drive links/server paths are returned. [S6 SQL and browser verification](specs/school-file-storage/verification/s6-import-and-rollout.md).
