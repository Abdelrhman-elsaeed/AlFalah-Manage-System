# C24 — School Duty administrator verification

Date: 2026-10-14. Status: implemented and locally verified; owner appearance and physical-device acceptance remain pending. This extends D3 within the owner's four administrator requests and subsequent reload/button correction. Ordinary teacher presentation remains approved and unchanged. No later feature delegation, waiting register or external messaging was started.

## Delivered behavior

Setup steps sit outside the content card. The owner rejected a continuous green strip; the final design uses individual white buttons, an outlined/tinted active button and visible keyboard focus. Steps remain available while scrolling. Calendar/preview fill the administrative width; compact teacher and rotation lists show ten items with search and unchanged exclusion/restoration/move actions. Fairness and change history also page ten records. The setup footer no longer overlays rows. Pagination isolates numeric text from RTL direction.

Untouched setup no longer triggers the browser reload warning. Search, step navigation and automatic initial regeneration dates are not edits. Normalized draft comparison detects actual settings changes; changing custom time back to its original default clears the warning. Changed settings, pending operations and unsaved teacher drafts retain protection. A native browser reload was exercised without a dialog; changed settings still cancel beforeunload. The existing section draft retention and explicit error OK/focus behavior continue.

The administrative archive shows one table: duty date/actual teacher, responsibility letter on the RTL right, report on the left, and history. It supports school/term/teacher/date filters, ten groups per page, independent local/archived availability, retained correction/history, PDF preview/native download and authorized retry. Phone rows retain two equal document columns under their identity. SQL performs count/filter/order/Skip/Take, then fetches at most twenty latest signed documents/artifacts for the page. Earlier signed corrections remain available through existing history. Unsigned future dates are omitted by default; signed retained artifacts remain accessible. Explicit date filters allow future inspection.

Index delivery status comes from the committed archive intent and last verification; it does not probe Drive once per row. The worker and explicit archived reads still reconcile/verify provider identity/bytes and report moves, missing content or disconnection truthfully. No archive completion is inferred from a PDF read. Permission denial clears index/preview/history; browser destinations/provider IDs are not accepted. Existing immutable artifact/signature/school enforcement is reused.

The dismissal setting is nullable TimeOnly in existing versioned SettingsJson. Default still resolves the final study-day timetable period. Custom time is school-local and uses the existing preview digest, plan revision, publication transaction and regeneration range. When setting/calendar revision changes, append-only SettingsTimingPreserved events retain the prior base time for protected/past/locked/manual/exchanged/range-preserved assignments. Explicit daily exceptions have priority; preservation does not invent an exception reason or bypass lateness confirmation. Signed snapshots and PDF hashes remain unchanged. No schema migration is needed.

Today's effective duty teacher and the preceding seven calendar dates appear on the general account dashboard, outside the duty pages. The range is explicitly shown; no completion/attendance is invented. Active school membership and live database Duty.Manage grant administrator reads independently of role label/recorded primary-manager identity. Signing remains restricted to the actual effective teacher. Existing permission administration is used; no operational grants or generic delegation UI were created. The summary clears data on school change/revocation and refreshes while visible. Ordinary teachers make no administrative dashboard request.

## Actual verification

