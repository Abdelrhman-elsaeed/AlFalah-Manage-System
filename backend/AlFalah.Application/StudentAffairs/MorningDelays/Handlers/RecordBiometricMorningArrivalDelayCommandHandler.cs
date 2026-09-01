using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Delays;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Events;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.MorningDelays.Handlers;

public sealed class RecordBiometricMorningArrivalDelayCommandHandler
    : IRequestHandler<RecordBiometricMorningArrivalDelayCommand, ApiResponse<MorningDelayDto>>
{
    private const string NotificationPolicy = "ImmediateGuardian";
    private readonly IMorningDelayWorkflowRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public RecordBiometricMorningArrivalDelayCommandHandler(
        IMorningDelayWorkflowRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<MorningDelayDto>> Handle(
        RecordBiometricMorningArrivalDelayCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<MorningDelayDto>.Fail("An authenticated system actor and active school are required");
        if (command.StudentId <= 0 || command.DelayMinutes <= 0)
            return ApiResponse<MorningDelayDto>.Fail("Student and a positive delay duration are required");

        var existing = await _repository.GetExistingAsync(
            schoolId.Value,
            command.StudentId,
            command.SchoolLocalDate,
            cancellationToken).ConfigureAwait(false);
        if (existing is not null)
            return ApiResponse<MorningDelayDto>.Success(existing, "Morning arrival delay already exists");

        var enrollment = await _repository.GetActiveEnrollmentAsync(
            schoolId.Value,
            command.StudentId,
            command.SchoolLocalDate,
            cancellationToken).ConfigureAwait(false);
        if (enrollment is null)
            return ApiResponse<MorningDelayDto>.Fail("Student does not have an active enrollment");

        var now = _timeProvider.GetUtcNow();
        var delay = new MorningArrivalDelay
        {
            SchoolId = schoolId.Value,
            StudentId = command.StudentId,
            AcademicTermId = enrollment.AcademicTermId,
            ArrivalAt = command.ArrivalAt,
            SchoolLocalDate = command.SchoolLocalDate,
            CutoffTimeSnapshot = command.CutoffTimeSnapshot,
            DelayMinutes = command.DelayMinutes,
            Reason = command.Reason?.Trim(),
            NotificationPolicySnapshot = NotificationPolicy,
            CreatedAt = now,
            CreatedByUserId = userId,
            UpdatedAt = now,
            UpdatedByUserId = userId
        };
        delay.AppendDomainEvent(new MUaCqczw28YRmuXBYNYtWgMhWwXe7qmYC3(
            Guid.NewGuid(),
            delay.Id,
            delay.StudentId,
            delay.SchoolId,
            delay.AcademicTermId,
            delay.ArrivalAt,
            delay.SchoolLocalDate,
            delay.CutoffTimeSnapshot,
            delay.DelayMinutes,
            delay.NotificationPolicySnapshot,
            now));

        _repository.Add(delay);
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var dto = await _repository.GetDtoAsync(schoolId.Value, delay.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The saved morning arrival delay could not be loaded");
        return ApiResponse<MorningDelayDto>.Success(dto, "Morning arrival delay recorded successfully");
    }
}
