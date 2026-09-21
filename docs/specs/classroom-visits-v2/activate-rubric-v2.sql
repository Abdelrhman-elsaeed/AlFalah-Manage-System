SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM RubricVersions WHERE VersionNumber = 2 AND IsDeleted = 0)
    THROW 51000, 'Rubric V2 is missing. Run the application seeder before cutover.', 1;

IF (SELECT COUNT(*) FROM RubricDomains d JOIN RubricVersions v ON v.Id = d.RubricVersionId WHERE v.VersionNumber = 2 AND d.IsDeleted = 0) <> 5
    THROW 51001, 'Rubric V2 domain count is not 5.', 1;

IF (SELECT COUNT(*) FROM RubricStandards s JOIN RubricDomains d ON d.Id = s.RubricDomainId JOIN RubricVersions v ON v.Id = d.RubricVersionId WHERE v.VersionNumber = 2 AND s.IsDeleted = 0) <> 25
    THROW 51002, 'Rubric V2 standard count is not 25.', 1;

IF (SELECT COUNT(*) FROM RubricIndicators i JOIN RubricStandards s ON s.Id = i.RubricStandardId JOIN RubricDomains d ON d.Id = s.RubricDomainId JOIN RubricVersions v ON v.Id = d.RubricVersionId WHERE v.VersionNumber = 2 AND i.IsDeleted = 0) <> 66
    THROW 51003, 'Rubric V2 indicator count is not 66.', 1;

UPDATE RubricVersions SET IsActive = 0 WHERE VersionNumber <> 2 AND IsActive = 1 AND IsDeleted = 0;
UPDATE RubricVersions SET IsActive = 1 WHERE VersionNumber = 2 AND IsDeleted = 0;

COMMIT TRANSACTION;
