# C36 / D-128 — Assignment statement verification

Date:2026-10-16. [Runtime contracts and limits](../../statement.md), [phase gates](../../implementation-plan.md).

## Evidence boundaries

- Real current-school manager UI reads ran at390/768/1440; opening/closing PDF options did not change its assignments or classifications.
- A fresh `AlFalahStatementTests_*` integrated LocalDB fixture exercised migration, persistence and authorization boundaries. An API instance on5266 used that isolated database. HTTP classification saves, revision conflicts, artifact generation and downloads were confined to it. The browser used the authenticated local manager shell with a labelled synthetic setup context and proxied only the intelligent-timetable API prefix into the isolated API. The browser shell's school heading is therefore not evidence of operational assignment writes.
- Renderer cases contain synthetic teachers, labels and signatories. Nothing was copied from the photographed teacher list or signature. Local private sessions/configuration/database names/logs remain under ignored `.audit/assignment-statement/` and are not published here.

## Results

| Check | Result |
|---|---|
| Backend behavior/rendering/regression |67 final checks pass across TeachingStatementTests, TeachingAssignmentPhase6Tests, SubjectPhase5Tests and SchoolTimetableServiceTests. |
| Isolated SQL |One additional distinct SQL test passes: baseline migration, preserved allocation/pair sentinel, additive upgrade twice, query translation, category save, snapshot consistency, school boundaries, composite FK, retained bytes after revision change, duplicate recovery, immutability and guarded Down.68 unique backend test cases across the final and SQL runs; overlapping tests are not counted twice. [Counters](backend-results.json). |
| Angular |26 checks pass: existing19 assignment checks (including classification-preserving teacher reselection) plus7 statement checks covering explicit scope, no guessed classification, scoped sparse saves, duplicate-save blocking,409 retention, discard/control-rollback guards, partial-progress saves, saved Blob/filename, pending-preview close without false dirty state and JSON Blob errors. |
| PDF.js |Eight renderer cases; all teacher IDs present, A4 landscape, no text outside page bounds, real canvas rendering of every page.24-row reference fits one page; large120 teachers takes5; long names3; dense appendix2. [Complete results](pdf-results.json). |
| HTTP |Actual isolated SQL generation, same retained bytes on direct/negotiated calls, transfer metadata/no attachment for awaited Blob, uncached stale409, bad scope400, missing404 and teacher403. |
| Browser |390/768/1440: real read-only options, isolated SQL classification save, actual concurrent-revision recovery, PDF.js preview and native download byte equality. No page errors/document overflow. [Results](live-results.json). |
| Existing ledger browser regression |Real manager/teacher and synthetic single/split/pair/paging/conflict/retry/large-school checks still pass after the new action. [C36 run](ledger-regression.json); C35 historical evidence was preserved. |
| Builds |Backend and Development Angular builds pass. Production remains blocked by unchanged VisitsV2 CSS34.50kB and legacy-storage CSS39.34kB against32kB. Existing assignment CSS warning remains; new statement component does not add a blocking style error. [Summary](build-results.json). |

The timetable test fixture now supplies its AcademicTerm actor fields, previously omitted and tolerated by InMemory but rejected by SQL FKs. This is a fixture correction, not an operational workflow change. C30 transfer behavior is reused without modifying global binary handling. Automated Chromium has no installed IDM; final owner desktop confirmation remains pending.

## Review artifacts

| PDF case | File | First rendered page |
|---|---|---|
| Source structure,24 teachers | [PDF](reference-24.pdf) | [Image](reference-24-1.png) |
|120 teachers/continuation | [PDF](large-120.pdf) | [Image](large-120-1.png) |
| Long names/whole rows | [PDF](long-names.pdf) | [Image](long-names-1.png) |
| Dense cells/other materials | [PDF](dense-appendix.pdf) | [Image](dense-appendix-1.png) |
| Six primary grades | [PDF](primary-six.pdf) | [Image](primary-six-1.png) |
| Multiple stages | [PDF](multi-stage.pdf) | [Image](multi-stage-1.png) |
| Co/split/other totals | [PDF](co-split-other.pdf) | [Image](co-split-other-1.png) |
| Configured year prefix | [PDF](prefixed-year.pdf) | [Image](prefixed-year-1.png) |
| Actual isolated SQL HTTP file | [PDF](sql-http.pdf) | Download bytes verified in live-results |

| UI |390 |768 |1440 |
|---|---|---|---|
| Current-school read-only options | [Image](live-options-390.png) | [Image](live-options-768.png) | [Image](live-options-1440.png) |
| Isolated SQL preview | [Image](preview-390.png) | [Image](preview-768.png) | [Image](preview-1440.png) |

The desktop reference, long/dense/multi-stage documents and phone/desktop previews were visually reviewed. The ledger remains the working surface; the PDF uses the approved monochrome form. The phone footer gives the download action more room, and the document uses its own scroll/zoom area. Header/body/continuation assets and row bounds come from the PDF renderer, not screenshots of HTML pretending to be exported PDFs.

The owner's `2.jpeg` was reopened directly. SHA256:`933185E5121A48A19AEFFE6354FEF3515302FB5B42D1828A8996A98F2AEB11E3`. Logo/signatory/source limitations are explicit in the runtime document. Physical print parity and assistive-technology acceptance remain C6.

## Reproduce

From the repository root, select a genuinely fresh disposable LocalDB name with the required prefix; the SQL test rejects existing databases before migration:

```powershell
$env:ALFALAH_STATEMENT_TEST_CONNECTION='Server=(localdb)\mssqllocaldb;Database=AlFalahStatementTests_' + [Guid]::NewGuid().ToString('N') + ';Trusted_Connection=True;TrustServerCertificate=True'
$env:ALFALAH_STATEMENT_OUTPUT='D:/AlFalah-Manage-System/.audit/assignment-statement/pdf'
dotnet test backend/AlFalah.Tests/AlFalah.Tests.csproj --filter 'FullyQualifiedName~TeachingStatement|FullyQualifiedName~TeachingAssignmentPhase6Tests|FullyQualifiedName~SubjectPhase5Tests|FullyQualifiedName~SchoolTimetableServiceTests'
node frontend/scripts/check-assignment-statement-pdf.mjs
node frontend/scripts/check-assignment-statement-live.mjs
```

The last script requires local app4200/5264, the isolated test API5266, uncommitted manager session state and the prefix-validated isolated private connection/token file. It must not be redirected into an operational database. It changes classifications only through the isolated API. PDF.js inspection requires the local worker assets served on4200.

From `frontend`:

```powershell
npx ng test --watch=false --browsers=ChromeHeadless --ts-config=tsconfig.assignment-spec.json --include='**/assignments/*.spec.ts'
npx ng build --configuration development
npx ng build --configuration production
```

[Local additive migration result](local-migration.json) records schema72 and preserved original-column hashes across37 relevant tables. It is distinct from the disposable database test. No production deployment, original-school test classification write or new Drive upload is claimed.
