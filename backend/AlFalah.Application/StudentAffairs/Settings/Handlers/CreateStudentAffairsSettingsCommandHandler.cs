using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Settings;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Settings.Handlers;

public sealed class CreateStudentAffairsSettingsCommandHandler
    : IRequestHandler<CreateStudentAffairsSettingsCommand, ApiResponse<SchoolStudentAffairsSettingsDto>>
{
    private readonly IStudentAffairsSettingsRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public CreateStudentAffairsSettingsCommandHandler(
        IStudentAffairsSettingsRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<SchoolStudentAffairsSettingsDto>> Handle(
        CreateStudentAffairsSettingsCommand command,
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

        var existing = await _repository
            .GetSettingsAsync(schoolId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return ApiResponse<SchoolStudentAffairsSettingsDto>.Fail("توجد إعدادات مخصصة بالفعل لهذه المدرسة. يرجى التعديل بدلاً من الإنشاء.");
        }

        var now = _timeProvider.GetUtcNow();
        var entity = new SchoolStudentAffairsSettings
        {
            SchoolId = schoolId.Value,
            MorningDelayThresholdPerTerm = req.MorningDelayThresholdPerTerm,
            BehaviorIncidentMultiplePerTerm = req.BehaviorIncidentMultiplePerTerm,
            AcademicConcernThresholdPerTerm = req.AcademicConcernThresholdPerTerm,
            ClassroomEntryPermitThresholdPerTerm = req.ClassroomEntryPermitThresholdPerTerm,
            AbsenceVisualAlertThresholdPerTerm = req.AbsenceVisualAlertThresholdPerTerm,
            AbsenceReferralThresholdPerTerm = req.AbsenceReferralThresholdPerTerm,
            AbsenceChildRightsThresholdPerTerm = req.AbsenceChildRightsThresholdPerTerm,
            BehaviorCountabilityPolicy = req.BehaviorCountabilityPolicy.Trim(),
            ArrivalCutoffLocalTime = req.ArrivalCutoffLocalTime,
            ArrivalGraceMinutes = req.ArrivalGraceMinutes,
            Version = 1,
            EffectiveFrom = now,
            CreatedAt = now,
            CreatedByUserId = userId,
            UpdatedAt = now,
            UpdatedByUserId = userId
        };

        _repository.AddSettings(entity);
        _repository.WriteAudit(
            schoolId.Value,
            userId,
            "StudentAffairs.Settings.Created",
            null,
            "Initial school student affairs settings customization",
            null,
            entity);

        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var dto = await _repository
            .GetSettingsDtoAsync(schoolId.Value, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The saved student affairs settings could not be loaded");

        return ApiResponse<SchoolStudentAffairsSettingsDto>.Success(dto, "تم حفظ إعدادات شؤون الطلاب بنجاح.");
    }
}
