using System.Security.Cryptography;
using System.Text.Json;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Application.Classrooms;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Entities;

namespace AlFalah.Application.IntelligentTimetable;

public sealed class TeachingStatementService(ITeachingStatementRepository repository, ITeachingStatementRenderer renderer)
{
    public async Task<TeachingStatementFile> GetAsync(int school, int setup, int revision, string scope, string actor, CancellationToken ct)
    {
        if (revision < 1 || scope is not ("all" or "stage:1" or "stage:2" or "stage:3"))
            throw new ArgumentException("اختر نطاق البيان والمراجعة المحفوظة.");
        var retained = await repository.FindAsync(school,setup,revision,scope,renderer.Version,ct);
        if (retained is not null) return File(retained);
        var source = await repository.CaptureAsync(school,setup,ct);
        if (source.Revision != revision) throw new BellScheduleConflictException("Stale statement revision");
        var snapshot = Project(source,scope);
        ct.ThrowIfCancellationRequested();
        var pdf = renderer.Render(snapshot);
        var saved = await repository.StoreAsync(new() {SchoolId=school,TimetableSetupProfileId=setup,Revision=revision,Scope=scope,
            RendererVersion=renderer.Version,SnapshotJson=JsonSerializer.Serialize(snapshot),Pdf=pdf,Sha256=Hash(pdf),
            FileName=$"بيان إسنادات التدريس - {revision} - {scope.Replace(':','-')}.pdf",CreatedByUserId=actor,CreatedAt=snapshot.CreatedAt},ct);
        return File(saved);
    }
    public static TeachingStatementSnapshot Project(StatementSource source, string scope)
    {
        var stage = scope == "all" ? (int?)null : int.Parse(scope.AsSpan(6));
        var classes = source.Classes.ToDictionary(x=>x.Id); var subjects=source.Subjects.ToDictionary(x=>x.Id);
        var requirements=source.Requirements.ToDictionary(x=>x.Id);var teachers=source.Teachers.ToDictionary(x=>x.Id);
        var allocations=new List<StatementAllocation>();
        foreach(var cell in source.Assignments)
        {
            if(!requirements.TryGetValue(cell.ClassSubjectRequirementId,out var r) || !classes.TryGetValue(r.ClassroomId,out var classroom) || !subjects.TryGetValue(r.SubjectId,out var subject))
                throw new ArgumentException("يوجد إسناد لمادة أو فصل غير متاح؛ راجع الإسنادات قبل إصدار البيان.");
            TeachingAllocationPolicy.Validate(new() {IndividualPeriodCount=r.Individual,PairedBlockCount=r.Pairs},
                new(cell.ClassSubjectRequirementId,cell.Mode,cell.Members.Select(m=>new TeachingMemberRequest(m.TeacherTimetableProfileId,m.AllocatedPeriodCount,m.AllocatedPairedBlockCount)).ToArray()));
            foreach(var m in cell.Members)
            {
                if(!teachers.TryGetValue(m.TeacherTimetableProfileId,out var teacher) || !teacher.IsActive)
                    throw new ArgumentException("يوجد إسناد لمعلم غير نشط؛ راجع الإسنادات قبل إصدار البيان.");
                if(stage.HasValue && classroom.Stage!=stage)continue;
                if(m.StatementCategory is null || !StatementCategories.Valid(m.StatementCategory))
                    throw new ArgumentException("حدّد تصنيف أنصبة التخصص والمواد الإضافية من إعدادات البيان أولًا.");
                allocations.Add(new(r.Id,m.TeacherTimetableProfileId,subject.Name,classroom.Stage,classroom.Grade,classroom.Label,classroom.Code,
                    m.StatementCategory,cell.Mode,m.AllocatedPeriodCount,m.AllocatedPairedBlockCount));
            }
        }
        if(allocations.Count==0)throw new ArgumentException("لا توجد إسنادات محفوظة في النطاق المختار.");
        foreach(var t in teachers.Values)
            if(source.Assignments.SelectMany(x=>x.Members).Where(x=>x.TeacherTimetableProfileId==t.Id).Sum(x=>(long)x.AllocatedPeriodCount)>t.MaximumWeeklyPeriods)
                throw new ArgumentException("يوجد تجاوز للنصاب الأسبوعي؛ صحّح الإسنادات قبل إصدار البيان.");
        var selected=source.Teachers.Where(t=>stage.HasValue ? allocations.Any(a=>a.TeacherId==t.Id) : t.IsActive || allocations.Any(a=>a.TeacherId==t.Id))
            .OrderBy(t=>t.Name,StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("ar"),false)).ThenBy(t=>t.Id)
            .Select(t=>new StatementTeacher(t.Id,t.Name,t.Specialization ?? "—",allocations.Where(a=>a.TeacherId==t.Id).OrderBy(a=>a.Stage).ThenBy(a=>a.Grade).ThenBy(a=>a.RequirementId).ToArray())).ToArray();
        var assigned=source.Assignments.Where(x=>x.Members.Count>0).Select(x=>x.ClassSubjectRequirementId).ToHashSet();
        var pending=source.Requirements.Count(r=>classes.TryGetValue(r.ClassroomId,out var c) && subjects.ContainsKey(r.SubjectId) && (!stage.HasValue || c.Stage==stage) && !assigned.Contains(r.Id));
        return new(source.Revision,scope,stage.HasValue ? StageLabel(stage.Value) : "جميع إسنادات ملف الإعداد",source.Header,DateTimeOffset.UtcNow,selected,pending) {
            Grades=source.Classes.Where(c=>!stage.HasValue||c.Stage==stage).Select(c=>c.Stage).Distinct().OrderBy(s=>s)
                .SelectMany(s=>Enumerable.Range(1,ClassroomIdentity.GradeCount((SchoolStage)s)).Select(g=>new StatementGrade(s,g)))
                .Union(source.Classes.Where(c=>!stage.HasValue||c.Stage==stage).Select(c=>new StatementGrade(c.Stage,c.Grade)))
                .OrderBy(g=>g.Stage).ThenBy(g=>g.Grade).ToArray()
        };
    }
    public static string StageLabel(int stage)=>stage switch {1=>"المرحلة الابتدائية",2=>"المرحلة المتوسطة",3=>"المرحلة الثانوية",_=>"مرحلة غير محددة"};
    private static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
    private static TeachingStatementFile File(TeachingAssignmentStatement row)
    {
        if(Hash(row.Pdf)!=row.Sha256)throw new InvalidOperationException("Statement integrity verification failed.");
        return new(row.Pdf,row.FileName,row.Sha256,row.Revision,row.Scope);
    }
}
