using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Domain.Events;

namespace AlFalah.Application.StudentAffairs.Summons.Handlers;

public static class SummonHandlerSupport
{
    public const string AuthenticationRequired = "An authenticated social worker and active school are required";
    public const string PermissionDenied = "You do not have permission to perform this action";
    public const string NotFound = "Guardian summons was not found";
    public const string AssignmentDenied = "Guardian summons is not assigned to the current social worker";
    public const string ConcurrencyConflict = "Guardian summons was modified by another user";

    public static bool TryDecodeExpectedRowVersion(
        string encodedRowVersion,
        byte[] currentRowVersion,
        out byte[] expectedRowVersion)
    {
        expectedRowVersion = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(encodedRowVersion)) return false;

        try
        {
            expectedRowVersion = Convert.FromBase64String(encodedRowVersion);
            return expectedRowVersion.Length > 0
                && currentRowVersion.AsSpan().SequenceEqual(expectedRowVersion);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static GuardianSummonStatusHistory History(
        GuardianSummon summon,
        GuardianSummonStatus fromStatus,
        GuardianSummonStatus toStatus,
        string actorUserId,
        DateTimeOffset occurredAt,
        Guid correlationId,
        string? notes) => new()
        {
            SchoolId = summon.SchoolId,
            GuardianSummonId = summon.Id,
            GuardianSummon = summon,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ActorUserId = actorUserId,
            OccurredAt = occurredAt,
            Notes = notes,
            CorrelationId = correlationId
        };

    public static void AppendStateEvent(
        GuardianSummon summon,
        GuardianSummonStatus fromStatus,
        GuardianSummonStatus toStatus,
        string action,
        string actorUserId,
        DateTimeOffset now,
        Guid correlationId) =>
        summon.AppendDomainEvent(new GP9jdFE6bJJJBXm548MTsCQvpLk7RqkKB7(
            correlationId,
            summon.Id,
            summon.StudentId,
            summon.SchoolId,
            summon.AcademicTermId,
            summon.GuardianProfileId,
            fromStatus,
            toStatus,
            action,
            actorUserId,
            now,
            summon.ScheduledAt,
            summon.AttendedAt,
            summon.ObservationStartedAt,
            summon.ImprovedAt,
            now));

    public static bool IsSocialWorkerWithPermission(
        AlFalah.Application.Interfaces.ICurrentUserService currentUser,
        string permission) =>
        currentUser.IsInRole(RoleNames.SocialWorker) && currentUser.HasPermission(permission);
}
