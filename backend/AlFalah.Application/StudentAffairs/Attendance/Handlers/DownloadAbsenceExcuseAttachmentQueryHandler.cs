using AlFalah.Application.Interfaces;
using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.StudentAffairs.DTOs.Attendance;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Domain.Enums;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Attendance.Handlers;

public sealed class DownloadAbsenceExcuseAttachmentQueryHandler
    : IRequestHandler<DownloadAbsenceExcuseAttachmentQuery, AuthorizedFileDto>
{
    private readonly IAttendanceWorkflowRepository _repository;
    private readonly IFileStorageService _fileStorage;
    private readonly ICurrentUserService _currentUser;
    private readonly ISchoolLocalDateResolver _localDateResolver;
    private readonly TimeProvider _timeProvider;

    public DownloadAbsenceExcuseAttachmentQueryHandler(
        IAttendanceWorkflowRepository repository,
        IFileStorageService fileStorage,
        ICurrentUserService currentUser,
        ISchoolLocalDateResolver localDateResolver,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _fileStorage = fileStorage;
        _currentUser = currentUser;
        _localDateResolver = localDateResolver;
        _timeProvider = timeProvider;
    }

    public async Task<AuthorizedFileDto> Handle(
        DownloadAbsenceExcuseAttachmentQuery request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException(AttendanceHandlerSupport.AuthenticationRequired);

        var isGuardian = _currentUser.IsInRole(RoleNames.Guardian);
        if (isGuardian
            ? !_currentUser.HasPermission(PermissionNames.AttendanceSubmitExcuse)
            : !_currentUser.HasPermission(PermissionNames.AttendanceViewStudents))
            throw new UnauthorizedAccessException(AttendanceHandlerSupport.PermissionDenied);

        var result = await _repository.GetExcuseAttachmentAsync(
            schoolId.Value,
            request.ExcuseId,
            request.AttachmentId,
            cancellationToken).ConfigureAwait(false);

        if (result is null)
            throw new KeyNotFoundException("Attachment was not found");

        var (attachment, excuse) = result.Value;
        if (isGuardian)
        {
            var localDate = await _localDateResolver.ResolveAsync(
                schoolId.Value,
                _timeProvider.GetUtcNow(),
                cancellationToken).ConfigureAwait(false)
                ?? throw new UnauthorizedAccessException("School local date could not be resolved");
            var link = await _repository.GetGuardianExcuseLinkAsync(
                schoolId.Value,
                userId,
                excuse.DailyStudentAttendance.StudentId,
                localDate,
                cancellationToken).ConfigureAwait(false);
            if (link is null
                || !link.GuardianIsActive
                || !link.StudentIsActive
                || link.GuardianProfileId != excuse.GuardianProfileId)
                throw new KeyNotFoundException("Attachment was not found");
        }

        var bytes = await _fileStorage.ReadBytesAsync(attachment.StorageKey, cancellationToken)
            .ConfigureAwait(false);

        if (bytes is null)
            throw new FileNotFoundException("Attachment file could not be found on storage");

        return new AuthorizedFileDto(bytes, attachment.ContentType, attachment.OriginalFileName);
    }
}
