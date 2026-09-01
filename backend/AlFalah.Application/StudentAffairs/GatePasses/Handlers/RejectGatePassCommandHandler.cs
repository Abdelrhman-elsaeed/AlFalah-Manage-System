using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.GatePasses;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.GatePasses.Handlers;

public sealed class RejectGatePassCommandHandler
    : IRequestHandler<RejectGatePassCommand, ApiResponse<GatePassDto>>
{
    private readonly IGatePassWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public RejectGatePassCommandHandler(
        IGatePassWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<GatePassDto>> Handle(
        RejectGatePassCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<GatePassDto>.Fail(GatePassHandlerSupport.AuthenticationRequired);

        if (!_currentUser.IsInRole(RoleNames.StudentAffairsOfficer)
            && !_currentUser.HasPermission(PermissionNames.GatePassReject))
            return ApiResponse<GatePassDto>.Fail(GatePassHandlerSupport.PermissionDenied);

        var gatePass = await _repository.GetForUpdateAsync(
            schoolId.Value,
            command.GatePassId,
            cancellationToken).ConfigureAwait(false);

        if (gatePass is null)
            return ApiResponse<GatePassDto>.Fail("Gate pass was not found");

        if (gatePass.Status != GatePassStatus.Requested)
            return ApiResponse<GatePassDto>.Fail("Gate pass cannot be rejected from its current state");

        if (!GatePassHandlerSupport.TryDecodeExpectedRowVersion(
                command.Request.RowVersion,
                gatePass.RowVersion,
                out var expectedRowVersion))
            return ApiResponse<GatePassDto>.Fail(GatePassHandlerSupport.ConcurrencyConflict);

        var reason = command.Request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
            return ApiResponse<GatePassDto>.Fail("Rejection reason is required");

        var now = _timeProvider.GetUtcNow();
        _repository.SetExpectedRowVersion(gatePass, expectedRowVersion);
        var correlationId = Guid.NewGuid();

        gatePass.Status = GatePassStatus.Rejected;
        gatePass.RejectionReason = reason;
        gatePass.ReviewedByUserId = userId;
        gatePass.ReviewedAt = now;
        gatePass.UpdatedByUserId = userId;
        gatePass.Transitions.Add(GatePassHandlerSupport.Transition(
            gatePass,
            GatePassStatus.Requested,
            GatePassStatus.Rejected,
            userId,
            RoleNames.StudentAffairsOfficer,
            now,
            correlationId,
            reason));

        try
        {
            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (GatePassConcurrencyException)
        {
            return ApiResponse<GatePassDto>.Fail(GatePassHandlerSupport.ConcurrencyConflict);
        }

        var dto = await _repository.GetDtoAsync(schoolId.Value, gatePass.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The rejected gate pass could not be loaded");

        return ApiResponse<GatePassDto>.Success(dto, "Gate pass rejected");
    }
}
