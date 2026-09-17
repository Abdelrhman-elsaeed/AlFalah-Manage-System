using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlFalah.Application.DTOs.Timetables;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Application.IntelligentTimetable.Handlers;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.IntelligentTimetable;

public sealed class TimetableReviewService(ITimetableReviewRepository repository, TimetableValidationEngine validator,
    TimetableRepairEngine repair, ICurrentUserService user)
{
    private bool CanManage => user.HasPermission(PermissionNames.TimetableManage) || user.IsGlobalAdmin() || user.IsInRole(RoleNames.SchoolManager);
    private bool CanOverride => CanManage && (user.IsGlobalAdmin() || user.IsInRole(RoleNames.SchoolManager));
    private int School(bool manage = false)
    {
        if (!user.IsAuthenticated || string.IsNullOrWhiteSpace(user.UserId) || user.ActiveSchoolId is null ||
            TimetableSettingsHandlerSupport.IsExcludedRole(user) ||
            !(CanManage || !manage && user.HasPermission(PermissionNames.TimetableReview)))
            throw new UnauthorizedAccessException(TimetableSettingsHandlerSupport.PermissionDenied);
        return user.ActiveSchoolId.Value;
    }
    public Task<IReadOnlyList<ReviewTimetableOption>> ListAsync(CancellationToken ct) => repository.GetTimetablesAsync(School(), ct);
    private async Task<TimetableValidationContext> Context(int id, CancellationToken ct) =>
        await repository.GetValidationContextAsync(School(), id, ct) ?? throw new KeyNotFoundException();
    public Task<TimetableReviewResultDto> EvaluateAsync(int id, CancellationToken ct) => repository.ExecuteAsync(async token => {
        var c = await Context(id, token); return Map(c, await Analyze(c, false, token));
    }, ct);
    public Task<List<RepairProposalDto>> ProposalsAsync(int id, int findingId, CancellationToken ct) => repository.ExecuteAsync(async token => {
        School(true); var c = await Context(id, token); var finding = await CurrentFinding(c, findingId, token);
        return repair.Propose(c, finding.AnalysisRun, finding, token);
    }, ct);
    public Task<bool> OverrideAsync(int findingId, string reason, CancellationToken ct) => repository.ExecuteAsync(async token => {
        School(true); if (!CanOverride) throw new UnauthorizedAccessException(TimetableSettingsHandlerSupport.PermissionDenied);
        var finding = await repository.GetFindingByIdAsync(School(), findingId, token) ?? throw new KeyNotFoundException();
        var c = await Context(finding.AnalysisRun.SchoolTimetableId, token);
        await CurrentFinding(c, findingId, token);
        Override(finding, user.UserId!, reason);
        Audit(c, "Timetable.Review.SoftOverride", new { FindingId = finding.Id, finding.OverrideReason, finding.AnalysisRunId });
        await repository.SaveAsync(token); return true;
    }, ct);
    public static void Override(TimetableAnalysisFinding finding, string actor, string reason)
    {
        if (finding.Severity != ViolationSeverity.Warning) throw new ArgumentException("لا يمكن تجاوز الأخطاء الحرجة مطلقاً.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000) throw new ArgumentException("اكتب سبباً للتجاوز لا يتجاوز 1000 حرف.");
        if (finding.IsOverridden) throw new BellScheduleConflictException("Finding already overridden");
        finding.IsOverridden = true; finding.OverrideReason = reason.Trim();
        finding.OverriddenByUserId = actor; finding.OverriddenAt = DateTimeOffset.UtcNow;
    }
    public Task<TimetableReviewResultDto> ApplyAsync(int id, RepairProposalDto proposal, CancellationToken ct) => repository.ExecuteAsync(async token => {
        School(true); var c = await Context(id, token); var finding = await CurrentFinding(c, proposal.FindingId, token);
        EnsureDraft(c.Timetable);
        if (proposal.AnalysisRunId != finding.AnalysisRunId || proposal.TimetableRevision != c.Timetable.Revision)
            throw new BellScheduleConflictException("Stale proposal");
        var accepted = repair.Propose(c, finding.AnalysisRun, finding, token).SingleOrDefault(x => x.Id == proposal.Id)
            ?? throw new ArgumentException("الاقتراح غير صالح الآن؛ اطلب اقتراحات جديدة.");
        if (proposal.Movements is null || !accepted.Movements.SequenceEqual(proposal.Movements))
            throw new ArgumentException("تغيرت تفاصيل الاقتراح؛ اطلب اقتراحات جديدة.");
        await repository.StageMovementsAsync(c.Timetable, accepted.Movements, token);
        Advance(c); c.Timetable.IsPublished = false; c.Timetable.PublishedAt = null; c.Timetable.PublishedByUserId = null;
        await Snapshot(c, TimetableChangeKind.Saved, token);
        Audit(c, "Timetable.Review.RepairApplied", accepted);
        return Map(c, await Analyze(c, true, token));
    }, ct);
    public Task<TimetableReviewResultDto> PublishAsync(int id, int revision, CancellationToken ct) => repository.ExecuteAsync(async token => {
        School(true); var c = await Context(id, token);
        if (c.Timetable.Revision != revision) throw new BellScheduleConflictException("Stale timetable");
        var run = await Analyze(c, false, token);
        if (run.HardViolationCount > 0 || !c.Timetable.Entries.Any(x => !x.IsDeleted && x.EntryType == TimetableEntryType.Lesson))
            throw new ArgumentException("تمنع الأخطاء الحرجة نشر الجدول. أصلح الأخطاء وأعد المراجعة.");
        // Warnings remain visible and do not invalidate the timetable; management may record an override.
        if (!c.Timetable.IsPublished)
        {
            Advance(c); c.Timetable.IsPublished = true; c.Timetable.PublishedAt = DateTimeOffset.UtcNow; c.Timetable.PublishedByUserId = user.UserId;
            await Snapshot(c, TimetableChangeKind.Published, token);
            var publishedRun = CreateRun(c);
            foreach (var f in publishedRun.Findings)
            {
                var original = run.Findings.FirstOrDefault(x => x.Severity == ViolationSeverity.Warning && x.IsOverridden && x.EvidenceJson == f.EvidenceJson);
                if (original is null) continue;
                f.IsOverridden = true; f.OverrideReason = original.OverrideReason; f.OverriddenAt = original.OverriddenAt; f.OverriddenByUserId = original.OverriddenByUserId;
            }
            repository.AddAnalysis(publishedRun); run = publishedRun;
            Audit(c, "Timetable.Review.Published", new { c.Timetable.Revision, AnalyzedRevision = run.TimetableRevision });
            await repository.SaveAsync(token);
        }
        return Map(c, run);
    }, ct);
    private void Advance(TimetableValidationContext c)
    {
        c.Timetable.Revision++; c.Timetable.UpdatedAt = DateTimeOffset.UtcNow; c.Timetable.UpdatedByUserId = user.UserId!;
        c.Timetable.SetupRevision = c.Setup?.Revision; c.Timetable.TimingsRequireRevalidation = false;
    }
    private async Task<TimetableAnalysisFinding> CurrentFinding(TimetableValidationContext c, int id, CancellationToken ct)
    {
        var finding = await repository.GetFindingByIdAsync(School(), id, ct) ?? throw new KeyNotFoundException();
        if (finding.AnalysisRun.SchoolTimetableId != c.Timetable.Id) throw new KeyNotFoundException();
        var latest = c.Timetable.IsPublished
            ? await repository.GetPublishedSnapshotAnalysisRunAsync(School(), c.Timetable.Id, ct)
            : await repository.GetLatestAnalysisRunAsync(School(), c.Timetable.Id, ct);
        if (latest?.Id != finding.AnalysisRunId || !c.Timetable.IsPublished && !Fresh(c, finding.AnalysisRun))
            throw new BellScheduleConflictException("Stale analysis");
        return finding;
    }
    private bool Fresh(TimetableValidationContext c, TimetableAnalysisRun run) => run.CompletedAt.HasValue &&
        run.TimetableRevision == c.Timetable.Revision && run.SetupRevision == c.Setup?.Revision &&
        run.BellScheduleRevisionId == c.Schedule?.Id && run.AnalyzerVersion == Fingerprint(c);
    private async Task<TimetableAnalysisRun> Analyze(TimetableValidationContext c, bool force, CancellationToken ct)
    {
        if (c.Timetable.IsPublished)
        {
            var frozen = await repository.GetPublishedSnapshotAnalysisRunAsync(School(), c.Timetable.Id, ct);
            if (frozen is not null) return frozen;
        }
        var latest = await repository.GetLatestAnalysisRunAsync(School(), c.Timetable.Id, ct);
        if (!force && latest is not null && Fresh(c, latest)) return latest;
        var run = CreateRun(c); repository.AddAnalysis(run); await repository.SaveAsync(ct); return run;
    }
    private TimetableAnalysisRun CreateRun(TimetableValidationContext c)
    {
        var violations = validator.Evaluate(c);
        return new() { SchoolId = c.Timetable.SchoolId, SchoolTimetableId = c.Timetable.Id, TimetableRevision = c.Timetable.Revision,
            SetupRevision = c.Setup?.Revision, BellScheduleRevisionId = c.Schedule?.Id, RequestedByUserId = user.UserId!,
            StartedAt = DateTimeOffset.UtcNow, CompletedAt = DateTimeOffset.UtcNow, AnalyzerVersion = Fingerprint(c),
            HardViolationCount = violations.Count(x => x.Severity == ViolationSeverity.Error), WarningCount = violations.Count(x => x.Severity == ViolationSeverity.Warning),
            Findings = violations.Select(v => new TimetableAnalysisFinding { RuleCode = v.RuleCode, Severity = v.Severity, MessageAr = v.MessageAr,
                ClassroomId = c.Classrooms.Any(x => x.Id == v.ClassroomId) ? v.ClassroomId : null,
                InstructorProfileId = c.Teachers.Any(x => x.InstructorProfileId == v.InstructorProfileId) ? v.InstructorProfileId : null,
                SubjectId = c.Subjects.Any(x => x.Id == v.SubjectId) ? v.SubjectId : null,
                Day = v.Day, Period = v.Period, EvidenceJson = JsonSerializer.Serialize(v) }).ToList() };
    }
    private string Fingerprint(TimetableValidationContext c)
    {
        // Analyzer version + SHA256 input stamp fit the existing 50-character schema. This is not a quality score.
        var input = JsonSerializer.Serialize(new {
            c.Setup?.Revision, Schedule = c.Schedule?.Id, TemplateRevision = c.Schedule?.Template.Revision,
            Entries = c.Timetable.Entries.Where(x => !x.IsDeleted).OrderBy(x => x.Id).Select(x => new { x.Id, x.Day, x.Period, x.InstructorProfileId, x.ClassroomId, x.SubjectId, x.ClassSubjectRequirementId, x.RoomId, x.EntryType }),
            Teachers = c.Teachers.OrderBy(x => x.Id).Select(x => new { x.Id, x.Revision, x.MaximumWeeklyPeriods, x.BellScheduleRevisionId }),
            Requirements = c.Requirements.OrderBy(x => x.Id).Select(x => new { x.Id, x.Revision, x.IsDeleted }),
            Assignments = c.Assignments.OrderBy(x => x.Id).Select(x => new { x.Id, x.Revision, x.Mode, Members = x.Members.OrderBy(m => m.Id).Select(m => new { m.TeacherTimetableProfileId, m.AllocatedPeriodCount, m.AllocatedPairedBlockCount }) }),
            Violations = validator.Evaluate(c).Select(x => x.Key)
        });
        return "7.0:" + Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
    }
    public async Task<SchoolTimetableVersion> Snapshot(TimetableValidationContext c, TimetableChangeKind kind, CancellationToken ct)
    {
        var entries = c.Timetable.Entries.Where(x => !x.IsDeleted).Select(x => new TimetableEntryDto(x.InstructorProfileId, x.Day, x.Period,
            x.EntryType, x.ClassLabel, x.Subject, x.ClassroomId, x.SubjectId, x.ClassSubjectRequirementId, x.RoomId, Id: x.Id)).ToArray();
        BellPeriodDto Period(BellPeriod p) => new(p.Sequence, p.DisplayLabel, p.StartLocalTime, p.EndLocalTime);
        var s = c.Schedule;
        BellScheduleDto? schedule = s is null ? null : new(s.BellScheduleTemplateId, s.Id, s.SchoolId, c.Timetable.AcademicYearId,
            c.Timetable.Semester, s.Name, s.Revision, s.SchoolTimeZoneId,
            s.Days.FirstOrDefault(x => x.Day == 0)?.Periods.Select(Period).ToArray() ?? [],
            s.Days.Where(x => x.Day > 0).Select(d => new BellScheduleDayDto(d.Day, d.IsStudyDay, false,
                TimetableValidationEngine.Periods(s, d.Day).Select(Period).ToArray(), false,
                TimetableValidationEngine.Breaks(s, d.Day).Select(b => new ScheduleBreakDto(b.Name, b.Category, b.Window.StartLocalTime, b.Window.EndLocalTime)).ToArray())).ToArray(), []);
        var version = new SchoolTimetableVersion { SchoolTimetableId = c.Timetable.Id, VersionNumber = await repository.NextVersionAsync(c.Timetable.Id, ct),
            ChangeKind = kind, Title = c.Timetable.Title, CreatedByUserId = user.UserId!,
            SnapshotJson = JsonSerializer.Serialize(new TimetableSnapshotDto(c.Timetable.Title, entries, schedule)) };
        repository.AddVersion(version);
        return version;
    }
    private static void EnsureDraft(SchoolTimetable timetable)
    {
        if (timetable.IsPublished)
            throw new InvalidOperationException("الجدول المنشور لقطة ثابتة؛ أنشئ مسودة جديدة لإجراء التعديلات.");
    }
    private void Audit(TimetableValidationContext c, string action, object evidence) => repository.AddAudit(new() {
        SchoolId = c.Timetable.SchoolId, UserId = user.UserId!, Action = action, EntityName = nameof(SchoolTimetable),
        EntityId = c.Timetable.Id.ToString(), NewValues = JsonSerializer.Serialize(evidence), CreatedAt = DateTimeOffset.UtcNow });
    private TimetableReviewResultDto Map(TimetableValidationContext c, TimetableAnalysisRun run)
    {
        string? Teacher(int? id) => c.Teachers.Where(x => x.InstructorProfileId == id).Select(x => (x.Instructor.User.FirstName + " " + x.Instructor.User.LastName).Trim()).FirstOrDefault();
        string? Classroom(int? id) => c.Classrooms.FirstOrDefault(x => x.Id == id)?.ClassLabel;
        string? Subject(int? id) => c.Subjects.FirstOrDefault(x => x.Id == id)?.Name;
        var entries = c.Timetable.Entries.Where(x => !x.IsDeleted).Select(x => new ReviewEntryDto(x.Id, x.InstructorProfileId, x.ClassroomId, x.SubjectId,
            Teacher(x.InstructorProfileId) ?? "معلم غير صالح", Classroom(x.ClassroomId), Subject(x.SubjectId), x.Day, x.Period, x.RoomId)).ToArray();
        return new(run.Id, c.Timetable.Id, c.Timetable.Title, run.TimetableRevision, run.SetupRevision, run.BellScheduleRevisionId,
            c.Timetable.IsPublished, run.CompletedAt, run.HardViolationCount, run.WarningCount,
            run.HardViolationCount == 0 && c.Timetable.Entries.Any(x => !x.IsDeleted && x.EntryType == TimetableEntryType.Lesson),
            CanManage, CanOverride, run.Findings.OrderBy(x => x.Severity).ThenBy(x => x.RuleCode).Select(f => new ValidationFindingDto(f.Id, f.RuleCode, RuleName(f.RuleCode), f.Severity,
                f.MessageAr, f.ClassroomId, Classroom(f.ClassroomId), f.InstructorProfileId, Teacher(f.InstructorProfileId), f.SubjectId, Subject(f.SubjectId),
                f.Day, f.Period, f.IsOverridden, f.OverrideReason, f.OverriddenByUserId, f.OverriddenAt,
                !c.Timetable.IsPublished && f.Severity == ViolationSeverity.Error && JsonSerializer.Deserialize<ValidationViolation>(f.EvidenceJson ?? "null")?.EntryIds.Length > 0)).ToArray(),
            entries, Enumerable.Range(1, 7).SelectMany(day => TimetableValidationEngine.Periods(c.Schedule, day).Select(p => new ReviewPeriodDto(day, p.Sequence, p.StartLocalTime.ToString("HH:mm"), p.EndLocalTime.ToString("HH:mm")))).ToArray(),
            c.Teachers.SelectMany(t => t.Slots.Where(s => !s.IsAvailable).Select(s => new ReviewUnavailableDto(t.InstructorProfileId, s.Day, s.Period.Sequence))).ToArray(),
            c.Teachers.Select(x => new ReviewOption(x.InstructorProfileId, Teacher(x.InstructorProfileId)!)).ToArray(),
            c.Classrooms.Select(x => new ReviewOption(x.Id, x.ClassLabel)).ToArray(), c.Subjects.Select(x => new ReviewOption(x.Id, x.Name)).ToArray(),
            Enumerable.Range(1, 7).SelectMany(day => TimetableValidationEngine.Breaks(c.Schedule, day)
                .Select(b => new ReviewBreakDto(day, b.Name, b.Window.StartLocalTime.ToString("HH:mm"), b.Window.EndLocalTime.ToString("HH:mm")))).ToArray());
    }
    public static string RuleName(ViolationRuleCode code) => code switch {
        ViolationRuleCode.TeacherDoubleBooking => "تعارض المعلم", ViolationRuleCode.ClassroomDoubleBooking => "تعارض الفصل",
        ViolationRuleCode.RoomDoubleBooking => "تعارض القاعة", ViolationRuleCode.UnavailableSlot => "عدم إتاحة المعلم",
        ViolationRuleCode.NonStudyDayPlacement => "توقيت غير صالح", ViolationRuleCode.MissingAssignment => "إسناد مفقود أو غير صالح",
        ViolationRuleCode.ScheduleCountMismatch => "عدم تطابق النصاب", ViolationRuleCode.BrokenPairedBlock => "حصة مزدوجة مقسمة",
        ViolationRuleCode.DisallowedDay => "يوم غير مسموح", ViolationRuleCode.ViolatedFixedSlot => "موعد ثابت غير محقق",
        ViolationRuleCode.ExcessiveConsecutive => "حصص متتالية زائدة", ViolationRuleCode.UnfairLastPeriods => "عدم عدالة الحصص الأخيرة",
        ViolationRuleCode.ExcessiveGaps => "فترات انتظار زائدة", ViolationRuleCode.SubjectConcentration => "تكرار المادة في اليوم",
        ViolationRuleCode.EarlyPreferenceMissed => "أفضلية مبكرة غير محققة", _ => "عدم توازن توزيع المادة" };
}
