# C26 / D-118 — Archive controls, school root and library presentation

Owner-authorized follow-up, 2026-10-14. This extends the implemented D3 workflow and the existing storage experience. It does not deploy production, migrate the operational database, change credentials/keys/grants/roots, remove history or start general feature delegation/waiting-register/external-messaging work.

## Diagnosis and delivered behavior

The screenshot showed two **letters**, with reports still awaiting submission. Read-only SQL found both letters already `Archived`, one upload attempt each, no delivery error. Their frozen bytes matched protected archived reads. The manager's paginated archive had no timer; it now refreshes every five seconds while visible, with preview/history/busy/denied guards. Acknowledging the letter archives it independently; a report is generated and queued after actual signed submission.

The running API had an explicit `SchoolFileStorage:ArchiveProcessingEnabled=false` left from bounded D3 testing. The school visit flag was ON, so the school setting and process status disagreed. OAuth was valid; another Google login was not required. `VisitArchiveService` previously returned `workerEnabled=true` even when the process was stopped. It now reports the actual processing gate. The UI no longer invents `ready`/external-write success immediately after a school toggle; it reloads authorized server status.

At the owner's request the local API was restarted with both processing gates true and startup initialization false. School18 duty and visit flags, selected by the owner, remain ON; school1 stays OFF. Existing visits were already completed. No per-school flag, provider root, credential, key or teacher grant was changed by the agent. The operational database remains at68 migrations; no new schema migration.

Only Storage administration presents activation controls: independent duty/visit on/off switches, current readiness and stopping without deleting signatures or artifacts. Duplicate activation buttons in the duty table/document panel were removed. Duty-only administrators retain their current document/retry scope and do not gain general storage administration.

Initial library setup includes `المناوبات`, using the same protected duty identity, SQL session lock and persisted provider ID as the worker. Repeat setup/delivery reuses it. Missing/renamed/moved protected folders require review; general recovery consent cannot detach/rebind signed history. Untracked same-named native folders remain conflicts. Year/term/actual-author/date descendants are created from signed document scope, never guessed during initial setup.

The manager Files entry exposes Library, Teacher Files, Duty and Visit Archive under the verified configured school root, opening each existing authorized workflow. Deep folder/review/file/year links are retained. Library shortcuts on the storage overview open the actual library folder. `D3_Test` and arbitrary Drive-only folders are not adopted into operational storage or evidence.

The switchable presentation uses actual scoped folder identities, colored connector rails/section cards, search, collapse, folder navigation, registered file counts including deeper descendants, keyboard focus, English labels, reduced motion and native fullscreen. Canonical template structures use domain/standard labels; arbitrary existing structures use library section/subfolder labels. No fake readiness percentages or sample historical identities. Counts are retained-ledger counts, not approval or an assertion of every descendant's current provider availability. Missing/moved/ambiguous roots cannot be opened from the map. Large trees are bounded and directed to paginated browsing.

## Performance evidence

Measured authenticated local HTTP reads with complete response/body timing against the same configured synthetic school and unchanged real Google provider:

| Read | Before | After |
|---|---:|---:|
| Full library setup inspection | 10,451 ms | 4,053 ms |
| Protected archived letter read, document1 | 7,592 ms | 4,508 ms |
| Protected archived letter read, document2 | 10,948 ms | 4,739 ms |
| School-root/map, three samples | New endpoint | 1,676 / 1,510 / 1,508 ms |
| SQL archive index | 207 ms | 234 ms |
| Local frozen PDF | 13 /24 ms | 12 /167 ms |

The existing detailed context still takes about1.9–2.3 seconds. Ordinary navigation uses its existing SQL-only summary/cache, and administration no longer eagerly scans the whole setup tree while that tab is hidden. Individual actual Drive requests observed in the preceding runtime log were approximately300–650 ms. This is bounded local evidence, not a promised network SLA or production load test.

Complete metadata from a list response is reused within the scoped request. Mutations invalidate the changed ID even on uncertain/lost responses. Connectivity and new verification/request boundaries remain fresh; the SQL regression caught and corrected a stale connectivity-cache attempt. A narrow internal verified-metadata download seam avoids a second metadata request before streaming. Legacy adapters retain their safe default. There is no cross-request permission/availability cache and no relaxed root containment.

[Sanitized readiness, exact timings and existing artifact hashes](storage-duty-readiness-performance.json).

## Verification and evidence

