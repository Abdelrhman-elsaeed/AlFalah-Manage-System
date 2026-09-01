using System;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;

namespace AlFalah.Application.StudentAffairs.Referrals.Handlers;

public static class ReferralHandlerSupport
{
    public const string AuthenticationRequired = "An authenticated user and active school are required";
    public const string PermissionDenied = "You do not have permission to perform this action";
    public const string NotFound = "Referral was not found";
    public const string AssignmentDenied = "Referral is not assigned to the current social worker";
    public const string ConcurrencyConflict = "Referral was modified by another user";

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

    public static StudentCaseAction CreateAction(
        StudentReferral referral,
        StudentCaseActionType actionType,
        string description,
        string actorUserId,
        DateTimeOffset actionAt,
        string? result = null) => new()
        {
            SchoolId = referral.SchoolId,
            StudentReferralId = referral.Id,
            ActionType = actionType,
            Description = description,
            ActorUserId = actorUserId,
            ActionAt = actionAt,
            Result = result,
            CreatedAt = actionAt,
            CreatedByUserId = actorUserId,
            UpdatedAt = actionAt,
            UpdatedByUserId = actorUserId
        };
}
