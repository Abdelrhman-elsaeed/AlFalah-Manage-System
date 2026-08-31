using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Attendance;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Domain.Events;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Attendance.Handlers;

public sealed class SubmitAbsenceExcuseCommandHandler
    : IRequestHandler<SubmitAbsenceExcuseCommand, ApiResponse<AbsenceExcuseDto>>
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;
    private readonly IAttendanceWorkflowRepository _repository;
    private readonly IFileStorageService _fileStorage;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public SubmitAbsenceExcuseCommandHandler(
        IAttendanceWorkflowRepository repository,
        IFileStorageService fileStorage,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _fileStorage = fileStorage;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<AbsenceExcuseDto>> Handle(
        SubmitAbsenceExcuseCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<AbsenceExcuseDto>.Fail(AttendanceHandlerSupport.AuthenticationRequired);
        if (!_currentUser.IsInRole(RoleNames.Guardian)
            || !_currentUser.HasPermission(PermissionNames.AttendanceSubmitExcuse))
            return ApiResponse<AbsenceExcuseDto>.Fail(AttendanceHandlerSupport.PermissionDenied);

        var idempotencyKey = command.IdempotencyKey?.Trim();
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200)
            return ApiResponse<AbsenceExcuseDto>.Fail("A valid Idempotency-Key is required");
        if (command.SizeBytes <= 0 || command.SizeBytes > MaxFileSizeBytes)
            return ApiResponse<AbsenceExcuseDto>.Fail("Excuse attachment size is invalid");
        if (!string.Equals(command.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetExtension(command.OriginalFileName), ".pdf", StringComparison.OrdinalIgnoreCase))
            return ApiResponse<AbsenceExcuseDto>.Fail("Excuse attachment must be a PDF");

        var attendance = await _repository.GetAttendanceForUpdateAsync(
            schoolId.Value,
            command.AttendanceId,
            cancellationToken).ConfigureAwait(false);
        if (attendance is null)
            return ApiResponse<AbsenceExcuseDto>.Fail("Attendance record was not found");
        if (attendance.Status != StudentAttendanceStatus.Absent)
            return ApiResponse<AbsenceExcuseDto>.Fail("An excuse can only be submitted for an absent attendance record");

        var link = await _repository.GetGuardianExcuseLinkAsync(
            schoolId.Value,
            userId,
            attendance.StudentId,
            attendance.AttendanceDate,
            cancellationToken).ConfigureAwait(false);
        if (link is null
            || !link.GuardianIsActive
            || !link.StudentIsActive
            || !link.CanSubmitExcuses
            || link.ValidFrom > attendance.AttendanceDate
            || (link.ValidTo is not null && link.ValidTo < attendance.AttendanceDate))
            return ApiResponse<AbsenceExcuseDto>.Fail("Student is not linked to this guardian for excuse submission");

        var existing = await _repository.GetExcuseByIdempotencyKeyAsync(
            schoolId.Value,
            link.GuardianProfileId,
            idempotencyKey,
            cancellationToken).ConfigureAwait(false);
        if (existing is not null)
            return ApiResponse<AbsenceExcuseDto>.Success(existing, "Absence excuse already exists");

        var storedFile = await _fileStorage.StoreAsync(
            schoolId.Value,
            command.Content,
            command.OriginalFileName,
            command.ContentType,
            cancellationToken).ConfigureAwait(false);
        if (storedFile.SizeBytes != command.SizeBytes)
        {
            await _fileStorage.DeleteIfExistsAsync(storedFile.StorageKey, cancellationToken)
                .ConfigureAwait(false);
            return ApiResponse<AbsenceExcuseDto>.Fail("Excuse attachment size did not match the upload");
        }

        var now = _timeProvider.GetUtcNow();
        var excuse = new AbsenceExcuse
        {
            SchoolId = schoolId.Value,
            DailyStudentAttendanceId = attendance.Id,
            GuardianProfileId = link.GuardianProfileId,
            IdempotencyKey = idempotencyKey,
            ExcuseType = command.Request.ExcuseType,
            GuardianNotes = command.Request.Notes?.Trim(),
            Status = AbsenceExcuseStatus.Pending,
            SubmittedAt = now,
            CreatedAt = now,
            CreatedByUserId = userId,
            UpdatedAt = now,
            UpdatedByUserId = userId
        };
        excuse.Attachments.Add(new AbsenceExcuseAttachment
        {
            SchoolId = schoolId.Value,
            OriginalFileName = Path.GetFileName(command.OriginalFileName),
            ContentType = command.ContentType,
            SizeBytes = storedFile.SizeBytes,
            Sha256 = storedFile.Sha256,
            StorageProvider = storedFile.StorageProvider,
            StorageKey = storedFile.StorageKey,
            UploadedByUserId = userId,
            UploadedAt = now,
            CreatedAt = now,
            CreatedByUserId = userId,
            UpdatedAt = now,
            UpdatedByUserId = userId
        });
        excuse.AppendDomainEvent(new AcbXX3KgvqD7B8Y4WjCu6yNx1Prfu5cNHz(
            Guid.NewGuid(),
            excuse.Id,
            attendance.Id,
            attendance.StudentId,
            schoolId.Value,
            attendance.AcademicTermId,
            link.GuardianProfileId,
            excuse.ExcuseType,
            now,
            now));
        attendance.ExcuseStatus = AbsenceExcuseStatus.Pending;
        attendance.UpdatedAt = now;
        attendance.UpdatedByUserId = userId;
        _repository.AddExcuse(excuse);

        try
        {
            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (AttendancePersistenceConflictException)
        {
            await _fileStorage.DeleteIfExistsAsync(storedFile.StorageKey, cancellationToken)
                .ConfigureAwait(false);
            var duplicate = await _repository.GetExcuseByIdempotencyKeyAsync(
                schoolId.Value,
                link.GuardianProfileId,
                idempotencyKey,
                cancellationToken).ConfigureAwait(false);
            if (duplicate is not null)
                return ApiResponse<AbsenceExcuseDto>.Success(duplicate, "Absence excuse already exists");
            throw;
        }
        catch (AttendanceConcurrencyException)
        {
            await _fileStorage.DeleteIfExistsAsync(storedFile.StorageKey, cancellationToken)
                .ConfigureAwait(false);
            return ApiResponse<AbsenceExcuseDto>.Fail(
                "The linked attendance record was modified by another user");
        }
        catch
        {
            await _fileStorage.DeleteIfExistsAsync(storedFile.StorageKey, cancellationToken)
                .ConfigureAwait(false);
            throw;
        }

        var dto = await _repository.GetExcuseDtoAsync(schoolId.Value, excuse.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The submitted absence excuse could not be loaded");
        return ApiResponse<AbsenceExcuseDto>.Success(dto, "Absence excuse submitted successfully");
    }
}