- **100/100 backend checks**, including **78 actual isolated SQL checks**, on fresh guarded LocalDB with the migration sentinel preserved. Covers D1 rotation/execution locks, D2 signature/fact/correction ownership, D3 frozen artifacts/protection/recovery, narrow delegation, new root setup and scoped map/counts. Additional focused5/5 and final2/2 checks confirmed deeper-file counts and fresh disconnection detection; these overlap the main suite and are not added to its total.
- **146/146 Angular checks**, duty/timetable/standby/shared picker/internal messaging/guardian regressions. Development frontend and final API builds pass. New API controllers are thin `ApiResponse<T>` wrappers over Application scope/decisions and Infrastructure queries.
- **28/29 existing storage/Visit SQL checks** pass. The failing legacy S5 script test hard-codes the entire future pending-migration list to one S6 migration;13 later existing migrations make that assertion stale. Its fresh-database guard was added and sentinel equality moved before that assertion. Guarded repeat independently preserves its sentinel and still fails the unchanged historical assertion. No product constraints or expected list was relaxed.
- Existing library browser contracts were adapted to the requested root entry/deep library shortcut, actual mobile navigation, scroll-normalized geometry and current array-shaped candidate/setup API fixtures. The final combined run/recheck covers86 cases:85 passed in the full run, with the remaining mobile navigation assumption corrected and its desktop/mobile case rerun. See retained command output. No forced hidden-element clicks, increased timeouts or relaxed CSS budgets.
- Actual current Angular presentation at **390/768/1440/1920**, with explicitly intercepted synthetic APIs: four root branches,4 template domains/11 standards, search, collapse, keyboard events, denied-data clearing, independent switch on/off, process-stop truthfulness, lazy setup, native fullscreen and automatic `Delivering→Archived` table update. The final current-code browser recheck also covers HTTP503 and unsuccessful response envelopes during background refresh: last saved rows remain visible, no repeated error modal, and recovery clears the notice. [Principal captures and result](storage-map-screens/browser-results.json), [desktop tree](storage-map-screens/map-domains-1440.png), [phone presentation](storage-map-screens/map-390.png), [English presentation](storage-map-screens/map-en-1440.png).
- **Real current local authenticated integration**, no API interception, at390/768/1440: actual configured provider-root branch reads, restored Visit readiness, paired archived duty rows, PDF preview and native browser downloads. All downloaded bytes match the frozen/verified archived hashes; zero browser mutations/provider writes. [Live evidence](storage-live-screens/browser-results.json), [actual root](storage-live-screens/actual-root-1440.png), [actual archive](storage-live-screens/actual-archived-1440.png).
- **Real Google Drive delivery/recovery**, fresh isolated SQL, only the previously designated `D3_Test` within the verified current synthetic-school root: **16 passed checks, exactly2 actual uploads**, a letter and report. Root provision/replay, deliberately lost response after a successful actual upload, real SQL rollback after a successful report upload, retry/reconciliation without another write, exactly2 ledger versions, local/archived/download SHA-256 equality, and no guardian/System/human messages. [Result](storage-duty-drive-results.json). Run `Run-20261014-142021-5b372bbe` is retained; no existing provider data was deleted. Existing school source connection/root/flags/encrypted credentials compare equal before and after. A legacy hard-coded disabled-source metadata field was corrected from captured read-only flags; the reproducible harness now emits the actual source flag.

The renderer was unchanged. Actual new synthetic uploaded/downloaded letter and report pages were rendered and visually inspected: Arabic shaping, report source order/RTL columns, original signature ownership/empty substitute position, both explicit empty tables, observations/00:02 final departure, next-day handover, separate unsigned manager letter position and embedded synthetic teacher signature are intact. [Letter page](storage-drive-pdf/letter-1.png), [report page](storage-drive-pdf/report-1.png). Original source fingerprints, source-to-output comparison and representative multipage/correction/missing-branding inspections remain in [D3 PDF verification](../../school-duty/verification/d3.md); no original/private source sample was added.

[Retained build/test outputs and SQL result files](storage-duty-checks/) distinguish the initial full library run and its targeted correction/recheck. Temporary diagnostic console hooks were removed.

## Reproduce

From repository root; every database name must be newly generated. Existing catalogs are rejected before migration, never reset or dropped:

