# School Duty workspace and live Drive amendment — 2026-10-14

This records C20–C23 / D-114–115. Earlier D3, C18 and C19 evidence remains historical; the following is the current workflow. Final owner visual approval and physical-device/OS keyboard checks remain pending. No later feature was started.

## Delivered behavior

School Duty has its own sidebar category. Managers receive schedule, settings, execution, archive, fairness and change history; teachers receive schedule, edit duty and archive. Settings have three focused stages: teachers/exclusions, timetable calendar and review/publication. Returning preserves reviewed configuration and server drafts. Publication navigates before refreshing, avoiding a race that reopened settings.

The schedule shows a full-width green nearest-own-duty card with the actual date, a prominent today/no-duty alert based on server school today, then `الفصل الدراسي` totals. Teacher/date cards show ten operating days per page with own/all/specific-teacher filters. Other teachers' roster cards expose no signing or document access. Execution is labelled `تحرير المناوبة`, fills the same available width, and defaults automatically to the current school day/current term. It has no date picker and never falls back to the next future duty. Old dated execution links open the archive. Route activation waits for settled query parameters and fences pending today requests; picker remount emissions for unchanged selections are ignored, preserving the inspected historical date. Archive alone provides term/date/assignment browsing, frozen document cards and retained revision history, labelled `أرشيف المناوبات`.

Historical inspection is read-only. Explicit **Continue report** in the archive preserves existing overdue completion for the actual current owner of an unsubmitted, non-future revision. Reasoned manager correction remains possible, with actual teacher re-signature and retained prior artifacts. Historical inspection does not hydrate unsaved local content over the retained view. Main navigation always returns to today. The daily report's late and violating categories have equal side-by-side desktop columns and separate stacked phone sections. Final review has no additional document/archive section.

Duty errors use a centered native top-layer dialog requiring **OK / موافق**, with current-language text, focused button, Tab containment and focus return. Escape/backdrop cannot silently dismiss errors. Failed preview, picker, download, permission and report checks retain appropriate input/context. Archive availability remains a status card distinct from local signing success. PDF download checks accept normal MIME parameters and validate actual PDF magic/size before preview/native download; no malformed response is offered as a PDF. The owner's earlier signed-letter failure was not reproduced against the current running API; a MIME mismatch is not asserted as its proven cause.

Application ownership remains authoritative: another teacher cannot take an assignment through acknowledgement. Receipt replay returns current access/ownership after emergency reassignment without re-signing. Future own duty cannot acknowledge, save or submit; school today derives from the frozen plan time zone, with a Honolulu boundary case tested independently of host time. Previously signed future artifacts remain readable and byte-identical. Past late completion is preserved.

Manager storage administration includes a duty-specific readiness/enable/pause card, separate from Visit archive. Actual root health is read before enabling; disconnected/disabled processing cannot pretend activation. Teachers cannot read or change manager settings, and duty retry does not grant storage administration. The original school's enablement remains **false**; the manager can choose activation manually after review.

## Actual results and boundaries

| Check | Actual result |
|---|---|
| Focused backend | **75 passed, 0 failed/skipped**: 24 D3 SQL + 21 D2 execution SQL + 22 D1 SQL + 6 rotation + 2 Visit operation checks. Fresh guarded LocalDB, real constraints/locks/rollback; [TRX](workspace-tests/duty-final.trx), [log](workspace-tests/backend-final.txt). |
| Focused Angular | **138 passed** covering duty/navigation/PDF/picker, timetable/intelligent timetable, shared selects, Messages and guardian dashboard; [log](workspace-tests/angular-final.txt). One newly added test fixture initially lacked required `DutyDay` fields; the fixture was corrected before the passing run. |
| Development build / current API | Angular development build passes; current Release API builds with zero warnings/errors. API5264 and Angular4200 are running with startup initialization disabled. No new migration is needed; original database remains at **68** migrations. [Readiness](workspace-readiness.json). |
| Current UI | [Results](organized-screens/browser-results.json): actual Angular dev server, **12 role/width journeys**, plus English error and denied teacher-settings checks. APIs are synthetic intercepted fixtures. 390/768/1440, keyboard focus/OK, equal columns, ten-day pagination/filters, today selection/no fallback, historical inspection/overdue resume, retained drafts, native PDF download and storage enable/pause presentation. |
| D1/D2/D3 UI regression | Three-width planning/manual-exchange/publication [D1](organized-regression-d1/browser-results.json); teacher/picker/draft/signature/conflict/correction/System/guardian/statistic views [D2](organized-regression-d2/browser-results.json); document/history/preview/recovery/error states [D3](organized-regression-d3/browser-results.json). Actual rendered Angular, mocked synthetic APIs; no real parent traffic. |
| Actual internal communication | The independent fresh SQL/HTTP/JWT workflow passes **39 checks**: [results](d3-live-http-results.json). Real hosted System guardian dashboard/Messages, read receipts and student/manager projections; explicitly confirmed synthetic enrollment/fact only. Its provider-double results remain **TestVerified**, independent of the real Drive run below. |
| Existing manual-test signed letter | Actual current API author/manager reads return `application/pdf`, 58,632 bytes, SHA-256 `AF4E696C5E97AF2BF3EAF7F4E5EB8CE6806E90DC4D9B1E28033A708DEB56F6E5`, unchanged after the future-date guard. Earlier actual native teacher/manager downloads and previews at all three widths remain valid. Signature-bearing original manual-school captures stay in private `.audit`, outside committed evidence. |
| Real Drive delivery/recovery | **15 passed checks**, **2 actual uploads**, [results](d3-live-drive-results.json). Real GoogleDriveClient, fresh isolated SQL, explicitly authorized `D3_Test` inside verified current synthetic-school root. One lost-response fault after actual upload and one real SQL rollback after successful upload; recovery verifies the same reserved IDs/bytes without a second upload or ledger version. Letter archives before report exists. Both finish **Archived**, with two immutable ledger versions and one five-folder duty hierarchy. |
| Real protected archived HTTP access | **10 passed checks**: author/manager local and archived letter/report reads hash-identical; other teacher local and archived reads return403. Actual isolated API and Google metadata/content; [results](d3-live-drive-http-results.json). No signing/submission/provider write during this read harness. |
| Real archive browser | Teacher/manager at390/768/1440: **6 journeys and18 native downloads** for letter/report/roster, real isolated duty APIs, real SQL artifacts and actual **Archived** output; PDF.js previews/hash equality. [Results](d3-live-drive-screens/browser-results.json). Only unrelated shell APIs are fixtures; roster remains local and adds no archive obligation. |

