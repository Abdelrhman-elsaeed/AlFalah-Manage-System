# C27 / D-119 — Native Drive browsing, confirmed deletion and empty-root generation

Owner-authorized storage amendment, 2026-10-15. C27 replaces C26's feature shortcuts with actual folder navigation. The owner explicitly authorized clearing the existing **Al-Falah E2E Test School** root, including `D3_Test`, and testing initial generation and automatic deliveries. This exception is specific to that verified synthetic root; it is not permission to clear another school or deploy production.

## Delivered contracts

Files starts with one **School files / ملفات المدرسة** folder. Opening it lists the actual configured Drive root and its native folders/files, including unindexed items. Every folder remains inside the storage browser; Duty/Visit/Teacher cards no longer redirect to feature workspaces. Existing deep library/evidence links and the teacher's own scoped file workspace remain available. The committee map is retained and its root links open native folders.

The native browser has breadcrumbs, paged loading, a filter explicitly limited to displayed items, authenticated downloads, actual PDF canvas previews, and download of the already authorized displayed PDF. Rename, move and delete have accessible quick actions. Native dialogs are centered, keyboard operable and return focus. Protected archive identities/system roots do not offer rename/move. The configured school root itself cannot be deleted. Phone, tablet and desktop layouts retain 44px actions and reduced-motion support.

`/api/v1/storage/drive` is a thin `ApiResponse<T>` controller over an Application service and Infrastructure repository/provider. Handles are protected and bound to actor, school, configured root and expiry. Mutations require the **actual current school manager** and `Storage.ManageSchool`; storage delegates retain their existing view scope and additionally require the protected Duty/Visit policy for archive content. Feature enablement is not a prerequisite for the actual manager browsing their own school storage. Cross-school identities, moved items, forged/stale handles and destinations are checked on the server. There are no browser-selected raw provider IDs or general storage rights granted to duty delegates.

Deletion first returns actual subtree/file counts, active evidence/task names, signed-document impact and an expiring confirmation ticket. The operation, exact provider identities and ledger fingerprint are committed **before** provider I/O. SQL session locks serialize requests. Repeated requests return the same operation. Lost responses, SQL rollback after a successful trash operation and restart reconcile the existing result before another mutation. A changed subtree/link, moved item or revoked scope prevents a new write. Completion requires verified provider output and atomic ledger/evidence updates.

Files go to **Drive trash**, not irreversible permanent deletion. Ordinary stored files are soft-deleted; active evidence links are withdrawn and the existing legacy writer recalculates task status. Review decisions/audit remain. Signed snapshots, frozen artifacts, correction history, folder/file identities, immutable versions and completed delivery history are never removed or rewritten. Signed external copies become unavailable with an explicit manager-deletion reason (`DutyArchiveDeleted` / `DeletedByManager`); the duty UI shows Deleted and local frozen PDFs remain available. Background workers do not recreate a deliberately deleted copy. Existing legacy/archive mutation protections remain enforced.

Folder deletion also withdraws linked records already missing from Drive; provider writes remain confined to the selected native subtree. Recovery does not fabricate those previously missing bytes or revive their evidence. Rename/move cannot bypass protected ancestors through native/unindexed descendants.

Deleted items offers explicit manager recovery. Restoration reuses the recorded identities and verifies saved parent/name relationships and known byte hashes before updating SQL. **Drive can leave descendants trashed after restoring their parent**; recovery restores the confirmed subtree in parent-first order and resumes the same durable operation. It never adopts additional items. Restored files do not reactivate withdrawn evidence approvals. Native downloads of managed signed documents verify immutable length/hash into a private bounded-memory temporary stream before sending a response; staging files are deleted on close. A changed signed Drive payload is rejected.

## Actual external sequence

The private read-only inventory verified the exact already designated school root: **five top-level folders, 50 descendants, 41 folders and nine files**. They comprised `D3_Test` (four files), Visits (two PDFs), Duty (two letters; reports were not submitted), Library (empty files) and Teacher Files (one evidence file). The evidence deletion impact included the existing professional-experience task.

