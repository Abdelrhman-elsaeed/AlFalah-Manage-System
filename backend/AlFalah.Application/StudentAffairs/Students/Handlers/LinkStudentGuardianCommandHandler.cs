using System;
using System.Threading;
using System.Threading.Tasks;
using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.DTOs.Students;
using AlFalah.Application.StudentAffairs.Guardians;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Students.Handlers;

public sealed class LinkStudentGuardianCommandHandler
    : IRequestHandler<LinkStudentGuardianCommand, ApiResponse<StudentGuardianLinkDto>>
{
    private readonly IStudentWorkflowRepository _repository;
    private readonly IGuardianDirectoryRepository _guardianDirectory;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public LinkStudentGuardianCommandHandler(
        IStudentWorkflowRepository repository,
        IGuardianDirectoryRepository guardianDirectory,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _guardianDirectory = guardianDirectory;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<StudentGuardianLinkDto>> Handle(
        LinkStudentGuardianCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<StudentGuardianLinkDto>.Fail(StudentHandlerSupport.AuthenticationRequired);

        if (!_currentUser.HasPermission(PermissionNames.GuardianLinkStudent)
            && !(_currentUser.IsInRole(RoleNames.Secretary)
                && _currentUser.HasPermission(PermissionNames.StudentManage))
            && !_currentUser.IsInRole(RoleNames.StudentAffairsOfficer)
            && !_currentUser.IsInRole(RoleNames.MainManager)
            && !_currentUser.IsInRole(RoleNames.SchoolManager))
        {
            return ApiResponse<StudentGuardianLinkDto>.Fail(StudentHandlerSupport.PermissionDenied);
        }

        var student = await _repository.GetStudentForUpdateAsync(
            schoolId.Value,
            command.StudentId,
            cancellationToken).ConfigureAwait(false);

        if (student is null)
            return ApiResponse<StudentGuardianLinkDto>.Fail(StudentHandlerSupport.StudentNotFound);

        var req = command.Request;
        var now = _timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.DateTime);

        if (req.GuardianProfileId <= 0
            || (req.ValidTo.HasValue && req.ValidTo.Value < req.ValidFrom)
            || !await _guardianDirectory.IsActiveAsync(
                schoolId.Value,
                req.GuardianProfileId,
                cancellationToken).ConfigureAwait(false))
        {
            return ApiResponse<StudentGuardianLinkDto>.Fail("ولي الأمر المحدد غير متاح في المدرسة الحالية");
        }

        var existingLinks = await _repository.GetStudentGuardiansAsync(
            schoolId.Value,
            command.StudentId,
            today,
            cancellationToken).ConfigureAwait(false);

        if (existingLinks.Any(item => item.Guardian.Id == req.GuardianProfileId))
            return ApiResponse<StudentGuardianLinkDto>.Fail("ولي الأمر مرتبط بالفعل بهذا الطالب");

        if (req.IsPrimary && existingLinks.Any(item => item.IsActive && item.Guardian.IsPrimary))
            return ApiResponse<StudentGuardianLinkDto>.Fail("يوجد ولي أمر أساسي نشط بالفعل لهذا الطالب");

        var link = new StudentGuardian
        {
            SchoolId = schoolId.Value,
            StudentId = command.StudentId,
            GuardianProfileId = req.GuardianProfileId,
            RelationshipType = req.Relationship,
            IsPrimary = req.IsPrimary,
            ReceivesNotifications = req.ReceivesNotifications,
            CanSubmitExcuses = req.CanSubmitExcuses,
            CanRequestGatePass = req.CanRequestGatePass,
            ValidFrom = req.ValidFrom,
            ValidTo = req.ValidTo,
            CreatedAt = now,
            CreatedByUserId = userId,
            UpdatedAt = now,
            UpdatedByUserId = userId
        };

        _repository.AddGuardianLink(link);
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var dto = await _repository.GetGuardianLinkDtoAsync(schoolId.Value, link.Id, today, cancellationToken).ConfigureAwait(false);
        return ApiResponse<StudentGuardianLinkDto>.Success(dto!, "Guardian linked successfully");
    }
}
