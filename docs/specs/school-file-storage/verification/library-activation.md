# Owner-authorized LOCAL library activation — 2026-10-06

The owner explicitly instructed applying migrations, adding an activation button and restarting the application. This later instruction authorizes local library operation on the original configured Development database and supersedes earlier OFF/no-original-migration restrictions for that scope. No production deployment, archive-worker activation, historical original-byte upload or legacy retirement was requested/executed.

## Executed operation

| Event | Actual UTC / result |
|---|---|
| Preflight | Original configured LocalDB resolved privately; COPY_ONLY + checksum backup/VERIFYONLY; original counts, encrypted credential hashes and config/key hashes retained only in ignored scratch |
| Migration window | 2026-10-06 15:23:35.6036885 → 15:23:58.7375381 UTC; Codex executing owner's explicit request; original local API stopped first |
| Schema | 10 storage migrations through `20261005162718_SchoolFileStorageHistoricalImport`; existing45 →55. Existing migrations seed8 storage permissions/role mappings; no general seeder or broad permission reset |
| Real old-data backfill | Source1 Approved submission; first apply1 file/version/link/decision; repeat0 new, existing1, issues0. Exact configured Development requires the explicit authorized CLI switch |
| API restart | 15:28:58.6852095 UTC, Release API5264, startup initialization OFF, persisted local administration/read ON, archive worker/external writes OFF |
| Frontend restart | 15:30:34.7688984 UTC, Angular4200, hidden process; current code compiled and serving |
| School18 | Actual manager sees Connected, exactly1 school-library root, upload controls visible desktop/mobile. Existing active year1/template1 initialized from S4:36 requirements, repeat retains same IDs, no inferred responsible people/review decisions |
| Recovery check | 15:34:20.3305109 UTC; post-write COPY_ONLY/checksum backup restored to a **fresh** isolated DB, CHECKDB passed; counts and version identities/hashes match |

Sanitized [machine checks](library-activation-checks.json) and [original backfill comparison](library-activation-backfill.json) record actual outcomes. Raw encrypted DB backups, local SQL identifiers, tokens and screenshots remain in ignored `.audit/storage-activation-20261006`. Original Development configuration/Google stored credentials/existing keys have zero hash differences. No Down or Drive deletion occurred.

Post-write restore preserved1 legacy submission,1 stored file/version/link/review decision,2 folders,37 requirements (old1 + school18's36),1 evaluation scope and256 audit rows. This local clone had no archived artifacts; archive readiness is not inferred from it. School1's pre-existing undecryptable Google credential was not repaired. Earlier synthetic Google artifacts from isolated S6 tests remain retained and are not auto-adopted into the original ledger.

## Activation button and persistence

Settings and library show **تفعيل المكتبة** when the connected school's library has not been initialized and live context allows management. It uses existing `POST /api/v1/storage/folders` root provisioning; backend checks current SchoolScope/DB permission/manager or current direct delegation, root boundary and teacher overlap, reserves a stable school-wide provider identity, reconciles uncertain creation and reuses existing root. The UI prevents repeated pending clicks and clears state on authorization failure. Ready schools show ready status/open-library controls. There is no browser endpoint that changes global flags or executes migrations.

The button was covered by enabled desktop/mobile browser contracts. In the real session, the school root had already been activated when the live checks ran; those checks verified Connected, authorized upload controls and repeated POST returning the **same** root on both viewports. They do not claim a second real button activation or a real uploaded evidence file. Historical source metadata remains separate from originals/approval.

Local `backend/AlFalah.Api/appsettings.StorageActivation.Development.json` is ignored and contains:

```json
{"SchoolFileStorage":{"AdministrationEnabled":true,"ReadModelEnabled":true,"ArchiveWorkerEnabled":false,"ArchiveExternalWritesEnabled":false},"Database":{"InitializeOnStartup":false}}
```

Only Development loads this explicit local opt-in file. Environment/command-line overrides still win; restart is required after changes. The original Development file/Google credentials/keys remain unchanged, and production never loads this local file. These hosting gates are global for the local application; each school still has live scope/DB permission/root checks. School18 activation does not grant access to another school's data or resolve another school's credential problem.

Rollback changes local administration/read to false, leaves archive flags false and restarts API. Preserve all new SQL records, decisions, snapshots, files and provider identities; no Down, delete or overwrite of the original DB. Reconcile compatibility writes through the shared evidence review service before reopening legacy writers, as described in the [runbook](../s6-rollout-runbook.md).

## Validation and commands

Backend build0 errors/0 warnings; library backend suite **30/30** on fresh explicitly isolated LocalDB. Angular transport **4/4**. Drive-settings + library browser suite **30/30** desktop/mobile, including activation/read-only/disabled/revoked/error cases. Frontend production build passed with existing CSS budget/PrimeNG selector warnings. Actual manager checks against4200/5264 verify Connected, upload UI and same-root replay. Source backfill repeat and restored counts/identity hashes are independent real SQL evidence.

Executed commands (repository root unless noted):

```powershell
dotnet build backend/AlFalah.Tests --configuration Release --no-restore --verbosity quiet
# ALFALAH_MIGRATIONS_CONNECTION resolved privately from configured local settings, after verified backup:
dotnet ef database update 20261005162718_SchoolFileStorageHistoricalImport --configuration Release --no-build --project backend/AlFalah.Infrastructure --startup-project backend/AlFalah.Api
dotnet build docs/specs/school-file-storage/scripts/StorageBackfill --configuration Release --verbosity quiet
# Exact configured local DB, explicit switch, dry-run first, apply then repeat; stop on issues:
dotnet --roll-forward Major docs/specs/school-file-storage/scripts/StorageBackfill/bin/Release/net8.0/StorageBackfill.dll --repository 'D:\AlFalah-Manage-System' --database '<configured-local-db>' --authorized-configured-development --dry-run --report '.audit/activation/backfill-dry.json'
# Separate fresh ALFALAH_STORAGE_TEST_CONNECTION for tests, never original:
dotnet test backend/AlFalah.Tests --configuration Release --no-build --filter 'FullyQualifiedName~StorageLibrary'
```

From frontend:

```powershell
npm test -- --watch=false --browsers=ChromeHeadless --include='src/app/features/storage/storage-api.service.spec.ts'
npx playwright test --config playwright.storage.config.ts drive-settings.spec.ts library.spec.ts
npm run build
```

Restart with the persisted local file and initialization disabled:

```powershell
dotnet run --project backend/AlFalah.Api --configuration Release --no-build --no-launch-profile --urls http://localhost:5264 --environment Development --Database:InitializeOnStartup=false
# frontend working directory:
npm start -- --host localhost --port 4200
```

Both actual background restarts used hidden processes and redirected local logs. PDF Arabic search/copy, school1 decryption, historical originals and live archive/production rollout gates remain recorded limitations; this instruction does not claim they were resolved. D-102, stop at S6.
