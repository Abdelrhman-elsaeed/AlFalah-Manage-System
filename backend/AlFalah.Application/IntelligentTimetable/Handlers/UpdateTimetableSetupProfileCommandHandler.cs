using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Application.Interfaces;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.IntelligentTimetable.Handlers;

public sealed class UpdateTimetableSetupProfileCommandHandler
    : IRequestHandler<UpdateTimetableSetupProfileCommand, ApiResponse<TimetableSetupProfileDto>>
{
    private readonly ITimetableSettingsRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public UpdateTimetableSetupProfileCommandHandler(
        ITimetableSettingsRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<TimetableSetupProfileDto>> Handle(
        UpdateTimetableSetupProfileCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (!_currentUser.IsAuthenticated || schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<TimetableSetupProfileDto>.Fail(TimetableSettingsHandlerSupport.AuthenticationRequired);
        if (!await TimetableSettingsHandlerSupport.CanManageAsync(
                _currentUser, _repository, schoolId.Value, cancellationToken).ConfigureAwait(false))
            return ApiResponse<TimetableSetupProfileDto>.Fail(TimetableSettingsHandlerSupport.PermissionDenied);

        var profile = await _repository.GetProfileForUpdateAsync(
            schoolId.Value, command.ProfileId, cancellationToken).ConfigureAwait(false);
        if (profile is null)
            return ApiResponse<TimetableSetupProfileDto>.Fail(TimetableSettingsHandlerSupport.NotFound);
        if (profile.Revision != command.Request.Revision)
            return ApiResponse<TimetableSetupProfileDto>.Fail(TimetableSettingsHandlerSupport.ConcurrencyConflict);

        var name = command.Request.Name.Trim();
        if (await _repository.ProfileNameExistsAsync(
                schoolId.Value,
                profile.AcademicYearId,
                profile.Semester,
                name,
                profile.Id,
                cancellationToken).ConfigureAwait(false))
            return ApiResponse<TimetableSetupProfileDto>.Fail(TimetableSettingsHandlerSupport.DuplicateName);

        var oldValues = new { profile.Name, profile.Status, profile.Revision, profile.UpdatedAt };
        profile.Name = name;
        profile.Revision++;
        profile.UpdatedAt = _timeProvider.GetUtcNow();
        profile.UpdatedByUserId = userId;
        _repository.WriteAudit(
            schoolId.Value,
            userId,
            "Timetable.SetupProfile.Updated",
            profile,
            oldValues,
            new { profile.Name, profile.Status, profile.Revision, profile.UpdatedAt });

        try
        {
            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return ApiResponse<TimetableSetupProfileDto>.Fail(TimetableSettingsHandlerSupport.ConcurrencyConflict);
        }

        var dto = await _repository.GetProfileDtoAsync(schoolId.Value, profile.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("تعذر تحميل ملف الإعداد بعد تحديثه.");
        return ApiResponse<TimetableSetupProfileDto>.Success(dto, "تم حفظ إعدادات الجدول.");
    }
}
