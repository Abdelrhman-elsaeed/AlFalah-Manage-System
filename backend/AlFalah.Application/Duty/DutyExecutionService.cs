using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities.Duty;

namespace AlFalah.Application.Duty;

public sealed class DutyExecutionService(IDutyRepository duty, IDutyExecutionRepository repo, ICurrentUserService current, IDutyDocumentAssets? assets = null) : IDutyExecutionService
{
    private int School => current.ActiveSchoolId ?? throw Error("DutyForbidden", "اختر المدرسة. / Select a school.", 403);
    private string User => current.UserId ?? throw Error("DutyForbidden", "سجّل الدخول. / Sign in.", 403);
    private static DutyException Error(string code, string text, int status = 400) => new(code, text, status);
    private async Task<DutyAccess> Access(bool manager, CancellationToken ct)
    {
        var access = await duty.AccessAsync(School, User, ct);
        if (access is null || !(access.Manager || access.Teacher) || manager && !access.Manager)
            throw Error("DutyForbidden", "هذه العملية غير متاحة لحسابك. / This action is unavailable to your account.", 403);
        return access;
    }
    private async Task<(DutyAssignment Assignment, DutyPlan Plan, DutyAccess Access)> Read(int id, CancellationToken ct)
    {
        var access = await Access(false, ct);
        var a = await repo.AssignmentAsync(School, id, ct) ?? throw Error("DutyNotFound", "المناوبة غير متاحة. / Duty unavailable.", 404);
        if (!access.Manager && a.EffectiveTeacherId != User && !(await repo.DocumentsAsync(School, id, ct)).Any(x => x.AuthorUserId == User)
            && !(await repo.DraftsAsync(School, id, ct)).Any(x => x.AuthorUserId == User))
            throw Error("DutyForbidden", "يمكنك فتح مناوباتك فقط. / You may open only your own duty.", 403);
        return (a, await repo.PlanAsync(School, a.DutyPlanId, ct), access);
    }
    private async Task<(DutyAssignment A, DutyPlan P)> Own(int id, int revision, CancellationToken ct)
    {
        var (a, p, access) = await Read(id, ct);
        if (!access.Teacher || a.EffectiveTeacherId != User) throw Error("DutyForbidden", "يوقّع المعلم الفعلي بنفسه. / The actual teacher must sign personally.", 403);
        if (a.IsCancelled || a.Revision != revision) throw Error("DutyRevisionConflict", "تغيّرت المناوبة. احتُفظ بإدخالك؛ راجع النسخة الحالية. / Duty changed; your input is retained. Review the current version.", 409);
        var state = await duty.LoadAsync(School, ct);
        var unavailable = await repo.UnavailableAsync(School, User, a.Date, ct);
        var eligible = state.Teachers.Any(x => x.UserId == User) && DutyRotation.Eligible(DutyRotation.Settings(p), User, a.Date);
        if ((!eligible || unavailable) && !(a.IsManuallyProtected && a.ExceptionCode == "ManualEligibilityException"))
            throw Error("DutyAttendanceReview", "يلزم مراجعة المدير لحالة الحضور أو الإسناد. / Manager review of attendance or assignment is required.", 409);
        if (a.ExceptionCode is "AttendanceCorrectionReview" or "ManualAbsenceReview" or "EligibilityReview" or "NoEligibleSubstitute")
            throw Error("DutyAttendanceReview", "المناوبة بانتظار مراجعة المدير. / Duty awaits manager review.", 409);
        return (a, p);
    }
    private async Task<TimeOnly?> Dismissal(DutyAssignment a, DutyPlan p, CancellationToken ct) =>
        await repo.DismissalOverrideAsync(School, a.Id, a.Revision, ct) ?? await repo.DismissalAsync(p, a.Date, ct);
    private static DutyReportContent Empty => new(false, false, [], "", null);
    private static DutyReportContent Content(DutyReportDraft d) => JsonSerializer.Deserialize<DutyReportContent>(d.ContentJson) ?? Empty;
    private static DutyDraftDto DraftDto(DutyReportDraft d) => new(d.Id, d.Version, d.AssignmentRevision, d.AuthorUserId, Content(d), d.SavedAt, d.SubmittedAt, d.CorrectionReason);
    private async Task<DutyLateDto> FactDto(StudentDismissalLateness f, CancellationToken ct) => FactDto(f, await repo.NoticeStatusAsync(School, f.Id, ct));
    private static DutyLateDto FactDto(StudentDismissalLateness f, IReadOnlyList<DutyNoticeStatus> notices) => new(f.Id, f.Revision, f.StudentId, f.EnrollmentId,
        f.ClassroomId, f.StudentName, f.Classroom, f.DutyDate, f.ObservedAt, f.DismissalAt, f.DepartureAt, f.Action, f.Notes, f.IsRetracted, notices);
    public async Task<DutyExecution> GetAsync(int assignment, CancellationToken ct)
    {
        var (a, p, access) = await Read(assignment, ct);
        var docs = await repo.DocumentsAsync(School, a.Id, ct); var drafts = await repo.DraftsAsync(School, a.Id, ct);
        var owns = a.EffectiveTeacherId == User && access.Teacher && !a.IsCancelled;
        var draft = drafts.SingleOrDefault(x => x.AuthorUserId == (access.Manager ? a.EffectiveTeacherId : User));
        var canSeeCurrent = access.Manager || owns;
        var facts = canSeeCurrent ? await repo.FactsAsync(School, a.Id, ct) : [];
        var notices = await repo.NoticeStatusesAsync(School, facts.Select(x => x.Id).ToArray(), ct);
        var lines = facts.Select(f => FactDto(f, notices.GetValueOrDefault(f.Id) ?? [])).ToArray();
        var visible = docs.Where(x => access.Manager || x.AuthorUserId == User).ToArray();
        var state = await duty.LoadAsync(School, ct);
        var prior = state.Changes.Where(x => x.AssignmentId == a.Id && x.Kind == "Emergency" && x.SettingsSnapshotJson is not null)
            .SelectMany(x => JsonSerializer.Deserialize<List<DutyReportDraft>>(x.SettingsSnapshotJson!) ?? [])
            .Where(x => access.Manager || x.AuthorUserId == User).Select(DraftDto)
            .Concat(drafts.Where(x => x != draft && (access.Manager || x.AuthorUserId == User)).Select(DraftDto))
            .DistinctBy(x => new { x.Id, x.Version, x.AssignmentRevision }).ToArray();
        return new(a.Id, a.Revision, a.Date, owns, access.Manager, await repo.HeaderAsync(School, a, p, ct), await Dismissal(a, p, ct),
            await repo.TimingReasonAsync(School, a.Id, a.Revision, ct), owns ? (await repo.SignatureAsync(User, ct))?.SignatureDrawnData : null,
            docs.Any(x => x.Kind == "Letter" && x.AuthorUserId == a.EffectiveTeacherId && x.AssignmentRevision == a.Revision),
            draft is null ? null : DraftDto(draft), lines,
            visible.Select(x => new DutyDocumentSummary(x.Id, x.Kind, x.AssignmentRevision, x.AuthorUserId, x.SignedAt, x.SignatureHash, x.PreviousDocumentId, x.CorrectionReason)).ToArray(),
            prior);
    }
    public async Task<DutyRoster> RosterAsync(DutyRosterQuery query, CancellationToken ct)
    {
        var (a, p, access) = await Read(query.AssignmentId, ct);
        if (!access.Manager && (a.EffectiveTeacherId != User || a.IsCancelled)) throw Error("DutyForbidden", "البحث متاح للمناوب الحالي فقط. / Search requires current duty ownership.", 403);
        if (query.Search?.Length > 100 || query.Page is < 1 or > 10000) throw Error("DutyInvalidSearch", "راجع البحث. / Check your search.");
        return await repo.RosterAsync(School, p, a.Date, query, ct);
    }
    private async Task<T?> Replay<T>(Guid id, object payload, CancellationToken ct) where T : class
    {
        if (id == Guid.Empty) throw Error("DutyRequestIdRequired", "معرّف الطلب مطلوب. / Request ID required.");
        var receipt = await duty.ReceiptAsync(School, id, ct);
        if (receipt is null) return null;
        if (receipt.Digest != DutyRotation.Digest(payload)) throw Error("DutyIdempotencyConflict", "استُخدم الطلب لمحتوى مختلف. / Request reused for different content.", 409);
        return JsonSerializer.Deserialize<T>(receipt.ResultJson);
    }
    private async Task<T> Receipt<T>(Guid id, object payload, T result, CancellationToken ct)
    {
        duty.Add(new DutyRequestReceipt { SchoolId = School, RequestId = id, Digest = DutyRotation.Digest(payload), ResultJson = JsonSerializer.Serialize(result) });
        await duty.SaveAsync(ct); return result;
    }
    private async Task AckRequired(DutyAssignment a, CancellationToken ct)
    {
        if (!(await repo.DocumentsAsync(School, a.Id, ct)).Any(x => x.Kind == "Letter" && x.AuthorUserId == User && x.AssignmentRevision == a.Revision))
            throw Error("DutyAcknowledgementRequired", "اطّلع على الخطاب وأقرّ بالتعهد أولًا. / Acknowledge the responsibility letter first.");
    }
    private async Task ValidateContent(DutyAssignment a, DutyPlan p, DutyReportContent content, bool submitting, CancellationToken ct)
    {
        if (content.Violations is null || content.Violations.Count > 500 || content.Observations is null || content.Observations.Length > 10000
            || content.DutyNotes is null || content.DutyNotes.Length > 2000 || content.SubstituteNotes is null || content.SubstituteNotes.Length > 2000
            || content.NoViolations && content.Violations.Count > 0 || content.Violations.Select(x => x.StudentId).Distinct().Count() != content.Violations.Count)
            throw Error("DutyInvalidReport", "راجع أقسام التقرير. / Check report sections.");
        var roster = await repo.StudentsAsync(School, p, a.Date, content.Violations.Select(x => x.StudentId).ToArray(), ct);
        foreach (var line in content.Violations)
        {
            if (!roster.Any(x => x.StudentId == line.StudentId && x.EnrollmentId == line.EnrollmentId)) throw Error("DutyRosterUnavailable", "الطالب لم يعد متاحًا في الفصل. راجع سجل القيد. / Student enrollment unavailable; review the roster.");
            ValidateText(line.Action, line.Notes);
            ValidateLocal(a.Date, line.Time, p.TimeZoneId);
        }
        var facts = (await repo.FactsAsync(School, a.Id, ct)).Where(x => !x.IsRetracted).ToArray();
        if (content.NoLateStudents && facts.Length > 0) throw Error("DutyInvalidReport", "هناك تأخر انصراف مؤكد. / Confirmed lateness exists.");
        if (!submitting) return;
        if (content.LastDeparture is null || !content.NoLateStudents && facts.Length == 0 || !content.NoViolations && content.Violations.Count == 0)
            throw Error("DutyIncompleteReport", "أكّد الفئات الخالية ووقت مغادرة آخر طالب. / Confirm empty categories and last departure.");
        ValidateLocal(a.Date, content.LastDeparture.Value, p.TimeZoneId);
        var dismissal = await Dismissal(a, p, ct);
        if (dismissal is null) throw Error("DutyDismissalMissing", "وقت الانصراف غير محدد. راجع المدير. / Dismissal timing unavailable; contact your manager.");
        var latest = facts.SelectMany(x => new[] { x.ObservedAt, x.DepartureAt ?? x.ObservedAt }).Concat(content.Violations.Select(x => x.Time)).Append(dismissal.Value).Max();
        if (content.LastDeparture < latest && await repo.TimingReasonAsync(School, a.Id, a.Revision, ct) is null)
            throw Error("DutyDepartureInvalid", "مغادرة آخر طالب تسبق الانصراف أو ملاحظة مسجلة. يلزم استثناء مسبب من المدير. / Last departure precedes dismissal or an observation; a reasoned manager exception is required.");
    }
    private static void ValidateText(string action, string notes)
    {
        if (string.IsNullOrWhiteSpace(action) || action.Length > 1000 || notes is null || notes.Length > 2000)
            throw Error("DutyActionRequired", "أدخل الإجراء المتخذ. / Enter the action taken.");
    }
    private static void ValidateLocal(DateOnly date, TimeOnly time, string zone)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified); var tz = TimeZoneInfo.FindSystemTimeZoneById(zone);
        if (tz.IsInvalidTime(local) || tz.IsAmbiguousTime(local)) throw Error("DutyInvalidLocalTime", "الوقت المحلي غير صالح أو ملتبس. / Invalid or ambiguous local time.");
        if (new DateTimeOffset(local, tz.GetUtcOffset(local)) > DateTimeOffset.UtcNow.AddMinutes(1))
            throw Error("DutyFutureObservation", "لا يمكن تسجيل مشاهدة أو مغادرة مستقبلية. / Observation or departure cannot be in the future.");
    }
    public async Task<DutyExecution> SaveDraftAsync(int assignment, DutyDraftRequest request, CancellationToken ct)
    {
        await Access(false, ct);
        return await duty.AtomicAsync(School, async () =>
        {
            var (a, p) = await Own(assignment, request.AssignmentRevision, ct);
            await ValidateContent(a, p, request.Content, false, ct);
            var d = (await repo.DraftsAsync(School, a.Id, ct)).SingleOrDefault(x => x.AuthorUserId == User);
            if ((d?.Version ?? 0) != request.ExpectedVersion || d?.SubmittedAt is not null)
                throw Error("DutyDraftConflict", "توجد نسخة أحدث. احتُفظ بإدخالك؛ راجع النسختين قبل الحفظ. / A newer version exists; your input is retained. Review both versions.", 409);
            if (d is null) { d = new() { SchoolId = School, AssignmentId = a.Id, AuthorUserId = User }; duty.Add(d); }
            else d.Version++;
            var roster = await repo.StudentsAsync(School, p, a.Date, request.Content.Violations.Select(x => x.StudentId).ToArray(), ct);
            var normalized = request.Content.Violations.Select(line => { var student = roster.Single(x => x.StudentId == line.StudentId && x.EnrollmentId == line.EnrollmentId);
                return line with { StudentName = student.Name, Classroom = student.Classroom }; }).ToArray();
            d.AssignmentRevision = a.Revision; d.ContentJson = JsonSerializer.Serialize(request.Content with { Violations = normalized }); d.SavedAt = DateTimeOffset.UtcNow;
            a.IsLocked = true; await duty.SaveAsync(ct); return await GetAsync(a.Id, ct);
        }, ct);
    }
    private async Task<DutySignedDocument> Sign(DutyAssignment a, DutyPlan p, string kind, DutyReportDraft? draft, object payload, CancellationToken ct)
    {
        var signature = await repo.SignatureAsync(User, ct);
        var data = signature?.SignatureDrawnData;
        if (data is null || !data.StartsWith("data:image/png;base64,", StringComparison.Ordinal) || data.Length > 500 * 1024)
            throw Error("DutySignatureRequired", "احفظ توقيعك في الإعدادات ثم عد إلى المسودة. / Save your signature in settings, then return to your draft.");
        byte[] bytes; try { bytes = Convert.FromBase64String(data[22..]); } catch (FormatException) { throw Error("DutySignatureRequired", "راجع التوقيع المحفوظ. / Review your saved signature."); }
        if (bytes.Length < 33 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) throw Error("DutySignatureRequired", "التوقيع المحفوظ غير صالح. / Saved signature invalid.");
        var at = DateTimeOffset.UtcNow; var header = await repo.HeaderAsync(School, a, p, ct);
        var state = await duty.LoadAsync(School, ct);
        var logo = assets is null ? new DutyLogoSnapshot(null, null, header.LogoUrl is null ? "NotConfigured" : "UnavailableAtSigning")
            : await assets.FreezeLogoAsync(header.LogoUrl, ct);
        var snapshot = JsonSerializer.Serialize(new { TemplateVersion = "school-duty/source-2026-v1", Form = kind == "Report" ? "QF71-B-2-3" : "ResponsibilityLetter",
            Header = header, Logo = logo, a.Date, Day = a.Date.DayOfWeek.ToString(), a.OriginalTeacherId, a.EffectiveTeacherId, AssignmentRevision = a.Revision,
            AuthorUserId = User, SignedAt = at, AccountSignatureUpdatedAt = signature!.UpdatedAt, SignatureDisplayName = header.ActualTeacher,
            SignaturePosition = a.OriginalTeacherId == User ? "OriginalTeacher" : "SubstituteTeacher",
            OriginalTeacherAttendance = state.Attendance.FirstOrDefault(x => x.UserId == a.OriginalTeacherId && x.AttendanceDate == a.Date)?.Status.ToString() ?? "Unknown",
            AssignmentHistory = state.Changes.Where(x => x.AssignmentId == a.Id || x.OtherAssignmentId == a.Id).Select(x => new {
                x.Id, x.Kind, x.BeforeTeacherId, x.AfterTeacherId, x.Reason, x.Date, x.OtherDate, x.CreatedAt }).ToArray(),
            Obligation = "الالتزام بالإشراف واستشعار المسؤولية الكاملة على الطلاب حتى انصراف آخر طالب من المدرسة",
            Handover = "يسلم أصل التقرير إلى وكيل الشؤون التعليمية في اليوم التالي للمناوبة لمتابعة الحالات المتأخرين ودراسة الملاحظات ولتدوين في سجل المناوبة.",
            DismissalAt = await Dismissal(a, p, ct), p.BellScheduleRevisionId, p.TimeZoneId,
            TimingException = await repo.TimingReasonAsync(School, a.Id, a.Revision, ct), Payload = payload });
        var document = new DutySignedDocument { SchoolId = School, AssignmentId = a.Id, AssignmentRevision = a.Revision, AuthorUserId = User,
            Kind = kind, DraftId = draft?.Id, PreviousDocumentId = draft?.PreviousDocumentId, CorrectionReason = draft?.CorrectionReason,
            SnapshotJson = snapshot, SnapshotHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot))), SignatureBytes = bytes, SignatureHash = Convert.ToHexString(SHA256.HashData(bytes)), SignedAt = at };
        duty.Add(document); a.IsLocked = true; await duty.SaveAsync(ct);
        duty.Add(new DutyArchiveIntent { SchoolId = School, DocumentId = document.Id }); return document;
    }
    public async Task<DutyExecution> AcknowledgeAsync(int assignment, DutySignRequest request, CancellationToken ct)
    {
        await Read(assignment, ct); var payload = new { Action = "Acknowledge", assignment, User, request };
        return await duty.AtomicAsync(School, async () =>
        {
            await Read(assignment, ct); var replay = await Replay<DutyExecution>(request.RequestId, payload, ct); if (replay is not null) return replay;
            var (a, p) = await Own(assignment, request.AssignmentRevision, ct);
            if (!(await repo.DocumentsAsync(School, a.Id, ct)).Any(x => x.Kind == "Letter" && x.AuthorUserId == User && x.AssignmentRevision == a.Revision))
                await Sign(a, p, "Letter", null, new { Acknowledgement = "اطلعت وأتعهد" }, ct);
            await duty.SaveAsync(ct); return await Receipt(request.RequestId, payload, await GetAsync(a.Id, ct), ct);
        }, ct);
    }
    public async Task<DutyExecution> SubmitAsync(int assignment, DutySignRequest request, CancellationToken ct)
    {
        await Read(assignment, ct); var payload = new { Action = "Submit", assignment, User, request };
        return await duty.AtomicAsync(School, async () =>
        {
            await Read(assignment, ct); var replay = await Replay<DutyExecution>(request.RequestId, payload, ct); if (replay is not null) return replay;
            var (a, p) = await Own(assignment, request.AssignmentRevision, ct); await AckRequired(a, ct);
            var draft = (await repo.DraftsAsync(School, a.Id, ct)).SingleOrDefault(x => x.AuthorUserId == User);
            if (draft is null || draft.Version != request.DraftVersion || draft.AssignmentRevision != a.Revision || draft.SubmittedAt is not null)
                throw Error("DutyDraftConflict", "راجع المسودة الحالية قبل التوقيع. / Review the current draft before signing.", 409);
            var content = Content(draft); await ValidateContent(a, p, content, true, ct);
            var late = (await repo.FactsAsync(School, a.Id, ct)).Where(x => !x.IsRetracted).ToArray();
            var roster = await repo.StudentsAsync(School, p, a.Date, content.Violations.Select(x => x.StudentId).ToArray(), ct);
            var violations = content.Violations.Select(line => new { Student = roster.Single(x => x.StudentId == line.StudentId && x.EnrollmentId == line.EnrollmentId), Line = line }).ToArray();
            var doc = await Sign(a, p, "Report", draft, new { Content = content, LateStudents = late, Violations = violations }, ct);
            draft.SubmittedAt = doc.SignedAt; draft.Version++; await duty.SaveAsync(ct);
            return await Receipt(request.RequestId, payload, await GetAsync(a.Id, ct), ct);
        }, ct);
    }
    public async Task<DutyExecution> CorrectReportAsync(int assignment, DutyCorrectionRequest request, CancellationToken ct)
    {
        await Access(true, ct); var payload = new { Action = "CorrectReport", assignment, User, request };
        return await duty.AtomicAsync(School, async () =>
        {
            await Access(true, ct); var replay = await Replay<DutyExecution>(request.RequestId, payload, ct); if (replay is not null) return replay;
            var (a, _, _) = await Read(assignment, ct); Reason(request.Reason);
            var draft = (await repo.DraftsAsync(School, a.Id, ct)).SingleOrDefault(x => x.AuthorUserId == a.EffectiveTeacherId);
            if (a.Revision != request.AssignmentRevision || draft?.Version != request.ExpectedVersion || draft.SubmittedAt is null)
                throw Error("DutyDraftConflict", "تغيّر التقرير أو الإسناد. / Report or assignment changed.", 409);
            var previous = (await repo.DocumentsAsync(School, a.Id, ct)).Last(x => x.Kind == "Report" && x.AuthorUserId == draft.AuthorUserId);
            draft.PreviousDocumentId = previous.Id; draft.CorrectionReason = request.Reason.Trim(); draft.SubmittedAt = null;
            draft.Version++; draft.SavedAt = DateTimeOffset.UtcNow; draft.AssignmentRevision = a.Revision;
            Audit(a, "ReportCorrection", request.Reason); await duty.SaveAsync(ct);
            return await Receipt(request.RequestId, payload, await GetAsync(a.Id, ct), ct);
        }, ct);
    }
    private static void Reason(string reason) { if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000) throw Error("DutyReasonRequired", "أدخل سببًا واضحًا. / Enter a clear reason."); }
    private void Audit(DutyAssignment a, string kind, string reason) => duty.Add(new DutyAssignmentChange { SchoolId = School, DutyPlanId = a.DutyPlanId, AssignmentId = a.Id,
        Date = a.Date, BeforeTeacherId = a.EffectiveTeacherId, AfterTeacherId = a.EffectiveTeacherId, AfterRevision = a.Revision, ActorUserId = User, Kind = kind, Reason = reason });
    private async Task AppendFact(StudentDismissalLateness f, string reason, CancellationToken ct)
    {
        duty.Add(new DismissalLatenessRevision { SchoolId = School, FactId = f.Id, Revision = f.Revision, SnapshotJson = JsonSerializer.Serialize(f), Reason = reason, ActorUserId = User, CreatedAt = DateTimeOffset.UtcNow });
        var school = (await duty.AccessAsync(School, User, ct))!.School;
        var departureArabic = f.DepartureAt is { } knownDeparture ? $"غادر الطالب الساعة {knownDeparture:HH:mm}." : "يرجى ترتيب استلام الطالب.";
        var departureEnglish = f.DepartureAt is { } departure ? $"The student departed at {departure:HH:mm}." : "Please arrange pickup.";
        var content = f.IsRetracted
            ? $"تم سحب تسجيل تأخر انصراف {f.StudentName} بتاريخ {f.DutyDate:yyyy-MM-dd}. / The dismissal-lateness record for {f.StudentName} on {f.DutyDate:yyyy-MM-dd} was retracted."
            : $"{school} — {(f.Revision > 1 ? "تصحيح: " : "") }تم رصد تأخر انصراف الطالب {f.StudentName} بتاريخ {f.DutyDate:yyyy-MM-dd} الساعة {f.ObservedAt:HH:mm}، بعد وقت الانصراف {f.DismissalAt:HH:mm}. {departureArabic} "
                + $"/ {(f.Revision > 1 ? "Correction: " : "")}{f.StudentName} was observed awaiting dismissal pickup on {f.DutyDate:yyyy-MM-dd} at {f.ObservedAt:HH:mm}, after dismissal at {f.DismissalAt:HH:mm}. {departureEnglish}";
        duty.Add(new DismissalNoticeBatch { SchoolId = School, FactId = f.Id, Revision = f.Revision, Content = content });
    }
    public async Task<DutyLateDto> ConfirmLateAsync(int assignment, DutyLateRequest request, CancellationToken ct)
    {
        await Read(assignment, ct); var payload = new { Action = "ConfirmLate", assignment, User, request };
        return await duty.AtomicAsync(School, async () =>
        {
            await Read(assignment, ct); var replay = await Replay<DutyLateDto>(request.RequestId, payload, ct); if (replay is not null) return replay;
            var (a, p) = await Own(assignment, request.AssignmentRevision, ct); await AckRequired(a, ct);
            var student = await repo.StudentAsync(School, p, a.Date, request.StudentId, request.EnrollmentId, ct)
                ?? throw Error("DutyRosterUnavailable", "قيد الطالب غير متاح. راجع بيانات الفصل. / Student enrollment unavailable; review the roster.");
            var existing = await repo.ExistingFactAsync(School, student.StudentId, a.Date, ct);
            if (existing is not null) return await Receipt(request.RequestId, payload, await FactDto(existing, ct), ct);
            var dismissal = await Dismissal(a, p, ct) ?? throw Error("DutyDismissalMissing", "يلزم تحديد وقت الانصراف من المدير. / Manager must resolve dismissal timing.");
            ValidateText(request.Action, request.Notes); ValidateLocal(a.Date, request.ObservedAt, p.TimeZoneId);
            if (request.ObservedAt <= dismissal) throw Error("DutyNotDismissalLate", "المشاهدة يجب أن تكون بعد وقت الانصراف. / Observation must be after dismissal.");
            if (request.DepartureAt is { } departure) { ValidateLocal(a.Date, departure, p.TimeZoneId); if (departure < request.ObservedAt) throw Error("DutyDepartureInvalid", "المغادرة تسبق المشاهدة. / Departure precedes observation."); }
            var f = new StudentDismissalLateness { SchoolId = School, StudentId = student.StudentId, EnrollmentId = student.EnrollmentId, ClassroomId = student.ClassroomId,
                AcademicTermId = p.AcademicTermId, DutyDate = a.Date, AssignmentId = a.Id, AssignmentRevision = a.Revision, AuthorUserId = User,
                StudentName = student.Name, Classroom = student.Classroom, ObservedAt = request.ObservedAt, DismissalAt = dismissal,
                BellScheduleRevisionId = p.BellScheduleRevisionId, TimeZoneId = p.TimeZoneId, DepartureAt = request.DepartureAt, Action = request.Action.Trim(), Notes = request.Notes.Trim(), RecordedAt = DateTimeOffset.UtcNow };
            duty.Add(f); a.IsLocked = true; await duty.SaveAsync(ct); await AppendFact(f, "Explicit confirmation", ct);
            Audit(a, "DismissalConfirmed", "Explicit dismissal-lateness confirmation"); await duty.SaveAsync(ct);
            return await Receipt(request.RequestId, payload, await FactDto(f, ct), ct);
        }, ct);
    }
    public async Task<DutyLateDto> CorrectLateAsync(int fact, DutyLateCorrection request, CancellationToken ct)
    {
        await Access(true, ct); var payload = new { Action = "CorrectLate", fact, User, request };
        return await duty.AtomicAsync(School, async () =>
        {
            await Access(true, ct); var replay = await Replay<DutyLateDto>(request.RequestId, payload, ct); if (replay is not null) return replay;
            var f = await repo.FactAsync(School, fact, ct) ?? throw Error("DutyNotFound", "السجل غير متاح. / Record unavailable.", 404);
            Reason(request.Reason); if (f.Revision != request.ExpectedRevision) throw Error("DutyRevisionConflict", "تغيّر السجل. / Record changed.", 409);
            ValidateText(request.Action, request.Notes); ValidateLocal(f.DutyDate, request.ObservedAt, f.TimeZoneId);
            if (request.ObservedAt <= f.DismissalAt) throw Error("DutyNotDismissalLate", "المشاهدة يجب أن تكون بعد الانصراف. / Observation must follow dismissal.");
            if (request.DepartureAt is { } departure) { ValidateLocal(f.DutyDate, departure, f.TimeZoneId); if (departure < request.ObservedAt) throw Error("DutyDepartureInvalid", "المغادرة تسبق المشاهدة. / Departure precedes observation."); }
            f.ObservedAt = request.ObservedAt; f.DepartureAt = request.DepartureAt; f.Action = request.Action.Trim(); f.Notes = request.Notes.Trim(); f.IsRetracted = request.Retract; f.Revision++;
            await AppendFact(f, request.Reason.Trim(), ct);
            var a = (await repo.AssignmentAsync(School, f.AssignmentId, ct))!; Audit(a, "DismissalCorrection", request.Reason);
            // Existing signed reports remain immutable; open a reasoned teacher re-signature draft.
            foreach (var draft in (await repo.DraftsAsync(School, a.Id, ct)).Where(x => x.SubmittedAt is not null && x.AuthorUserId == a.EffectiveTeacherId))
            {
                draft.PreviousDocumentId = (await repo.DocumentsAsync(School, a.Id, ct)).Last(x => x.Kind == "Report" && x.AuthorUserId == draft.AuthorUserId).Id;
                draft.CorrectionReason = request.Reason; draft.SubmittedAt = null; draft.Version++; draft.SavedAt = DateTimeOffset.UtcNow;
            }
            await duty.SaveAsync(ct); return await Receipt(request.RequestId, payload, await FactDto(f, ct), ct);
        }, ct);
    }
    public async Task<DutyExecution> TimingExceptionAsync(int assignment, DutyTimingException request, CancellationToken ct)
    {
        await Access(true, ct); var payload = new { Action = "TimingException", assignment, User, request };
        return await duty.AtomicAsync(School, async () =>
        {
            await Access(true, ct); var replay = await Replay<DutyExecution>(request.RequestId, payload, ct); if (replay is not null) return replay;
            var (a, _, _) = await Read(assignment, ct); Reason(request.Reason);
            if (a.Revision != request.AssignmentRevision) throw Error("DutyRevisionConflict", "تغيّرت المناوبة. / Duty changed.", 409);
            duty.Add(new DutyAssignmentChange { SchoolId = School, DutyPlanId = a.DutyPlanId, AssignmentId = a.Id, Date = a.Date,
                Kind = "TimingException", BeforeTeacherId = a.EffectiveTeacherId, AfterTeacherId = a.EffectiveTeacherId, AfterRevision = a.Revision,
                ActorUserId = User, Reason = request.Reason.Trim(), SettingsSnapshotJson = JsonSerializer.Serialize(request.DismissalAt) });
            var draft = (await repo.DraftsAsync(School, a.Id, ct)).SingleOrDefault(x => x.AuthorUserId == a.EffectiveTeacherId && x.SubmittedAt != null);
            if (draft is not null)
            {
                draft.PreviousDocumentId = (await repo.DocumentsAsync(School, a.Id, ct)).Last(x => x.Kind == "Report" && x.AuthorUserId == draft.AuthorUserId).Id;
                draft.CorrectionReason = request.Reason.Trim(); draft.SubmittedAt = null; draft.Version++; draft.SavedAt = DateTimeOffset.UtcNow;
            }
            await duty.SaveAsync(ct); return await Receipt(request.RequestId, payload, await GetAsync(a.Id, ct), ct);
        }, ct);
    }
    public async Task<DutyDismissalStatistics> StatisticsAsync(DutyDismissalQuery query, CancellationToken ct)
    {
        await Access(true, ct);
        var today = DutyService.Today(await repo.SchoolTimeZoneAsync(School, ct));
        if (query.Page is < 1 or > 10000 || query.From is { } from && query.To is { } to && from > to)
            throw Error("DutyInvalidSearch", "راجع فترة البحث. / Check the date range.");
        return await repo.StatisticsAsync(School, query.From is null && query.To is null && query.TermId is null ? query with { From = today, To = today } : query, ct);
    }
    public async Task RetryNoticesAsync(int fact, CancellationToken ct)
    {
        await Access(true, ct);
        await duty.AtomicAsync(School, async () =>
        {
            await Access(true, ct);
            var f = await repo.FactAsync(School, fact, ct) ?? throw Error("DutyNotFound", "السجل غير متاح. / Record unavailable.", 404);
            foreach (var batch in await repo.FactBatchesAsync(School, f.Id, ct))
                if (batch.State != "Delivered") { batch.NextAttemptAt = DateTimeOffset.UtcNow; batch.State = "Pending"; }
            return true;
        }, ct);
    }
    private async Task<DateOnly> GuardianDate(CancellationToken ct)
    {
        if (!current.IsInRole("Guardian")) throw Error("DutyForbidden", "الإشعارات متاحة لولي الأمر. / Guardian access required.", 403);
        return DutyService.Today(await repo.SchoolTimeZoneAsync(School, ct));
    }
    public async Task<List<DutyGuardianNotice>> GuardianNoticesAsync(CancellationToken ct) => await repo.GuardianNoticesAsync(School, User, await GuardianDate(ct), ct);
    public async Task MarkGuardianNoticeReadAsync(int notification, CancellationToken ct) => await repo.MarkGuardianNoticeReadAsync(School, User, notification, await GuardianDate(ct), ct);
    public async Task<string> SignedSnapshotAsync(int document, CancellationToken ct)
    {
        var access = await Access(false, ct); var doc = await repo.DocumentAsync(School, document, ct) ?? throw Error("DutyNotFound", "الوثيقة غير متاحة. / Document unavailable.", 404);
        if (!access.Manager && doc.AuthorUserId != User) throw Error("DutyForbidden", "الوثيقة غير متاحة. / Document unavailable.", 403);
        return JsonSerializer.Serialize(new { Snapshot = JsonSerializer.Deserialize<JsonElement>(doc.SnapshotJson), SignatureData = "data:image/png;base64," + Convert.ToBase64String(doc.SignatureBytes), doc.SignatureHash, doc.SnapshotHash });
    }
}
