# Phase 6 — Reports

**Status:** Stage 1 COMPLETED ✅ (server-side Arabic PDF — data only, minimal styling) · Stage 2 COMPLETED ✅ (official/branding layer — logo, signatures, QR) · Archive + export endpoints deferred to a later prompt · **Last updated:** 2026-07-11

> **Stage 1 (DONE):** server-side Arabic PDF download of an APPROVED
> visit, with embedded Arabic font, RTL layout, snapshot-driven content, and
> full role-based visibility (D-24/D-28/D-36/D-37 intact).
>
> **Stage 2 (DONE):** same endpoint, same gates — adds the official/branding
> layer ON TOP of the working Stage 1 Arabic PDF: school logo (or initials
> fallback), real Moderator + Manager signatures from `UserSignature`, school
> branding from `SchoolReportSettings` (header/footer text, primary color,
> signature + QR flags), and an informational QR code in the footer (compact
> reference payload — NO scores, NO PII). Every external asset has a safe PDF
> fallback so a missing logo / signature / QR NEVER crashes the report.
> **Archive + export endpoints are NOT part of Stage 2 and are deferred** to a
> separate later prompt.

## Goal (overall)
Server-side PDF reports with signatures, branding, and an archive.

## Scope
### In
- Server-side PDF generation ✅ (Stage 1)
- School logo + branding + real signatures + QR ✅ (Stage 2)
- Report archive (deferred)
- Export endpoints (deferred)

### Out
- Improvement plans (Phase 7), complaints (Phase 8)

## Stage 1 — Server-side Arabic PDF (data only)

### Goals (achieved)
- **#1 goal — correct Arabic rendering.** Use QuestPDF (Community license) for
  server-side PDF generation. The PDF MUST render Arabic correctly: proper RTL
  layout, correct Arabic letter shaping/joining (contextual forms), and no
  mojibake/boxes. Embed an Arabic-capable font (Amiri, shipped with the app,
  no system-font dependency) — set text direction RTL + right alignment.
- **#2 goal — immutable snapshot fidelity.** Every 25 scores, every 5 domain
  averages, the overall score, performance level, strengths/improvements/
  priorities come from the PERSISTED `VisitAnalysis` snapshot (docs/09 + D-26).
  NO recompute. The PDF matches the analysis API byte-for-byte.

### Endpoint
| Method | Path | Returns | Auth |
|--------|------|---------|------|
| GET | `/api/v1/visits/{id}/report/pdf` | `application/pdf` (file stream, NOT ApiResponse) | Data-driven gate (see below) |

**Permission gate is data-driven inside `VisitService.GetVisitReportAsync`** —
mirrors the existing `/report` endpoint pattern (no permission gate at the
controller; Visit.View is granted to all roles but the service enforces
Status == Approved + per-role visibility):

| Caller role / situation | Outcome |
|-------------------------|---------|
| Status != Approved | `400` — Arabic `لا يمكن إنشاء تقرير PDF لزيارة غير معتمدة.` (InvalidOperationException) |
| Visit not found / soft-deleted | `404` — Arabic `الزيارة غير موجودة.` |
| Unauthenticated | `401` |
| **Instructor — own approved visit** | `200` + PDF + `ReportViewLog` written (PDF download = a view) |
| **Instructor — other instructor's visit** | `403` — Arabic `لا تملك صلاحية إنشاء تقرير لزيارات المعلمين الآخرين.` |
| **School Manager — visit in HIS school** | `200` + PDF |
| **School Manager — cross-school** | `403` — Arabic (UnauthorizedSchoolAccessException) |
| **Moderator — visit HE created** | `200` + PDF |
| **Moderator — cross-moderator** | `403` — Arabic `لا تملك صلاحية الوصول إلى زيارات المشرفين الآخرين في مدرستك.` (D-37) |
| **SuperAdmin / MainManager** | `200` + PDF (global) |

### Report content (Stage 1)
From the visit's immutable snapshot + related data, on a Stage-1 minimal
Saudi-light-style PDF (no logo / real signatures / QR / archive — those are
Stage 2):