Both archive processors were paused in the test API process; startup initialization stayed disabled and original school enablement/credentials/root/grants were preserved. Five real manager HTTP deletion operations completed, with an actual zero-item root verified afterward. No test database was substituted for the original application's deletion ledger.

A separate **fresh isolated LocalDB** was rejected if already present before any migration, migrated with its sentinel preserved, and given synthetic users, a synthetic signature, isolated school/term/student scope and the existing protected connection. No original SQL migration, reset, account/signature change or real-parent notification occurred. The root was verified empty before the first provider create. Actual setup provisioned all four branches and teacher grants; replay preserved their provider identities.

**Sixteen live checks passed, with five actual uploads:**

| Trigger | Real output |
|---|---|
| Actual teacher acknowledgement | Responsibility letter, before its report existed |
| Actual signed report submission | Separate daily duty report |
| Actual V2 visit finalization/approval | Frozen official visit PDF |
| School library upload | File under the provisioned library |
| Teacher own-file upload | File under the provisioned teacher grant |

All five actual downloaded hashes/lengths matched their committed versions. Repeated processing produced no additional versions/uploads. No guardian, System or human messages were created. These were actual Application workflows and the real Google transport, rather than mocked delivery; they were not five browser form submissions. An initial harness-only missing mapping dependency was corrected, its partial synthetic hierarchy removed through the durable storage workflow, and the successful run repeated from an empty root with a new isolated SQL catalog.

The real current browser, with **no API interception**, then opened the actual four folders and downloaded/previewed the generated documents at **390/768/1440px**. Desktop exercised all five artifacts; phone/tablet exercised the official letter. Native browser downloads matched the generation hashes. Browser checks made no mutation requests. The generated synthetic hierarchy was subsequently cleaned through its own retained SQL operations, allowing recovery of the original registered hierarchy without rebinding signed history. `D3_Test` remains intentionally trashed. Final readiness/cleanup receipts are retained with this report.

The current renderer was unchanged. Actual downloaded letter/report pages and **all five visit pages** were rasterized and visually inspected: Arabic shaping, separate letter positions, original report signature, empty substitute fields, two explicit empty categories, last departure `00:02`, observations and handover are present. The synthetic timing exception belongs only to the isolated test school. The existing Visit renderer continues a remedial-plan cell from page4 to5; it was not redesigned. Original Duty source fingerprints/comparisons and representative multi-page/correction evidence remain in [D3 verification](../../school-duty/verification/d3.md). No private original sample or real personal signature was added.

## Verification

- **112/112 backend checks (Debug)** and **12/12 final focused Release checks** (overlapping tests, not an additional total), including actual guarded isolated SQL, frozen signature/hash/correction scope, legacy protection, manager deletion, link withdrawal, live revocation, two-context races, lost responses, real SQL rollback, explicit restore and signed-byte tampering. A fixture's missing enum status was corrected. The full suite and final focused run pass, including parent-first recovery and deletion of already missing linked evidence. An earlier full Release run passed111 checks before the additional missing-evidence case.
- **146/146 Angular regression checks**, Duty/timetable/standby/picker/guardian/internal messaging.
- **86/86 existing library Playwright contracts**; new native folder fixture reflects the requested root entry.
- Actual Angular with explicitly intercepted synthetic APIs: native navigation, linked deletion confirmation, cancellation/focus return, centered dialog, uncertainty/reconciliation without another delete, restore without evidence reapproval, rename, denied-data clearing, download and PDF rendering at390/768/1440. Committee map/switch/polling checks remain separate synthetic presentation evidence.
- **28/29 existing Storage/Visit SQL checks** pass; the unchanged S5 test expects a historical fixed pending-migration list and fails on13 already existing later migrations. Guard/sentinel/rollback checks remain intact.
- Current API Release build passes with zero warnings/errors. Development frontend build passes. Production CSS budgets and unrelated broad storage-spec/SQLite fixture limitations remain the recorded baseline; no budget was relaxed or production deployed.

Evidence: [new native UI](drive-browser-screens/browser-results.json), [real local/Drive browser reads](drive-browser-live-screens/browser-results.json), [letter/report/visit page images](drive-generated-pages/pages.json), and retained checks/results linked below. Mocked UI presentation, actual local integration and real provider delivery are explicitly distinct. Owner final appearance and physical-device/OS-keyboard acceptance remain **pending**.

