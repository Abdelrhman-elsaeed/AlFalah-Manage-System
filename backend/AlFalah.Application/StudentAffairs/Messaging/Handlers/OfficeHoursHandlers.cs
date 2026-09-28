using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Messaging;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Messaging.Handlers;

public sealed class GetEligibleOfficeHoursQueryHandler
    : IRequestHandler<GetEligibleOfficeHoursQuery, ApiResponse<OfficeHoursAggregateDto>>
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

    public async Task<ApiResponse<OfficeHoursAggregateDto>> Handle(
        GetEligibleOfficeHoursQuery request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<OfficeHoursAggregateDto>.Fail("An authenticated user and active school are required");

        if (!_currentUser.IsInRole(RoleNames.Instructor) || !_currentUser.HasPermission(PermissionNames.OfficeHoursManageOwn))
            return ApiResponse<OfficeHoursAggregateDto>.Fail("You do not have permission to perform this action");

        try
        {
            var result = await _repository.GetEligibleOfficeHoursAsync(schoolId.Value, userId, cancellationToken).ConfigureAwait(false);
            return ApiResponse<OfficeHoursAggregateDto>.Success(result);
        }
        catch (InvalidOperationException exception)
        {
            return ApiResponse<OfficeHoursAggregateDto>.Fail(exception.Message);
        }
    }
}

public sealed class GetMyOfficeHoursQueryHandler
    : IRequestHandler<GetMyOfficeHoursQuery, ApiResponse<OfficeHoursAggregateDto>>
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

    public async Task<ApiResponse<OfficeHoursAggregateDto>> Handle(
        GetMyOfficeHoursQuery request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<OfficeHoursAggregateDto>.Fail("An authenticated user and active school are required");

        if (!_currentUser.IsInRole(RoleNames.Instructor) || !_currentUser.HasPermission(PermissionNames.OfficeHoursManageOwn))
            return ApiResponse<OfficeHoursAggregateDto>.Fail("You do not have permission to perform this action");

        try
        {
            var result = await _repository.GetMyOfficeHoursAsync(schoolId.Value, userId, cancellationToken).ConfigureAwait(false);
            return ApiResponse<OfficeHoursAggregateDto>.Success(result);
        }
        catch (InvalidOperationException exception)
        {
            return ApiResponse<OfficeHoursAggregateDto>.Fail(exception.Message);
        }
    }
}

public sealed class UpdateMyOfficeHoursCommandHandler
    : IRequestHandler<UpdateMyOfficeHoursCommand, ApiResponse<OfficeHoursAggregateDto>>
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

    public async Task<ApiResponse<OfficeHoursAggregateDto>> Handle(
        UpdateMyOfficeHoursCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<OfficeHoursAggregateDto>.Fail("An authenticated user and active school are required");

        if (!_currentUser.IsInRole(RoleNames.Instructor) || !_currentUser.HasPermission(PermissionNames.OfficeHoursManageOwn))
            return ApiResponse<OfficeHoursAggregateDto>.Fail("You do not have permission to perform this action");

        try
        {
            var result = await _repository.UpdateMyOfficeHoursAsync(schoolId.Value, userId, command.Request, cancellationToken).ConfigureAwait(false);
            return ApiResponse<OfficeHoursAggregateDto>.Success(result, "Office hours updated successfully");
        }
        catch (InvalidOperationException exception)
        {
            return ApiResponse<OfficeHoursAggregateDto>.Fail(exception.Message);
        }
    }
}

public sealed class GetTeacherOfficeHoursQueryHandler
    : IRequestHandler<GetTeacherOfficeHoursQuery, ApiResponse<OfficeHoursAggregateDto>>
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

    public async Task<ApiResponse<OfficeHoursAggregateDto>> Handle(
        GetTeacherOfficeHoursQuery request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        if (schoolId is null || string.IsNullOrWhiteSpace(_currentUser.UserId))
            return ApiResponse<OfficeHoursAggregateDto>.Fail("An authenticated user and active school are required");

        if (!_currentUser.HasPermission(PermissionNames.OfficeHoursView))
            return ApiResponse<OfficeHoursAggregateDto>.Fail("You do not have permission to perform this action");

        try
        {
            var result = await _repository.GetTeacherOfficeHoursAsync(schoolId.Value, _currentUser.UserId!, request.InstructorId, cancellationToken).ConfigureAwait(false);
            return ApiResponse<OfficeHoursAggregateDto>.Success(result);
        }
        catch (InvalidOperationException exception)
        {
            return ApiResponse<OfficeHoursAggregateDto>.Fail(exception.Message);
        }
    }
}

public sealed class OverrideTeacherOfficeHoursCommandHandler
    : IRequestHandler<OverrideTeacherOfficeHoursCommand, ApiResponse<OfficeHoursAggregateDto>>
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

    public async Task<ApiResponse<OfficeHoursAggregateDto>> Handle(
        OverrideTeacherOfficeHoursCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<OfficeHoursAggregateDto>.Fail("An authenticated user and active school are required");

        if (!_currentUser.IsInRole(RoleNames.SchoolManager) || !_currentUser.HasPermission(PermissionNames.OfficeHoursManageSchool))
            return ApiResponse<OfficeHoursAggregateDto>.Fail("You do not have permission to perform this action");

        try
        {
            var result = await _repository.OverrideTeacherOfficeHoursAsync(schoolId.Value, userId, command.InstructorId, command.Request, cancellationToken).ConfigureAwait(false);
            return ApiResponse<OfficeHoursAggregateDto>.Success(result, "Teacher office hours overridden successfully");
        }
        catch (InvalidOperationException exception)
        {
            return ApiResponse<OfficeHoursAggregateDto>.Fail(exception.Message);
        }
    }
}
