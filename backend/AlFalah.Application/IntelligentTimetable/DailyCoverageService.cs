using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Domain.Entities;

namespace AlFalah.Application.IntelligentTimetable;

public sealed class DailyCoverageService(IDailyCoverageRepository repository, DailyCoverageReconciler reconciler,
    CoverageAccess access, TimetableSwapEngine engine, TimetableSubstitutionService substitutions, TimeProvider clock)
{
    public Task<CoverageBoardDto> BoardAsync(DateOnly date, CancellationToken ct) => repository.ExecuteAsync(async token => {
        await access.Manage(token); return await Board(date, token);
    }, ct);
    public Task<CoverageBoardDto> RefreshAsync(DateOnly date, CancellationToken ct) => repository.ExecuteAsync(async token => {
        await access.Manage(token); await reconciler.ReconcileAsync(access.School, date, token); return await Board(date, token);
    }, ct);
    private async Task<CoverageBoardDto> Board(DateOnly date, CancellationToken ct)
    {
        var ids = await repository.PublishedTimetablesAsync(access.School, date, ct);
        var c = ids.Count == 1 ? await repository.GetValidationContextAsync(access.School, ids[0], ct) : null;
        var needs = await repository.NeedsAsync(access.School, date, ct);
        var teachers = c == null ? [] : await Counts(c, needs, ct);
        return new(date, c?.Timetable.Id, c?.Timetable.Revision, true, access.CanOverride,
            c == null ? "يلزم جدول منشور واحد داخل الفصل الدراسي لهذا التاريخ. راجع النشر والتقويم الدراسي." : null,
            clock.GetUtcNow(), needs.Select(Map).ToArray(), teachers);
    }
    public static CoverageNeedDto Map(DailyCoverageNeed n) => new(n.Id, n.Revision, n.SchoolTimetableId, n.SourceEntryId, n.LocalDate,
        n.OriginalTeacherName, n.Classroom, n.Subject, JsonSerializer.Deserialize<int[]>(n.PeriodsJson) ?? [], n.StartsAt, n.EndsAt, n.TimeZoneId,
        n.State, n.IsRequired, n.ActiveSubstitutionId, n.ActiveSubstitution?.Movements.FirstOrDefault()?.ToTeacherId, n.AssignedTeacherName,
        n.ReviewReason, n.AcknowledgedAt, n.SubmittedAt, n.Narrative, n.Notes, n.CorrectionReason);
    private async Task<IReadOnlyList<CoverageTeacherDto>> Counts(TimetableValidationContext c, IReadOnlyList<DailyCoverageNeed> needs, CancellationToken ct)
    {
        var executions = await repository.ExecutionsAsync(access.School, c.Timetable.AcademicYearId, (int)c.Timetable.Semester, ct);
        var actual = executions.GroupBy(r => r.DailyCoverageNeedId).Select(g => g.OrderByDescending(r => r.Id).First()).GroupBy(r => r.ActorUserId)
            .ToDictionary(g => g.Key, g => g.Sum(r => JsonSerializer.Deserialize<int[]>(r.Need.PeriodsJson)!.Length));
        // Pending commitments from the same term, not just today's visible rows.
        var pending = await repository.PendingTermAsync(access.School, c.Timetable.AcademicYearId, (int)c.Timetable.Semester, ct);
        return c.Teachers.Where(t => t.Instructor.SchoolId == access.School && t.Instructor.IsActive && t.Instructor.User.IsActive)
            .Select(t => new CoverageTeacherDto(t.InstructorProfileId, $"{t.Instructor.User.FirstName} {t.Instructor.User.LastName}".Trim(),
                actual.GetValueOrDefault(t.Instructor.UserId), pending.Where(n => n.AssignedUserId == t.Instructor.UserId).Sum(n => JsonSerializer.Deserialize<int[]>(n.PeriodsJson)!.Length))).ToArray();
    }
    public Task<IReadOnlyList<CoverageOptionDto>> OptionsAsync(int needId, CancellationToken ct) => repository.ExecuteAsync(async token => {
        await access.Manage(token); var n = await repository.NeedAsync(access.School, needId, token) ?? throw new KeyNotFoundException();
        var source = await Source(n.LocalDate, token); EnsureDate(source, n.LocalDate);
        var counts = await Counts(source, await repository.NeedsAsync(access.School, n.LocalDate, token), token);
        var absent = await repository.GetAbsentTeachersAsync(access.School, n.LocalDate, token);
        var changes = await repository.GetChangesAsync(access.School, source.Timetable.Id, n.LocalDate, token);
        return Options(source, n, changes, counts, absent, clock.GetUtcNow().AddMinutes(5), token);
    }, ct);
    private IReadOnlyList<CoverageOptionDto> Options(TimetableValidationContext c, DailyCoverageNeed n, IReadOnlyList<TimetableSubstitution> changes,
        IReadOnlyList<CoverageTeacherDto> counts, IReadOnlyList<int> absent, DateTimeOffset expires, CancellationToken ct)
    {
        if (!n.IsRequired || !DailyCoverageReconciler.Matches(n, c) || n.SubmittedAt != null || n.Records.Any(r => r.Kind == "Execution") || n.State == "Withdrawn") return [];
        var effective = TimetableSubstitutionService.Effective(c, changes);
        var assigned = c.Timetable.Entries.ToDictionary(e => e.Id, e => e.InstructorProfileId);
        return engine.Candidates(effective, n.SourceEntryId, "Substitution", n.LocalDate, expires, assigned, ct).Select(p => {
            var id = p.Movements[0].ToTeacherId; var teacher = counts.FirstOrDefault(t => t.Id == id) ?? new(id, p.Label, 0, 0);
            var errors = p.Errors.Concat(absent.Contains(id) ? new[] { "المعلم مسجل غائبًا في هذا اليوم." } : []).ToArray();
            return new CoverageOptionDto(id, teacher.Name, errors.Length > 0 ? "Red" : p.Color, errors, p.Warnings, teacher.ExecutedPeriods, teacher.PendingPeriods);
        }).ToArray();
    }
    private async Task<TimetableValidationContext> Source(DateOnly date, CancellationToken ct)
    {
        var ids = await repository.PublishedTimetablesAsync(access.School, date, ct);
        if (ids.Count != 1) throw new ArgumentException("يلزم جدول منشور واحد لهذا الفصل الدراسي والتاريخ.");
        return await repository.GetValidationContextAsync(access.School, ids[0], ct) ?? throw new KeyNotFoundException();
    }
    private void EnsureDate(TimetableValidationContext c, DateOnly date)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(c.Schedule!.SchoolTimeZoneId)).DateTime);
        if (date < today) throw new ArgumentException("لا يمكن إنشاء تغطية ليوم سابق. السجل متاح للقراءة.");
    }
    public Task<CoveragePreviewDto> PreviewAsync(CoveragePreviewRequest request, CancellationToken ct) => repository.ExecuteAsync(async token => {
        await access.Manage(token); return await Preview(request, token);
    }, ct);
    private async Task<CoveragePreviewDto> Preview(CoveragePreviewRequest request, CancellationToken ct)
    {
        if (request.Mode is not ("Manual" or "Fair") || request.Choices == null || request.Choices.Any(x => x == null) ||
            request.Choices.Count > 500 || request.Choices.GroupBy(x => x.NeedId).Any(g => g.Count() > 1)) throw new ArgumentException("اختيارات التوزيع غير صالحة.");
        if (request.ExpiresAt <= clock.GetUtcNow() || request.ExpiresAt > clock.GetUtcNow().AddMinutes(5)) throw new BellScheduleConflictException("انتهت صلاحية المعاينة؛ أعد التوزيع.");
        var c = await Source(request.Date, ct); EnsureDate(c, request.Date);
        var allNeeds = await repository.NeedsAsync(access.School, request.Date, ct);
        var needs = allNeeds.Where(n => n.SchoolTimetableId == c.Timetable.Id && n.IsRequired && n.SubmittedAt == null && !n.Records.Any(r => r.Kind == "Execution") &&
            (n.ActiveSubstitutionId == null || request.Choices.Any(x => x.NeedId == n.Id))).ToArray();
        if (request.Choices.Any(x => !needs.Any(n => n.Id == x.NeedId))) throw new BellScheduleConflictException("إحدى الحصص تغيرت أو تم اعتماد تنفيذها.");
        var counts = await Counts(c, allNeeds, ct);
        var absent = await repository.GetAbsentTeachersAsync(access.School, request.Date, ct);
        var changes = (await repository.GetChangesAsync(access.School, c.Timetable.Id, request.Date, ct)).ToList();
        if (needs.Any(n => !absent.Contains(n.OriginalTeacherId))) throw new BellScheduleConflictException("تغير كشف الغياب؛ حدّث احتياجات اليوم.");
        var initialOptions = needs.ToDictionary(n => n.Id, n => Options(c, n, changes, counts, absent, request.ExpiresAt, ct));
        var selected = new List<CoverageProposalItemDto>(); var proposalLoad = new Dictionary<int, int>();
        // Explicit choices are commitments in this preview. Reserve them before filling gaps.
        foreach (var n in needs.OrderByDescending(n => request.Choices.Any(x => x.NeedId == n.Id))
            .ThenBy(n => initialOptions[n.Id].Count(o => o.Color != "Red")).ThenBy(n => n.StartsAt).ThenBy(n => n.Id))
        {
            ct.ThrowIfCancellationRequested();
            var options = Options(c, n, changes, counts, absent, request.ExpiresAt, ct);
            var explicitChoice = request.Choices.FirstOrDefault(x => x.NeedId == n.Id);
            var chosen = explicitChoice != null ? options.FirstOrDefault(o => o.TeacherId == explicitChoice.TeacherId) :
                request.Mode == "Fair" ? options.Where(o => o.Color == "Green" || o.Color == "Yellow" && access.CanOverride)
                    .OrderBy(o => o.ExecutedPeriods + o.PendingPeriods + proposalLoad.GetValueOrDefault(o.TeacherId)).ThenBy(o => o.ExecutedPeriods).ThenBy(o => o.TeacherId).FirstOrDefault() : null;
            if (explicitChoice != null && (chosen == null || chosen.Color == "Red" || chosen.Color == "Yellow" && !access.CanOverride))
                throw new ArgumentException("الاختيار اليدوي يتعارض مع الانتظار أو الإتاحة أو تكليف آخر.");
            if (chosen == null) continue;
            selected.Add(new(n.Id, n.Revision, chosen.TeacherId, chosen.Name, chosen.Color, chosen.Warnings));
            var ids = JsonSerializer.Deserialize<int[]>(n.EntryIdsJson)!;
            proposalLoad[chosen.TeacherId] = proposalLoad.GetValueOrDefault(chosen.TeacherId) + ids.Length;
            changes.Add(new() { Id = int.MaxValue - needs.Length + selected.Count, Kind = "Substitution", LocalDate = request.Date,
                Movements = ids.Select(id => { var e = c.Timetable.Entries.Single(e => e.Id == id); return new TimetableSubstitutionMovement { SchoolTimetableEntryId = id,
                    FromTeacherId = e.InstructorProfileId, ToTeacherId = chosen.TeacherId, Day = (int)e.Day, ToDay = (int)e.Day, FromPeriod = e.Period, ToPeriod = e.Period }; }).ToArray() });
        }
        var evidence = JsonSerializer.Serialize(new { request, c.Timetable.Revision, SetupRevision = c.Setup?.Revision, Bell = c.Schedule?.Id,
            Teachers = c.Teachers.Select(t => new { t.Id, t.Revision }), Counts = counts, Absence = absent.Order(), Needs = allNeeds.Select(n => new { n.Id, n.Revision, n.State, n.ActiveSubstitutionId }), Selected = selected });
        return new(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidence))), request.ExpiresAt, request.Mode, selected,
            needs.Where(n => selected.All(s => s.NeedId != n.Id)).Select(n => n.Id).ToArray());
    }
    public Task<CoverageBoardDto> ApproveAsync(CoverageApproveRequest request, CancellationToken ct) => repository.ExecuteAsync(async token => {
        await access.Manage(token);
        if (request.RequestId == Guid.Empty) throw new ArgumentException("معرف العملية مطلوب.");
        var previous = await repository.RecordAsync(access.School, request.RequestId, token);
        if (previous != null)
        {
            if (previous.ActorUserId != access.User || previous.Kind != "BatchApproval" || previous.SnapshotJson != JsonSerializer.Serialize(request)) throw new BellScheduleConflictException("معرف العملية مستخدم لطلب مختلف.");
            return await Board(request.Preview.Date, token);
        }
        var preview = await Preview(request.Preview, token);
        if (preview.ProposalId != request.ProposalId) throw new BellScheduleConflictException("تغير الغياب أو الجدول أو التوزيع؛ أعد المعاينة.");
        if (preview.Assignments.Count == 0) throw new ArgumentException("لا توجد تغطيات مختارة للاعتماد.");
        foreach (var item in preview.Assignments)
        {
            var need = await repository.NeedAsync(access.School, item.NeedId, token) ?? throw new KeyNotFoundException();
            var candidates = await substitutions.CandidatesAsync(need.SchoolTimetableId, need.LocalDate, need.SourceEntryId, "Substitution", token);
            var candidate = candidates.Candidates.SingleOrDefault(p => p.Movements.All(m => m.ToTeacherId == item.TeacherId)) ?? throw new BellScheduleConflictException("تغير بديل إحدى الحصص.");
            var actionId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"{request.RequestId:N}:{item.NeedId}"))[..16]);
            await substitutions.ExecuteAsync(need.SchoolTimetableId, new(actionId, candidates.Revision, need.LocalDate, need.SourceEntryId,
                "Substitution", candidate.Id, candidates.ExpiresAt, request.Reason), token);
        }
        var first = await repository.NeedAsync(access.School, preview.Assignments[0].NeedId, token) ?? throw new KeyNotFoundException();
        var snapshot = JsonSerializer.Serialize(request);
        repository.AddRecord(new() { SchoolId = access.School, Need = first, SubstitutionId = first.ActiveSubstitution?.Id,
            RequestId = request.RequestId, ActorUserId = access.User, Kind = "BatchApproval", Revision = first.Revision, SnapshotJson = snapshot,
            SnapshotHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot))), CreatedAt = clock.GetUtcNow() });
        await repository.SaveAsync(token); return await Board(request.Preview.Date, token);
    }, ct);
}
