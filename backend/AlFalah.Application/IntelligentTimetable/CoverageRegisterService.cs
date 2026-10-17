using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlFalah.Domain.Entities;
using AlFalah.Shared.Models;

namespace AlFalah.Application.IntelligentTimetable;

public sealed class CoverageRegisterService(ICoverageRegisterRepository repository, ICoverageRegisterRenderer renderer, CoverageAccess access, TimeProvider clock)
{
    public Task<CoverageRegisterContext> ContextAsync(DateOnly date,int page,int pageSize,CancellationToken ct,string scope="Day")=>repository.ExecuteAsync<CoverageRegisterContext>(async token=>{
        await access.Manage(token);
        var period=Period(date,scope);date=period.Start;
        if(page<1||page>100000||pageSize<1||pageSize>50)throw new ArgumentException("صفحة سجل النسخ غير صالحة.");
        var history=await repository.RegisterHistoryAsync(access.School,date,page,pageSize,token,scope);
        Captured? snapshot=null;string? issue=null;
        try{snapshot=await Capture(period,scope,token);}catch(ArgumentException ex){issue=ex.Message;}
        var rows=snapshot?.Days.SelectMany(d=>d.Rows).ToArray()??[];
        return new(date,snapshot==null?null:Hash(Encoding.UTF8.GetBytes(snapshot.Json)),issue,rows.Select(r=>r.OriginalTeacherId).Distinct().Count(),
            rows.Length,rows.Count(r=>r.Execution!=null),snapshot?.IsComplete??false,
            new PagedResult<CoverageRegisterInfo>{Page=history.Page,PageSize=history.PageSize,TotalCount=history.TotalCount,Items=history.Items.Select(Info).ToList()},scope,period.End);
    },ct);
    public Task<CoverageRegisterInfo> IssueAsync(IssueCoverageRegisterRequest request,CancellationToken ct)=>repository.ExecuteAsync(async token=>{
        await access.Manage(token);
        var period=Period(request.Date,request.Scope);var date=period.Start;
        if(request.SourceRevision==null||request.SourceRevision.Length!=64||request.SourceRevision.Any(c=>!Uri.IsHexDigit(c)))throw new ArgumentException("حدّث بيانات السجل قبل إصدار النسخة.");
        var version=renderer.Version+(request.Scope=="Week"?"/week-v3":"");
        var retained=await repository.RegisterBySourceAsync(access.School,date,request.SourceRevision,version,token,request.Scope);
        if(retained!=null){Verify(retained);return Info(retained);}
        var snapshot=await Capture(period,request.Scope,token);
        if(Hash(Encoding.UTF8.GetBytes(snapshot.Json))!=request.SourceRevision)throw new BellScheduleConflictException("تغيرت التكليفات أو التوقيعات؛ حدّث بيانات السجل ثم أعد الإصدار. النسخ السابقة محفوظة.");
        var revision=await repository.NextRegisterRevisionAsync(access.School,date,token,request.Scope);var at=clock.GetUtcNow();
        token.ThrowIfCancellationRequested();var bytes=request.Scope=="Week"?renderer.RenderWeek(new(date,period.End,snapshot.Days),revision,at):renderer.Render(snapshot.Days[0],revision,at);token.ThrowIfCancellationRequested();
        if(bytes.Length<100||!bytes.AsSpan(0,5).SequenceEqual("%PDF-"u8))throw new InvalidOperationException("Coverage register renderer returned invalid bytes.");
        var artifact=new CoverageRegisterArtifact {SchoolId=access.School,LocalDate=date,Scope=request.Scope,Revision=revision,SourceRevision=request.SourceRevision,
            RendererVersion=version,IsComplete=snapshot.IsComplete,SnapshotJson=snapshot.Json,Pdf=bytes,Sha256=Hash(bytes),
            FileName=request.Scope=="Week"?$"سجل حصص الانتظار الأسبوعي - {date.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture)} - {period.End.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture)} - {revision}.pdf":$"سجل توزيع حصص الانتظار - {date:yyyy-MM-dd} - {revision}.pdf",CreatedByUserId=access.User,CreatedAt=at};
        repository.AddRegister(artifact);
        repository.AddAudit(new(){SchoolId=access.School,UserId=access.User,Action="Timetable.Coverage.RegisterIssued",EntityName=nameof(CoverageRegisterArtifact),
            NewValues=JsonSerializer.Serialize(new{Date=date,EndDate=period.End,request.Scope,revision,artifact.SourceRevision,artifact.Sha256,artifact.IsComplete})});
        await repository.SaveAsync(token);return Info(artifact);
    },ct);
    public async Task<CoverageRegisterFile> ContentAsync(int id,CancellationToken ct)
    {
        await access.Manage(ct);var artifact=await repository.RegisterAsync(access.School,id,ct)??throw new KeyNotFoundException();Verify(artifact);
        return new(artifact.Pdf,artifact.FileName,artifact.Sha256,artifact.Revision);
    }
    private sealed record Captured(string Json,IReadOnlyList<CoverageRegisterSnapshot> Days,bool IsComplete);
    private static (DateOnly Start,DateOnly End) Period(DateOnly date,string scope)
    {
        if(scope is not ("Day" or "Week"))throw new ArgumentException("اختر سجل يوم واحد أو أسبوع كامل.");
        if(scope=="Day")return(date,date);
        if(date.DayNumber<(int)date.DayOfWeek)throw new ArgumentException("اختر أسبوعًا داخل نطاق التواريخ المتاح.");
        var start=date.AddDays(-(int)date.DayOfWeek);
        if(start.DayNumber>DateOnly.MaxValue.DayNumber-6)throw new ArgumentException("اختر أسبوعًا داخل نطاق التواريخ المتاح.");
        return(start,start.AddDays(6));
    }
    private async Task<Captured> Capture((DateOnly Start,DateOnly End) period,string scope,CancellationToken ct)
    {
        var needs=await repository.RegisterNeedsAsync(access.School,period.Start,period.End,ct);
        var included=needs.Where(n=>n.State!="Withdrawn"||n.Records.Any(r=>r.Kind=="Execution")).ToArray();
        var header=await repository.RegisterHeaderAsync(access.School,period.Start,included.Select(n=>n.SchoolTimetableId).Distinct().ToArray(),ct);
        var grouped=included.ToLookup(n=>n.LocalDate);
        var days=Enumerable.Range(0,period.End.DayNumber-period.Start.DayNumber+1).Select(offset=>{
            var date=period.Start.AddDays(offset);return CoverageRegisterProjector.Project(header,date,grouped[date].ToArray());
        }).ToArray();
        var json=scope=="Week"?JsonSerializer.Serialize(new CoverageRegisterWeek(period.Start,period.End,days)):JsonSerializer.Serialize(days[0]);
        return new(json,days,days.Any(d=>d.Rows.Count>0)&&days.Where(d=>d.Rows.Count>0).All(d=>d.IsComplete));
    }
    public static string Fingerprint(CoverageRegisterSnapshot snapshot)=>Hash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(snapshot)));
    private static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
    private static void Verify(CoverageRegisterArtifact artifact)
    {if(Hash(Encoding.UTF8.GetBytes(artifact.SnapshotJson))!=artifact.SourceRevision||Hash(artifact.Pdf)!=artifact.Sha256||artifact.Pdf.Length<100||!artifact.Pdf.AsSpan(0,5).SequenceEqual("%PDF-"u8))throw new InvalidOperationException("Retained coverage register failed integrity verification.");}
    private static CoverageRegisterInfo Info(CoverageRegisterArtifact artifact)=>new(artifact.Id,artifact.LocalDate,artifact.Revision,artifact.SourceRevision,
        artifact.IsComplete,artifact.FileName,artifact.Sha256,artifact.CreatedAt,artifact.Scope,artifact.Scope=="Week"?artifact.LocalDate.AddDays(6):artifact.LocalDate);
}
