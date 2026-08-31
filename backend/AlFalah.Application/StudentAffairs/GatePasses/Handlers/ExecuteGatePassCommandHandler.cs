using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.GatePasses;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Domain.Events;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.GatePasses.Handlers;

public sealed class ExecuteGatePassCommandHandler
    : IRequestHandler<ExecuteGatePassCommand, ApiResponse<GatePassDto>>
{
    private readonly IGatePassWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public ExecuteGatePassCommandHandler(
        IGatePassWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<GatePassDto>> Handle(
        ExecuteGatePassCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<GatePassDto>.Fail(GatePassHandlerSupport.AuthenticationRequired);
        if (!_currentUser.IsInRole(RoleNames.SecurityGuard)
            || !_currentUser.HasPermission(PermissionNames.GatePassExecute))
            return ApiResponse<GatePassDto>.Fail(GatePassHandlerSupport.PermissionDenied);

        var gatePass = await _repository.GetForUpdateAsync(
            schoolId.Value,
            command.GatePassId,
            cancellationToken).ConfigureAwait(false);
        if (gatePass is null)
            return ApiResponse<GatePassDto>.Fail("Gate pass was not found");
        if (gatePass.Status == GatePassStatus.Exited)
            return ApiResponse<GatePassDto>.Fail("Gate pass has already been executed");
        if (gatePass.Status != GatePassStatus.SecurityAcknowledged)
            return ApiResponse<GatePassDto>.Fail("Gate pass must be acknowledged by security before exit");
        if (!GatePassHandlerSupport.TryDecodeExpectedRowVersion(
                command.Request.RowVersion,
                gatePass.RowVersion,
                out var expectedRowVersion))
            return ApiResponse<GatePassDto>.Fail(GatePassHandlerSupport.ConcurrencyConflict);
        if (!Enum.IsDefined(command.Request.VerificationMethod)
            || string.IsNullOrWhiteSpace(command.Request.VerificationNote))
            return ApiResponse<GatePassDto>.Fail("Pickup verification method and note are required");

        var now = _timeProvider.GetUtcNow();
        if (gatePass.ApprovedWindowStartsAt is null
            || gatePass.ApprovedWindowEndsAt is null
            || now < gatePass.ApprovedWindowStartsAt
            || now > gatePass.ApprovedWindowEndsAt)
            return ApiResponse<GatePassDto>.Fail("Gate pass is outside its execution window");

        _repository.SetExpectedRowVersion(gatePass, expectedRowVersion);
        var correlationId = Guid.NewGuid();
        gatePass.Status = GatePassStatus.Exited;
        gatePass.ExitedAt = now;
        gatePass.ExitRecordedByUserId = userId;
        gatePass.PickupVerificationMethod = command.Request.VerificationMethod;
        gatePass.PickupVerificationNote = command.Request.VerificationNote.Trim();
        gatePass.ExitGateNote = command.Request.GateNote?.Trim();
        gatePass.UpdatedByUserId = userId;
        gatePass.Transitions.Add(GatePassHandlerSupport.Transition(
            gatePass,
            GatePassStatus.SecurityAcknowledged,
            GatePassStatus.Exited,
            userId,
            RoleNames.SecurityGuard,
            now,
            correlationId,
            command.Request.GateNote?.Trim(),
            command.Request.VerificationMethod,
            command.Request.VerificationNote.Trim()));
        gatePass.AppendDomainEvent(new StudentExitedSchoolEvent(
            correlationId,
            gatePass.Id,
            gatePass.StudentId,
            gatePass.SchoolId,
            userId,
            now,
            command.Request.VerificationMethod,
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
            ?? throw new InvalidOperationException("The executed gate pass could not be loaded");
        return ApiResponse<GatePassDto>.Success(dto, "Student exit recorded successfully");
    }
}
