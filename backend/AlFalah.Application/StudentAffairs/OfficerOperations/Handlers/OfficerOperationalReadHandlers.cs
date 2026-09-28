using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Behaviors;
using AlFalah.Application.StudentAffairs.DTOs.Delays;
using AlFalah.Application.StudentAffairs.DTOs.Recognitions;
using AlFalah.Application.StudentAffairs.TeacherActions;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.OfficerOperations.Handlers;

internal static class OfficerReadAccess
{
    public const string AuthenticationRequired = "An authenticated user and active school are required";
    public const string PermissionDenied = "You do not have permission to view officer operational records";
    public const string NotFound = "Operational record was not found";

    public static bool CanRead(ICurrentUserService user, string permission) =>
        user.IsInRole(RoleNames.StudentAffairsOfficer) && user.HasPermission(permission);
}

public sealed class GetAcademicConcernsQueryHandler
    : IRequestHandler<GetAcademicConcernsQuery, ApiResponse<PagedResult<AcademicConcernDto>>>
{
    private readonly IOfficerOperationalReadRepository _repository;
    private readonly ICurrentUserService _user;
    public GetAcademicConcernsQueryHandler(IOfficerOperationalReadRepository repository, ICurrentUserService user) => (_repository, _user) = (repository, user);
    public async Task<ApiResponse<PagedResult<AcademicConcernDto>>> Handle(GetAcademicConcernsQuery request, CancellationToken ct)
    {
        if (!_user.IsAuthenticated || _user.ActiveSchoolId is not { } schoolId || string.IsNullOrWhiteSpace(_user.UserId)) return ApiResponse<PagedResult<AcademicConcernDto>>.Fail(OfficerReadAccess.AuthenticationRequired);
        if (!OfficerReadAccess.CanRead(_user, PermissionNames.AcademicConcernView)) return ApiResponse<PagedResult<AcademicConcernDto>>.Fail(OfficerReadAccess.PermissionDenied);
        return ApiResponse<PagedResult<AcademicConcernDto>>.Success(await _repository.GetAcademicConcernsAsync(schoolId, request.Query, ct).ConfigureAwait(false));
    }
}

public sealed class GetBehaviorIncidentsQueryHandler
    : IRequestHandler<GetBehaviorIncidentsQuery, ApiResponse<PagedResult<BehaviorIncidentDto>>>
{
    private readonly IOfficerOperationalReadRepository _repository;
    private readonly ICurrentUserService _user;
    public GetBehaviorIncidentsQueryHandler(IOfficerOperationalReadRepository repository, ICurrentUserService user) => (_repository, _user) = (repository, user);
    public async Task<ApiResponse<PagedResult<BehaviorIncidentDto>>> Handle(GetBehaviorIncidentsQuery request, CancellationToken ct)
    {
        if (!_user.IsAuthenticated || _user.ActiveSchoolId is not { } schoolId || string.IsNullOrWhiteSpace(_user.UserId)) return ApiResponse<PagedResult<BehaviorIncidentDto>>.Fail(OfficerReadAccess.AuthenticationRequired);
        if (!OfficerReadAccess.CanRead(_user, PermissionNames.BehaviorView)) return ApiResponse<PagedResult<BehaviorIncidentDto>>.Fail(OfficerReadAccess.PermissionDenied);
        return ApiResponse<PagedResult<BehaviorIncidentDto>>.Success(await _repository.GetBehaviorIncidentsAsync(schoolId, request.Query, ct).ConfigureAwait(false));
    }
}

public sealed class GetMorningDelaysQueryHandler
    : IRequestHandler<GetMorningDelaysQuery, ApiResponse<PagedResult<MorningDelayDto>>>
{
    private readonly IOfficerOperationalReadRepository _repository;
    private readonly ICurrentUserService _user;
    public GetMorningDelaysQueryHandler(IOfficerOperationalReadRepository repository, ICurrentUserService user) => (_repository, _user) = (repository, user);
    public async Task<ApiResponse<PagedResult<MorningDelayDto>>> Handle(GetMorningDelaysQuery request, CancellationToken ct)
    {
        if (!_user.IsAuthenticated || _user.ActiveSchoolId is not { } schoolId || string.IsNullOrWhiteSpace(_user.UserId)) return ApiResponse<PagedResult<MorningDelayDto>>.Fail(OfficerReadAccess.AuthenticationRequired);
        if (!OfficerReadAccess.CanRead(_user, PermissionNames.MorningDelayView)) return ApiResponse<PagedResult<MorningDelayDto>>.Fail(OfficerReadAccess.PermissionDenied);
        return ApiResponse<PagedResult<MorningDelayDto>>.Success(await _repository.GetMorningDelaysAsync(schoolId, request.Query, ct).ConfigureAwait(false));
    }
}

