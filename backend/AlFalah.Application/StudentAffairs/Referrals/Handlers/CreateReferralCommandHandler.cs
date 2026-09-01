using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Referrals;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Referrals.Handlers;

public sealed class CreateReferralCommandHandler
    : IRequestHandler<CreateReferralCommand, ApiResponse<ReferralDto>>
{
    private readonly IReferralWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public CreateReferralCommandHandler(
        IReferralWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<ReferralDto>> Handle(
        CreateReferralCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<ReferralDto>.Fail(ReferralHandlerSupport.AuthenticationRequired);

        if (!_currentUser.HasPermission(PermissionNames.ReferralCreate))
            return ApiResponse<ReferralDto>.Fail(ReferralHandlerSupport.PermissionDenied);

        var request = command.Request;
        if (request.StudentId <= 0)
            return ApiResponse<ReferralDto>.Fail("A valid student is required");

        if (string.IsNullOrWhiteSpace(request.Reason))
            return ApiResponse<ReferralDto>.Fail("A reason for referral is required");

        var now = _timeProvider.GetUtcNow();
        var enrollment = await _repository.GetActiveEnrollmentAsync(
            schoolId.Value,
            request.StudentId,
            DateOnly.FromDateTime(now.DateTime),
            cancellationToken).ConfigureAwait(false);

        if (enrollment is null)
            return ApiResponse<ReferralDto>.Fail("Student does not have an active enrollment in the current term");

        var referral = new StudentReferral
        {
            SchoolId = schoolId.Value,
            StudentId = request.StudentId,
            AcademicTermId = enrollment.AcademicTermId,
            SourceType = request.Source == 0 ? ReferralSourceType.Manual : request.Source,
            Priority = request.Priority == 0 ? ReferralPriority.Normal : request.Priority,
            Status = StudentReferralStatus.Open,
            RecommendedActions = request.Reason.Trim(),
            CreatedAt = now,
            CreatedByUserId = userId,
            UpdatedAt = now,
            UpdatedByUserId = userId
        };

        _repository.Add(referral);
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var dto = await _repository.GetDtoAsync(schoolId.Value, referral.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The created referral could not be loaded");

        return ApiResponse<ReferralDto>.Success(dto, "Referral created successfully");
    }
}
