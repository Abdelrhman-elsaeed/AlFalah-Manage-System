using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Application.Storage;

namespace AlFalah.Infrastructure.Services;

public sealed class StorageDriveBoundary(TeacherDriveFolderGuard guard) : IStorageDriveBoundary
{
    public async Task EnsureWithinAsync(int schoolId, StorageDriveRoot root, string itemId, CancellationToken ct) =>
        await guard.EnsureWithinGrantAsync(new DriveFolderMappingDto(0, schoolId, root.DriveId, root.RootItemId, "", null, true), itemId, ct);
}
