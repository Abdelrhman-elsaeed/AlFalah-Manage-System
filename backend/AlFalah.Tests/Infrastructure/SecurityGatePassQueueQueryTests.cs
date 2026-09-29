using AlFalah.Application.StudentAffairs.DTOs.GatePasses;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Xunit;

namespace AlFalah.Tests.Infrastructure;

public sealed class SecurityGatePassQueueQueryTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Queue_executes_relationally_and_enforces_scope_states_paging_and_stable_order()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new SqliteAlFalahDbContext(options);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE "Students" (
                "Id" INTEGER NOT NULL,
                "SchoolId" INTEGER NOT NULL,
                "StudentNumber" TEXT NOT NULL,
                "FirstName" TEXT NOT NULL,
                "MiddleName" TEXT NULL,
                "LastName" TEXT NOT NULL,
                "IsActive" INTEGER NOT NULL,
                "IsDeleted" INTEGER NOT NULL,
                PRIMARY KEY ("Id"),
                UNIQUE ("SchoolId", "Id")
            );
            CREATE TABLE "Classrooms" (
                "Id" INTEGER NOT NULL,
                "SchoolId" INTEGER NOT NULL,
                "ClassLabel" TEXT NOT NULL,
                "IsDeleted" INTEGER NOT NULL,
                PRIMARY KEY ("Id"),
                UNIQUE ("SchoolId", "Id")
            );
            CREATE TABLE "Users" (
                "Id" TEXT NOT NULL PRIMARY KEY,
                "FirstName" TEXT NOT NULL,
                "LastName" TEXT NOT NULL,
                "IsDeleted" INTEGER NOT NULL
            );
            CREATE TABLE "GatePasses" (
                "Id" INTEGER NOT NULL PRIMARY KEY,
                "SchoolId" INTEGER NOT NULL,
                "StudentId" INTEGER NOT NULL,
                "CurrentClassroomId" INTEGER NULL,
                "ApprovedWindowStartsAt" INTEGER NULL,
                "ApprovedWindowEndsAt" INTEGER NULL,
                "ReviewedAt" INTEGER NULL,
                "SecurityAcknowledgedAt" INTEGER NULL,
                "PickupPersonName" TEXT NOT NULL,
                "PickupRelationship" TEXT NULL,
                "PickupIdentityHint" TEXT NULL,
                "Status" INTEGER NOT NULL,
                "RowVersion" BLOB NOT NULL,
                "ReviewedByUserId" TEXT NULL,
                "IsDeleted" INTEGER NOT NULL
            );
            INSERT INTO "Students" VALUES (1, 42, 'S-1', 'أحمد', NULL, 'علي', 1, 0);
            INSERT INTO "Students" VALUES (2, 43, 'S-2', 'طالب', NULL, 'آخر', 1, 0);
            INSERT INTO "Classrooms" VALUES (10, 42, '1/A', 0);
            INSERT INTO "Classrooms" VALUES (20, 43, '2/A', 0);
            INSERT INTO "Users" VALUES ('officer', 'Officer', 'One', 0);
            """);

        await InsertGatePass(db, 1, 42, 1, 10, GatePassStatus.Approved, Now.AddMinutes(-5), Now.AddMinutes(30), Now.AddMinutes(-2));
        await InsertGatePass(db, 6, 42, 1, 10, GatePassStatus.Approved, Now.AddMinutes(-4), Now.AddMinutes(30), Now.AddMinutes(-1));
        await InsertGatePass(db, 3, 42, 1, 10, GatePassStatus.Approved, Now.AddMinutes(-30), Now.AddMinutes(-1), Now.AddMinutes(-4));
        await InsertGatePass(db, 2, 42, 1, 10, GatePassStatus.SecurityAcknowledged, Now.AddMinutes(10), Now.AddMinutes(40), Now.AddMinutes(-3));
        await InsertGatePass(db, 4, 43, 2, 20, GatePassStatus.Approved, Now.AddMinutes(-5), Now.AddMinutes(30), Now.AddMinutes(-2));
        await InsertGatePass(db, 5, 42, 1, 10, GatePassStatus.Requested, Now.AddMinutes(-5), Now.AddMinutes(30), Now.AddMinutes(-2));
        await InsertGatePass(db, 8, 42, 1, 10, GatePassStatus.Approved, Now.AddMinutes(-5), Now.AddMinutes(30), null);
        await InsertGatePass(db, 9, 43, 2, 20, GatePassStatus.SecurityAcknowledged, Now.AddMinutes(-5), Now.AddMinutes(30), Now.AddMinutes(-2));

        var repository = new GatePassWorkflowRepository(db, null!);
        var first = await repository.GetSecurityGatePassQueueAsync(
            42,
            new GatePassListQuery
            {
                PageNumber = 1,
                PageSize = 2,
                Status = GatePassStatus.Requested,
                ClassroomId = 999,
                Date = new DateOnly(1999, 1, 1)
            },
            Now,
            CancellationToken.None);
        var second = await repository.GetSecurityGatePassQueueAsync(
            42,
            new GatePassListQuery { PageNumber = 2, PageSize = 2 },
            Now,
            CancellationToken.None);

        first.TotalCount.Should().Be(4);
        first.Items.Select(item => item.Id).Should().Equal(1, 6);
        second.Items.Select(item => item.Id).Should().Equal(3, 2);
        first.Items.Should().OnlyContain(item =>
            item.Status == GatePassStatus.Approved
            || item.Status == GatePassStatus.SecurityAcknowledged);
        first.ServerNow.Should().Be(Now);

        var dashboard = await repository.GetSecurityDashboardAsync(42, Now, CancellationToken.None);
        dashboard.ApprovedGatePasses.Select(item => item.Id).Should().Equal(1, 6, 3, 2);
        dashboard.Counts.Should().Contain(item => item.Code == "Approved" && item.Count == 3);
        dashboard.Counts.Should().Contain(item => item.Code == "SecurityAcknowledged" && item.Count == 1);
        dashboard.Counts.Should().Contain(item => item.Code == "ActiveWindow" && item.Count == 2);
        dashboard.Counts.Should().Contain(item => item.Code == "OverdueAcknowledged" && item.Count == 0);
        dashboard.GeneratedAt.Should().Be(Now);
    }

    private static Task<int> InsertGatePass(
        AlFalahDbContext db,
        int id,
        int schoolId,
        int studentId,
        int classroomId,
        GatePassStatus status,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        DateTimeOffset? reviewedAt)
    {
        var pickup = "Pickup";
        var relationship = "Father";
        var identityHint = "ID";
        var officer = "officer";
        return db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "GatePasses"
                ("Id", "SchoolId", "StudentId", "CurrentClassroomId", "ApprovedWindowStartsAt",
                 "ApprovedWindowEndsAt", "ReviewedAt", "SecurityAcknowledgedAt", "PickupPersonName",
                 "PickupRelationship", "PickupIdentityHint", "Status", "RowVersion", "ReviewedByUserId", "IsDeleted")
            VALUES
                ({id}, {schoolId}, {studentId}, {classroomId}, {startsAt.UtcTicks}, {endsAt.UtcTicks}, {reviewedAt?.UtcTicks}, NULL,
                 {pickup}, {relationship}, {identityHint}, {(int)status}, {new byte[] { (byte)id }}, {officer}, 0)
            """);
    }

    private sealed class SqliteAlFalahDbContext(DbContextOptions<AlFalahDbContext> options)
        : AlFalahDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            var gatePass = builder.Entity<GatePass>();
            var converter = new ValueConverter<DateTimeOffset?, long?>(
                value => value.HasValue ? value.Value.UtcTicks : (long?)null,
                value => value.HasValue ? new DateTimeOffset(value.Value, TimeSpan.Zero) : null);
            gatePass.Property(entity => entity.ApprovedWindowStartsAt).HasConversion(converter);
            gatePass.Property(entity => entity.ApprovedWindowEndsAt).HasConversion(converter);
            gatePass.Property(entity => entity.ReviewedAt).HasConversion(converter);
            gatePass.Property(entity => entity.SecurityAcknowledgedAt).HasConversion(converter);
        }
    }
}
