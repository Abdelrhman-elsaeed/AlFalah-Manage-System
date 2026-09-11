using System.Text.Json;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Application.IntelligentTimetable.Handlers;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Events;

namespace AlFalah.Application.IntelligentTimetable;

public sealed class TimetableSubstitutionService(ITimetableSubstitutionRepository repository, TimetableSwapEngine engine,
    TimetableReviewService review, TimetableValidationEngine validator, ICurrentUserService user, TimeProvider clock)
{
    private bool CanManage => user.HasPermission(PermissionNames.TimetableManage) || user.IsGlobalAdmin() || user.IsInRole(RoleNames.SchoolManager);
    public Task<IReadOnlyList<ReviewTimetableOption>> ListAsync(CancellationToken ct) => repository.GetTimetablesAsync(School(), ct);
    private bool CanOverride => CanManage && (user.IsGlobalAdmin() || user.IsInRole(RoleNames.SchoolManager) || user.IsInRole(RoleNames.Secretary));
    private int School(bool manage = false)
    {
        if (!user.IsAuthenticated || string.IsNullOrWhiteSpace(user.UserId) || user.ActiveSchoolId is null ||
            TimetableSettingsHandlerSupport.IsExcludedRole(user) || !(CanManage || !manage && user.HasPermission(PermissionNames.TimetableView)))
            throw new UnauthorizedAccessException();
        return user.ActiveSchoolId.Value;
    }
    private async Task<TimetableValidationContext> Context(int id, DateOnly date, CancellationToken ct)
    {
        var c = await repository.GetValidationContextAsync(School(), id, ct) ?? throw new KeyNotFoundException();
        if (date < c.Timetable.AcademicYear.StartsOn || date > c.Timetable.AcademicYear.EndsOn)
            throw new ArgumentException("التاريخ خارج العام الدراسي للجدول.");
        return c;
    }
    public Task<DailySubstitutionDto> DailyAsync(int id, DateOnly date, CancellationToken ct) => repository.ExecuteAsync(async token => {
        var c = await Context(id, date, token);
        var changes = await repository.GetChangesAsync(School(), id, date, token);
        var effective = Effective(c, changes);
        var lessons = effective.Timetable.Entries.Where(e => !e.IsDeleted && e.EntryType == TimetableEntryType.Lesson &&
            e.Day == BellScheduleResolver.ToDay(date.DayOfWeek)).Select(e => {
                var block = TimetableSwapEngine.Block(effective, e).Where(x => x.InstructorProfileId == e.InstructorProfileId).ToArray();
                var periods = block.Select(x => x.Period).Distinct().Order().ToArray();
                var windows = TimetableValidationEngine.Periods(c.Schedule, (int)e.Day);
                var t = c.Teachers.FirstOrDefault(t => t.InstructorProfileId == e.InstructorProfileId);
                return new SwapLessonDto(block[0].Id, e.InstructorProfileId, t is null ? "" : $"{t.Instructor.User.FirstName} {t.Instructor.User.LastName}",
                    e.ClassLabel ?? "", e.Subject ?? "", periods, block.Select(x => x.Id).ToArray(),
                    windows.FirstOrDefault(p => p.Sequence == periods[0])?.StartLocalTime.ToString("HH:mm") ?? "",
                    windows.FirstOrDefault(p => p.Sequence == periods[^1])?.EndLocalTime.ToString("HH:mm") ?? "", e.RoomId);
            }).DistinctBy(x => x.EntryId).OrderBy(x => x.Periods[0]).ToArray();
        return new DailySubstitutionDto(id, c.Timetable.Title, c.Timetable.Revision, c.Timetable.IsPublished, CanManage, CanOverride,
            lessons, changes.OrderByDescending(x => x.Id).Select(History).ToArray());
    }, ct);
    public Task<SwapCandidatesDto> CandidatesAsync(int id, DateOnly date, int source, string mode, CancellationToken ct) => repository.ExecuteAsync(async token => {
        School(true); var c = await Context(id, date, token); EnsureOperational(c, date, mode);
        var expires = clock.GetUtcNow().AddMinutes(5);
        return new SwapCandidatesDto(id, c.Timetable.Revision, date, source, mode, expires, CanOverride,
            await Candidates(c, date, source, mode, expires, token));
    }, ct);
    private async Task<IReadOnlyList<SwapCandidateDto>> Candidates(TimetableValidationContext c, DateOnly date, int source, string mode, DateTimeOffset expires, CancellationToken ct)
    {
        var changes = await repository.GetChangesAsync(School(), c.Timetable.Id, null, ct);
        var context = mode == "Substitution" ? Effective(c, changes.Where(x => x.LocalDate == date)) : c;
        var assigned = mode == "Substitution" ? c.Timetable.Entries.ToDictionary(e => e.Id, e => e.InstructorProfileId) : null;
        var candidates = engine.Candidates(context, source, mode, date, expires, assigned, ct);
        if (mode == "Substitution")
        {
            var absent = await repository.GetAbsentTeachersAsync(School(), date, ct);
            candidates = candidates.Select(p => p.Movements.Any(m => absent.Contains(m.ToTeacherId))
                ? p with { Color = "Red", Errors = p.Errors.Append("المعلم مسجل غائباً في هذا اليوم.").ToArray() } : p).ToArray();
        }
        if (mode == "Swap")
        {
            // Structural moves also have to preserve already-confirmed daily cover on every affected date.
            var future = changes.Where(x => x.Kind == "Substitution" && x.LocalDate >= Today(c)).GroupBy(x => x.LocalDate).ToArray();
            candidates = candidates.Select(candidate => {
                var moved = TimetableRepairEngine.Simulate(c, candidate.Movements);
                var findings = future.SelectMany(g => validator.Evaluate(Effective(moved, g), scheduledTeachers: assigned ?? c.Timetable.Entries.ToDictionary(e => e.Id, e => e.InstructorProfileId))).ToArray();
                var errors = candidate.Errors.Concat(findings.Where(v => v.Severity == ViolationSeverity.Error).Select(v => v.MessageAr)).Distinct().ToArray();
                var warnings = candidate.Warnings.Concat(findings.Where(v => v.Severity == ViolationSeverity.Warning).Select(v => v.MessageAr)).Distinct().ToArray();
                var stamp = candidate.Id + JsonSerializer.Serialize(new { errors, warnings });
                return candidate with { Id = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(stamp))),
                    Color = errors.Length > 0 ? "Red" : warnings.Length > 0 ? "Yellow" : "Green", Errors = errors, Warnings = warnings };
            }).ToArray();
        }
        return candidates;
    }
    private DateOnly Today(TimetableValidationContext c) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(),
        TimeZoneInfo.FindSystemTimeZoneById(c.Schedule?.SchoolTimeZoneId ?? "Africa/Cairo")).DateTime);
    private void EnsureOperational(TimetableValidationContext c, DateOnly date, string mode)
    {
        if (date < Today(c)) throw new ArgumentException("لا يمكن تعديل يوم سابق؛ سجل التغييرات متاح للقراءة.");
        if (mode == "Substitution" && !c.Timetable.IsPublished) throw new ArgumentException("الاحتياطي اليومي يتطلب جدولاً منشوراً.");
    }
    public Task<SubstitutionHistoryDto> ExecuteAsync(int id, ExecuteSwapRequest request, CancellationToken ct) => repository.ExecuteAsync(async token => {
        School(true);
        if (request.RequestId == Guid.Empty) throw new ArgumentException("معرف العملية مطلوب.");
        var existing = await repository.FindRequestAsync(School(), request.RequestId, token);
        if (existing is not null)
        {
            if (existing.SchoolTimetableId != id || existing.RequestedByUserId != user.UserId || existing.ProposalId != request.ProposalId ||
                existing.OverrideReason != request.OverrideReason?.Trim()) throw new BellScheduleConflictException("Request id reused");
            return History(existing);
        }
        var c = await Context(id, request.Date, token); EnsureOperational(c, request.Date, request.Mode);
        if (request.Revision != c.Timetable.Revision || request.ExpiresAt <= clock.GetUtcNow() || request.ExpiresAt > clock.GetUtcNow().AddMinutes(5))
            throw new BellScheduleConflictException("Stale proposal");
        var candidate = (await Candidates(c, request.Date, request.SourceEntryId, request.Mode, request.ExpiresAt, token))
            .SingleOrDefault(x => x.Id == request.ProposalId) ?? throw new BellScheduleConflictException("Proposal changed");
        if (candidate.Color == "Red") throw new ArgumentException("لا يمكن تنفيذ تبديل ذي تعارض حرج: " + string.Join("، ", candidate.Errors));
        if (candidate.Color == "Yellow")
        {
            if (!CanOverride) throw new UnauthorizedAccessException();
            if (string.IsNullOrWhiteSpace(request.OverrideReason) || request.OverrideReason.Trim().Length > 1000)
                throw new ArgumentException("يجب تسجيل سبب التجاوز (1–1000 حرف).");
        }
        if (request.OverrideReason?.Length > 1000) throw new ArgumentException("السبب أطول من 1000 حرف.");
        if (candidate.Kind != "Substitution") await repository.StageMovementsAsync(c.Timetable, candidate.Movements, token);
        c.Timetable.Revision++; c.Timetable.UpdatedByUserId = user.UserId!; c.Timetable.UpdatedAt = clock.GetUtcNow();
        var version = await review.Snapshot(c, Enum.Parse<TimetableChangeKind>(candidate.Kind), token);
        var change = new TimetableSubstitution { SchoolId = School(), SchoolTimetableId = id, RequestId = request.RequestId,
            ProposalId = candidate.Id, Kind = candidate.Kind, LocalDate = request.Date, BeforeRevision = request.Revision,
            AfterRevision = c.Timetable.Revision, RequestedByUserId = user.UserId!, ApprovedByUserId = user.UserId!,
            OverrideReason = request.OverrideReason?.Trim(), WarningsJson = JsonSerializer.Serialize(candidate.Warnings), Version = version,
            Movements = candidate.Movements.Select(m => new TimetableSubstitutionMovement { SchoolId = School(), SchoolTimetableEntryId = m.EntryId,
                FromTeacherId = m.FromTeacherId, ToTeacherId = m.ToTeacherId, Day = (int)m.FromDay, FromPeriod = m.FromPeriod, ToPeriod = m.ToPeriod }).ToList() };
        if (c.Timetable.IsPublished)
        {
            var ids = candidate.Movements.SelectMany(m => new[] { m.FromTeacherId, m.ToTeacherId }).ToHashSet();
            change.AppendDomainEvent(new TeacherTimetableChangedEvent(Guid.NewGuid(), School(), 0, id, c.Timetable.Revision, request.Date,
                candidate.Kind, c.Teachers.Where(t => ids.Contains(t.InstructorProfileId)).Select(t => t.Instructor.UserId).Distinct().ToArray(), clock.GetUtcNow()));
        }
        repository.AddChange(change);
        repository.AddAudit(new() { SchoolId = School(), UserId = user.UserId!, Action = "Timetable." + candidate.Kind,
            EntityName = nameof(SchoolTimetable), EntityId = id.ToString(), NewValues = JsonSerializer.Serialize(new { request, candidate }) });
        await repository.SaveAsync(token);
        // The increment invalidates Phase 7 analysis; structural changes also persist a fresh full analysis now.
        if (candidate.Kind != "Substitution") await review.EvaluateAsync(id, token);
        return History(change);
    }, ct);
    public static TimetableValidationContext Effective(TimetableValidationContext c, IEnumerable<TimetableSubstitution> changes)
    {
        var replacements = changes.Where(x => x.Kind == "Substitution").OrderBy(x => x.Id).SelectMany(x => x.Movements)
            .GroupBy(x => x.SchoolTimetableEntryId).ToDictionary(g => g.Key, g => g.Last().ToTeacherId);
        var moves = c.Timetable.Entries.Where(e => replacements.ContainsKey(e.Id)).Select(e => new RepairMovementDto(e.Id, e.Day, e.Period,
            e.InstructorProfileId, e.Day, e.Period, replacements[e.Id])).ToArray();
        var effective = TimetableRepairEngine.Simulate(c, moves);
        if (moves.Length > 0) TimetableSwapEngine.SuppressCoveredStandby(effective);
        return effective;
    }
    private static SubstitutionHistoryDto History(TimetableSubstitution x) => new(x.Id, x.Kind, x.LocalDate, x.BeforeRevision, x.AfterRevision,
        x.RequestedByUserId, x.ApprovedByUserId, x.OverrideReason, x.ConfirmedAt);
}
