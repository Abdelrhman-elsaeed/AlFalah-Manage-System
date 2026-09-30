using System;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Domain.Events;

namespace AlFalah.Application.StudentAffairs.Referrals.Handlers;

public static class ReferralHandlerSupport
{
    public const string AuthenticationRequired = "An authenticated user and active school are required";
    public const string PermissionDenied = "You do not have permission to perform this action";
    public const string NotFound = "Referral was not found";
    public const string AssignmentDenied = NotFound;
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

    public static bool IsSocialWorkerWithPermission(ICurrentUserService currentUser, string permission) =>
        currentUser.IsInRole(RoleNames.SocialWorker) && currentUser.HasPermission(permission);

    public static void AppendTransition(
        StudentReferral referral,
        StudentReferralStatus fromStatus,
        StudentReferralStatus toStatus,
        string actorUserId,
        string actorRole,
        DateTimeOffset occurredAt,
        string? reason)
    {
        var correlationId = Guid.NewGuid();
        referral.Transitions.Add(new StudentReferralTransition
        {
            SchoolId = referral.SchoolId,
            StudentReferralId = referral.Id,
            StudentReferral = referral,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ActorUserId = actorUserId,
            ActorRole = actorRole,
            OccurredAt = occurredAt,
            Reason = reason,
            CorrelationId = correlationId
        });
        referral.AppendDomainEvent(new StudentReferralTransitionedEvent(
            correlationId,
            referral.Id,
            referral.StudentId,
            referral.SchoolId,
            referral.AcademicTermId,
            fromStatus,
            toStatus,
            actorUserId,
            actorRole,
            reason,
            occurredAt));
    }
}
