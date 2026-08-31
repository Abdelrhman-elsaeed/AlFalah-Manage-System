using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;

namespace AlFalah.Application.StudentAffairs.GatePasses.Handlers;

internal static class GatePassHandlerSupport
{
    public const string PermissionDenied = "You do not have permission to perform this action";
    public const string AuthenticationRequired = "An authenticated user and active school are required";
    public const string ConcurrencyConflict = "Gate pass was modified by another user";

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

    public static TimetableDay? ToTimetableDay(DayOfWeek dayOfWeek) => dayOfWeek switch
    {
        DayOfWeek.Saturday => TimetableDay.Saturday,
        DayOfWeek.Sunday => TimetableDay.Sunday,
        DayOfWeek.Monday => TimetableDay.Monday,
        DayOfWeek.Tuesday => TimetableDay.Tuesday,
        DayOfWeek.Wednesday => TimetableDay.Wednesday,
        DayOfWeek.Thursday => TimetableDay.Thursday,
        _ => null
    };

    public static GatePassTransition Transition(
        GatePass gatePass,
        GatePassStatus? fromStatus,
        GatePassStatus toStatus,
        string actorUserId,
        string actorRole,
        DateTimeOffset occurredAt,
        Guid correlationId,
        string? reason = null,
        PickupVerificationMethod? verificationMethod = null,
        string? verificationNote = null) => new()
        {
            SchoolId = gatePass.SchoolId,
            GatePassId = gatePass.Id,
            GatePass = gatePass,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ActorUserId = actorUserId,
            ActorRole = actorRole,
            OccurredAt = occurredAt,
            CorrelationId = correlationId,
            Reason = reason,
            PickupVerificationMethod = verificationMethod,
            PickupVerificationNote = verificationNote
        };
}
