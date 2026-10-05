# S6 operating plan — school 18 — 2026-10-05

Status: preparation and isolated rehearsal. **Operational cutover is not authorized or executed by this plan.** Four original storage flags remain OFF. Read the [S6 verification](verification/s6-import-and-rollout.md) for actual outcomes; historical S0–S5 reports describe their respective run dates.

## Pilot identity and inputs

The designated test school is **18 / Al-Falah E2E Test School**; its existing manager is **admin.test**. Backend resolves the actual manager, current membership and DB permissions. Additional operators need an explicit current direct delegation from that manager; there is no inferred permission from responsible names. Do not grant anyone during a rehearsal without an actual school decision.

Available: enabled Google connection and selected dedicated root, 4617 index references, 145 matrix rows in two alternative JSON/CSV representations, 36 inert tracker rows, existing S4 template, and actual old SQL rows. The HTML totals 4116/4994 belong to different static source displays; they are not Drive inventory. Missing: historical original bytes and attributable historical review decisions. Do not count those references as files, approved evidence or readiness.

The owner's request to test the connected account covers a clearly identified synthetic file/folder smoke on school 18 with an isolated SQL clone. It does not authorize original SQL migrations or flag/cutover changes. Synthetic files contain no personal/school records, remain retained on Google for audit, and their SQL identity remains in the isolated clone. The ordinary API must never discover and adopt them as historical operational evidence.

## Migration, capacity and backups

1. Build and inspect the idempotent S6 SQL. Only replace the import batch identity index; add columns/keys/triggers. `Down` refuses data loss. Keep compatibility with existing source-only rows. No deployment can run schema updates through API startup.
2. Before an operational window is selected, use `rehearse-s6.ps1`: COPY_ONLY + checksum verification of local Development, restore to a **fresh** `AlFalahSFS_S6_*` clone, migrate/backfill only the clone, reapply backfill, back up retained new records, restore to a second fresh DB and run CHECKDB. Existing databases are never overwritten or deleted.
3. Operations owner and school manager must choose the operational migration window and identify the executor. Record start/end UTC, school/year/template, migration IDs, backup identifiers and checksum/restore results in a retained cutover log. **No operational window/executor is approved yet.**
4. Capacity review must budget SQL frozen snapshots/PDFs, log growth during batch imports, disk for COPY_ONLY and restored databases, bounded upload spool/proxy limits (250 MiB file + 1 MiB request), available Google quota and provider throttling. Rehearsal measurements describe observed samples; they do not prove production capacity or Google tail latency.

## Compatibility and a single review writer

| Existing surface | New surface | Decision authority |
|---|---|---|
| Teacher Drive upload/rename/delete | S2 library/own files | Shared reservation/ledger; same file/version; protected assets need change requests |
| Legacy evidence review/matrix/export | S3 links/queue/decisions and S4 readiness | Existing compatibility adapters use `EvidenceReviewService` while `ReadModelEnabled` is ON; do not run an independent legacy decision writer |
| V1 historical visit reports | V2 official report + S5 frozen archive | Legacy V1 stays historical/read-only; S5 only captures current V2 approvals |
| Desktop Windows open/copy physical path | Internal authorized preview/download/file URL | Original Windows paths are provenance only; no server-open endpoint |
| Prototype completed/reference counters | S6 retained metadata and independent Draft links | No imported approval from completed; explicit normal review is required |

Before reopening legacy writes after rollback, stop review actions, export the pilot's new file/version/link/change/decision ledgers, compare each legacy submission with the shared provenance baseline, replay only supported compatibility mappings through the same service, retain unrepresentable school files/multiple links as protected new records, and record dispositions. Do not flatten multiple link decisions into one old submission state or overwrite a historical approval. Resume legacy writes only after reconciliation is reviewed by the school manager and operations executor.

## Monitoring and responsibility

`monitor-s6.ps1` performs scoped SELECTs only. Missing schema is **unavailable**, not zero. Unindexed Drive files and drift remain null until a live read observer/comparison supplies them. Run every five minutes during an approved pilot, and read provider metrics without tokens/IDs/content.

| Alarm | Threshold | Responsible / correction |
|---|---|---|
| Failed/NeedsAttention upload | Any | Storage operator: inspect reservation, reconcile same provider identity; never create a replacement blindly |
| Pending upload age | >10 min | Operations: inspect SQL/network/quota, resume same operation after live school authorization |
| Archive attention or expired lease | Any | Operations: inspect safe error code/lease; use authorized retry or reasoned recreate; retain original snapshot/version |
| Outbox/archive queue age | >10 min **with workers intended ON** | Operations: verify four gates, worker heartbeat, DB locking and quota; OFF during rollback is expected, not permission to switch ON |
| Approved link with missing/deleted bytes | Any | School manager: reconcile availability, remove fulfillment, recover retained authorized bytes or review replacement |
| Import conflict/missing source | Any unresolved before pilot acceptance | Import operator and manager: correct source/map an existing requirement; preserve exception or create an approved template revision, never mutate published provenance |
| Unindexed Drive files | Any unexplained | Storage operator: compare authorized read observation with ledger, identify retained synthetic smoke files, review attribution; no auto-adoption |
| Counts or readiness drift | Any unexplained difference | School manager: compare distinct files, versions, links, approved links and requirements separately; preserve filter denominator and classify reference-only rows |

## Activation and rollback

Activation needs owner parity/UI acceptance, approved operators/year, migration/capacity/backup window, root/grant/quota checks, attributable inputs and a monitored real V2 approval sample. School 1's old undecryptable credential is an independent blocker for that school; do not change keys to work around it. A single synthetic Google round trip does not validate long uploads, media codecs, live archive recovery or sustained load.

Use process/environment overlays only for isolated tests. For operational activation, record the actual four values and executor: `AdministrationEnabled`, `ReadModelEnabled`, `ArchiveWorkerEnabled`, `ArchiveExternalWritesEnabled`. Enable administrative/read surfaces first for the approved scope, then workers/external writes only after their separate gate. The current flags are global, **not school-specific**: do not activate the original multi-school environment as a school-18-only pilot without isolating hosting/data or an approved all-school rollout plan.

Rollback closes imports/UI/read surfaces and stops scheduling workers/external writes using all four flags OFF. The manager's disabled navigation/status page stays discoverable. Drain or record in-flight external reservations, retain every SQL write/outbox/snapshot/prepared PDF/file/version/decision and all Drive files, then run the compatibility reconciliation above. **No Down, table deletion, folder deletion, or Drive trash/delete.** After settings restart, verify direct APIs are gated and hashes/history/identities survive; checksum-restore into a fresh isolated database is the recovery rehearsal, not replacement of live data.

## Cutover log

| Event | Status | Executor/time |
|---|---|---|
| Owner requested S6 implementation and connected-account tests | Authorized task | Current request, 2026-10-05 |
| Isolated migrations/backfill/restore | See verification artifacts | Recorded tool timestamps |
| Synthetic school-18 live smoke | See verification artifacts | Recorded smoke timestamp |
| Operational migration / flag activation / review cutover | **Not executed** | No approved executor/window; no fabricated timestamp |
| Historical original-byte upload | **Not executed** | Originals absent |
| Physical legacy retirement | **Outside S6** | No action |