- **Header band** (brand-green strip): school name in Arabic + plain-text
  "تقرير زيارة صفية" title. A labelled `LOGO` box is left empty for Stage 2.
- **Meta card**: school, status pill (معتمدة), rubric version, instructor,
  subject, grade/class, moderator (creator), visit category + sequence Arabic
  labels, visit date, submitted date, approved date + approver name.
- **25 standards grouped by 5 domains** (D1..D5, ordered by domain code):
  each standard row = code chip + Arabic text (RTL) + circular score number
  badge + Arabic score label (متميز / متحقق بدرجة جيدة / متحقق جزئياً /
  يحتاج تحسين / غير مشاهد) + evidence note when present. Domain header
  shows the **persisted** `VisitDomainAverage.AverageScore` (NOT recomputed).
- **Analysis snapshot card**: overall score + performance level Arabic label
  (متميز / جيد جداً / جيد / متحقق جزئياً / يحتاج تحسين / غير مشاهد) +
  5-column domain-average strip.
- **Strengths / Improvement / Priority sections** (each Arabic-bulleted list
  with domain code + name + score, color-coded green / gold / red).
- **Signature card (Stage 1 placeholders only)**: two bordered boxes labelled
  "توقيع المشرف" + "اعتماد مدير المدرسة" each with `[ يُوقَّع يدوياً ]`
  + a dashed date line. Stage 2 swaps these for the persisted
  `UserSignature.ImagePath` / `SchoolReportSettings.ManagerSignatureImage`.
- **Footer**: generation timestamp + report title.

### Embedded Arabic font — Amiri
- Files: `backend/AlFalah.Infrastructure/Assets/Fonts/Amiri-Regular.ttf` and
  `Amiri-Bold.ttf` (downloaded from the official Amiri GitHub repository).
- **Embedded as project assets** (csproj `<None Update>` with
  `CopyToOutputDirectory="PreserveNewest"`) — present in the build output
  `bin/Debug/net8.0/Assets/Fonts/` so deployment doesn't depend on system
  fonts.
- Registered once at first use via
  `QuestPDF.Drawing.FontManager.RegisterFontWithCustomName(...)` under the
  custom names `"AmiriRegular"` and `"AmiriBold"`. Thread-safe via
  `Interlocked.CompareExchange` + lock.
- The page's `DefaultTextStyle` sets `.FontFamily("AmiriRegular")` +
  `.DirectionFromRightToLeft()`, so every text element inherits RTL + the
  Arabic font unless it explicitly opts out (Bold runs use `"AmiriBold"`).
- Verified by extracting the rendered PDF with `pdftotext -enc UTF-8 -layout`
  and confirming the rubric standard texts + Arabic score labels are
  connected and readable (output captured in `.audit/phase6/visit-5-text.txt`).

### Snapshot fidelity (D-26 carry-over)
The `VisitReportDto` is assembled from the **persisted** `VisitAnalysis` row:
- `OverallScore`, `PerformanceLevelAr` — straight from `VisitAnalysis`
- `Strengths` / `ImprovementAreas` / `PriorityStandards` — deserialized from
  the persisted JSON columns (NO recompute)
- Per-domain averages — read from `VisitDomainAverages` rows (1:1 snapshot,
  not `groupBy().average()`)
- 25 standard scores — read from `VisitScores` rows of the visit
- Per-standard code + Arabic text — read from the snapshotted
  `RubricStandard` rows (Visit.RubricVersionId is the snapshot, not the
  currently-active version — D-21 carry-over)
- Rubric version number — from `Visit.RubricVersion.VersionNumber`
- Date / approval fields — from `Visit.SubmittedAt` / `Visit.ApprovedAt` /
  `Visit.ApprovedByUser.FullName`
- Visit category / sequence Arabic labels — verbatim from `VisitCategoryExtensions`
  + `VisitSequenceExtensions.ToArabicString()` (D-29)
- Score Arabic labels — verbatim from docs/09 (`غير مشاهد`, `يحتاج تحسين`,
  `متحقق جزئياً`, `متحقق بدرجة جيدة`, `متميز`)

