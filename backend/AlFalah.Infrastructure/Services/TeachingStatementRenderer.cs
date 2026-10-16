using AlFalah.Application.IntelligentTimetable;
using QuestPDF.Drawing.Exceptions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AlFalah.Infrastructure.Services;

public sealed class TeachingStatementRenderer : ITeachingStatementRenderer
{
    public string Version => "assignments/source2-v1";
    private const string Ink="#171717", Rule="#666666";
    private static readonly string[] Ordinals=["","الأول","الثاني","الثالث","الرابع","الخامس","السادس"];
    public byte[] Render(TeachingStatementSnapshot snapshot)
    {
        PdfTheme.EnsureFonts();
        foreach(var rows in new[]{24,18,12,8,4,1})
        {
            try {return Build(snapshot,rows);}
            catch(DocumentLayoutException) when(rows>1) { }
        }
        throw new InvalidOperationException("Cannot lay out assignment statement.");
    }
    private static byte[] Build(TeachingStatementSnapshot snapshot,int rowCount)
    {
        var grades=snapshot.Grades.Select(g=>(g.Stage,g.Grade)).ToArray();
        if(grades.Length==0)grades=[(2,1),(2,2),(2,3)];
        var stages=grades.Select(g=>g.Stage).Distinct().Count();
        // A numbered appendix carries exceptional dense cells without shrinking Arabic type.
        var details=snapshot.Teachers.SelectMany((teacher,index)=>teacher.Allocations.Select(a=>(Number:index+1,Teacher:teacher,Allocation:a)))
            .Where(x=>x.Allocation.Category=="Other" || Dense(x.Teacher.Allocations.Where(a=>SameColumn(a,x.Allocation)))).ToArray();
        var logoPath=Path.Combine(AppContext.BaseDirectory,"Assets","Logo.png");
        var logo=System.IO.File.Exists(logoPath)?System.IO.File.ReadAllBytes(logoPath):null;
        var document=Document.Create(doc=>
        {
            var offset=0;
            foreach(var chunk in snapshot.Teachers.Chunk(rowCount))
            {
                var start=offset;offset+=chunk.Length;
                doc.Page(page=>
                {
                    Setup(page,snapshot,logo);
                    page.Content().ShowEntire().Table(table=>
                    {
                        table.ColumnsDefinition(c=>{c.ConstantColumn(22);c.ConstantColumn(115);c.ConstantColumn(70);
                            foreach(var _ in grades)c.RelativeColumn();c.ConstantColumn(36);
                            c.RelativeColumn();c.RelativeColumn();c.RelativeColumn();c.ConstantColumn(36);});
                        table.Header(header=>
                        {
                            Cell(header.Cell().ColumnSpan((uint)(grades.Length+8))).Text(text=>
                            {
                                text.Span("إسنادات المواد للمعلمين لعام ");
                                var year=snapshot.Header.Year.StartsWith("العام الدراسي",StringComparison.Ordinal)
                                    ? snapshot.Header.Year["العام الدراسي".Length..].Trim(' ',':','：') : snapshot.Header.Year;
                                text.Span("\u2066"+year+"\u2069").DirectionFromLeftToRight();
                                text.DefaultTextStyle(x=>x.Bold().FontSize(11));
                            });
                            Cell(header.Cell().RowSpan(2)).Text("م");Cell(header.Cell().RowSpan(2)).Text("اسم المعلم");Cell(header.Cell().RowSpan(2)).Text("تخصص المعلم");
                            Cell(header.Cell().ColumnSpan((uint)grades.Length)).Text("إسنادات مواد التخصص للمعلمين");
                            Cell(header.Cell().RowSpan(2)).Text("نصاب حصص التخصص");
                            Cell(header.Cell().ColumnSpan(3)).Text("إسنادات مواد غير التخصص للمعلمين");
                            Cell(header.Cell().RowSpan(2)).Text("نصاب الحصص للمعلم");
                            foreach(var g in grades)Cell(header.Cell()).Text($"الصف {Grade(g.Grade)}"+(stages>1?"\n"+TeachingStatementService.StageLabel(g.Stage):""));
                            foreach(var label in new[]{"مهارات حياتية","تفكير ناقد","النشاط"})Cell(header.Cell()).Text(label);
                        });
                        foreach(var (teacher,index) in chunk.Select((t,i)=>(t,i)))
                        {
                            Cell(table.Cell()).MinHeight(13).Text((start+index+1).ToString());
                            Cell(table.Cell()).Text(teacher.Name);Cell(table.Cell()).Text(teacher.Specialization);
                            foreach(var g in grades)AllocationCell(table.Cell(),teacher.Allocations.Where(a=>a.Category=="Specialization" && a.Stage==g.Stage && a.Grade==g.Grade),start+index+1);
                            Cell(table.Cell()).Text(teacher.Allocations.Where(a=>a.Category=="Specialization").Sum(a=>a.Periods).ToString());
                            foreach(var category in new[]{"LifeSkills","CriticalThinking","Activity"})AllocationCell(table.Cell(),teacher.Allocations.Where(a=>a.Category==category),start+index+1);
                            Cell(table.Cell()).Text(teacher.Allocations.Sum(a=>a.Periods)+(teacher.Allocations.Any(a=>a.Category=="Other")?" *":""));
                        }
                    });
                });
            }
            if(details.Length>0)doc.Page(page=>
            {
                Setup(page,snapshot,logo);
                page.Content().Column(column=>
                {
                    column.Item().PaddingBottom(6).Text("ملحق المواد الإضافية وتفاصيل الخانات الممتدة").FontSize(12).Bold();
                    column.Item().Table(table=>
                    {
                        table.ColumnsDefinition(c=>{c.ConstantColumn(24);c.RelativeColumn(2);c.RelativeColumn(2);c.RelativeColumn(2);c.RelativeColumn();c.ConstantColumn(40);c.ConstantColumn(40);});
                        table.Header(h=>{foreach(var label in new[]{"م","اسم المعلم","المادة","الفصل","نوع الإسناد","الحصص","الأزواج"})Cell(h.Cell()).Text(label).Bold();});
                        foreach(var d in details)
                        {
                            Cell(table.Cell()).ShowEntire().Text(d.Number.ToString());Cell(table.Cell()).ShowEntire().Text(d.Teacher.Name);
                            Cell(table.Cell()).ShowEntire().Text(d.Allocation.Subject);Cell(table.Cell()).ShowEntire().Text(d.Allocation.Classroom);
                            Cell(table.Cell()).ShowEntire().Text(Mode(d.Allocation.Mode));Cell(table.Cell()).ShowEntire().Text(d.Allocation.Periods.ToString());Cell(table.Cell()).ShowEntire().Text(d.Allocation.Pairs.ToString());
                        }
                    });
                });
            });
        });
        return document.GeneratePdf();
    }
    private static void Setup(PageDescriptor page,TeachingStatementSnapshot snapshot,byte[]? logo)
    {
        page.Size(PageSizes.A4.Landscape());page.MarginHorizontal(22);page.MarginVertical(18);page.ContentFromRightToLeft();
        page.DefaultTextStyle(x=>x.FontFamily(PdfTheme.Font).FontSize(8.5f).FontColor(Ink).DirectionFromRightToLeft().LineHeight(1.15f));
        page.Header().PaddingBottom(5).Column(column=>
        {
            column.Item().MinHeight(50).Row(row=>
            {
                row.RelativeItem(2).AlignRight().Text(snapshot.Header.Header ?? snapshot.Header.School).FontSize(9.5f);
                row.RelativeItem().AlignCenter().Text("إسنادات التدريس\n"+snapshot.Header.School).FontSize(11).Bold();
                row.ConstantItem(85).Height(45).Element(c=>{if(logo is not null)c.Image(logo).FitArea();});
            });
            column.Item().Text($"{snapshot.ScopeLabel}    |    {snapshot.Header.Setup}    |    الفصل الدراسي {(snapshot.Header.Semester==1?"الأول":"الثاني")}    |    المراجعة {snapshot.Revision}").FontSize(8).AlignCenter();
        });
        page.Footer().PaddingTop(6).Column(column=>
        {
            column.Item().Text("الرمز: الصف/الشعبة؛ ش = تدريس مشترك؛ ج(حصص، أزواج) = نصيب المعلم في التقسيم؛ الزوج ضمن الحصص. (*) راجع الملحق.").FontSize(7.5f);
            if(snapshot.Teachers.Any(t=>t.Allocations.Any(a=>a.Category=="Other")))column.Item().Text("حصص المواد الإضافية الأخرى موضحة بالملحق ومحسوبة ضمن إجمالي نصاب المعلم (*).").FontSize(7.5f);
            if(snapshot.PendingRequirements>0)column.Item().Text($"البيان يعكس الإسنادات المحفوظة؛ {snapshot.PendingRequirements} مادة/فصل لم تُسند بعد.").FontSize(7.5f);
            column.Item().AlignCenter().Text("وكيل شؤون المعلمين: __________________").FontSize(10);
            if(!string.IsNullOrWhiteSpace(snapshot.Header.Footer))column.Item().Text(snapshot.Header.Footer).FontSize(7);
            column.Item().AlignCenter().ContentFromLeftToRight().DefaultTextStyle(x=>x.DirectionFromLeftToRight().FontSize(7)).Text(t=>{t.CurrentPageNumber();t.Span(" / ");t.TotalPages();});
        });
    }
    private static IContainer Cell(IContainer c)=>c.Border(.4f).BorderColor(Rule).PaddingHorizontal(2).PaddingVertical(1.2f).AlignMiddle().AlignCenter();
    private static bool SameColumn(StatementAllocation a,StatementAllocation b)=>a.Category==b.Category && (a.Category!="Specialization" || a.Stage==b.Stage && a.Grade==b.Grade);
    private static bool Dense(IEnumerable<StatementAllocation> rows)=>rows.Count()>4 || rows.Sum(a=>a.Code.Length)>55;
    private static void AllocationCell(IContainer c,IEnumerable<StatementAllocation> rows,int teacherNumber)
    {
        var allocations=rows.ToArray();
        Cell(c).Text(Dense(allocations)?$"(*) {teacherNumber}":string.Join("، ",allocations.Select(a=>"\u2066"+a.Code+"\u2069"+(a.Mode=="CoTeaching"?" ش":a.Mode=="SplitQuota"?$" ج({a.Periods}، {a.Pairs})":""))));
    }
    private static string Grade(int grade)=>grade>0 && grade<Ordinals.Length?Ordinals[grade]:grade.ToString();
    private static string Mode(string mode)=>mode switch {"CoTeaching"=>"مشترك","SplitQuota"=>"تقسيم",_=>"معلم واحد"};
}