public sealed class GetSessionDelaysQueryHandler
    : IRequestHandler<GetSessionDelaysQuery, ApiResponse<PagedResult<SessionDelayDto>>>
{
    private readonly IOfficerOperationalReadRepository _repository;
    private readonly ICurrentUserService _user;
    public GetSessionDelaysQueryHandler(IOfficerOperationalReadRepository repository, ICurrentUserService user) => (_repository, _user) = (repository, user);
    public async Task<ApiResponse<PagedResult<SessionDelayDto>>> Handle(GetSessionDelaysQuery request, CancellationToken ct)
    {
        if (!_user.IsAuthenticated || _user.ActiveSchoolId is not { } schoolId || string.IsNullOrWhiteSpace(_user.UserId)) return ApiResponse<PagedResult<SessionDelayDto>>.Fail(OfficerReadAccess.AuthenticationRequired);
        if (!OfficerReadAccess.CanRead(_user, PermissionNames.SessionDelayView)) return ApiResponse<PagedResult<SessionDelayDto>>.Fail(OfficerReadAccess.PermissionDenied);
        return ApiResponse<PagedResult<SessionDelayDto>>.Success(await _repository.GetSessionDelaysAsync(schoolId, request.Query, ct).ConfigureAwait(false));
    }
}

public sealed class GetRecognitionsQueryHandler
    : IRequestHandler<GetRecognitionsQuery, ApiResponse<PagedResult<RecognitionDto>>>
{
    private readonly IOfficerOperationalReadRepository _repository;
    private readonly ICurrentUserService _user;
    public GetRecognitionsQueryHandler(IOfficerOperationalReadRepository repository, ICurrentUserService user) => (_repository, _user) = (repository, user);
    public async Task<ApiResponse<PagedResult<RecognitionDto>>> Handle(GetRecognitionsQuery request, CancellationToken ct)
    {
        if (!_user.IsAuthenticated || _user.ActiveSchoolId is not { } schoolId || string.IsNullOrWhiteSpace(_user.UserId)) return ApiResponse<PagedResult<RecognitionDto>>.Fail(OfficerReadAccess.AuthenticationRequired);
        if (!OfficerReadAccess.CanRead(_user, PermissionNames.RecognitionView)) return ApiResponse<PagedResult<RecognitionDto>>.Fail(OfficerReadAccess.PermissionDenied);
        return ApiResponse<PagedResult<RecognitionDto>>.Success(await _repository.GetRecognitionsAsync(schoolId, request.Query, ct).ConfigureAwait(false));
    }
}

public sealed class GetMorningDelayByIdQueryHandler : IRequestHandler<GetMorningDelayByIdQuery, ApiResponse<MorningDelayDto>>
{
    private readonly IOfficerOperationalReadRepository _repository; private readonly ICurrentUserService _user;
    public GetMorningDelayByIdQueryHandler(IOfficerOperationalReadRepository repository, ICurrentUserService user) => (_repository, _user) = (repository, user);
    public async Task<ApiResponse<MorningDelayDto>> Handle(GetMorningDelayByIdQuery request, CancellationToken ct)
    {
        if (!_user.IsAuthenticated || _user.ActiveSchoolId is not { } schoolId || string.IsNullOrWhiteSpace(_user.UserId)) return ApiResponse<MorningDelayDto>.Fail(OfficerReadAccess.AuthenticationRequired);
        if (!OfficerReadAccess.CanRead(_user, PermissionNames.MorningDelayView)) return ApiResponse<MorningDelayDto>.Fail(OfficerReadAccess.PermissionDenied);
        var dto = await _repository.GetMorningDelayAsync(schoolId, request.DelayId, ct).ConfigureAwait(false);
        return dto is null ? ApiResponse<MorningDelayDto>.Fail(OfficerReadAccess.NotFound) : ApiResponse<MorningDelayDto>.Success(dto);
    }
}

public sealed class GetAcademicConcernByIdQueryHandler : IRequestHandler<GetAcademicConcernByIdQuery, ApiResponse<AcademicConcernDto>>
{
    private readonly ITeacherActionWorkflowRepository _repository; private readonly ICurrentUserService _user;
    public GetAcademicConcernByIdQueryHandler(ITeacherActionWorkflowRepository repository, ICurrentUserService user) => (_repository, _user) = (repository, user);
    public async Task<ApiResponse<AcademicConcernDto>> Handle(GetAcademicConcernByIdQuery request, CancellationToken ct) => await Read(request.ConcernId, ct);
    private async Task<ApiResponse<AcademicConcernDto>> Read(int id, CancellationToken ct)
    {
        if (!_user.IsAuthenticated || _user.ActiveSchoolId is not { } schoolId || string.IsNullOrWhiteSpace(_user.UserId)) return ApiResponse<AcademicConcernDto>.Fail(OfficerReadAccess.AuthenticationRequired);
        if (!OfficerReadAccess.CanRead(_user, PermissionNames.AcademicConcernView)) return ApiResponse<AcademicConcernDto>.Fail(OfficerReadAccess.PermissionDenied);
        var dto = await _repository.GetAcademicConcernDtoAsync(schoolId, id, ct).ConfigureAwait(false);
        return dto is null ? ApiResponse<AcademicConcernDto>.Fail(OfficerReadAccess.NotFound) : ApiResponse<AcademicConcernDto>.Success(dto);
    }
}

