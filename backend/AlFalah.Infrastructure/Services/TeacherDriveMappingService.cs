using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Services;

public sealed class TeacherDriveMappingService : ITeacherDriveMappingService
{
    private readonly AlFalahDbContext _context;
    private readonly SchoolScopeGuard _scopeGuard;
    public TeacherDriveMappingService(AlFalahDbContext context, SchoolScopeGuard scopeGuard) { _context = context; _scopeGuard = scopeGuard; }

    public async Task<DriveFolderMappingDto> GetForTeacherAsync(int teacherId, CancellationToken cancellationToken = default)
    {
        var result = await _context.TeacherDriveFolders.AsNoTracking()
            .Where(x => x.TeacherId == teacherId && x.IsActive)
            .Select(x => new DriveFolderMappingDto(x.TeacherId, x.SchoolId, x.DriveId, x.RootItemId, x.FolderDisplayName, x.RootWebUrl, x.IsActive))
            .SingleOrDefaultAsync(cancellationToken);
        return result ?? throw new InvalidOperationException("لم يتم إعداد مجلد OneDrive لهذا المعلم.");
    }

    public async Task<DriveFolderMappingDto> UpsertAsync(int teacherId, UpsertDriveFolderMappingRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.DriveId) || string.IsNullOrWhiteSpace(request.RootItemId) || string.IsNullOrWhiteSpace(request.FolderDisplayName))
            throw new ArgumentException("بيانات مجلد OneDrive غير مكتملة.");
        var teacher = await _context.InstructorProfiles.SingleOrDefaultAsync(x => x.Id == teacherId && !x.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("المعلم غير موجود.");
        await _scopeGuard.EnsureCanMutateSchoolAsync(teacher.SchoolId, cancellationToken);
        var schoolDrive = await _context.SchoolMicrosoftDrives.AsNoTracking()
            .SingleOrDefaultAsync(x => x.SchoolId == teacher.SchoolId && x.IsEnabled, cancellationToken);
        if (schoolDrive is null)
            throw new InvalidOperationException("يجب إعداد حساب Microsoft الخاص بالمدرسة قبل إعداد مجلدات المدرسين.");
        if (!string.Equals(request.DriveId.Trim(), schoolDrive.DriveId, StringComparison.Ordinal))
            throw new InvalidOperationException("يجب أن يكون مجلد المدرس داخل Drive حساب المدرسة المُعدّ.");
        var mapping = await _context.TeacherDriveFolders.SingleOrDefaultAsync(x => x.TeacherId == teacherId, cancellationToken);
        if (mapping is null)
        {
            mapping = new TeacherDriveFolder { TeacherId = teacherId, SchoolId = teacher.SchoolId };
            _context.TeacherDriveFolders.Add(mapping);
        }
        mapping.DriveId = request.DriveId.Trim();
        mapping.RootItemId = request.RootItemId.Trim();
        mapping.FolderDisplayName = request.FolderDisplayName.Trim();
        mapping.RootWebUrl = string.IsNullOrWhiteSpace(request.RootWebUrl) ? null : request.RootWebUrl.Trim();
        mapping.IsActive = request.IsActive;
        await _context.SaveChangesAsync(cancellationToken);
        return new(mapping.TeacherId, mapping.SchoolId, mapping.DriveId, mapping.RootItemId, mapping.FolderDisplayName, mapping.RootWebUrl, mapping.IsActive);
    }
}
