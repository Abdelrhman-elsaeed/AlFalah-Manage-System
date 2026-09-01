using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Settings;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Settings.Handlers;

public sealed class UpdateStudentAffairsSettingsCommandHandler
    : IRequestHandler<UpdateStudentAffairsSettingsCommand, ApiResponse<SchoolStudentAffairsSettingsDto>>
{
    private readonly IStudentAffairsSettingsRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public UpdateStudentAffairsSettingsCommandHandler(
        IStudentAffairsSettingsRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<SchoolStudentAffairsSettingsDto>> Handle(
        UpdateStudentAffairsSettingsCommand command,
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
        if (string.IsNullOrWhiteSpace(req.AuditReason))
        {
            return ApiResponse<SchoolStudentAffairsSettingsDto>.Fail("سبب التعديل مطلوب.");
        }

        var validationError = SettingsHandlerSupport.ValidateThresholds(
            req.MorningDelayThresholdPerTerm,
            req.BehaviorIncidentMultiplePerTerm,
            req.AcademicConcernThresholdPerTerm,
            req.ClassroomEntryPermitThresholdPerTerm,
            req.AbsenceVisualAlertThresholdPerTerm,
            req.AbsenceReferralThresholdPerTerm,
            req.AbsenceChildRightsThresholdPerTerm,
            req.ArrivalGraceMinutes,
            req.BehaviorCountabilityPolicy);
        if (validationError is not null)
        {
            return ApiResponse<SchoolStudentAffairsSettingsDto>.Fail(validationError);
        }

        var settings = await _repository
            .GetSettingsForUpdateAsync(schoolId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (settings is null)
        {
            return ApiResponse<SchoolStudentAffairsSettingsDto>.Fail(SettingsHandlerSupport.NotFound);
        }

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

        var now = _timeProvider.GetUtcNow();
        settings.MorningDelayThresholdPerTerm = req.MorningDelayThresholdPerTerm;
        settings.BehaviorIncidentMultiplePerTerm = req.BehaviorIncidentMultiplePerTerm;
        settings.AcademicConcernThresholdPerTerm = req.AcademicConcernThresholdPerTerm;
        settings.ClassroomEntryPermitThresholdPerTerm = req.ClassroomEntryPermitThresholdPerTerm;
        settings.AbsenceVisualAlertThresholdPerTerm = req.AbsenceVisualAlertThresholdPerTerm;
        settings.AbsenceReferralThresholdPerTerm = req.AbsenceReferralThresholdPerTerm;
        settings.AbsenceChildRightsThresholdPerTerm = req.AbsenceChildRightsThresholdPerTerm;
        settings.BehaviorCountabilityPolicy = req.BehaviorCountabilityPolicy.Trim();
        settings.ArrivalCutoffLocalTime = req.ArrivalCutoffLocalTime;
        settings.ArrivalGraceMinutes = req.ArrivalGraceMinutes;
        settings.Version += 1;
        settings.EffectiveFrom = now;
        settings.UpdatedAt = now;
        settings.UpdatedByUserId = userId;

        _repository.WriteAudit(
            schoolId.Value,
            userId,
            "StudentAffairs.Settings.Updated",
            settings.Id.ToString(),
            req.AuditReason.Trim(),
            oldSnapshot,
            settings);

        try
        {
            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return ApiResponse<SchoolStudentAffairsSettingsDto>.Fail(SettingsHandlerSupport.ConcurrencyConflict);
        }

        var dto = await _repository
            .GetSettingsDtoAsync(schoolId.Value, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The updated student affairs settings could not be loaded");

        return ApiResponse<SchoolStudentAffairsSettingsDto>.Success(dto, "تم تحديث إعدادات شؤون الطلاب بنجاح.");
    }
}
