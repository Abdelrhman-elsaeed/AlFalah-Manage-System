BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015060717_VersionDutyRosterRenderings'
)
BEGIN
    DROP INDEX [IX_DutyPdfArtifacts_SchoolId_PlanId_PlanRevision] ON [DutyPdfArtifacts];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015060717_VersionDutyRosterRenderings'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_DutyPdfArtifacts_SchoolId_PlanId_PlanRevision_RendererVersion] ON [DutyPdfArtifacts] ([SchoolId], [PlanId], [PlanRevision], [RendererVersion]) WHERE [PlanId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015060717_VersionDutyRosterRenderings'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261015060717_VersionDutyRosterRenderings', N'8.0.0');
END;
GO

COMMIT;
GO

