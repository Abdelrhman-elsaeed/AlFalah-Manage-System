using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Events;

namespace AlFalah.Application.IntelligentTimetable;

public sealed class CoverageExecutionService(IDailyCoverageRepository repository, CoverageAccess access, TimeProvider clock,
    TimetableReviewService review)
{
    public async Task<AlFalah.Shared.Models.PagedResult<CoverageNeedDto>> MyTasksAsync(int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || page > 100000 || pageSize < 1 || pageSize > 100) throw new ArgumentException("صفحة المهام غير صالحة.");
        var result = await repository.TeacherTasksAsync(access.School, access.User, page, pageSize, ct);
        return new() { Page = page, PageSize = pageSize, TotalCount = result.TotalCount, Items = result.Items.Select(DailyCoverageService.Map).ToList() };
    }
    public Task<CoverageTaskDetailDto> DetailAsync(int id, CancellationToken ct) => repository.ExecuteAsync(async token =>
        await Detail(await Require(id, token), token), ct);
    private async Task<DailyCoverageNeed> Require(int id, CancellationToken ct)
    {
        var n = await repository.NeedAsync(access.School, id, ct) ?? throw new KeyNotFoundException();
        if (!await repository.IsAssignedUserAsync(access.School, id, access.User, ct) && !await access.CanManage(ct)) throw new UnauthorizedAccessException();
        return n;
    }
    private async Task<string?> Blocked(DailyCoverageNeed n, CancellationToken ct)
    {
        if (n.ActiveSubstitutionId == null || n.State is "Withdrawn" or "ReviewRequired" or "Unable") return n.ReviewReason ?? "التكليف غير متاح للتنفيذ؛ راجع مسؤول الانتظار.";
        var changes = await repository.GetChangesAsync(access.School, n.SchoolTimetableId, n.LocalDate, ct);
        var last = changes.Where(c => c.Kind == "Substitution" && c.Movements.Any(m => m.SchoolTimetableEntryId == n.SourceEntryId)).LastOrDefault();
        if (last?.Id != n.ActiveSubstitutionId) return "تم تغيير التكليف؛ حدّث الصفحة.";
        var teacher = n.ActiveSubstitution?.Movements.FirstOrDefault()?.ToTeacherId;
        if (teacher == null || (await repository.GetAbsentTeachersAsync(access.School, n.LocalDate, ct)).Contains(teacher.Value)) return "المعلم البديل مسجل غائبًا؛ راجع المسؤول.";
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(n.TimeZoneId)).DateTime);
        if (n.LocalDate >= today && !await repository.IsEffectivePublicationAsync(access.School, n.SchoolTimetableId, n.LocalDate, ct)) return "نشر الجدول تغير؛ راجع المسؤول قبل التنفيذ.";
        if (n.LocalDate >= today)
        {
            var context = await repository.GetValidationContextAsync(access.School, n.SchoolTimetableId, ct);
            if (context == null || !DailyCoverageReconciler.Matches(n, context)) return "تغيرت تفاصيل الحصة أو توقيتها؛ راجع المسؤول قبل التنفيذ.";
        }
        return null;
    }
    private async Task<CoverageTaskDetailDto> Detail(DailyCoverageNeed n, CancellationToken ct)
    {
        var owner = n.AssignedUserId == access.User && await repository.IsAssignedUserAsync(access.School, n.Id, access.User, ct);
        var manager = await access.CanManage(ct); var blocked = await Blocked(n, ct);
        var signature = owner ? (await repository.SignatureAsync(access.User, ct))?.SignatureDrawnData : null;
        return new(DailyCoverageService.Map(n), owner && blocked == null && n.AcknowledgedAt == null && n.SubmittedAt == null,
            owner && blocked == null && n.AcknowledgedAt != null && n.SubmittedAt == null && clock.GetUtcNow() >= n.EndsAt,
            owner && blocked == null && n.SubmittedAt == null,
            blocked, signature, n.Records.OrderByDescending(r => r.Id).Select(r => new CoverageRecordDto(r.Id, r.Kind, r.ActorUserId,
                r.CreatedAt, r.Narrative, r.Notes, r.Reason, r.SnapshotHash, r.SignatureHash,
                r.SignatureBytes != null && (manager || r.ActorUserId == access.User) ? "data:image/png;base64," + Convert.ToBase64String(r.SignatureBytes) : null)).ToArray());
    }
    private async Task Owner(DailyCoverageNeed n, int revision, int substitution, CancellationToken ct)
    {
        if (n.AssignedUserId != access.User || !await repository.IsAssignedUserAsync(access.School, n.Id, access.User, ct)) throw new UnauthorizedAccessException();
        if (n.Revision != revision || n.ActiveSubstitutionId != substitution) throw new BellScheduleConflictException("تغير التكليف أو المسودة؛ حدّث البيانات قبل المتابعة.");
        var blocked = await Blocked(n, ct); if (blocked != null) throw new ArgumentException(blocked);
    }
    public Task<CoverageTaskDetailDto> SaveDraftAsync(int id, CoverageDraftRequest request, CancellationToken ct) => repository.ExecuteAsync(async token => {
        var n = await Require(id, token); await Owner(n, request.Revision, request.SubstitutionId, token);
        if (n.SubmittedAt != null) throw new ArgumentException("التنفيذ المرسل محفوظ. اطلب فتح تصحيح من المسؤول.");
        if (request.Narrative == null || request.Notes == null || request.Narrative.Length > 10000 || request.Notes.Length > 2000) throw new ArgumentException("راجع طول نص التنفيذ والملاحظات.");
        n.Narrative = request.Narrative.Trim(); n.Notes = request.Notes.Trim(); DailyCoverageReconciler.Touch(n, clock.GetUtcNow());
        await repository.SaveAsync(token); return await Detail(n, token);
    }, ct);
    public Task<CoverageTaskDetailDto> ActAsync(int id, string action, CoverageActionRequest request, CancellationToken ct) => repository.ExecuteAsync(async token => {
        if (action is not ("Acknowledgement" or "Execution" or "Unable" or "Reopen" or "Keep" or "Release")) throw new ArgumentException("الإجراء غير صالح.");
        if (request.RequestId == Guid.Empty || request.Reason?.Length > 1000) throw new ArgumentException("معرف العملية والسبب غير صالحين.");
        var existing = await repository.RecordAsync(access.School, request.RequestId, token);
        if (existing != null)
        {
            if (existing.DailyCoverageNeedId != id || existing.ActorUserId != access.User || existing.Kind != action ||
                existing.Revision != request.Revision || existing.SubstitutionId != request.SubstitutionId || existing.Reason != request.Reason?.Trim())
                throw new BellScheduleConflictException("معرف العملية مستخدم لإجراء مختلف.");
            return await Detail(await Require(id, token), token);
        }
        var n = await Require(id, token);
        if (action is "Keep" or "Reopen" or "Release")
        {
            await access.Manage(token);
            if (n.ActiveSubstitutionId == null || n.Revision != request.Revision || n.ActiveSubstitutionId != request.SubstitutionId) throw new BellScheduleConflictException("تغير التكليف؛ حدّث البيانات.");
            if (string.IsNullOrWhiteSpace(request.Reason)) throw new ArgumentException("اذكر سبب القرار الإداري (1–1000 حرف).");
        }
        else await Owner(n, request.Revision, request.SubstitutionId, token);
        byte[]? signature = null; DateTimeOffset? signatureAt = null;
        if (action is "Acknowledgement" or "Execution")
        {
            if (n.SubmittedAt != null) throw new ArgumentException("تم إرسال التنفيذ بالفعل.");
            if (action == "Acknowledgement" && n.AcknowledgedAt != null) throw new ArgumentException("تم الإقرار بالعلم بالفعل.");
            if (action == "Execution" && (n.AcknowledgedAt == null || clock.GetUtcNow() < n.EndsAt || string.IsNullOrWhiteSpace(n.Narrative)))
                throw new ArgumentException("أقر بالاستلام ثم اكتب ما تم تنفيذه واحفظ المسودة. الإرسال متاح بعد انتهاء الحصة.");
            var saved = await repository.SignatureAsync(access.User, token); var data = saved?.SignatureDrawnData;
            if (data == null || !data.StartsWith("data:image/png;base64,", StringComparison.Ordinal) || data.Length > 500 * 1024) throw new ArgumentException("احفظ توقيعك في إعدادات الحساب ثم عد إلى المسودة.");
            try { signature = Convert.FromBase64String(data[22..]); } catch (FormatException) { throw new ArgumentException("التوقيع المحفوظ غير صالح؛ حدّثه من إعدادات الحساب."); }
            if (signature.Length < 33 || !signature.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) throw new ArgumentException("التوقيع المحفوظ غير صالح.");
            signatureAt = saved!.UpdatedAt;
        }
        var snapshot = JsonSerializer.Serialize(new { Version = "standby-execution-v1", Need = DailyCoverageService.Map(n), Action = action,
            Actor = access.User, At = clock.GetUtcNow(), SignatureUpdatedAt = signatureAt, Reason = request.Reason?.Trim() });
        var record = new CoverageRecord { SchoolId = access.School, Need = n, SubstitutionId = request.SubstitutionId, RequestId = request.RequestId,
            ActorUserId = access.User, Kind = action, Revision = request.Revision, Narrative = n.Narrative, Notes = n.Notes,
            Reason = request.Reason?.Trim(), SnapshotJson = snapshot, SnapshotHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot))),
            SignatureBytes = signature, SignatureHash = signature == null ? null : Convert.ToHexString(SHA256.HashData(signature)), SignatureUpdatedAt = signatureAt, CreatedAt = clock.GetUtcNow() };
        switch (action)
        {
            case "Acknowledgement": n.AcknowledgedAt = clock.GetUtcNow(); n.State = "Acknowledged"; break;
            case "Execution": n.SubmittedAt = clock.GetUtcNow(); n.State = "Executed"; break;
            case "Unable":
                if (n.SubmittedAt != null || string.IsNullOrWhiteSpace(request.Reason)) throw new ArgumentException("اذكر سبب تعذر التنفيذ؛ التنفيذ المرسل يتطلب تصحيحًا إداريًا.");
                n.State = "Unable"; n.ReviewReason = request.Reason.Trim(); break;
            case "Reopen":
                if (n.SubmittedAt == null) throw new ArgumentException("لا يوجد تنفيذ مرسل لفتح تصحيح.");
                n.CorrectionReason = request.Reason!.Trim(); n.SubmittedAt = null; n.State = "Acknowledged"; break;
            case "Keep":
                var current = await repository.GetValidationContextAsync(access.School, n.SchoolTimetableId, token);
                if (current == null || !DailyCoverageReconciler.Matches(n, current) || !await repository.IsEffectivePublicationAsync(access.School, n.SchoolTimetableId, n.LocalDate, token)) throw new ArgumentException("تغير مصدر التكليف؛ لا يمكن الإبقاء عليه دون تصحيح الجدول أولًا.");
                if ((await repository.GetAbsentTeachersAsync(access.School, n.LocalDate, token)).Contains(n.ActiveSubstitution!.Movements.First().ToTeacherId)) throw new ArgumentException("البديل مسجل غائبًا؛ اختر بديلًا آخر أو صحح الحضور.");
                n.SubstituteWasAbsent = false;
                n.IsRequired = (await repository.GetAbsentTeachersAsync(access.School, n.LocalDate, token)).Contains(n.OriginalTeacherId);
                n.ReviewReason = null; n.State = n.SubmittedAt != null ? "Executed" : n.AcknowledgedAt != null ? "Acknowledged" : "Assigned"; break;
            case "Release": await Release(n, request, token); break;
        }
        repository.AddRecord(record);
        if (action is "Unable" or "Reopen") n.AppendDomainEvent(new CoverageAttentionEvent(Guid.NewGuid(), access.School, n.Id, action,
            n.AssignedUserId, $"{(action == "Unable" ? "تعذر تنفيذ تغطية" : "تم فتح تصحيح تنفيذ")} {n.Classroom} · {n.Subject} · {n.LocalDate:yyyy-MM-dd}. {request.Reason}", clock.GetUtcNow()));
        repository.AddAudit(new() { SchoolId = access.School, UserId = access.User, Action = "Timetable.Coverage." + action, EntityName = nameof(DailyCoverageNeed), EntityId = n.Id.ToString(), NewValues = JsonSerializer.Serialize(new { action, record.SnapshotHash, request.Revision }) });
        DailyCoverageReconciler.Touch(n, clock.GetUtcNow()); await repository.SaveAsync(token); return await Detail(n, token);
    }, ct);
    private async Task Release(DailyCoverageNeed n, CoverageActionRequest request, CancellationToken ct)
    {
        if (n.Records.Any(r => r.Kind == "Execution") || clock.GetUtcNow() >= n.EndsAt) throw new ArgumentException("لا يمكن إلغاء تكليف له تنفيذ موقع أو انتهى وقته؛ استخدم التصحيح مع حفظ السجل.");
        var c = await repository.GetValidationContextAsync(access.School, n.SchoolTimetableId, ct) ?? throw new KeyNotFoundException();
        if (!c.Timetable.IsPublished || !c.Timetable.Entries.Any(e => e.Id == n.SourceEntryId)) throw new ArgumentException("راجع نشر الجدول وهوية الحصة أولًا.");
        var before = c.Timetable.Revision; c.Timetable.Revision++; c.Timetable.UpdatedAt = clock.GetUtcNow(); c.Timetable.UpdatedByUserId = access.User;
        var version = await review.Snapshot(c, TimetableChangeKind.Substitution, ct);
        var entries = JsonSerializer.Deserialize<int[]>(n.EntryIdsJson)!;
        var restored = new TimetableSubstitution { SchoolId = access.School, SchoolTimetableId = c.Timetable.Id, RequestId = request.RequestId,
            ProposalId = "release:" + n.Id, Kind = "Substitution", LocalDate = n.LocalDate, BeforeRevision = before, AfterRevision = c.Timetable.Revision,
            RequestedByUserId = access.User, ApprovedByUserId = access.User, OverrideReason = request.Reason, Version = version,
            Movements = entries.Select(id => { var e = c.Timetable.Entries.Single(e => e.Id == id); return new TimetableSubstitutionMovement {
                SchoolId = access.School, SchoolTimetableEntryId = id, FromTeacherId = n.ActiveSubstitution!.Movements.First().ToTeacherId,
                ToTeacherId = n.OriginalTeacherId, Day = (int)e.Day, ToDay = (int)e.Day, FromPeriod = e.Period, ToPeriod = e.Period }; }).ToArray() };
        restored.AppendDomainEvent(new TeacherTimetableChangedEvent(Guid.NewGuid(), access.School, 0, c.Timetable.Id, c.Timetable.Revision,
            n.LocalDate, "CoverageRelease", c.Teachers.Where(t => t.InstructorProfileId == n.OriginalTeacherId || t.Instructor.UserId == n.AssignedUserId).Select(t => t.Instructor.UserId).ToArray(), clock.GetUtcNow()));
        repository.AddChange(restored); n.ActiveSubstitutionId = null; n.ActiveSubstitution = null; n.AssignedUserId = null; n.AssignedTeacherName = null;
        n.IsRequired = (await repository.GetAbsentTeachersAsync(access.School, n.LocalDate, ct)).Contains(n.OriginalTeacherId); n.SubstituteWasAbsent = false;
        n.State = n.IsRequired ? "Needed" : "Withdrawn"; n.ReviewReason = null; n.Narrative = ""; n.Notes = ""; n.AcknowledgedAt = null;
    }
}
