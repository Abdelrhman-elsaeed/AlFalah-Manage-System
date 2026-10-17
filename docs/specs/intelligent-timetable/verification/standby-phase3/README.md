# Official standby register — phase 3 verification

Date: 2026-10-17. C40 / D-132. [Implementation](../../standby-periods/register-implementation.md).

## Automated evidence

- `dotnet test backend/AlFalah.Tests/AlFalah.Tests.csproj -c Release --no-build --no-restore --filter '(FullyQualifiedName~Timetables|FullyQualifiedName~CurrentLesson|FullyQualifiedName~TeacherContext|FullyQualifiedName~GatePass|FullyQualifiedName~ClassroomEntryPermit|FullyQualifiedName~Messaging|FullyQualifiedName~StudentAffairsOutbox)&FullyQualifiedName!~SocialWorkerCrmRepositoryQueryTests'`:341 passing checks, four opt-in SQL tests skipped without their separate connection variables. Projection tests exclude draft text, reject unrelated/previous assignment evidence and retain prior signed work during correction. Issue tests cover replay, stale conflict, bounded history, management/school scope and append-only storage. Existing timetable, swaps, ownership, permits, messaging and outbox behavior stays covered.
- `CoverageRegisterSqlTests` with a fresh guarded `ALFALAH_REGISTER_TEST_CONNECTION` (`AlFalahRegisterTests_<guid>`, integrated LocalDB, refuses existing databases): one passing real-SQL journey. Applies all migrations twice; manually allocates then fairly reallocates weekly standby while preserving locks; validates/publishes teaching; secretary attendance enqueues one durable signal despite repeated save; worker prepares needs; daily manual/fair review approves them; existing dispatcher deduplicates teacher notifications; actual teacher separately signs receipt/execution; actual renderer issues incomplete/complete retained PDFs; competing issuers retain only one source artifact. Total across the focused and SQL runs:342 passing backend/SQL checks.
- `npx ng test --no-watch --browsers=ChromeHeadless --ts-config=tsconfig.coverage-spec.json --include='src/app/features/timetables/standby/*.spec.ts' --include='src/app/features/dashboards/instructor-dashboard/*.spec.ts'`:16 passing Angular checks. New tests verify exact preview/download blob, lost-response replay, retained content retry, non-PDF/hash rejection, date clearing, conditional unload protection and issuing from a later history page without mixing pages.
- `node frontend/scripts/check-coverage-register-pdf.mjs`: [six independent PDF.js cases](pdf/pdf-results.json), actual A4 portrait geometry, repeated headings, text bounds, signatures and readable continuation completion. Renderer tests run with QuestPDF debugging disabled.
- `node frontend/scripts/check-coverage-register-live.mjs`: [real isolated SQL HTTP/browser workflow](browser-results.json), signed correction/new revision, stale409, unchanged older bytes, ten-item pagination, binary-transfer MIME and SHA equality between displayed and downloaded versions. Actual Angular shell at390/768/1440/1920; no page overflow. [Separate scoped HTTP checks](http-scope.json) prove teacher403 and valid other-school manager404.

Detailed logs, red/green failures, TRX and private synthetic connection/session material stay in ignored `.audit/standby-phase3/`. No secrets or private source photograph are included here. [Additive local migration](local-migration.json) proves schema75 and unchanged original values across45 checked tables; [authenticated local reads](local-read-check.json) exercise board/tasks/register without operational writes.

The initial SQL competing-issue run reproduced SQL1205 wrapped by EF. The repaired boundary produces the established conflict instead of500; retry returns the retained winner. A long-cell reference initially caused a real layout exception; shortening that reference preserves full appendix content. The pagination regression first failed before the history refresh fix. Test harness CORS forwarding was corrected to retain the actual transfer metadata rather than misclassifying a valid PDF as a failed response.

One existing timing-bounded repair-search assertion failed while production compilation competed for CPU (`regression-under-build-load.txt`). The same code/threshold passes in the subsequent focused regression without that concurrent build. The failed run is retained; no assertion or budget was loosened.

## Visual source comparison

Original `3.jpeg` was reopened and its193527-byte SHA-256 rechecked against discovery: `FA3FEE9E95089E1230230F2CD2657AD9A711B390267480627F856B327D7ACD36`.

The actual [three-teacher page](pdf/three-1.png) follows the portrait paper structure, title/code, exact instruction, seven RTL columns, three groups/eight rows, blank unused fields and deputy footer. [Four-teacher output](pdf/four.pdf) continues to a second sheet; [period9 output](pdf/period-nine.pdf) continues beyond the paper's eight-row group capacity. [Long signed output](pdf/long-signed.pdf) retains full names/narrative/notes with repeated headers and a separate receipt/execution signature pair. [Last continuation page](pdf/long-signed-4.png) was inspected independently of the application preview. Fixture signatures are synthetic test assets, not staff signatures.

UI captures: [desktop](register-1440.png), [phone](register-390.png), [tablet](register-768.png), [wide desktop](register-1920.png). Review confirmed a fit-width document rather than a narrow thumbnail, independent history, visible issue/download actions and bounded mobile scrolling. Duplicate stored academic-year prefixes were normalized after actual page inspection.

Ministry/Vision logo assets and named deputy signing are not configured. The approved platform logo/available letterhead replaces those marks; deputy name/signature stay blank. Exact official branding and owner visual/physical-print acceptance remain pending. Arabic shaping is visually correct; PDF.js extraction exposes NUL mappings for contextual Amiri glyphs, recorded per PDF. ASCII end markers in synthetic long fixtures verify continuation endpoints; full Arabic copy/search/assistive access is **not** claimed.

## Manual handoff

The frontend runs at `http://localhost:4200`; the normal local Development API is `http://localhost:5264`. Use existing accounts; no credentials are published.

1. As timetable administrator, complete the teaching timetable, open its weekly standby tab, choose manual/fair, review/save, then validate/publish. Standby is visible in school/teacher weekly views.
2. As authorized Secretary, record absence. Open **التبديل والاحتياطي → التغطية اليومية**, refresh needs when needed, choose manual/fair independently of the weekly choice, inspect candidates and approve the reviewed proposal.
3. As assigned teacher, use dashboard/navigation **تكليفات الانتظار**. Sign receipt, save the execution draft, then review/sign submission after the actual period ends. The administrator cannot sign for the teacher.
4. As administrator, open **سجل توزيع حصص الانتظار** in the daily ledger. Choose the date, then **تجهيز ومعاينة نسخة PDF**. It covers the whole day regardless of the ledger's search/filter/page. Review the fitted pages and **تنزيل النسخة المعروضة**.
5. Reopen a submitted execution with a reason; the previous signed text remains available, drafts are excluded and the new issue is incomplete until re-signed. Issue a later revision, then open/download an older history entry to verify that it stays unchanged.
6. Check empty/error states, history pagination, Arabic/English labels, keyboard/focus/zoom, and A4 portrait printing at100% with the school. This owner/printer acceptance has not been performed by the agent.

## Boundaries and baseline failures

No Drive upload policy/destination for this register has been agreed. Preview/download remain local retained documents; existing duty/visit archives and storage are not changed. No operational data reset, provider write, commit or production deployment occurs. Full-day attendance only; partial-day absence remains outside this delivery.

The Development build passes. Production build still hits the existing VisitsV2 CSS34.50kB and storage CSS39.34kB component budgets of32kB; no budgets are relaxed. The previously reproduced SQLite messaging fixture missing `Users.ProtectedPassword` is outside this selection and unchanged. Passing focused checks do not mean the entire repository suite or production build is green.
