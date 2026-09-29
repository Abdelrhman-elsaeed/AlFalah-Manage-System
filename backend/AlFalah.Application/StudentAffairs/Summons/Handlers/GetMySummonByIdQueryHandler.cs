using AlFalah.Application.Interfaces;
using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.StudentAffairs.DTOs.Summons;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Summons.Handlers;

public sealed class GetMySummonByIdQueryHandler
    : IRequestHandler<GetMySummonByIdQuery, ApiResponse<GuardianSummonDto>>
{
    private readonly ISummonWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly ISchoolLocalDateResolver _localDateResolver;
    private readonly TimeProvider _timeProvider;

    public GetMySummonByIdQueryHandler(
        ISummonWorkflowRepository repository,
        ICurrentUserService currentUser,
        ISchoolLocalDateResolver localDateResolver,
        TimeProvider timeProvider) =>
        (_repository, _currentUser, _localDateResolver, _timeProvider) =
        (repository, currentUser, localDateResolver, timeProvider);

    public async Task<ApiResponse<GuardianSummonDto>> Handle(
        GetMySummonByIdQuery request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<GuardianSummonDto>.Fail(SummonHandlerSupport.AuthenticationRequired);
        if (!_currentUser.IsInRole(RoleNames.Guardian)
            || !_currentUser.HasPermission(PermissionNames.GuardianViewLinkedStudents))
            return ApiResponse<GuardianSummonDto>.Fail(SummonHandlerSupport.PermissionDenied);
        if (!await _repository.IsActiveGuardianAsync(schoolId.Value, userId, cancellationToken).ConfigureAwait(false))
            return ApiResponse<GuardianSummonDto>.Fail(SummonHandlerSupport.PermissionDenied);

        var localDate = await _localDateResolver.ResolveAsync(
            schoolId.Value, _timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        if (localDate is null)
            return ApiResponse<GuardianSummonDto>.Fail("School local date could not be resolved");

        var summon = await _repository.GetMySummonAsync(
            schoolId.Value, userId, request.SummonId, localDate.Value, cancellationToken).ConfigureAwait(false);
        return summon is null
            ? ApiResponse<GuardianSummonDto>.Fail(SummonHandlerSupport.NotFound)
            : ApiResponse<GuardianSummonDto>.Success(summon);
    }
}
