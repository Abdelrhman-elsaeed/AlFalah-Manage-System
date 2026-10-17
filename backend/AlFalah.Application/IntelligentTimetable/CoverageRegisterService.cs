using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlFalah.Domain.Entities;
using AlFalah.Shared.Models;

namespace AlFalah.Application.IntelligentTimetable;

public sealed class CoverageRegisterService(ICoverageRegisterRepository repository, ICoverageRegisterRenderer renderer, CoverageAccess access, TimeProvider clock)
{
    public Task<CoverageRegisterContext> ContextAsync(DateOnly date,int page,int pageSize,CancellationToken ct)=>repository.ExecuteAsync<CoverageRegisterContext>(async token=>{
        await access.Manage(token);
        if(page<1||page>100000||pageSize<1||pageSize>50)throw new ArgumentException("صفحة سجل النسخ غير صالحة.");
        var history=await repository.RegisterHistoryAsync(access.School,date,page,pageSize,token);
        CoverageRegisterSnapshot? snapshot=null;string? issue=null;
        try{snapshot=await Capture(date,token);}catch(ArgumentException ex){issue=ex.Message;}
        return new(date,snapshot==null?null:Fingerprint(snapshot),issue,snapshot?.Rows.Select(r=>r.OriginalTeacherId).Distinct().Count()??0,
            snapshot?.Rows.Count??0,snapshot?.Rows.Count(r=>r.Execution!=null)??0,snapshot?.IsComplete??false,
            new PagedResult<CoverageRegisterInfo>{Page=history.Page,PageSize=history.PageSize,TotalCount=history.TotalCount,Items=history.Items.Select(Info).ToList()});
    },ct);
    public Task<CoverageRegisterInfo> IssueAsync(IssueCoverageRegisterRequest request,CancellationToken ct)=>repository.ExecuteAsync(async token=>{
        await access.Manage(token);
        if(request.SourceRevision==null||request.SourceRevision.Length!=64||request.SourceRevision.Any(c=>!Uri.IsHexDigit(c)))throw new ArgumentException("حدّث بيانات السجل قبل إصدار النسخة.");
        var retained=await repository.RegisterBySourceAsync(access.School,request.Date,request.SourceRevision,renderer.Version,token);
        if(retained!=null){Verify(retained);return Info(retained);}
        var snapshot=await Capture(request.Date,token);
        if(Fingerprint(snapshot)!=request.SourceRevision)throw new BellScheduleConflictException("تغيرت التكليفات أو التوقيعات؛ حدّث بيانات السجل ثم أعد الإصدار. النسخ السابقة محفوظة.");
        var revision=await repository.NextRegisterRevisionAsync(access.School,request.Date,token);var at=clock.GetUtcNow();
        token.ThrowIfCancellationRequested();var bytes=renderer.Render(snapshot,revision,at);token.ThrowIfCancellationRequested();
        if(bytes.Length<100||!bytes.AsSpan(0,5).SequenceEqual("%PDF-"u8))throw new InvalidOperationException("Coverage register renderer returned invalid bytes.");
        var artifact=new CoverageRegisterArtifact {SchoolId=access.School,LocalDate=request.Date,Revision=revision,SourceRevision=request.SourceRevision,
            RendererVersion=renderer.Version,IsComplete=snapshot.IsComplete,SnapshotJson=JsonSerializer.Serialize(snapshot),Pdf=bytes,Sha256=Hash(bytes),
            FileName=$"سجل توزيع حصص الانتظار - {request.Date:yyyy-MM-dd} - {revision}.pdf",CreatedByUserId=access.User,CreatedAt=at};
        repository.AddRegister(artifact);
        repository.AddAudit(new(){SchoolId=access.School,UserId=access.User,Action="Timetable.Coverage.RegisterIssued",EntityName=nameof(CoverageRegisterArtifact),
            NewValues=JsonSerializer.Serialize(new{request.Date,revision,artifact.SourceRevision,artifact.Sha256,artifact.IsComplete})});
        await repository.SaveAsync(token);return Info(artifact);
    },ct);
    public async Task<CoverageRegisterFile> ContentAsync(int id,CancellationToken ct)
    {
        await access.Manage(ct);var artifact=await repository.RegisterAsync(access.School,id,ct)??throw new KeyNotFoundException();Verify(artifact);
        return new(artifact.Pdf,artifact.FileName,artifact.Sha256,artifact.Revision);
    }
    private async Task<CoverageRegisterSnapshot> Capture(DateOnly date,CancellationToken ct)
    {
        var needs=await repository.NeedsAsync(access.School,date,ct);
        var included=needs.Where(n=>n.State!="Withdrawn"||n.Records.Any(r=>r.Kind=="Execution")).ToArray();
        var header=await repository.RegisterHeaderAsync(access.School,date,included.Select(n=>n.SchoolTimetableId).Distinct().ToArray(),ct);
        return CoverageRegisterProjector.Project(header,date,included);
    }
    public static string Fingerprint(CoverageRegisterSnapshot snapshot)=>Hash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(snapshot)));
    private static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
    private static void Verify(CoverageRegisterArtifact artifact)
    {if(Hash(Encoding.UTF8.GetBytes(artifact.SnapshotJson))!=artifact.SourceRevision||Hash(artifact.Pdf)!=artifact.Sha256||artifact.Pdf.Length<100||!artifact.Pdf.AsSpan(0,5).SequenceEqual("%PDF-"u8))throw new InvalidOperationException("Retained coverage register failed integrity verification.");}
    private static CoverageRegisterInfo Info(CoverageRegisterArtifact artifact)=>new(artifact.Id,artifact.LocalDate,artifact.Revision,artifact.SourceRevision,
        artifact.IsComplete,artifact.FileName,artifact.Sha256,artifact.CreatedAt);
}
