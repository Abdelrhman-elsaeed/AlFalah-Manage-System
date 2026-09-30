using System;
using System.Threading;
using System.Threading.Tasks;
using AlFalah.Application.Interfaces;
using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.StudentAffairs.DTOs.Dashboards;
using AlFalah.Application.StudentAffairs.GatePasses;
using AlFalah.Application.StudentAffairs.Students;
using AlFalah.Application.StudentAffairs.Students.Handlers;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Dashboards.Handlers;

public sealed class GetTeacherStudentAffairsDashboardQueryHandler
    : IRequestHandler<GetTeacherStudentAffairsDashboardQuery, ApiResponse<TeacherStudentAffairsDashboardDto>>
{
    private readonly IStudentWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public GetTeacherStudentAffairsDashboardQueryHandler(
        IStudentWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<TeacherStudentAffairsDashboardDto>> Handle(
        GetTeacherStudentAffairsDashboardQuery query,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<TeacherStudentAffairsDashboardDto>.Fail(StudentHandlerSupport.AuthenticationRequired);

        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().DateTime);
        var result = await _repository.GetTeacherDashboardAsync(
            schoolId.Value,
            userId,
            today,
            cancellationToken).ConfigureAwait(false);

        return ApiResponse<TeacherStudentAffairsDashboardDto>.Success(result);
    }
}

