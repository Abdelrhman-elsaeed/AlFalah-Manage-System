using System.Text.Json;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities.Duty;

namespace AlFalah.Application.Duty;

public sealed class DutyService(IDutyRepository repo, ICurrentUserService current) : IDutyService
{
    private async Task<DutyState> Load(CancellationToken ct)
    {
        var state = await repo.LoadAsync(SchoolId, ct);
        var duplicateNames = state.Teachers.GroupBy(x => x.Name).Where(x => x.Count() > 1).Select(x => x.Key).ToHashSet();
        for (var i = 0; i < state.Teachers.Count; i++)
        {
            var teacher = state.Teachers[i];
            if (duplicateNames.Contains(teacher.Name)) state.Teachers[i] = teacher with { Name = $"{teacher.Name} · #{teacher.ProfileId}" };
            state.Names[teacher.UserId] = state.Teachers[i].Name;
        }
        return state;
    }
    private static DutyException Invalid(string code, string ar) => new(code, ar);
    private static string? ChangeKind(DutyState state, DutyAssignment assignment, bool manager)
    {
        var kind = state.Changes.LastOrDefault(c => (c.AssignmentId == assignment.Id || c.OtherAssignmentId == assignment.Id)
            && c.Kind is "Regenerate" or "AutoExchange" or "UnresolvedCompensation" or "Undo" or "Replace" or "Exchange" or "ResolveCompensation")?.Kind;
        if (manager) return kind;
        return kind switch
        {
            "AutoExchange" or "UnresolvedCompensation" => "AutoExchange",
            "Replace" or "Exchange" or "ResolveCompensation" => "Manual",
            "Regenerate" => "Regenerate",
            "Undo" => "Adjusted",
            _ => null
        };
    }
    private static DutyException Conflict() => new("DutyRevisionConflict", "تغيّر الجدول. حدّث البيانات ثم راجع التعديل. / Schedule changed; refresh and review.", 409);
    private int SchoolId => current.ActiveSchoolId ?? throw new DutyException("DutySchoolRequired", "اختر المدرسة الحالية. / Select an active school.", 403);
    private string UserId => current.UserId ?? throw new DutyException("DutyUnauthorized", "يجب تسجيل الدخول. / Sign in.", 403);
    private async Task<DutyAccess> Access(bool manage, CancellationToken ct)
    {
        var access = await repo.AccessAsync(SchoolId, UserId, ct);
        if (access is null || !(access.Manager || access.Teacher) || (manage && !access.Manager))
            throw new DutyException("DutyForbidden", "إدارة المناوبة متاحة لمدير المدرسة الحالي فقط. / Duty management requires the current school manager.", 403);
        // Read the live database capability; refreshed role assignment takes effect immediately.
        return access;
    }
    public async Task<DutyWorkspace> WorkspaceAsync(int? termId, CancellationToken ct)
    {
        var access = await Access(false, ct);
        var terms = await repo.TermsAsync(SchoolId, ct);
        var term = termId.HasValue ? terms.SingleOrDefault(x => x.Id == termId) : terms.FirstOrDefault();
        if (termId.HasValue && term is null) throw new DutyException("DutyNotFound", "الفصل غير متاح. / Term unavailable.", 404);
        var state = await Load(ct);
        var plan = state.Plans.SingleOrDefault(x => x.AcademicTermId == term?.Id);
        var timing = term is null ? new DutyTiming(null, "Africa/Cairo", []) : await repo.TimingAsync(SchoolId, term, ct);
        var today = Today(plan?.TimeZoneId ?? timing.Zone);
        var settings = plan is null ? null : DutyRotation.Settings(plan);
        string Name(string id) => state.Names.GetValueOrDefault(id) ?? state.Teachers.FirstOrDefault(x => x.UserId == id)?.Name ?? id;
        var days = state.Assignments.Where(x => x.DutyPlanId == plan?.Id && !x.IsCancelled).Select(x => new DutyDay(x.Id, x.Date,
            x.OriginalTeacherId, Name(x.OriginalTeacherId), x.EffectiveTeacherId, Name(x.EffectiveTeacherId), x.Revision,
            x.IsManuallyProtected, x.IsLocked, access.Manager ? x.ExceptionCode ?? (x.Date >= today && settings is not null
                && (!state.Teachers.Any(t => t.UserId == x.EffectiveTeacherId) || !DutyRotation.Eligible(settings, x.EffectiveTeacherId, x.Date)
                    || !DutyRotation.Operating(settings, x.Date)) ? "EligibilityReview" : null) : null,
            access.Manager ? state.Attendance.FirstOrDefault(a => a.UserId == x.EffectiveTeacherId && a.AttendanceDate == x.Date)?.Status.ToString() ?? "Unknown" : null,
            x.EffectiveTeacherId == UserId, ChangeKind(state, x, access.Manager))).ToArray();
        var history = access.Manager ? state.Changes.Where(x => x.DutyPlanId == plan?.Id || DutyRotation.Unresolved(x, state.Changes)).OrderByDescending(x => x.Id)
            .Select(x => new DutyHistory(x.Id, x.Kind, x.Date, x.OtherDate, Name(x.BeforeTeacherId), Name(x.AfterTeacherId),
                x.OtherBeforeTeacherId is null ? null : Name(x.OtherBeforeTeacherId), x.OtherAfterTeacherId is null ? null : Name(x.OtherAfterTeacherId),
                x.Reason, x.ActorUserId == "system" ? "system" : Name(x.ActorUserId), x.CreatedAt, x.RelatedChangeId, DutyRotation.Unresolved(x, state.Changes), x.BeforeTeacherId, x.AfterTeacherId)).ToArray() : [];
        var teachers = state.Teachers.ToList();
        if (access.Manager && settings is not null)
            foreach (var id in settings.TeacherIds.Where(id => !teachers.Any(x => x.UserId == id)))
                teachers.Add(new(plan!.Participants.FirstOrDefault(x => x.UserId == id)?.InstructorProfileId ?? 0, id, Name(id), false));
        return new(access.Manager, access.School, today, UserId, terms, term?.Id, plan?.Revision ?? 0,
            access.Manager && plan is not null ? DutyRotation.Settings(plan) : null,
            access.Manager ? teachers : [], timing.Weekdays, days, history,
            access.Manager ? Fairness(state, plan?.Id, today) : [], access.Manager ? await repo.PendingCountAsync(SchoolId, ct) : 0,
            plan?.Closures.Select(x => new DutyClosure(x.From, x.To, access.Manager ? x.Reason : "")).ToArray() ?? [],
            state.Executions.Where(x => x.AuthorUserId == UserId).OrderByDescending(x => x.Date).ToArray());
    }
    internal static DateOnly Today(string zone) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(zone)).DateTime);
    internal static IReadOnlyList<DutyFairness> Fairness(DutyState state, int? planId, DateOnly today)
    {
        var selected = state.Plans.FirstOrDefault(p => p.Id == planId)?.Participants.Select(p => p.UserId).ToHashSet() ?? [];
        var relevantIds = state.Assignments.Where(a => a.DutyPlanId == planId).SelectMany(a => new[] { a.OriginalTeacherId, a.EffectiveTeacherId })
            .Concat(state.Changes.Where(c => DutyRotation.Unresolved(c, state.Changes)).Select(c => c.BeforeTeacherId)).Distinct();
        var display = state.Teachers.ToList();
        foreach (var id in relevantIds.Where(id => !display.Any(t => t.UserId == id)))
            display.Add(new(0, id, state.Names.GetValueOrDefault(id) ?? id, false));
        return display.Select(t => new DutyFairness(t.UserId, t.Name,
        state.Assignments.Count(x => x.DutyPlanId == planId && !x.IsCancelled && x.OriginalTeacherId == t.UserId),
        state.Assignments.Count(x => x.DutyPlanId == planId && !x.IsCancelled && x.EffectiveTeacherId == t.UserId),
        state.Assignments.Count(a => a.DutyPlanId == planId && !a.IsCancelled && a.EffectiveTeacherId == t.UserId
            && state.Changes.Any(x => x.AssignmentId == a.Id && x.Kind is "AutoExchange" or "UnresolvedCompensation"
                && !state.Changes.Any(u => u.Kind == "Undo" && u.RelatedChangeId == x.Id))),
        state.Executions.Count(x => x.AuthorUserId == t.UserId && x.Submitted && state.Assignments.Any(a => a.Id == x.AssignmentId && a.DutyPlanId == planId)),
        state.Changes.Count(x => x.BeforeTeacherId == t.UserId && (DutyRotation.Unresolved(x, state.Changes) ||
            x.Kind == "AutoExchange" && x.OtherDate >= today && state.Assignments.Any(a => a.Id == x.OtherAssignmentId && !a.IsCancelled && a.EffectiveTeacherId == t.UserId)
            && !state.Changes.Any(u => u.Kind == "Undo" && u.RelatedChangeId == x.Id)))))
            .Where(f => selected.Contains(f.UserId) || f.Planned > 0 || f.Effective > 0 || f.Covered > 0 || f.PendingCompensation > 0).ToArray();
    }

    private async Task<(DutyState State, DutyPlan? Plan, DutyTiming Timing, DutySettings Settings)> Validate(DutySettings s, int revision, CancellationToken ct)
    {
        await Access(true, ct);
        var term = (await repo.TermsAsync(SchoolId, ct)).SingleOrDefault(x => x.Id == s.TermId)
            ?? throw new DutyException("DutyNotFound", "الفصل غير متاح. / Term unavailable.", 404);
        var state = await Load(ct);
        var plan = state.Plans.SingleOrDefault(x => x.AcademicTermId == s.TermId);
        if ((plan?.Revision ?? 0) != revision) throw Conflict();
        if (s.From < term.From || s.To > term.To || s.From > s.To || s.To.DayNumber - s.From.DayNumber > 400)
            throw Invalid("DutyDateBounds", "اختر فترة داخل حدود الفصل والسنة. / Dates must lie inside the term and year.");
        if (s.TeacherIds.Distinct().Count() != s.TeacherIds.Count || s.TeacherIds.Any(id => !state.Teachers.Any(t => t.UserId == id)))
            throw Invalid("DutyInvalidPool", "اختر معلمين نشطين من المدرسة دون تكرار. / Select distinct active school teachers.");
        // Legacy selected pools remain explicit exclusions when reviewed, never silently rewritten.
        var excluded = s.PermanentlyExcludedTeacherIds ?? (s.TeacherIds.Count == 0 ? [] :
            state.Teachers.Where(t => !s.TeacherIds.Contains(t.UserId)).Select(t => t.UserId).ToArray());
        var previousExcluded = plan is null ? [] : DutyRotation.Settings(plan).PermanentlyExcludedTeacherIds ?? [];
        if (excluded.Distinct().Count() != excluded.Count || excluded.Any(id => !state.Names.ContainsKey(id)
            || !state.Teachers.Any(t => t.UserId == id) && !(plan?.Participants.Any(t => t.UserId == id) ?? false) && !previousExcluded.Contains(id)))
            throw Invalid("DutyInvalidExclusion", "اختر معلمًا من المدرسة للاستبعاد. / Exclusions require school teachers.");
        var ids = s.TeacherIds.Concat(state.Teachers.Select(t => t.UserId)).Distinct().Where(id => !excluded.Contains(id)).ToArray();
        if (ids.Length == 0) throw Invalid("DutyInvalidPool", "يلزم بقاء معلم مشارك على الأقل. / Keep at least one participating teacher.");
        var timing = await repo.TimingAsync(SchoolId, term, ct);
        if (timing.Weekdays.Count == 0)
            throw Invalid("DutyStudyCalendarRequired", "اضبط أيام الدراسة في توقيتات الجدول لهذا الفصل أولًا. / Configure this term's timetable study days first.");
        s = s with { TeacherIds = ids, PermanentlyExcludedTeacherIds = excluded, Weekdays = timing.Weekdays };
        if (s.RegenerateFrom < s.From || s.RegenerateTo > s.To || s.RegenerateFrom > s.RegenerateTo)
            throw Invalid("DutyRegenerationRange", "حدّد نطاق إعادة التوليد داخل الفترة. / Select a regeneration range inside operating dates.");
        foreach (var x in s.Exclusions)
            if (!state.Teachers.Any(t => t.UserId == x.UserId) || x.From < s.From || x.To > s.To || x.From > x.To || string.IsNullOrWhiteSpace(x.Reason) || x.Reason.Length > 1000)
                throw Invalid("DutyInvalidExclusion", "راجع المعلم وفترة الاستبعاد والسبب. / Check exclusion teacher, range and reason.");
        foreach (var x in s.Closures)
            if (x.From < s.From || x.To > s.To || x.From > x.To || string.IsNullOrWhiteSpace(x.Reason) || x.Reason.Length > 1000)
                throw Invalid("DutyInvalidClosure", "راجع فترة الإغلاق والسبب. / Check closure range and reason.");
        if (s.Weekdays.Any(x => x is 5 or 6) && !s.AcceptAttendanceCalendarMismatch)
            throw Invalid("DutyAttendanceCalendarMismatch", "الحضور لا يدعم الجمعة والسبت. يلزم إقرار صريح؛ سيظل الحضور غير مؤكد. / Explicitly accept unsupported attendance weekdays.");
        return (state, plan, timing, s);
    }
    private static bool Preserved(DutyAssignment a, DateOnly today, DutyState state) => a.Date < today || a.IsLocked || a.IsManuallyProtected
        || state.Changes.Any(c => (c.AssignmentId == a.Id || c.OtherAssignmentId == a.Id) && c.Kind is "AutoExchange" or "UnresolvedCompensation"
            && !state.Changes.Any(u => u.Kind == "Undo" && u.RelatedChangeId == c.Id));
    private DutyPreview BuildPreview(DutySettings s, DutyState state, DutyPlan? plan, DutyTiming timing)
    {
        var today = Today(plan?.TimeZoneId ?? timing.Zone);
        var old = state.Assignments.Where(x => x.DutyPlanId == plan?.Id).ToDictionary(x => x.Date);
        var dates = DutyRotation.Generate(s).ToDictionary(x => x.Date, x => x.Teacher);
        var result = new List<DutyPreviewDay>();
        var warnings = new List<string>();
        foreach (var date in old.Keys.Union(dates.Keys).Order())
        {
            old.TryGetValue(date, out var a); dates.TryGetValue(date, out var teacher);
            var preserve = a is not null && (Preserved(a, today, state) || date < s.RegenerateFrom || date > s.RegenerateTo);
            if (a is null && (date < today || date < s.RegenerateFrom || date > s.RegenerateTo)) continue;
            if (preserve && a!.IsCancelled) continue;
            if (preserve) teacher = a!.EffectiveTeacherId;
            var exception = preserve && date >= today && (!DutyRotation.Operating(s, date) || !DutyRotation.Eligible(s, teacher!, date)) ? "EligibilityReview"
                : !preserve && DutyRotation.Operating(s, date) && teacher is null ? "NoEligibleTeacher" : null;
            if (exception is not null) warnings.Add(exception == "EligibilityReview" ? "ProtectedEligibilityReview" : "NoEligibleTeacher");
            var name = state.Names.GetValueOrDefault(teacher ?? "") ?? state.Teachers.FirstOrDefault(t => t.UserId == teacher)?.Name ?? teacher ?? "";
            result.Add(new(date, teacher ?? "", name, a is null || a.IsCancelled ? null : state.Names.GetValueOrDefault(a.EffectiveTeacherId) ?? state.Teachers.FirstOrDefault(t => t.UserId == a.EffectiveTeacherId)?.Name ?? a.EffectiveTeacherId, preserve, exception));
        }
        if (s.Weekdays.Any(x => x is 5 or 6)) warnings.Add("AttendanceCalendarMismatch");
        var fairness = s.TeacherIds.Select(id => new DutyFairness(id, state.Teachers.Single(x => x.UserId == id).Name,
            result.Count(x => x.TeacherId == id), result.Count(x => x.TeacherId == id), 0, 0,
            Fairness(state, plan?.Id, today).FirstOrDefault(x => x.UserId == id)?.PendingCompensation ?? 0)).ToArray();
        var digest = DutyRotation.Digest(new { s, TimingRevision = timing.RevisionId, Revision = plan?.Revision ?? 0, Today = today, Days = result });
        var changedDates = result.Count(p => !p.Preserved && (old.TryGetValue(p.Date, out var a)
            ? p.TeacherId.Length == 0 ? !a.IsCancelled : a.IsCancelled || a.EffectiveTeacherId != p.TeacherId
            : p.TeacherId.Length > 0));
        return new(digest, result, fairness, warnings.Distinct().ToArray(), changedDates);
    }
    public async Task<DutyPreview> PreviewAsync(DutyPreviewRequest r, CancellationToken ct)
    {
        var v = await Validate(r.Settings, r.ExpectedRevision, ct); return BuildPreview(v.Settings, v.State, v.Plan, v.Timing);
    }
    public async Task<DutyMutationResult> PublishAsync(DutyPublishRequest r, CancellationToken ct)
    {
        await Access(true, ct);
        return await repo.AtomicAsync(SchoolId, async () =>
        {
            await Access(true, ct);
            var receipt = await ReadReceipt(r.RequestId, r, ct); if (receipt is not null) return receipt;
            var v = await Validate(r.Settings, r.ExpectedRevision, ct);
            var settings = v.Settings;
            var preview = BuildPreview(settings, v.State, v.Plan, v.Timing);
            if (preview.Digest != r.PreviewDigest) throw Conflict();
            if (preview.Days.Any(x => string.IsNullOrEmpty(x.TeacherId) && DutyRotation.Operating(settings, x.Date)))
                throw Invalid("DutyNoEligibleTeacher", "توجد أيام دون معلم مؤهل. راجع الاستبعادات. / Some days have no eligible teacher.");
            var plan = v.Plan;
            if (plan is null)
            {
                plan = new DutyPlan { SchoolId = SchoolId, AcademicTermId = r.Settings.TermId, TimeZoneId = v.Timing.Zone, BellScheduleRevisionId = v.Timing.RevisionId };
                repo.Add(plan); await repo.SaveAsync(ct);
            }
            else plan.Revision++;
            plan.SettingsJson = JsonSerializer.Serialize(settings); plan.BellScheduleRevisionId = v.Timing.RevisionId;
            plan.PublishedAt = DateTimeOffset.UtcNow; plan.PublishedByUserId = UserId;
            foreach (var x in plan.Participants.ToArray()) repo.Remove(x);
            foreach (var x in plan.Closures.ToArray()) repo.Remove(x);
            await repo.SaveAsync(ct); plan.Participants.Clear(); plan.Closures.Clear();
            for (var i = 0; i < settings.TeacherIds.Count; i++)
            {
                var t = v.State.Teachers.Single(x => x.UserId == settings.TeacherIds[i]);
                plan.Participants.Add(new() { SchoolId = SchoolId, DutyPlanId = plan.Id, InstructorProfileId = t.ProfileId, UserId = t.UserId, Position = i });
            }
            foreach (var c in settings.Closures) plan.Closures.Add(new() { SchoolId = SchoolId, DutyPlanId = plan.Id, From = c.From, To = c.To, Reason = c.Reason });
            var affectedTeachers = settings.TeacherIds.ToHashSet();
            foreach (var p in preview.Days)
            {
                var a = v.State.Assignments.SingleOrDefault(x => x.DutyPlanId == plan.Id && x.Date == p.Date);
                if (p.Preserved)
                {
                    if (a is not null && a.Date >= Today(plan.TimeZoneId))
                    {
                        if (p.ExceptionCode is not null) a.ExceptionCode = p.ExceptionCode;
                        else if (a.ExceptionCode == "EligibilityReview") a.ExceptionCode = null;
                        repo.Add(new DutyReconciliationOperation { SchoolId = SchoolId, Date = a.Date });
                    }
                    continue;
                }
                if (a is not null && !a.IsCancelled && (p.TeacherId.Length == 0 || a.EffectiveTeacherId != p.TeacherId))
                    affectedTeachers.Add(a.EffectiveTeacherId);
                if (p.TeacherId == "")
                {
                    if (a is not null && !a.IsCancelled) { a.IsCancelled = true; a.Revision++; AddChange(a, "Regenerate", a.EffectiveTeacherId, a.EffectiveTeacherId, "Operating date closed", null); }
                    continue;
                }
                if (v.State.Assignments.Any(x => x.Date == p.Date && x.DutyPlanId != plan.Id)) throw Invalid("DutyOverlappingTerms", "يتداخل التاريخ مع خطة فصل آخر. / Another term already owns this date.");
                if (a is null) { a = new() { SchoolId = SchoolId, DutyPlanId = plan.Id, Date = p.Date, OriginalTeacherId = p.TeacherId, EffectiveTeacherId = p.TeacherId }; repo.Add(a); }
                else if (a.EffectiveTeacherId != p.TeacherId || a.IsCancelled)
                { var before = a.EffectiveTeacherId; a.EffectiveTeacherId = p.TeacherId; a.IsCancelled = false; a.ExceptionCode = null; a.Revision++; AddChange(a, "Regenerate", before, p.TeacherId, "Future regeneration", null); }
                repo.Add(new DutyReconciliationOperation { SchoolId = SchoolId, Date = a.Date });
            }
            AddChange(new() { SchoolId = SchoolId, DutyPlanId = plan.Id, Date = r.Settings.RegenerateFrom }, "Publish", "", "", "Rotation/settings publication", null, false).SettingsSnapshotJson = plan.SettingsJson;
            foreach (var id in affectedTeachers) repo.Notify(SchoolId, id, $"duty:publish:{r.RequestId}:{id}", "تم نشر جدول المناوبة. / Duty schedule published.");
            return await Receipt(r.RequestId, r, new(plan.Id, plan.Revision), ct);
        }, ct);
    }
    private DutyAssignmentChange AddChange(DutyAssignment a, string kind, string before, string after, string reason, int? related, bool assignment = true)
    {
        var c = new DutyAssignmentChange { SchoolId = SchoolId, DutyPlanId = a.DutyPlanId, AssignmentId = assignment ? a.Id : null,
            Date = a.Date, Kind = kind, BeforeTeacherId = before, AfterTeacherId = after, AfterRevision = a.Revision, Reason = reason, ActorUserId = UserId, RelatedChangeId = related };
        repo.Add(c); return c;
    }
    private async Task<DutyMutationResult?> ReadReceipt<T>(Guid id, T request, CancellationToken ct)
    {
        if (id == Guid.Empty) throw Invalid("DutyRequestIdRequired", "معرّف الطلب مطلوب. / Request ID required.");
        var existing = await repo.ReceiptAsync(SchoolId, id, ct); if (existing is null) return null;
        if (existing.Digest != DutyRotation.Digest(request)) throw new DutyException("DutyIdempotencyConflict", "استُخدم معرّف الطلب لمحتوى آخر. / Request ID reused with different content.", 409);
        return JsonSerializer.Deserialize<DutyMutationResult>(existing.ResultJson);
    }
    private async Task<DutyMutationResult> Receipt<T>(Guid id, T request, DutyMutationResult result, CancellationToken ct)
    {
        repo.Add(new DutyRequestReceipt { SchoolId = SchoolId, RequestId = id, Digest = DutyRotation.Digest(request), ResultJson = JsonSerializer.Serialize(result) });
        await repo.SaveAsync(ct); return result;
    }
    private (DutyAssignment A, DutyAssignment? B, DutyPlan Plan, List<string> Warnings) ValidateChange(DutyState state, DutyChangeRequest r)
    {
        var a = state.Assignments.SingleOrDefault(x => x.Id == r.AssignmentId && !x.IsCancelled) ?? throw new DutyException("DutyNotFound", "المناوبة غير متاحة. / Assignment unavailable.", 404);
        var plan = state.Plans.Single(x => x.Id == a.DutyPlanId);
        if (plan.Revision != r.ExpectedRevision) throw Conflict();
        if (a.Date < Today(plan.TimeZoneId) || a.IsLocked && r.Mode != "Emergency") throw Invalid("DutyLocked", "لا يمكن تعديل مناوبة مقفلة أو ماضية دون مراجعة طارئة. / Past or locked duty requires reviewed emergency handling.");
        if (string.IsNullOrWhiteSpace(r.Reason) || r.Reason.Length > 1000) throw Invalid("DutyReasonRequired", "أدخل سببًا للتعديل. / A reason is required.");
        if (r.Mode is not ("Replace" or "Exchange" or "Unprotect" or "ResolveCompensation" or "Emergency")) throw Invalid("DutyInvalidChange", "نوع التعديل غير صالح. / Invalid change mode.");
        if (r.Mode == "Emergency" && (!r.AcceptException || r.TeacherId == a.EffectiveTeacherId)) throw Invalid("DutyExceptionConfirmation", "راجع تغيير المناوب واحتفاظ التوقيعات السابقة ثم أكّد الاستثناء. / Review replacement and retained signed history, then confirm the exception.");
        if (r.Mode != "Exchange" && r.OtherAssignmentId.HasValue || r.Mode != "ResolveCompensation" && r.ResolveChangeId.HasValue)
            throw Invalid("DutyInvalidChange", "معرّفات التبادل أو التعويض لا تطابق نوع التعديل. / Exchange or obligation IDs do not match this change mode.");
        if (r.Mode == "Unprotect") return (a, null, plan, []);
        if (!state.Teachers.Any(x => x.UserId == r.TeacherId)) throw Invalid("DutyInvalidTeacher", "اختر معلمًا نشطًا من المدرسة. / Select an active school teacher.");
        var b = r.OtherAssignmentId.HasValue ? state.Assignments.SingleOrDefault(x => x.Id == r.OtherAssignmentId && x.DutyPlanId == plan.Id && !x.IsCancelled) : null;
        if (r.Mode == "Exchange" && (b is null || b.Id == a.Id || b.IsLocked || b.Date < Today(plan.TimeZoneId) || b.EffectiveTeacherId != r.TeacherId)) throw Invalid("DutyInvalidExchange", "اختر تاريخ تبادل صالحًا للمعلم البديل. / Select a valid replacement teacher duty date.");
        if (r.Mode == "ResolveCompensation" && !state.Changes.Any(x => x.Id == r.ResolveChangeId && x.BeforeTeacherId == r.TeacherId && DutyRotation.Unresolved(x, state.Changes))) throw Invalid("DutyInvalidCompensation", "اختر التزام تعويض مفتوحًا لصاحب المناوبة. / Select an unresolved obligation for this teacher.");
        var warnings = new List<string>();
        bool Unavailable(string id, DateOnly date) => state.Attendance.Any(x => x.UserId == id && x.AttendanceDate == date && x.Status != AlFalah.Domain.Enums.AttendanceStatus.Present);
        if (r.Mode == "Emergency") warnings.Add("EmergencyHistoryRetention");
        if (!DutyRotation.Eligible(DutyRotation.Settings(plan), r.TeacherId, a.Date) || Unavailable(r.TeacherId, a.Date)) warnings.Add("ManualEligibilityException");
        if (b is not null && (!DutyRotation.Eligible(DutyRotation.Settings(plan), a.EffectiveTeacherId, b.Date) || Unavailable(a.EffectiveTeacherId, b.Date))) warnings.Add("CompensationEligibilityException");
        if (warnings.Count > 0 && !r.AcceptException) throw Invalid("DutyExceptionConfirmation", "يلزم إقرار صريح بتحذير الغياب أو الاستبعاد. / Explicitly accept absence/exclusion warning.");
        return (a, b, plan, warnings);
    }
    private DutyPreview ManualPreview(DutyState state, DutyChangeRequest r)
    {
        var v = ValidateChange(state, r);
        string Name(string id) => state.Names.GetValueOrDefault(id) ?? state.Teachers.FirstOrDefault(x => x.UserId == id)?.Name ?? id;
        var rows = new List<DutyPreviewDay> { new(v.A.Date, r.Mode == "Unprotect" ? v.A.EffectiveTeacherId : r.TeacherId, Name(r.Mode == "Unprotect" ? v.A.EffectiveTeacherId : r.TeacherId), Name(v.A.EffectiveTeacherId), false, null) };
        if (v.B is not null) rows.Add(new(v.B.Date, v.A.EffectiveTeacherId, Name(v.A.EffectiveTeacherId), Name(v.B.EffectiveTeacherId), false, null));
        var beforeTeachers = new[] { v.A.EffectiveTeacherId, v.B?.EffectiveTeacherId };
        var fairness = Fairness(state, v.Plan.Id, Today(v.Plan.TimeZoneId)).Select(f => f with { Effective = f.Effective + rows.Count(x => x.TeacherId == f.UserId) - beforeTeachers.Count(x => x == f.UserId) }).ToArray();
        return new(DutyRotation.Digest(r), rows, fairness, v.Warnings, rows.Count);
    }
    public async Task<DutyPreview> ChangePreviewAsync(DutyChangeRequest r, CancellationToken ct) { await Access(true, ct); return ManualPreview(await Load(ct), r); }
    public async Task<DutyMutationResult> ChangeAsync(DutyChangeRequest r, CancellationToken ct)
    {
        await Access(true, ct);
        return await repo.AtomicAsync(SchoolId, async () =>
        {
            await Access(true, ct);
            var receipt = await ReadReceipt(r.RequestId, r, ct); if (receipt is not null) return receipt;
            var state = await Load(ct); var v = ValidateChange(state, r); var before = v.A.EffectiveTeacherId;
            var c = AddChange(v.A, r.Mode, before, r.Mode == "Unprotect" ? before : r.TeacherId, r.Reason.Trim(), r.ResolveChangeId);
            if (r.Mode == "Emergency") c.SettingsSnapshotJson = JsonSerializer.Serialize(await repo.ExecutionDraftsAsync(SchoolId, v.A.Id, ct));
            v.A.IsManuallyProtected = r.Mode != "Unprotect"; v.A.Revision++; v.A.ExceptionCode = v.Warnings.Count > 0 ? "ManualEligibilityException" : null;
            if (r.Mode != "Unprotect") v.A.EffectiveTeacherId = r.TeacherId;
            if (r.Mode == "Emergency") v.A.IsLocked = false;
            c.AfterRevision = v.A.Revision;
            if (v.B is not null)
            {
                c.OtherAssignmentId = v.B.Id; c.OtherDate = v.B.Date; c.OtherBeforeTeacherId = v.B.EffectiveTeacherId;
                v.B.EffectiveTeacherId = before; v.B.IsManuallyProtected = true; v.B.Revision++; v.B.ExceptionCode = v.Warnings.Count > 0 ? "ManualEligibilityException" : null;
                c.OtherAfterTeacherId = before; c.OtherAfterRevision = v.B.Revision;
            }
            v.Plan.Revision++;
            if (r.Mode == "Unprotect") repo.Add(new DutyReconciliationOperation { SchoolId = SchoolId, Date = v.A.Date });
            foreach (var id in new[] { before, v.A.EffectiveTeacherId }.Distinct()) repo.Notify(SchoolId, id, $"duty:manual:{r.RequestId}:{id}", $"تعديل مناوبة {v.A.Date:yyyy-MM-dd}. / Duty assignment changed.");
            return await Receipt(r.RequestId, r, new(v.Plan.Id, v.Plan.Revision), ct);
        }, ct);
    }
    public async Task RetryAsync(CancellationToken ct)
    {
        await Access(true, ct);
        await repo.AtomicAsync(SchoolId, async () =>
        {
            await Access(true, ct);
            var state = await Load(ct);
            foreach (var a in state.Assignments.Where(x => !x.IsCancelled)) repo.Add(new DutyReconciliationOperation { SchoolId = SchoolId, Date = a.Date });
            return true;
        }, ct);
    }
}
