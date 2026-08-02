using AlFalah.Application.Common.Exceptions;
using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Application.Interfaces;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Services;

/// <summary>
/// Maps the signed-in local user onto the teacher whose evidence folder they may use.
///
/// This replaces the old "link your Microsoft account" step outright. There is no second
/// identity to reconcile any more: a teacher is simply the <c>InstructorProfile</c> attached
/// to the authenticated user, so nobody can be tricked into operating as another teacher by
/// controlling an external mailbox.
/// </summary>
public sealed class TeacherDriveIdentityService : ITeacherDriveIdentityService
{
    private readonly AlFalahDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public TeacherDriveIdentityService(AlFalahDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<TeacherDriveIdentity> ResolveCurrentTeacherAsync(CancellationToken cancellationToken = default)
    {
        var teacher = await FindCurrentTeacherAsync(cancellationToken)
            ?? throw new TeacherDriveAccessDeniedException("ملفات الإنجاز متاحة لحساب المعلم فقط.");

        var schoolDriveEnabled = await _context.SchoolGoogleDrives.AsNoTracking()
            .AnyAsync(x => x.SchoolId == teacher.SchoolId && x.IsEnabled, cancellationToken);
        if (!schoolDriveEnabled)
            throw new TeacherDriveAccessDeniedException("لم تُفعّل مدرستك ملفات الإنجاز على Google Drive بعد. يرجى التواصل مع مدير المدرسة.");

        return new(teacher.TeacherId, teacher.SchoolId, teacher.TeacherName);
    }

    public async Task<TeacherDriveStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var teacher = await FindCurrentTeacherAsync(cancellationToken);
        if (teacher is null)
            return new(false, false, null, TeacherDriveConnectionState.NotATeacher, string.Empty);

        var schoolDriveEnabled = await _context.SchoolGoogleDrives.AsNoTracking()
            .AnyAsync(x => x.SchoolId == teacher.SchoolId && x.IsEnabled, cancellationToken);
        if (!schoolDriveEnabled)
            return new(false, false, null, TeacherDriveConnectionState.SchoolNotConfigured, teacher.TeacherName);

        var folderName = await _context.TeacherDriveFolders.AsNoTracking()
            .Where(x => x.TeacherId == teacher.TeacherId && x.IsActive)
            .Select(x => x.FolderDisplayName)
            .SingleOrDefaultAsync(cancellationToken);

        return new(
            true,
            folderName is not null,
            folderName,
            folderName is null ? TeacherDriveConnectionState.FolderNotAssigned : TeacherDriveConnectionState.Connected,
            teacher.TeacherName);
    }

    private async Task<CurrentTeacher?> FindCurrentTeacherAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrWhiteSpace(userId)) return null;

        var teacher = await _context.InstructorProfiles.AsNoTracking()
            .Where(x => x.UserId == userId && x.IsActive && !x.IsDeleted)
            .Select(x => new CurrentTeacher(x.Id, x.SchoolId, x.User.FirstName + " " + x.User.LastName))
            .SingleOrDefaultAsync(cancellationToken);
        if (teacher is null) return null;

        // A school-scoped session is pinned to one school. If the token's active school and
        // the profile's school disagree, the session is stale (or being replayed after a
        // transfer) and must not reach any school's evidence folders.
        if (_currentUser.IsSchoolScopedRole()
            && _currentUser.ActiveSchoolId.HasValue
            && _currentUser.ActiveSchoolId.Value != teacher.SchoolId)
            return null;

        return teacher with { TeacherName = teacher.TeacherName.Trim() };
    }

    private sealed record CurrentTeacher(int TeacherId, int SchoolId, string TeacherName);
}
