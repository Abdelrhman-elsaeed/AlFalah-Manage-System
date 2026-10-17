using AlFalah.Application.IntelligentTimetable;
using AlFalah.Domain.Entities;
using AlFalah.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed partial class TimetableSubstitutionRepository : ICoverageRegisterRepository
{
    public async Task<CoverageRegisterHeader> RegisterHeaderAsync(int school,DateOnly date,IReadOnlyList<int> timetableIds,CancellationToken ct)
    {
        var ids=timetableIds.Count>0?timetableIds:await PublishedTimetablesAsync(school,date,ct);
        var headers=await db.SchoolTimetables.AsNoTracking().Where(t=>t.SchoolId==school&&ids.Contains(t.Id))
            .Select(t=>new CoverageRegisterHeader(t.School.Name,t.AcademicYear.NameAr,(int)t.Semester,
                t.School.ReportSettings==null?null:t.School.ReportSettings.ReportHeaderText,
                t.BellScheduleRevision==null?"Africa/Cairo":t.BellScheduleRevision.SchoolTimeZoneId)).Distinct().ToListAsync(ct);
        if(headers.Count!=1)throw new ArgumentException("يلزم سجل داخل عام وفصل دراسي محددين. راجع الجدول المنشور وتاريخ السجل.");
        return headers[0];
    }
    public Task<CoverageRegisterArtifact?> RegisterAsync(int school,int id,CancellationToken ct)=>db.Set<CoverageRegisterArtifact>().AsNoTracking().SingleOrDefaultAsync(x=>x.SchoolId==school&&x.Id==id,ct);
    public Task<CoverageRegisterArtifact?> RegisterBySourceAsync(int school,DateOnly date,string source,string renderer,CancellationToken ct,string scope="Day")=>
        db.Set<CoverageRegisterArtifact>().AsNoTracking().SingleOrDefaultAsync(x=>x.SchoolId==school&&x.LocalDate==date&&x.SourceRevision==source&&x.RendererVersion==renderer&&x.Scope==scope,ct);
    public async Task<IReadOnlyList<DailyCoverageNeed>> RegisterNeedsAsync(int school,DateOnly start,DateOnly end,CancellationToken ct)=>
        await db.Set<DailyCoverageNeed>().AsNoTracking().Where(n=>n.SchoolId==school&&n.LocalDate>=start&&n.LocalDate<=end)
            .Include(n=>n.Records).AsSplitQuery().OrderBy(n=>n.LocalDate).ThenBy(n=>n.Id).ToArrayAsync(ct);
    public async Task<PagedResult<CoverageRegisterArtifact>> RegisterHistoryAsync(int school,DateOnly date,int page,int size,CancellationToken ct,string scope="Day")
    {
        var query=db.Set<CoverageRegisterArtifact>().AsNoTracking().Where(x=>x.SchoolId==school&&x.LocalDate==date&&x.Scope==scope);
        return new() {Page=page,PageSize=size,TotalCount=await query.CountAsync(ct),Items=await query.OrderByDescending(x=>x.Revision)
            .Skip((page-1)*size).Take(size).Select(x=>new CoverageRegisterArtifact{Id=x.Id,SchoolId=x.SchoolId,LocalDate=x.LocalDate,Scope=x.Scope,Revision=x.Revision,
                SourceRevision=x.SourceRevision,IsComplete=x.IsComplete,Sha256=x.Sha256,FileName=x.FileName,CreatedAt=x.CreatedAt}).ToListAsync(ct)};
    }
    public async Task<int> NextRegisterRevisionAsync(int school,DateOnly date,CancellationToken ct,string scope="Day")=>
        (await db.Set<CoverageRegisterArtifact>().Where(x=>x.SchoolId==school&&x.LocalDate==date&&x.Scope==scope).MaxAsync(x=>(int?)x.Revision,ct)??0)+1;
    public void AddRegister(CoverageRegisterArtifact artifact)=>db.Add(artifact);
}
