# C33 implementation verification — 2026-10-15

This is evidence for the production catalogue/teacher-selection foundation, distinct from the C32 standalone prototype evidence in the parent directory.

| Check | Result / limits |
|---|---|
| Backend focused regression |117 passed,0 failed,0 skipped: catalogue identity/policy, student/classroom workflows, teacher visibility/progress, current-lesson resolver, subject/availability and teaching assignments. [Counters](backend-results.json). |
| Real SQL | Included in the117: fresh prefix-guarded LocalDB; migrations69→70 twice; exact legacy backfill, Arabic numeric section, retained unmatched/letter text, atomic batch rejection,105 catalogue rows across pages, teacher service self-save/read-back and subject-only update, audit, two-context concurrency, ID rename/access, foreign school FK, structural duplicate and guarded rollback refusal. |
| Angular focused |11 passed: manager authority, loading/error/range/past-year/mixed-stage behavior, complete paging/late-page failure, selected inactive/off-page ID hydration, stale option requests, lookup failure preservation and ID-only changes. |
| Angular Development build | Passed. Runtime watch server also rebuilt successfully. |
| Real authenticated browser | Manager catalogue/dialog and teacher account selector at390/768/1440 against the existing test-school API. Zero page errors/business writes/page overflow. [Results](browser-live.json); screenshots below. |
| Local migration | [Preservation result](migration-result.json): original10 classroom rows and4 legacy association rows preserved,0 guessed matches, schema70. Signed artifacts, storage/archive records/settings/Drive roots/credentials and timetable entries hash-checked unchanged. No provider write. |
| Expanded backend sweep |280 of281 passed. Existing `StudentAffairsModelTests.MutableEntities_HaveSoftDeleteFilters_TenantKeys_AndRestrictedForeignKeys` fails because the pre-existing ConversationMessage→StudentDismissalLateness FK uses `NoAction` while its assertion requires `Restrict`. That mapping was not changed in this task. |
| General Angular suite | Blocked by existing storage spec compilation errors (`EvidenceWorkspaceComponent` argument changes and missing `schoolEnabled` fixture). A broader shell run also found a pre-existing exact people-menu expectation mismatch. These are reported, not hidden by changing unrelated tests. The checked-in focused tsconfig allows this feature's tests to run independently. |

Read-only real browser checks do not claim that operational teacher associations were changed for testing. Persistence/mutation tests ran in isolated SQL with synthetic users. Real test-school manager/teacher sessions remain private under ignored `.audit`; tokens and credentials are not in these artifacts. Existing Drive files and signed reports were not reset or regenerated.

## Screenshots

- Manager: [desktop](manager-1440.png), [tablet](manager-768.png), [phone](manager-390.png).
- Batch dialog: [desktop](manager-dialog-1440.png), [phone](manager-dialog-390.png).
- Teacher selection: [desktop](teacher-selector-1440.png), [tablet](teacher-selector-768.png), [phone](teacher-selector-390.png).

## Repeatable checks

From `frontend`:

```powershell
npx ng build --configuration development
npx ng test --watch=false --browsers=ChromeHeadless --ts-config=tsconfig.classroom-spec.json --include='**/classrooms-management.component.spec.ts' --include='**/classroom-catalog.service.spec.ts' --include='**/classroom-selector.component.spec.ts'
```

From the repository root, use a **fresh** local test database; the SQL test rejects every existing database before migration and does not drop existing databases:

```powershell
$env:ALFALAH_CLASSROOM_TEST_CONNECTION='Server=(localdb)\mssqllocaldb;Database=AlFalahClassroomTests_'+[Guid]::NewGuid().ToString('N')+';Trusted_Connection=True;TrustServerCertificate=True'
dotnet test backend/AlFalah.Tests/AlFalah.Tests.csproj --filter 'FullyQualifiedName~ClassroomCatalog'
python docs/specs/classroom-catalog/scripts/audit-classroom-surfaces.py --output docs/specs/classroom-catalog/verification/implementation/source-inventory.json --scope 'C33 implementation inventory'
```

The [new inventory](source-inventory.json) is a lexical index of373 files, not proof of full consumer cutover. The original354-file C32 inventory remains intact. See [remaining phases](../../implementation-plan.md) and [runtime decisions](../../implementation.md).

For the real read-only browser script, prepare local private sessions for the existing school18 test manager/teacher, then run `node scripts/check-classroom-catalog-live.mjs` from `frontend`. The script never embeds account passwords or prints tokens. Local application URLs are4200 (frontend) and5264 (API). Local deployment/physical-device/print acceptance and full C3–C5 implementation are not claimed by these checks.
