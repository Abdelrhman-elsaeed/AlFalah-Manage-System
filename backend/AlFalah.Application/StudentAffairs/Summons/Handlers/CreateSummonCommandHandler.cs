using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Summons;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Summons.Handlers;

public sealed class CreateSummonCommandHandler
    : IRequestHandler<CreateSummonCommand, ApiResponse<SummonDto>>
{
    private readonly ISummonWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public CreateSummonCommandHandler(
        ISummonWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<SummonDto>> Handle(
        CreateSummonCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.AuthenticationRequired);

        if (!_currentUser.HasPermission(PermissionNames.SummonCreate))
            return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.PermissionDenied);

        var request = command.Request;
        if (request.StudentId <= 0)
            return ApiResponse<SummonDto>.Fail("A valid student is required");

        if (request.GuardianProfileId <= 0)
            return ApiResponse<SummonDto>.Fail("A valid guardian is required");

        if (string.IsNullOrWhiteSpace(request.Reason))
            return ApiResponse<SummonDto>.Fail("A reason for summons is required");

        var now = _timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.DateTime);

        var isGuardianLinked = await _repository.IsGuardianLinkActiveAsync(
            schoolId.Value,
            request.GuardianProfileId,
            request.StudentId,
            today,
            cancellationToken).ConfigureAwait(false);

        if (!isGuardianLinked)
            return ApiResponse<SummonDto>.Fail("Selected guardian is not actively linked to the student");

        var enrollment = await _repository.GetActiveEnrollmentAsync(
            schoolId.Value,
            request.StudentId,
            today,
            cancellationToken).ConfigureAwait(false);

        if (enrollment is null)
            return ApiResponse<SummonDto>.Fail("Student does not have an active enrollment in the current term");

        var summon = new GuardianSummon
        {
            SchoolId = schoolId.Value,
            StudentId = request.StudentId,
            AcademicTermId = enrollment.AcademicTermId,
            StudentReferralId = request.ReferralId,
            CreatedReason = request.Reason.Trim(),
            Priority = request.Priority == 0 ? ReferralPriority.Normal : request.Priority,
            Status = GuardianSummonStatus.Pending,
            GuardianProfileId = request.GuardianProfileId,
            CreatedAt = now,
            CreatedByUserId = userId,
            UpdatedAt = now,
            UpdatedByUserId = userId
        };

        var correlationId = Guid.NewGuid();
        summon.StatusHistory.Add(SummonHandlerSupport.History(
            summon,
            GuardianSummonStatus.Pending,
            GuardianSummonStatus.Pending,
            userId,
            now,
            correlationId,
            "Guardian summons created"));

        SummonHandlerSupport.AppendStateEvent(
            summon,
            GuardianSummonStatus.Pending,
            GuardianSummonStatus.Pending,
            "Created",
            userId,
            now,
            correlationId);

        _repository.Add(summon);
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var dto = await _repository.GetDtoAsync(schoolId.Value, summon.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The created summons could not be loaded");

        return ApiResponse<SummonDto>.Success(dto, "Guardian summons created successfully");
    }
}
