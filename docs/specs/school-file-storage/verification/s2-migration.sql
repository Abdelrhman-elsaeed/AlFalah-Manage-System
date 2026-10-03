BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003034907_SchoolFileStorageLibrary'
)
BEGIN
    CREATE TABLE [StorageOperations] (
        [Id] int NOT NULL IDENTITY,
        [SchoolId] int NOT NULL,
        [ActorUserId] nvarchar(450) NOT NULL,
        [RequestKey] nvarchar(128) NOT NULL,
        [Fingerprint] nvarchar(64) NOT NULL,
        [Action] nvarchar(32) NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [FolderId] int NULL,
        [StoredFileId] int NULL,
        [VersionId] int NULL,
        [OwnerTeacherId] int NULL,
        [DriveId] nvarchar(256) NOT NULL,
        [ProviderItemId] nvarchar(256) NOT NULL,
        [ParentItemId] nvarchar(256) NOT NULL,
        [DisplayName] nvarchar(255) NOT NULL,
        [MimeType] nvarchar(256) NOT NULL,
        [Size] bigint NOT NULL,
        [SHA256] nvarchar(64) NULL,
        [LegacyTaskId] int NULL,
        [LegacyOperationId] bigint NULL,
        [LegacySubmissionId] bigint NULL,
        [ErrorCode] nvarchar(64) NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_StorageOperations] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_StorageOperations_SchoolId_Id] UNIQUE ([SchoolId], [Id]),
        CONSTRAINT [CK_StorageOperations_Status] CHECK ([Status] IN ('Pending','Completed','Failed','NeedsAttention')),
        CONSTRAINT [FK_StorageOperations_Schools_SchoolId] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StorageOperations_StorageFolders_SchoolId_FolderId] FOREIGN KEY ([SchoolId], [FolderId]) REFERENCES [StorageFolders] ([SchoolId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StorageOperations_Users_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003034907_SchoolFileStorageLibrary'
)
BEGIN
    CREATE INDEX [IX_StorageOperations_ActorUserId] ON [StorageOperations] ([ActorUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003034907_SchoolFileStorageLibrary'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StorageOperations_SchoolId_ActorUserId_RequestKey] ON [StorageOperations] ([SchoolId], [ActorUserId], [RequestKey]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003034907_SchoolFileStorageLibrary'
)
BEGIN
    CREATE INDEX [IX_StorageOperations_SchoolId_FolderId] ON [StorageOperations] ([SchoolId], [FolderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003034907_SchoolFileStorageLibrary'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_StorageOperations_SchoolId_ProviderItemId] ON [StorageOperations] ([SchoolId], [ProviderItemId]) WHERE [Action] IN (''Upload'',''CreateFolder'')');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003034907_SchoolFileStorageLibrary'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_StorageOperations_SchoolId_RequestKey] ON [StorageOperations] ([SchoolId], [RequestKey]) WHERE [Action] = ''CreateFolder''');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003034907_SchoolFileStorageLibrary'
)
BEGIN
    CREATE INDEX [IX_StorageOperations_SchoolId_Status_CreatedAtUtc] ON [StorageOperations] ([SchoolId], [Status], [CreatedAtUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003034907_SchoolFileStorageLibrary'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261003034907_SchoolFileStorageLibrary', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003040217_SchoolFileStorageLibraryIntegrity'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_StoredFiles_SchoolId_OwnerTeacherId_FolderId_DisplayName_Id] ON [StoredFiles] ([SchoolId], [OwnerTeacherId], [FolderId], [DisplayName], [Id]) WHERE [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003040217_SchoolFileStorageLibraryIntegrity'
)
BEGIN
    CREATE INDEX [IX_StorageOperations_SchoolId_StoredFileId_VersionId] ON [StorageOperations] ([SchoolId], [StoredFileId], [VersionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003040217_SchoolFileStorageLibraryIntegrity'
)
BEGIN
    EXEC(N'ALTER TABLE [StorageOperations] ADD CONSTRAINT [CK_StorageOperations_Completed] CHECK ([Status] <> ''Completed'' OR [Action] <> ''Upload'' OR ([StoredFileId] IS NOT NULL AND [VersionId] IS NOT NULL))');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003040217_SchoolFileStorageLibraryIntegrity'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_StorageFolders_SchoolId_Kind] ON [StorageFolders] ([SchoolId], [Kind]) WHERE [IsActive] = 1 AND [OwnerTeacherId] IS NULL AND [ParentFolderId] IS NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003040217_SchoolFileStorageLibraryIntegrity'
)
BEGIN
    CREATE INDEX [IX_StorageFolders_SchoolId_OwnerTeacherId_ParentFolderId_DisplayName_Id] ON [StorageFolders] ([SchoolId], [OwnerTeacherId], [ParentFolderId], [DisplayName], [Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003040217_SchoolFileStorageLibraryIntegrity'
)
BEGIN
    ALTER TABLE [StorageOperations] ADD CONSTRAINT [FK_StorageOperations_StoredFileVersions_SchoolId_StoredFileId_VersionId] FOREIGN KEY ([SchoolId], [StoredFileId], [VersionId]) REFERENCES [StoredFileVersions] ([SchoolId], [StoredFileId], [Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003040217_SchoolFileStorageLibraryIntegrity'
)
BEGIN
    ALTER TABLE [StorageOperations] ADD CONSTRAINT [FK_StorageOperations_StoredFiles_SchoolId_StoredFileId] FOREIGN KEY ([SchoolId], [StoredFileId]) REFERENCES [StoredFiles] ([SchoolId], [Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003040217_SchoolFileStorageLibraryIntegrity'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261003040217_SchoolFileStorageLibraryIntegrity', N'8.0.0');
END;
GO

COMMIT;
GO
