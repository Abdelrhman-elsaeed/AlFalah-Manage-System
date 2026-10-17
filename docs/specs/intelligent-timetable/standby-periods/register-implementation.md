# Official coverage register — phase 3

State: delivered locally on 2026-10-17. C40 / D-132. Owner visual/physical-print acceptance remains pending. [Verification and manual handoff](../verification/standby-phase3/README.md).

The local source `3.jpeg` is re-inspected. Verify its SHA-256 against the discovery record before visual comparison. Preserve A4 portrait, QF71-B-2-1, the exact instruction, seven RTL columns, three groups of eight printed rows, and the deputy/footer position. Use embedded Amiri and approved platform identity; the source's photographed logos, names and signatures are not assets. Ministry/Vision logo assets and a named deputy sign-off are not configured. Leave deputy name/signature blank and report this fidelity limit separately.

The source's signature column records signed receipt. Independent signed execution evidence goes in a clearly labelled appendix, retaining both actions and timestamps. Only the latest genuine execution for the current assignment is printable; unsent draft text/notes stay blank. An open correction keeps the prior signed execution, explicitly marked pending correction. Uncovered/review/inability states make the issue incomplete. Never turn notification delivery into a signature.

Issue one whole-school dated register from the management ledger, independent of client search/page/filter. Existing management/delegation applies before source, history or artifact access; teachers do not gain access to other teachers' reports. Freeze source fingerprint, human revision, snapshot and exact PDF bytes/hash. Repeated issue for a retained source returns that artifact. A stale uncached source conflicts. Later edits create another immutable revision. Retained reads do not depend on current publication, staff activation or Drive.

Use the existing PDF.js preview and blob download helper. The exact retained blob shown is downloaded; switching date or issuing a new revision clears obsolete preview state. Use paged history and explicit local issue status. Natural continuations handle extra teachers/periods; long cell content uses referenced appendices without shrinking/cropping Arabic text. Additional archive policy/target is unagreed, so this phase does not enable a Drive writer or reuse duty/visit archive flags.

Tests proceed through public projection, issue/replay/history, authorization and actual renderer/SQL/HTTP/browser workflows. Inspect one/three/four teachers, period9+, long names/narrative/notes, missing signatures, before execution and retained corrections. Final evidence distinguishes software, visual comparison and pending owner/printer acceptance. Existing production CSS and unrelated regression failures remain visible; do not weaken budgets.

## Delivered architecture and API

`CoverageRegisterController -> CoverageRegisterService -> ICoverageRegisterRepository -> existing TimetableSubstitutionRepository / EF` keeps authorization and issue policy in the service. `CoverageRegisterProjector` selects only current-assignment, actual-owner signed evidence; it verifies signature/snapshot hashes. `CoverageRegisterRenderer` uses the existing Amiri/QuestPDF infrastructure. Append-only `CoverageRegisterArtifact` retains source JSON/fingerprint, exact PDF/hash, renderer version, school/date, human revision, actor and issue time.

- `GET /api/v1/timetable-coverage/register?date=YYYY-MM-DD&page=1&pageSize=10`: current whole-day context and bounded metadata history; never issues a file. A missing/ambiguous current publication explains why a new issue is unavailable while retained history remains readable.
- `POST /api/v1/timetable-coverage/register` with `date` and `sourceRevision`: explicit issue or replay. An already retained source returns the same artifact even after source changes. A stale, never-issued source returns409. Unique school/date/revision and school/date/source/renderer keys prevent duplicate issues.
- `GET /api/v1/timetable-coverage/register/{id}/content`: school-scoped retained bytes, integrity checks, no-store, filename and SHA header; existing binary-transfer filter/interceptor remains in use. Teachers cannot access this whole-school register.

The manager ledger opens a separate register dialog with date, completion status, ten-item revision history and the existing fit-width PDF.js reader. Preview/download use the same validated blob. Issuing from history page two refreshes the actual first page; a secondary refresh failure preserves the successful preview and reports a recoverable history warning. Lost responses retry the same source. Read-only viewing does not install a reload warning; only an issue currently in flight does.

Migration `20261017072905_AddCoverageRegisterArtifacts` creates one additive artifact table and indexes. Fresh SQL verification preceded local schema75 rollout. Existing column hashes across45 related tables match before/after; no operational records were reset and no Drive files were written/deleted by this delivery. Normal local startup retains existing background workers; observed token refresh/Drive metadata reads are distinct from the isolated verification mutations.

## Verification and refinements

The focused backend regression selection passes341 checks, with four separately opt-in SQL suites skipped in that run. The new fresh-SQL integrated test passes separately:342 passing backend/SQL checks across those runs. Six actual renderer cases are independently parsed/rasterized;16 focused Angular checks and authenticated HTTP/browser workflows pass. See the evidence index for exact commands and scope rather than treating this as a passing full repository suite.

Real SQL exposed an EF-wrapped deadlock during simultaneous issuance. The existing transaction boundary now recognizes nested SQL1205 and returns the established conflict flow without attempting to roll back an already-aborted victim. The successful issue remains unique and replayable. Long-cell testing exposed a narrow class-column reference that did not fit; shorter appendix references fix it without cropping text or shrinking the font. Debug-enabled layout was not accepted as proof: normal renderer tests pass with debugging disabled.

Actual page review also removed a duplicate academic-year label when stored year names already include that prefix. Desktop/phone review confirms a contained wide document, distinguishable revision controls and the platform's established green/gold typography. The printed sheet deliberately follows the source's restrained ruled-paper layout.

## Remaining limits

Approved Ministry/Vision assets and deputy signing configuration are absent; the platform logo and school letterhead are used, with deputy name/signature blank. Structural source comparison is complete; exact official-branding and physical-printer acceptance remain owner tasks. Existing shaped Amiri glyphs render correctly, but PDF.js text extraction maps many contextual Arabic glyphs to NUL. This phase records that observed copy/search/accessibility limitation; independent canvas inspection and fixture-only ASCII continuation markers verify rendering/long-content completion without claiming full Arabic text extraction.

Drive archival policy for this distinct register is unagreed. Local retained preview/download works independently of Drive; duty/visit archiving settings are untouched. Existing VisitsV2/storage production CSS budget blockers and the previously reproduced unrelated SQLite messaging fixture issue remain recorded. No production rollout or commit was performed.
