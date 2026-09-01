using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.GatePasses;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.GatePasses.Handlers;

public sealed class AcknowledgeGatePassByTeacherCommandHandler
    : IRequestHandler<AcknowledgeGatePassByTeacherCommand, ApiResponse<GatePassDto>>
{
    private readonly IGatePassWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public AcknowledgeGatePassByTeacherCommandHandler(
        IGatePassWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<GatePassDto>> Handle(
        AcknowledgeGatePassByTeacherCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<GatePassDto>.Fail(GatePassHandlerSupport.AuthenticationRequired);

        if (!_currentUser.IsInRole(RoleNames.Instructor)
            && !_currentUser.HasPermission(PermissionNames.GatePassAcknowledgeTeacher))
            return ApiResponse<GatePassDto>.Fail(GatePassHandlerSupport.PermissionDenied);

        var gatePass = await _repository.GetForUpdateAsync(
            schoolId.Value,
            command.GatePassId,
            cancellationToken).ConfigureAwait(false);

        if (gatePass is null)
            return ApiResponse<GatePassDto>.Fail("Gate pass was not found");

        if (!GatePassHandlerSupport.TryDecodeExpectedRowVersion(
                command.Request.RowVersion,
                gatePass.RowVersion,
                out var expectedRowVersion))
            return ApiResponse<GatePassDto>.Fail(GatePassHandlerSupport.ConcurrencyConflict);

        var now = _timeProvider.GetUtcNow();
        _repository.SetExpectedRowVersion(gatePass, expectedRowVersion);
        var correlationId = Guid.NewGuid();

        gatePass.UpdatedByUserId = userId;
        gatePass.Transitions.Add(GatePassHandlerSupport.Transition(
            gatePass,
            gatePass.Status,
            gatePass.Status,
            userId,
            RoleNames.Instructor,
            now,
            correlationId,
            "Acknowledged by teacher"));

        try
        {
            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (GatePassConcurrencyException)
        {
            return ApiResponse<GatePassDto>.Fail(GatePassHandlerSupport.ConcurrencyConflict);
        }

        var dto = await _repository.GetDtoAsync(schoolId.Value, gatePass.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The acknowledged gate pass could not be loaded");

        return ApiResponse<GatePassDto>.Success(dto, "Gate pass acknowledged by teacher");
    }
}