| Check | Actual result | Boundary |
|---|---|---|
| Backend focused suite | **78 passed, 0 failed/skipped**:70 real SQL +6 rotation +2 Visit archive operation checks | Fresh uniquely named guarded LocalDB, synthetic schools/accounts; retained, never reset/dropped. [TRX](admin-tests/backend.trx), [summary](admin-tests/results.json) |
| C24 SQL behavior |36 archive groups page/filter without overlap, latest corrected report/three retained documents, unchanged prior PDF; non-primary scoped administrator access, revoked membership and foreign-school denial; protected custom time/exception precedence | Actual SQL Server EF queries, existing triggers/transactions/constraints; no provider writes |
| Angular affected/regression suite | **141 passed** | Timetable/duty/settings/clearable select/intelligent timetable/internal messaging/guardian dashboard. [Output](admin-tests/angular.txt) |
| Current administrator browser |390,768,1440:105 synthetic teachers,103 archive groups, ten-item paging/search, custom13:45 preview/publication, all administrator tabs, native PDF/download/hash, history/retry/filter/empty/denied/revoked states, reload protection | Actual Angular server/browser/keyboard; **intercepted synthetic APIs**. [Results](admin-screens/browser-results.json) |
| General dashboard authorization |SchoolManager at three widths; actual-capability StudentAffairsOfficer dashboard/settings; ordinary Instructor does not request/render summary | UI APIs mocked; live database capability/actual-author/foreign-school/revocation verified separately in SQL |
| Existing workspace regression |Ordered manager pages/setup retention, teacher letter/report/review/submission/server draft/picker recovery, centered language-aware explicit OK, cards/filter/today/archive inspection, storage enable/pause | Actual browser, synthetic APIs; [current results](organized-screens/browser-results.json) |
| Retained actual Drive HTTP | **14 passed**:8 teacher/manager local and archived letter/report reads,2 other-teacher denials,4 administrative archive/dashboard200/403 checks | Actual isolated API/JWT/SQL and existing owner-designated Drive test artifacts. [HTTP results](d3-live-drive-http-results.json). **No new upload** |
| Retained actual Drive browser | **6 role/width journeys,18 native downloads**, letter/report/roster preview and SHA256 equal to frozen SQL/provider output | Actual authenticated duty/document/archive APIs, SQL and real archived content; unrelated shell APIs are synthetic. [Results](admin-live-screens/browser-results.json) |
| Development / Release |Angular development build passed; API Release build0 warnings/errors; manual API5264/Angular4200 respond | [Development output](admin-tests/development-build.txt); startup database initialization false |
| Production build |Failed only known unrelated storage39.34kB/Visit34.50kB stylesheet budgets against32kB | [Output](admin-tests/production-build.txt); budgets unchanged, no claim of production readiness |

The inherited future-duty test attempted to move a seeded immutable assignment into another seeded date. SQL correctly rejected the collision/identity mutation. The test now selects an existing owned future assignment, retaining the real constraints and testing frozen school-local time without date-dependent collisions. The non-primary revocation fixture revokes the actual database membership directly, avoiding a detached entity after repository transaction tracking resets. Final78 checks pass; no SQL constraint was weakened.

## Rendered inspection and PDF fidelity

Principal captures: [teacher setup desktop](admin-screens/admin-teachers-1440.png), [setup phone](admin-screens/admin-teachers-390.png), [calendar tablet](admin-screens/admin-calendar-768.png), [publication phone](admin-screens/admin-preview-390.png), [administrative follow-up](admin-screens/admin-followup-1440.png), [archive desktop](admin-screens/admin-archive-1440.png), [archive tablet](admin-screens/admin-archive-768.png), [archive phone](admin-screens/admin-archive-390.png), [history](admin-screens/admin-document-history-1440.png), [denied read](admin-screens/admin-denied-pdf-390.png), [general manager dashboard](admin-screens/manager-general-dashboard-1440.png), [authorized officer](admin-screens/authorized-officer-general-dashboard-1440.png).

Rendered output was visually inspected across the three widths: long/duplicate Arabic names, two document columns, equal actions, no horizontal overflow, bounded teacher/preview lists, numeric direction, active button visibility, calendar44px geometry, footer overlap, focus return and readable dashboard contrast. Refinements removed the green strip/footer overlay, widened the history heading and explicitly set light text on the green summary. The general-dashboard synthetic fixture deliberately returns unavailable for unrelated dashboard APIs; any unrelated error shown in these captures is **mock presentation**, not evidence that the live general dashboard API failed.

The PDF renderer/source mapping was not changed. Current actual PDF.js previews and native letter/report/roster downloads reuse the source-faithful official artifacts already inspected against originals; current native SHA256 matches retained real Drive/frozen SQL hashes. [Live administrator archive](admin-live-screens/live-manager-document-cards-1440.png), [live letter preview](admin-live-screens/live-manager-letter-preview-1440.png), [live report preview](admin-live-screens/live-manager-report-preview-1440.png). Full source/page comparison remains in [D3 verification](d3.md) and [real Drive comparison](workspace-amendment.md). No private source sample, real signature or account/token was added to the repository.

