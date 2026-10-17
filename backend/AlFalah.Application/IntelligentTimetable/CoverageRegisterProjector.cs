using AlFalah.Domain.Entities;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AlFalah.Application.IntelligentTimetable;

public static class CoverageRegisterProjector
{
    public static CoverageRegisterSnapshot Project(CoverageRegisterHeader header, DateOnly date, IReadOnlyList<DailyCoverageNeed> needs)
    {
        if(needs.Any(n=>n.LocalDate!=date)||needs.Select(n=>n.SchoolId).Distinct().Count()>1)throw new ArgumentException("مصدر السجل لا يطابق المدرسة والتاريخ.");
        var rows = needs.Where(n => n.State != "Withdrawn" || n.Records.Any(r => r.Kind == "Execution"))
            .OrderBy(n => n.OriginalTeacherId).ThenBy(n => n.SourceEntryId)
            .SelectMany(n => {
                var evidence=n.Records.Where(r=>r.SchoolId==n.SchoolId&&r.DailyCoverageNeedId==n.Id&&n.ActiveSubstitutionId!=null&&
                    r.SubstitutionId==n.ActiveSubstitutionId&&r.ActorUserId==n.AssignedUserId).ToArray();
                var receipt=Signature(evidence.Where(r=>r.Kind=="Acknowledgement").OrderByDescending(r=>r.Id).FirstOrDefault());
                var executionRecord=evidence.Where(r=>r.Kind=="Execution").OrderByDescending(r=>r.Id).FirstOrDefault();
                var execution=Signature(executionRecord);
                return JsonSerializer.Deserialize<int[]>(n.PeriodsJson)!.Distinct().Order()
                .Select(p => new CoverageRegisterRow(n.Id, n.Revision, n.OriginalTeacherId, n.OriginalTeacherName, p,
                    n.ActiveSubstitutionId, n.Classroom, n.Subject, n.AssignedTeacherName, n.State, false,
                    execution==null?"":executionRecord!.Narrative,execution==null?"":executionRecord!.Notes,receipt,execution)
                    with {CorrectionOpen=execution!=null&&n.SubmittedAt==null}); }).ToArray();
        return new(header, date, rows.Length>0&&rows.All(r=>r.State=="Executed"&&!r.CorrectionOpen&&r.Receipt!=null&&r.Execution!=null), rows);
    }
    private static CoverageRegisterSignature? Signature(CoverageRecord? record)
    {
        if(record==null)return null;
        if(record.SignatureBytes==null||record.SignatureHash!=Convert.ToHexString(SHA256.HashData(record.SignatureBytes))||
            record.SnapshotHash!=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(record.SnapshotJson))))
            throw new InvalidOperationException("Signed coverage evidence failed integrity verification.");
        return new(record.Id,record.ActorUserId,record.CreatedAt,record.SignatureHash,record.SignatureBytes.ToArray());
    }
}
