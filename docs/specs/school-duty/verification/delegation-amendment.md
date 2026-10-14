# C25 — Duty delegation and matching dashboard width

2026-10-14. Implemented under the owner's explicit new School Duty delegation request. This authorization extends duty administration only; general feature delegation and later features remain deferred. Owner appearance and physical-device acceptance remain pending.

The summary remains at the end of the general account dashboard and uses its existing1440px maximum width/centered margins. Actual1920px geometry measured both dashboard and duty summary at **1440px, x134**, with identical boundaries at390/768/1440 too. [Four-width results](delegation-screens/browser-results.json), [wide desktop](delegation-screens/dashboard-width-1920.png).

Manager: **School Duty → Duty permissions** (/school-duty/permissions). Select any active current-school account from role groups; moderators are grouped explicitly, while teachers, secretaries, student-affairs staff, social workers, security, guardians and other existing roles remain selectable. Duplicate display names include existing usernames; requests bind stable user IDs, never a browser-selected school or destination. A successful grant appears in the target's sidebar and general dashboard after page refresh/current identity verification. A delegate receives settings, schedule, follow-up, archive, fairness and history. Only an actual active local SchoolManager with live Duty.Manage can grant/revoke; the seventh permissions page is manager-only. Existing role-based managers appear as inherited access and cannot be revoked through this feature.

Revoke uses an explicit confirm/cancel dialog. Errors use the existing centered language-aware OK; picker selection survives conflict/failure. The administrative list has search/ten-row paging, inactive retained grants can be revoked, and history remains retained. [Granted desktop](delegation-screens/permissions-granted-1440.png), [phone picker](delegation-screens/permissions-selected-390.png), [tablet](delegation-screens/permissions-granted-768.png), [revoke confirmation](delegation-screens/permissions-revoke-confirm-390.png), [conflict](delegation-screens/permissions-conflict-390.png).

## Persistence, scope and recovery

Thin GET /api/v1/school-duty/administrators and PUT /api/v1/school-duty/administrators/{userId} return ApiResponse and delegate decisions to DutyAdministrationService. Scoped repositories reuse UserSchoolRoles, RolePermissions and AuditLogs, with no new entity/table/migration. The fixed SchoolDutyAdministrator role is provisioned only inside the first explicit grant transaction and has exactly Duty.View/Duty.Manage. It is never added to global UserRoles; original account roles, dashboards and other permission families remain unchanged. There is no automatic user grant at startup.

The existing per-school transaction/application lock serializes mutations. A shared reserved-role lock protects cross-school role provisioning; the existing unique UserId/SchoolId/RoleId index prevents duplicate memberships. A request includes the retained UpdatedAt version. Duplicate identical state is idempotent; a stale opposite-state request conflicts409. Membership and audit commit or roll back together. No file/signature/student fact/notice or plan assignment changes as a side effect of granting access. Existing signed PDF provenance remains immutable.

Live authorization requires an active user/school and ordinary active local membership in addition to the duty grant. Removing/deactivating that membership, disabling the account/role or revoking the grant removes administration even with an old token. Auth/me and school login derive Duty permissions from live current-school access; primary role labels and unrelated permissions are preserved. Multi-school identity uses the authenticated active-school claim. A delegate cannot grant others, gain Storage.ManageSchool, sign for another assigned teacher or bypass protected storage/archive paths. Local/private documents and archive readiness/retry use the existing duty service authorization.

## Actual results