The designated destination is `D3_Test / Run-20261014-091349-28c0a41c / المناوبات / {year} / {term} / {actual teacher} / 2026-10-14`. The extra Run folder isolates verification runs only; ordinary school archive production hierarchy remains exactly the approved school-root hierarchy. Both synthetic PDFs remain available for owner inspection. No pre-existing provider data was changed/deleted, original roots/grants were preserved, and original school flags were not enabled. The private root reservation is persisted before folder creation; duty folder/artifact reservations and operations are committed to isolated SQL before their external writes. Repeat/restart reconciles reserved identities; ambiguous existing same-name folders without a trusted reservation are refused.

## Source and rendered PDF comparison

The renderer and template versions are unchanged from the [nine-document/fifteen-page source comparison](d3.md). That evidence covers original/substitute signatures, empty categories, long/duplicate names, >7 rows, continuation pages, long observations, unavailable frozen branding, retained/corrected reports and roster closures/exams. Private originals/fingerprints remain in [references](../references.md), never runtime defaults.

Both PDFs actually downloaded from Google were independently rasterized and visually inspected: [report page](d3-live-drive-screens/drive-report-1.png), [letter page](d3-live-drive-screens/drive-letter-1.png). Report preserves administrative header, form code/year/term, original teacher row/signature/notes, separate substitute row/signature/notes, late table, violation table, observations, departure and next-day handover/footer in source order. Letter preserves the obligation until the last student leaves and separate manager/name and actual-teacher signature positions, without manufacturing a manager signature. Connected Arabic, RTL columns, readable contrast/margins and no clipping were checked on both pages. These use synthetic people and a synthetic signature fixture. [Drive/SQL hashes](d3-live-drive-results.json) match [HTTP/native-download hashes](d3-live-drive-screens/browser-results.json); extraction/hash evidence is additional to visual inspection.

Principal latest UI captures: [schedule desktop](organized-screens/teacher-schedule-1440.png), [phone schedule](organized-screens/teacher-schedule-390.png), [phone no-duty-today](organized-screens/teacher-no-duty-today-390.png), [report desktop](organized-screens/teacher-report-1440.png), [review desktop](organized-screens/teacher-review-1440.png), [archive phone](organized-screens/teacher-documents-390.png), [error phone](organized-screens/teacher-required-error-390.png), [manager archive control](organized-screens/manager-archive-enabled-1440.png), and [real archived cards](d3-live-drive-screens/live-document-cards-1440.png). Browser keyboard checks are actual headless keyboard events; viewport simulation does not establish physical keyboard/assistive-technology acceptance.

## OAuth and preservation

The inherited production callback caused the owner's local `redirect_uri_mismatch`. The previously registered pilot callback was already documented in `DrivePilotSetup`: `http://localhost:5264/api/v1/school-google-drive/callback`. At the owner's request, only ignored local Development public GoogleOAuth redirect/completion URLs were overridden to5264/4200. Production configuration and all other Development fields/keys remain unchanged. Owner manually completed Google consent; live readiness became **Connected**. The owner-renewed encrypted refresh token is an expected credential change, not a claim of unchanged pre-consent ciphertext. Existing client secrets/root/grants/flags are preserved. [Final read-only preservation/readiness](workspace-readiness.json).

