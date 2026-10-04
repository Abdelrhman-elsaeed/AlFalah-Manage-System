using Microsoft.EntityFrameworkCore.Migrations;

namespace AlFalah.Infrastructure.Data.Migrations;

public partial class SchoolFileStorageSelfEvaluation
{
    private static void AddSelfEvaluationIntegrity(MigrationBuilder m)
    {
        static void Trigger(MigrationBuilder migration, string sql) => migration.Sql("EXEC(N'" + sql.Replace("'", "''") + "');");
        foreach (var table in new[] { "SelfEvaluationTemplates", "SchoolEvaluationScopes", "ManualEvaluationRevisions", "RequirementFollowUpRevisions" })
            Trigger(m, $"CREATE TRIGGER TR_{table}_{(table is "SelfEvaluationTemplates" or "SchoolEvaluationScopes" ? "Immutable" : "AppendOnly")} ON {table} AFTER UPDATE, DELETE AS BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM deleted) THROW 51201, 'Self evaluation templates, scopes and revisions are immutable.', 1; END");
        Trigger(m, """
            CREATE TRIGGER TR_EvidenceRequirements_S4History ON EvidenceRequirements AFTER UPDATE, DELETE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id WHERE d.SourceKey IS NOT NULL AND i.Id IS NULL)
                    THROW 51202, 'Retain template requirements and their history.', 1;
                IF EXISTS(SELECT Id, SchoolId, AcademicYearId, TemplateVersion, SourceKey, SourceSHA256, ReferencePath, CompletionAction, CandidateTaskCodesJson, ImportanceReason, DisplayName, DomainCode, StandardCode FROM deleted WHERE SourceKey IS NOT NULL
                    EXCEPT SELECT Id, SchoolId, AcademicYearId, TemplateVersion, SourceKey, SourceSHA256, ReferencePath, CompletionAction, CandidateTaskCodesJson, ImportanceReason, DisplayName, DomainCode, StandardCode FROM inserted)
                    THROW 51203, 'Curated template requirement provenance is immutable.', 1;
            END
            """);
        Trigger(m, """
            CREATE TRIGGER TR_ManualEvaluations_History ON ManualEvaluations AFTER INSERT, UPDATE, DELETE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS(SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id=d.Id WHERE i.Id IS NULL)
                    THROW 51204, 'Retain manual evaluation history.', 1;
                IF EXISTS(SELECT Id, SchoolId, AcademicYearId, TemplateVersion, ScopeCode, CreatedAtUtc FROM deleted
                    EXCEPT SELECT Id, SchoolId, AcademicYearId, TemplateVersion, ScopeCode, CreatedAtUtc FROM inserted)
                    THROW 51205, 'Manual evaluation scope is immutable.', 1;
                IF EXISTS(SELECT 1 FROM inserted i LEFT JOIN deleted d ON i.Id=d.Id WHERE i.Revision <> ISNULL(d.Revision,0)+1
                    OR LTRIM(RTRIM(i.Reason))='' OR (LTRIM(RTRIM(i.Judgment))='' AND i.Value IS NULL))
                    THROW 51206, 'Manual evaluations require a reason and a new revision.', 1;
                IF EXISTS(SELECT 1 FROM inserted i WHERE NOT EXISTS(SELECT 1 FROM SchoolEvaluationScopes s
                    WHERE s.SchoolId=i.SchoolId AND s.AcademicYearId=i.AcademicYearId AND s.TemplateVersion=i.TemplateVersion))
                    THROW 51207, 'Manual evaluations require an initialized school year template.', 1;
            END
            """);
        Trigger(m, """
            CREATE TRIGGER TR_RequirementFollowUpRevisions_Scope ON RequirementFollowUpRevisions AFTER INSERT AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS(SELECT 1 FROM inserted i JOIN EvidenceRequirements r ON r.Id=i.RequirementId WHERE r.SchoolId<>i.SchoolId OR r.SchoolId IS NULL)
                    THROW 51208, 'Follow-up revision is outside requirement school.', 1;
            END
            """);
    }
}
