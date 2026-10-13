using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Duty;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.Duty;

public sealed class DutyReconciler(IDutyRepository repo)
{
    public async Task ProcessAsync(DutyReconciliationOperation pending, CancellationToken ct)
    {
        await repo.AtomicAsync(pending.SchoolId, async () =>
        {
            var op = await repo.OperationAsync(pending.Id, ct);
            if (op is null || op.CompletedAt is not null) return false;
            var state = await repo.LoadAsync(op.SchoolId, ct);
            Reconcile(state, op.SchoolId, op.Date);
            op.CompletedAt = DateTimeOffset.UtcNow; op.ErrorCode = null; op.Attempts++;
            return true;
        }, ct);
    }
    public async Task FailedAsync(DutyReconciliationOperation pending, CancellationToken ct)
    {
        await repo.AtomicAsync(pending.SchoolId, async () =>
        {
            var op = await repo.OperationAsync(pending.Id, ct);
            if (op is null || op.CompletedAt is not null) return false;
            op.Attempts++; op.ErrorCode = "DutyReconciliationRetry";
            op.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(Math.Min(3600, Math.Pow(2, Math.Min(op.Attempts, 10)) * 5));
            return true;
        }, ct);
    }
    // Public deterministic workflow seam for service-level verification with persisted facts.
    public void Reconcile(DutyState state, int schoolId, DateOnly date)
    {
        var a = state.Assignments.SingleOrDefault(x => x.Date == date && !x.IsCancelled);
        if (a is null) return;
        var plan = state.Plans.Single(x => x.Id == a.DutyPlanId);
        var settings = DutyRotation.Settings(plan);
        AttendanceRecord? Fact(string id, DateOnly day) => state.Attendance.SingleOrDefault(x => x.UserId == id && x.AttendanceDate == day);
        bool Eligible(string id, DateOnly day) => state.Teachers.Any(t => t.UserId == id) && DutyRotation.Eligible(settings, id, day)
            && Fact(id, day)?.Status is not (AttendanceStatus.Absent or AttendanceStatus.Excused);
        bool UndoExists(DutyAssignmentChange c) => state.Changes.Any(x => x.Kind == "Undo" && x.RelatedChangeId == c.Id);
        // Corrections only reverse the precise, untouched current pair. Never unwind a modified chain.
        foreach (var c in state.Changes.Where(x => x.Date == date && x.Kind is "AutoExchange" or "UnresolvedCompensation").Reverse().ToArray())
        {
            if (UndoExists(c) || Fact(c.BeforeTeacherId, date)?.Status != AttendanceStatus.Present) continue;
            var b = c.OtherAssignmentId is null ? null : state.Assignments.SingleOrDefault(x => x.Id == c.OtherAssignmentId);
            var exact = a.Revision == c.AfterRevision && a.EffectiveTeacherId == c.AfterTeacherId && !a.IsLocked && !a.IsManuallyProtected
                && (b is null || b.Revision == c.OtherAfterRevision && b.EffectiveTeacherId == c.OtherAfterTeacherId && !b.IsLocked && !b.IsManuallyProtected && !b.IsCancelled)
                && Eligible(c.BeforeTeacherId, date) && (b is null || Eligible(c.AfterTeacherId, b.Date));
            if (!exact) { Exception(a, "AttendanceCorrectionReview", Fact(c.BeforeTeacherId, date), state, schoolId); continue; }
            var undo = Change(state, schoolId, a, b, "Undo", c.BeforeTeacherId, "Attendance corrected to Present", Fact(c.BeforeTeacherId, date), c.Id);
            undo.OtherAfterTeacherId = b is null ? null : c.OtherBeforeTeacherId;
            if (b is not null) b.EffectiveTeacherId = c.OtherBeforeTeacherId!;
            a.EffectiveTeacherId = c.BeforeTeacherId; a.ExceptionCode = null;
            Touch(plan, a, b, undo); Notify(schoolId, a, b, undo);
        }
        // Each pass checks the current effective teacher; known absent cover candidates are skipped.
        var fact = Fact(a.EffectiveTeacherId, date);
        if (fact?.Status is not (AttendanceStatus.Absent or AttendanceStatus.Excused))
        {
            var eligibleOwner = state.Teachers.Any(t => t.UserId == a.EffectiveTeacherId)
                && DutyRotation.Eligible(settings, a.EffectiveTeacherId, date) && DutyRotation.Operating(settings, date);
            if (!eligibleOwner && !(a.IsManuallyProtected && a.ExceptionCode == "ManualEligibilityException"))
            { Exception(a, "EligibilityReview", fact, state, schoolId); return; }
            if (a.ExceptionCode is "NoEligibleSubstitute" or "ManualAbsenceReview" or "EligibilityReview") { a.ExceptionCode = null; a.Revision++; plan.Revision++; }
            return;
        }
        if (a.IsLocked || a.IsManuallyProtected) { Exception(a, "ManualAbsenceReview", fact, state, schoolId); return; }
        var future = state.Assignments.Where(x => x.DutyPlanId == plan.Id && x.Date > date && !x.IsCancelled && !x.IsLocked && !x.IsManuallyProtected
            && x.EffectiveTeacherId != a.EffectiveTeacherId && Eligible(x.EffectiveTeacherId, date)
            // Compensation cannot silently violate an exclusion or closed operating date.
            && state.Teachers.Any(t => t.UserId == a.EffectiveTeacherId)
            && DutyRotation.Eligible(settings, a.EffectiveTeacherId, x.Date) && DutyRotation.Operating(settings, x.Date))
            .OrderBy(x => x.Date).FirstOrDefault();
        var replacement = future?.EffectiveTeacherId;
        // End-of-term fallback retains a visible obligation; never invent an out-of-term date.
        replacement ??= settings.TeacherIds.FirstOrDefault(id => id != a.EffectiveTeacherId && Eligible(id, date));
        if (replacement is null) { Exception(a, "NoEligibleSubstitute", fact, state, schoolId); return; }
        var original = a.EffectiveTeacherId;
        var change = Change(state, schoolId, a, future, future is null ? "UnresolvedCompensation" : "AutoExchange", replacement,
            fact.Status == AttendanceStatus.Excused ? "Recorded Excused staff attendance" : "Recorded Absent staff attendance", fact, null);
        a.EffectiveTeacherId = replacement; a.ExceptionCode = future is null ? "UnresolvedCompensation" : null;
        if (future is not null) future.EffectiveTeacherId = original;
        Touch(plan, a, future, change); Notify(schoolId, a, future, change);
        // The future sheet may have been reconciled before ownership moved to A.
        // Requeue E durably in the same exchange transaction, even if its earlier signal completed.
        if (future is not null) repo.Add(new DutyReconciliationOperation { SchoolId = schoolId, Date = future.Date });
    }
    private void Exception(DutyAssignment a, string code, AttendanceRecord? fact, DutyState state, int schoolId)
    {
        if (a.ExceptionCode == code && state.Changes.Any(c => c.AssignmentId == a.Id && c.Kind == code && c.AttendanceRecordId == fact?.Id && c.AttendanceUpdatedAt == fact?.UpdatedAt)) return;
        a.ExceptionCode = code; a.Revision++; state.Plans.Single(x => x.Id == a.DutyPlanId).Revision++;
        Change(state, schoolId, a, null, code, a.EffectiveTeacherId, code, fact, null);
    }
    private DutyAssignmentChange Change(DutyState state, int schoolId, DutyAssignment a, DutyAssignment? b, string kind, string after, string reason, AttendanceRecord? fact, int? related)
    {
        var c = new DutyAssignmentChange { SchoolId = schoolId, DutyPlanId = a.DutyPlanId, AssignmentId = a.Id, OtherAssignmentId = b?.Id,
            Date = a.Date, OtherDate = b?.Date, Kind = kind, BeforeTeacherId = a.EffectiveTeacherId, AfterTeacherId = after,
            OtherBeforeTeacherId = b?.EffectiveTeacherId, OtherAfterTeacherId = b is null ? null : a.EffectiveTeacherId,
            Reason = reason, ActorUserId = "system", AttendanceRecordId = fact?.Id, AttendanceUpdatedAt = fact?.UpdatedAt, RelatedChangeId = related };
        repo.Add(c); state.Changes.Add(c); return c;
    }
    private static void Touch(DutyPlan plan, DutyAssignment a, DutyAssignment? b, DutyAssignmentChange c)
    {
        plan.Revision++; a.Revision++; c.AfterRevision = a.Revision;
        if (b is not null) { b.Revision++; c.OtherAfterRevision = b.Revision; }
    }
    private void Notify(int schoolId, DutyAssignment a, DutyAssignment? b, DutyAssignmentChange c)
    {
        foreach (var id in new[] { c.BeforeTeacherId, c.AfterTeacherId }.Distinct()) repo.Notify(schoolId, id,
            $"duty:{a.Id}:{a.Revision}:{c.Kind}:{id}", $"تحديث مناوبة {a.Date:yyyy-MM-dd}" + (b is null ? " — تعويض يحتاج المراجعة" : $" ↔ {b.Date:yyyy-MM-dd}") + " / Duty schedule updated.");
    }
}
