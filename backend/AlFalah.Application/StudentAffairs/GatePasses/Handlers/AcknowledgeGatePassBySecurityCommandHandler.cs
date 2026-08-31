using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.GatePasses;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Domain.Events;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.GatePasses.Handlers;

public sealed class AcknowledgeGatePassBySecurityCommandHandler
    : IRequestHandler<AcknowledgeGatePassBySecurityCommand, ApiResponse<GatePassDto>>
{
    private readonly IGatePassWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public AcknowledgeGatePassBySecurityCommandHandler(
        IGatePassWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<GatePassDto>> Handle(
        AcknowledgeGatePassBySecurityCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<GatePassDto>.Fail(GatePassHandlerSupport.AuthenticationRequired);
        if (!_currentUser.IsInRole(RoleNames.SecurityGuard)
            || !_currentUser.HasPermission(PermissionNames.GatePassAcknowledgeSecurity))
            return ApiResponse<GatePassDto>.Fail(GatePassHandlerSupport.PermissionDenied);

        var gatePass = await _repository.GetForUpdateAsync(
            schoolId.Value,
            command.GatePassId,
            cancellationToken).ConfigureAwait(false);
        if (gatePass is null)
            return ApiResponse<GatePassDto>.Fail("Gate pass was not found");
        if (gatePass.Status == GatePassStatus.SecurityAcknowledged)
            return ApiResponse<GatePassDto>.Fail("Gate pass is already acknowledged by security");
        if (gatePass.Status != GatePassStatus.Approved)
            return ApiResponse<GatePassDto>.Fail("Gate pass must be approved before security acknowledgement");
        if (!GatePassHandlerSupport.TryDecodeExpectedRowVersion(
                command.Request.RowVersion,
                gatePass.RowVersion,
                out var expectedRowVersion))
            return ApiResponse<GatePassDto>.Fail(GatePassHandlerSupport.ConcurrencyConflict);

        var now = _timeProvider.GetUtcNow();
        if (gatePass.ApprovedWindowStartsAt is null
            || gatePass.ApprovedWindowEndsAt is null
            || now < gatePass.ApprovedWindowStartsAt
            || now > gatePass.ApprovedWindowEndsAt)
            return ApiResponse<GatePassDto>.Fail("Gate pass is outside its execution window");

        _repository.SetExpectedRowVersion(gatePass, expectedRowVersion);
        var correlationId = Guid.NewGuid();
        gatePass.Status = GatePassStatus.SecurityAcknowledged;
        gatePass.SecurityAcknowledgedByUserId = userId;
        gatePass.SecurityAcknowledgedAt = now;
        gatePass.UpdatedByUserId = userId;
        gatePass.Transitions.Add(GatePassHandlerSupport.Transition(
            gatePass,
            GatePassStatus.Approved,
            GatePassStatus.SecurityAcknowledged,
            userId,
            RoleNames.SecurityGuard,
            now,
            correlationId));
        gatePass.AppendDomainEvent(new GatePassSecurityAcknowledgedEvent(
            correlationId,
            gatePass.Id,
            gatePass.StudentId,
            gatePass.SchoolId,
            userId,
            now,
            gatePass.ApprovedWindowEndsAt.Value,
            now));

        try
        {
            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (GatePassConcurrencyException)
        {
            return ApiResponse<GatePassDto>.Fail(GatePassHandlerSupport.ConcurrencyConflict);
        }

        var dto = await _repository.GetDtoAsync(schoolId.Value, gatePass.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The acknowledged gate pass could not be loaded");
        return ApiResponse<GatePassDto>.Success(dto, "Gate pass acknowledged by security");
    }
}
