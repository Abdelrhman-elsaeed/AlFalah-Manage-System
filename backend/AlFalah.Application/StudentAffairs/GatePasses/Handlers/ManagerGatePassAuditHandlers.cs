using System.Text.Json;
using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.GatePasses;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.GatePasses.Handlers;

public sealed class GetManagerGatePassAuditQueryHandler
    : IRequestHandler<GetManagerGatePassAuditQuery, ApiResponse<PagedResult<ManagerGatePassAuditItemDto>>>
{
    private readonly IGatePassWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public GetManagerGatePassAuditQueryHandler(IGatePassWorkflowRepository repository, ICurrentUserService currentUser, TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<PagedResult<ManagerGatePassAuditItemDto>>> Handle(GetManagerGatePassAuditQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.ActiveSchoolId is not int schoolId || string.IsNullOrWhiteSpace(_currentUser.UserId))
            return ApiResponse<PagedResult<ManagerGatePassAuditItemDto>>.Fail(GatePassHandlerSupport.AuthenticationRequired);
        if (!_currentUser.IsInRole(RoleNames.SchoolManager)
            || !_currentUser.HasPermission(PermissionNames.GatePassViewAudit))
            return ApiResponse<PagedResult<ManagerGatePassAuditItemDto>>.Fail(GatePassHandlerSupport.PermissionDenied);

        var page = await _repository.GetManagerAuditAsync(schoolId, request.Query, _timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return ApiResponse<PagedResult<ManagerGatePassAuditItemDto>>.Success(page);
    }
}

public sealed class RecordFalseExitIncidentCommandHandler
    : IRequestHandler<RecordFalseExitIncidentCommand, ApiResponse<GatePassDto>>
{
    private const int ReasonMaxLength = 1000;
    private readonly IGatePassWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public RecordFalseExitIncidentCommandHandler(IGatePassWorkflowRepository repository, ICurrentUserService currentUser, TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<GatePassDto>> Handle(RecordFalseExitIncidentCommand command, CancellationToken cancellationToken)
    {
        if (_currentUser.ActiveSchoolId is not int schoolId || string.IsNullOrWhiteSpace(_currentUser.UserId))
            return ApiResponse<GatePassDto>.Fail(GatePassHandlerSupport.AuthenticationRequired);
        if (!_currentUser.IsInRole(RoleNames.SchoolManager)
            || !_currentUser.HasPermission(PermissionNames.GatePassOverride))
            return ApiResponse<GatePassDto>.Fail(GatePassHandlerSupport.PermissionDenied);

        var reason = command.Request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > ReasonMaxLength)
            return ApiResponse<GatePassDto>.Fail($"Incident reason is required and must not exceed {ReasonMaxLength} characters");

        var gatePass = await _repository.GetForUpdateAsync(schoolId, command.GatePassId, cancellationToken).ConfigureAwait(false);
        if (gatePass is null) return ApiResponse<GatePassDto>.Fail("Gate pass was not found");
        if (gatePass.Status != GatePassStatus.Exited)
            return ApiResponse<GatePassDto>.Fail("A false-exit incident can only be recorded for an exited gate pass");
        if (!GatePassHandlerSupport.TryDecodeExpectedRowVersion(command.Request.RowVersion, gatePass.RowVersion, out var expectedRowVersion))
            return ApiResponse<GatePassDto>.Fail(GatePassHandlerSupport.ConcurrencyConflict);

        _repository.SetExpectedRowVersion(gatePass, expectedRowVersion);
        var now = _timeProvider.GetUtcNow();
        gatePass.UpdatedAt = now;
        gatePass.UpdatedByUserId = _currentUser.UserId!;
        var incident = GatePassHandlerSupport.Transition(
            gatePass, GatePassStatus.Exited, GatePassStatus.Exited, _currentUser.UserId!, RoleNames.SchoolManager,
            now, Guid.NewGuid(), reason);
        incident.MetadataJson = JsonSerializer.Serialize(new { Kind = "FalseExitIncident" });
        gatePass.Transitions.Add(incident);

        try { await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false); }
        catch (GatePassConcurrencyException) { return ApiResponse<GatePassDto>.Fail(GatePassHandlerSupport.ConcurrencyConflict); }

        var dto = await _repository.GetDtoAsync(schoolId, gatePass.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The annotated gate pass could not be loaded");
        return ApiResponse<GatePassDto>.Success(dto, "False-exit incident recorded without changing exit history");
    }
}
