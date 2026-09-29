using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.GatePasses;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Domain.Events;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.GatePasses.Handlers;

public sealed class ExecuteGatePassCommandHandler
    : IRequestHandler<ExecuteGatePassCommand, ApiResponse<SecurityGatePassDetailDto>>
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

    public async Task<ApiResponse<SecurityGatePassDetailDto>> Handle(
        ExecuteGatePassCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<SecurityGatePassDetailDto>.Fail(GatePassHandlerSupport.AuthenticationRequired);
        if (!_currentUser.IsInRole(RoleNames.SecurityGuard)
            || !_currentUser.HasPermission(PermissionNames.GatePassExecute))
            return ApiResponse<SecurityGatePassDetailDto>.Fail(GatePassHandlerSupport.PermissionDenied);

        var gatePass = await _repository.GetForUpdateAsync(
            schoolId.Value,
            command.GatePassId,
            cancellationToken).ConfigureAwait(false);
        if (gatePass is null)
            return ApiResponse<SecurityGatePassDetailDto>.Fail("Gate pass was not found");
        if (gatePass.Status == GatePassStatus.Exited)
            return ApiResponse<SecurityGatePassDetailDto>.Fail("Gate pass state conflict: exit was already recorded");
        if (gatePass.Status != GatePassStatus.SecurityAcknowledged)
            return ApiResponse<SecurityGatePassDetailDto>.Fail("Gate pass state conflict: security acknowledgement is required before exit");
        if (!GatePassHandlerSupport.TryDecodeExpectedRowVersion(
                command.Request.RowVersion,
                gatePass.RowVersion,
                out var expectedRowVersion))
            return ApiResponse<SecurityGatePassDetailDto>.Fail(GatePassHandlerSupport.ConcurrencyConflict);

        var verificationNote = command.Request.VerificationNote?.Trim();
        var gateNote = command.Request.GateNote?.Trim();
        if (!Enum.IsDefined(command.Request.VerificationMethod)
            || string.IsNullOrWhiteSpace(verificationNote))
            return ApiResponse<SecurityGatePassDetailDto>.Fail("Pickup verification method and note are required");
        if (verificationNote.Length > GatePassValidationRules.VerificationNoteMaxLength)
            return ApiResponse<SecurityGatePassDetailDto>.Fail(
                $"Pickup verification note cannot exceed {GatePassValidationRules.VerificationNoteMaxLength} characters");
        if (gateNote?.Length > GatePassValidationRules.GateNoteMaxLength)
            return ApiResponse<SecurityGatePassDetailDto>.Fail(
                $"Gate note cannot exceed {GatePassValidationRules.GateNoteMaxLength} characters");
        if (gateNote?.Length == 0) gateNote = null;

        var now = _timeProvider.GetUtcNow();
        if (!GatePassHandlerSupport.IsWithinExecutionWindow(gatePass, now))
            return ApiResponse<SecurityGatePassDetailDto>.Fail("Gate pass is outside its execution window");

        _repository.SetExpectedRowVersion(gatePass, expectedRowVersion);
        var correlationId = Guid.NewGuid();
        gatePass.Status = GatePassStatus.Exited;
        gatePass.ExitedAt = now;
        gatePass.ExitRecordedByUserId = userId;
        gatePass.PickupVerificationMethod = command.Request.VerificationMethod;
        gatePass.PickupVerificationNote = verificationNote;
        gatePass.ExitGateNote = gateNote;
        gatePass.UpdatedByUserId = userId;
        gatePass.Transitions.Add(GatePassHandlerSupport.Transition(
            gatePass,
            GatePassStatus.SecurityAcknowledged,
            GatePassStatus.Exited,
            userId,
            RoleNames.SecurityGuard,
            now,
            correlationId,
            gateNote,
            command.Request.VerificationMethod,
            verificationNote));
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
            return ApiResponse<SecurityGatePassDetailDto>.Fail(GatePassHandlerSupport.ConcurrencyConflict);
        }

        var dto = await _repository.GetSecurityDetailAsync(schoolId.Value, gatePass.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The executed gate pass could not be loaded");
        return ApiResponse<SecurityGatePassDetailDto>.Success(dto, "Student exit recorded successfully");
    }
}