## Reproduction

```powershell
# Repository root. The fixture rejects an existing catalog before any migration.
$env:ALFALAH_DUTY_TEST_CONNECTION = 'Server=(localdb)\MSSQLLocalDB;Database=AlFalahDutyTests_Admin_' + [guid]::NewGuid().ToString('N') + ';Integrated Security=true;TrustServerCertificate=true'
dotnet test backend/AlFalah.Tests --filter 'FullyQualifiedName~DutyDocumentSqlTests|FullyQualifiedName~DutyExecutionSqlTests|FullyQualifiedName~DutySqlTests|FullyQualifiedName~DutyTests|FullyQualifiedName~VisitArchiveOperationsTests' --logger 'trx;LogFileName=admin.trx'

cd frontend
npm.cmd test -- --watch=false --browsers=ChromeHeadless --ts-config=tsconfig.d2-spec.json --include='src/app/features/timetables/**/*.spec.ts' --include='src/app/features/intelligent-timetable/**/*.spec.ts' --include='src/app/shared/components/clearable-select/**/*.spec.ts' --include='src/app/features/student-affairs/messaging-chat/**/*.spec.ts' --include='src/app/features/student-affairs/guardian-dashboard/**/*.spec.ts'
npx.cmd ng build --configuration development
node scripts/check-duty-admin.mjs
node scripts/check-duty-workspace.mjs

# From root, retained isolated D3_Test verification only; no migration/new upload.
$env:ALFALAH_DUTY_LIVE_EVIDENCE = '../docs/specs/school-duty/verification/admin-live-screens'
dotnet run --project docs/specs/school-duty/verification/d3-live-drive-browser -- D:/AlFalah-Manage-System
```

The live read harness validates the retained synthetic SQL sentinel and matching designated test root before hosting5391, disables duty/Visit writes and startup migrations, and requires authenticated role requests. It retains secrets/JWTs under ignored .audit only. Ordinary SQL tests use provider doubles labelled TestVerified; real Drive Archived evidence remains separate. The earlier15 real provider recovery checks/two uploads are retained in [live results](d3-live-drive-results.json); this amendment performed no new provider write or test notice.

## Manual readiness and remaining gates

Current Release API runs at http://localhost:5264 and Angular at http://localhost:4200. Original database remains at68 migrations; no new migration was run/needed. [Read-only preservation/readiness](workspace-readiness.json) confirms original roots, grants, client secrets, other configuration and existing keys preserved, original enabled duty schools0, startup initialization off, duty process ready and unrelated Visit processing disabled. Owner-renewed Google consent and previously authorized local public callbacks remain intact.

Use existing school archive readiness/enablement controls when choosing operational rollout. Rollback disables new processing/per-school upload while retaining signed snapshots, facts, PDFs, intents, history, folders/files and ledger versions; authorized local PDFs remain available. Never use destructive Down or delete history/provider data.

Owner appearance acceptance, physical devices/OS keyboard/assistive-technology review and operational activation remain pending. Existing SQLite ProtectedPassword fixture, broad Angular storage-spec typing, historical Restrict-versus-NoAction assertion and production CSS budgets were not repaired or relaxed. Browser keyboard checks are actual headless events; viewport simulation is not physical-device acceptance. Stop after School Duty.

## Owner dashboard placement correction

2026-10-14: move the duty summary to the end of the general account dashboard, after its header/tools and complete routed content. The summary is the last direct child of shell-content after the routed component; top spacing separates it from preceding content. Actual browser verification passed390/768/1440 manager, authorized officer and ordinary teacher isolation. [Results](dashboard-footer-screens/browser-results.json), [desktop](dashboard-footer-screens/manager-footer-1440.png), [phone](dashboard-footer-screens/manager-footer-390.png). Dashboard APIs here are synthetic; this small presentation correction did not repeat SQL/Drive tests or modify backend/settings. Local Angular4200 serves the update; final owner review remains pending.
