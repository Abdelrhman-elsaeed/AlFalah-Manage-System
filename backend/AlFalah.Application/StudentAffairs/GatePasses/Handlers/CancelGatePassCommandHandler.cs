using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.GatePasses;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.GatePasses.Handlers;

public sealed class CancelGatePassCommandHandler
    : IRequestHandler<CancelGatePassCommand, ApiResponse<GatePassDto>>
{
    private readonly IGatePassWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public CancelGatePassCommandHandler(
        IGatePassWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<GatePassDto>> Handle(
        CancelGatePassCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<GatePassDto>.Fail(GatePassHandlerSupport.AuthenticationRequired);

        if (!_currentUser.HasPermission(PermissionNames.GatePassCancelOwn)
            && !_currentUser.HasPermission(PermissionNames.GatePassOverride))
            return ApiResponse<GatePassDto>.Fail(GatePassHandlerSupport.PermissionDenied);

        var gatePass = await _repository.GetForUpdateAsync(
            schoolId.Value,
            command.GatePassId,
            cancellationToken).ConfigureAwait(false);

        if (gatePass is null)
            return ApiResponse<GatePassDto>.Fail("Gate pass was not found");

        if (gatePass.Status == GatePassStatus.Cancelled)
            return ApiResponse<GatePassDto>.Fail("Gate pass is already cancelled");

        if (gatePass.Status != GatePassStatus.Requested
            && gatePass.Status != GatePassStatus.Approved
            && gatePass.Status != GatePassStatus.SecurityAcknowledged)
            return ApiResponse<GatePassDto>.Fail("Gate pass cannot be cancelled from its current state");

        if (!GatePassHandlerSupport.TryDecodeExpectedRowVersion(
                command.Request.RowVersion,
                gatePass.RowVersion,
                out var expectedRowVersion))
            return ApiResponse<GatePassDto>.Fail(GatePassHandlerSupport.ConcurrencyConflict);

        var reason = command.Request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
            return ApiResponse<GatePassDto>.Fail("Cancellation reason is required");

        var now = _timeProvider.GetUtcNow();
        var fromStatus = gatePass.Status;
        _repository.SetExpectedRowVersion(gatePass, expectedRowVersion);
        var correlationId = Guid.NewGuid();

        var actorRole = _currentUser.GetRoles().FirstOrDefault() ?? RoleNames.Guardian;

        gatePass.Status = GatePassStatus.Cancelled;
        gatePass.CancellationReason = reason;
        gatePass.CancelledByUserId = userId;
        gatePass.CancelledAt = now;
        gatePass.UpdatedByUserId = userId;
        gatePass.Transitions.Add(GatePassHandlerSupport.Transition(
            gatePass,
            fromStatus,
            GatePassStatus.Cancelled,
            userId,
            actorRole,
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
            ?? throw new InvalidOperationException("The cancelled gate pass could not be loaded");

        return ApiResponse<GatePassDto>.Success(dto, "Gate pass cancelled successfully");
    }
}
