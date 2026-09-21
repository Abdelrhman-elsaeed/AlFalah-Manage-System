SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @LegacyRubricId int = (
    SELECT TOP (1) Id FROM RubricVersions
    WHERE VersionNumber <> 2 AND IsDeleted = 0
    ORDER BY VersionNumber DESC
);

IF @LegacyRubricId IS NULL
    THROW 51010, 'No retained legacy rubric is available for rollback.', 1;

UPDATE RubricVersions SET IsActive = CASE WHEN Id = @LegacyRubricId THEN 1 ELSE 0 END
WHERE IsDeleted = 0;

COMMIT TRANSACTION;
