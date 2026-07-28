using AlFalah.Application.Analysis;
using AlFalah.Application.DTOs.Reports;
using AlFalah.Application.Interfaces;
using System.Globalization;
using QRCoder;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AlFalah.Infrastructure.Services;

/// <summary>
/// Phase 6 / Stage 1 + Stage 2 — server-side PDF builder using QuestPDF
/// (Community license). Renders the visit's approved report with:
///   - RTL text direction + right-aligned Arabic paragraphs
///   - Embedded Amiri font (Regular + Bold) so Arabic shaping + diacritics
///     render correctly on every deployment (no system-font dependency)
///   - Stage 1 minimal styling + Stage 2 official/branding layer:
///       school logo (or initials fallback), primary-color accents,
///       header/footer text from SchoolReportSettings, real Moderator +
///       Manager signatures from UserSignature, and an optional QR code
///       in the footer. Every external asset has a safe PDF fallback —
///       a missing logo / signature / QR MUST NEVER crash the report.
///
/// BUG-FIX OVERHAUL (2026-07-11):
///   Bug 2 — Signature: renders the real drawn-signature image when bytes
///     are present; falls back to a BLANK LINE only (never any text like
///     "S. Manager"). The printed name + date line are always shown below.
///   Bug 3 — Domain averages: rewritten as a clean RTL row of 5 compact
///     cards — no stray "الاسم"/"المتوسط" header cells floating.
///   Bug 4 — Score badges: fixed-size 26×26 circular badge, semantic color,
///     aligned column, label left of badge, no oversized cells.
///   Bug 5 — Header RTL: logo is now on the RIGHT (QuestPDF Row items flow
///     L→R physically; logo placed as the second/last item so it appears on
///     the right; text block on the left so it's readable RTL).
///   Bug 6 — MetaCard / Footer: status badge aligned to the left (visual end
///     in RTL), footer text aligned right, clean 2-column meta grid.
/// </summary>
public class PdfReportService : IPdfReportService
{
    // Font names — mapped to the actual family name inside the TTF since we use RegisterFont(stream)
    private const string AmiriRegular = "Amiri";
    private const string AmiriBold = "Amiri";

    /// <summary>Verbatim Arabic labels — kept in a const class so future
    /// i18n extraction is one-file.</summary>
    private static class T
    {
        public const string ReportTitle        = "تقرير زيارة صفية";
        public const string LabelSchool        = "المدرسة";
        public const string LabelInstructor    = "المعلم المُقيَّم";
        public const string LabelSubject       = "المادة";
        public const string LabelGradeClass    = "الصف";
        public const string LabelLessonTitle   = "عنوان الدرس";
        public const string LabelPresentCount  = "عدد الحاضرين";
        public const string LabelAbsentCount   = "عدد الغائبين";
        public const string LabelNotes         = "ملاحظات المشرف العامة";
        public const string LabelModerator     = "المشرف الزائر";
        public const string LabelCategory      = "نوع الزيارة";
        public const string LabelSequence      = "التسلسل";
        public const string LabelVisitDate     = "تاريخ الزيارة";
        public const string LabelSubmittedAt   = "تاريخ الإرسال";
        public const string LabelApprovedAt    = "تاريخ الاعتماد";
        public const string LabelApprovedBy    = "اعتمدها";
        public const string LabelRubricVersion = "إصدار الأداة";
        public const string StatusApproved     = "معتمدة";
        public const string StatusDraft        = "مسودة — غير معتمدة";
        public const string LabelAttendance    = "الحاضرون / الغائبون";
        public const string LabelDomain        = "المحور";
        public const string LabelProgress      = "نسبة التقدم";
        public const string LabelFollowUpNote  = "ما تم تنفيذه";
        public const string LabelFollowDate    = "تاريخ المتابعة";
        public const string SectionIdentification = "بيانات الزيارة";
        public const string SectionStandards   = "تقييم المعايير";
        public const string SectionAnalysis    = "تحليل الزيارة";
        public const string SectionStrengths   = "نقاط القوة";
        public const string SectionImprovements= "مجالات التحسين";
        public const string SectionPriorities  = "معايير ذات أولوية";
        public const string SectionRecommendations = "التوصيات";
        public const string SectionFollowUps     = "متابعة خطط التحسين";
        public const string SectionSignatures  = "التوقيع والاعتماد";
        public const string SectionDomainAvg   = "متوسطات المحاور";
        public const string LabelOverallScore  = "الدرجة الكلية";
        public const string LabelAverageScore  = "المتوسط العام";
        /// <summary>Raw tally of standard scores — points, not a score out of 100.</summary>
        public const string LabelRawPoints     = "مجموع النقاط";
        // D-UI-1: the متوسطات المحاور table used LabelAverageScore ("overall
        // average") for a column that holds each DOMAIN's average — two
        // different quantities under one label. This is the domain column.
        public const string LabelDomainAverage = "متوسط المحور";
        public const string LabelScore         = "الدرجة";
        public const string LabelStandard      = "المعيار";
        public const string LabelNumber        = "م";
        public const string LabelPerformance   = "مستوى الأداء";
        public const string LabelSupervisorSig = "توقيع المشرف";
        public const string LabelInstructorSig = "توقيع المعلم";
        public const string LabelManagerSig    = "اعتماد مدير المدرسة";

        public const string FooterGenerated   = "تاريخ إنشاء التقرير";
        public const string LabelDomainScore = "درجة المحور";

        /// <summary>D-UI-1 — states the one published scale, once per table.</summary>
        public const string ScaleNote = "جميع الدرجات من 100.";

        // Explicit "nothing to show" lines. A printed official form must never
        // leave a section as a bare heading — the reader cannot tell whether
        // the data is missing or the report is truncated.
        public const string NoStandards      = "لم تُسجَّل معايير لهذه الزيارة.";
        public const string NoStrengths      = "لم تُحدَّد نقاط قوة لهذه الزيارة.";
        public const string NoImprovements   = "لا توجد مجالات تحسين مُحدَّدة.";
        public const string NoPriorities     = "لا توجد معايير ذات أولوية.";
        public const string NoRecommendations= "لا توجد توصيات مُسجَّلة.";
        public const string NoFollowUps      = "لم تُضف متابعات لخطط التحسين بعد.";
    }

    // ── Brand palette (Saudi light identity — same tokens as the frontend) ──
    // Stage 1 keeps the body composition on these static constants (Saudi
    // green) — that guarantees no visual regression on the body. Stage 2
    // honours a school's PrimaryColor by applying it to the NEW header band,
    // header rule, signature box border, and the brand accent strip via the
    // per-render _palette field below.
    private static class Palette
    {
        public const string BrandGreen    = "#0F7132";
        public const string BrandGreenText= "#15603D";
        public const string Gold          = "#D4AF37";
        public const string Text          = "#1E293B";
        public const string Muted         = "#64748B";
        public const string Border        = "#D9E1DC";
        public const string BorderStrong  = "#B9C7BF";
        public const string White         = "#FFFFFF";
        public const string Page          = "#F6F1E7";
        public const string ScoreTop      = "#22C55E";
        public const string ScoreHigh     = "#4ADE80";
        public const string ScoreMid      = "#D4AF37";
        public const string ScoreLow      = "#DC2626";
        public const string ScoreNone     = "#94A3B8";
    }

