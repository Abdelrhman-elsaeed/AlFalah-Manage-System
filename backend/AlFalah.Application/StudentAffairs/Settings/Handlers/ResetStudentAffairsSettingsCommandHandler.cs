using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Settings;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Settings.Handlers;

public sealed class ResetStudentAffairsSettingsCommandHandler
    : IRequestHandler<ResetStudentAffairsSettingsCommand, ApiResponse<SchoolStudentAffairsSettingsDto>>
{
    private readonly IStudentAffairsSettingsRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public ResetStudentAffairsSettingsCommandHandler(
        IStudentAffairsSettingsRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<SchoolStudentAffairsSettingsDto>> Handle(
        ResetStudentAffairsSettingsCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (!_currentUser.IsAuthenticated || schoolId is null || string.IsNullOrWhiteSpace(userId))
        {
            return ApiResponse<SchoolStudentAffairsSettingsDto>.Fail(SettingsHandlerSupport.AuthenticationRequired);
        }

        if (!_currentUser.HasPermission(PermissionNames.StudentAffairsSettingsManage))
        {
            return ApiResponse<SchoolStudentAffairsSettingsDto>.Fail(SettingsHandlerSupport.PermissionDenied);
        }

        var req = command.Request;
        if (string.IsNullOrWhiteSpace(req.Reason))
        {
            return ApiResponse<SchoolStudentAffairsSettingsDto>.Fail("سبب الاستعادة مطلوب.");
        }

        var settings = await _repository
            .GetSettingsForUpdateAsync(schoolId.Value, cancellationToken)
            .ConfigureAwait(false);

        var now = _timeProvider.GetUtcNow();
        var defaultBaseline = SettingsHandlerSupport.CreateDefaultBaseline(now);

        if (settings is not null)
        {
            if (!SettingsHandlerSupport.TryDecodeExpectedRowVersion(
                    req.RowVersion,
                    settings.RowVersion,
                    out var expectedRowVersion))
            {
                return ApiResponse<SchoolStudentAffairsSettingsDto>.Fail(SettingsHandlerSupport.ConcurrencyConflict);
            }

            _repository.SetExpectedRowVersion(settings, expectedRowVersion);

            var oldSnapshot = new
            {
                settings.MorningDelayThresholdPerTerm,
                settings.BehaviorIncidentMultiplePerTerm,
                settings.AcademicConcernThresholdPerTerm,
                settings.ClassroomEntryPermitThresholdPerTerm,
                settings.AbsenceVisualAlertThresholdPerTerm,
                settings.AbsenceReferralThresholdPerTerm,
                settings.AbsenceChildRightsThresholdPerTerm,
                settings.BehaviorCountabilityPolicy,
                settings.ArrivalCutoffLocalTime,
                settings.ArrivalGraceMinutes,
                settings.Version
            };

            settings.IsDeleted = true;
            settings.DeletedAt = now;
            settings.DeletedByUserId = userId;
            settings.UpdatedAt = now;
            settings.UpdatedByUserId = userId;

            _repository.WriteAudit(
                schoolId.Value,
                userId,
                "StudentAffairs.Settings.Reset",
                settings.Id.ToString(),
                req.Reason.Trim(),
                oldSnapshot,
                defaultBaseline);

            try
            {
                await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                return ApiResponse<SchoolStudentAffairsSettingsDto>.Fail(SettingsHandlerSupport.ConcurrencyConflict);
            }
        }

        return ApiResponse<SchoolStudentAffairsSettingsDto>.Success(defaultBaseline, "تمت استعادة الإعدادات الافتراضية المقفلة.");
    }
}