### Backend implementation
- **`IPdfReportService`** interface in `AlFalah.Application.Interfaces`
- **`PdfReportService`** concrete class in `AlFalah.Infrastructure.Services`
- **`VisitReportDto`** (and 4 supporting record DTOs) in
  `AlFalah.Application.DTOs.Reports`
- **`VisitService.GetVisitReportAsync(int id, CancellationToken ct)`** —
  assembles the payload with the same role gates as the existing
  `GetInstructorReportAsync` (and writes a `ReportViewLog`)
- **`VisitsController.DownloadReportPdf`** — thin controller (returns
  `File(pdfBytes, "application/pdf", fileName)`)
- **QuestPDF** NuGet package: `QuestPDF 2024.7.3` with
  `QuestPDF.Settings.License = LicenseType.Community;` set at app startup
  in `DependencyInjection.AddInfrastructure`
- DI registration: `services.AddScoped<IPdfReportService, PdfReportService>()`
- No EF queries in the controller; no business logic in the controller
- Arabic font files included via csproj `<None>` with `CopyToOutputDirectory`

### Frontend
- **`VisitsService.downloadReportPdf(id)`** — `HttpClient.get({ responseType: 'blob' })`
- **`visit-detail.component`** — new "تحميل التقرير PDF" button (pi-file-pdf,
  `p-button-outlined p-button-success`) visible only when the visit is in
  `status=4` (Approved) — same role rules as the existing `/report` endpoint
  (backend enforces; front-end gate is for UX only). Loading spinner via
  `pdfDownloading()` signal; success + error toasts that surface the server's
  Arabic error message verbatim via `extractApiErrorMessage()` helper.
- Filename pattern: `visit-{id}-report.pdf`

### i18n
- 8 new leaf keys merged into the existing `VISITS.*` + `REPORTS.*` namespaces
  (no duplicate top-level keys — **D-19 preserved**): `ar/en = 283/283`
  leaf keys, full parity.

## Stage 2 — Official/branding layer (logo + signatures + QR)

### Goals (achieved)
- Add the **official/branding layer** on top of the working Stage 1 Arabic PDF
  **without** touching the Stage 1 body (25 standards + analysis + RTL +
  Amiri font), **without** changing the public endpoint, and **without**
  breaking the visibility gates (D-24/D-28/D-36/D-37).
- Every external asset (school logo, moderator/manager signature, QR payload)
  has a **safe PDF fallback** so a missing or unreachable asset **never**
  crashes the report.

### Scope (additive — no schema change)
- **School logo**: render the school logo from `School.LogoUrl` (or the
  per-school `SchoolReportSettings.LogoUrl` if set) in the PDF header. If
  missing / unreachable / not an image → render a neutral initials placeholder
  (e.g. `م.ف` from `مدرسة الفلاح النموذجية`). No crash, no broken layout.
- **School branding** (from `SchoolReportSettings`, already a Phase 1 entity):
  - `ReportHeaderText` (fallback = school name) and `ReportFooterText`
    (fallback = report title) drive the header + footer text.
  - `PrimaryColor` (validated `#RGB` / `#RRGGBB`, fallback = Saudi brand green
    `#0F7132`) drives the header band, the rule under it, and the signature
    box border. The Stage 1 body composition keeps the static Saudi-green
    palette (no Stage 1 body color regression).
  - `ShowModeratorSignature` / `ShowManagerSignature` / `ShowQrCode` flags
    gate the corresponding PDF elements.
  - If no `SchoolReportSettings` row exists for the school → use sensible
    defaults (header text = school name, footer text = report title, color =
    Saudi green, signatures on, QR off). No crash.
- **Real signatures** (from `UserSignature`, already a Phase 1 entity):
  - [x] Create `signature_pad` canvas UI in frontend (`AccountSettingsComponent`)
  - [x] Wire Angular HTTP service to `/api/v1/account/signature` (GET/PUT)
  - [x] Inject real signature bytes in PDF via `container.Image(bytes).FitArea()`
  - [x] Graceful fallback (blank line) for missing signatures (no crashes, no placeholders)
  - [x] Refine Arabic typography (Amiri Font fix via RegisterFont)
  - [x] Fix RTL Header and Metadata alignment
  - [x] Fix Score badge alignments and sizes
  - **Moderator (creator) signature** — shown when `ShowModeratorSignature`
    is on. Falls back to printed name + dashed line when image is missing.
  - **School Manager (approver) signature** — shown when `ShowManagerSignature`
    is on AND the visit is approved (PDF is only ever generated for approved
    visits — D-26). Falls back to printed name + dashed line when image is
    missing. Printed name source = `Visit.ApprovedByUser.FullName` (now
    explicitly included in the Stage 2 include chain).