    /// <summary>Stage 2: dynamic palette for the new branding layer
    /// (header band + rule + signature box border). Saudi green default.</summary>
    private static class DynamicPalette
    {
        public static string BrandGreen = "#0F7132";
        public static string BrandGreenText = "#15603D";

        public static void Apply(string? primaryHex)
        {
            var hex = Normalize(primaryHex) ?? "#0F7132";
            BrandGreen = hex;
            BrandGreenText = Darken(hex, 0.10);
        }

        private static string? Normalize(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var s = raw.Trim();
            if (!s.StartsWith('#')) return null;
            var h = s[1..];
            if (h.Length == 6 && IsHex(h)) return "#" + h.ToUpperInvariant();
            return null;
        }
        private static bool IsHex(string s)
        {
            foreach (var c in s)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    return false;
            return true;
        }
        private static string Darken(string hex, double amount)
        {
            try
            {
                var s = hex.StartsWith('#') ? hex[1..] : hex;
                if (s.Length != 6) return hex;
                int r = Convert.ToInt32(s.Substring(0, 2), 16);
                int g = Convert.ToInt32(s.Substring(2, 2), 16);
                int b = Convert.ToInt32(s.Substring(4, 2), 16);
                r = (int)Math.Clamp(r * (1 - amount), 0, 255);
                g = (int)Math.Clamp(g * (1 - amount), 0, 255);
                b = (int)Math.Clamp(b * (1 - amount), 0, 255);
                return $"#{r:X2}{g:X2}{b:X2}";
            }
            catch { return hex; }
        }
    }

    public PdfReportService()
    {
        EnsureFontsRegistered();
    }

