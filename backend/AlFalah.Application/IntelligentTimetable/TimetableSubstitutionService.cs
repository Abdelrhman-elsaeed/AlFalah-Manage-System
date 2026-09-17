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
    public async Task<SwapCandidatesDto> CandidatesAsync(int id, DateOnly date, int source, string mode, CancellationToken ct)
    {
        School(true); var c = await Context(id, date, ct); EnsureOperational(c, date, mode);
        var expires = clock.GetUtcNow().AddMinutes(5);
        return new SwapCandidatesDto(id, c.Timetable.Revision, date, source, mode, expires, CanOverride,
            await Candidates(c, date, source, mode, expires, SwapSearchScope.SameDay, ct));
    }
    public async Task<InlineSwapCandidatesDto> InlineCandidatesAsync(int id, DateOnly date, int source,
        SwapSearchScope scope, CancellationToken ct)
    {
        School(true);
        if (!Enum.IsDefined(scope)) throw new ArgumentException("نطاق البحث غير صالح.");
        var c = await Context(id, date, ct);
        EnsureOperational(c, date, "Swap");
        var expires = clock.GetUtcNow().AddMinutes(5);
        var proposals = await Candidates(c, date, source, "Swap", expires, scope, ct);
        var sourceEntry = c.Timetable.Entries.SingleOrDefault(e => !e.IsDeleted && e.Id == source && e.EntryType == TimetableEntryType.Lesson)
            ?? throw new KeyNotFoundException();
        var sourceEntryIds = TimetableSwapEngine.Block(c, sourceEntry).Select(e => e.Id).ToHashSet();
        var cells = ProjectInlineCells(c, sourceEntryIds, proposals);
        return new InlineSwapCandidatesDto(id, c.Timetable.Revision, date, source, sourceEntryIds.Order().ToArray(),
            scope, expires, CanOverride, cells, proposals);
    }
    private async Task<IReadOnlyList<SwapCandidateDto>> Candidates(TimetableValidationContext c, DateOnly date, int source,
        string mode, DateTimeOffset expires, SwapSearchScope scope, CancellationToken ct)
    {
        var changes = await repository.GetChangesAsync(School(), c.Timetable.Id, null, ct);
        var context = mode == "Substitution" ? Effective(c, changes.Where(x => x.LocalDate == date)) : c;
        var assigned = mode == "Substitution" ? c.Timetable.Entries.ToDictionary(e => e.Id, e => e.InstructorProfileId) : null;
        var candidates = engine.Candidates(context, source, mode, date, expires, assigned, ct, scope);
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
            var futureCoveredEntries = future.SelectMany(x => x).SelectMany(x => x.Movements)
                .Select(x => x.SchoolTimetableEntryId).ToHashSet();
            var scheduledTeachers = assigned ?? c.Timetable.Entries.ToDictionary(e => e.Id, e => e.InstructorProfileId);
            var futureChecks = future.Select(group => {
                var effective = Effective(c, group);
                return (Validation: validator.PrepareSwap(effective, "Swap", scheduledTeachers, ct),
                    Teachers: effective.Timetable.Entries.ToDictionary(x => x.Id, x => x.InstructorProfileId));
            }).ToArray();
            candidates = candidates.Select(candidate => {
                var futureEvaluations = futureChecks.Select(check => {
                    // Structural moves keep the date-specific covering teacher. Applying the
                    // movement as a delta after the cover avoids rebuilding a full validator for
                    // every proposal/date pair while producing the same effective timetable.
                    var movements = candidate.Movements.Select(move => {
                        var teacher = check.Teachers.GetValueOrDefault(move.EntryId, move.ToTeacherId);
                        return move with { FromTeacherId = teacher, ToTeacherId = teacher };
                    }).ToArray();
                    return check.Validation.Evaluate(movements);
                }).ToArray();
                var isCrossDay = candidate.Movements.Any(m => m.FromDay != m.ToDay);
                var invalidatesConfirmedCover = scope == SwapSearchScope.WholeTimetable && isCrossDay &&
                    candidate.Movements.Any(m => futureCoveredEntries.Contains(m.EntryId));
                var policyErrors = invalidatesConfirmedCover
                    ? new[] { "لا يمكن نقل حصة بين يومين لأنها مرتبطة باحتياطي مستقبلي مؤكد." }
                    : [];
                var errors = candidate.Errors.Concat(policyErrors).Concat(futureEvaluations.SelectMany(x => x.Errors)).Distinct().ToArray();
                var warnings = candidate.Warnings.Concat(futureEvaluations.SelectMany(x => x.Warnings)).Distinct().ToArray();
                var stamp = candidate.Id + JsonSerializer.Serialize(new { errors, warnings });
                return candidate with { Id = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(stamp))),
                    Color = errors.Length > 0 ? "Red" : warnings.Length > 0 ? "Yellow" : "Green", Errors = errors, Warnings = warnings };
            }).OrderBy(x => x.Color == "Green" ? 0 : x.Color == "Yellow" ? 1 : 2)
                .ThenBy(x => x.Warnings.Length).ToArray();
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
        if (!Enum.IsDefined(request.Scope)) throw new ArgumentException("نطاق البحث غير صالح.");
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
        var candidate = (await Candidates(c, request.Date, request.SourceEntryId, request.Mode, request.ExpiresAt, request.Scope, token))
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
                FromTeacherId = m.FromTeacherId, ToTeacherId = m.ToTeacherId, Day = (int)m.FromDay, ToDay = (int)m.ToDay,
                FromPeriod = m.FromPeriod, ToPeriod = m.ToPeriod }).ToList() };
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
    private static IReadOnlyList<InlineCandidateCellDto> ProjectInlineCells(TimetableValidationContext c,
        IReadOnlySet<int> sourceEntryIds, IReadOnlyList<SwapCandidateDto> proposals)
    {
        var projected = new Dictionary<int, InlineCellProjection>();
        foreach (var proposal in proposals)
        {
            var reason = proposal.Errors.FirstOrDefault() ?? proposal.Warnings.FirstOrDefault();
            var targetEntries = proposal.Movements.Select(m => m.EntryId).Where(id => !sourceEntryIds.Contains(id)).Distinct();
            foreach (var targetEntryId in targetEntries)
            {
                var target = c.Timetable.Entries.Single(e => e.Id == targetEntryId);
                var block = TimetableSwapEngine.Block(c, target);
                var blockIds = block.Select(e => e.Id).Order().ToArray();
                var anchor = blockIds[0];
                foreach (var entry in block)
                {
                    if (!projected.TryGetValue(entry.Id, out var cell))
                    {
                        cell = new InlineCellProjection(anchor, blockIds, entry.InstructorProfileId, entry.Day, entry.Period);
                        projected.Add(entry.Id, cell);
                    }
                    if (proposal.Kind == "DirectSwap")
                    {
                        cell.Color = proposal.Color;
                        cell.DirectProposalId = proposal.Id;
                        cell.ReasonSummary = reason;
                    }
                    else if (!cell.AlternativeProposalIds.Contains(proposal.Id))
                    {
                        cell.AlternativeProposalIds.Add(proposal.Id);
                    }
                }
            }
        }
        return projected.Values.OrderBy(x => x.Day).ThenBy(x => x.Period).ThenBy(x => x.TeacherId)
            .Select(x => new InlineCandidateCellDto(x.AnchorEntryId, x.EntryIds, x.TeacherId, x.Day, x.Period,
                x.Color, x.DirectProposalId, x.AlternativeProposalIds, x.ReasonSummary)).ToArray();
    }
    private sealed class InlineCellProjection(int anchorEntryId, IReadOnlyList<int> entryIds, int teacherId,
        TimetableDay day, int period)
    {
        public int AnchorEntryId { get; } = anchorEntryId;
        public IReadOnlyList<int> EntryIds { get; } = entryIds;
        public int TeacherId { get; } = teacherId;
        public TimetableDay Day { get; } = day;
        public int Period { get; } = period;
        public string Color { get; set; } = "Red";
        public string? DirectProposalId { get; set; }
        public List<string> AlternativeProposalIds { get; } = [];
        public string? ReasonSummary { get; set; }
    }
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
