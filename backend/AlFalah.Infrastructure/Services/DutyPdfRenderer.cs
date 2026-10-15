using System.Globalization;
using System.Text.Json;
using AlFalah.Application.Duty;
using AlFalah.Domain.Entities.Duty;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AlFalah.Infrastructure.Services;

/// <summary>Official source 1/4/5 forms. Never resolves a mutable asset or account.</summary>
public sealed partial class DutyPdfRenderer : IDutyPdfRenderer
{
    public string Version => "school-duty/official-2026-v2";
    private const string Ink = "#172C25", Rule = "#7C8D83", Green = "#174B36", Tint = "#EDF3EF";
    private static readonly string[] Days = ["الأحد", "الاثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت"];
    private static string S(JsonElement e, string property) => e.TryGetProperty(property, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : "";
    private static IContainer Cell(IContainer c, bool heading = false) => c.Border(.5f).BorderColor(Rule)
        .Background(heading ? Tint : "#FFFFFF").PaddingHorizontal(5).PaddingVertical(1.5f).DefaultTextStyle(t=>t.LineHeight(1.2f)).AlignMiddle().AlignCenter();
    private static void TextCell(IContainer c, string value, bool heading = false) => Cell(c, heading).Text(value).FontSize(9);
    private static void Header(IContainer c, DutyExecutionHeader h, byte[]? logo)
    {
        c.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().AlignRight().Text(h.Header ?? "").FontSize(10).FontColor(Ink);
                row.ConstantItem(82).Height(44).PaddingHorizontal(12).Element(x =>
                {
                    if (logo is not null) x.Image(logo).FitArea();
                });
                row.RelativeItem().AlignRight().Text($"المدرسة: {h.School}\n{h.City}").FontSize(11);
            });
            col.Item().PaddingTop(10).LineHorizontal(1.2f).LineColor(Green);
        });
    }
    private static byte[]? Logo(JsonElement s)
    {
        var logo = s.GetProperty("Logo");
        return S(logo,"State") == "Frozen" ? Convert.FromBase64String(S(logo,"Data").Split(',').Last()) : null;
    }
    private static void Frame(PageDescriptor p, DutyExecutionHeader h, byte[]? logo)
    {
        p.Size(PageSizes.A4); p.MarginHorizontal(34); p.MarginVertical(28); p.ContentFromRightToLeft();
        p.DefaultTextStyle(t => t.FontFamily(PdfTheme.Font).FontSize(11).FontColor(Ink).DirectionFromRightToLeft());
        p.Header().Element(c => Header(c,h,logo));
        p.Footer().PaddingTop(8).BorderTop(.5f).BorderColor(Rule).Row(row =>
        {
            row.RelativeItem().Text(h.Footer ?? "").FontSize(8);
            row.AutoItem().ContentFromLeftToRight().DefaultTextStyle(t=>t.DirectionFromLeftToRight()).Text(t => { t.CurrentPageNumber(); t.Span(" / "); t.TotalPages(); });
            row.AutoItem().PaddingRight(5).Text("صفحة");
        });
    }
    public DutyPdf Signed(DutySignedDocument document)
    {
        PdfTheme.EnsureFonts(); var s = DutyDocumentIntegrity.Validate(document);
        var header = s.GetProperty("Header").Deserialize<DutyExecutionHeader>() ?? throw DutyDocumentIntegrity.Error();
        var localDate = DateOnly.Parse(s.GetProperty("Date").GetString()!,CultureInfo.InvariantCulture);
        var pdf = Document.Create(root => root.Page(p =>
        {
            Frame(p,header,Logo(s));
            p.Content().PaddingTop(16).Element(c =>
            {
                if (document.Kind == "Letter") Letter(c,header,localDate,s,document.SignatureBytes);
                else Report(c,header,localDate,s,document.SignatureBytes);
            });
        })).GeneratePdf();
        var name = $"{(document.Kind == "Letter" ? "خطاب المناوبة" : "تقرير المناوبة")} - {DutyDocumentIntegrity.SafeName(header.ActualTeacher)} - {localDate:yyyy-MM-dd}.pdf";
        return new(pdf,name,DutyDocumentIntegrity.Hash(pdf),Version);
    }
    private static void Letter(IContainer c, DutyExecutionHeader h, DateOnly date, JsonElement s, byte[] signature)
    {
        c.Column(col =>
        {
            col.Spacing(20);
            col.Item().PaddingTop(12).Text($"المكرم الأستاذ: {h.ActualTeacher}                         وفقه الله").FontSize(14);
            col.Item().AlignCenter().Text("السلام عليكم ورحمة الله وبركاته").FontSize(15);
            col.Item().DefaultTextStyle(t=>t.FontSize(15).LineHeight(1.7f)).Text(t =>
            {
                t.Span($"نذكركم بأنه لديكم اليوم: {Days[(int)date.DayOfWeek]}  الموافق: ");
                t.Span("\u2066"+date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)+"\u2069").DirectionFromLeftToRight();
                t.Span($" مناوبة الإشراف على الطلاب حتى انصراف آخر طالب من المدرسة؛ لذا نأمل {S(s,"Obligation")}.");
            });
            col.Item().AlignCenter().Text("تقبلوا فائق تحياتي وتقديري").FontSize(14);
            col.Item().PaddingTop(12).AlignLeft().Width(220).Column(manager =>
            {
                manager.Item().AlignCenter().Text("مدير المدرسة").FontSize(14);
                manager.Item().AlignCenter().Text(h.Manager).FontSize(14);
            });
            col.Item().PaddingTop(24).ShowEntire().Column(teacher =>
            {
                teacher.Item().AlignCenter().Text("توقيع المعلم بالعلم والالتزام").FontSize(15).Bold();
                teacher.Item().PaddingTop(18).Row(row =>
                {
                    row.RelativeItem().Text($"اسم المعلم: {h.ActualTeacher}").FontSize(13);
                    row.ConstantItem(190).Column(sig =>
                    {
                        sig.Item().Text("التوقيع:");
                        sig.Item().Height(58).Image(signature).FitArea();
                    });
                });
            });
        });
    }
    private static void Title(IContainer c, DutyExecutionHeader h, string title, string code)
    {
        c.Table(t =>
        {
            t.ColumnsDefinition(d => { d.RelativeColumn(3); d.RelativeColumn(2); });
            Cell(t.Cell(),true).Text(title).FontSize(17).Bold();
            Cell(t.Cell(),true).Text(code).FontSize(12).DirectionFromLeftToRight();
            TextCell(t.Cell(),$"العام الدراسي: {h.Year}"); TextCell(t.Cell(),$"الفصل الدراسي: {Term(h.Semester)}");
        });
    }
    private static string Term(int n) => n switch {1=>"الأول",2=>"الثاني",3=>"الثالث",_=>n.ToString(CultureInfo.InvariantCulture)};
    private static void Report(IContainer c, DutyExecutionHeader h, DateOnly date, JsonElement s, byte[] signature)
    {
        var payload = s.GetProperty("Payload"); var content = payload.GetProperty("Content").Deserialize<DutyReportContent>()!;
        var late = payload.GetProperty("LateStudents").EnumerateArray().Select(x => new Line(S(x,"StudentName"),S(x,"Classroom"),S(x,"ObservedAt"),S(x,"Action"),S(x,"Notes"))).ToArray();
        var violations = payload.GetProperty("Violations").EnumerateArray().Select(x => new Line(S(x.GetProperty("Student"),"Name"),S(x.GetProperty("Student"),"Classroom"),S(x.GetProperty("Line"),"Time"),S(x.GetProperty("Line"),"Action"),S(x.GetProperty("Line"),"Notes"))).ToArray();
        c.Column(col =>
        {
            col.Item().ShowEntire().Column(identity =>
            {
                identity.Item().Element(x => Title(x,h,"تقرير المناوبة اليومي","QF71-B-2-3"));
                identity.Item().Table(t =>
                {
                    t.ColumnsDefinition(d => { d.RelativeColumn(1.1f); d.RelativeColumn(1.15f); d.RelativeColumn(2.5f); d.RelativeColumn(1.3f); d.RelativeColumn(1.7f); });
                    foreach (var label in new[]{"مناوبة دوام يوم","التاريخ","اسم الموظف المناوب","التوقيع","ملاحظات"}) TextCell(t.Cell(),label,true);
                    TextCell(t.Cell(),Days[(int)date.DayOfWeek]); TextCell(t.Cell(),date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)); TextCell(t.Cell(),h.OriginalTeacher);
                    Cell(t.Cell()).Height(30).Element(x => { if (!h.IsSubstitute) x.Image(signature).FitArea(); }); TextCell(t.Cell(),content.DutyNotes);
                    TextCell(t.Cell().ColumnSpan(3),"بدلاً عنه الأستاذ",true); TextCell(t.Cell(),"التوقيع",true); TextCell(t.Cell(),"ملاحظات",true);
                    TextCell(t.Cell().ColumnSpan(3),h.IsSubstitute ? h.ActualTeacher : "—");
                    Cell(t.Cell()).Height(30).Element(x => { if (h.IsSubstitute) x.Image(signature).FitArea(); }); TextCell(t.Cell(),content.SubstituteNotes);
                });
            });
            col.Item().Element(x => StudentTable(x,"أسماء الطلاب المتأخرين",late));
            col.Item().Element(x => StudentTable(x,"أسماء الطلاب المخالفين",violations));
            col.Item().PaddingTop(8).Column(obs =>
            {
                obs.Item().EnsureSpace(50).Background(Tint).Padding(6).Text("الملاحظات:").Bold();
                obs.Item().Padding(4).MinHeight(30).Text(string.IsNullOrWhiteSpace(content.Observations) ? "لا توجد ملاحظات عامة." : content.Observations).FontSize(10).LineHeight(1.3f);
            });
            col.Item().ShowEntire().Column(end =>
            {
                end.Item().Border(.5f).BorderColor(Rule).Padding(5).Text($"وقت مغادرة آخر طالب: {content.LastDeparture:HH:mm}").Bold();
                end.Item().PaddingTop(6).Border(.5f).BorderColor(Rule).Padding(5).Text(S(s,"Handover")).FontSize(10);
                end.Item().PaddingTop(8).Row(row =>
                {
                    row.RelativeItem().Text("وكيل الشؤون التعليمية: ____________________").FontSize(10);
                    row.RelativeItem().AlignLeft().Text("التوقيع: ____________________").FontSize(10);
                });
            });
        });
    }
    private sealed record Line(string Name,string Class,string Time,string Action,string Notes);
    private static void StudentTable(IContainer c, string title, IReadOnlyList<Line> rows)
    {
        c.Table(t =>
        {
            t.ColumnsDefinition(d => { d.ConstantColumn(23); d.RelativeColumn(2.1f); d.RelativeColumn(1); d.ConstantColumn(43); d.RelativeColumn(2); d.RelativeColumn(1.7f); });
            t.Header(header =>
            {
                Cell(header.Cell().ColumnSpan(6),true).Text(title).FontSize(11).Bold();
                foreach(var label in new[]{"م","الاسم","الفصل","الزمن","الإجراء المتخذ","ملاحظات"}) TextCell(header.Cell(),label,true);
            });
            for(var i=0;i<Math.Max(rows.Count,7);i++)
            {
                var empty = rows.Count==0&&i==0 ? title.Contains("المتأخرين") ? "لا يوجد طلاب متأخرون." : "لا يوجد طلاب مخالفون." : "";
                var line = i<rows.Count ? rows[i] : new Line(empty,"","","","");
                TextCell(t.Cell(),(i+1).ToString(CultureInfo.InvariantCulture));
                Cell(t.Cell()).MinHeight(14).AlignRight().Text(line.Name).FontSize(9);
                TextCell(t.Cell(),line.Class); TextCell(t.Cell(),line.Time.Length>=5 ? line.Time[..5] : line.Time);
                Cell(t.Cell()).AlignRight().Text(line.Action).FontSize(9); Cell(t.Cell()).AlignRight().Text(line.Notes).FontSize(9);
            }
        });
    }
}