## Reproduce safely

```powershell
# Always choose a previously nonexistent isolated SQL catalog; never migrate/reset operational SQL.
$env:ALFALAH_DUTY_TEST_CONNECTION='Server=(localdb)\MSSQLLocalDB;Database=AlFalahDutyTests_C27_'+[guid]::NewGuid().ToString('N')+';Integrated Security=true;TrustServerCertificate=true'
dotnet test backend/AlFalah.Tests --configuration Release --filter 'FullyQualifiedName~StorageDriveBrowserSqlTests'
# frontend directory, actual Angular4200 server:
node scripts/check-storage-drive.mjs
node scripts/check-storage-map.mjs
npx.cmd playwright test --config playwright.storage.config.ts library.spec.ts
```

The live generation harness additionally requires the private verified root inventory, existing credentials/keys, explicit empty-root authorization and an actually empty authorized synthetic root. Its guard must not be removed or redirected to an operational school. `ALFALAH_STORAGE_EMPTY_ROOT_TEST=1` plus a fresh `ALFALAH_DUTY_TEST_CONNECTION` invokes `dotnet run --project docs/specs/school-file-storage/verification/drive-generation-live -- D:/AlFalah-Manage-System`. Cleanup uses the retained generation reservation with `ALFALAH_STORAGE_GENERATION_CLEANUP=1`, opens only that isolated catalog and does **not** migrate/reset/drop it. The real-browser script needs a privately issued current manager session and the captured generation receipt; it fails without those boundaries.

Rollback disables new processing and preserves every signature, artifact, fact, version, operation and audit. Native UI rollback can restore the prior entry component while retaining storage history. No destructive Down migration is required. C27 does not authorize general feature delegation, waiting-register extensions, external messaging or production rollout.

## Final recovery and current manual-test state

All four original registered branches were recovered with their existing identities and verified signed bytes after the empty-root generation test was cleaned. `D3_Test` stays in Drive trash. The ordinary evidence file stays trashed and its evidence/task association stays withdrawn; restoration did not reactivate an approval. Both prior duty documents and both completed visits retain their original ledger/version pointers, signatures and completed archive history. There are zero pending native storage operations. No new migration was introduced; the original catalog remains at68 migrations and original teacher grants are unchanged.

Angular4200 and the latest Release API5264 are running for manual testing. Duty and Visit process switches are ON; the owner's school18 enablement stays ON and unrelated school1 stays OFF. Startup database initialization stays OFF. The final read-only smoke verified four actual native folders, absence of D3_Test, and both original duty PDFs equal to their frozen/local/archived hashes.

```powershell
Set-Location backend/AlFalah.Api
dotnet --roll-forward Major bin/Release/net8.0/AlFalah.Api.dll --urls http://localhost:5264 --environment Development --Database:InitializeOnStartup=false --SchoolDutyArchive:ProcessingEnabled=true --SchoolFileStorage:ArchiveProcessingEnabled=true
# Existing local configuration is read from this working directory.
```

Retained evidence: [sanitized live generation/recovery receipt](drive-generation-recovery-results.json), [current actual root/archive/switch screenshots](drive-final-screens/browser-results.json), [mocked map presentation](drive-map-screens/browser-results.json), [112 backend checks](drive-browser-checks/final-all-tests.txt), [final12 Release checks](drive-browser-checks/final-protection-tests.txt), [146 Angular checks](drive-browser-checks/angular-tests.txt), [86 library browser checks](drive-browser-checks/library-browser.txt), [real generation output](drive-browser-checks/generation-live.txt), [actual final native signed reads](drive-browser-checks/native-read-log.txt), [Storage/Visit baseline failure](drive-browser-checks/storage-visit-tests.txt), [development build](drive-browser-checks/frontend-development.txt) and [unchanged production-budget failure](drive-browser-checks/frontend-production.txt). Detailed TRX files accompany the retained logs. No private sessions, protected tickets, provider identifiers, original samples, keys or API request logs are included.
