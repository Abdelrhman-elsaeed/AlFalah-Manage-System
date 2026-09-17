using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.IntelligentTimetable.Handlers;

public sealed class GetTimetableSettingsQueryHandler
    : IRequestHandler<GetTimetableSettingsQuery, ApiResponse<TimetableSettingsOverviewDto>>
{
    private readonly ITimetableSettingsRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IBellScheduleRepository _timings;

    public GetTimetableSettingsQueryHandler(
        ITimetableSettingsRepository repository,
        ICurrentUserService currentUser,
        IBellScheduleRepository timings)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timings = timings;
    }

    public async Task<ApiResponse<TimetableSettingsOverviewDto>> Handle(
        GetTimetableSettingsQuery query,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        if (!_currentUser.IsAuthenticated || schoolId is null)
            return ApiResponse<TimetableSettingsOverviewDto>.Fail(TimetableSettingsHandlerSupport.AuthenticationRequired);
        if (!TimetableSettingsHandlerSupport.CanView(_currentUser))
            return ApiResponse<TimetableSettingsOverviewDto>.Fail(TimetableSettingsHandlerSupport.PermissionDenied);

        var schoolName = await _repository.GetSchoolNameAsync(schoolId.Value, cancellationToken).ConfigureAwait(false);
        if (schoolName is null)
            return ApiResponse<TimetableSettingsOverviewDto>.Fail("المدرسة النشطة غير موجودة أو غير مفعلة.");

        var years = await _repository.GetAcademicYearsAsync(schoolId.Value, cancellationToken).ConfigureAwait(false);
        var selectedYearId = query.AcademicYearId
            ?? years.FirstOrDefault(x => x.IsActive)?.Id
            ?? years.FirstOrDefault()?.Id
            ?? 0;
        var semester = query.Semester is not null && Enum.IsDefined(query.Semester.Value)
            ? query.Semester.Value
            : TimetableSemester.First;

        var profiles = selectedYearId > 0
            ? await _repository.GetProfilesAsync(schoolId.Value, selectedYearId, semester, cancellationToken).ConfigureAwait(false)
            : Array.Empty<TimetableSetupProfileDto>();
        var selectedProfile = query.ProfileId.HasValue
            ? profiles.FirstOrDefault(x => x.Id == query.ProfileId.Value)
            : profiles.FirstOrDefault();
        var readiness = selectedYearId > 0
            ? await _repository.GetReadinessDataAsync(
                schoolId.Value,
                selectedYearId,
                semester,
                selectedProfile?.Id,
                cancellationToken).ConfigureAwait(false)
            : new TimetableReadinessData(0, 0, 0, 0, 0, 0, 0, 0, [], false, false);
        var canManage = await TimetableSettingsHandlerSupport.CanManageAsync(
            _currentUser, _repository, schoolId.Value, cancellationToken).ConfigureAwait(false);
        var (steps, warnings) = TimetableSettingsHandlerSupport.BuildReadiness(selectedProfile, readiness);
        var completeCount = steps.Count(x => x.Status == "complete");
        var hardPrerequisitesValid = steps
            .Where(x => x.Key != "timetable")
            .All(x => x.Status == "complete");

        var overview = new TimetableSettingsOverviewDto(
            schoolId.Value,
            schoolName,
            years,
            profiles,
            selectedProfile,
            selectedYearId,
            (int)semester,
            TimetableSettingsHandlerSupport.SemesterLabel(semester),
            canManage,
            (int)Math.Round(completeCount * 100d / steps.Count),
            hardPrerequisitesValid,
            new TimetableReadinessCountsDto(
                readiness.ActiveClassrooms,
                readiness.ActiveStudents,
                readiness.ClassroomsMissingLocation,
                readiness.ActiveTeachers,
                readiness.AvailableSubjects),
            steps,
            warnings, selectedProfile is null ? null : await _timings.GetSelectedAsync(schoolId.Value, selectedYearId, semester, selectedProfile.Id, cancellationToken));

        return ApiResponse<TimetableSettingsOverviewDto>.Success(overview);
    }
}
