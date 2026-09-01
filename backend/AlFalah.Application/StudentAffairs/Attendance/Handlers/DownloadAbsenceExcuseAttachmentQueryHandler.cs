using AlFalah.Application.Interfaces;
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

    public DownloadAbsenceExcuseAttachmentQueryHandler(
        IAttendanceWorkflowRepository repository,
        IFileStorageService fileStorage,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _fileStorage = fileStorage;
        _currentUser = currentUser;
    }

    public async Task<AuthorizedFileDto> Handle(
        DownloadAbsenceExcuseAttachmentQuery request,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException(AttendanceHandlerSupport.AuthenticationRequired);

        if (!_currentUser.HasPermission(PermissionNames.AttendanceViewStudents)
            && !_currentUser.HasPermission(PermissionNames.AttendanceSubmitExcuse))
            throw new UnauthorizedAccessException(AttendanceHandlerSupport.PermissionDenied);

        var result = await _repository.GetExcuseAttachmentAsync(
            schoolId.Value,
            request.ExcuseId,
            request.AttachmentId,
            cancellationToken).ConfigureAwait(false);

        if (result is null)
            throw new KeyNotFoundException("Attachment was not found");

        var (attachment, _) = result.Value;

        var bytes = await _fileStorage.ReadBytesAsync(attachment.StorageKey, cancellationToken)
            .ConfigureAwait(false);

        if (bytes is null)
            throw new FileNotFoundException("Attachment file could not be found on storage");

        return new AuthorizedFileDto(bytes, attachment.ContentType, attachment.OriginalFileName);
    }
}
