# C29 / D-121 — Source1 official roster PDF

Owner-requested refinement, 2026-10-15. The official roster download now follows the supplied `1.jpeg` layout rather than the earlier four-week-per-page presentation. Current renderer: `school-duty/roster-source-2026-v5`. The signed letter/report templates, signatures, facts and archive obligations are unchanged.

## Source inspection and mapping

The original was opened visually at `C:/Users/abdelrhman/Desktop/المناوبة/New folder/1.jpeg`; SHA-256 remains `242FF09D0CE830E5592A05E3FD785F67B66C77CF3E68B998532B63465B05B997`. The private photograph, historical names, signatures, school values and logo were not copied into the repository or runtime defaults.

| Source position | Current output |
|---|---|
| Administrative header right, logo center, school/city left | Frozen configured header/assets and actual school display values in those positions. Missing branding remains blank; sample ministry/school assets are not invented. |
| Bordered title at right, form code left | `جدول المناوبة` / `QF71-B-2-2`, monochrome, without the previous colored heading band. |
| Academic year and term beneath title | Actual frozen values; a redundant existing `العام الدراسي` prefix is not printed twice. Year/numeric dates retain their correct reading direction. |
| Two touching vertical table halves | Up to nine complete weeks right, remaining weeks left; an ordinary 18-week/90-day fixture fits one A4 page. |
| Repeated weekly headings | RTL `أسبوع / اليوم / التاريخ / الاسم / التوقيع / ملاحظات`, with a vertically merged week-number cell and the source's organization. |
| Closure/exam rows | Original reason in the name position; canonical dates retained, no invented duty teacher or signature. |
| Educational-agent footer right, manager left | Separate positions, actual frozen manager name and blank signature lines. No signature is borrowed from acknowledgement or submission. |

The existing approved Gregorian school convention remains in place; photographed historical Hijri values are not runtime defaults. Whole weeks stay together. If long names or a longer configured period need more room, additional physical sheets repeat the official heading/footer without shrinking names, dropping rows or manufacturing completion. Short/partial weeks and seven-study-day calendars remain supported. The export covers the frozen full plan revision, independently of the UI's ten-day/teacher filter. Existing unassigned dates remain `—`; this presentation change does not generate or reassign duties.

## Immutable export versions and migration

Roster templates have a separate renderer identity from signed letters/reports. A new rendering of an existing plan revision reuses its **earliest retained roster snapshot**, preserving frozen teacher/header/logo/date values. Existing PDF bytes/hash/provenance remain immutable. Repeated current-template requests return exactly the committed artifact; no report/acknowledgement, lateness fact, notification or Drive upload is created.

Migration `20261015060717_VersionDutyRosterRenderings` changes only the filtered roster unique index from school/plan/revision to school/plan/revision/renderer. The independent signed-document unique index, immutable SQL triggers, scope/foreign-key/hash constraints and every existing row remain intact. Duplicate current-template artifacts are still rejected by SQL. Old versions, including local rendering refinements, remain retained.

The migration was validated on **fresh guarded isolated SQL first**, rejecting an existing catalog before any migration. The previous schema was seeded with a sentinel and frozen artifact; the idempotent upgrade script was applied twice and their bytes/hash/provenance preserved. Only after that passed, the owner's already authorized local Development catalog received this single pending migration: **68 → 69**, with all prior artifact/signature hashes, version pointers, root/credentials/enablement settings unchanged. No production migration/reset/deployment or external provider write occurred. Startup initialization remains disabled.

Down explicitly refuses to remove retained template versions. Rollback can hide the export or use an earlier renderer with the version-aware repository; retain the index/history. Do not restore the old single-artifact query against a revision that now has multiple retained versions.

## Actual verification

