using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

// Shared S3/S4 SQL projection of current permitted links. Decisions stay on EvidenceLink.
internal static class EvidenceLinkQueries
{
    internal static IQueryable<EvidenceLink> Eligible(AlFalahDbContext db, int school, int year, int? owner = null) =>
        db.EvidenceLinks.AsNoTracking().Where(x => x.SchoolId == school &&
        x.AcademicYearId == year && x.IsActive && x.Requirement.IsActive && (owner == null || x.TeacherId == owner) &&
        !x.StoredFile.IsDeleted && x.StoredFile.SourceKind != StoredFileSourceKind.VisitArchive &&
        x.StoredFile.SourceKind != StoredFileSourceKind.HistoricalImport &&
        x.VersionId == x.StoredFile.CurrentVersionId &&
        db.SchoolGoogleDrives.Any(s => s.SchoolId == school && s.IsEnabled && (s.SharedDriveId ?? "") == x.Version.DriveId) &&
        (x.StoredFile.OwnerTeacherId == null || db.TeacherDriveFolders.Any(g => g.SchoolId == school && g.TeacherId == x.StoredFile.OwnerTeacherId && g.IsActive) &&
         db.InstructorProfiles.Any(t => t.Id == x.StoredFile.OwnerTeacherId && t.SchoolId == school && t.IsActive && t.User.IsActive &&
             db.UserSchoolRoles.Any(m => m.UserId == t.UserId && m.SchoolId == school && m.IsActive &&
                 db.RolePermissions.Any(p => p.RoleId == m.RoleId && p.Permission.Name == PermissionNames.StorageViewOwn)))) &&
        (x.TeacherId == null || db.InstructorProfiles.Any(t => t.Id == x.TeacherId && t.SchoolId == school && t.IsActive && t.User.IsActive)));

    internal static IQueryable<EvidenceLink> Approved(AlFalahDbContext db, int school, int year, int? owner = null) =>
        Eligible(db, school, year, owner).Where(x => x.Status == EvidenceLinkStatus.Approved &&
        x.Version.Availability == StoredFileAvailability.Available &&
        db.EvidenceReviewDecisions.Any(d => d.EvidenceLinkId == x.Id && d.VersionId == x.VersionId && d.Decision == EvidenceReviewStatus.Approved));
}
