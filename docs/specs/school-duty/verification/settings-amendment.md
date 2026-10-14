# School Duty settings amendment — 2026-10-14

C19 / D-113 records the owner's three requested changes. The implementation extends the existing independent `/school-duty` workspace and existing settings/publication/reconciliation contracts.

## Delivered behavior

New setup includes all active school teachers. Managers can quickly exclude a teacher permanently or choose dates, or use the searchable exclusion picker with the same two modes. Permanent applies to this duty plan until restored; dated exclusions retain dates/reason and remain independent when permanent eligibility is restored. Duplicate permanent additions do not duplicate IDs. Teacher identities remain distinct for duplicate names, rotation ordering remains editable, and newly active teachers enter the next reviewed configuration. Legacy selected-pool omissions become explicit permanent exclusions when reviewed; no published plan is silently regenerated. Inactive teachers remain outside the reviewed rotation with historical records retained.

Duty weekdays are read-only study-day chips derived by the scoped repository from the current valid published non-clone timetable timing revision. Before lesson publication, the latest configured school/year/semester setup's attached active timing revision supplies study days. There is no guessed Sunday–Thursday fallback. Without a configured calendar, preview is disabled with an actionable timetable-timings link. Application validation ignores caller weekday substitutions, derives the trusted days again, and includes the timing revision in the preview digest; a changed revision requires a fresh preview even if weekdays are identical. Existing explicit acceptance of unsupported staff-attendance weekdays remains when the school studies on Friday/Saturday.

Operating from/to, future-regeneration bounds, closures and review/publication remain. Past, manually protected, exchanged and locked assignments retain existing protections. Published normalized settings and explicit exclusions are retained through the existing JSON publication history. No new database columns/migration, signature application, PDF renderer change, lateness confirmation or guardian notice is caused by editing, previewing or reading these settings.

Calendar inputs and icon buttons both measure **44px**, with matching vertical positions in RTL/LTR at all three widths. Date fields have associated labels. Long teacher names wrap; mobile exclusion actions wrap below identity, and rows do not shrink into overlapping buttons. Quick permanent exclusion/restoration returns keyboard focus to the replacement action; quick dated exclusion uses the canonical school duty date and focuses its date field. Failed previews retain the draft, exclusions and useful actions.

## Actual checks

| Check | Result / evidence |
|---|---|
| Backend affected suite | **72/72 passed**: six rotation/reconciliation checks, 22 real D1 SQL checks, 21 real D2 SQL checks, 21 real D3 SQL checks and two visit processing/status checks. [Executed log](settings-tests/backend.txt). |
| SQL boundaries | Fresh `AlFalahDutyTests_SettingsVerified_*` integrated-security LocalDB, rejected existing DB before any migration, sentinel preserved across non-destructive reapplication. No original database migration. |
| New SQL coverage | All-by-default normalization; forged weekdays replaced by trusted study days; explicit permanent and dated exclusion/restoration; same-day dated exclusion retained on restoration; foreign/non-teacher/duplicate/all-teacher exclusions denied; no calendar cannot mutate existing plan; identical-days timing revision invalidates preview; published calendar takes precedence over setup/clones/invalid timing. |
| Regression SQL coverage | Repeated absence/effective queue, manual protection, renewal/compensation, atomic rollback/races, school/teacher/manager scope, signature/profile immutability, emergency history, corrections, System messages/facts/counts, immutable PDFs, protected storage, reservations/leases/uncertainty/recovery and provider doubles remain covered. All Drive results here are test-provider results, not real delivery. |
| Focused Angular | **114/114 passed**, including nine new settings checks and retained navigation, timetable/setup, picker, document, guardian and human-message checks. [Log](settings-tests/angular.txt). |
| Frontend/backend builds | Development Angular build passed; current Release API build passed with **zero warnings/errors**. [Frontend](settings-tests/frontend-build.txt), [API](settings-tests/api-build.txt). Production CSS budgets were not changed or claimed resolved. |
| Current running UI | [Settings browser results](settings-screens/browser-results.json): actual localhost4200 Angular server, synthetic intercepted API only; three widths in Arabic/English, all included, both exclusion modes/restoration, duplicate safety, canonical date picker, equal height/alignment, keyboard focus/Enter, failed-preview retention, missing-calendar action, no page errors/overflow. |
| D1/D2/D3 rendered regression | Actual built Angular SPA with synthetic APIs at 390/768/1440; D1 settings/order/closures/exclusions/publication/manual review, D2 signature/draft/student picker/System/human messaging/corrections, D3 document previews/native fixture downloads/history/retry/recovery all passed. [D1](settings-tests/d1-browser.txt), [D2](settings-tests/d2-browser.txt), [D3](settings-tests/d3-browser.txt). |
| Local readiness / preservation | API5264 and current Angular4200 return successfully; live OpenAPI exposes permanent exclusions; anonymous context/PDF reads return **401**. Existing **68** migrations, Drive credential/root/enablement values, teacher grants, config and key hashes preserved. [Sanitized read-only probe](settings-tests/readiness.json). No local user/student/guardian/test plan was seeded. |

The settings browser now waits for PrimeNG's opening transform to settle before measuring geometry; the exact 44px acceptance assertions remain unchanged. A watch-server reload during an earlier run was followed by a settled successful run.

