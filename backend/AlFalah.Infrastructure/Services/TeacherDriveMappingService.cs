using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Services;

/// <summary>
/// Where the manager's "this person may use that folder" decision is recorded.
///
/// A grant is validated against Google itself before it is stored: the folder must exist, be
/// a folder, and live inside the school's own evidence root. That closes the hole where a
/// mistyped or pasted-from-elsewhere folder id would otherwise hand a teacher a window onto
/// unrelated files that the school credential happens to be able to read.
/// </summary>
public sealed class TeacherDriveMappingService : ITeacherDriveMappingService
{
    private readonly AlFalahDbContext _context;
    private readonly SchoolScopeGuard _scopeGuard;
    private readonly IGoogleDriveClient _drive;
    private readonly TeacherDriveFolderGuard _guard;
    private readonly AuditLogWriter _audit;
    private readonly ICurrentUserService _currentUser;

    public TeacherDriveMappingService(
        AlFalahDbContext context,
        SchoolScopeGuard scopeGuard,
        IGoogleDriveClient drive,
        TeacherDriveFolderGuard guard,
        AuditLogWriter audit,
        ICurrentUserService currentUser)
    {
        _context = context;
        _scopeGuard = scopeGuard;
        _drive = drive;
        _guard = guard;
        _audit = audit;
        _currentUser = currentUser;
    }

    public async Task<DriveFolderMappingDto> GetForTeacherAsync(int teacherId, CancellationToken cancellationToken = default) =>
        await FindForTeacherAsync(teacherId, cancellationToken)
        ?? throw new InvalidOperationException("لم يتم منح هذا المعلم مجلداً على Google Drive بعد.");

