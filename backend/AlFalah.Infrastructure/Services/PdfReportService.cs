using AlFalah.Application.DTOs.Reports;
using AlFalah.Application.Interfaces;
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
        public const string SectionStandards   = "تقييم المعايير";
        public const string SectionAnalysis    = "تحليل الزيارة";
        public const string SectionStrengths   = "نقاط القوة";
        public const string SectionImprovements= "مجالات التحسين";
        public const string SectionPriorities  = "معايير ذات أولوية";
        public const string SectionRecommendations = "التوصيات";
        public const string SectionFollowUps     = "متابعة خطط التحسين";
        public const string SectionSignatures  = "التوقيع والاعتماد";
        public const string SectionDomainAvg   = "متوسطات المحاور";
        public const string LabelOverallScore  = "المتوسط العام";
        public const string LabelPerformance   = "مستوى الأداء";
        public const string LabelSupervisorSig = "توقيع المشرف";
        public const string LabelInstructorSig = "توقيع المعلم";
        public const string LabelManagerSig    = "اعتماد مدير المدرسة";

        public const string FooterGenerated   = "تاريخ إنشاء التقرير";
        public const string LabelDomainAvgPrefix = "متوسط المحور";
        // D-41 / Task 3 — clear Arabic watermark stamped on PDFs generated
        // for non-Approved visits. Must be unambiguous so the document
        // cannot be mistaken for an official report.
        public const string DraftWatermark    = "نسخة غير معتمدة";
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
        public const string Border        = "#E3E1D8";
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
                page.Margin(28, Unit.Point);
                page.PageColor(Palette.White);
                page.DefaultTextStyle(t =>
                    t.FontFamily(AmiriRegular)
                     .FontSize(10)
                     .DirectionFromRightToLeft()
                     .FontColor(Palette.Text));

                // D-41 / Task 3 — when the visit is not Approved, draw a
                // translucent Arabic watermark banner across the top of every
                // page (red band, white text, RTL-aligned). page.Foreground
                // renders ABOVE content but does NOT cover the page header /
                // footer, so the watermark stays visible at the very top of
                // each page where it cannot be missed.
                if (dto.IsDraftWatermark)
                {
                    page.Foreground().Element(c => ComposeDraftWatermark(c));
                }

                page.Header().Element(c => ComposeHeader(c, dto));
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
            .Background(DynamicPalette.BrandGreen)
            .Padding(14)
            .Column(outer =>
            {
                outer.Item().Row(row =>
                {
                    // ── LEFT side (visual left in the physical row) ──
                    // Text block: school name + report title, right-aligned RTL.
                    row.RelativeItem().PaddingRight(12).Column(col =>
                    {
                        col.Item().AlignRight().Text(dto.HeaderText)
                            .FontFamily(AmiriBold).FontSize(16).Bold().FontColor(Palette.White);
                        col.Item().PaddingTop(4).AlignRight().Text(T.ReportTitle)
                            .FontFamily(AmiriRegular).FontSize(13).FontColor("#FFF8DC");
                    });

                    // ── RIGHT side (visual right = RTL inline-start) ──
                    // Logo or initials — BUG-5 FIX: logo now on the right.
                    row.ConstantItem(72).Height(56)
                        .AlignMiddle().AlignCenter()
                        .Element(c => RenderSchoolLogoOrInitials(c, dto));
                });

                // Brand-color rule under the header.
                outer.Item().PaddingTop(6).LineHorizontal(1.2f).LineColor(DynamicPalette.BrandGreen);
            });
    }

    /// <summary>
    /// Stage 2: renders the school logo image when bytes are available,
    /// otherwise falls back to a neutral initials placeholder so a missing or
    /// unreachable logo NEVER breaks the report.
    /// </summary>
    private void RenderSchoolLogoOrInitials(IContainer container, VisitReportDto dto)
    {
        container.Border(1).BorderColor("#FFFFFF55")
            .Element(inner => RenderLogoContent(inner, dto));
    }

    /// <summary>Inner cell of the logo box — adds either the logo image
    /// or the initials text on a fresh single-child container.</summary>
    private static void RenderLogoContent(IContainer container, VisitReportDto dto)
    {
        if (dto.SchoolLogoBytes is { Length: > 0 } logoBytes)
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

    private void ComposeContent(IContainer container, VisitReportDto dto)
    {
        container.PaddingVertical(12).Column(col =>
        {
            col.Spacing(14);

            col.Item().Element(c => ComposeMetaCard(c, dto));
            col.Item().Element(c => ComposeStandardsCard(c, dto));
            col.Item().Element(c => ComposeAnalysisCard(c, dto));

            if (dto.Strengths.Count > 0)
                col.Item().Element(c => ComposeStrengthsBlock(c, dto));

            if (dto.ImprovementAreas.Count > 0)
                col.Item().Element(c => ComposeImprovementsBlock(c, dto));

            col.Item().Element(c => ComposeRecommendationsBlock(c, dto));

            if (dto.PriorityStandards.Count > 0)
                col.Item().Element(c => ComposePrioritiesBlock(c, dto));

            if (dto.PlanFollowUps.Count > 0)
                col.Item().Element(c => ComposeFollowUpsBlock(c, dto));

            col.Item().Element(c => ComposeSignatureCard(c, dto));
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
                row.ConstantItem(72).Height(50).AlignCenter().AlignMiddle()
                    .Element(c => RenderQrCode(c, dto));

                // Generated timestamp (middle, centered).
                row.RelativeItem().AlignCenter().Text(t =>
                {
                    t.Span(T.FooterGenerated + ": ").FontColor(Palette.Muted).FontFamily(AmiriRegular).FontSize(8);
                    t.Span(dto.ApprovedAt.HasValue
                        ? dto.ApprovedAt.Value.ToString("u")
                        : DateTimeOffset.UtcNow.ToString("u"))
                     .FontColor(Palette.Muted).FontFamily(AmiriRegular).FontSize(8);
                });

                // BUG-6 FIX: footer text aligned to the right (RTL start/inline-start).
                row.RelativeItem().AlignRight().Text(t =>
                {
                    var footer = string.IsNullOrWhiteSpace(dto.FooterText)
                        ? T.ReportTitle
                        : dto.FooterText;
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

    /// <summary>
    /// D-41 / Task 3 — Arabic draft watermark. Renders a translucent red band
    /// across the very top of every page with the text "مسودة — غير معتمدة" in
    /// bold white. Used when the visit is NOT Approved so the document cannot
    /// be mistaken for an official report.
    ///
    /// Implementation note: QuestPDF exposes a single <c>page.Foreground()</c>
    /// layer that draws above content but does NOT cover the page header /
    /// footer. We render the watermark there, with <c>PaddingTop</c> set to
    /// 0 so it sits flush against the top edge — clearly visible, impossible
    /// to crop out.
    /// </summary>
    private static void ComposeDraftWatermark(IContainer container)
    {
        container
            .Padding(0)
            .Background(Palette.Page)
            .BorderBottom(1)
            .BorderColor(Palette.Gold)
            .PaddingVertical(6)
            .AlignCenter()
            .Text(T.DraftWatermark)
            .FontFamily(AmiriBold)
            .FontSize(14)
            .Bold()
            .FontColor(Palette.Muted)
            .LetterSpacing(0.5f);
    }

    // ─── Sections ────────────────────────────────────────────────────────────

    /// <summary>
    /// BUG-6 FIX — Meta card: clean 2-column RTL grid.
    /// Status badge is now on the LEFT (visual end in RTL); rubric version
    /// on the RIGHT (visual start in RTL). No overlap.
    /// Row order: school | teacher, subject | class, lesson | attendance,
    /// moderator | visit type, sequence | visit date, submitted | approved,
    /// approved-by | supervisor notes.
    /// </summary>
    private void ComposeMetaCard(IContainer container, VisitReportDto dto)
    {
        container.Border(1).BorderColor(Palette.Border).Padding(12).Column(col =>
        {
            col.Spacing(8);

            // Top row: rubric version (RIGHT = RTL start) | status badge (LEFT = RTL end)
            col.Item().Row(row =>
            {
                // BUG-6 FIX: status badge on the LEFT (visual end) — in RTL,
                // the "approved" badge reads naturally from the left edge.
                row.AutoItem()
                    .Background(Palette.BrandGreen).PaddingVertical(4).PaddingHorizontal(8)
                    .AlignCenter().Text(T.StatusApproved)
                    .FontFamily(AmiriBold).FontSize(10).Bold().FontColor(Palette.White);

                row.RelativeItem(); // spacer

                // Rubric version label on the right (RTL start)
                row.AutoItem().AlignRight().Text(t =>
                {
                    t.Span(T.LabelRubricVersion + ": " + dto.RubricVersionNumber)
                     .FontColor(Palette.Muted).FontSize(9);
                });
            });

            // 2-column info grid — strictly ordered per spec
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn();
                    c.RelativeColumn();
                });

                // Row 1: School | Teacher
                table.Cell().AlignRight().Element(c => MetaCell(c, T.LabelSchool, dto.SchoolName));
                table.Cell().AlignRight().Element(c => MetaCell(c, T.LabelInstructor, dto.InstructorFullName));

                // Row 2: Subject | Class
                table.Cell().AlignRight().Element(c => MetaCell(c, T.LabelSubject, dto.Subject ?? "—"));
                table.Cell().AlignRight().Element(c => MetaCell(c, T.LabelGradeClass, dto.GradeClass ?? "—"));

                // Row 3: Lesson title | attendance
                table.Cell().AlignRight().Element(c => MetaCell(c, T.LabelLessonTitle, dto.LessonTitle ?? "—"));
                table.Cell().AlignRight().Element(c => MetaCell(
                    c,
                    $"{T.LabelPresentCount} / {T.LabelAbsentCount}",
                    $"{dto.PresentCount} / {dto.AbsentCount}"));

                // Row 4: Moderator | Visit Type
                table.Cell().AlignRight().Element(c => MetaCell(c, T.LabelModerator, dto.CreatedByFullName));
                table.Cell().AlignRight().Element(c => MetaCell(c, T.LabelCategory, dto.VisitCategoryLabelAr));

                // Row 5: Sequence | Visit Date
                table.Cell().AlignRight().Element(c => MetaCell(c, T.LabelSequence, dto.VisitSequenceLabelAr));
                table.Cell().AlignRight().Element(c => MetaCell(c, T.LabelVisitDate, dto.VisitDate.ToString("yyyy-MM-dd")));

                // Row 6: Submitted Date | Approved Date
                table.Cell().AlignRight().Element(c => MetaCell(c, T.LabelSubmittedAt, dto.SubmittedAt?.ToString("yyyy-MM-dd HH:mm") ?? "—"));
                table.Cell().AlignRight().Element(c => MetaCell(c, T.LabelApprovedAt, dto.ApprovedAt?.ToString("yyyy-MM-dd HH:mm") ?? "—"));

                // Row 7: Approved By | supervisor notes
                table.Cell().AlignRight().Element(c => MetaCell(c, T.LabelApprovedBy, string.IsNullOrWhiteSpace(dto.ApprovedByFullName) ? "—" : dto.ApprovedByFullName));
                table.Cell().AlignRight().Element(c => MetaCell(c, T.LabelNotes, string.IsNullOrWhiteSpace(dto.Notes) ? "—" : dto.Notes));
            });
        });
    }

    private static void MetaCell(IContainer container, string label, string value)
    {
        container.PaddingBottom(6).Text(t =>
        {
            t.Span(label + ": ").FontColor(Palette.Muted).FontSize(10);
            t.Span(value).FontFamily(AmiriBold).Bold().FontSize(10).FontColor(Palette.Text);
        });
    }

    private void ComposeStandardsCard(IContainer container, VisitReportDto dto)
    {
        container.Column(col =>
        {
            col.Item().AlignRight().Text(T.SectionStandards)
                .FontFamily(AmiriBold).FontSize(13).Bold().FontColor(Palette.BrandGreenText);

            foreach (var d in dto.Domains)
            {
                col.Item().PaddingTop(8).Element(c => ComposeDomainBlock(c, d));
            }
        });
    }

    private void ComposeDomainBlock(IContainer container, ReportDomainBlockDto block)
    {
        container.Border(1).BorderColor(Palette.Border).Column(col =>
        {
            // Domain header — RTL: [domain average right-aligned] [domain name center] [domain code LEFT=start in LTR row]
            // Physical row: [code chip LEFT] [name CENTER expanding] [average RIGHT]
            // Visually in RTL: average reads first (right), code chip last (left).
            col.Item().Background(Palette.Page).Padding(8).Row(row =>
            {
                // Code chip — left side (physical) = RTL end
                row.ConstantItem(56).AlignCenter().Background(Palette.Gold)
                    .PaddingVertical(4).PaddingHorizontal(8)
                    .Text(block.DomainCode)
                    .FontFamily(AmiriBold).FontSize(11).Bold().FontColor(Palette.Text);

                // Domain name — expanding, right-aligned
                row.RelativeItem().AlignRight().PaddingRight(8)
                    .Text(block.DomainNameAr)
                    .FontFamily(AmiriBold).FontSize(11).Bold().FontColor(Palette.BrandGreenText);

                // Domain average — compact, right side (physical) = RTL start
                row.ConstantItem(80).AlignCenter().Text(t =>
                {
                    t.Span(T.LabelDomainAvgPrefix + ": ").FontColor(Palette.Muted).FontSize(9);
                    t.Span(block.AverageScore.ToString("0.000"))
                     .FontFamily(AmiriBold).Bold().FontColor(Palette.BrandGreen).FontSize(9);
                });
            });

            // BUG-4 FIX — Standards table: 3 columns, tighter badge cell.
            // Physical order (L→R): [score pill 90pt] [standard text expanding] [code chip 48pt]
            // In RTL reading: code (right) → text → score pill (left/end)
            col.Item().Padding(8).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(90);  // score pill (visual left = RTL end)
                    c.RelativeColumn();    // standard text (expands, right-aligned)
                    c.ConstantColumn(34);  // row number (visual right = RTL start)
                });

                for (var standardIndex = 0; standardIndex < block.Standards.Count; standardIndex++)
                {
                    var std = block.Standards[standardIndex];
                    var scoreColor = ScoreColor(std.Score);

                    // Score pill — LEFT column (physical) = RTL end
                    table.Cell().AlignCenter().AlignMiddle()
                        .Element(c => ComposeScorePill(c, std.Score, std.ScoreLabelAr, scoreColor));

                    // Standard text — MIDDLE column, right-aligned RTL
                    table.Cell().AlignRight().AlignMiddle().PaddingVertical(4).Column(sc =>
                    {
                        sc.Item().AlignRight().Text(std.StandardTextAr)
                            .FontSize(10).FontColor(Palette.Text);
                        if (!string.IsNullOrWhiteSpace(std.EvidenceNote))
                        {
                            sc.Item().AlignRight().PaddingTop(2).Text(std.EvidenceNote!)
                                .FontSize(8).Italic().FontColor(Palette.Muted);
                        }
                    });

                    // Code chip — RIGHT column (physical) = RTL start
                    table.Cell().AlignCenter().AlignMiddle().Text((standardIndex + 1).ToString())
                        .FontFamily(AmiriBold).FontSize(9).Bold().FontColor(Palette.Muted);
                }
            });
        });
    }

    /// <summary>
    /// BUG-4 FIX — Score pill: fixed 26×26 badge, semantic color,
    /// Arabic label to the right of the badge (in RTL context, label reads first).
    ///
    /// Physical row (L→R): [badge 26×26] [label text]
    /// In RTL reading order: label (right = RTL start) → badge (left = RTL end)
    /// This means the reader sees the performance label first, then the number.
    /// </summary>
    private static void ComposeScorePill(IContainer container, int? score, string label, string color)
    {
        container.AlignMiddle().Row(row =>
        {
            // Score number badge — fixed 26×26, rounded corners via border+radius
            row.ConstantItem(26).Height(26)
                .Background(color)
                .Border(0)
                .AlignCenter().AlignMiddle()
                .Text(score?.ToString() ?? "—")
                .FontFamily(AmiriBold).FontSize(10).Bold().FontColor(Palette.White);

            // Performance label — to the right in RTL reading
            row.RelativeItem().PaddingRight(4).AlignRight().AlignMiddle()
                .Text(label)
                .FontSize(8).FontColor(color);
        });
    }

    private void ComposeAnalysisCard(IContainer container, VisitReportDto dto)
    {
        container.Column(col =>
        {
            col.Item().AlignRight().Text(T.SectionAnalysis)
                .FontFamily(AmiriBold).FontSize(13).Bold().FontColor(Palette.BrandGreenText);

            // Summary row: overall + performance level (RTL → level first visually)
            col.Item().PaddingTop(6).Row(row =>
            {
                row.RelativeItem().AlignRight().Element(c => ComposeSummaryCell(c,
                    T.LabelPerformance, dto.PerformanceLevelAr, ScoreColorForLevel(dto.PerformanceLevelAr)));
                row.ConstantItem(12);
                row.RelativeItem().AlignRight().Element(c => ComposeSummaryCell(c,
                    T.LabelOverallScore, dto.OverallScore.ToString("0.000"), Palette.BrandGreen));
            });

            // BUG-3 FIX — Domain averages: clean RTL row of 5 compact cards.
            // Each card: [domain code] / [domain name] / [average score]
            // No stray "الاسم"/"المتوسط" header row cells floating.
            if (dto.Domains.Count > 0)
            {
                col.Item().PaddingTop(8).AlignRight().Text(T.SectionDomainAvg)
                    .FontFamily(AmiriBold).FontSize(11).Bold().FontColor(Palette.BrandGreenText);

                col.Item().PaddingTop(6).Row(row =>
                {
                    // Render 5 compact cards in a row (equal widths via RelativeItem).
                    // Physical order: D1 ... D5 left-to-right.
                    // In RTL context the first domain (D1) will appear on the right.
                    // To present D1 on the right (RTL start) we iterate in normal order.
                    foreach (var d in dto.Domains)
                    {
                        row.RelativeItem().Element(c => ComposeDomainAvgCard(c, d));
                        // Small gap between cards (except after last)
                        if (d != dto.Domains[^1])
                            row.ConstantItem(4);
                    }
                });
            }
        });
    }

    /// <summary>
    /// BUG-3 FIX — Individual domain average card: compact, vertically centered,
    /// shows [code] / [name] / [average] stacked. No floating label row.
    /// </summary>
    private static void ComposeDomainAvgCard(IContainer container, ReportDomainBlockDto domain)
    {
        var scoreColor = ScoreColorForLevelNumeric(domain.AverageScore);

        container
            .Border(1).BorderColor(Palette.Border)
            .Background(Palette.Page)
            .Padding(6)
            .Column(col =>
            {
                col.Spacing(2);

                // Domain code (e.g. D1) — centered, gold
                col.Item().AlignCenter().Text(domain.DomainCode)
                    .FontFamily(AmiriBold).FontSize(10).Bold().FontColor(Palette.Gold);

                // Domain name — centered, small, truncated if needed
                col.Item().AlignCenter().Text(domain.DomainNameAr)
                    .FontSize(8).FontColor(Palette.Muted);

                // Average score — prominent, colored by score band
                col.Item().AlignCenter().PaddingTop(2).Text(domain.AverageScore.ToString("0.000"))
                    .FontFamily(AmiriBold).FontSize(11).Bold().FontColor(scoreColor);
            });
    }

    private static void ComposeSummaryCell(IContainer container, string label, string value, string scoreColor)
    {
        container.Border(1).BorderColor(Palette.Border).Padding(10).AlignRight().Column(col =>
        {
            col.Item().AlignRight().Text(label).FontSize(9).FontColor(Palette.Muted);
            col.Item().AlignRight().PaddingTop(4).Text(value)
                .FontFamily(AmiriBold).FontSize(22).Bold().FontColor(scoreColor);
        });
    }

    private void ComposeStrengthsBlock(IContainer container, VisitReportDto dto)
    {
        container.Column(col =>
        {
            col.Item().AlignRight().Text(T.SectionStrengths)
                .FontFamily(AmiriBold).FontSize(13).Bold().FontColor(Palette.BrandGreen);
            foreach (var s in dto.Strengths)
            {
                col.Item().PaddingTop(4).AlignRight().Text(t =>
                {
                    t.Span("✓ ").FontSize(10);
                    t.Span(s.DomainCode + "  ").FontFamily(AmiriBold).Bold().FontColor(Palette.Gold).FontSize(10);
                    t.Span(s.DomainNameAr + "  ").FontColor(Palette.Text).FontSize(10);
                    t.Span(s.AverageScore.ToString("0.000")).FontFamily(AmiriBold).Bold().FontColor(Palette.BrandGreen).FontSize(10);
                });
            }
        });
    }

    private void ComposeImprovementsBlock(IContainer container, VisitReportDto dto)
    {
        container.Column(col =>
        {
            col.Item().AlignRight().Text(T.SectionImprovements)
                .FontFamily(AmiriBold).FontSize(13).Bold().FontColor(Palette.Gold);
            foreach (var i in dto.ImprovementAreas)
            {
                col.Item().PaddingTop(4).AlignRight().Text(t =>
                {
                    t.Span("!  ").FontSize(10);
                    t.Span(i.DomainCode + "  ").FontFamily(AmiriBold).Bold().FontColor(Palette.Gold).FontSize(10);
                    t.Span(i.DomainNameAr + "  ").FontColor(Palette.Text).FontSize(10);
                    t.Span(i.AverageScore.ToString("0.000")).FontFamily(AmiriBold).Bold().FontColor(Palette.Gold).FontSize(10);
                });
            }
        });
    }

    private void ComposePrioritiesBlock(IContainer container, VisitReportDto dto)
    {
        container.Column(col =>
        {
            col.Item().AlignRight().Text(T.SectionPriorities)
                .FontFamily(AmiriBold).FontSize(13).Bold().FontColor(Palette.ScoreLow);
            foreach (var p in dto.PriorityStandards)
            {
                col.Item().PaddingTop(4).AlignRight().Text(t =>
                {
                    t.Span("▲ ").FontSize(10);
                    t.Span(p.StandardCode + "  ").FontFamily(AmiriBold).Bold().FontColor(Palette.ScoreLow).FontSize(10);
                    t.Span(p.StandardTextAr + "  ").FontColor(Palette.Text).FontSize(10);
                    t.Span(p.Score.ToString()).FontFamily(AmiriBold).Bold().FontColor(Palette.ScoreLow).FontSize(10);
                });
            }
        });
    }

    private static void ComposeRecommendationsBlock(IContainer container, VisitReportDto dto)
    {
        container.PaddingTop(8).Column(col =>
        {
            col.Item().AlignRight().Text(T.SectionRecommendations)
                .FontFamily(AmiriBold).FontSize(13).Bold().FontColor(Palette.BrandGreen);
            foreach (var recommendation in dto.Recommendations)
            {
                col.Item().PaddingTop(4).AlignRight().Text($"• {recommendation}")
                    .FontFamily(AmiriRegular).FontSize(10).FontColor(Palette.Text);
            }
        });
    }

    private static void ComposeFollowUpsBlock(IContainer container, VisitReportDto dto)
    {
        container.PaddingTop(8).Column(col =>
        {
            col.Item().AlignRight().Text(T.SectionFollowUps)
                .FontFamily(AmiriBold).FontSize(13).Bold().FontColor(Palette.BrandGreenText);

            foreach (var followUp in dto.PlanFollowUps)
            {
                col.Item().PaddingTop(6)
                    .Border(1).BorderColor(Palette.Border)
                    .Background(Palette.Page).Padding(8)
                    .Column(card =>
                    {
                        card.Item().AlignRight().Text(t =>
                        {
                            t.Span(followUp.FollowDate.ToString("yyyy/MM/dd") + "  ")
                                .FontFamily(AmiriBold).Bold().FontColor(Palette.BrandGreen);
                            if (followUp.ProgressScore.HasValue)
                                t.Span($"({followUp.ProgressScore.Value}%)").FontColor(Palette.Gold);
                        });
                        card.Item().AlignRight().PaddingTop(3).Text(followUp.ProgressNote)
                            .FontFamily(AmiriRegular).FontSize(10).FontColor(Palette.Text);
                        if (!string.IsNullOrWhiteSpace(followUp.EvidenceNote))
                            card.Item().AlignRight().PaddingTop(2).Text(followUp.EvidenceNote!)
                                .FontFamily(AmiriRegular).FontSize(8).FontColor(Palette.Muted);
                    });
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
        container.PaddingTop(8).Column(col =>
        {
            col.Item().AlignRight().Text(T.SectionSignatures)
                .FontFamily(AmiriBold).FontSize(13).Bold().FontColor(Palette.BrandGreenText);

            col.Item().PaddingTop(8).Row(row =>
            {
                if (dto.ShowModeratorSignature)
                    row.RelativeItem().Element(c => SignatureBox(c, dto, SignatureParty.Moderator));
                else
                    row.RelativeItem();

                row.ConstantItem(10);

                row.RelativeItem().Element(c => SignatureBox(c, dto, SignatureParty.Instructor));

                row.ConstantItem(10);

                if (dto.ShowManagerSignature)
                    row.RelativeItem().Element(c => SignatureBox(c, dto, SignatureParty.Manager));
                else
                    row.RelativeItem();
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

        var dateText = party == SignatureParty.Manager && dto.ApprovedAt.HasValue
            ? $"التاريخ: {dto.ApprovedAt.Value:yyyy-MM-dd}"
            : "التاريخ: ________________";

        container.Border(1).BorderColor(DynamicPalette.BrandGreenText).Padding(10).Column(col =>
        {
            // Section label
            col.Item().AlignRight().Text(label)
                .FontFamily(AmiriBold).FontSize(10).Bold().FontColor(Palette.Muted);

            // Signature image area — fixed 60pt height so both columns stay aligned.
            // BUG-2 FIX: if bytes are present → render real image.
            //             if bytes are null  → render blank dashed line only.
            //             NEVER any hard-coded "S. Manager" text.
            col.Item().PaddingTop(6).Height(60).AlignCenter().AlignMiddle()
                .Element(c => RenderSignatureImage(c, imageBytes));

            // Printed name from visit data (always shown, never hard-coded)
            if (!string.IsNullOrWhiteSpace(printedName))
            {
                col.Item().PaddingTop(6).AlignCenter()
                    .Text(printedName)
                    .FontFamily(AmiriBold).FontSize(10).Bold().FontColor(Palette.Text);
            }

            // Date line
            col.Item().PaddingTop(8).BorderTop(1).BorderColor(Palette.Muted)
                .AlignCenter().PaddingTop(4).Text(t =>
                {
                    t.Span(dateText).FontFamily(AmiriRegular).FontColor(Palette.Muted).FontSize(8);
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
                // FitArea keeps the image inside the 60-pt signature cell without
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
        container.AlignBottom().PaddingHorizontal(10).LineHorizontal(1).LineColor(Palette.Muted);
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
        var year     = visitDate.Year.ToString();
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
        var dateStr = generatedAt.ToString("yyyy-MM-dd");
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
