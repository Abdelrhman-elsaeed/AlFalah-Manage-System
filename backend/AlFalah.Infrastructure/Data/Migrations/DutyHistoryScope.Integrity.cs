using Microsoft.EntityFrameworkCore.Migrations;

namespace AlFalah.Infrastructure.Data.Migrations;

public partial class PreserveDutyHistoryAcrossTeacherTransfers
{
    private static void ProtectTeacherScope(MigrationBuilder migration)
    {
        migration.Sql("""
            CREATE TRIGGER TR_DutyAssignments_IdentityScope ON DutyAssignments AFTER INSERT, UPDATE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (
                    SELECT 1 FROM inserted i LEFT JOIN deleted d ON i.Id=d.Id
                    WHERE (d.Id IS NULL AND NOT EXISTS (
                        SELECT 1 FROM InstructorProfiles p WHERE p.UserId=i.OriginalTeacherId
                            AND p.SchoolId=i.SchoolId AND p.IsDeleted=0))
                    OR ((d.Id IS NULL OR i.EffectiveTeacherId<>d.EffectiveTeacherId) AND NOT EXISTS (
                        SELECT 1 FROM InstructorProfiles p WHERE p.UserId=i.EffectiveTeacherId
                            AND p.SchoolId=i.SchoolId AND p.IsDeleted=0))
                    OR (d.Id IS NOT NULL AND (i.OriginalTeacherId<>d.OriginalTeacherId
                        OR i.SchoolId<>d.SchoolId OR i.DutyPlanId<>d.DutyPlanId OR i.Date<>d.Date)))
                    THROW 51003, 'Duty identity is immutable; a new effective teacher must belong to the assignment school', 1;
            END;
            """);
        migration.Sql("""
            CREATE TRIGGER TR_DutyParticipants_IdentityScope ON DutyParticipants AFTER INSERT, UPDATE AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (
                    SELECT 1 FROM inserted i LEFT JOIN deleted d ON i.Id=d.Id
                    WHERE (d.Id IS NULL OR i.InstructorProfileId<>d.InstructorProfileId
                        OR i.UserId<>d.UserId OR i.SchoolId<>d.SchoolId OR i.DutyPlanId<>d.DutyPlanId)
                    AND NOT EXISTS (SELECT 1 FROM InstructorProfiles p
                        WHERE p.Id=i.InstructorProfileId AND p.UserId=i.UserId
                            AND p.SchoolId=i.SchoolId AND p.IsDeleted=0))
                    THROW 51004, 'Duty participant identity must belong to its school when selected', 1;
            END;
            """);
    }
    private static void RemoveScopeProtection(MigrationBuilder migration)
    {
        migration.Sql("DROP TRIGGER IF EXISTS TR_DutyAssignments_IdentityScope;");
        migration.Sql("DROP TRIGGER IF EXISTS TR_DutyParticipants_IdentityScope;");
    }
}
