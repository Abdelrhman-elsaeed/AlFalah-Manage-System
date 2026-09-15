using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.IntelligentTimetable.Handlers;

public sealed class CreateTimetableAcademicYearCommandHandler
    : IRequestHandler<CreateTimetableAcademicYearCommand, ApiResponse<TimetableSetupAcademicYearDto>>
{
    private readonly ITimetableSettingsRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public CreateTimetableAcademicYearCommandHandler(
        ITimetableSettingsRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<TimetableSetupAcademicYearDto>> Handle(
        CreateTimetableAcademicYearCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (!_currentUser.IsAuthenticated || schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<TimetableSetupAcademicYearDto>.Fail(TimetableSettingsHandlerSupport.AuthenticationRequired);
        if (!await TimetableSettingsHandlerSupport.CanManageAsync(
                _currentUser, _repository, schoolId.Value, cancellationToken).ConfigureAwait(false))
            return ApiResponse<TimetableSetupAcademicYearDto>.Fail(TimetableSettingsHandlerSupport.PermissionDenied);

        var request = command.Request;
        var code = request.Code.Trim().ToUpperInvariant();
        var name = request.NameAr.Trim();
        var academicYear = await _repository.GetAcademicYearByCodeForUpdateAsync(code, cancellationToken)
            .ConfigureAwait(false);

        if (academicYear is not null
            && (academicYear.StartsOn != request.StartsOn || academicYear.EndsOn != request.EndsOn))
            return ApiResponse<TimetableSetupAcademicYearDto>.Fail(TimetableSettingsHandlerSupport.AcademicYearCodeConflict);

        var schoolTerms = await _repository.GetAcademicTermsForUpdateAsync(schoolId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (academicYear is not null && schoolTerms.Any(term => term.AcademicYearId == academicYear.Id))
            return ApiResponse<TimetableSetupAcademicYearDto>.Fail(TimetableSettingsHandlerSupport.DuplicateAcademicYearScope);

        var now = _timeProvider.GetUtcNow();
        academicYear ??= new AcademicYear
        {
            Code = code,
            NameAr = name,
            StartsOn = request.StartsOn,
            EndsOn = request.EndsOn,
            // Timetable activation is school-specific and lives on AcademicTerm.
            // Do not create a second globally-active year used by other modules.
            IsActive = false
        };

        await _repository.ExecuteInTransactionAsync(async ct =>
        {
            if (academicYear.Id == 0)
            {
                _repository.Add(academicYear);
                await _repository.SaveChangesAsync(ct).ConfigureAwait(false);
            }

            var activeTerms = schoolTerms.Where(term => term.IsActive).ToList();
            foreach (var activeTerm in activeTerms)
            {
                activeTerm.IsActive = false;
                activeTerm.UpdatedAt = now;
                activeTerm.UpdatedByUserId = userId;
            }
            if (activeTerms.Count > 0)
                await _repository.SaveChangesAsync(ct).ConfigureAwait(false);

            _repository.Add(CreateTerm(
                schoolId.Value,
                academicYear.Id,
                TimetableSemester.First,
                request.FirstSemesterStartsOn,
                request.FirstSemesterEndsOn,
                request.ActiveSemester == TimetableSemester.First,
                userId,
                now));
            _repository.Add(CreateTerm(
                schoolId.Value,
                academicYear.Id,
                TimetableSemester.Second,
                request.SecondSemesterStartsOn,
                request.SecondSemesterEndsOn,
                request.ActiveSemester == TimetableSemester.Second,
                userId,
                now));
            await _repository.SaveChangesAsync(ct).ConfigureAwait(false);

            _repository.WriteAcademicScopeAudit(schoolId.Value, userId, academicYear, request.ActiveSemester);
            await _repository.SaveChangesAsync(ct).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        return ApiResponse<TimetableSetupAcademicYearDto>.Success(
            new TimetableSetupAcademicYearDto(academicYear.Id, academicYear.Code, academicYear.NameAr, true),
            "تمت إضافة العام الدراسي وربطه بالمدرسة.");
    }

    private static AcademicTerm CreateTerm(
        int schoolId,
        int academicYearId,
        TimetableSemester semester,
        DateOnly startsOn,
        DateOnly endsOn,
        bool isActive,
        string userId,
        DateTimeOffset now) =>
        new()
        {
            SchoolId = schoolId,
            AcademicYearId = academicYearId,
            Semester = semester,
            StartsOn = startsOn,
            EndsOn = endsOn,
            IsActive = isActive,
            CreatedAt = now,
            CreatedByUserId = userId,
            UpdatedAt = now,
            UpdatedByUserId = userId
        };
}