| Check | Result | Boundary |
|---|---|---|
| Focused backend suite | **82 passed,0 failed/skipped**:74 SQL +6 rotation +2 Visit operations | Fresh guarded synthetic LocalDB, existing68 migrations/reapplication/sentinel, no operational migration. [TRX](delegation-tests/backend.trx) |
| Added SQL cases | Grant/revoke/live identity; every role including guardian; delegate cannot recursively grant or sign another teacher; exact two permissions/no global role; concurrent duplicate produces1 row/1 audit; stale regrant rejected; foreign/inactive/ordinary-membership-loss denial; SQL audit failure rolls back both grant and audit | Actual SQL queries, transactions, locks/unique index and injected SQL failure, not provider mocks |
| Focused Angular suite | **146 passed** | Existing affected timetable/duty/messaging/guardian/control tests plus grouping/version/duplicate-click/conflict/revoke/duplicate-name/manager-only guard checks. [Output](delegation-tests/angular.txt) |
| Authenticated HTTP | **16 passed**,2 audit rows | Actual isolated running API/JWT/SQL: manager lists/grants, target auth/me gains Duty.Manage without storage administration, context/dashboard/archive/readiness, actual preview/publication, denied delegation/other-teacher signing, manager revoke and immediate auth/API denial. [Results](delegation-http-results.json) |
| Current browser |390/768/1440/1920 passed | Actual Angular/PrimeNG grouped picker, grant/revoke/cancel, conflict/retained selection, delegate six pages/cannot-open-permissions, summary DOM placement and matching geometry. **Synthetic intercepted APIs**, not live operational grants. [Results](delegation-screens/browser-results.json) |
| Existing workspace regression |Passed all teacher/manager scenarios | Actual browser with synthetic APIs; focused setup/teacher drafts/signing/review/PDF download/error/picker/current-day/archive and storage activation fixtures. [Results](organized-screens/browser-results.json) |
| Builds |Development passed; Release API0 warnings/errors | Running local API5264/Angular4200; startup initialization false |
| Production build |Known storage39.34kB/Visit34.50kB versus32kB stylesheet failures only | Re-run; budgets unchanged. [Output](delegation-tests/production-build.txt) |

The simple grouped dropdown seam adds optional PrimeNG grouping properties to the existing ClearableSelectComponent; defaults preserve existing callers. Its affected regression suite passes. Screens were visually inspected for Arabic names, grouped choice, spacing, phone confirmation/actions, centered summary and wide-screen alignment. The synthetic general-dashboard fixture deliberately returns unrelated dashboard errors; these captures do not establish a live dashboard API failure. Browser keyboard events/viewport simulation are not physical-device acceptance.

The HTTP harness rejects an existing/non-isolated database before any migration, preserves the fixture sentinel, uses a separate5392 process with initialization and duty/Visit external writes disabled, and creates only synthetic users/grants. No Drive connection/upload or real-parent notices are used. Logs/credentials/tokens remain private under ignored .audit. Output contains only sanitized results.

## Reproduce

```powershell
# Root. Always choose a fresh catalog; existing catalogs are rejected, never reset.
$env:ALFALAH_DUTY_TEST_CONNECTION='Server=(localdb)\MSSQLLocalDB;Database=AlFalahDutyTests_Delegation_'+[guid]::NewGuid().ToString('N')+';Integrated Security=true;TrustServerCertificate=true'
dotnet test backend/AlFalah.Tests --filter 'FullyQualifiedName~DutyAdministrationSqlTests|FullyQualifiedName~DutyDocumentSqlTests|FullyQualifiedName~DutyExecutionSqlTests|FullyQualifiedName~DutySqlTests|FullyQualifiedName~DutyTests|FullyQualifiedName~VisitArchiveOperationsTests'
$env:ALFALAH_DUTY_TEST_CONNECTION='Server=(localdb)\MSSQLLocalDB;Database=AlFalahDutyTests_DelegationHttp_'+[guid]::NewGuid().ToString('N')+';Integrated Security=true;TrustServerCertificate=true'
dotnet run --project docs/specs/school-duty/verification/duty-delegation-http -- D:/AlFalah-Manage-System
cd frontend
npm.cmd test -- --watch=false --browsers=ChromeHeadless --ts-config=tsconfig.d2-spec.json --include='src/app/features/timetables/**/*.spec.ts' --include='src/app/features/intelligent-timetable/**/*.spec.ts' --include='src/app/shared/components/clearable-select/**/*.spec.ts' --include='src/app/features/student-affairs/messaging-chat/**/*.spec.ts' --include='src/app/features/student-affairs/guardian-dashboard/**/*.spec.ts'
node scripts/check-duty-delegation.mjs
node scripts/check-duty-workspace.mjs
npx.cmd ng build --configuration development
```

Original local database remains at68 migrations, with no schema change/operational grant/activation performed by verification. [Preservation/readiness](workspace-readiness.json) confirms roots/grants/client secrets/configuration/existing keys preserved and original duty school flags disabled; the owner-renewed connection remains intact. Current Release API5264 and Angular4200 serve this amendment. Managers choose operational grants themselves from the new page.

Rollback revokes the duty membership through the page, retaining audit/history and all signed documents/facts/artifacts. General archive rollback still pauses new processing, never deletes history or provider content. Previously verified real Drive/source/PDF evidence is retained unchanged; this amendment performed **zero provider writes**. Known unrelated SQLite fixture, broad Angular storage-spec typing, historical model assertion and CSS budgets remain unresolved. Final owner/physical review pending; no generic feature delegation or later work.
