using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlFalah.Application.Storage;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

internal static class StorageProvenance
{
    internal static string Serialize(LegacyStorageSubmission row) => JsonSerializer.Serialize(row);
    internal static string Hash(string snapshot) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot)));
    internal static Task<LegacyStorageSubmission> ReadAsync(AlFalahDbContext db, long id, CancellationToken ct) =>
        db.TeacherEvidenceSubmissions.AsNoTracking().Where(x => x.Id == id).Select(x => new LegacyStorageSubmission(x.Id, x.SchoolId, x.TeacherId, x.TaskId, x.AcademicYearId,
            x.DriveId, x.DriveItemId, x.ParentItemId, x.FileName, x.FileExtension, x.MimeType, x.SizeInBytes, x.ETag,
            x.UploadStatus, x.ReviewStatus, x.ReviewedAtUtc, x.ReviewedByUserId, x.ReviewNote, x.IsDeleted, x.DeletedAtUtc, x.DeletedByUserId,
            x.IsMissingFromDrive, x.MissingFromDriveAtUtc, x.UploadedAtUtc, x.CreatedAtUtc, x.UpdatedAtUtc)).SingleAsync(ct);
}
