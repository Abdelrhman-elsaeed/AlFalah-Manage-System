BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005112201_SchoolFileStorageVisitArchive'
)
BEGIN
    ALTER TABLE [Visits] ADD [ApprovalRevision] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005112201_SchoolFileStorageVisitArchive'
)
BEGIN
    ALTER TABLE [Visits] ADD [RowVersion] rowversion NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005112201_SchoolFileStorageVisitArchive'
)
BEGIN
    ALTER TABLE [VisitArchiveOperations] ADD [ApprovalSource] nvarchar(32) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005112201_SchoolFileStorageVisitArchive'
)
BEGIN
    ALTER TABLE [VisitArchiveOperations] ADD [ApprovedAtUtc] datetimeoffset NOT NULL DEFAULT '0001-01-01T00:00:00.0000000+00:00';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005112201_SchoolFileStorageVisitArchive'
)
BEGIN
    ALTER TABLE [VisitArchiveOperations] ADD [ArchiveFolderId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005112201_SchoolFileStorageVisitArchive'
)
BEGIN
    ALTER TABLE [VisitArchiveOperations] ADD [ArchiveFolderItemId] nvarchar(256) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005112201_SchoolFileStorageVisitArchive'
)
BEGIN
    ALTER TABLE [VisitArchiveOperations] ADD [CompletedAtUtc] datetimeoffset NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005112201_SchoolFileStorageVisitArchive'
)
BEGIN
    ALTER TABLE [VisitArchiveOperations] ADD [DriveId] nvarchar(256) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005112201_SchoolFileStorageVisitArchive'
)
BEGIN
    ALTER TABLE [VisitArchiveOperations] ADD [LastAttemptAtUtc] datetimeoffset NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005112201_SchoolFileStorageVisitArchive'
)
BEGIN
    ALTER TABLE [VisitArchiveOperations] ADD [LeaseExpiresAtUtc] datetimeoffset NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005112201_SchoolFileStorageVisitArchive'
)
BEGIN
    ALTER TABLE [VisitArchiveOperations] ADD [LeaseToken] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005112201_SchoolFileStorageVisitArchive'
)
BEGIN
    ALTER TABLE [VisitArchiveOperations] ADD [PdfBytes] varbinary(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005112201_SchoolFileStorageVisitArchive'
)
BEGIN
    ALTER TABLE [VisitArchiveOperations] ADD [PdfSHA256] nvarchar(64) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005112201_SchoolFileStorageVisitArchive'
)
BEGIN
    ALTER TABLE [VisitArchiveOperations] ADD [ProviderItemId] nvarchar(256) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005112201_SchoolFileStorageVisitArchive'
)
BEGIN
    ALTER TABLE [VisitArchiveOperations] ADD [RecoveryGeneration] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005112201_SchoolFileStorageVisitArchive'
)
BEGIN
    ALTER TABLE [VisitArchiveOperations] ADD [SchoolRootItemId] nvarchar(256) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005112201_SchoolFileStorageVisitArchive'
)
BEGIN
    ALTER TABLE [VisitArchiveOperations] ADD [SnapshotJson] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005112201_SchoolFileStorageVisitArchive'
)
BEGIN
    ALTER TABLE [VisitArchiveOperations] ADD [SnapshotSHA256] nvarchar(64) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005112201_SchoolFileStorageVisitArchive'
)
BEGIN
    ALTER TABLE [VisitArchiveOperations] ADD [UploadIdentity] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005112201_SchoolFileStorageVisitArchive'
)
BEGIN
    ALTER TABLE [VisitArchiveOperations] ADD [UploadStarted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005112201_SchoolFileStorageVisitArchive'
)
BEGIN
    ALTER TABLE [VisitArchiveArtifacts] ADD [LastReconciledAtUtc] datetimeoffset NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005112201_SchoolFileStorageVisitArchive'
)
BEGIN
    ALTER TABLE [VisitArchiveArtifacts] ADD [OriginalStoredFileVersionId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005112201_SchoolFileStorageVisitArchive'
)
BEGIN
    CREATE INDEX [IX_VisitArchiveOperations_SchoolId_ApprovedAtUtc_Id] ON [VisitArchiveOperations] ([SchoolId], [ApprovedAtUtc], [Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005112201_SchoolFileStorageVisitArchive'
)
BEGIN
    CREATE INDEX [IX_VisitArchiveArtifacts_SchoolId_OriginalStoredFileVersionId] ON [VisitArchiveArtifacts] ([SchoolId], [OriginalStoredFileVersionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005112201_SchoolFileStorageVisitArchive'
)
BEGIN
    ALTER TABLE [VisitArchiveArtifacts] ADD CONSTRAINT [FK_VisitArchiveArtifacts_StoredFileVersions_SchoolId_OriginalStoredFileVersionId] FOREIGN KEY ([SchoolId], [OriginalStoredFileVersionId]) REFERENCES [StoredFileVersions] ([SchoolId], [Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005112201_SchoolFileStorageVisitArchive'
)
BEGIN
    EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_Visits_ApprovalRevision ON dbo.Visits AFTER UPDATE AS
    BEGIN
      SET NOCOUNT ON;
      IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON i.Id=d.Id WHERE i.ApprovalRevision<d.ApprovalRevision)
        THROW 51201, ''Approval revision cannot decrease.'', 1;
    END');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005112201_SchoolFileStorageVisitArchive'
)
BEGIN
    EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_VisitArchiveArtifacts_History ON dbo.VisitArchiveArtifacts AFTER UPDATE,DELETE AS
    BEGIN
      SET NOCOUNT ON;
      IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON d.Id=i.Id WHERE i.Id IS NULL OR
        i.SchoolId<>d.SchoolId OR i.VisitId<>d.VisitId OR i.ApprovalRevision<>d.ApprovalRevision OR i.OperationId<>d.OperationId OR
        (d.OriginalStoredFileVersionId IS NOT NULL AND (i.OriginalStoredFileVersionId IS NULL OR i.OriginalStoredFileVersionId<>d.OriginalStoredFileVersionId)))
        THROW 51204, ''Archive identity and original history are immutable.'', 1;
      IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON i.Id=d.Id
        JOIN StoredFileVersions oldVersion ON oldVersion.Id=d.StoredFileVersionId
        JOIN StoredFileVersions newVersion ON newVersion.Id=i.StoredFileVersionId
        WHERE oldVersion.StoredFileId<>newVersion.StoredFileId)
        THROW 51205, ''Recreation must retain the original file identity.'', 1;
    END');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005112201_SchoolFileStorageVisitArchive'
)
BEGIN
    EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_VisitArchiveOperations_Snapshot ON dbo.VisitArchiveOperations AFTER UPDATE,DELETE AS
    BEGIN
      SET NOCOUNT ON;
      IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON d.Id=i.Id WHERE d.SnapshotJson IS NOT NULL AND
        (i.Id IS NULL OR i.SnapshotJson IS NULL OR CONVERT(varbinary(max),i.SnapshotJson)<>CONVERT(varbinary(max),d.SnapshotJson) OR
         i.SnapshotSHA256 IS NULL OR CONVERT(varbinary(max),i.SnapshotSHA256)<>CONVERT(varbinary(max),d.SnapshotSHA256) OR i.VisitId<>d.VisitId OR i.SchoolId<>d.SchoolId OR
         i.ApprovalRevision<>d.ApprovalRevision OR i.ApprovedAtUtc<>d.ApprovedAtUtc OR i.ApprovalSource IS NULL OR i.ApprovalSource<>d.ApprovalSource))
        THROW 51202, ''Approval snapshots are immutable.'', 1;
      IF EXISTS (SELECT 1 FROM deleted d JOIN inserted i ON d.Id=i.Id WHERE d.PdfBytes IS NOT NULL AND
        (i.PdfBytes IS NULL OR i.PdfBytes<>d.PdfBytes OR i.PdfSHA256 IS NULL OR i.PdfSHA256<>d.PdfSHA256))
        THROW 51203, ''Prepared report bytes are immutable.'', 1;
    END');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005112201_SchoolFileStorageVisitArchive'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261005112201_SchoolFileStorageVisitArchive', N'8.0.0');
END;
GO

COMMIT;
GO