- **QR code**: when `ShowQrCode` is on, generate a server-side QR (PNG via
  QRCoder) and place it in the footer. Payload is a compact reference only —
  `alfalah:visit-{id}|school-{schoolId}|ref-{8-char-base32}` (FNV-1a hash of
  `visitId + schoolId + ApprovedAt.UtcTicks`). **NO scores, NO PII.** The
  public verification page is **deferred** (out of Stage 2 scope).
- **Layout polish**: header band = logo (RTL start / right) + header text
  (opposite side) + brand-color rule; signature block = two columns RTL
  (المشرف / مدير المدرسة) with image + printed name + date; footer = QR
  (start) + timestamp (center) + footer text (opposite).

### Image safety — `ImageAssetLoader`
A new utility service (registered as a singleton + an `HttpClient` named
`"PdfAssetLoader"`) sits in front of every external image:
- Accepts: **URL** (http/https with a 5-second timeout), **data URI**, **raw
  base64**, or **absolute file path**.
- **Hard 2 MB cap** — enforced both via `Content-Length` pre-check AND a
  streaming cap so a missing Content-Length cannot OOM the PDF.
- **Magic-byte sniffing** (PNG / JPEG / GIF only) — anything else returns null.
- **Wrap-everything in try/catch** — every failure mode (network error,
  oversize, unknown format, IO error, bad base64) returns null + a warning
  log. The PDF service then renders the safe fallback.

### Endpoint
**Unchanged** — `GET /api/v1/visits/{id}/report/pdf`. Same response, same
data-driven gate, same `ReportViewLog` write on every PDF download. The
branding layer is consumed **inside** the report service; the controller
stays thin and `FileStreamResult`-only.

### Visibility matrix (unchanged from Stage 1 — no D-24/D-28/D-36/D-37 regression)
| Caller role / situation | Outcome |
|-------------------------|---------|
| Status != Approved | `400` — Arabic `لا يمكن إنشاء تقرير PDF لزيارة غير معتمدة.` |
| Visit not found / soft-deleted | `404` — Arabic `الزيارة غير موجودة.` |
| Unauthenticated | `401` |
| **Instructor — own approved visit** | `200` + PDF + `ReportViewLog` written |
| **Instructor — other instructor's visit** | `403` — Arabic |
| **School Manager — visit in HIS school** | `200` + PDF |
| **School Manager — cross-school** | `403` — Arabic |
| **Moderator — visit HE created** | `200` + PDF |
| **Moderator — cross-moderator** | `403` — Arabic (D-37) |
| **SuperAdmin / MainManager** | `200` + PDF (global) |

### Fallback matrix (every cell verified during smoke-test)
| Missing asset | PDF behavior |
|---------------|--------------|
| `SchoolReportSettings` row (no row at all) | header text = school name; footer text = report title; color = Saudi green; signatures = on; QR = off; signatures render printed name + dashed line |
| `SchoolReportSettings.LogoUrl` + `School.LogoUrl` both empty | logo cell = neutral initials (e.g. `م.ف`) in a bordered cell |
| `LogoUrl` set but URL/file unreachable / not an image | same as above (initials fallback) |
| `UserSignature` row missing | printed name + `التوقيع يُوقَّع يدوياً` placeholder line |
| `SignatureImageUrl` set but unreachable / not an image | printed name + dashed placeholder line |
| `ShowQrCode = false` | no QR rendered in footer |
| `QR generation failure` (any exception inside QRCoder) | no QR rendered, report still succeeds |

