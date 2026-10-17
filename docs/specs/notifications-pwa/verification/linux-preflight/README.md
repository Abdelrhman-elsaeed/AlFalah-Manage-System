# Linux VPS Preflight

**Date:** 2026-10-17  
**Scope:** Inspect Hostinger Linux VPS suitability before purchase; no feature implementation or deployment.  
**Decision:** Linux VPS remains a viable direction, but the current repository is not ready for an unattended production move. Resolve the release blockers below before publishing school data.

## Isolation and evidence

The audit copies source and non-secret test assets to a temporary directory outside the repository. Original `appsettings*`, `App_Data`, deployment credentials, generated `wwwroot`, build output and production data are excluded. The copy uses synthetic connection/JWT settings. Linux executes under Docker in the existing local WSL installation; the SQL container is on an internal audit-only network with no published database port. This is a Linux compatibility check, not a Hostinger benchmark or a production deployment.

The existing local development database was queried read-only for edition, version, migration count and aggregate file sizes. No school records, secrets, connection strings or key contents are included in retained evidence. Existing source files and configuration are unchanged.

## Findings and release blockers

| ID | Finding | Evidence / impact | Required disposition |
|---|---|---|---|
| LP-01 | Production frontend build fails | Current `npm run build` exceeds the 32 kB per-component CSS error budget: `visit-workspace.component.css` about 34.50 kB and `storage-page.component.css` about 39.34 kB. This was reproduced on Windows; it is not a Linux-specific compiler failure. | Reduce the oversized styles or make an explicit reviewed budget decision; do not silently relax limits during deployment. |
| LP-02 | Existing local key ring contains Windows DPAPI encryption | One key XML file was inspected for encryption metadata only and contains `DpapiXmlDecryptor`. Setting `KeyEncryption=None` for newly generated keys does not decrypt historical DPAPI-wrapped keys. Linux cannot use Windows DPAPI. Stored Drive/API credentials can become unreadable after a naive database/key-file copy. | Decide whether existing encrypted integrations must migrate. Re-enter/re-authorize them on the target with a new protected key ring, or perform an explicit secure key/data migration on the original Windows host. Preserve originals until verification; never publish decrypted keys/secrets in reports. |
| LP-03 | Production startup always initializes the database | `Program.cs` uses `!IsDevelopment() || ...` before `MigrateAsync`, permission/guardian/rubric seeding, and catches initialization errors without failing outside E2E. `Database:InitializeOnStartup=false` cannot suppress this in Production. | Establish controlled migration/seeding and fail-fast/readiness behavior before connecting to the customer's database. Do not assume the current flag disables production writes. |
| LP-04 | Reverse proxy scheme/client-IP handling is not configured in source | No `UseForwardedHeaders` registration is present, while production uses HTTPS redirection and IP-based rate limits. A Linux TLS-terminating proxy needs explicit trusted forwarding configuration. | Configure the actual trusted proxy/header path before HTTPS redirection and validate redirects, OAuth URLs and client-IP limits. Avoid trusting arbitrary forwarded headers from public clients. |
| LP-05 | Persistent private directories need deployment treatment | Excuse files and Data Protection keys default under the app content root. Replacing a release directory can lose them; temporary upload files also require a writable, bounded directory. | Use stable private paths/volumes outside release replacements, owned by the service account; include files and keys in encrypted backups and restore tests. |
| LP-06 | Production configuration must be supplied explicitly | No `appsettings.Production.json` is present. Base settings contain a plaintext database password, a placeholder-like JWT secret, existing-origin settings and OAuth configuration. An incorrectly quoted search accidentally exposed the existing connection value in tool output; values are excluded from this report, retained artifacts and audit containers. | Rotate the exposed database credential through the authorized account operator and inject target-specific connection/JWT/CORS/OAuth/domain settings securely. Do not publish the base file as production truth or reuse audit credentials. |
| LP-07 | Runtime support window is short | Target framework is .NET 8; official support ends 2026-11-10. Angular 17 is outside current support. `RollForward=Major` is not a tested framework migration. | Resolve the documented supported-stack upgrade/exception gate before production rollout. |
| LP-08 | Database edition and OS versions must be explicit | Local development uses SQL Server 2019 Express (15.0.4382.1), 76 migrations and 153 application tables; allocated data 136 MB, used data about 72.88 MB, allocated log 8 MB. These are development figures, not the customer's capacity profile. | SQL Server 2022 Express is a concrete candidate: 10 GB relational DB limit, buffer-pool limit 1,410 MB, at most lesser of one socket/four cores. Validate actual load/growth. Use the tested edition in production; Developer is not a production license. |
| LP-09 | File containment uses a Windows-style case-insensitive comparison on Linux | A synthetic Linux call to `LocalFileStorageService.ReadBytesAsync` with root `private` and key `../PRIVATE/outside.txt` read a file in the distinct sibling directory `PRIVATE`. `StartsWith(...OrdinalIgnoreCase)` accepts the normalized path, while Linux treats the two directories differently. `DutyDocumentAssets` has a similar case-insensitive local-path check to review. | Use platform-correct, separator-aware containment and test traversal/case/symlink behavior before release. The demonstrated issue is at the storage-helper boundary; this audit does not claim an unauthenticated HTTP exploit. The current attachment handler obtains storage keys from authorized database records. |
| LP-10 | API publication alone does not build/package the Angular site | API project has no frontend build target. The isolated API publish has no `wwwroot/index.html`, and its root HTTP request returns 404 while API endpoints work. The current frontend release build also fails LP-01. | Build the Angular production output explicitly and package its actual browser output into the chosen static-serving path; validate `/`, SPA deep links and assets after deployment. |