    public Task<byte[]> RenderAsync(VisitReportDto dto, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureFontsRegistered();

        // Stage 2: dynamic palette for the new branding layer (header band,
        // header rule, signature box border). The body composition methods
        // continue to use the static Palette (Saudi green) so Stage-1 visuals
        // do not regress.
        DynamicPalette.Apply(dto.PrimaryColor);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                // 24pt keeps the sheet inside every common printer's
                // unprintable margin while recovering enough height that the
                // signature block no longer needs a sheet of its own.
                page.Margin(24, Unit.Point);
                page.PageColor(Palette.White);
                page.Background().Background(Palette.White);
                page.DefaultTextStyle(t =>
                    t.FontFamily(AmiriRegular)
                     .FontSize(10)
                     .DirectionFromRightToLeft()
                     .FontColor(Palette.Text));

                // The full branded masthead prints on the first sheet only —
                // repeating the logo band clipped unpredictably in some viewers
                // and ate usable height. Continuation sheets instead carry a
                // slim running header so a page separated from the staple can
                // still be identified.
                page.Header().Column(header =>
                {
                    header.Item().ShowOnce().Element(c => ComposeHeader(c, dto));
                    header.Item().SkipOnce().Element(c => ComposeRunningHeader(c, dto));
                });
                page.Content().Element(c => ComposeContent(c, dto));
                page.Footer().Element(c => ComposeFooter(c, dto));
            });
        });

        var pdfBytes = document.GeneratePdf();
        return Task.FromResult(pdfBytes);
    }

    // ─── Page regions ─────────────────────────────────────────────────────────

    /// <summary>
    /// BUG-5 FIX — RTL header: brand-colored band with the school logo (or
    /// an initials placeholder) on the RIGHT (RTL inline-start / visual right)
    /// and the school name + report title text block on the LEFT (visual left).
    ///
    /// QuestPDF Row items flow LEFT-to-RIGHT physically, regardless of the
    /// page DefaultTextStyle's DirectionFromRightToLeft setting. To achieve
    /// "logo on the right" in an RTL document we place the TEXT column first
    /// (it renders on the left) and the LOGO column second (it renders on the
    /// right). The text inside the text column is right-aligned and RTL, so
    /// it reads correctly.
    /// </summary>
    private void ComposeHeader(IContainer container, VisitReportDto dto)
    {
        container
            .BorderBottom(2).BorderColor(DynamicPalette.BrandGreen)
            .PaddingBottom(8)
            .Column(outer =>
            {
                outer.Item().Background(DynamicPalette.BrandGreen).Padding(14).Row(row =>
                {
                    // ── LEFT side (visual left in the physical row) ──
                    // Text block: school name + report title, right-aligned RTL.
                    row.RelativeItem().PaddingRight(12).Column(col =>
                    {
                        col.Item().AlignRight().Text(SafeDisplayText(dto.HeaderText, dto.SchoolName))
                            .FontFamily(AmiriBold).FontSize(18).Bold().FontColor(Palette.White);
                        col.Item().PaddingTop(4).AlignRight().Text(T.ReportTitle)
                            .FontFamily(AmiriRegular).FontSize(12).FontColor("#FFF8DC");
                        col.Item().PaddingTop(3).AlignRight().Text(dto.VisitDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                            .FontFamily(AmiriRegular).FontSize(9).FontColor("#D7F1DF");
                    });

                    // ── RIGHT side (visual right = RTL inline-start) ──
                    // Logo or initials — BUG-5 FIX: logo now on the right.
                    row.ConstantItem(84).Height(72)
                        .Background(Palette.White).Padding(5)
                        .AlignMiddle().AlignCenter()
                        .Element(c => RenderSchoolLogoOrInitials(c, dto));
                });

                outer.Item().Height(3).Background(Palette.Gold);
            });
    }

    /// <summary>
    /// Slim identification band for sheets 2..n: teacher, visit date and the
    /// report title, so a loose page is still traceable to its report.
    /// </summary>
    private void ComposeRunningHeader(IContainer container, VisitReportDto dto)
    {
        container
            .PaddingBottom(8)
            .BorderBottom(1).BorderColor(DynamicPalette.BrandGreen)
            .PaddingBottom(5)
            .Row(row =>
            {
                row.ConstantItem(110).AlignLeft().AlignMiddle()
                    .Text(dto.VisitDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                    .FontFamily(AmiriRegular).FontSize(8.5f).FontColor(Palette.Muted);

                row.RelativeItem().AlignRight().AlignMiddle().Text(t =>
                {
                    t.Span(T.ReportTitle + " — ")
                        .FontFamily(AmiriBold).Bold().FontSize(9).FontColor(Palette.BrandGreenText);
                    t.Span(Dash(dto.InstructorFullName))
                        .FontFamily(AmiriRegular).FontSize(9).FontColor(Palette.Text);
                });
            });
    }

    /// <summary>
    /// Stage 2: renders the school logo image when bytes are available,
    /// otherwise falls back to a neutral initials placeholder so a missing or
    /// unreachable logo NEVER breaks the report.
    /// </summary>
    private void RenderSchoolLogoOrInitials(IContainer container, VisitReportDto dto)
    {
        container.Border(1).BorderColor(DynamicPalette.BrandGreenText)
            .Element(inner => RenderLogoContent(inner, dto));
    }

    /// <summary>Inner cell of the logo box — adds either the logo image
    /// or the initials text on a fresh single-child container.</summary>
    private static void RenderLogoContent(IContainer container, VisitReportDto dto)
    {
        var logoBytes = dto.SchoolLogoBytes is { Length: > 0 }
            ? dto.SchoolLogoBytes
            : TryLoadApplicationLogo();

        if (logoBytes is { Length: > 0 })
        {
            try
            {
                // QuestPDF detects the format from the byte magic (PNG/JPEG).
                // FitArea scales the image to fit the cell without overflowing
                // (a 1:1 logo in a 72x56 cell otherwise triggers the
                // AspectRatio "conflicting size constraints" exception).
                container.Image(logoBytes).FitArea();
                return;
            }
            catch
            {
                // Defensive: never let a bad image payload crash the report.
                // Fall through to the initials placeholder below.
            }
        }

        container
            .Text(string.IsNullOrWhiteSpace(dto.SchoolInitials) ? "؟" : dto.SchoolInitials)
            .FontFamily(AmiriBold).FontSize(20).Bold().FontColor("#FFF8DC");
    }

    private static byte[]? TryLoadApplicationLogo()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Assets", "Logo.png"),
            Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Logo.png"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "frontend", "src", "assets", "Logo.png")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "frontend", "src", "assets", "Logo.png"))
        };

        foreach (var path in candidates)
        {
            try
            {
                if (File.Exists(path)) return File.ReadAllBytes(path);
            }
            catch
            {
                // Branding is optional; continue to the safe initials fallback.
            }
        }

        return null;
    }

    /// <summary>
    /// Section order for the printed sheet: who/when → the rubric → the
    /// analysis → the narrative findings → signatures.
    ///
    /// Optional narrative sections are omitted when the analysis produced
    /// nothing, so the sheet does not carry pages of "no data" boxes. The
    /// signature block is NOT forced onto a fresh page any more — a forced
    /// break routinely produced an almost-empty final sheet. Each section is
    /// kept whole instead, so a card never splits across a page boundary.
    /// </summary>
    private void ComposeContent(IContainer container, VisitReportDto dto)
    {
        container.PaddingVertical(8).Column(col =>
        {
            col.Spacing(7);

            // Each block decides its own pagination (short → pinned whole,
            // long → heading glued to the first row and the rest flows), so no
            // ShowEntire is applied here: wrapping a block that cannot fit a
            // whole page raises a QuestPDF layout exception.
            col.Item().Element(c => ComposeMetaCard(c, dto));
            col.Item().Element(c => ComposeAnalysisCard(c, dto));
            col.Item().Element(c => ComposeStandardsCard(c, dto));

            if (dto.Strengths.Count > 0)
                col.Item().Element(c => ComposeStrengthsBlock(c, dto));

            if (dto.ImprovementAreas.Count > 0)
                col.Item().Element(c => ComposeImprovementsBlock(c, dto));

            if (dto.PriorityStandards.Count > 0)
                col.Item().Element(c => ComposePrioritiesBlock(c, dto));

            if (dto.Recommendations.Count > 0)
                col.Item().Element(c => ComposeRecommendationsBlock(c, dto));

            if (dto.PlanFollowUps.Count > 0)
                col.Item().Element(c => ComposeFollowUpsBlock(c, dto));

            col.Item().ShowEntire().Element(c => ComposeSignatureCard(c, dto));
        });
    }

    /// <summary>
    /// Stage 2 footer: school footer text (or generated timestamp) on the
    /// right, and the QR code on the left when <c>ShowQrCode</c> is on.
    /// QR encodes ONLY a compact reference (visit id + school id + short
    /// hash) — no scores, no PII. The verification page is deferred.
    ///
    /// BUG-6 FIX: footer text is now AlignRight (RTL start), QR on the left.
    /// </summary>
    private void ComposeFooter(IContainer container, VisitReportDto dto)
    {
        container.PaddingTop(8).BorderTop(0.5f).BorderColor(Palette.Border).PaddingTop(6)
            .Row(row =>
            {
                // QR (visual left side — end side in RTL).
                row.ConstantItem(50).Height(50).AlignCenter().AlignMiddle()
                    .Element(c => RenderQrCode(c, dto));

                // Page position — essential on a stapled paper report.
                row.ConstantItem(96).AlignLeft().AlignBottom().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontFamily(AmiriRegular).FontSize(8).FontColor(Palette.Muted));
                    t.Span("صفحة ");
                    t.CurrentPageNumber();
                    t.Span(" من ");
                    t.TotalPages();
                });

                // Generated timestamp (middle, centered). Local wall-clock
                // format — the ISO "u" form was unreadable on a printed sheet.
                row.RelativeItem().AlignCenter().AlignBottom().Text(t =>
                {
                    t.Span(T.FooterGenerated + ": ").FontColor(Palette.Muted).FontFamily(AmiriRegular).FontSize(8);
                    t.Span((dto.ApprovedAt ?? DateTimeOffset.UtcNow)
                            .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
                     .FontColor(Palette.Muted).FontFamily(AmiriRegular).FontSize(8);
                });

                // BUG-6 FIX: footer text aligned to the right (RTL start/inline-start).
                row.RelativeItem().AlignRight().AlignBottom().Text(t =>
                {
                    var footer = SafeDisplayText(dto.FooterText, T.ReportTitle);
                    t.Span(footer).FontFamily(AmiriRegular).FontColor(Palette.Muted).FontSize(8);
                });
            });
    }

    /// <summary>
    /// Stage 2: renders the QR code in the footer when <c>ShowQrCode</c> is on.
    /// Uses QRCoder (PNG output, ECC level M, 4 px per module) — small enough
    /// to embed in a 50pt footer cell without bloating the PDF.
    /// </summary>
    private static void RenderQrCode(IContainer container, VisitReportDto dto)
    {
        if (!dto.ShowQrCode || string.IsNullOrWhiteSpace(dto.QrPayload))
            return;

        byte[]? qrBytes = null;
        try
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(dto.QrPayload, QRCodeGenerator.ECCLevel.M);
            using var png = new PngByteQRCode(data);
            qrBytes = png.GetGraphic(4);
        }
        catch
        {
            // QR failures are non-fatal — the report still renders.
        }

        if (qrBytes is null || qrBytes.Length == 0)
            return;

        try
        {
            // QRCoder emits a PNG; QuestPDF detects the format from the byte
            // magic. FitArea keeps the QR inside the 72x50 footer cell.
            container.Image(qrBytes).FitArea();
        }
        catch
        {
            // Defensive: never crash on bad PNG.
        }
    }

    // ─── Sections ────────────────────────────────────────────────────────────

    /// <summary>
    /// Identification block. Two label/value columns inside a ruled table so a
    /// printed sheet reads as an official form: every value sits under its own
    /// rule, and the RTL start edge (visual right) always carries the label.
    /// </summary>
    private void ComposeMetaCard(IContainer container, VisitReportDto dto)
    {
        PdfTheme.SectionCard(container, T.SectionIdentification, card =>
        {
            // Status + rubric version strip. QuestPDF Row children flow
            // left-to-right physically, so the badge is declared first to land
            // on the visual left (the RTL end) and the version on the right.
            card.Item().PaddingBottom(6).Row(row =>
            {
                if (!dto.IsDraftWatermark)
                {
                    row.AutoItem()
                        .Background(Palette.BrandGreen)
                        .PaddingVertical(3).PaddingHorizontal(9)
                        .AlignMiddle().Text(T.StatusApproved)
                        .FontFamily(AmiriBold).FontSize(9).Bold().FontColor(Palette.White);
                }
                else
                {
                    row.AutoItem()
                        .Background(Palette.ScoreLow)
                        .PaddingVertical(3).PaddingHorizontal(9)
                        .AlignMiddle().Text(T.StatusDraft)
                        .FontFamily(AmiriBold).FontSize(9).Bold().FontColor(Palette.White);
                }

                row.RelativeItem();

                row.AutoItem().AlignRight().AlignMiddle().Text(t =>
                {
                    t.Span(T.LabelRubricVersion + ": ").FontColor(Palette.Muted).FontSize(8.5f);
                    t.Span(dto.RubricVersionNumber.ToString(CultureInfo.InvariantCulture))
                        .FontFamily(AmiriBold).Bold().FontSize(9).FontColor(Palette.Text);
                });
            });

            card.Item().Table(table =>
            {
                // Physical L→R: [value][label] | [value][label]
                // Reading RTL that is label → value for each of the two columns.
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn();
                    c.ConstantColumn(96);
                    c.RelativeColumn();
                    c.ConstantColumn(96);
                });

                var pairs = new List<(string Label, string Value)>
                {
                    (T.LabelInstructor,    Dash(dto.InstructorFullName)),
                    (T.LabelSchool,        Dash(dto.SchoolName)),
                    (T.LabelGradeClass,    Dash(dto.GradeClass)),
                    (T.LabelSubject,       Dash(dto.Subject)),
                    (T.LabelAttendance,    $"{dto.PresentCount} / {dto.AbsentCount}"),
                    (T.LabelLessonTitle,   Dash(dto.LessonTitle)),
                    (T.LabelCategory,      Dash(dto.VisitCategoryLabelAr)),
                    (T.LabelModerator,     Dash(dto.CreatedByFullName)),
                    (T.LabelVisitDate,     dto.VisitDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                    (T.LabelSequence,      Dash(dto.VisitSequenceLabelAr)),
                    (T.LabelApprovedAt,    DateOrDash(dto.ApprovedAt)),
                    (T.LabelSubmittedAt,   DateOrDash(dto.SubmittedAt)),
                };

                // Each iteration emits two label/value pairs. QuestPDF fills
                // cells left-to-right, so the SECOND pair lands on the visual
                // right — i.e. it is the one read first in RTL.
                for (var i = 0; i < pairs.Count; i += 2)
                {
                    var zebra = (i / 2) % 2 == 1;
                    MetaPair(table, pairs[i], zebra);
                    if (i + 1 < pairs.Count) MetaPair(table, pairs[i + 1], zebra);
                }
            });

            if (!string.IsNullOrWhiteSpace(dto.ApprovedByFullName))
            {
                card.Item().PaddingTop(6).AlignRight().Text(t =>
                {
                    t.Span(T.LabelApprovedBy + ": ").FontColor(Palette.Muted).FontSize(8.5f);
                    t.Span(dto.ApprovedByFullName!).FontFamily(AmiriBold).Bold().FontSize(9.5f);
                });
            }

            if (!string.IsNullOrWhiteSpace(dto.Notes))
            {
                card.Item().PaddingTop(8)
                    .Background(Palette.Page)
                    .Border(PdfTheme.BorderWidth).BorderColor(Palette.Border)
                    .Padding(8)
                    .Column(note =>
                    {
                        note.Item().AlignRight().Text(T.LabelNotes)
                            .FontFamily(AmiriBold).FontSize(9).Bold().FontColor(Palette.Muted);
                        note.Item().PaddingTop(3).AlignRight().Text(dto.Notes!)
                            .FontFamily(AmiriRegular).FontSize(9.5f).FontColor(Palette.Text);
                    });
            }
        });
    }

    private static void MetaPair(TableDescriptor table, (string Label, string Value) pair, bool zebra)
    {
        PdfTheme.BodyCell(table.Cell(), pair.Value, zebra: zebra, strong: true);
        PdfTheme.BodyCell(table.Cell(), pair.Label, zebra: zebra, color: Palette.Muted);
    }

    /// <summary>
    /// The rubric itself: one ruled table per domain. The domain code chip was
    /// removed — the printed sheet shows the domain's Arabic name and score
    /// only, because the internal D1…D5 identifiers mean nothing to a reader
    /// holding the paper.
    /// </summary>
    private void ComposeStandardsCard(IContainer container, VisitReportDto dto)
    {
        container.Column(col =>
        {
            col.Spacing(7);

            if (dto.Domains.Count == 0)
            {
                col.Item().Element(c => PdfTheme.SectionHeading(c, T.SectionStandards));
                col.Item().Element(c => PdfTheme.EmptyNote(c, T.NoStandards));
                return;
            }

            // The heading is glued to the first domain so it can never be left
            // stranded alone at the foot of a page, and every domain block is
            // atomic so a table never starts on a new sheet without the domain
            // name above it.
            col.Item().ShowEntire().Column(first =>
            {
                first.Item().Element(c => PdfTheme.SectionHeading(c, T.SectionStandards));
                first.Item().PaddingTop(7).Element(c => ComposeDomainBlock(c, dto.Domains[0]));
            });

            foreach (var d in dto.Domains.Skip(1))
                col.Item().ShowEntire().Element(c => ComposeDomainBlock(c, d));
        });
    }

    private void ComposeDomainBlock(IContainer container, ReportDomainBlockDto block)
    {
        container
            .Border(PdfTheme.BorderWidth).BorderColor(Palette.Border)
            .Column(col =>
            {
                // Domain strip: name on the RTL start edge (visual right),
                // score summary pinned to the visual left.
                col.Item()
                    .Background("#EAF5EE")
                    .BorderBottom(PdfTheme.BorderWidth).BorderColor(Palette.Border)
                    .PaddingVertical(5).PaddingHorizontal(8)
                    .Row(row =>
                    {
                        // D-UI-1: one figure, one scale. This used to print
                        // "91.7%  (3.67 / 4)" — the same result twice, on two
                        // different scales, in one strip.
                        row.ConstantItem(150).AlignLeft().AlignMiddle().Text(t =>
                        {
                            t.Span(ScoreScale.FormatWithMaximum(block.AverageScore))
                                .FontFamily(AmiriBold).Bold().FontSize(10.5f)
                                .FontColor(ScoreColorForLevelNumeric(block.AverageScore));
                            t.Span($"  {VisitAnalysisEngine.MapPerformanceLevel(block.AverageScore)}")
                                .FontColor(Palette.Muted).FontSize(8);
                        });

                        row.RelativeItem().AlignRight().AlignMiddle()
                            .Text(block.DomainNameAr)
                            .FontFamily(AmiriBold).FontSize(10.5f).Bold().FontColor(Palette.BrandGreenText);
                    });

                col.Item().Table(table =>
                {
                    // Physical L→R: [score 96] [standard text] [number 30]
                    // Reading RTL: number → standard → score.
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(96);
                        c.RelativeColumn();
                        c.ConstantColumn(30);
                    });

                    table.Header(header =>
                    {
                        PdfTheme.HeaderCell(header.Cell(), T.LabelScore, PdfTheme.CellAlign.Center);
                        PdfTheme.HeaderCell(header.Cell(), T.LabelStandard);
                        PdfTheme.HeaderCell(header.Cell(), T.LabelNumber, PdfTheme.CellAlign.Center);
                    });

                    if (block.Standards.Count == 0)
                    {
                        PdfTheme.EmptyRow(table, 3, T.NoStandards);
                        return;
                    }

                    for (var i = 0; i < block.Standards.Count; i++)
                    {
                        var std = block.Standards[i];
                        var zebra = i % 2 == 1;

                        table.Cell()
                            .Background(zebra ? PdfTheme.ZebraRow : Palette.White)
                            .BorderBottom(PdfTheme.BorderWidth).BorderRight(PdfTheme.BorderWidth)
                            .BorderColor(Palette.Border)
                            .PaddingVertical(4).PaddingHorizontal(5)
                            .AlignMiddle()
                            .Element(c => ComposeScorePill(c, std.Score, std.ScoreLabelAr, ScoreColor(std.Score)));

                        table.Cell()
                            .Background(zebra ? PdfTheme.ZebraRow : Palette.White)
                            .BorderBottom(PdfTheme.BorderWidth).BorderRight(PdfTheme.BorderWidth)
                            .BorderColor(Palette.Border)
                            .PaddingVertical(4).PaddingHorizontal(6)
                            .AlignMiddle()
                            .Column(sc =>
                            {
                                sc.Item().AlignRight().Text(std.StandardTextAr)
                                    .FontFamily(AmiriRegular).FontSize(9.5f).FontColor(Palette.Text);
                                if (!string.IsNullOrWhiteSpace(std.EvidenceNote))
                                    sc.Item().PaddingTop(2).AlignRight().Text(std.EvidenceNote!)
                                        .FontFamily(AmiriRegular).FontSize(8).Italic().FontColor(Palette.Muted);
                            });

                        PdfTheme.BodyCell(table.Cell(), (i + 1).ToString(),
                            PdfTheme.CellAlign.Center, zebra, color: Palette.Muted);
                    }
                });
            });
    }

    /// <summary>
    /// Score cell: a fixed 22×22 colour chip carrying the number, with the
    /// Arabic level word beside it. Fixed geometry keeps the whole column
    /// aligned down the page.
    /// </summary>
    private static void ComposeScorePill(IContainer container, int? score, string label, string color)
    {
        container.Row(row =>
        {
            row.ConstantItem(22).Height(22)
                .Background(color)
                .AlignCenter().AlignMiddle()
                .Text(score?.ToString() ?? "—")
                .FontFamily(AmiriBold).FontSize(9.5f).Bold().FontColor(Palette.White);

            row.RelativeItem().PaddingRight(4).AlignRight().AlignMiddle()
                .Text(label)
                .FontFamily(AmiriRegular).FontSize(7.5f).FontColor(color);
        });
    }

    private void ComposeAnalysisCard(IContainer container, VisitReportDto dto)
    {
        PdfTheme.SectionCard(container, T.SectionAnalysis, card =>
        {
            // D-UI-1: one published scale, and each cell a DISTINCT fact.
            // This strip used to print the same result three ways —
            // "3.12 / 4" beside "78 / 100" beside "91.7%" — so a reader could
            // not tell which number was the visit's score. Now:
            //   الدرجة الكلية = the authoritative score (equal-weight mean of the
            //                   domain averages, docs/09 D1) published on 0–100
            //   مجموع النقاط  = the raw tally of standard scores, labelled as
            //                   points so it can never be read as a second score
            //   مستوى الأداء  = the Arabic level derived from the same figure
            card.Item().Row(row =>
            {
                row.Spacing(6);
                // Declared left-to-right; reading RTL the level lands first.
                row.RelativeItem().Element(c => ComposeSummaryCell(c,
                    T.LabelPerformance, dto.PerformanceLevelAr, ScoreColorForLevel(dto.PerformanceLevelAr)));
                row.RelativeItem().Element(c => ComposeSummaryCell(c,
                    T.LabelRawPoints,
                    $"{dto.TotalScore:0.#} / {dto.MaximumScore:0.#}",
                    Palette.Muted));
                row.RelativeItem().Element(c => ComposeSummaryCell(c,
                    T.LabelOverallScore, ScoreScale.FormatWithMaximum(dto.OverallScore),
                    ScoreColorForLevelNumeric(dto.OverallScore)));
            });

            if (dto.Domains.Count == 0) return;

            card.Item().PaddingTop(10).AlignRight().Text(T.SectionDomainAvg)
                .FontFamily(AmiriBold).FontSize(10).Bold().FontColor(Palette.BrandGreenText);

            card.Item().PaddingTop(5).Table(table =>
            {
                // Level / score / name, name last so it holds the RTL start edge.
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(104);
                    c.ConstantColumn(84);
                    c.RelativeColumn();
                });

                table.Header(h =>
                {
                    PdfTheme.HeaderCell(h.Cell(), T.LabelPerformance, PdfTheme.CellAlign.Center);
                    PdfTheme.HeaderCell(h.Cell(), T.LabelDomainAverage, PdfTheme.CellAlign.Center);
                    PdfTheme.HeaderCell(h.Cell(), T.LabelDomain);
                });

                for (var i = 0; i < dto.Domains.Count; i++)
                {
                    var d = dto.Domains[i];
                    var zebra = i % 2 == 1;
                    var levelAr = VisitAnalysisEngine.MapPerformanceLevel(d.AverageScore);
                    PdfTheme.BodyCell(table.Cell(), levelAr, PdfTheme.CellAlign.Center, zebra,
                        color: ScoreColorForLevel(levelAr));
                    PdfTheme.BodyCell(table.Cell(), ScoreScale.Format(d.AverageScore), PdfTheme.CellAlign.Center, zebra,
                        strong: true, color: ScoreColorForLevelNumeric(d.AverageScore));
                    PdfTheme.BodyCell(table.Cell(), d.DomainNameAr, zebra: zebra, strong: true);
                }
            });

            // The column header says "متوسط المحور"; state the scale once so a
            // printed sheet passed around on paper never leaves it implicit.
            card.Item().PaddingTop(4).Element(c => PdfTheme.Caption(c, T.ScaleNote));
        });
    }

    private static void ComposeSummaryCell(IContainer container, string label, string value, string scoreColor)
    {
        container
            .Background(Palette.Page)
            .Border(PdfTheme.BorderWidth).BorderColor(Palette.Border)
            .Padding(9)
            .Column(col =>
            {
                col.Item().AlignRight().Text(label)
                    .FontFamily(AmiriRegular).FontSize(8.5f).FontColor(Palette.Muted);
                col.Item().PaddingTop(3).AlignRight().Text(value)
                    .FontFamily(AmiriBold).FontSize(16).Bold().FontColor(scoreColor);
            });
    }

    /// <summary>
    /// Rows a narrative list may hold and still be pinned to one sheet inside
    /// its bordered card. Above this the card is not pinned — a bordered card
    /// that splits leaves a rule hanging at the page fold — so the flowing form
    /// below is used instead.
    /// </summary>
    private const int MaxBulletRowsInPinnedCard = 12;

    /// <summary>
    /// Shared renderer for the four narrative lists (strengths, improvement
    /// areas, priority standards, recommendations). Each entry is a ruled row
    /// with a coloured marker, an optional trailing figure, and no internal
    /// domain / standard codes.
    ///
    /// Pagination: a short list keeps the card look and is pinned whole. A long
    /// one drops the outer card and glues the heading to its FIRST row inside a
    /// <c>ShowEntire</c> block — the same structure the standards card uses —
    /// so the title can never be left stranded at the foot of a page while its
    /// rows start on the next one.
    /// </summary>
    private static void ComposeBulletSection(
        IContainer container,
        string title,
        string accent,
        string marker,
        IReadOnlyList<(string Text, string? Figure)> entries,
        string emptyMessage)
    {
        if (entries.Count <= MaxBulletRowsInPinnedCard)
        {
            container.ShowEntire().Element(card => PdfTheme.SectionCard(card, title, body =>
            {
                if (entries.Count == 0)
                {
                    PdfTheme.EmptyNote(body.Item(), emptyMessage);
                    return;
                }

                body.Spacing(3);
                for (var i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    var index = i;
                    body.Item().Element(c => ComposeBulletRow(c, entry, index, accent, marker));
                }
            }, accent));
            return;
        }

        container.Column(col =>
        {
            col.Item().ShowEntire().Column(first =>
            {
                first.Item().Element(c => PdfTheme.SectionHeading(c, title, accent));
                first.Item().PaddingTop(3).Element(c => ComposeBulletRow(c, entries[0], 0, accent, marker));
            });

            for (var i = 1; i < entries.Count; i++)
            {
                var entry = entries[i];
                var index = i;
                col.Item().Element(c => ComposeBulletRow(c, entry, index, accent, marker));
            }
        });
    }

    private static void ComposeBulletRow(
        IContainer container,
        (string Text, string? Figure) entry,
        int index,
        string accent,
        string marker)
    {
        var (text, figure) = entry;
        var zebra = index % 2 == 1;

        container
            .Background(zebra ? PdfTheme.ZebraRow : Palette.White)
            .BorderBottom(PdfTheme.BorderWidth).BorderColor(Palette.Border)
            .PaddingVertical(4).PaddingHorizontal(6)
            .Row(row =>
            {
                // Physical L→R: [figure] [text] [marker].
                if (!string.IsNullOrWhiteSpace(figure))
                    row.ConstantItem(96).AlignCenter().AlignMiddle().Text(figure!)
                        .FontFamily(AmiriBold).Bold().FontSize(9.5f).FontColor(accent);

                row.RelativeItem().PaddingHorizontal(6).AlignRight().AlignMiddle().Text(text)
                    .FontFamily(AmiriRegular).FontSize(9.5f).FontColor(Palette.Text);

                row.ConstantItem(14).AlignCenter().AlignMiddle().Text(marker)
                    .FontFamily(AmiriBold).FontSize(9.5f).FontColor(accent);
            });
    }

    // D-UI-1: these printed a bare "3.67" / "1.17" with no scale beside it, so a
    // reader could not tell whether it was out of 4, 5 or 10. Published on 100
    // like every other aggregate figure in the document.
    private void ComposeStrengthsBlock(IContainer container, VisitReportDto dto) =>
        ComposeBulletSection(container, T.SectionStrengths, Palette.BrandGreen, "✓",
            dto.Strengths.Select(s => (s.DomainNameAr, (string?)ScoreScale.Format(s.AverageScore))).ToList(),
            T.NoStrengths);

    private void ComposeImprovementsBlock(IContainer container, VisitReportDto dto) =>
        ComposeBulletSection(container, T.SectionImprovements, Palette.Gold, "!",
            dto.ImprovementAreas.Select(i => (i.DomainNameAr, (string?)ScoreScale.Format(i.AverageScore))).ToList(),
            T.NoImprovements);

    /// <summary>
    /// Priority standards keep their flag marker — it is the visual cue the
    /// school uses to spot what must be worked on first — but no longer print
    /// the internal standard code.
    ///
    /// The figure here is the documented D-UI-1 exception: a single standard's
    /// rubric LEVEL (0..4) shown with its Arabic word, not a score out of 100.
    /// </summary>
    private void ComposePrioritiesBlock(IContainer container, VisitReportDto dto) =>
        ComposeBulletSection(container, T.SectionPriorities, Palette.ScoreLow, "⚑",
            dto.PriorityStandards
                .Select(p => (p.StandardTextAr, (string?)PriorityFigure(p)))
                .ToList(),
            T.NoPriorities);

    /// <summary>
    /// "1 يحتاج تحسين" — the level number with the Arabic word the visit
    /// service already resolved, so this block can never disagree with the
    /// standards table above it.
    /// </summary>
    private static string PriorityFigure(ReportPriorityStandardDto priority) =>
        string.IsNullOrWhiteSpace(priority.ScoreLabelAr)
            ? ScoreScale.FormatRubricLevel(priority.Score)
            : $"{ScoreScale.FormatRubricLevel(priority.Score)} {priority.ScoreLabelAr}";

    private static void ComposeRecommendationsBlock(IContainer container, VisitReportDto dto) =>
        ComposeBulletSection(container, T.SectionRecommendations, Palette.BrandGreen, "•",
            dto.Recommendations.Select(r => (r, (string?)null)).ToList(),
            T.NoRecommendations);

    /// <summary>
    /// Plan follow-ups. Uses <see cref="PdfTheme.TableSection"/> rather than a
    /// bordered card: the card form printed its title band at the foot of one
    /// sheet with the table on the next, and a long list needs the column
    /// header repeated on every page.
    /// </summary>
    private static void ComposeFollowUpsBlock(IContainer container, VisitReportDto dto)
    {
        PdfTheme.TableSection(container, T.SectionFollowUps, dto.PlanFollowUps.Count, table =>
        {
            // Physical L→R: [progress] [note] [date].
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(64);
                c.RelativeColumn();
                c.ConstantColumn(80);
            });

            table.Header(h =>
            {
                PdfTheme.HeaderCell(h.Cell(), T.LabelProgress, PdfTheme.CellAlign.Center);
                PdfTheme.HeaderCell(h.Cell(), T.LabelFollowUpNote);
                PdfTheme.HeaderCell(h.Cell(), T.LabelFollowDate, PdfTheme.CellAlign.Center);
            });

            if (dto.PlanFollowUps.Count == 0)
            {
                PdfTheme.EmptyRow(table, 3, T.NoFollowUps);
                return;
            }

            for (var i = 0; i < dto.PlanFollowUps.Count; i++)
            {
                var followUp = dto.PlanFollowUps[i];
                var zebra = i % 2 == 1;

                PdfTheme.BodyCell(table.Cell(),
                    followUp.ProgressScore.HasValue ? $"{followUp.ProgressScore.Value}%" : "—",
                    PdfTheme.CellAlign.Center, zebra, strong: true, color: Palette.Gold);

                table.Cell()
                    .Background(zebra ? PdfTheme.ZebraRow : Palette.White)
                    .BorderBottom(PdfTheme.BorderWidth).BorderRight(PdfTheme.BorderWidth)
                    .BorderColor(Palette.Border)
                    .PaddingVertical(4).PaddingHorizontal(6)
                    .Column(cell =>
                    {
                        cell.Item().AlignRight().Text(followUp.ProgressNote)
                            .FontFamily(AmiriRegular).FontSize(9.5f).FontColor(Palette.Text);
                        if (!string.IsNullOrWhiteSpace(followUp.EvidenceNote))
                            cell.Item().PaddingTop(2).AlignRight().Text(followUp.EvidenceNote!)
                                .FontFamily(AmiriRegular).FontSize(8).Italic().FontColor(Palette.Muted);
                    });

                PdfTheme.BodyCell(table.Cell(),
                    followUp.FollowDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    PdfTheme.CellAlign.Center, zebra);
            }
        });
    }

    /// <summary>
    /// Signature card: supervisor, evaluated instructor, and approving manager.
    /// Every available UserSignature image is rendered from real persisted data.
    ///
    /// Fallback: when the image bytes are null, render ONLY a blank horizontal
    /// line as the signature area. Never show "S. Manager" or any hard-coded
    /// name. The printed name + date line are ALWAYS shown below (these come
    /// from the visit's actual data).
    ///
    /// Each column can be suppressed via the corresponding SchoolReportSettings
    /// flag.
    /// </summary>
    private void ComposeSignatureCard(IContainer container, VisitReportDto dto)
    {
        PdfTheme.SectionCard(container, T.SectionSignatures, card =>
        {
            card.Item().Row(row =>
            {
                row.Spacing(8);

                // Declared left-to-right. Manager first so that, read RTL, the
                // order is supervisor → instructor → manager approval.
                if (dto.ShowManagerSignature)
                    row.RelativeItem().Element(c => SignatureBox(c, dto, SignatureParty.Manager));

                row.RelativeItem().Element(c => SignatureBox(c, dto, SignatureParty.Instructor));

                if (dto.ShowModeratorSignature)
                    row.RelativeItem().Element(c => SignatureBox(c, dto, SignatureParty.Moderator));
            });
        });
    }

    /// <summary>
    /// BUG-2 FIX — Signature box: renders the real signature image when
    /// bytes are present. If bytes are null/empty, renders ONLY a blank
    /// dashed line — no hard-coded text placeholder, no "S. Manager".
    /// The printed name (from visit data) and the date line are always shown.
    /// </summary>
    private enum SignatureParty { Moderator, Instructor, Manager }

    private static void SignatureBox(IContainer container, VisitReportDto dto, SignatureParty party)
    {
        var label = party switch
        {
            SignatureParty.Moderator => T.LabelSupervisorSig,
            SignatureParty.Instructor => T.LabelInstructorSig,
            _ => T.LabelManagerSig
        };
        var printedName = party switch
        {
            SignatureParty.Moderator => dto.CreatedByFullName,
            SignatureParty.Instructor => dto.InstructorFullName,
            _ => dto.ApprovedByFullName ?? string.Empty
        };
        byte[]? imageBytes = party switch
        {
            SignatureParty.Moderator => dto.ModeratorSignatureBytes,
            SignatureParty.Instructor => dto.InstructorSignatureBytes,
            _ => dto.ManagerSignatureBytes
        };

        // A known date is printed; an unknown one gets a ruled blank to be
        // filled in by hand. The blank is drawn, not typed as underscores —
        // a run of underscores next to Arabic text is reordered by the bidi
        // algorithm and the colon ends up on the wrong side.
        var dateValue = party == SignatureParty.Manager && dto.ApprovedAt.HasValue
            ? dto.ApprovedAt.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;

        container
            .Border(PdfTheme.BorderWidth).BorderColor(Palette.Border)
            .Column(col =>
            {
                col.Item()
                    .Background(Palette.Page)
                    .BorderBottom(PdfTheme.BorderWidth).BorderColor(Palette.Border)
                    .PaddingVertical(4).PaddingHorizontal(8)
                    .AlignRight().Text(label)
                    .FontFamily(AmiriBold).FontSize(9).Bold().FontColor(Palette.BrandGreenText);

                // Fixed height keeps all three boxes on one baseline whether or
                // not a drawn signature exists.
                col.Item().Padding(8).Height(58).AlignCenter().AlignMiddle()
                    .Element(c => RenderSignatureImage(c, imageBytes));

                col.Item().PaddingHorizontal(8).AlignCenter()
                    .Text(string.IsNullOrWhiteSpace(printedName) ? "________________" : printedName)
                    .FontFamily(AmiriBold).FontSize(9.5f).Bold().FontColor(Palette.Text);

                col.Item().PaddingTop(6).PaddingBottom(7).PaddingHorizontal(8).Row(dateRow =>
                {
                    // Physical L→R: [value or blank rule] [label].
                    if (dateValue is null)
                        dateRow.RelativeItem().PaddingHorizontal(4).AlignBottom()
                            .LineHorizontal(0.6f).LineColor(Palette.BorderStrong);
                    else
                        dateRow.RelativeItem().AlignRight().Text(dateValue)
                            .FontFamily(AmiriRegular).FontSize(8).FontColor(Palette.Text);

                    dateRow.ConstantItem(44).AlignRight().Text("التاريخ:")
                        .FontFamily(AmiriRegular).FontSize(8).FontColor(Palette.Muted);
                });
            });
    }

    /// <summary>
    /// BUG-2 FIX — Renders the signature image when bytes are present.
    /// Falls back to a blank dashed horizontal line when bytes are null/empty.
    /// Never throws — a malformed payload falls back to the blank line.
    /// </summary>
    private static void RenderSignatureImage(IContainer container, byte[]? bytes)
    {
        if (bytes is { Length: > 0 })
        {
            try
            {
                // QuestPDF detects the format from the byte magic (PNG/JPEG/GIF).
                // FitArea keeps the image inside the signature cell without
                // triggering the AspectRatio constraint exception.
                container.Image(bytes).FitArea();
                return;
            }
            catch
            {
                // fall through to blank line
            }
        }

        // BUG-2 FIX: Fallback is ONLY a blank signature line — no text placeholder.
        container.AlignBottom().PaddingHorizontal(6).LineHorizontal(0.8f).LineColor(Palette.BorderStrong);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// D-41 / Task 7 — single-visit PDF filename pattern.
    /// "{teacher} - {year} - {visitType}.pdf"
    ///   teacher  = evaluated instructor's Arabic name
    ///   year     = visit date's year (e.g. 2026)
    ///   visitType= the VisitCategory's verbatim Arabic label
    /// Sanitized for filesystem safety (Windows / Unix illegal characters
    /// replaced with whitespace + trimmed). Falls back to a safe placeholder
    /// ("ملف") when a component is empty / all illegal.
    /// </summary>
    public static string BuildPdfFilename(string? instructorFullName, DateTimeOffset visitDate, string? visitCategoryLabelAr)
    {
        var teacher  = SanitizeForFilename(instructorFullName);
        var year     = visitDate.Year.ToString(CultureInfo.InvariantCulture);
        var category = SanitizeForFilename(visitCategoryLabelAr);
        return $"{teacher} - {year} - {category}.pdf";
    }

    /// <summary>
    /// D-41 / Task 6 — ZIP filename pattern.
    /// "زيارات-{school}-{yyyy-MM-dd}.zip"
    /// Falls back to "زيارات-{date}.zip" when school name is empty.
    /// Sanitized for filesystem safety.
    /// </summary>
    public static string BuildZipFilename(string? schoolName, DateTimeOffset generatedAt)
    {
        var dateStr = generatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var school  = SanitizeForFilename(schoolName);
        if (string.IsNullOrWhiteSpace(school) || school == "ملف")
            return $"زيارات-{dateStr}.zip";
        return $"زيارات-{school}-{dateStr}.zip";
    }

    /// <summary>
    /// D-41 — filesystem-safe filename component. Removes Windows + Unix
    /// illegal characters, collapses whitespace, trims, and caps the length
    /// at 80 chars to leave headroom under the 255-char NTFS limit. Keeps
    /// Arabic letters / digits intact. Falls back to "ملف" when the input
    /// is null / empty / all illegal.
    /// </summary>
    public static string SanitizeForFilename(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "ملف";
        var s = System.Text.RegularExpressions.Regex.Replace(input, @"[\\/:*?""<>|\u0000-\u001F]", " ");
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim();
        if (s.Length == 0) return "ملف";
        if (s.Length > 80) s = s.Substring(0, 80).Trim();
        if (s.Length == 0) return "ملف";
        return s;
    }

    /// <summary>Em dash for an unset field, so a blank never reads as a bug.</summary>
    private static string Dash(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "—" : value;

    private static string DateOrDash(DateTimeOffset? value) =>
        value?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "—";

    private static string SafeDisplayText(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var questionMarks = value.Count(character => character is '?' or '�');
        return questionMarks >= 3 && questionMarks >= value.Length / 4
            ? fallback
            : value;
    }

    private static string ScoreColor(int? score) => score switch
    {
        4 => Palette.ScoreTop,
        3 => Palette.ScoreHigh,
        2 => Palette.ScoreMid,
        1 => Palette.ScoreLow,
        0 => Palette.ScoreLow,
        _ => Palette.ScoreNone
    };

    private static string ScoreColorForLevel(string levelAr) => levelAr switch
    {
        "متميز"          => Palette.ScoreTop,
        "جيد جداً"       => Palette.ScoreHigh,
        "جيد"            => Palette.ScoreHigh,
        "متحقق جزئياً"   => Palette.ScoreMid,
        "يحتاج تحسين"    => Palette.Gold,
        _                => Palette.ScoreLow
    };

    private static string ScoreColorForLevelNumeric(decimal average) => average switch
    {
        >= 3.5m => Palette.ScoreTop,
        >= 3.0m => Palette.ScoreHigh,
        >= 2.5m => Palette.ScoreHigh,
        >= 2.0m => Palette.ScoreMid,
        >= 1.0m => Palette.Gold,
        _       => Palette.ScoreLow
    };

    // ─── Font registration (embedded Arabic font) ───────────────────────────

    private static int _fontsRegistered;
    private static readonly object _fontsLock = new();

    private static void EnsureFontsRegistered()
    {
        if (System.Threading.Interlocked.CompareExchange(ref _fontsRegistered, 1, 0) != 0)
            return;

        lock (_fontsLock)
        {
            try
            {
                var regularPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "Amiri-Regular.ttf");
                var boldPath    = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "Amiri-Bold.ttf");

                if (File.Exists(regularPath))
                    using (var fs = File.OpenRead(regularPath))
                        FontManager.RegisterFont(fs);
                if (File.Exists(boldPath))
                    using (var fs = File.OpenRead(boldPath))
                        FontManager.RegisterFont(fs);
            }
            catch
            {
                // Re-registration is a no-op when called twice; swallow.
            }
        }
    }
}
