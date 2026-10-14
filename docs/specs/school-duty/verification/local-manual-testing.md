# Owner-authorized local migration and manual-test startup — 2026-10-14

The owner explicitly requested applying necessary migrations and running the project for manual testing. This later request authorizes migration of the configured **local Development database** and local startup, superseding the earlier no-original-database-migration restriction for this operation. It does not authorize production deployment or external duty/visit archive delivery. D3 implementation evidence remains in [d3.md](d3.md).

## Executed operation

- Built the current Release API: zero warnings and errors.
- Resolved the configured Development connection privately; required integrated-security LocalDB and the exact existing migration history. No new database, reset, credential or connection setting was substituted.
- Created a new COPY_ONLY database backup with CHECKSUM and successfully executed RESTORE VERIFYONLY before migration. Backup, raw local database identifiers, connection/root/grant comparisons and key/configuration hashes remain in ignored `.audit/duty-manual-20261014/`.
- Applied nine reviewed pending additive migrations, from 59 existing migrations to **68**, through `20261013165202_AddSchoolDutyD3`. The chain includes three existing timetable checkpoint/clone/audit migrations and the six D1/D2/D3 migrations below.
- Postflight verified unchanged existing core row counts, encrypted Drive credential hashes, connection/root/enablement values, teacher grants and original configuration/key hashes. Required duty tables exist, all school duty archive enablement values remain false, and **DBCC CHECKDB passed**. No Down or general seeder ran. A final EF check reports no pending model changes; the existing key-file set also remains unchanged.
- Started the current Release API at **http://localhost:5264**, with startup initialization explicitly disabled. Existing local library administration/read settings are retained; duty external processing and visit archive worker/external writes remain false.
- Started the actual Angular development server at **http://localhost:4200** in watch mode. Compilation passed. A real browser opened the protected duty route, reached the school-login page with no page errors, and made no authenticated operational writes.
- HTTP readiness verified frontend 200, current document/roster routes in Swagger, anonymous PDF 401 and CORS for the real frontend origin. Sanitized evidence: [local-manual-testing-checks.json](local-manual-testing-checks.json).

## Applied migration chain

```text
20261012144441_AddTimetableCheckpointsAndClones
20261012181800_AddTimetableCloneIdentity
20261013071807_AddTimetableChangeAuditDetails
20261013102255_AddSchoolDutyD1
20261013102540_AddDutySettingsHistory
20261013104512_AddDutyTeacherSchoolIntegrity
20261013113721_PreserveDutyHistoryAcrossTeacherTransfers
20261013135411_AddSchoolDutyD2
20261013165202_AddSchoolDutyD3
```

The duty migration adds only its scoped permission mappings; startup does not invoke broad database/user/rubric/student/timetable seeding. Existing role/permission isolation remains enforced.

## Manual-test entry and restart

Open **http://localhost:4200/school-duty** (C18; old duty URLs remain compatible) and sign in as the actual school manager or duty teacher. A stale session may need sign-out/sign-in to refresh the newly added duty permissions. Existing accounts and saved signatures are retained; no teacher signature or sample duty plan was manufactured for this operation.

Restart the API from the workspace root with the existing Development settings:

```powershell
dotnet --roll-forward Major backend/AlFalah.Api/bin/Release/net8.0/AlFalah.Api.dll --contentRoot backend/AlFalah.Api --urls http://localhost:5264 --environment Development --Database:InitializeOnStartup=false --SchoolDutyArchive:ProcessingEnabled=false --SchoolFileStorage:ArchiveProcessingEnabled=false
```

From `frontend`:

```powershell
npm.cmd start -- --host localhost --port 4200
```

The actual starts used hidden persistent background processes with logs redirected into the ignored audit directory. Existing configuration files, credentials, Drive roots and grants were not edited. These are local development servers, not a production deployment.

Local acknowledgement, drafts, submitted PDFs, preview/download and retained history are available for manual testing. User-triggered explicit dismissal-lateness confirmation uses the existing actual internal guardian delivery workflow; startup/readiness verification did not create facts or parent notices. External duty delivery stays disabled. The C11 hierarchy is implemented but no operational Drive folder/file was created by this operation. A designated isolated synthetic external destination remains required for the separate live-provider gate. Owner visual and physical-device acceptance remain pending; starting manual review does not mark acceptance complete.

Rollback disables new processing and retains the applied schema, snapshots, facts, artifacts and ledger history. Do not execute destructive Down, reset data or overwrite the original database to stop testing.

Later C18 navigation refinement is independently verified in [navigation.md](navigation.md). The running development server watches the new independent entry; no additional database migration or API restart is required.

Subsequent C19 verification found the legacy visit archive flags were compatibility fields and did not stop the worker/processor. Earlier statements of visit worker disablement describe requested flags, not an effective process boundary. Current startup uses the supported `SchoolFileStorage:ArchiveProcessingEnabled=false` override while retaining library access and school configuration. Observed intermediate OAuth attempts failed with `invalid_grant`; no external delivery acceptance is claimed. The corrected current log/probe is in [settings verification](settings-amendment.md).

## Current manual operation after C20–C23 (2026-10-14)

[Current workspace and live verification](workspace-amendment.md) supersedes earlier process flags for today's manual test. API5264/Angular4200 run the latest implementation; initialization stays off and original SQL remains at68 migrations. Global duty processing is ready, every original school's duty enablement remains false, and supported Visit processing is false. Owner manually reconnected OAuth after the requested local public callback fix. The specifically authorized D3_Test provider verification used fresh isolated SQL and no original-school activation. [Read-only preservation/readiness](workspace-readiness.json); owner/physical appearance approval remains pending.