## Positive source findings

- API/Application/Infrastructure target ordinary `net8.0`; no Windows-only target framework is required.
- Timetable generation is managed C#; no Python executable, OR-Tools binary or external solver process was found in the active engine.
- QuestPDF 2024.3.4 supplies a Linux native renderer; fonts are embedded and copied for publication, with a platform logo asset copied as well. Actual rendering results are recorded separately below.
- ClosedXML/QRCoder usage does not require Excel/Office to be installed on the server.
- Local file storage uses `Path.Combine` and invariant Gregorian upload folders. The normal synthetic Linux save/read succeeds, but the containment failure in LP-09 must be corrected.
- Frontend production API origin is relative (`apiUrl: ''`), supporting a same-origin reverse-proxy deployment.
- Seven hosted services are registered: visit archive, duty archive, duty notice, duty reconciliation, coverage reconciliation, evidence reconciliation and Student Affairs outbox. Supervise the application process and test restart behavior; no Windows Service implementation is required for these `BackgroundService` instances.
- A scan of 1,204 resolvable relative TypeScript imports found no filename-case mismatches. This is a static import scan, not a Linux frontend production build.

## Runtime verification - completed

| Check | Actual result | Evidence |
|---|---|---|
| Source fidelity | 1,090 copied source/assets match original SHA-256 hashes; configuration is deliberately replaced with synthetic settings. | [Summary](evidence/summary.json) |
| Linux Release publication | Passed for `linux-x64`, framework-dependent; existing compiler/analyzer warnings remain. SDK 8.0.426, runtime 8.0.32, Debian 12 userland on WSL/Docker x86-64. | [Runtime information](evidence/linux-dotnet-info.txt), [summary](evidence/summary.json) |
| Focused Linux tests | 52 passed, 0 failed, 0 skipped. Includes 18 timetable generation, 13 coverage-register, 5 Duty roster, 8 visit V2 document, 3 local-storage, 2 key-persistence, 1 dashboard PDF/Excel, 1 visit PDF and 1 visual-dump test. | [TRX results](evidence/linux-focused.trx) |
| SQL Server Linux | SQL Server 2022 Express 16.0.4305.5 applied all 76 migrations to an isolated synthetic DB; 153 application tables; zero pending; second migration run completed; Arabic parameter roundtrip passed. | [SQL probe](evidence/harness-sql.json) |
| Platform behavior | Normal file save/read and invariant Gregorian path pass; Cairo/Riyadh and a Windows-style zone ID resolve. Case-variant outside-root read reproduces LP-09. | [Platform probe](evidence/harness-platform.json) |
| Published API Production startup | Separate synthetic SQL DB; Swagger JSON returns 200 with 421 paths; unauthenticated conversation and notification-count requests return 401. Root returns 404 because Angular is not packaged. No public container ports were published. | [HTTP probe](evidence/api-probe.json) |
| Arabic rendering | Seven PDFs were generated by Linux tests and independently parsed; every inspected PDF includes Amiri Regular/Bold. First pages of a visit and Duty roster were rasterized and visually inspected for readable Arabic/RTL. This is sample review, not every report/form. | [PDF inspection and hashes](evidence/pdf-inspection.json), [visit sample](evidence/visit-report.pdf), [Duty sample](evidence/source-eighteen-weeks.pdf), [Duty preview](evidence/linux-pdf-preview.png) |
| Frontend production build | Failed on the two existing CSS budget violations in LP-01. No build budget was changed and no successful production frontend artifact is claimed. | [Build log](evidence/frontend-build.log) |
| Development DB metadata | Read-only local aggregate check; no database backup or operational record copied. | [Metadata](evidence/local-db-metadata.json) |