    public async Task<DriveFolderMappingDto?> FindForTeacherAsync(int teacherId, CancellationToken cancellationToken = default)
    {
        // Resolve the teacher's school and scope-check it BEFORE returning anything, so a
        // manager cannot enumerate teacherId values to read another school's DriveId/RootItemId
        // (a real folder identifier, not just a display name) through this admin lookup.
        var teacher = await _context.InstructorProfiles.AsNoTracking()
            .Where(x => x.Id == teacherId && !x.IsDeleted)
            .Select(x => new { x.SchoolId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("المعلم غير موجود.");
        await _scopeGuard.EnsureCanMutateSchoolAsync(teacher.SchoolId, cancellationToken);

        return await _context.TeacherDriveFolders.AsNoTracking()
            .Where(x => x.TeacherId == teacherId && x.IsActive)
            .Select(x => new DriveFolderMappingDto(x.TeacherId, x.SchoolId, x.DriveId, x.RootItemId, x.FolderDisplayName, x.RootWebUrl, x.IsActive))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<AdminDriveFolderPageDto> BrowseFoldersAsync(
        int teacherId, BrowseAdminDriveFoldersRequest request, CancellationToken cancellationToken = default)
    {
        var teacher = await _context.InstructorProfiles.AsNoTracking()
            .Where(x => x.Id == teacherId && !x.IsDeleted)
            .Select(x => new { x.SchoolId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("المعلم غير موجود.");
        await _scopeGuard.EnsureCanMutateSchoolAsync(teacher.SchoolId, cancellationToken);

        var schoolDrive = await _context.SchoolGoogleDrives.AsNoTracking()
            .Where(x => x.SchoolId == teacher.SchoolId && x.IsEnabled)
            .Select(x => new { x.RootFolderId, x.RootFolderDisplayName, x.SharedDriveId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("يجب ربط حساب Google Drive الخاص بالمدرسة أولاً.");

        var parentId = string.IsNullOrWhiteSpace(request.ParentItemId)
            ? schoolDrive.RootFolderId
            : request.ParentItemId.Trim();
        var isSchoolRoot = string.Equals(parentId, schoolDrive.RootFolderId, StringComparison.Ordinal);
        var parentName = schoolDrive.RootFolderDisplayName;

        if (!isSchoolRoot)
        {
            var parent = await _drive.GetFileAsync(teacher.SchoolId, parentId, cancellationToken)
                ?? throw new InvalidOperationException("المجلد غير موجود على Google Drive.");
            if (parent.Trashed || !parent.IsFolder
                || !await _guard.IsWithinAsync(teacher.SchoolId, schoolDrive.RootFolderId, parentId, cancellationToken))
                throw new InvalidOperationException("لا يمكن استعراض مجلد خارج المجلد الرئيسي للمدرسة.");
            parentName = parent.Name;
        }

        var drivePage = await _drive.ListChildrenAsync(teacher.SchoolId, new(
            parentId,
            NameContains: null,
            OrderBy: "folder,name",
            PageSize: 100,
            PageToken: string.IsNullOrWhiteSpace(request.PageToken) ? null : request.PageToken,
            SharedDriveId: schoolDrive.SharedDriveId), cancellationToken);
        var folders = drivePage.Files.Where(x => x.IsFolder && !x.Trashed).ToList();
        var folderIds = folders.Select(x => x.Id).ToList();

        var assignments = await _context.TeacherDriveFolders.AsNoTracking()
            .Where(x => x.SchoolId == teacher.SchoolId && x.IsActive && folderIds.Contains(x.RootItemId))
            .Select(x => new
            {
                x.RootItemId,
                x.TeacherId,
                TeacherName = (x.Teacher.User.FirstName + " " + x.Teacher.User.LastName).Trim()
            })
            .ToListAsync(cancellationToken);
        var assignmentsByFolder = assignments.ToDictionary(x => x.RootItemId, StringComparer.Ordinal);

        var items = folders.Select(folder =>
        {
            assignmentsByFolder.TryGetValue(folder.Id, out var assignment);
            return new AdminDriveFolderItemDto(
                folder.Id,
                folder.Name,
                assignment is not null,
                assignment?.TeacherId == teacherId,
                assignment?.TeacherName);
        }).ToList();

        return new(parentId, parentName, isSchoolRoot, items, drivePage.NextPageToken);
    }

    public async Task<DriveFolderMappingDto> UpsertAsync(
        int teacherId, UpsertDriveFolderMappingRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RootItemId))
            throw new ArgumentException("معرّف مجلد Google Drive مطلوب.");

        var teacher = await _context.InstructorProfiles.SingleOrDefaultAsync(x => x.Id == teacherId && !x.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("المعلم غير موجود.");
        await _scopeGuard.EnsureCanMutateSchoolAsync(teacher.SchoolId, cancellationToken);

        var schoolDrive = await _context.SchoolGoogleDrives.AsNoTracking()
            .SingleOrDefaultAsync(x => x.SchoolId == teacher.SchoolId && x.IsEnabled, cancellationToken)
            ?? throw new InvalidOperationException("يجب ربط حساب Google Drive الخاص بالمدرسة قبل منح مجلدات للمعلمين.");

        var folderId = request.RootItemId.Trim();
        // Returns the validated folder so its real name and link can be stored — the manager
        // supplies only the id, and a renamed folder is re-read on the next grant.
        var folder = await EnsureFolderIsInsideSchoolRootAsync(
            teacher.SchoolId, schoolDrive.RootFolderId, folderId, cancellationToken);

        // Equal or nested grants would let one teacher reach another teacher's evidence.
        var otherActiveGrantRoots = await _context.TeacherDriveFolders.AsNoTracking()
            .Where(x => x.SchoolId == teacher.SchoolId && x.IsActive && x.TeacherId != teacherId)
            .Select(x => x.RootItemId)
            .ToListAsync(cancellationToken);
        foreach (var otherRoot in otherActiveGrantRoots)
        {
            var overlaps = await _guard.IsWithinAsync(teacher.SchoolId, otherRoot, folderId, cancellationToken)
                || await _guard.IsWithinAsync(teacher.SchoolId, folderId, otherRoot, cancellationToken);
            if (overlaps)
                throw new InvalidOperationException("هذا المجلد أو أحد المجلدات المتداخلة معه ممنوح لمعلم آخر بالفعل. اختر مجلداً مستقلاً لكل معلم.");
        }

        var mapping = await _context.TeacherDriveFolders.SingleOrDefaultAsync(x => x.TeacherId == teacherId, cancellationToken);
        var before = mapping is null ? null : Describe(mapping);
        if (mapping is null)
        {
            mapping = new TeacherDriveFolder { TeacherId = teacherId, SchoolId = teacher.SchoolId };
            _context.TeacherDriveFolders.Add(mapping);
        }

        mapping.SchoolId = teacher.SchoolId;
        // Taken from the school's connection, never from the request: a grant cannot be
        // pointed at a drive the school did not configure.
        mapping.DriveId = schoolDrive.SharedDriveId ?? string.Empty;
        mapping.RootItemId = folderId;
        // Both taken from Drive itself, so the stored name always matches what the manager sees
        // in Google and can never drift from a typo.
        mapping.FolderDisplayName = folder.Name;
        mapping.RootWebUrl = string.IsNullOrWhiteSpace(folder.WebViewLink) ? null : folder.WebViewLink;
        // Upsert is the grant operation, so a valid folder always activates (or reactivates)
        // the mapping. Revocation remains a separate, explicit endpoint.
        mapping.IsActive = true;

        _audit.Write(teacher.SchoolId, _currentUser.UserId, "TeacherDriveFolder.Granted", "TeacherDriveFolder",
            mapping.Id == 0 ? null : mapping.Id.ToString(), null, before, Describe(mapping));
        await _context.SaveChangesAsync(cancellationToken);
        return new(mapping.TeacherId, mapping.SchoolId, mapping.DriveId, mapping.RootItemId, mapping.FolderDisplayName, mapping.RootWebUrl, mapping.IsActive);
    }

    public async Task RevokeAsync(int teacherId, CancellationToken cancellationToken = default)
    {
        var mapping = await _context.TeacherDriveFolders.SingleOrDefaultAsync(x => x.TeacherId == teacherId, cancellationToken)
            ?? throw new KeyNotFoundException("لا يوجد مجلد ممنوح لهذا المعلم.");
        await _scopeGuard.EnsureCanMutateSchoolAsync(mapping.SchoolId, cancellationToken);
        if (!mapping.IsActive) return;

        // Deactivate rather than delete: already-uploaded evidence must stay auditable, and
        // the matrix keeps showing what the teacher submitted while access is withdrawn.
        mapping.IsActive = false;
        _audit.Write(mapping.SchoolId, _currentUser.UserId, "TeacherDriveFolder.Revoked", "TeacherDriveFolder",
            mapping.Id.ToString(), null, new { teacherId, mapping.RootItemId });
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Validates the folder and returns it, so the caller can store its real name and link.</summary>
    private async Task<GoogleDriveFile> EnsureFolderIsInsideSchoolRootAsync(
        int schoolId, string schoolRootFolderId, string folderId, CancellationToken cancellationToken)
    {
        var folder = await _drive.GetFileAsync(schoolId, folderId, cancellationToken)
            ?? throw new InvalidOperationException("المجلد غير موجود على Google Drive أو لا يملك حساب المدرسة صلاحية عليه.");
        if (folder.Trashed)
            throw new InvalidOperationException("المجلد المحدد محذوف على Google Drive.");
        if (!folder.IsFolder)
            throw new InvalidOperationException("المعرّف المحدد يشير إلى ملف وليس مجلداً.");

        // The school root itself is not a valid grant: it would expose every teacher's folder
        // to whoever received it.
        if (string.Equals(folderId, schoolRootFolderId, StringComparison.Ordinal))
            throw new InvalidOperationException("لا يمكن منح المجلد الرئيسي للمدرسة لمعلم. أنشئ مجلداً فرعياً خاصاً به.");

        if (!await _guard.IsWithinAsync(schoolId, schoolRootFolderId, folderId, cancellationToken))
            throw new InvalidOperationException("يجب أن يكون مجلد المعلم داخل المجلد الرئيسي لملفات إنجاز المدرسة.");

        return folder;
    }

    private static object Describe(TeacherDriveFolder mapping) => new
    {
        mapping.TeacherId,
        mapping.SchoolId,
        mapping.DriveId,
        mapping.RootItemId,
        mapping.FolderDisplayName,
        mapping.IsActive
    };
}