No agent-created credentials, new root grants, operational school activation, production deployment or destructive database action occurred. The live harness clones the existing encrypted OAuth connection into its fresh synthetic database and reuses existing keys with automatic generation disabled. Original DB access is read-only. Hosted workers are not started in the write harness. The read/browser host disables new duty processing and unrelated Visit writes. The running manual API has process-level duty processing ready, school flags remain off, and `SchoolFileStorage:ArchiveProcessingEnabled=false` stops unrelated Visit processing.

## Reproduction

```powershell
# From repository root: reject existing database BEFORE any migration.
$env:ALFALAH_DUTY_TEST_CONNECTION = 'Server=(localdb)\MSSQLLocalDB;Database=AlFalahDutyTests_Workspace_' + [guid]::NewGuid().ToString('N') + ';Integrated Security=true;TrustServerCertificate=true'
dotnet test backend/AlFalah.Tests --filter 'FullyQualifiedName~DutyDocumentSqlTests|FullyQualifiedName~DutyExecutionSqlTests|FullyQualifiedName~DutySqlTests|FullyQualifiedName~DutyTests|FullyQualifiedName~VisitArchiveOperationsTests' --logger 'trx;LogFileName=duty-final.trx'

# Bounded external run ONLY while the same D3_Test designation remains authorized.
# Fresh SQL only; retained private destination reservation reconciles the same D3_Test.
$env:ALFALAH_DUTY_TEST_CONNECTION = 'Server=(localdb)\MSSQLLocalDB;Database=AlFalahDutyTests_D3Drive_' + [guid]::NewGuid().ToString('N') + ';Integrated Security=true;TrustServerCertificate=true'
dotnet run --project docs/specs/school-duty/verification/d3-live-drive -- D:/AlFalah-Manage-System
# Reads retained test SQL/results, no migration/provider upload; temporarily hosts5391.
dotnet run --project docs/specs/school-duty/verification/d3-live-drive-browser -- D:/AlFalah-Manage-System

cd frontend
npm.cmd test -- --watch=false --browsers=ChromeHeadless --ts-config=tsconfig.d2-spec.json --include='src/app/features/timetables/**/*.spec.ts' --include='src/app/features/intelligent-timetable/**/*.spec.ts' --include='src/app/shared/components/clearable-select/**/*.spec.ts' --include='src/app/features/student-affairs/messaging-chat/**/*.spec.ts' --include='src/app/features/student-affairs/guardian-dashboard/**/*.spec.ts'
npx.cmd ng build --configuration development
node scripts/check-duty-workspace.mjs
$env:ALFALAH_DUTY_BROWSER_EVIDENCE='../docs/specs/school-duty/verification/organized-regression-d1'
node scripts/check-school-duty.mjs
$env:ALFALAH_DUTY_BROWSER_EVIDENCE='../docs/specs/school-duty/verification/organized-regression-d2'
node scripts/check-duty-execution.mjs
$env:ALFALAH_DUTY_BROWSER_EVIDENCE='../docs/specs/school-duty/verification/organized-regression-d3'
node scripts/check-duty-documents.mjs
```

Existing test databases are deliberately retained, never reset/dropped. Live destination IDs, synthetic catalog and hashes are in results; credentials/JWTs/config backups remain private ignored `.audit`. Do not copy those files into source control. If the trusted destination reservation is absent, the live harness refuses to adopt an existing ambiguous `D3_Test`.

## Activation, rollback and remaining gates

Manager: **مساحة الملفات → إدارة المساحة → أرشيف المناوبات → تفعيل الأرشفة والرفع التلقائي**. Verify the intended school/account/root and Connected readiness first. Manager activation uses existing frozen pending intents, never re-acknowledges or resubmits. A letter can upload independently. School enablement is audited and requires Duty.Manage; it grants no general storage administration. Manual activation remains an owner rollout choice after these synthetic results.

Rollback pauses duty auto-upload per school, or disables global new processing. Keep every draft, signature snapshot, lateness fact, System notification/message, frozen PDF, intent, folder/file and ledger version. Local authorized PDF reads continue during pause/disconnection. Never run destructive Down/remove history or delete provider artifacts as rollback. Visit processing stays separately controlled.

Known baseline limits remain: unrelated SQLite `ProtectedPassword` fixture, broad Angular storage-spec type errors, historical backend Restrict-versus-NoAction assertion, and production CSS budgets (storage39.34kB / Visit34.50kB versus32kB). Production build was rerun and failed only the known CSS budgets; budgets were not relaxed. These results do not establish production readiness, physical keyboard/assistive-device behavior or final owner appearance acceptance. No external archive gate is now missing for this **designated synthetic destination**; general operational rollout remains unperformed. Stop after D3.
