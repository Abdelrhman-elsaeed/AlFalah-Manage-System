BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003231834_SchoolFileStorageEvidenceReview'
)
BEGIN
    ALTER TABLE [StoredFiles] ADD [SharedWriterFingerprint] nvarchar(64) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003231834_SchoolFileStorageEvidenceReview'
)
BEGIN
    ALTER TABLE [StoredFiles] ADD [SharedWriterProvenanceJson] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003231834_SchoolFileStorageEvidenceReview'
)
BEGIN
    ALTER TABLE [StorageOperations] ADD [ChangeRequestId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003231834_SchoolFileStorageEvidenceReview'
)
BEGIN
    CREATE TABLE [FileChangeRequests] (
        [Id] int NOT NULL IDENTITY,
        [SchoolId] int NOT NULL,
        [StoredFileId] int NOT NULL,
        [OriginalVersionId] int NOT NULL,
        [CandidateVersionId] int NULL,
        [Kind] nvarchar(16) NOT NULL,
        [Status] nvarchar(16) NOT NULL,
        [Reason] nvarchar(1000) NOT NULL,
        [RequestedByUserId] nvarchar(450) NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_FileChangeRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_FileChangeRequests_SchoolId_Id] UNIQUE ([SchoolId], [Id]),
        CONSTRAINT [CK_FileChangeRequest_State] CHECK ([Kind] IN ('Replace','Delete') AND [Status] IN ('Pending','Approved','Rejected') AND LTRIM(RTRIM([Reason])) <> ''),
        CONSTRAINT [FK_FileChangeRequests_Schools_SchoolId] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FileChangeRequests_StoredFileVersions_SchoolId_StoredFileId_CandidateVersionId] FOREIGN KEY ([SchoolId], [StoredFileId], [CandidateVersionId]) REFERENCES [StoredFileVersions] ([SchoolId], [StoredFileId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FileChangeRequests_StoredFileVersions_SchoolId_StoredFileId_OriginalVersionId] FOREIGN KEY ([SchoolId], [StoredFileId], [OriginalVersionId]) REFERENCES [StoredFileVersions] ([SchoolId], [StoredFileId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FileChangeRequests_StoredFiles_SchoolId_StoredFileId] FOREIGN KEY ([SchoolId], [StoredFileId]) REFERENCES [StoredFiles] ([SchoolId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FileChangeRequests_Users_RequestedByUserId] FOREIGN KEY ([RequestedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003231834_SchoolFileStorageEvidenceReview'
)
BEGIN
    CREATE TABLE [FileChangeDecisions] (
        [Id] int NOT NULL IDENTITY,
        [SchoolId] int NOT NULL,
        [FileChangeRequestId] int NOT NULL,
        [Decision] nvarchar(16) NOT NULL,
        [ReviewedByUserId] nvarchar(450) NOT NULL,
        [Note] nvarchar(1000) NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_FileChangeDecisions] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_FileChangeDecisions_SchoolId_Id] UNIQUE ([SchoolId], [Id]),
        CONSTRAINT [CK_FileChangeDecision_State] CHECK ([Decision] IN ('Approved','Rejected') AND ([Decision] <> 'Rejected' OR LTRIM(RTRIM([Note])) <> '' AND [Note] IS NOT NULL)),
        CONSTRAINT [FK_FileChangeDecisions_FileChangeRequests_SchoolId_FileChangeRequestId] FOREIGN KEY ([SchoolId], [FileChangeRequestId]) REFERENCES [FileChangeRequests] ([SchoolId], [Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FileChangeDecisions_Schools_SchoolId] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FileChangeDecisions_Users_ReviewedByUserId] FOREIGN KEY ([ReviewedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003231834_SchoolFileStorageEvidenceReview'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_StorageOperations_ChangeRequestId] ON [StorageOperations] ([ChangeRequestId]) WHERE [ChangeRequestId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003231834_SchoolFileStorageEvidenceReview'
)
BEGIN
    CREATE INDEX [IX_StorageOperations_SchoolId_ChangeRequestId] ON [StorageOperations] ([SchoolId], [ChangeRequestId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003231834_SchoolFileStorageEvidenceReview'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FileChangeDecisions_FileChangeRequestId] ON [FileChangeDecisions] ([FileChangeRequestId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003231834_SchoolFileStorageEvidenceReview'
)
BEGIN
    CREATE INDEX [IX_FileChangeDecisions_ReviewedByUserId] ON [FileChangeDecisions] ([ReviewedByUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003231834_SchoolFileStorageEvidenceReview'
)
BEGIN
    CREATE INDEX [IX_FileChangeDecisions_SchoolId_FileChangeRequestId] ON [FileChangeDecisions] ([SchoolId], [FileChangeRequestId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003231834_SchoolFileStorageEvidenceReview'
)
BEGIN
    CREATE INDEX [IX_FileChangeRequests_RequestedByUserId] ON [FileChangeRequests] ([RequestedByUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003231834_SchoolFileStorageEvidenceReview'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_FileChangeRequests_SchoolId_StoredFileId] ON [FileChangeRequests] ([SchoolId], [StoredFileId]) WHERE [Status] = ''Pending''');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003231834_SchoolFileStorageEvidenceReview'
)
BEGIN
    CREATE INDEX [IX_FileChangeRequests_SchoolId_StoredFileId_CandidateVersionId] ON [FileChangeRequests] ([SchoolId], [StoredFileId], [CandidateVersionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003231834_SchoolFileStorageEvidenceReview'
)
BEGIN
    CREATE INDEX [IX_FileChangeRequests_SchoolId_StoredFileId_OriginalVersionId] ON [FileChangeRequests] ([SchoolId], [StoredFileId], [OriginalVersionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003231834_SchoolFileStorageEvidenceReview'
)
BEGIN
    ALTER TABLE [StorageOperations] ADD CONSTRAINT [FK_StorageOperations_FileChangeRequests_SchoolId_ChangeRequestId] FOREIGN KEY ([SchoolId], [ChangeRequestId]) REFERENCES [FileChangeRequests] ([SchoolId], [Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003231834_SchoolFileStorageEvidenceReview'
)
BEGIN
    EXEC(N'CREATE TRIGGER TR_FileChangeDecisions_AppendOnly ON FileChangeDecisions AFTER UPDATE, DELETE AS
    BEGIN
        SET NOCOUNT ON;
        IF EXISTS(SELECT 1 FROM deleted) THROW 51101, ''File change decisions are append-only.'', 1;
    END');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003231834_SchoolFileStorageEvidenceReview'
)
BEGIN
    EXEC(N'CREATE TRIGGER TR_FileChangeRequests_History ON FileChangeRequests AFTER UPDATE, DELETE AS
    BEGIN
        SET NOCOUNT ON;
        IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id WHERE i.Id IS NULL)
            THROW 51102, ''File change requests cannot be removed.'', 1;
        IF EXISTS(SELECT Id, SchoolId, StoredFileId, OriginalVersionId, Kind, Reason, RequestedByUserId, CreatedAtUtc FROM deleted
            EXCEPT SELECT Id, SchoolId, StoredFileId, OriginalVersionId, Kind, Reason, RequestedByUserId, CreatedAtUtc FROM inserted)
            THROW 51103, ''File change request provenance is immutable.'', 1;
        IF EXISTS(SELECT 1 FROM deleted d JOIN inserted i ON i.Id=d.Id WHERE d.Status <> ''Pending'' AND
            (i.Status <> d.Status OR ISNULL(i.CandidateVersionId,0) <> ISNULL(d.CandidateVersionId,0)))
            THROW 51104, ''Decided file changes are immutable.'', 1;
        IF EXISTS(SELECT 1 FROM deleted d JOIN inserted i ON i.Id=d.Id WHERE d.CandidateVersionId IS NOT NULL AND
            ISNULL(i.CandidateVersionId,0) <> d.CandidateVersionId)
            THROW 51105, ''Candidate version cannot be replaced.'', 1;
    END');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003231834_SchoolFileStorageEvidenceReview'
)
BEGIN
    EXEC(N'CREATE TRIGGER TR_StoredFiles_SharedProvenance ON StoredFiles AFTER UPDATE AS
    BEGIN
        SET NOCOUNT ON;
        IF EXISTS(SELECT Id, SharedWriterProvenanceJson, SharedWriterFingerprint FROM deleted WHERE SharedWriterFingerprint IS NOT NULL
            EXCEPT SELECT Id, SharedWriterProvenanceJson, SharedWriterFingerprint FROM inserted)
            THROW 51106, ''Shared writer provenance is immutable.'', 1;
    END');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003231834_SchoolFileStorageEvidenceReview'
)
BEGIN
    EXEC(N'CREATE TRIGGER TR_AuditLogs_StorageAppendOnly ON AuditLogs AFTER UPDATE, DELETE AS
    BEGIN
        SET NOCOUNT ON;
        IF EXISTS(SELECT 1 FROM deleted WHERE Action LIKE ''Storage.%'')
            THROW 51107, ''Storage audit is append-only.'', 1;
    END');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003231834_SchoolFileStorageEvidenceReview'
)
BEGIN
    EXEC(N'CREATE TRIGGER TR_TeacherEvidenceSubmissions_ReviewAuthority ON TeacherEvidenceSubmissions AFTER UPDATE AS
    BEGIN
        SET NOCOUNT ON;
        IF EXISTS(SELECT d.Id, d.ReviewStatus, d.ReviewNote, d.ReviewedAtUtc, d.ReviewedByUserId FROM deleted d
            WHERE EXISTS(SELECT 1 FROM StoredFiles f WHERE f.LegacySubmissionId=d.Id)
            EXCEPT SELECT i.Id, i.ReviewStatus, i.ReviewNote, i.ReviewedAtUtc, i.ReviewedByUserId FROM inserted i)
            THROW 51108, ''Mapped submission review is a compatibility baseline; use EvidenceLink decisions.'', 1;
    END');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003231834_SchoolFileStorageEvidenceReview'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261003231834_SchoolFileStorageEvidenceReview', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003232149_SchoolFileStorageReviewIntegrity'
)
BEGIN
    EXEC(N'ALTER TABLE [EvidenceReviewDecisions] ADD CONSTRAINT [CK_EvidenceReviewDecisions_RejectReason] CHECK ([Decision] <> 4 OR [IsLegacyImported] = 1 OR ([Note] IS NOT NULL AND LTRIM(RTRIM([Note])) <> ''''))');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003232149_SchoolFileStorageReviewIntegrity'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_EvidenceRequirements_SchoolId_AcademicYearId_TemplateVersion_OriginalTaskId] ON [EvidenceRequirements] ([SchoolId], [AcademicYearId], [TemplateVersion], [OriginalTaskId]) WHERE [SchoolId] IS NOT NULL AND [AcademicYearId] IS NOT NULL AND [OriginalTaskId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003232149_SchoolFileStorageReviewIntegrity'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261003232149_SchoolFileStorageReviewIntegrity', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003233803_SchoolFileStoragePreApprovalReplacement'
)
BEGIN
    ALTER TABLE [FileChangeRequests] ADD [ReplaceBeforeReview] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003233803_SchoolFileStoragePreApprovalReplacement'
)
BEGIN
    EXEC(N'ALTER TRIGGER TR_FileChangeRequests_History ON FileChangeRequests AFTER UPDATE, DELETE AS
    BEGIN
        SET NOCOUNT ON;
        IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id WHERE i.Id IS NULL)
            THROW 51102, ''File change requests cannot be removed.'', 1;
        IF EXISTS(SELECT Id, SchoolId, StoredFileId, OriginalVersionId, Kind, Reason, RequestedByUserId, CreatedAtUtc, ReplaceBeforeReview FROM deleted
            EXCEPT SELECT Id, SchoolId, StoredFileId, OriginalVersionId, Kind, Reason, RequestedByUserId, CreatedAtUtc, ReplaceBeforeReview FROM inserted)
            THROW 51103, ''File change request provenance is immutable.'', 1;
        IF EXISTS(SELECT 1 FROM deleted d JOIN inserted i ON i.Id=d.Id WHERE d.Status <> ''Pending'' AND
            (i.Status <> d.Status OR ISNULL(i.CandidateVersionId,0) <> ISNULL(d.CandidateVersionId,0)))
            THROW 51104, ''Decided file changes are immutable.'', 1;
        IF EXISTS(SELECT 1 FROM deleted d JOIN inserted i ON i.Id=d.Id WHERE d.CandidateVersionId IS NOT NULL AND
            ISNULL(i.CandidateVersionId,0) <> d.CandidateVersionId)
            THROW 51105, ''Candidate version cannot be replaced.'', 1;
    END');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003233803_SchoolFileStoragePreApprovalReplacement'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261003233803_SchoolFileStoragePreApprovalReplacement', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003233933_SchoolFileStorageReviewerIdentity'
)
BEGIN
    ALTER TABLE [FileChangeDecisions] ADD [ReviewerName] nvarchar(300) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003233933_SchoolFileStorageReviewerIdentity'
)
BEGIN
    ALTER TABLE [EvidenceReviewDecisions] ADD [ReviewerName] nvarchar(300) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261003233933_SchoolFileStorageReviewerIdentity'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261003233933_SchoolFileStorageReviewerIdentity', N'8.0.0');
END;
GO

COMMIT;
GO
