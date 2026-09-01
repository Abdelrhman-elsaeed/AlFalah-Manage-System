using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Messaging;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Messaging.Handlers;

public sealed class GetEligibleOfficeHoursQueryHandler
    : IRequestHandler<GetEligibleOfficeHoursQuery, ApiResponse<IReadOnlyList<OfficeHourSlotDto>>>
{
    private readonly IMessagingWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetEligibleOfficeHoursQueryHandler(
        IMessagingWorkflowRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<IReadOnlyList<OfficeHourSlotDto>>> Handle(
        GetEligibleOfficeHoursQuery request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<IReadOnlyList<OfficeHourSlotDto>>.Fail("An authenticated user and active school are required");

        if (!_currentUser.HasPermission(PermissionNames.OfficeHoursManageOwn))
            return ApiResponse<IReadOnlyList<OfficeHourSlotDto>>.Fail("You do not have permission to perform this action");

        var result = await _repository.GetEligibleOfficeHoursAsync(schoolId.Value, userId, cancellationToken).ConfigureAwait(false);
        return ApiResponse<IReadOnlyList<OfficeHourSlotDto>>.Success(result);
    }
}

public sealed class GetMyOfficeHoursQueryHandler
    : IRequestHandler<GetMyOfficeHoursQuery, ApiResponse<IReadOnlyList<OfficeHourSlotDto>>>
{
    private readonly IMessagingWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetMyOfficeHoursQueryHandler(
        IMessagingWorkflowRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<IReadOnlyList<OfficeHourSlotDto>>> Handle(
        GetMyOfficeHoursQuery request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<IReadOnlyList<OfficeHourSlotDto>>.Fail("An authenticated user and active school are required");

        if (!_currentUser.HasPermission(PermissionNames.OfficeHoursManageOwn))
            return ApiResponse<IReadOnlyList<OfficeHourSlotDto>>.Fail("You do not have permission to perform this action");

        var result = await _repository.GetMyOfficeHoursAsync(schoolId.Value, userId, cancellationToken).ConfigureAwait(false);
        return ApiResponse<IReadOnlyList<OfficeHourSlotDto>>.Success(result);
    }
}

public sealed class UpdateMyOfficeHoursCommandHandler
    : IRequestHandler<UpdateMyOfficeHoursCommand, ApiResponse<IReadOnlyList<OfficeHourSlotDto>>>
{
    private readonly IMessagingWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public UpdateMyOfficeHoursCommandHandler(
        IMessagingWorkflowRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<IReadOnlyList<OfficeHourSlotDto>>> Handle(
        UpdateMyOfficeHoursCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<IReadOnlyList<OfficeHourSlotDto>>.Fail("An authenticated user and active school are required");

        if (!_currentUser.HasPermission(PermissionNames.OfficeHoursManageOwn))
            return ApiResponse<IReadOnlyList<OfficeHourSlotDto>>.Fail("You do not have permission to perform this action");

        var result = await _repository.UpdateMyOfficeHoursAsync(schoolId.Value, userId, command.Request, cancellationToken).ConfigureAwait(false);
        return ApiResponse<IReadOnlyList<OfficeHourSlotDto>>.Success(result, "Office hours updated successfully");
    }
}

public sealed class GetTeacherOfficeHoursQueryHandler
    : IRequestHandler<GetTeacherOfficeHoursQuery, ApiResponse<IReadOnlyList<OfficeHourSlotDto>>>
{
    private readonly IMessagingWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public GetTeacherOfficeHoursQueryHandler(
        IMessagingWorkflowRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<IReadOnlyList<OfficeHourSlotDto>>> Handle(
        GetTeacherOfficeHoursQuery request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        if (schoolId is null || string.IsNullOrWhiteSpace(_currentUser.UserId))
            return ApiResponse<IReadOnlyList<OfficeHourSlotDto>>.Fail("An authenticated user and active school are required");

        if (!_currentUser.HasPermission(PermissionNames.OfficeHoursView))
            return ApiResponse<IReadOnlyList<OfficeHourSlotDto>>.Fail("You do not have permission to perform this action");

        var result = await _repository.GetTeacherOfficeHoursAsync(schoolId.Value, request.InstructorId, cancellationToken).ConfigureAwait(false);
        return ApiResponse<IReadOnlyList<OfficeHourSlotDto>>.Success(result);
    }
}

public sealed class OverrideTeacherOfficeHoursCommandHandler
    : IRequestHandler<OverrideTeacherOfficeHoursCommand, ApiResponse<IReadOnlyList<OfficeHourSlotDto>>>
{
    private readonly IMessagingWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public OverrideTeacherOfficeHoursCommandHandler(
        IMessagingWorkflowRepository repository,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<IReadOnlyList<OfficeHourSlotDto>>> Handle(
        OverrideTeacherOfficeHoursCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<IReadOnlyList<OfficeHourSlotDto>>.Fail("An authenticated user and active school are required");

        if (!_currentUser.HasPermission(PermissionNames.OfficeHoursManageSchool))
            return ApiResponse<IReadOnlyList<OfficeHourSlotDto>>.Fail("You do not have permission to perform this action");

        var result = await _repository.OverrideTeacherOfficeHoursAsync(schoolId.Value, userId, command.InstructorId, command.Request, cancellationToken).ConfigureAwait(false);
        return ApiResponse<IReadOnlyList<OfficeHourSlotDto>>.Success(result, "Teacher office hours overridden successfully");
    }
}
