BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    ALTER TABLE [StoredFiles] DROP CONSTRAINT [CK_StoredFiles_SourceKind];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    ALTER TABLE [StorageFolders] DROP CONSTRAINT [CK_StorageFolders_Kind];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    ALTER TABLE [StoredFiles] ADD [DutyIdentity] nvarchar(450) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    ALTER TABLE [StorageFolders] ADD [DutyIdentity] nvarchar(450) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    ALTER TABLE [StorageFolders] ADD [DutyProvisioned] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    ALTER TABLE [SchoolGoogleDrives] ADD [DutyArchiveEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    ALTER TABLE [DutyArchiveIntents] ADD [ArtifactId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    ALTER TABLE [DutyArchiveIntents] ADD [Attempts] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    ALTER TABLE [DutyArchiveIntents] ADD [DriveId] nvarchar(256) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    ALTER TABLE [DutyArchiveIntents] ADD [ErrorCode] nvarchar(80) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    ALTER TABLE [DutyArchiveIntents] ADD [FolderId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    ALTER TABLE [DutyArchiveIntents] ADD [FolderItemId] nvarchar(256) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    ALTER TABLE [DutyArchiveIntents] ADD [LastAttemptAt] datetimeoffset NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    ALTER TABLE [DutyArchiveIntents] ADD [LeaseExpiresAt] datetimeoffset NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    ALTER TABLE [DutyArchiveIntents] ADD [LeaseToken] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    ALTER TABLE [DutyArchiveIntents] ADD [NextAttemptAt] datetimeoffset NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    ALTER TABLE [DutyArchiveIntents] ADD [OperationIdentity] nvarchar(200) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    ALTER TABLE [DutyArchiveIntents] ADD [ProviderItemId] nvarchar(256) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    ALTER TABLE [DutyArchiveIntents] ADD [RowVersion] rowversion NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    ALTER TABLE [DutyArchiveIntents] ADD [SchoolRootItemId] nvarchar(256) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    ALTER TABLE [DutyArchiveIntents] ADD [StoredFileVersionId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    ALTER TABLE [DutyArchiveIntents] ADD [VerifiedAt] datetimeoffset NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    CREATE TABLE [DutyPdfArtifacts] (
        [Id] int NOT NULL IDENTITY,
        [SchoolId] int NOT NULL,
        [DocumentId] int NULL,
        [PlanId] int NULL,
        [PlanRevision] int NOT NULL,
        [ProvenanceJson] nvarchar(max) NOT NULL,
        [ProvenanceHash] nvarchar(64) NOT NULL,
        [RendererVersion] nvarchar(80) NOT NULL,
        [FileName] nvarchar(512) NOT NULL,
        [Bytes] varbinary(max) NOT NULL,
        [Hash] nvarchar(64) NOT NULL,
        [PreparedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_DutyPdfArtifacts] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_DutyPdfArtifacts_SchoolId_Id] UNIQUE ([SchoolId], [Id]),
        CONSTRAINT [CK_DutyPdfArtifacts_Identity] CHECK (([DocumentId] IS NOT NULL AND [PlanId] IS NULL) OR ([DocumentId] IS NULL AND [PlanId] IS NOT NULL AND [PlanRevision] > 0)),
        CONSTRAINT [FK_DutyPdfArtifacts_DutyPlans_SchoolId_PlanId] FOREIGN KEY ([SchoolId], [PlanId]) REFERENCES [DutyPlans] ([SchoolId], [Id]),
        CONSTRAINT [FK_DutyPdfArtifacts_DutySignedDocuments_SchoolId_DocumentId] FOREIGN KEY ([SchoolId], [DocumentId]) REFERENCES [DutySignedDocuments] ([SchoolId], [Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_StoredFiles_SchoolId_DutyIdentity] ON [StoredFiles] ([SchoolId], [DutyIdentity]) WHERE [DutyIdentity] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    EXEC(N'ALTER TABLE [StoredFiles] ADD CONSTRAINT [CK_StoredFiles_SourceKind] CHECK ([SourceKind] BETWEEN 1 AND 5)');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_StorageFolders_SchoolId_DutyIdentity] ON [StorageFolders] ([SchoolId], [DutyIdentity]) WHERE [DutyIdentity] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    EXEC(N'ALTER TABLE [StorageFolders] ADD CONSTRAINT [CK_StorageFolders_Kind] CHECK ([Kind] BETWEEN 1 AND 4)');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_DutyArchiveIntents_OperationIdentity] ON [DutyArchiveIntents] ([OperationIdentity]) WHERE [OperationIdentity] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    CREATE INDEX [IX_DutyArchiveIntents_SchoolId_ArtifactId] ON [DutyArchiveIntents] ([SchoolId], [ArtifactId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    CREATE INDEX [IX_DutyArchiveIntents_SchoolId_FolderId] ON [DutyArchiveIntents] ([SchoolId], [FolderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    CREATE INDEX [IX_DutyArchiveIntents_SchoolId_StoredFileVersionId] ON [DutyArchiveIntents] ([SchoolId], [StoredFileVersionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    CREATE INDEX [IX_DutyArchiveIntents_State_NextAttemptAt] ON [DutyArchiveIntents] ([State], [NextAttemptAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_DutyPdfArtifacts_DocumentId] ON [DutyPdfArtifacts] ([DocumentId]) WHERE [DocumentId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    CREATE INDEX [IX_DutyPdfArtifacts_SchoolId_DocumentId] ON [DutyPdfArtifacts] ([SchoolId], [DocumentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_DutyPdfArtifacts_SchoolId_PlanId_PlanRevision] ON [DutyPdfArtifacts] ([SchoolId], [PlanId], [PlanRevision]) WHERE [PlanId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    ALTER TABLE [DutyArchiveIntents] ADD CONSTRAINT [FK_DutyArchiveIntents_DutyPdfArtifacts_SchoolId_ArtifactId] FOREIGN KEY ([SchoolId], [ArtifactId]) REFERENCES [DutyPdfArtifacts] ([SchoolId], [Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    ALTER TABLE [DutyArchiveIntents] ADD CONSTRAINT [FK_DutyArchiveIntents_StorageFolders_SchoolId_FolderId] FOREIGN KEY ([SchoolId], [FolderId]) REFERENCES [StorageFolders] ([SchoolId], [Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    ALTER TABLE [DutyArchiveIntents] ADD CONSTRAINT [FK_DutyArchiveIntents_StoredFileVersions_SchoolId_StoredFileVersionId] FOREIGN KEY ([SchoolId], [StoredFileVersionId]) REFERENCES [StoredFileVersions] ([SchoolId], [Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    EXEC(N'CREATE TRIGGER TR_DutyPdfArtifacts_Immutable ON DutyPdfArtifacts AFTER UPDATE, DELETE AS
    BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM deleted) THROW 51301, ''Duty PDF evidence is immutable.'', 1; END');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    EXEC(N'CREATE TRIGGER TR_DutyPdfArtifacts_Integrity ON DutyPdfArtifacts AFTER INSERT AS
    BEGIN
      SET NOCOUNT ON;
      IF EXISTS(SELECT 1 FROM inserted WHERE ISJSON(ProvenanceJson)<>1 OR DATALENGTH(Bytes)<100
        OR SUBSTRING(Bytes,1,5)<>0x255044462D OR CONVERT(varchar(64),HASHBYTES(''SHA2_256'',Bytes),2)<>Hash
        OR CONVERT(varchar(64),HASHBYTES(''SHA2_256'',CONVERT(varbinary(max),CONVERT(varchar(max),ProvenanceJson COLLATE Latin1_General_100_BIN2_UTF8))),2)<>ProvenanceHash)
        THROW 51302, ''Duty PDF content hash is invalid.'', 1;
      IF EXISTS(SELECT 1 FROM inserted i JOIN DutySignedDocuments d ON d.Id=i.DocumentId
        WHERE ISNULL(JSON_VALUE(i.ProvenanceJson,''$.SnapshotHash''),'''')<>d.SnapshotHash
        OR ISNULL(JSON_VALUE(i.ProvenanceJson,''$.SignatureHash''),'''')<>d.SignatureHash
        OR ISNULL(TRY_CONVERT(int,JSON_VALUE(i.ProvenanceJson,''$.Id'')),0)<>d.Id
        OR ISNULL(JSON_VALUE(i.ProvenanceJson,''$.RendererVersion''),'''')<>i.RendererVersion)
        THROW 51303, ''Duty PDF provenance does not match its signed evidence.'', 1;
    END');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    EXEC(N'CREATE TRIGGER TR_DutyArchiveIntents_Identity ON DutyArchiveIntents AFTER INSERT, UPDATE, DELETE AS
    BEGIN
      SET NOCOUNT ON;
      IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id WHERE i.Id IS NULL
        OR i.SchoolId<>d.SchoolId OR i.DocumentId<>d.DocumentId OR i.CreatedAt<>d.CreatedAt
        OR (d.ArtifactId IS NOT NULL AND ISNULL(i.ArtifactId,0)<>d.ArtifactId)
        OR (d.OperationIdentity IS NOT NULL AND ISNULL(i.OperationIdentity,'''')<>d.OperationIdentity)
        OR (d.ProviderItemId IS NOT NULL AND ISNULL(i.ProviderItemId,'''')<>d.ProviderItemId)
        OR (d.DriveId IS NOT NULL AND ISNULL(i.DriveId,'''')<>d.DriveId)
        OR (d.SchoolRootItemId IS NOT NULL AND ISNULL(i.SchoolRootItemId,'''')<>d.SchoolRootItemId)
        OR (d.FolderId IS NOT NULL AND ISNULL(i.FolderId,0)<>d.FolderId)
        OR (d.FolderItemId IS NOT NULL AND ISNULL(i.FolderItemId,'''')<>d.FolderItemId)
        OR (d.StoredFileVersionId IS NOT NULL AND ISNULL(i.StoredFileVersionId,0)<>d.StoredFileVersionId)
        OR (d.State IN (''Archived'',''TestVerified'') AND i.State<>d.State))
        THROW 51304, ''Duty archive operation identity and completed history are immutable.'', 1;
      IF EXISTS(SELECT 1 FROM inserted i JOIN DutyPdfArtifacts a ON a.Id=i.ArtifactId
        WHERE a.DocumentId IS NULL OR a.DocumentId<>i.DocumentId)
        THROW 51305, ''Duty archive artifact belongs to another document.'', 1;
      IF EXISTS(SELECT 1 FROM inserted i JOIN StorageFolders f ON f.Id=i.FolderId
        WHERE f.Kind<>4 OR f.DriveItemId<>i.FolderItemId)
        THROW 51306, ''Duty archive requires a protected duty folder.'', 1;
      IF EXISTS(SELECT 1 FROM inserted i WHERE i.State IN (''Archived'',''TestVerified'') AND
        (i.VerifiedAt IS NULL OR i.ArtifactId IS NULL OR i.StoredFileVersionId IS NULL OR NOT EXISTS(
          SELECT 1 FROM StoredFileVersions v JOIN StoredFiles f ON f.Id=v.StoredFileId
          JOIN DutyPdfArtifacts a ON a.Id=i.ArtifactId WHERE v.Id=i.StoredFileVersionId AND f.SourceKind=5
          AND f.FolderId=i.FolderId AND v.DriveItemId=i.ProviderItemId AND v.SHA256=a.Hash AND v.SizeInBytes=DATALENGTH(a.Bytes))))
        THROW 51307, ''Duty completion requires committed verified ledger evidence.'', 1;
    END');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    EXEC(N'CREATE TRIGGER TR_StorageFolders_DutyProtection ON StorageFolders AFTER INSERT, UPDATE, DELETE AS
    BEGIN
      SET NOCOUNT ON;
      IF EXISTS(SELECT 1 FROM inserted WHERE (Kind=4 AND DutyIdentity IS NULL) OR (Kind<>4 AND (DutyIdentity IS NOT NULL OR DutyProvisioned=1)))
        THROW 51308, ''Duty folder identity requires the duty folder kind.'', 1;
      IF EXISTS(SELECT 1 FROM inserted i JOIN StorageFolders p ON p.Id=i.ParentFolderId
        WHERE (p.Kind=4 AND i.Kind<>4) OR (i.Kind=4 AND p.Kind<>4))
        THROW 51309, ''Duty folder hierarchy is protected.'', 1;
      IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id WHERE d.Kind=4 AND
        (i.Id IS NULL OR i.Kind<>4 OR i.SchoolId<>d.SchoolId OR ISNULL(i.ParentFolderId,0)<>ISNULL(d.ParentFolderId,0)
        OR i.DriveId<>d.DriveId OR i.DriveItemId<>d.DriveItemId OR i.DisplayName<>d.DisplayName
        OR ISNULL(i.DutyIdentity,'''')<>d.DutyIdentity OR i.IsActive<>d.IsActive
        OR ISNULL(i.OwnerTeacherId,0)<>ISNULL(d.OwnerTeacherId,0) OR i.DutyProvisioned<d.DutyProvisioned))
        THROW 51310, ''Duty folders cannot be renamed, moved, rebound or removed.'', 1;
    END');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    EXEC(N'CREATE TRIGGER TR_StoredFiles_DutyProtection ON StoredFiles AFTER INSERT, UPDATE, DELETE AS
    BEGIN
      SET NOCOUNT ON;
      IF EXISTS(SELECT 1 FROM inserted i JOIN StorageFolders f ON f.Id=i.FolderId WHERE
        (i.SourceKind=5 AND (f.Kind<>4 OR i.DutyIdentity IS NULL OR i.OwnerTeacherId IS NOT NULL
          OR i.NeedsLink=1 OR i.LegacySubmissionId IS NOT NULL)) OR (i.SourceKind<>5 AND (f.Kind=4 OR i.DutyIdentity IS NOT NULL)))
        THROW 51311, ''Duty files require independent protected provenance.'', 1;
      IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id WHERE d.SourceKind=5 AND
        (i.Id IS NULL OR i.SourceKind<>5 OR i.SchoolId<>d.SchoolId OR i.FolderId<>d.FolderId
        OR i.DisplayName<>d.DisplayName OR ISNULL(i.DutyIdentity,'''')<>d.DutyIdentity
        OR i.IsDeleted<>d.IsDeleted OR i.NeedsLink<>d.NeedsLink OR i.OwnerTeacherId IS NOT NULL))
        THROW 51312, ''Duty file identities and signed history are protected.'', 1;
      IF EXISTS(SELECT 1 FROM inserted i JOIN StoredFileVersions v ON v.Id=i.CurrentVersionId
        WHERE i.SourceKind=5 AND (v.StoredFileId<>i.Id OR NOT EXISTS(SELECT 1 FROM DutyArchiveIntents a
          WHERE a.ProviderItemId=v.DriveItemId AND a.SchoolId=i.SchoolId AND a.FolderId=i.FolderId)))
        THROW 51313, ''Duty current version requires reserved operation evidence.'', 1;
    END');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    EXEC(N'CREATE TRIGGER TR_StoredFileVersions_DutyProvenance ON StoredFileVersions AFTER INSERT AS
    BEGIN
      SET NOCOUNT ON;
      IF EXISTS(SELECT 1 FROM inserted i JOIN StoredFiles f ON f.Id=i.StoredFileId WHERE f.SourceKind=5 AND NOT EXISTS(
        SELECT 1 FROM DutyArchiveIntents o JOIN DutyPdfArtifacts a ON a.Id=o.ArtifactId
        JOIN DutySignedDocuments d ON d.Id=o.DocumentId WHERE o.SchoolId=i.SchoolId AND o.FolderId=f.FolderId
        AND o.ProviderItemId=i.DriveItemId AND o.DriveId=i.DriveId AND a.Hash=i.SHA256
        AND i.SizeInBytes=DATALENGTH(a.Bytes) AND i.UploadedByUserId=d.AuthorUserId AND i.MimeType=''application/pdf''))
        THROW 51314, ''Duty version must match frozen bytes and the reserved identity.'', 1;
    END');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    EXEC(N'CREATE TRIGGER TR_EvidenceLinks_NoDuty ON EvidenceLinks AFTER INSERT, UPDATE AS
    BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM inserted i JOIN StoredFiles f ON f.Id=i.StoredFileId WHERE f.SourceKind=5)
      THROW 51315, ''Duty documents cannot fulfill evidence requirements.'', 1; END');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    EXEC(N'CREATE TRIGGER TR_FileChangeRequests_NoDuty ON FileChangeRequests AFTER INSERT, UPDATE AS
    BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM inserted i JOIN StoredFiles f ON f.Id=i.StoredFileId WHERE f.SourceKind=5)
      THROW 51316, ''Duty documents cannot enter general storage change workflows.'', 1; END');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013165202_AddSchoolDutyD3'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261013165202_AddSchoolDutyD3', N'8.0.0');
END;
GO

COMMIT;
GO