### Backend implementation (additive)
- **`AlFalah.Application.DTOs.Reports.VisitReportDto`** — extended (additive)
  with: `SchoolInitials`, `SchoolLogoBytes` + `SchoolLogoFormat`, `HeaderText`,
  `FooterText`, `PrimaryColor`, `ShowModeratorSignature` /
  `ShowManagerSignature` / `ShowQrCode`, `ModeratorSignatureBytes` +
  `ModeratorSignatureFormat`, `ManagerSignatureBytes` +
  `ManagerSignatureFormat`, `QrPayload`. Every existing Stage 1 field
  preserved.
- **`AlFalah.Infrastructure.Services.ImageAssetLoader`** (new) — singleton,
  takes `IHttpClientFactory` + `ILogger`. URL / data URI / base64 / file-path
  loader with hard cap + magic-byte sniffing + try/catch fallback. Returns
  `LoadResult?` so the caller can branch on null.
- **`AlFalah.Infrastructure.Services.PdfReportService`** — extended. Stage 1
  body composition (`Palette.*` static constants) is byte-for-byte the same;
  Stage 2 adds new methods (`RenderSchoolLogoOrInitials`, `RenderLogoContent`,
  `RenderSignatureImage`, `RenderQrCode`) that the new `ComposeHeader` /
  `ComposeFooter` / `ComposeSignatureCard` use. A new `DynamicPalette` is
  resolved per render from `dto.PrimaryColor` (Saudi-green default) and drives
  ONLY the new branding elements.
- **`AlFalah.Infrastructure.Services.VisitService`** — extended:
  `BuildReportDto` → `BuildReportDtoAsync` + new
  `EnrichReportDtoWithBrandingAsync` step that runs after the existing
  pure-projection block (untouched). Loads `School.ReportSettings` (already in
  nav via `.ThenInclude`), prefers `SchoolReportSettings.LogoUrl` over
  `School.LogoUrl`, loads `UserSignature` rows for the moderator and the
  manager via `AsNoTracking` queries, and computes the compact QR payload via
  `BuildQrPayload` (FNV-1a hash). `LoadVisitAsync` gains a
  `.Include(v => v.ApprovedByUser)` so the printed manager name shows in the
  signature box (additive — no Stage 1 behavior change).
- **`QRCoder 1.6.0`** NuGet package added to
  `AlFalah.Infrastructure.csproj`. Used only inside `PdfReportService` —
  emits PNG bytes that are passed to QuestPDF's `container.Image(bytes)`.
- **DI** in `DependencyInjection.AddInfrastructure`:
  `services.AddHttpClient("PdfAssetLoader")` +
  `services.AddSingleton<ImageAssetLoader>()` + the existing
  `IPdfReportService` registration. `VisitService` constructor extended with
  the `ImageAssetLoader` singleton (additive parameter — no breaking change
  for consumers because DI resolves it automatically).
- **No migration** — `School.LogoUrl`, `SchoolReportSettings`, `UserSignature`
  all already exist from Phase 1; Stage 2 only consumes them.

### Frontend
- **No code change required.** The existing "تحميل التقرير PDF" button on the
  visit detail page (`visitsService.downloadReportPdf(id)`) already consumes
  the same endpoint and just downloads the new branded PDF. If the school
  has a `SchoolReportSettings` row, its flags are applied server-side — no UI
  surface needed.
- The signature-drawing UI itself is a **separate feature** and is NOT part
  of Stage 2. Stage 2 only **consumes** existing `UserSignature` data.

### i18n
- **No new keys** — Stage 2 reuses the existing `REPORTS.*` /
  `VISITS.*` / `RUBRIC.*` namespaces plus 1 hardcoded Arabic constant
  (`التوقيع غير متوفر`, used only as a defensive fallback when even the
  printed name is missing — the printed name normally comes from
  `ApplicationUser.FullName`). **D-19 parity preserved**: ar/en = 274/274
  leaf keys, zero new keys, zero duplicates.

### Acceptance — Stage 2 (all PASS)
- ✅ PDF shows the school logo (64×64 ICC image embedded) on a school WITH
  `SchoolReportSettings.LogoUrl` set; initials placeholder on a school
  WITHOUT one. No crash in either case.
