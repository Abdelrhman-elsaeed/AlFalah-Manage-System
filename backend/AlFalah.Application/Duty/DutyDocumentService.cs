using System.Text.Json;
using AlFalah.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace AlFalah.Application.Duty;

public sealed partial class DutyDocumentService(IDutyRepository duty, IDutyExecutionRepository execution, IDutyDocumentRepository repo,
    IDutyPdfRenderer renderer, ICurrentUserService current, DutyArchiveDriveService drive, IOptions<DutyArchiveOptions> options, IDutyDocumentAssets? assets = null) : IDutyDocumentService
{
    private int School => current.ActiveSchoolId ?? throw Forbidden();
    private string User => current.UserId ?? throw Forbidden();
    private static DutyException Forbidden() => new("DutyForbidden","الوثيقة غير متاحة لهذا الحساب. / Document unavailable to this account.",403);
    private async Task<DutyAccess> Access(bool manager, CancellationToken ct)
    {
        var a = await duty.AccessAsync(School,User,ct);
        if (a is null || !(a.Manager || a.Teacher) || manager && !a.Manager) throw Forbidden();
        return a;
    }
    private async Task<AlFalah.Domain.Entities.Duty.DutySignedDocument> Document(int id,CancellationToken ct)
    {
        var a = await Access(false,ct); var doc = await execution.DocumentAsync(School,id,ct) ?? throw new DutyException("DutyNotFound","الوثيقة غير متاحة. / Document unavailable.",404);
        if (!a.Manager && doc.AuthorUserId != User) throw Forbidden();
        return doc;
    }
    public async Task<DutyDocumentCollection> ListAsync(int assignment,CancellationToken ct)
    {
        var access = await Access(false,ct);
        var day = await execution.AssignmentAsync(School,assignment,ct) ?? throw new DutyException("DutyNotFound","المناوبة غير متاحة. / Duty unavailable.",404);
        var docs = await execution.DocumentsAsync(School,assignment,ct);
        if (!access.Manager && day.EffectiveTeacherId != User && !docs.Any(x=>x.AuthorUserId==User)) throw Forbidden();
        var intents = await repo.IntentsAsync(School,assignment,ct);
        var readiness = (await repo.ReadinessAsync(School,ct)) with { ProcessingEnabled=options.Value.ProcessingEnabled };
        var states = new List<DutyDocumentState>();
        foreach(var doc in docs.Where(x=>access.Manager || x.AuthorUserId==User).OrderByDescending(x=>x.Id))
        {
            var i = intents.SingleOrDefault(x=>x.DocumentId==doc.Id);
            // Keep ordinary local-document navigation independent of provider latency.
            // Explicit archived reads always verify; the worker also reconciles retained output.
            if (i is { State: "Archived", FolderId: not null } && (i.VerifiedAt is null || i.VerifiedAt < DateTimeOffset.UtcNow.AddMinutes(-15)))
            {
                try { await drive.VerifyAsync(i, (await repo.ArtifactAsync(School,doc.Id,ct))!,ct); await repo.MarkAvailabilityAsync(i,null,DateTimeOffset.UtcNow,ct); }
                catch(DutyException e) { await repo.MarkAvailabilityAsync(i,e.Code,DateTimeOffset.UtcNow,ct); }
                catch(Exception) when(!ct.IsCancellationRequested) { await repo.MarkAvailabilityAsync(i,"ConnectionUnavailable",DateTimeOffset.UtcNow,ct); }
            }
            var artifact=i?.ArtifactId is null?null:await repo.ArtifactAsync(School,doc.Id,ct);
            states.Add(Describe(doc,i,artifact,access.Manager,doc.AssignmentRevision==day.Revision&&doc.AuthorUserId==day.EffectiveTeacherId,readiness));
        }
        await Access(false,ct);
        return new(readiness,states);
    }
    public async Task<DutyPdf> ContentAsync(int document,bool archived,CancellationToken ct)
    {
        var doc = await Document(document,ct);
        var artifact = await DutyArtifactPreparation.PrepareAsync(repo,execution,renderer,School,document,ct);
        if (archived)
        {
            var i = await repo.IntentAsync(School,document,ct);
            if (i is not { State: "Archived" }) throw new DutyException("DutyArchiveUnavailable","الأرشيف غير متاح؛ يمكنك تنزيل النسخة المحفوظة. / Archive unavailable; download the retained copy.",409);
            try { await drive.VerifyAsync(i,artifact,ct); await repo.MarkAvailabilityAsync(i,null,DateTimeOffset.UtcNow,ct); }
            catch(DutyException e) { await repo.MarkAvailabilityAsync(i,e.Code,DateTimeOffset.UtcNow,ct); throw; }
        }
        await Document(document,ct); // Live authorization after rendering/provider I/O.
        return new(artifact.Bytes,artifact.FileName,artifact.Hash,artifact.RendererVersion);
    }
    public async Task<DutyPdf> RosterAsync(int term,int revision,CancellationToken ct)
    {
        await Access(false,ct);
        AlFalah.Domain.Entities.Duty.DutyPdfArtifact? artifact = null;
        await duty.AtomicAsync(School,async () =>
        {
            await Access(false,ct);
            var state = await duty.LoadAsync(School,ct);
            var plan = state.Plans.SingleOrDefault(x=>x.AcademicTermId==term) ?? throw new DutyException("DutyNotFound","الجدول غير متاح. / Roster unavailable.",404);
            if (revision != plan.Revision) throw new DutyException("DutyRevisionConflict","تغيّر جدول المناوبة. حدّثه ثم نزّل النسخة الحالية. / Duty roster changed. Refresh and download the current revision.",409);
            artifact = await repo.RosterArtifactAsync(School,plan.Id,revision,ct);
            if (artifact is not null) return true;
            var settings = DutyRotation.Settings(plan); var a = state.Assignments.FirstOrDefault(x=>x.DutyPlanId==plan.Id)
                ?? throw new DutyException("DutyNotFound","لا يوجد جدول منشور. / No published roster.",404);
            var header = await execution.HeaderAsync(School,a,plan,ct);
            var rows = new List<DutyRosterRow>();
            for(var date=settings.From;date<=settings.To;date=date.AddDays(1))
            {
                if (!settings.Weekdays.Contains((int)date.DayOfWeek)) continue;
                var closure = settings.Closures.FirstOrDefault(x=>date>=x.From&&date<=x.To);
                var day = state.Assignments.SingleOrDefault(x=>x.DutyPlanId==plan.Id&&x.Date==date&&!x.IsCancelled);
                rows.Add(new(date,closure?.Reason ?? (day is null ? "—" : state.Names.GetValueOrDefault(day.EffectiveTeacherId,"—")),"",closure is not null));
            }
            var logo = assets is null ? new DutyLogoSnapshot(null,null,header.LogoUrl is null?"NotConfigured":"UnavailableAtSigning") : await assets.FreezeLogoAsync(header.LogoUrl,ct);
            var snapshot = new DutyRosterSnapshot(School,plan.Id,revision,header,settings.From,settings.To,settings.Weekdays,rows,plan.TimeZoneId,logo);
            var json = JsonSerializer.Serialize(snapshot); var pdf = renderer.Roster(snapshot);
            artifact = new() { SchoolId=School,PlanId=plan.Id,PlanRevision=revision,ProvenanceJson=json,ProvenanceHash=DutyDocumentIntegrity.Hash(json),
                Bytes=pdf.Bytes,Hash=pdf.Hash,FileName=pdf.FileName,RendererVersion=pdf.RendererVersion,PreparedAt=DateTimeOffset.UtcNow };
            await repo.AddArtifactAsync(artifact,ct); return true;
        },ct);
        await Access(false,ct); DutyDocumentIntegrity.Validate(artifact!);
        return new(artifact!.Bytes,artifact.FileName,artifact.Hash,artifact.RendererVersion);
    }
    public async Task RetryAsync(int document,CancellationToken ct)
    {
        await Access(true,ct); await Document(document,ct);
        var ready = await repo.ReadinessAsync(School,ct);
        if (!options.Value.ProcessingEnabled || !ready.Enabled || ready.ConnectionState!="Connected") throw new DutyException("DutyArchiveSetup","يلزم تجهيز اتصال الأرشيف أولًا. / Set up archive connectivity first.",409);
        await repo.RetryAsync(School,document,User,ct);
    }
    public async Task<DutyArchiveReadiness> ReadinessAsync(CancellationToken ct)
    {
        await Access(true,ct);
        var ready = (await repo.ReadinessAsync(School,ct)) with { ProcessingEnabled=options.Value.ProcessingEnabled };
        if (ready.ConnectionState == "Connected")
        {
            try { await drive.RootAsync(School,null,ct); }
            catch (Exception) when (!ct.IsCancellationRequested) { ready = ready with { ConnectionState="Disconnected" }; }
        }
        await Access(true,ct);
        return ready;
    }
    public async Task<DutyArchiveReadiness> EnableAsync(bool enabled,CancellationToken ct)
    {
        await Access(true,ct);
        if (enabled)
        {
            if (!options.Value.ProcessingEnabled) throw new DutyException("DutyArchiveSetup","تفعيل الأرشفة غير متاح حاليًا. / Archive activation is currently unavailable.",409);
            try { await drive.RootAsync(School,null,ct); }
            catch (DutyException) { throw; }
            catch (Exception) when (!ct.IsCancellationRequested) { throw new DutyException("DutyArchiveConnection","تعذّر الاتصال بـ Google Drive. راجع اتصال المدرسة ثم أعد المحاولة. / Google Drive could not be reached. Review the school connection and try again.",409); }
        }
        await Access(true,ct); await repo.SetEnabledAsync(School,enabled,User,ct);
        return (await repo.ReadinessAsync(School,ct)) with {ProcessingEnabled=options.Value.ProcessingEnabled};
    }
}