public sealed class GetOfficerStudentAffairsDashboardQueryHandler
    : IRequestHandler<GetOfficerStudentAffairsDashboardQuery, ApiResponse<OfficerStudentAffairsDashboardDto>>
{
    private readonly IStudentWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public GetOfficerStudentAffairsDashboardQueryHandler(
        IStudentWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<OfficerStudentAffairsDashboardDto>> Handle(
        GetOfficerStudentAffairsDashboardQuery query,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<OfficerStudentAffairsDashboardDto>.Fail(StudentHandlerSupport.AuthenticationRequired);

        if (!_currentUser.IsInRole(RoleNames.StudentAffairsOfficer)
            || !_currentUser.HasPermission(PermissionNames.StudentAffairsDashboardOfficer))
        {
            return ApiResponse<OfficerStudentAffairsDashboardDto>.Fail(StudentHandlerSupport.PermissionDenied);
        }

        var now = _timeProvider.GetUtcNow();
        var result = await _repository.GetOfficerDashboardAsync(
            schoolId.Value,
            userId,
            now,
            cancellationToken).ConfigureAwait(false);

        return ApiResponse<OfficerStudentAffairsDashboardDto>.Success(result);
    }
}

public sealed class GetSocialWorkerStudentAffairsDashboardQueryHandler
    : IRequestHandler<GetSocialWorkerStudentAffairsDashboardQuery, ApiResponse<SocialWorkerStudentAffairsDashboardDto>>
{
    private readonly IStudentWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public GetSocialWorkerStudentAffairsDashboardQueryHandler(
        IStudentWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<SocialWorkerStudentAffairsDashboardDto>> Handle(
        GetSocialWorkerStudentAffairsDashboardQuery query,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<SocialWorkerStudentAffairsDashboardDto>.Fail(StudentHandlerSupport.AuthenticationRequired);

        if (!_currentUser.IsInRole(RoleNames.SocialWorker)
            || !_currentUser.HasPermission(PermissionNames.StudentAffairsDashboardSocialWorker))
            return ApiResponse<SocialWorkerStudentAffairsDashboardDto>.Fail(StudentHandlerSupport.PermissionDenied);

        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().DateTime);
        var result = await _repository.GetSocialWorkerDashboardAsync(
            schoolId.Value,
            userId,
            today,
            cancellationToken).ConfigureAwait(false);

        return ApiResponse<SocialWorkerStudentAffairsDashboardDto>.Success(result);
    }
}

public sealed class GetSecurityStudentAffairsDashboardQueryHandler
    : IRequestHandler<GetSecurityStudentAffairsDashboardQuery, ApiResponse<SecurityStudentAffairsDashboardDto>>
{
    private readonly IGatePassWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public GetSecurityStudentAffairsDashboardQueryHandler(
        IGatePassWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<SecurityStudentAffairsDashboardDto>> Handle(
        GetSecurityStudentAffairsDashboardQuery query,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<SecurityStudentAffairsDashboardDto>.Fail(StudentHandlerSupport.AuthenticationRequired);

        if (!_currentUser.IsInRole(RoleNames.SecurityGuard)
            || !_currentUser.HasPermission(PermissionNames.StudentAffairsDashboardSecurity))
            return ApiResponse<SecurityStudentAffairsDashboardDto>.Fail(StudentHandlerSupport.PermissionDenied);

        var now = _timeProvider.GetUtcNow();
        var result = await _repository.GetSecurityDashboardAsync(
            schoolId.Value,
            now,
            cancellationToken).ConfigureAwait(false);

        return ApiResponse<SecurityStudentAffairsDashboardDto>.Success(result);
    }
}

public sealed class GetGuardianStudentAffairsDashboardQueryHandler
    : IRequestHandler<GetGuardianStudentAffairsDashboardQuery, ApiResponse<GuardianStudentAffairsDashboardDto>>
{
    private readonly IStudentWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;
    private readonly ISchoolLocalDateResolver _schoolLocalDateResolver;

    public GetGuardianStudentAffairsDashboardQueryHandler(
        IStudentWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider,
        ISchoolLocalDateResolver schoolLocalDateResolver)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
        _schoolLocalDateResolver = schoolLocalDateResolver;
    }

    public async Task<ApiResponse<GuardianStudentAffairsDashboardDto>> Handle(
        GetGuardianStudentAffairsDashboardQuery query,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<GuardianStudentAffairsDashboardDto>.Fail(StudentHandlerSupport.AuthenticationRequired);

        if (!_currentUser.IsInRole(RoleNames.Guardian)
            || !_currentUser.HasPermission(PermissionNames.StudentAffairsDashboardGuardian)
            || !_currentUser.HasPermission(PermissionNames.GuardianViewLinkedStudents))
            return ApiResponse<GuardianStudentAffairsDashboardDto>.Fail(StudentHandlerSupport.PermissionDenied);

        if (!await _repository.IsActiveGuardianProfileAsync(
                schoolId.Value,
                userId,
                cancellationToken).ConfigureAwait(false))
            return ApiResponse<GuardianStudentAffairsDashboardDto>.Fail(StudentHandlerSupport.NotFound);

        var now = _timeProvider.GetUtcNow();
        var today = await _schoolLocalDateResolver.ResolveAsync(
            schoolId.Value,
            now,
            cancellationToken).ConfigureAwait(false);
        if (today is null)
            return ApiResponse<GuardianStudentAffairsDashboardDto>.Fail("School local date could not be resolved");
        var result = await _repository.GetGuardianDashboardAsync(
            schoolId.Value,
            userId,
            today.Value,
            now,
            cancellationToken).ConfigureAwait(false);

        return ApiResponse<GuardianStudentAffairsDashboardDto>.Success(result);
    }
}

public sealed class GetSchoolOversightDashboardQueryHandler
    : IRequestHandler<GetSchoolOversightDashboardQuery, ApiResponse<SchoolOversightDashboardDto>>
{
    private readonly IStudentWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;
    private readonly ISchoolLocalDateResolver _schoolLocalDateResolver;

    public GetSchoolOversightDashboardQueryHandler(
        IStudentWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider,
        ISchoolLocalDateResolver schoolLocalDateResolver)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
        _schoolLocalDateResolver = schoolLocalDateResolver;
    }

    public async Task<ApiResponse<SchoolOversightDashboardDto>> Handle(
        GetSchoolOversightDashboardQuery query,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<SchoolOversightDashboardDto>.Fail(StudentHandlerSupport.AuthenticationRequired);

        if (!_currentUser.IsInRole(RoleNames.SchoolManager)
            || !_currentUser.HasPermission(PermissionNames.StudentAffairsDashboardSchoolOversight))
            return ApiResponse<SchoolOversightDashboardDto>.Fail(StudentHandlerSupport.PermissionDenied);

        var now = _timeProvider.GetUtcNow();
        var today = await _schoolLocalDateResolver.ResolveAsync(
            schoolId.Value,
            now,
            cancellationToken).ConfigureAwait(false);
        if (today is null)
            return ApiResponse<SchoolOversightDashboardDto>.Fail("School local date could not be resolved");

        var result = await _repository.GetSchoolOversightDashboardAsync(
            schoolId.Value,
            today.Value,
            now,
            cancellationToken).ConfigureAwait(false);

        return ApiResponse<SchoolOversightDashboardDto>.Success(result);
    }
}