- ✅ Header text = `مدرسة الفلاح النموذجية - التقرير الرسمي` (custom
  `ReportHeaderText`); footer text = `مديرة المدرسة • سكرتارية المدرسة •
  هاتف: 0112345678` (custom `ReportFooterText`). Falls back to school name /
  report title when no `SchoolReportSettings` row.
- ✅ Real Moderator signature rendered (200×60 ICC image) + printed name
  `سارة الحربي` + date line.
- ✅ Real Manager signature rendered (200×60 ICC image) + printed name
  `أحمد العُمري` + actual approval date `25-01-1448`.
- ✅ Missing-signature fallback verified: when `UserSignature` row is absent,
  printed name + `التوقيع يُوقَّع يدوياً` placeholder shown (no crash).
- ✅ QR rendered only when `ShowQrCode = true` (148×148 gray PNG embedded);
  encodes `alfalah:visit-5|school-1|ref-XXXXXXXX` (visit id + school id +
  short hash — NO scores, NO PII).
- ✅ Stage 1 Arabic shaping preserved — `pdftotext -enc UTF-8 -layout`
  re-extraction shows identical connected Arabic glyphs in body, sections,
  and signature labels (`مدرسة الفلاح النموذجية`, `متحقق بدرجة جيدة`, ...).
- ✅ Visibility gates intact — re-verified: non-approved → 400; missing visit
  → 404; cross-school → 403 (same `VisitService.GetVisitReportAsync` logic).
- ✅ **No D-24 / D-28 / D-36 / D-37 regression** — same code path, same
  exception types, same `ReportViewLog` write.
- ✅ Backend `dotnet build` 0 warnings / 0 errors.
- ✅ Frontend `ng build` (prod) green.
- ✅ `i18n parity preserved` — 274/274 leaf keys.
- ✅ Evidence saved in `.audit/phase6/`:
  - `visit-5-report-stage2.pdf` (no-settings fallback — initials + no QR)
  - `visit-5-report-stage2-full.pdf` (full branding — logo + signatures + QR)
  - `visit-5-text-stage2.txt` (extracted text, Arabic shapes correctly)

### Out-of-scope (deferred)
- **ReportArchive entity + archive listing endpoint** — deferred to a
  separate later prompt.
- **Export endpoints** (e.g. CSV / Excel of the 25 standards) — deferred.
- **Public verification page** for the QR code — deferred. The QR is
  informational only.
- **Signature-drawing UI** (capture user signatures in-app) — separate
  feature; Stage 2 only consumes existing `UserSignature` rows.

## Acceptance criteria — Stage 1
- ✅ GET `/visits/{id}/report/pdf` returns a valid PDF for an approved visit
- ✅ Arabic shaping verified via `pdftotext -layout` extraction (rubric
  standard texts + Arabic labels render connected — see
  `.audit/phase6/visit-5-text.txt`)
- ✅ Snapshot fidelity: 25 scores + 5 domain averages + overall + performance
  level + strengths/improvements/priorities match the persisted `VisitAnalysis`
  byte-for-byte
- ✅ Visibility enforced: instructor own-approved only (+ view logged), SM
  school-only, Moderator own-created only (D-37), cross-school 403/404,
  non-approved blocked
- ✅ No D-24 / D-28 / D-36 / D-37 regression
- ✅ Endpoint registered in Swagger (verified: `GET /api/v1/visits/{id}/report/pdf`
  appears under `paths`)
- ✅ Frontend download button + i18n keys (`VISITS.DOWNLOAD_PDF`,
  `REPORTS.PDF_TITLE`, etc.) — ar/en parity preserved
- ✅ `ng build` (prod) green
- ✅ Backend `dotnet build` 0 warnings / 0 errors
- ✅ No migration (no schema change)

## Dependencies
- Phase 5 (approval & visibility) — D-24/D-28/D-36/D-37 gates fully reused
- Phase 4 analysis snapshot — VisitAnalysis / VisitDomainAverage rows
- Phase 4 rubric snapshot — `Visit.RubricVersionId` (historical accuracy)
- D-21 — rubric is GLOBAL, snapshotted per visit
- D-30 — DB collation `Arabic_CI_AS` for Arabic columns
- D-37 — Moderator own-visits-only