Existing LocalDB-only SQL test guards were retained; a separate narrowly guarded harness exercised only the named synthetic container database. It did not make the repository's full SQL suites portable. Most of the 52 tests are focused/unit/InMemory checks; the migration/Arabic SQL probe is separate real SQL evidence, not proof of every workflow, concurrency race or backup restore.

The application container's warnings confirmed missing `wwwroot`, no configured HTTPS redirect port in this internal HTTP-only setup, and the synthetic `None` key-encryption policy. These are expected audit-environment observations and do not establish a correct HTTPS/proxy/secret configuration for production. No real Google/push credentials were supplied and the internal network prevented external provider traffic.

### Commands and reproduction boundary

The commands below ran against the sanitized temporary copy, never against current operational configuration:

```text
dotnet publish backend/AlFalah.Api/AlFalah.Api.csproj -c Release -r linux-x64 --self-contained false -o /audit/publish
dotnet test backend/AlFalah.Tests/AlFalah.Tests.csproj -c Release --filter <the nine focused classes listed above> --logger trx
npm run build -- --output-path <temporary audit frontend output>
```

Temporary harnesses tested file containment/time zones and applied EF migrations only to the isolated SQL Express container. Do not blindly run the API with its original production/base settings: LP-03 explains its startup database writes. The original database received only the read-only metadata query.

The two running audit API/SQL containers were removed after retaining evidence; the internal audit network was removed as well. Existing Docker workloads were untouched. Temporary source/build caches and downloaded images can be reused locally; no server was purchased or provisioned.

### Not established by this audit

- Actual Hostinger/OVH resource performance, Gulf latency or capacity at the school's peak workload.
- A complete Linux browser end-to-end session, all authenticated business workflows, N1 realtime or N2 device push.
- Customer data migration/restore, continuity of existing encrypted credentials or production secrets/key-at-rest design.
- Deployment under the final non-root account, systemd/restart policy, reverse-proxy/TLS, external backups or real device notifications.
- A supported framework upgrade, runtime-only container certification or a full third-party vulnerability review.

## Recommended target and sequence

Hostinger KVM 4 remains the proposed all-in-one starting size; KVM 2 is a budget/light-load alternative. Neither has been load-tested by this audit. Choose an x86-64 supported Linux/runtime/SQL combination. For a direct SQL Server 2022 installation, Microsoft's documented Ubuntu choices include 20.04/22.04; do not select an arbitrary latest OS just because Hostinger offers it. SQL Server containers can use the vendor image's supported userland; the WSL host OS itself is not a production support certification.

Before release: fix the confirmed blockers, establish secure target configuration and persistent storage, rehearse synthetic database creation/restore and key/integration continuity, build the frontend production artifact, deploy a staging site with HTTPS/proxy/service supervision, then exercise real user flows and notification phase gates. This preflight does not start N1 or add a fourth notification delivery phase.

## Official references checked

- [Microsoft SQL Server Linux installation and supported platforms](https://learn.microsoft.com/en-us/sql/linux/install-upgrade/setup?view=sql-server-ver16).
- [SQL Server 2022 Linux editions and capacity limits](https://learn.microsoft.com/en-us/sql/linux/sql-server-linux-editions-and-components-2022?view=sql-server-ver16).
- [Microsoft Data Protection key encryption and Windows DPAPI](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-encryption-at-rest?view=aspnetcore-8.0).
- [ASP.NET Core trusted proxy/forwarded-header configuration](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-8.0).
- [.NET support dates](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) and [Angular supported versions](https://angular.dev/reference/releases).

SQL Server 2025 material returned inconsistent Express size limits between current official Linux and general edition pages during this audit. This report therefore uses the concrete SQL Server 2022 target/limits rather than choosing an untested new database version from a marketing claim.
