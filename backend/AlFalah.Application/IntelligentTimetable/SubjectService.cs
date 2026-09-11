using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.IntelligentTimetable;

public sealed class SubjectService(ISubjectRepository repository, IBellScheduleRepository bells)
{
    private async Task<TimetableSetupProfile> Setup(int school, int id, CancellationToken ct) =>
        await repository.GetSetupAsync(school, id, ct) ?? throw new KeyNotFoundException();
    public async Task<SubjectOverviewDto> GetAsync(int school, int setupId, CancellationToken ct)
    {
        var setup = await Setup(school, setupId, ct);
        var subjects = await repository.GetSubjectsAsync(school, ct);
        var classes = await repository.GetClassroomsAsync(school, setup.AcademicYearId, ct);
        var rooms = await repository.GetRoomsAsync(school, ct);
        var requirements = await repository.GetRequirementsAsync(school, setupId, ct);
        return new(subjects.Select(ToDto).ToArray(), classes, rooms, requirements.Select(r => new SubjectRequirementDto(r.Id,
            r.SubjectId, r.ClassroomId, classes.FirstOrDefault(c => c.Id == r.ClassroomId)?.Name ?? $"{r.ClassroomId}", r.Revision,
            r.TotalWeeklyPeriods, Rules(r))).ToArray(), await bells.GetSelectedAsync(school, setup.AcademicYearId, setup.Semester, setupId, ct));
    }
    public async Task<SubjectDto> SaveSubjectAsync(int school, int setupId, int? id, SaveSubjectRequest request, string actor, CancellationToken ct)
    {
        await Setup(school, setupId, ct);
        var subjects = await repository.GetSubjectsAsync(school, ct);
        if (subjects.Any(x => x.Id != id && string.Equals(x.Name, request.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("اسم المادة موجود بالفعل في دليل المدرسة.");
        var subject = id.HasValue ? subjects.SingleOrDefault(x => x.Id == id) ?? throw new KeyNotFoundException() : new SubjectDefinition { SchoolId = school };
        if (id.HasValue && subject.Revision != request.Revision) throw new BellScheduleConflictException("Subject changed");
        subject.Name = request.Name.Trim(); subject.Color = request.Color.ToLowerInvariant(); subject.UpdatedByUserId = actor;
        subject.UpdatedAt = DateTimeOffset.UtcNow;
        if (id.HasValue) subject.Revision++; else repository.AddSubject(subject);
        await repository.SaveAsync(school, setupId, actor, "Timetable.Subject.Saved", request, ct);
        return ToDto(subject);
    }
    public async Task<RoomDto> CreateRoomAsync(int school, int setupId, CreateRoomRequest request, string actor, CancellationToken ct)
    {
        await Setup(school, setupId, ct);
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 100) throw new ArgumentException("اسم الغرفة مطلوب وبحد أقصى 100 حرف.");
        if ((await repository.GetRoomsAsync(school, ct)).Any(x => string.Equals(x.Name, request.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("اسم الغرفة موجود بالفعل.");
        var room = new TimetableRoom { SchoolId = school, Name = request.Name.Trim() };
        repository.AddRoom(room);
        await repository.SaveAsync(school, setupId, actor, "Timetable.Room.Created", request, ct);
        return new(room.Id, room.Name);
    }
    public async Task<SubjectBulkResult> AllocateAsync(int school, int setupId, AllocateSubjectRequest request, string actor, CancellationToken ct)
    {
        var setup = await Setup(school, setupId, ct);
        if (!(await repository.GetSubjectsAsync(school, ct)).Any(x => x.Id == request.SubjectId)) throw new ArgumentException("المادة غير متاحة لهذه المدرسة.");
        var classes = await repository.GetClassroomsAsync(school, setup.AcademicYearId, ct);
        if (request.Classes.Any(t => classes.All(c => c.Id != t.ClassroomId))) throw new ArgumentException("أحد الفصول غير متاح في المدرسة أو العام الدراسي.");
        var rooms = await repository.GetRoomsAsync(school, ct);
        if (request.Rules.RoomIds.Any(id => rooms.All(r => r.Id != id))) throw new ArgumentException("إحدى الغرف غير متاحة لهذه المدرسة.");
        var schedule = await bells.GetSelectedAsync(school, setup.AcademicYearId, setup.Semester, setupId, ct)
            ?? throw new ArgumentException("اختر توقيتات الجدول أولاً.");
        SubjectSchedulingPolicy.Validate(request.Rules, schedule);
        var existing = await repository.GetRequirementsAsync(school, setupId, ct);
        var assignedRequirements = (await repository.GetTeachingAssignmentsAsync(school, setupId, ct)).Where(x => !x.IsDeleted).Select(x => x.ClassSubjectRequirementId).ToHashSet();
        var results = new List<SubjectAllocationResult>();
        var changes = new List<(SubjectClassTarget Target, ClassSubjectRequirement? Existing)>();
        foreach (var target in request.Classes)
        {
            var name = classes.Single(x => x.Id == target.ClassroomId).Name;
            var current = existing.SingleOrDefault(x => x.ClassroomId == target.ClassroomId && x.SubjectId == request.SubjectId);
            if (current is not null && !request.OverwriteExisting) { results.Add(new(target.ClassroomId, name, "Skipped", "تم الاحتفاظ بالإعدادات الحالية.")); continue; }
            if ((current?.Revision ?? 0) != target.Revision) throw new BellScheduleConflictException("Class requirements changed; reload before replacing");
            if (current is not null && assignedRequirements.Contains(current.Id) &&
                (current.IndividualPeriodCount != request.Rules.IndividualPeriodCount || current.PairedBlockCount != request.Rules.PairedBlockCount))
                throw new ArgumentException("ألغِ إسناد المعلمين لهذه المادة قبل تغيير نصابها أو عدد حصصها المزدوجة، ثم أعد الإسناد.");
            var others = existing.Where(x => x.ClassroomId == target.ClassroomId && x.SubjectId != request.SubjectId).ToArray();
            var error = ValidateClass(request.Rules, others, schedule);
            if (error is not null) { results.Add(new(target.ClassroomId, name, "Skipped", error)); continue; }
            changes.Add((target, current));
            results.Add(new(target.ClassroomId, name, current is null ? "Created" : "Updated", null));
        }
        foreach (var (target, current) in changes)
        {
            var r = current ?? new ClassSubjectRequirement { SchoolId = school, TimetableSetupProfileId = setupId, ClassroomId = target.ClassroomId, SubjectId = request.SubjectId };
            r.IndividualPeriodCount = request.Rules.IndividualPeriodCount; r.PairedBlockCount = request.Rules.PairedBlockCount;
            r.TimePreference = request.Rules.TimePreference;
            r.EarliestPeriodSequence = r.TimePreference == "None" ? null : request.Rules.EarliestPeriodSequence;
            r.LatestPreferredPeriodSequence = r.TimePreference == "None" ? null : request.Rules.LatestPreferredPeriodSequence;
            r.UpdatedAt = DateTimeOffset.UtcNow; r.UpdatedByUserId = actor;
            if (current is null) repository.AddRequirement(r); else r.Revision++;
            repository.SetRules(r, request.Rules);
        }
        if (changes.Count > 0)
        {
            await InvalidateAsync(setup, actor, ct);
            await repository.SaveAsync(school, setupId, actor, "Timetable.Subject.Allocated", new { Request = request, Results = results }, ct);
        }
        return new(results);
    }
    private static string? ValidateClass(SubjectRulesRequest rules, ClassSubjectRequirement[] others, BellScheduleDto schedule)
    {
        var capacity = schedule.Days.Where(x => x.IsStudyDay).Sum(d => BellScheduleResolver.EffectivePeriods(schedule, (TimetableDay)d.Day).Count);
        if (others.Sum(x => x.TotalWeeklyPeriods) + rules.IndividualPeriodCount + rules.PairedBlockCount * 2 > capacity)
            return "مجموع أنصبة مواد الفصل يتجاوز الأسبوع الدراسي.";
        if (rules.FixedSlots.Any(s => others.Any(o => o.FixedSlots.Any(f => f.Day == s.Day && f.Period == s.Period))))
            return "الحصة المثبتة تتعارض مع مادة أخرى للفصل.";
        return null;
    }
    public async Task<SubjectBulkResult> UpdateAsync(int school, int setupId, int id, UpdateSubjectRequirementsRequest request, string actor, CancellationToken ct)
    {
        await Setup(school, setupId, ct);
        var current = (await repository.GetRequirementsAsync(school, setupId, ct)).SingleOrDefault(x => x.Id == id) ?? throw new KeyNotFoundException();
        return await AllocateAsync(school, setupId, new(current.SubjectId, [new(current.ClassroomId, request.Revision)], request.Rules, true), actor, ct);
    }
    public async Task<int> RemoveAsync(int school, int setupId, int id, int revision, string actor, CancellationToken ct)
    {
        var setup = await Setup(school, setupId, ct);
        var r = (await repository.GetRequirementsAsync(school, setupId, ct)).SingleOrDefault(x => x.Id == id) ?? throw new KeyNotFoundException();
        if (r.Revision != revision) throw new BellScheduleConflictException("Requirement changed");
        if (await repository.IsUsedAsync(school, id, ct)) throw new ArgumentException("لا يمكن إزالة إعداد مرتبط بجدول؛ احتفظ به لحماية السجل التاريخي.");
        r.IsDeleted = true; r.Revision++; r.UpdatedByUserId = actor; r.UpdatedAt = DateTimeOffset.UtcNow;
        await InvalidateAsync(setup, actor, ct);
        await repository.SaveAsync(school, setupId, actor, "Timetable.Subject.RequirementRemoved", new { RequirementId = id }, ct);
        return id;
    }
    private async Task InvalidateAsync(TimetableSetupProfile setup, string actor, CancellationToken ct)
    {
        var dependencies = await bells.GetDependenciesAsync(setup.SchoolId, null, setup.Id, ct);
        dependencies.Invalidate(DateTimeOffset.UtcNow, actor);
    }
    private static SubjectDto ToDto(SubjectDefinition s) => new(s.Id, s.Name, s.Color, s.Revision);
    public static SubjectRulesRequest Rules(ClassSubjectRequirement r) => new(r.IndividualPeriodCount, r.PairedBlockCount, r.TimePreference,
        r.EarliestPeriodSequence, r.LatestPreferredPeriodSequence, r.AllowedDays.Select(x => x.Day).ToArray(),
        r.FixedSlots.Select(x => new FixedSubjectSlot(x.Day, x.Period)).ToArray(), r.Rooms.Select(x => x.RoomId).ToArray(),
        r.Rooms.Where(x => x.IsPreferred).Select(x => (int?)x.RoomId).FirstOrDefault());
}