```powershell
$env:ALFALAH_DUTY_TEST_CONNECTION='Server=(localdb)\MSSQLLocalDB;Database=AlFalahDutyTests_C26_'+[guid]::NewGuid().ToString('N')+';Integrated Security=true;TrustServerCertificate=true'
dotnet test backend/AlFalah.Tests --configuration Debug --filter 'FullyQualifiedName~DutyDocumentSqlTests|FullyQualifiedName~DutyExecutionSqlTests|FullyQualifiedName~DutySqlTests|FullyQualifiedName~DutyAdministrationSqlTests|FullyQualifiedName~DutyTests|FullyQualifiedName~GoogleStorageProviderCacheTests|FullyQualifiedName~GoogleDriveClientTests|FullyQualifiedName~StorageMapSqlTests|FullyQualifiedName~VisitArchiveOperationsTests'
$env:ALFALAH_STORAGE_TEST_CONNECTION='Server=(localdb)\MSSQLLocalDB;Database=AlFalahS1Tests_C26_'+[guid]::NewGuid().ToString('N')+';Integrated Security=true;TrustServerCertificate=true'
dotnet test backend/AlFalah.Tests --configuration Debug --filter 'FullyQualifiedName~StorageLibrarySqlServerTests|FullyQualifiedName~VisitArchiveSqlTests'
```

The second command retains the documented legacy pending-list failure. Bounded external verification requires the **already explicitly designated** retained D3_Test reservation and unchanged configured synthetic-school connection/keys; it must never choose an operational archive destination:

```powershell
$env:ALFALAH_DUTY_TEST_CONNECTION='Server=(localdb)\MSSQLLocalDB;Database=AlFalahDutyTests_C26Drive_'+[guid]::NewGuid().ToString('N')+';Integrated Security=true;TrustServerCertificate=true'
$env:ALFALAH_D3_ARCHIVE_AMENDMENT='1'
dotnet run --project docs/specs/school-duty/verification/d3-live-drive -- D:/AlFalah-Manage-System
```

Frontend directory:

```powershell
npm.cmd test -- --watch=false --browsers=ChromeHeadless --ts-config=tsconfig.d2-spec.json --include='src/app/features/timetables/**/*.spec.ts' --include='src/app/features/intelligent-timetable/**/*.spec.ts' --include='src/app/shared/components/clearable-select/**/*.spec.ts' --include='src/app/features/student-affairs/messaging-chat/**/*.spec.ts' --include='src/app/features/student-affairs/guardian-dashboard/**/*.spec.ts'
npx.cmd playwright test --config playwright.storage.config.ts library.spec.ts
node scripts/check-storage-map.mjs
npx.cmd ng build --configuration development
npx.cmd ng build --configuration production
```

The map script requires the current Angular4200 server and uses synthetic intercepted API fixtures. The read-only live-browser script uses a privately issued current manager session and captured real hashes in ignored `.audit`; it fails without that prepared boundary rather than fabricating access. Raw sessions/configuration/credentials remain ignored.

## Safe activation, rollback and remaining gates

Current local launch, after verifying the exact existing API process before replacement:

```powershell
# API working directory, existing local library configuration retained:
dotnet --roll-forward Major bin/Release/net8.0/AlFalah.Api.dll --urls http://localhost:5264 --environment Development --Database:InitializeOnStartup=false --SchoolDutyArchive:ProcessingEnabled=true --SchoolFileStorage:ArchiveProcessingEnabled=true
# frontend:
npm.cmd start -- --host localhost --port 4200
```

Connect/select the authorized school root normally; use Storage administration's independent switches. Initial setup does not activate archiving. Automatic archive refresh retains the last saved status and an inline retry notice on transient failures; explicit action failures and denied reads retain the duty feedback flow. Pausing a school switch or its process gate stops new deliveries while retaining all signed snapshots, facts, frozen artifacts, reservations, versions and audit. Re-enable/retry missing results through the existing workflow; never re-submit/sign to retry. No destructive Down migration or history deletion.

Owner final appearance and physical-phone/OS-keyboard checks remain pending. Playwright key events/fullscreen and viewport simulation are explicitly distinguished from physical checks. Production build still fails at the two existing32kB stylesheet budgets: storage-page39.34kB and visit-workspace34.50kB. Budgets were not changed. Known broad Angular storage-spec typing, SQLite ProtectedPassword fixture and historical model assertions were not repaired or claimed resolved. No production deployment/load assurance or real-parent test traffic. Final local restart/read-only smoke reconfirmed both processors ready, school18 ON/school1 OFF and unchanged frozen/archived byte equality; see [current local smoke](storage-duty-checks/current-local-smoke.json). Startup initialization remains disabled.
