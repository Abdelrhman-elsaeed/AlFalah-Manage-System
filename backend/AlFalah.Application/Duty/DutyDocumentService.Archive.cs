using AlFalah.Domain.Entities.Duty;

namespace AlFalah.Application.Duty;

public sealed partial class DutyDocumentService
{
    public async Task<DutyArchivePage> ArchiveAsync(DutyArchiveQuery query,CancellationToken ct)
    {
        await Access(true,ct);
        if(query.Page is <1 or >10000 || query.TeacherId?.Length>450 || query.From>query.To)
            throw new DutyException("DutyArchiveQuery","راجع المرشحات والتواريخ. / Check archive filters and dates.");
        if(query.TermId is {} term && !(await duty.TermsAsync(School,ct)).Any(x=>x.Id==term))
            throw new DutyException("DutyNotFound","الفصل غير متاح. / Term unavailable.",404);
        var slice=await repo.ArchiveAsync(School,query,DutyService.Today(await execution.SchoolTimeZoneAsync(School,ct)),ct);
        var readiness=(await repo.ReadinessAsync(School,ct)) with{ProcessingEnabled=options.Value.ProcessingEnabled};
        var rows=new List<DutyArchiveRow>();
        foreach(var group in slice.Groups)
        {
            var signed=slice.Documents.Where(x=>x.AssignmentId==group.AssignmentId&&x.AuthorUserId==group.AuthorId&&x.AssignmentRevision==group.Revision).ToArray();
            DutyDocumentState? State(string kind)
            {
                var doc=signed.FirstOrDefault(x=>x.Kind==kind);if(doc is null)return null;
                return Describe(doc,slice.Intents.FirstOrDefault(x=>x.DocumentId==doc.Id),slice.Artifacts.FirstOrDefault(x=>x.DocumentId==doc.Id),true,group.IsCurrent,readiness);
            }
            var letter=State("Letter");var report=State("Report");
            rows.Add(new(group.AssignmentId,group.AuthorId,letter?.AuthorName??report?.AuthorName??group.Name,group.Date,group.Revision,group.IsCurrent,
                group.DocumentCount,letter,report));
        }
        await Access(true,ct);return new(query.Page,10,slice.Total,readiness,rows);
    }
    private static DutyDocumentState Describe(DutySignedDocument doc,DutyArchiveIntent? intent,DutyPdfArtifact? artifact,bool manager,bool isCurrent,DutyArchiveReadiness readiness)
    {
        var state=intent?.State??"Pending";
        if(state is "PendingD3" or "Pending" && (!readiness.Enabled||!readiness.ProcessingEnabled||readiness.ConnectionState!="Connected"))state="SetupUnavailable";
        if(intent?.ErrorCode is not null&&state=="Archived")state=intent.ErrorCode=="ConnectionUnavailable"?"Disconnected":"NeedsAttention";
        var availability=intent?.ArtifactId is null?intent?.ErrorCode=="DutyArtifactUnavailable"?"Unavailable":"Preparing":"Available";
        string name="",date="";
        try{var frozen=DutyDocumentIntegrity.Validate(doc);name=frozen.GetProperty("Header").GetProperty("ActualTeacher").GetString()??"";date=frozen.GetProperty("Date").GetString()??"";
            if(intent?.ArtifactId is not null)DutyDocumentIntegrity.Validate(artifact??throw DutyDocumentIntegrity.Error());}
        catch(DutyException){availability="Unavailable";}
        return new(doc.Id,doc.Kind,doc.AssignmentRevision,doc.SignedAt,doc.PreviousDocumentId,doc.CorrectionReason,availability,state,
            manager&&availability!="Unavailable"&&intent is{State:"RetryScheduled" or "NeedsAttention"}&&readiness.Enabled&&readiness.ProcessingEnabled,
            intent?.Attempts??0,intent?.NextAttemptAt,intent?.VerifiedAt,intent?.ErrorCode,name,date,isCurrent);
    }
}
