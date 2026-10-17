using System.Globalization;
using AlFalah.Application.IntelligentTimetable;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AlFalah.Infrastructure.Services;

/// <summary>QF71-B-2-1 portrait paper layout; large content and execution evidence have explicit continuations.</summary>
public sealed class CoverageRegisterRenderer : ICoverageRegisterRenderer
{
    public string Version => "coverage/QF71-B-2-1-v1";
    public const string Instruction = "آمل تسديد مكانه حسب الجدول الموضح، والتوقيع بالعلم، ولكم جزيل الشكر.";
    private sealed record Group(string Name,IReadOnlyList<CoverageRegisterRow?> Rows,int FirstPeriod);
    private static string Digits(int number)=>string.Concat(number.ToString(CultureInfo.InvariantCulture).Select(c=>char.IsDigit(c)?"٠١٢٣٤٥٦٧٨٩"[c-'0']:c));
    private static string Compact(string text,int limit,int reference)=>text.Length>limit?$"ملحق ({Digits(reference)})":text;
    public byte[] Render(CoverageRegisterSnapshot snapshot,int revision,DateTimeOffset issuedAt)
    {
        PdfTheme.EnsureFonts();
        var references=snapshot.Rows.Select(r=>r.NeedId).Distinct().Select((id,i)=>(id,i:i+1)).ToDictionary(x=>x.id,x=>x.i);
        var groups=new List<Group>();
        foreach(var teacher in snapshot.Rows.GroupBy(r=>r.OriginalTeacherId))
        {
            var slots=new List<CoverageRegisterRow?>();var starts=new List<int>();
            var maximum=Math.Max(8,teacher.Max(r=>r.Period));
            if(maximum>100||teacher.Any(r=>r.Period<1))throw new ArgumentException("راجع أرقام الحصص قبل إصدار السجل.");
            for(var period=1;period<=maximum;period++)
            {
                var rows=teacher.Where(r=>r.Period==period).OrderBy(r=>r.NeedId).ToArray();
                if(rows.Length==0){slots.Add(null);starts.Add(period);}else foreach(var row in rows){slots.Add(row);starts.Add(period);}
            }
            for(var offset=0;offset<slots.Count;offset+=8)groups.Add(new(teacher.First().OriginalTeacher,slots.Skip(offset).Take(8).ToArray(),starts[offset]));
        }
        if(groups.Count==0)groups.Add(new("",[],1));
        var appendices=snapshot.Rows.GroupBy(r=>r.NeedId).Where(g=>g.Any(NeedsAppendix)).ToArray();
        var longHeader=(snapshot.Header.Letterhead?.Length??0)>180||snapshot.Header.School.Length>90;
        var logoPath=Path.Combine(AppContext.BaseDirectory,"Assets","Logo.png");var logo=File.Exists(logoPath)?File.ReadAllBytes(logoPath):null;
        return Document.Create(document=>{
            foreach(var pageGroups in groups.Chunk(3))document.Page(page=>{
                Setup(page,snapshot,revision,issuedAt,logo);
                page.Content().Column(column=>{
                    column.Spacing(12);
                    for(var index=0;index<3;index++){
                        var group=index<pageGroups.Length?pageGroups[index]:new Group("",[],1);
                        column.Item().ShowEntire().Element(c=>DrawGroup(c,snapshot,group,references));
                    }
                });
            });
            if(appendices.Length>0||longHeader)document.Page(page=>{
                Setup(page,snapshot,revision,issuedAt,logo);
                page.Content().Column(column=>{
                    column.Spacing(7);column.Item().Text("ملحق البيانات وإثبات تنفيذ حصص الانتظار").FontSize(13).Bold();
                    column.Item().Text("توقيع الجدول إقرار بالعلم. توقيع التنفيذ أدناه إجراء مستقل بعد انتهاء الحصة. النصوص تخص آخر تنفيذ مُرسل، وتبقى المسودات خارج السجل.").FontSize(10);
                    if(longHeader){column.Item().Text(snapshot.Header.School).FontSize(11).Bold();if(!string.IsNullOrWhiteSpace(snapshot.Header.Letterhead))column.Item().Text(snapshot.Header.Letterhead).FontSize(10);}
                    foreach(var entry in appendices){
                        var row=entry.First();var reference=references[row.NeedId];
                        column.Item().PaddingTop(9).BorderTop(.6f).PaddingTop(6).Text($"({Digits(reference)}) {row.OriginalTeacher} — الحصص {string.Join("، ",entry.Select(r=>Digits(r.Period)))}").FontSize(11).Bold();
                        column.Item().Text($"الفصل: {row.Classroom}    المادة: {row.Subject}\nالمعلم المنتظر: {row.Teacher??""}").FontSize(10);
                        column.Item().Text(State(row)).FontSize(9);
                        if(row.Execution!=null){
                            column.Item().Text("ما تم تنفيذه في حصة الانتظار").FontSize(10).Bold();column.Item().Text(row.Narrative).FontSize(11).LineHeight(1.35f);
                            if(!string.IsNullOrEmpty(row.Notes)){column.Item().Text("ملاحظات").FontSize(10).Bold();column.Item().Text(row.Notes).FontSize(11).LineHeight(1.35f);}
                        }
                        column.Item().PaddingVertical(6).ShowEntire().Row(line=>{
                            Sign(line.RelativeItem(),"إقرار الاستلام بالعلم",row.Receipt,snapshot.Header.TimeZoneId);
                            line.ConstantItem(18);Sign(line.RelativeItem(),"توقيع التنفيذ والإرسال",row.Execution,snapshot.Header.TimeZoneId);
                        });
                    }
                });
            });
        }).GeneratePdf();
    }
    private static bool NeedsAppendix(CoverageRegisterRow row)=>row.Execution!=null||row.CorrectionOpen||row.OriginalTeacher.Length>70||
        (row.Teacher?.Length??0)>32||row.Classroom.Length>16||row.Subject.Length>27||row.Narrative.Length>32||row.Notes.Length>18||row.State is "ReviewRequired" or "Unable";
    private static void Setup(PageDescriptor page,CoverageRegisterSnapshot snapshot,int revision,DateTimeOffset at,byte[]? logo)
    {
        page.Size(PageSizes.A4);page.MarginHorizontal(26);page.MarginTop(22);page.MarginBottom(20);page.ContentFromRightToLeft();
        page.DefaultTextStyle(x=>x.FontFamily(PdfTheme.Font).FontSize(9).FontColor("#111111").DirectionFromRightToLeft().LineHeight(1.08f));
        page.Header().PaddingBottom(9).Column(column=>{
            column.Item().Height(66).Row(row=>{
                var heading=snapshot.Header.Letterhead;
                if(string.IsNullOrWhiteSpace(heading))heading="المملكة العربية السعودية\nوزارة التعليم\n"+snapshot.Header.School;
                if(heading.Length>180||snapshot.Header.School.Length>90)heading="بيانات المدرسة كاملة في ملحق السجل";
                row.RelativeItem(3).AlignRight().AlignMiddle().Text(heading).FontSize(9);
                row.RelativeItem(2);
                row.ConstantItem(58).PaddingVertical(8).Element(c=>{if(logo!=null)c.Image(logo).FitArea();});
            });
            column.Item().Table(table=>{
                table.ColumnsDefinition(c=>{c.RelativeColumn(3);c.RelativeColumn();});
                table.Cell().Border(.6f).PaddingHorizontal(5).PaddingVertical(2).AlignRight().Text("سجل توزيع حصص الانتظار").FontSize(15).Bold();
                table.Cell().Border(.6f).PaddingHorizontal(5).PaddingVertical(2).AlignLeft().ContentFromLeftToRight().Text("QF71-B-2-1").DirectionFromLeftToRight().FontSize(12).Bold();
            });
            column.Item().PaddingTop(5).Text(text=>{
                var year=snapshot.Header.Year.Trim();
                if(year.StartsWith("العام الدراسي",StringComparison.Ordinal))year=year["العام الدراسي".Length..].Trim(' ',':','：');
                text.Span("العام الدراسي: ");text.Span("\u2066"+year+"\u2069").DirectionFromLeftToRight();
                text.Span("    /    الفصل الدراسي: ("+(snapshot.Header.Semester==1?"الأول":"الثاني")+")");text.DefaultTextStyle(x=>x.FontSize(11).Bold());
            });
        });
        page.Footer().PaddingTop(8).Column(column=>{
            column.Item().Row(row=>{row.RelativeItem().AlignCenter().Text("وكيل المدرسة: ____________________").FontSize(10);row.RelativeItem().AlignCenter().Text("التوقيع: ____________________").FontSize(10);});
            var local=TimeZoneInfo.ConvertTime(at,TimeZoneInfo.FindSystemTimeZoneById(snapshot.Header.TimeZoneId));
            column.Item().PaddingTop(5).AlignCenter().Text(text=>{
                text.Span(snapshot.IsComplete?"تنفيذ مكتمل":"سجل غير مكتمل — حسب البيانات المحفوظة وقت الإصدار");
                text.Span($"   |   النسخة {revision}   |   {snapshot.Date:yyyy-MM-dd}   |   {local:yyyy-MM-dd HH:mm}   |   ");
                text.CurrentPageNumber();text.Span(" / ");text.TotalPages();text.DefaultTextStyle(x=>x.FontSize(7));
            });
        });
    }
    private static void DrawGroup(IContainer container,CoverageRegisterSnapshot snapshot,Group group,IReadOnlyDictionary<int,int> references)
    {
        container.Column(column=>{
            var reference=group.Rows.FirstOrDefault(r=>r!=null) is {} first?references[first.NeedId]:0;
            column.Item().Row(row=>{
                row.RelativeItem(3).Text("زملائي المعلمين/ نظراً لغياب الزميل: "+Compact(group.Name,70,reference)).FontSize(9);
                row.RelativeItem().Text("لهذا اليوم: "+new CultureInfo("ar-SA").DateTimeFormat.GetDayName(snapshot.Date.DayOfWeek)).FontSize(9);
                row.RelativeItem().AlignLeft().Text("الموافق: "+Hijri(snapshot.Date)).FontSize(9);
            });
            column.Item().PaddingBottom(3).Text(Instruction).FontSize(10);
            column.Item().Table(table=>{
                table.ColumnsDefinition(c=>{foreach(var width in new[]{40,45,94,110,112,64,72})c.RelativeColumn(width);});
                foreach(var label in new[]{"الحصة","الفصل","المادة","اسم المعلم المنتظر","ما تم تنفيذه في حصة الانتظار","التوقيع","ملاحظات"})
                    Cell(table.Cell(),24).Text(label).FontSize(8).Bold();
                for(var index=0;index<8;index++){
                    var row=index<group.Rows.Count?group.Rows[index]:null;var id=row==null?0:references[row.NeedId];
                    Cell(table.Cell(),17).Text(Digits(row?.Period??group.FirstPeriod+index));
                    Cell(table.Cell(),17).Text(row==null?"":Compact(row.Classroom,16,id));
                    Cell(table.Cell(),17).Text(row==null?"":Compact(row.Subject,27,id));
                    Cell(table.Cell(),17).Text(row==null?"":Compact(row.Teacher??"",32,id));
                    Cell(table.Cell(),17).Text(row==null?"":Compact(row.Narrative,32,id));
                    var signature=Cell(table.Cell(),17);if(row?.Receipt!=null)signature.Image(row.Receipt.Bytes).FitArea();
                    Cell(table.Cell(),17).Text(row==null?"":Compact(row.Notes,18,id));
                }
            });
        });
    }
    private static IContainer Cell(IContainer c,float height)=>c.Height(height).Border(.4f).BorderColor("#555555").PaddingHorizontal(2).PaddingVertical(1).AlignCenter().AlignMiddle();
    private static string Hijri(DateOnly date)
    {
        var calendar=new UmAlQuraCalendar();var day=date.ToDateTime(TimeOnly.MinValue);
        return day>=calendar.MinSupportedDateTime&&day<=calendar.MaxSupportedDateTime
            ?$"{Digits(calendar.GetYear(day))} / {Digits(calendar.GetMonth(day))} / {Digits(calendar.GetDayOfMonth(day))} هـ":date.ToString("yyyy/MM/dd",CultureInfo.InvariantCulture);
    }
    private static string State(CoverageRegisterRow row)=>row.CorrectionOpen?"تصحيح مفتوح — المعروض آخر تنفيذ موقّع قبل التصحيح":row.State switch{
        "Executed"=>"تنفيذ موقّع محفوظ","Acknowledged"=>"تم الاستلام — لم يُرسل التنفيذ بعد","Assigned"=>"تم التكليف — لم يُقر الاستلام بعد",
        "ReviewRequired"=>"تكليف يحتاج مراجعة المسؤول","Unable"=>"تم الإبلاغ عن تعذّر التنفيذ","Withdrawn"=>"احتياج مسحوب مع سجل سابق",_=>"تحتاج الحصة إلى تغطية"};
    private static void Sign(IContainer c,string title,CoverageRegisterSignature? signature,string zone)=>c.Border(.4f).Padding(6).Column(column=>{
        column.Item().Text(title).FontSize(9).Bold();
        if(signature!=null){column.Item().Height(38).AlignCenter().Image(signature.Bytes).FitArea();column.Item().Text(TimeZoneInfo.ConvertTime(signature.SignedAt,TimeZoneInfo.FindSystemTimeZoneById(zone)).ToString("yyyy/MM/dd HH:mm",CultureInfo.InvariantCulture)).FontSize(8);}
        else column.Item().Height(38);
    });
}
