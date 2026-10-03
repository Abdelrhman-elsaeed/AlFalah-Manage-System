using Microsoft.EntityFrameworkCore.Migrations;

namespace AlFalah.Infrastructure.Data.Migrations;

public partial class SchoolFileStorageFoundation
{
    private static void AddStorageIntegrity(MigrationBuilder migration)
    {
        migration.Sql("""
            CREATE TRIGGER TR_StorageFolders_NoCycles ON StorageFolders AFTER INSERT, UPDATE AS
            BEGIN
                SET NOCOUNT ON;
                DECLARE @cycle bit = 0;
                ;WITH ancestors AS (
                    SELECT i.Id AS OriginId, i.Id AS NodeId, i.ParentFolderId, i.SchoolId, 0 AS Depth FROM inserted i
                    UNION ALL
                    SELECT a.OriginId, p.Id, p.ParentFolderId, p.SchoolId, a.Depth + 1
                    FROM ancestors a JOIN StorageFolders p WITH (UPDLOCK, HOLDLOCK)
                        ON p.Id = a.ParentFolderId AND p.SchoolId = a.SchoolId
                    WHERE a.ParentFolderId IS NOT NULL AND (a.Depth = 0 OR a.NodeId <> a.OriginId)
                )
                SELECT @cycle = 1 FROM ancestors WHERE NodeId = OriginId AND Depth > 0 OPTION (MAXRECURSION 32767);
                IF @cycle = 1 THROW 51001, 'Storage folder cycle is forbidden.', 1;
            END
            """);
        migration.Sql("""
            CREATE TRIGGER TR_EvidenceLinks_Scope ON EvidenceLinks AFTER INSERT, UPDATE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (
                    SELECT 1 FROM inserted i JOIN EvidenceRequirements r WITH (UPDLOCK, HOLDLOCK) ON r.Id = i.RequirementId
                    WHERE r.SchoolId IS NULL OR r.AcademicYearId IS NULL OR r.SchoolId <> i.SchoolId OR r.AcademicYearId <> i.AcademicYearId
                ) THROW 51002, 'Evidence links require the same concrete school and year.', 1;
                IF EXISTS (
                    SELECT 1 FROM inserted i JOIN StoredFiles f ON f.Id = i.StoredFileId
                    WHERE f.OwnerTeacherId IS NOT NULL AND (i.TeacherId IS NULL OR i.TeacherId <> f.OwnerTeacherId)
                ) THROW 51003, 'Teacher evidence ownership does not match the file.', 1;
                IF EXISTS (
                    SELECT 1 FROM inserted i JOIN InstructorProfiles t ON t.Id = i.TeacherId
                    JOIN StoredFiles f ON f.Id = i.StoredFileId
                    WHERE t.SchoolId <> i.SchoolId AND f.LegacySubmissionId IS NULL
                ) THROW 51004, 'Evidence teacher belongs to another school.', 1;
            END
            """);
        migration.Sql("""
            CREATE TRIGGER TR_EvidenceRequirements_Scope ON EvidenceRequirements AFTER UPDATE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (
                    SELECT 1 FROM inserted i JOIN EvidenceLinks l WITH (UPDLOCK, HOLDLOCK) ON l.RequirementId = i.Id
                    WHERE i.SchoolId IS NULL OR i.AcademicYearId IS NULL OR i.SchoolId <> l.SchoolId OR i.AcademicYearId <> l.AcademicYearId
                ) THROW 51005, 'A linked requirement cannot change its scope.', 1;
            END
            """);
        migration.Sql("""
            CREATE TRIGGER TR_VisitArchiveOperations_Scope ON VisitArchiveOperations AFTER INSERT, UPDATE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM inserted i JOIN Visits v ON v.Id = i.VisitId WHERE v.SchoolId <> i.SchoolId)
                    THROW 51006, 'Visit archive belongs to another school.', 1;
            END
            """);
        migration.Sql("""
            CREATE TRIGGER TR_EvidenceReviewDecisions_AppendOnly ON EvidenceReviewDecisions AFTER UPDATE, DELETE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM deleted) THROW 51007, 'Evidence decisions are append-only.', 1;
            END
            """);
        migration.Sql("""
            CREATE TRIGGER TR_StoredFileVersions_Immutable ON StoredFileVersions AFTER UPDATE, DELETE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id WHERE i.Id IS NULL)
                    THROW 51008, 'File versions cannot be deleted.', 1;
                IF EXISTS (
                    SELECT SchoolId, StoredFileId, VersionNumber, DriveId, DriveItemId, DriveFileName, FileExtension,
                        SHA256, SizeInBytes, MimeType, UploadedByUserId, UploadedAtUtc, CreatedAtUtc FROM deleted
                    EXCEPT
                    SELECT SchoolId, StoredFileId, VersionNumber, DriveId, DriveItemId, DriveFileName, FileExtension,
                        SHA256, SizeInBytes, MimeType, UploadedByUserId, UploadedAtUtc, CreatedAtUtc FROM inserted
                ) THROW 51009, 'File version bytes and identity are immutable.', 1;
            END
            """);
        migration.Sql("""
            CREATE TRIGGER TR_StoredFiles_Provenance ON StoredFiles AFTER UPDATE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (
                    SELECT Id, LegacySubmissionId, LegacyProvenanceJson, LegacyFingerprint FROM deleted
                    EXCEPT SELECT Id, LegacySubmissionId, LegacyProvenanceJson, LegacyFingerprint FROM inserted
                ) THROW 51010, 'Legacy provenance is immutable.', 1;
            END
            """);
        migration.Sql("""
            INSERT INTO Permissions (Name, [Group], DescriptionAr, DescriptionEn, CreatedAt)
            SELECT p.Name, N'Storage', p.Ar, p.En, SYSDATETIMEOFFSET()
            FROM (VALUES
                (N'Storage.ViewSchool', N'عرض مكتبة المدرسة', N'View school storage'),
                (N'Storage.ManageSchool', N'إدارة مكتبة المدرسة', N'Manage school storage'),
                (N'Storage.ReviewEvidence', N'مراجعة الشواهد', N'Review evidence'),
                (N'Storage.ViewOwn', N'عرض ملفاتي', N'View own storage'),
                (N'Storage.ManageOwn', N'إدارة ملفاتي', N'Manage own storage'),
                (N'Storage.ViewArchive', N'عرض أرشيف الزيارات', N'View visit archive'),
                (N'Storage.RetryArchive', N'إعادة محاولة الأرشفة', N'Retry visit archive'),
                (N'Storage.Delegate', N'تفويض إدارة التخزين', N'Delegate school storage')
            ) p(Name, Ar, En) WHERE NOT EXISTS (SELECT 1 FROM Permissions existing WHERE existing.Name = p.Name);
            INSERT INTO RolePermissions (RoleId, PermissionId, CreatedAt)
            SELECT r.Id, p.Id, SYSDATETIMEOFFSET() FROM Roles r CROSS JOIN Permissions p
            WHERE (r.Name = N'SchoolManager' AND p.[Group] = N'Storage'
                OR r.Name = N'Instructor' AND p.Name IN (N'Storage.ViewOwn', N'Storage.ManageOwn'))
                AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId = r.Id AND rp.PermissionId = p.Id);
            """);
    }
}
