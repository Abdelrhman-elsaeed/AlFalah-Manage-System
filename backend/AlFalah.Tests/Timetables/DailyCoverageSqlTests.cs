using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using Xunit;

namespace AlFalah.Tests.Timetables;

public sealed class CoverageSqlFactAttribute : FactAttribute
{
    public CoverageSqlFactAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ALFALAH_COVERAGE_TEST_CONNECTION"))) Skip = "Requires a fresh isolated AlFalahCoverageTests_<guid> LocalDB database."; }
}
public sealed class DailyCoverageSqlTests
{
    [CoverageSqlFact]
    public async Task Fresh_SQL_batch_is_atomic_replay_safe_and_retains_signed_versions_with_concurrency()
    {
        var cs = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ALFALAH_COVERAGE_TEST_CONNECTION"));
        if (!cs.DataSource.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase) || !cs.IntegratedSecurity ||
            !System.Text.RegularExpressions.Regex.IsMatch(cs.InitialCatalog, "^AlFalahCoverageTests_[a-f0-9]{32}$")) throw new InvalidOperationException("Fresh isolated integrated LocalDB required.");
        await using (var master = new SqlConnection(new SqlConnectionStringBuilder(cs.ConnectionString) { InitialCatalog = "master" }.ConnectionString))
        {
            await master.OpenAsync(); await using var command = master.CreateCommand(); command.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name=@name"; command.Parameters.AddWithValue("@name", cs.InitialCatalog);
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 0) throw new InvalidOperationException("An existing database is not an allowed test destination.");
        }
        AlFalahDbContext Db() => new(new DbContextOptionsBuilder<AlFalahDbContext>().UseSqlServer(cs.ConnectionString).Options);
        await using var db = Db(); await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
        (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        db.Users.AddRange(new() { Id = "manager", UserName = "coverage-manager", FirstName = "Synthetic", LastName = "Manager", IsActive = true },
            new() { Id = "teacher1", UserName = "coverage-original", FirstName = "Synthetic", LastName = "Original", IsActive = true },
            new() { Id = "teacher2", UserName = "coverage-replacement", FirstName = "Synthetic", LastName = "Replacement", IsActive = true });
        db.Add(new School { Name = "Synthetic coverage school", Stage = SchoolStage.Intermediate, City = "Test", ManagerUserId = "manager" });
        if (!await db.AcademicYears.AnyAsync(x => x.Id == 1)) db.Add(new AcademicYear { Code = "coverage-test", NameAr = "2026-2027", StartsOn = new(2026, 8, 1), EndsOn = new(2027, 7, 31), IsActive = true });
        await db.SaveChangesAsync(); var timing = await BellScheduleTestData.SeedAsync(db);
        var year = await db.AcademicYears.SingleAsync(x => x.Id == 1); year.StartsOn = new(2026, 8, 1); year.EndsOn = new(2027, 7, 31);
        var original = new InstructorProfile { SchoolId = 1, UserId = "teacher1", IsActive = true, SubjectSpecialization = "Math" };
        var substitute = new InstructorProfile { SchoolId = 1, UserId = "teacher2", IsActive = true, SubjectSpecialization = "Math" };
        db.AddRange(original, substitute); await db.SaveChangesAsync();
        var teachers = new[] { original, substitute }.Select(i => new TeacherTimetableProfile { SchoolId = 1, TimetableSetupProfileId = 1, InstructorProfileId = i.Id, BellScheduleRevisionId = timing.Id, MaximumWeeklyPeriods = 20, CreatedByUserId = "manager", UpdatedByUserId = "manager" }).ToArray();
        var classroom = new Classroom { SchoolId = 1, AcademicYearId = 1, Stage = SchoolStage.Intermediate, GradeLevel = 1, Section = "1", SectionNumber = 1, ClassLabel = "1/1", CreatedByUserId = "manager", UpdatedByUserId = "manager" };
        var subject = new SubjectDefinition { SchoolId = 1, Name = "Math", UpdatedByUserId = "manager" }; db.AddRange(teachers); db.AddRange(classroom, subject); await db.SaveChangesAsync();
        var requirement = new ClassSubjectRequirement { SchoolId = 1, TimetableSetupProfileId = 1, ClassroomId = classroom.Id, SubjectId = subject.Id, IndividualPeriodCount = 2, UpdatedByUserId = "manager" }; db.Add(requirement); await db.SaveChangesAsync();
        db.Add(new TeachingAssignment { SchoolId = 1, TimetableSetupProfileId = 1, ClassSubjectRequirementId = requirement.Id, UpdatedByUserId = "manager",
            Members = [new() { SchoolId = 1, TimetableSetupProfileId = 1, TeacherTimetableProfileId = teachers[0].Id, AllocatedPeriodCount = 2 }] });
        var timetable = new SchoolTimetable { SchoolId = 1, AcademicYearId = 1, Semester = TimetableSemester.First, TimetableSetupProfileId = 1,
            BellScheduleRevisionId = timing.Id, Title = "Synthetic coverage timetable", IsPublished = true, CreatedByUserId = "manager", UpdatedByUserId = "manager" };
        foreach (var p in new[] { 1, 2 })
        {
            timetable.Entries.Add(new() { SchoolId = 1, InstructorProfileId = original.Id, Day = TimetableDay.Sunday, Period = p, EntryType = TimetableEntryType.Lesson, ClassroomId = classroom.Id, ClassLabel = "1/1", SubjectId = subject.Id, Subject = "Math", ClassSubjectRequirementId = requirement.Id });
            timetable.Entries.Add(new() { SchoolId = 1, InstructorProfileId = substitute.Id, Day = TimetableDay.Sunday, Period = p, EntryType = TimetableEntryType.Standby });
        }
        db.Add(timetable); db.Add(new AttendanceRecord { SchoolId = 1, UserId = "teacher1", AttendanceDate = DailyCoverageTests.Date, Status = AttendanceStatus.Absent, RecordedByUserId = "manager" });
        db.Add(new UserSignature { UserId = "teacher2", SignatureDrawnData = DailyCoverageTests.Signature() });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var clock = new DailyCoverageTests.Clock(); var service = DailyCoverageTests.Services(db, clock).Coverage;
        var board = await service.RefreshAsync(DailyCoverageTests.Date, default); board.Needs.Should().HaveCount(2);
        var request = new CoveragePreviewRequest(DailyCoverageTests.Date, "Fair", [], clock.Now.AddMinutes(4));
        var preview = await service.PreviewAsync(request, default); preview.Assignments.Should().HaveCount(2);
        (await db.Set<TimetableSubstitution>().CountAsync()).Should().Be(0);
        // Fail the second ledger insert after the first assignment has saved. The outer batch must roll everything back.
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE TimetableSubstitutions ADD CONSTRAINT CK_CoverageAtomicityProbe CHECK (AfterRevision < 3);");
        var approval = new CoverageApproveRequest(Guid.NewGuid(), request, preview.ProposalId, "Synthetic reviewed warning override");
        await service.Invoking(s => s.ApproveAsync(approval, default)).Should().ThrowAsync<DbUpdateException>();
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE TimetableSubstitutions DROP CONSTRAINT CK_CoverageAtomicityProbe;"); db.ChangeTracker.Clear();
        (await db.Set<TimetableSubstitution>().CountAsync()).Should().Be(0); (await db.OutboxMessages.CountAsync()).Should().Be(0);
        (await db.SchoolTimetableVersions.CountAsync()).Should().Be(0);
        board = await service.ApproveAsync(approval, default); await service.ApproveAsync(approval, default);
        (await db.Set<TimetableSubstitution>().CountAsync()).Should().Be(2); (await db.OutboxMessages.CountAsync()).Should().Be(2);
        db.ChangeTracker.Clear(); var n = board.Needs.First();
        await using (var second = Db()) { var row = await second.Set<DailyCoverageNeed>().SingleAsync(x => x.Id == n.Id); row.Revision++; await second.SaveChangesAsync(); }
        var teacher = DailyCoverageTests.Services(db, clock, DailyCoverageTests.Teacher).Execution;
        await teacher.Invoking(s => s.ActAsync(n.Id, "Acknowledgement", new(Guid.NewGuid(), n.Revision, n.SubstitutionId!.Value), default)).Should().ThrowAsync<BellScheduleConflictException>();
        var detail = await teacher.DetailAsync(n.Id, default);
        detail = await teacher.ActAsync(n.Id, "Acknowledgement", new(Guid.NewGuid(), detail.Need.Revision, n.SubstitutionId!.Value), default);
        detail = await teacher.SaveDraftAsync(n.Id, new(detail.Need.Revision, n.SubstitutionId.Value, "Synthetic coverage execution", ""), default);
        var submit = new CoverageActionRequest(Guid.NewGuid(), detail.Need.Revision, n.SubstitutionId.Value);
        await teacher.ActAsync(n.Id, "Execution", submit, default); await teacher.ActAsync(n.Id, "Execution", submit, default);
        db.ChangeTracker.Clear();
        var saved = await teacher.DetailAsync(n.Id, default); saved.Need.State.Should().Be("Executed");
        saved.History.Count(r => r.Kind == "Execution").Should().Be(1); saved.History.Count(r => r.Kind == "Acknowledgement").Should().Be(1);
        saved.History.Where(r => r.Kind is "Execution" or "Acknowledgement").Should().OnlyContain(r => r.SignatureData != null && r.SignatureHash != null);
    }
}
