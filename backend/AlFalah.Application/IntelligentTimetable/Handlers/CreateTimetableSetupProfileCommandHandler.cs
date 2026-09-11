using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.IntelligentTimetable.Handlers;

public sealed class CreateTimetableSetupProfileCommandHandler
    : IRequestHandler<CreateTimetableSetupProfileCommand, ApiResponse<TimetableSetupProfileDto>>
{
    private readonly ITimetableSettingsRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public CreateTimetableSetupProfileCommandHandler(
        ITimetableSettingsRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<TimetableSetupProfileDto>> Handle(
        CreateTimetableSetupProfileCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (!_currentUser.IsAuthenticated || schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<TimetableSetupProfileDto>.Fail(TimetableSettingsHandlerSupport.AuthenticationRequired);
        if (!await TimetableSettingsHandlerSupport.CanManageAsync(
                _currentUser, _repository, schoolId.Value, cancellationToken).ConfigureAwait(false))
            return ApiResponse<TimetableSetupProfileDto>.Fail(TimetableSettingsHandlerSupport.PermissionDenied);

        var request = command.Request;
        var name = request.Name.Trim();
        if (!await _repository.AcademicScopeExistsAsync(
                schoolId.Value, request.AcademicYearId, request.Semester, cancellationToken).ConfigureAwait(false))
            return ApiResponse<TimetableSetupProfileDto>.Fail(TimetableSettingsHandlerSupport.InvalidAcademicScope);
        if (await _repository.ProfileNameExistsAsync(
                schoolId.Value, request.AcademicYearId, request.Semester, name, null, cancellationToken).ConfigureAwait(false))
            return ApiResponse<TimetableSetupProfileDto>.Fail(TimetableSettingsHandlerSupport.DuplicateName);

        var now = _timeProvider.GetUtcNow();
        var profile = new TimetableSetupProfile
        {
            SchoolId = schoolId.Value,
            AcademicYearId = request.AcademicYearId,
            Semester = request.Semester,
            Name = name,
            Status = TimetableSetupStatus.Draft,
            CreatedAt = now,
            CreatedByUserId = userId,
            UpdatedAt = now,
            UpdatedByUserId = userId
        };

        await _repository.ExecuteInTransactionAsync(async ct =>
        {
            _repository.Add(profile);
            await _repository.SaveChangesAsync(ct).ConfigureAwait(false);
            _repository.WriteAudit(
                schoolId.Value,
                userId,
                "Timetable.SetupProfile.Created",
                profile,
                null,
                new { profile.Name, profile.AcademicYearId, profile.Semester, profile.Status, profile.Revision });
            await _repository.SaveChangesAsync(ct).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        var dto = await _repository.GetProfileDtoAsync(schoolId.Value, profile.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("تعذر تحميل ملف الإعداد بعد إنشائه.");
        return ApiResponse<TimetableSetupProfileDto>.Success(dto, "تم إنشاء ملف إعداد الجدول.");
    }
}
