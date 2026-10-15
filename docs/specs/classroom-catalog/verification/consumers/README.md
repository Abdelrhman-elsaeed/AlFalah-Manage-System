# C34 consumer verification — 2026-10-15

Evidence for C3 runtime implementation, separate from the C32 prototype and C33 foundation.

| Check | Result and scope |
|---|---|
| Backend regression | **513 distinct tests passed**,0 final failures/skips. [Latest result per test](backend-results.json) combines the broad regression with the final26-test archive replay after correcting historical test fixtures. Coverage includes classroom policy/migration, V2 workflow/dashboard, retired V1 writes, student-affairs workflows and timetable subjects/assignments/generation/review/repair/substitution/restore. This is a selected regression, not the entire repository suite. |
| Real SQL consumer/migration test | Fresh guarded LocalDB; migrations69→71 twice; retained/mapped/unresolved labels,105-row catalogue paging, teacher selection/concurrency/FKs, catalogue rename with an untouched published null-ID lesson, current-lesson/teacher roster/gate resolution through retained names, forged label/wrong ID/foreign school rejection, ID conflict grouping, unknown import rejection, corrupt ambiguous binding denial, prior-year same-name lookup, current-year directory pages and rollback refusal. |
| Archive compatibility |26 SQL archive tests passed using the existing fake Google provider and real V2 creation/finalization/PDF/archive services. Tests include snapshot retention, immutable artifacts, manual/automatic approval, retries/leases, provider-loss reconciliation, protected access and bounded5000-operation listing. This does not claim live Google uploads. |
| Angular focused | **46 passed**: existing catalogue/multi-select and timetable component checks plus contextual single selection, retained inactive options, same-date saved-ID revalidation, stale/failing lookup, date/teacher reset, legacy label handling, scoring-step save readiness and complete105-class lookup with later-page failure. |
| Build | Angular Development build passed after the final mobile CSS change. Backend API/tests compiled successfully in the regression run. |
| Live authenticated browser | Existing test-school manager opened the actual V2 and timetable pickers at390/768/1440. Date/year query parameters verified; no page errors, outer-page overflow or business-write requests. [Results](browser-live.json). On390 the timetable's existing fullscreen control is used; sticky widths were corrected so lesson cells remain exposed. |
| Live API | Date context200, unknown date400, conflicting year400, anonymous401. [Read-only results](api-live.json). |
| Local migration | [Preservation result](migration-result.json): schema71,16 existing classrooms and4 unresolved legacy teacher labels preserved. Original columns of retained visit/duty/storage/archive/Drive tables and timetable entries/versions were hashed before/after, excluding only the newly added nullable visit column. Existing classroom/bridge fields were separately compared. No operational business records or provider roots/flags were reset. |
| Source audit | [384-file lexical index](source-inventory.json), with remaining text comparisons classified in [consumer disposition](../../consumers.md). |

Private authentication/configuration, operational row contents and raw logs stay under ignored `.audit/classroom-consumers`; they are not published here. SQL mutation tests reject pre-existing databases, use synthetic accounts and leave their isolated databases available for inspection. No operational teacher selection, student enrollment, visit or timetable was saved to produce browser evidence.

Historical SQL fixtures now insert only the columns available before later migrations, instead of asking the current EF model to insert `TeachingRevision` into an older schema. The SQLite visit-dashboard fixture includes the new nullable `ClassroomId`; V2 archive fixtures now supply valid school/year classroom references. Their original behavioral assertions remain.

General Angular tests were not claimed clean; C33 documents unrelated storage spec compilation blockers. The earlier unrelated model-test `NoAction`/`Restrict` discrepancy was not changed. No production release, full-school performance benchmark, assistive-technology certification or physical-device/printer acceptance is claimed. C4 assignment UX, C5 assignment PDF and C6 pilot gates remain pending.

## Screenshots

- V2 visit picker: [phone](visits-390.png), [tablet](visits-768.png), [desktop](visits-1440.png).
- Timetable cell picker: [phone](timetable-390.png), [tablet](timetable-768.png), [desktop](timetable-1440.png).

## Repeatable checks

From the repository root, select fresh isolated database names. Existing databases are rejected before migrations and are never dropped by these fixtures:

```powershell
$env:ALFALAH_CLASSROOM_TEST_CONNECTION='Server=(localdb)\mssqllocaldb;Database=AlFalahClassroomTests_'+[Guid]::NewGuid().ToString('N')+';Trusted_Connection=True;TrustServerCertificate=True'
$env:ALFALAH_STORAGE_TEST_CONNECTION='Server=(localdb)\mssqllocaldb;Database=AlFalahS1Tests_'+[Guid]::NewGuid().ToString('N')+';Trusted_Connection=True;TrustServerCertificate=True'
dotnet test backend/AlFalah.Tests/AlFalah.Tests.csproj --filter 'FullyQualifiedName~AlFalah.Tests.StudentAffairs.|FullyQualifiedName~AlFalah.Tests.Timetables.|FullyQualifiedName~AlFalah.Tests.Classrooms.|FullyQualifiedName~VisitV2|FullyQualifiedName~VisitArchiveSqlTests|FullyQualifiedName~LegacyVisitWriteCutoverTests'
python docs/specs/classroom-catalog/scripts/audit-classroom-surfaces.py --output docs/specs/classroom-catalog/verification/consumers/source-inventory.json --scope 'C34 consumer cutover'
```

From `frontend`:

```powershell
npx ng build --configuration development
npx ng test --watch=false --browsers=ChromeHeadless --ts-config=tsconfig.classroom-consumers-spec.json --include='**/classrooms-management.component.spec.ts' --include='**/classroom-catalog.service.spec.ts' --include='**/daily-operations.service.spec.ts' --include='**/classroom-selector/*.spec.ts' --include='**/visit-workspace.component.spec.ts' --include='**/school-timetable.component.spec.ts'
node scripts/check-classroom-consumers-live.mjs
```

The real browser script requires existing test-school private sessions under ignored `.audit/classroom-consumers`, a configured local API at5264 and frontend at4200. It does not create accounts or expose credentials. The migration and application are running locally for manual review; other environments require their normal reviewed deployment process.