The first backend run exposed historical fixtures lacking an explicit timetable calendar after removal of the guessed default. Synthetic D1/D2 execution/renewal fixtures now create a scoped calendar; a new source-priority fixture was corrected to respect the existing timetable uniqueness constraint. The final full affected run above passed. A parallel D2 screenshot run hit a detached autosave DOM element; the unchanged journey passed on isolated rerun without relaxing assertions/timeouts. Rendered inspection also found and fixed a teacher-row flex shrink overlap and a template label binding before the final captures.

## Principal visual evidence

| State | 390px | 768px | 1440px |
|---|---|---|---|
| All teachers / linked study days | [Phone](settings-screens/default-390.png) | [Tablet](settings-screens/default-768.png) | [Desktop](settings-screens/default-1440.png) |
| Dated exclusion / aligned dates | [Phone](settings-screens/dated-390.png) | [Tablet](settings-screens/dated-768.png) | [Desktop](settings-screens/dated-1440.png) |
| Permanent exclusion | [Phone](settings-screens/permanent-390.png) | [Tablet](settings-screens/permanent-768.png) | [Desktop](settings-screens/permanent-1440.png) |
| English / retained error | [Phone](settings-screens/english-390.png) | [Tablet](settings-screens/english-768.png) | [Desktop](settings-screens/english-1440.png) |

[Missing-calendar state](settings-screens/missing-calendar-390.png). Actual page images were inspected at all widths, including long/duplicate names, date fields, mobile actions, RTL/LTR and exclusion states. Viewports and Playwright keyboard input simulate devices; physical devices/keyboards and final owner visual acceptance remain pending. These current UI fixtures do not demonstrate authenticated operational mutations or live Drive delivery. Existing actual source/PDF/native local authenticated byte-hash evidence remains in [D3 verification](d3.md); renderers and frozen historical artifacts were not changed.

## Supported archive stop during manual testing

Startup review found that legacy `SchoolFileStorage:ArchiveWorkerEnabled=false` / `ArchiveExternalWritesEnabled=false` are compatibility fields; the existing visit worker/processor did not honor them. An intermediate startup attempted old visit reconciliation; observed attempts stopped at Google OAuth `invalid_grant` (expired/revoked token). This was not a designated D3 provider verification, and no successful external delivery is claimed. The API was stopped and the narrow process switch below implemented. Credentials/root/grants/settings were not modified, renewed or replaced.

`SchoolFileStorage:ArchiveProcessingEnabled` now gates the visit worker **and processor**, reports truthful paused processing with school activation retained, and disables retry while stopped. It defaults true to preserve existing visit behavior. The current manual-test API overrides it false and retains library administration/read access and existing per-school activation. Dedicated checks prove the stopped processor makes no repository/provider calls. The current startup was observed for multiple worker cycles before interactive library reads with no visit OAuth/processing. Later authenticated library requests attempted OAuth and received the same invalid_grant; those reads remain enabled and are distinct from worker processing. The current log contains no visit-worker reconciliation/operation errors; the read-only preservation probe passed. Duty external processing also remains false.

From the workspace root, with existing Development settings:

```powershell
dotnet --roll-forward Major backend/AlFalah.Api/bin/Release/net8.0/AlFalah.Api.dll --contentRoot backend/AlFalah.Api --urls http://localhost:5264 --environment Development --Database:InitializeOnStartup=false --SchoolDutyArchive:ProcessingEnabled=false --SchoolFileStorage:ArchiveProcessingEnabled=false
```

Safe rollback stops new processing/reverts the frontend/service change while retaining schema, original school configuration, publication/execution/signature/fact/PDF/ledger history. Do not run destructive Down or reset data. General provider connection is not a synthetic destination; external D3 acceptance still needs the previously requested designated test boundary. Owner review remains pending.

## Reproducible independent checks

Use a **fresh** isolated database name. The fixture intentionally rejects any existing database before migration and retains its synthetic evidence afterwards.

```powershell
$env:ALFALAH_DUTY_TEST_CONNECTION = 'Server=(localdb)\MSSQLLocalDB;Database=AlFalahDutyTests_Settings_' + [guid]::NewGuid().ToString('N') + ';Integrated Security=true;TrustServerCertificate=true'
dotnet test backend/AlFalah.Tests/AlFalah.Tests.csproj --filter 'FullyQualifiedName~AlFalah.Tests.Duty|FullyQualifiedName~VisitArchiveOperationsTests' --logger 'console;verbosity=normal'
```

From `frontend`:

```powershell
npm.cmd test -- --watch=false --browsers=ChromeHeadless --ts-config=tsconfig.d2-spec.json --include='src/app/features/timetables/**/*.spec.ts' --include='src/app/features/intelligent-timetable/**/*.spec.ts' --include='src/app/shared/components/clearable-select/**/*.spec.ts' --include='src/app/features/student-affairs/messaging-chat/**/*.spec.ts' --include='src/app/features/student-affairs/guardian-dashboard/**/*.spec.ts'
npx.cmd ng build --configuration development
node scripts/check-duty-settings.mjs
node scripts/check-school-duty.mjs
node scripts/check-duty-execution.mjs
node scripts/check-duty-documents.mjs
```

The settings check uses the running localhost4200 server; other browser scripts serve the actual development build. All intercept every API response. Optional `ALFALAH_DUTY_BROWSER_EVIDENCE` chooses an isolated output folder for D1/D2/D3 captures. No original database, real parent traffic or provider destination is used. Existing SQLite fixture, broad Angular storage-spec and production CSS budget limitations remain as documented in D1/D2/D3; they were not changed or claimed fixed by this amendment.