- **102/102** Duty/protected-storage backend checks passed with fresh isolated SQL. An expanded run passed **105/105**, including the shared visit PDF renderer and both manual/automatic approved-visit snapshot paths. The original D3 migration test now explicitly verifies that the newly added roster migration remains pending after the D3-only script, applies it, and still requires no pending migrations/model changes. Unrelated historical storage/SQLite/CSS failures were not relaxed.
- **8/8** focused roster/render/version/migration checks passed during refinement; **5/5** final renderer checks were repeated with the original shared PDF settings restored. These overlap the broad runs and are not an additional aggregate total.
- **74/74 Angular Duty checks** passed, including current term/revision download identity, duplicate-click suppression, stale term/revision responses and non-PDF rejection.
- Development frontend build passed in **75.728s**. Final backend build passed with zero warnings/errors. Existing production CSS budget failures remain the recorded baseline; no budget was changed.
- **Real authenticated local API/browser downloads** at **390/768/1440px**, without intercepted APIs: one export button inside the schedule panel; native download; full-plan bytes unchanged after UI filtering; server/artifact/download SHA-256 equality; repeated downloads identical; stale plan revision rejected409; no signing/archive requests. These are live local integration, not a mocked click.
- Five generated synthetic fixtures cover 18 weeks, 43 weeks, long/duplicate names with closure/exam rows, partial weeks and seven operating days. Their **10 pages**, plus all **three pages of the actual synthetic school's downloaded roster**, were rendered with the application's PDF.js engine and visually inspected. Every expected canonical date appears once, and embedded Amiri regular/bold fonts are retained. Text/hash checks supplement the page inspection rather than replace it.

The native Poppler/MuPDF intermediate captures showed missing repeated glyphs, while the actual PDF.js canvases showed the complete text. A temporary layout-cache experiment was reversed: the actual cached/default and experimental outputs were **pixel-identical in PDF.js on all three school pages**. Shared PDF/font configuration remains unchanged; no global cache override, dependency upgrade or broad Visit refactor was retained. Principal captures therefore use the actual browser PDF.js canvases, and misleading intermediate rasterizations remain private. See the pinned [QuestPDF rendering implementation](https://raw.githubusercontent.com/QuestPDF/QuestPDF/2024.3.4/Source/QuestPDF/Drawing/DocumentGenerator.cs) for the investigated rendering-pass boundary; the acceptance result is based on the actual generated/browser output.

Evidence: [source-style 18-week PDF](roster-source-pdf/source-eighteen-weeks.pdf), [18-week page](roster-source-pdf/pdfjs-source-eighteen-weeks-1.png), [actual school download](roster-source-pdf/actual-school-roster.pdf), [actual page3](roster-source-pdf/pdfjs-actual-school-roster-3.png), [page/font/hash inventory](roster-source-pdf/pages.json), [PDF.js rendering inventory](roster-source-pdf/pdfjs-results.json), [actual browser results](roster-source-screens/browser-results.json), and retained commands/results in [retained backend checks](roster-source-checks/final-tests.txt), [Angular checks](roster-source-checks/angular-tests.txt), [local migration receipt](roster-source-checks/local-migration.json), [actual API receipt](roster-source-checks/actual-result.json) and [render comparison](roster-source-checks/render-comparison.json).

## Reproduce and manual testing

```powershell
# New isolated catalogs only; never reset an existing catalog.
$env:ALFALAH_DUTY_TEST_CONNECTION='Server=(localdb)\MSSQLLocalDB;Database=AlFalahDutyTests_Roster_'+[guid]::NewGuid().ToString('N')+';Integrated Security=true;TrustServerCertificate=true'
$env:ALFALAH_DUTY_D3_PDF_DIR='D:/AlFalah-Manage-System/.audit/roster-source-20261015/pdf'
dotnet test backend/AlFalah.Tests --configuration Debug --filter 'FullyQualifiedName~DutyRosterRendererTests|FullyQualifiedName~Roster_export_is_frozen|FullyQualifiedName~Source_roster_upgrade|FullyQualifiedName~Roster_version_migration'
Set-Location frontend
npm.cmd test -- --watch=false --browsers=ChromeHeadless --ts-config=tsconfig.d2-spec.json --include='src/app/features/timetables/duty/**/*.spec.ts'
node scripts/check-duty-roster-live.mjs
node scripts/check-duty-roster-pdfjs.mjs
```

The live scripts require a privately issued current authorized synthetic-school session, expected artifact receipt and the retained local synthetic fixture PDFs. They must not be redirected to another school. PDF.js's inspection host is a static verification page, not a mocked application API; native-download checks do not intercept APIs. No private session, key, credential, provider ID or source sample is included in the evidence package.

Angular4200 and the current Debug API5264 are running for manual testing. Open **School Duty → Duty schedule → Official duty roster**, beside the schedule heading. Both archive processors remain ON with owner-selected school enablement preserved, and initialization stays OFF. Owner final appearance, physical-device/OS-keyboard and printer acceptance remain pending.