public static class DutyArtifactPreparation
{
    public static async Task<AlFalah.Domain.Entities.Duty.DutyPdfArtifact> PrepareAsync(IDutyDocumentRepository repo,IDutyExecutionRepository execution,IDutyPdfRenderer renderer,int school,int document,CancellationToken ct)
    {
        AlFalah.Domain.Entities.Duty.DutyPdfArtifact? artifact = null;
        var acquired = await repo.ExclusiveAsync($"artifact:{school}:{document}",async token =>
        {
            artifact=await repo.ArtifactAsync(school,document,token);
            var doc=await execution.DocumentAsync(school,document,token) ?? throw DutyDocumentIntegrity.Error();
            DutyDocumentIntegrity.Validate(doc);
            if (artifact is not null)
            {
                DutyDocumentIntegrity.Validate(artifact);
                var evidence = JsonSerializer.Deserialize<JsonElement>(artifact.ProvenanceJson);
                if (evidence.GetProperty("SnapshotHash").GetString()!=doc.SnapshotHash || evidence.GetProperty("SignatureHash").GetString()!=doc.SignatureHash
                    || evidence.GetProperty("Id").GetInt32()!=doc.Id || evidence.GetProperty("RendererVersion").GetString()!=artifact.RendererVersion) throw DutyDocumentIntegrity.Error();
                var intent=await repo.IntentAsync(school,document,token);
                if (intent is {ArtifactId:null}) {intent.ArtifactId=artifact.Id;await repo.SaveAsync(intent,token);}
                return;
            }
            DutyPdf pdf;
            try { pdf=renderer.Signed(doc); } catch(Exception) when(!token.IsCancellationRequested) { throw DutyDocumentIntegrity.Error(); }
            var provenance=JsonSerializer.Serialize(new { doc.SchoolId,doc.Id,doc.AssignmentId,doc.AssignmentRevision,doc.Kind,doc.SnapshotHash,doc.SignatureHash,doc.PreviousDocumentId,doc.TemplateVersion,pdf.RendererVersion });
            artifact=new() {SchoolId=school,DocumentId=document,ProvenanceJson=provenance,ProvenanceHash=DutyDocumentIntegrity.Hash(provenance),
                Bytes=pdf.Bytes,Hash=pdf.Hash,FileName=pdf.FileName,RendererVersion=pdf.RendererVersion,PreparedAt=DateTimeOffset.UtcNow};
            await repo.AddArtifactAsync(artifact,token);
        },ct);
        if(!acquired) throw new DutyException("DutyDocumentPreparing","جارٍ تجهيز الوثيقة. حاول مجددًا بعد قليل. / Preparing document. Try again shortly.",409);
        return artifact!;
    }
}
