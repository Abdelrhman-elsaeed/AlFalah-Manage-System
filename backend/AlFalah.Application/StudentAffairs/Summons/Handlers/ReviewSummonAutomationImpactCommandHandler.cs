using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Summons;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Summons.Handlers;

public sealed class ReviewSummonAutomationImpactCommandHandler
    : IRequestHandler<ReviewSummonAutomationImpactCommand, ApiResponse<SummonDto>>
{
    private readonly ISummonWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public ReviewSummonAutomationImpactCommandHandler(
        ISummonWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<SummonDto>> Handle(
        ReviewSummonAutomationImpactCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.AuthenticationRequired);

        if (!_currentUser.HasPermission(PermissionNames.SummonReviewAutomationImpact))
            return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.PermissionDenied);

        var summon = await _repository.GetForUpdateAsync(
            schoolId.Value,
            command.SummonId,
            cancellationToken).ConfigureAwait(false);

        if (summon is null)
            return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.NotFound);

        if (!SummonHandlerSupport.TryDecodeExpectedRowVersion(
                command.Request.RowVersion,
                summon.RowVersion,
                out var expectedRowVersion))
            return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.ConcurrencyConflict);

        if (string.IsNullOrWhiteSpace(command.Request.Rationale))
            return ApiResponse<SummonDto>.Fail("Review rationale is required");

        var now = _timeProvider.GetUtcNow();
        _repository.SetExpectedRowVersion(summon, expectedRowVersion);

        summon.RequiresOfficerReview = false;
        summon.OfficerReviewedAt = now;
        summon.OfficerReviewDecision = command.Request.Decision;
        summon.OfficerReviewReason = command.Request.Rationale.Trim();
        summon.UpdatedByUserId = userId;
        summon.UpdatedAt = now;

        var correlationId = Guid.NewGuid();
        summon.StatusHistory.Add(SummonHandlerSupport.History(
            summon,
            summon.Status,
            summon.Status,
            userId,
            now,
            correlationId,
            $"Automation impact reviewed: Decision={command.Request.Decision}, Rationale={command.Request.Rationale.Trim()}"));

        try
        {
            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SummonConcurrencyException)
        {
            return ApiResponse<SummonDto>.Fail(SummonHandlerSupport.ConcurrencyConflict);
        }

        var dto = await _repository.GetDtoAsync(schoolId.Value, summon.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The updated guardian summons could not be loaded");

        return ApiResponse<SummonDto>.Success(dto, "Automation impact reviewed successfully");
    }
}