public sealed class GetBehaviorIncidentByIdQueryHandler : IRequestHandler<GetBehaviorIncidentByIdQuery, ApiResponse<BehaviorIncidentDto>>
{
    private readonly ITeacherActionWorkflowRepository _repository; private readonly ICurrentUserService _user;
    public GetBehaviorIncidentByIdQueryHandler(ITeacherActionWorkflowRepository repository, ICurrentUserService user) => (_repository, _user) = (repository, user);
    public async Task<ApiResponse<BehaviorIncidentDto>> Handle(GetBehaviorIncidentByIdQuery request, CancellationToken ct)
    {
        if (!_user.IsAuthenticated || _user.ActiveSchoolId is not { } schoolId || string.IsNullOrWhiteSpace(_user.UserId)) return ApiResponse<BehaviorIncidentDto>.Fail(OfficerReadAccess.AuthenticationRequired);
        if (!OfficerReadAccess.CanRead(_user, PermissionNames.BehaviorView)) return ApiResponse<BehaviorIncidentDto>.Fail(OfficerReadAccess.PermissionDenied);
        var dto = await _repository.GetBehaviorDtoAsync(schoolId, request.IncidentId, ct).ConfigureAwait(false);
        return dto is null ? ApiResponse<BehaviorIncidentDto>.Fail(OfficerReadAccess.NotFound) : ApiResponse<BehaviorIncidentDto>.Success(dto);
    }
}

public sealed class GetSessionDelayByIdQueryHandler : IRequestHandler<GetSessionDelayByIdQuery, ApiResponse<SessionDelayDto>>
{
    private readonly ITeacherActionWorkflowRepository _repository; private readonly ICurrentUserService _user;
    public GetSessionDelayByIdQueryHandler(ITeacherActionWorkflowRepository repository, ICurrentUserService user) => (_repository, _user) = (repository, user);
    public async Task<ApiResponse<SessionDelayDto>> Handle(GetSessionDelayByIdQuery request, CancellationToken ct)
    {
        if (!_user.IsAuthenticated || _user.ActiveSchoolId is not { } schoolId || string.IsNullOrWhiteSpace(_user.UserId)) return ApiResponse<SessionDelayDto>.Fail(OfficerReadAccess.AuthenticationRequired);
        if (!OfficerReadAccess.CanRead(_user, PermissionNames.SessionDelayView)) return ApiResponse<SessionDelayDto>.Fail(OfficerReadAccess.PermissionDenied);
        var dto = await _repository.GetSessionDelayDtoAsync(schoolId, request.DelayId, ct).ConfigureAwait(false);
        return dto is null ? ApiResponse<SessionDelayDto>.Fail(OfficerReadAccess.NotFound) : ApiResponse<SessionDelayDto>.Success(dto);
    }
}

public sealed class GetRecognitionByIdQueryHandler : IRequestHandler<GetRecognitionByIdQuery, ApiResponse<RecognitionDto>>
{
    private readonly ITeacherActionWorkflowRepository _repository; private readonly ICurrentUserService _user;
    public GetRecognitionByIdQueryHandler(ITeacherActionWorkflowRepository repository, ICurrentUserService user) => (_repository, _user) = (repository, user);
    public async Task<ApiResponse<RecognitionDto>> Handle(GetRecognitionByIdQuery request, CancellationToken ct)
    {
        if (!_user.IsAuthenticated || _user.ActiveSchoolId is not { } schoolId || string.IsNullOrWhiteSpace(_user.UserId)) return ApiResponse<RecognitionDto>.Fail(OfficerReadAccess.AuthenticationRequired);
        if (!OfficerReadAccess.CanRead(_user, PermissionNames.RecognitionView)) return ApiResponse<RecognitionDto>.Fail(OfficerReadAccess.PermissionDenied);
        var dto = await _repository.GetRecognitionDtoAsync(schoolId, request.RecognitionId, ct).ConfigureAwait(false);
        return dto is null ? ApiResponse<RecognitionDto>.Fail(OfficerReadAccess.NotFound) : ApiResponse<RecognitionDto>.Success(dto);
    }
}
