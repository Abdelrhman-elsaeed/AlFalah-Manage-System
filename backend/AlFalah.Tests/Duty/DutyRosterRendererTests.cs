using AlFalah.Application.Duty;
using AlFalah.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace AlFalah.Tests.Duty;

public sealed class DutyRosterRendererTests
{
    [Theory]
    [InlineData("source-eighteen-weeks",18,false,5)]
    [InlineData("source-long-names-closures",18,true,5)]
    [InlineData("source-full-year",43,false,5)]
    [InlineData("source-seven-study-days",18,false,7)]
    [InlineData("source-partial-weeks",2,false,5)]
    public void Source_roster_keeps_every_canonical_day_and_frozen_provenance(string name,int weeks,bool longNames,int days)
    {
        QuestPDF.Settings.License=QuestPDF.Infrastructure.LicenseType.Community;
        var from=new DateOnly(2026,9,6);
        var rows=Enumerable.Range(0,weeks*7).Select(n=>from.AddDays(n)).Where(d=>(int)d.DayOfWeek<days)
            .Select((date,i)=>new DutyRosterRow(date,i is >=30 and <35?"إجازة رسمية":i>=weeks*days-5?"اختبارات نهاية الفصل":longNames?"عبدالرحمن عبدالله محمد عبدالرحمن معلم التحقق الاصطناعي ذو الاسم الطويل":"معلم التحقق الاصطناعي "+(i%7+1),"",i is >=30 and <35)).ToArray();
        var header=new DutyExecutionHeader("مدرسة التحقق الاصطناعية","مدينة التحقق","وزارة التعليم\nإدارة التعليم\nمكتب التعليم",null,null,"مدير التحقق الاصطناعي","2026–2027",1,"","",false);
        var snapshot=new DutyRosterSnapshot(1,1,7,header,from,from.AddDays(weeks*7-1),Enumerable.Range(0,days).ToArray(),rows,"Africa/Cairo",new(null,null,"UnavailableAtSigning"));
        var renderer=new DutyPdfRenderer();var pdf=renderer.Roster(snapshot);
        pdf.Bytes.Take(5).Should().Equal("%PDF-"u8.ToArray());pdf.Hash.Should().Be(DutyDocumentIntegrity.Hash(pdf.Bytes));
        pdf.RendererVersion.Should().Be(renderer.RosterVersion).And.NotBe(renderer.Version);
        snapshot.Rows.Should().HaveCount(weeks*days);snapshot.Revision.Should().Be(7);
        var target=Environment.GetEnvironmentVariable("ALFALAH_DUTY_D3_PDF_DIR");if(string.IsNullOrWhiteSpace(target))return;
        Directory.CreateDirectory(target);File.WriteAllBytes(Path.Combine(target,name+".pdf"),pdf.Bytes);
        File.WriteAllText(Path.Combine(target,name+".json"),System.Text.Json.JsonSerializer.Serialize(new {pdf.Hash,pdf.RendererVersion,ExpectedRows=rows.Length,ExpectedDates=rows.Select(x=>x.Date.ToString("yyyy-MM-dd")),Synthetic=true}));
    }
}
