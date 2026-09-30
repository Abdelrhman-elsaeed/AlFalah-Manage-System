using System.Text.Json;
using AlFalah.Application.IntelligentTimetable;
using AlFalah.Application.IntelligentTimetable.DTOs;
using AlFalah.Application.StudentAffairs.DTOs.Messaging;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Xunit;

namespace AlFalah.Tests.Infrastructure;

public sealed class SocialWorkerCrmRepositoryQueryTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Case_messaging_queries_execute_relationally_and_enforce_scope_paging_and_confidential_projection()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new SqliteAlFalahDbContext(options);
        await CreateSchemaAsync(db);
        await SeedAsync(db);

        var schedules = new RejectingBellScheduleRepository();
        var repository = new MessagingWorkflowRepository(db, schedules, TimeProvider.System);
        var request = new CreateConversationRequestDto(
            17,
            ConversationThreadType.GuardianSocialWorker,
            null,
            null,
            null,
            "Case follow-up",
            "Confidential",
            "case-key",
            100,
            9);

        (await repository.IsConversationTargetAllowedAsync(42, "worker", request, Now, CancellationToken.None))
            .Should().BeTrue();
        (await repository.IsConversationTargetAllowedAsync(42, "other-worker", request, Now, CancellationToken.None))
            .Should().BeFalse("another social worker is not assigned to the referral");
        (await repository.IsConversationTargetAllowedAsync(43, "worker", request, Now, CancellationToken.None))
            .Should().BeFalse("cross-school case identifiers must remain hidden");
        schedules.PublishedCandidateCalls.Should().Be(0, "social-worker messages are not teacher-office-hour constrained");

        var first = await repository.GetConversationsAsync(
            42,
            "worker",
            new ConversationListQuery { Search = "Case", PageNumber = 1, PageSize = 1 },
            CancellationToken.None);
        var second = await repository.GetConversationsAsync(
            42,
            "worker",
            new ConversationListQuery { Search = "Case", PageNumber = 2, PageSize = 1 },
            CancellationToken.None);
        var unread = await repository.GetConversationsAsync(
            42,
            "worker",
            new ConversationListQuery { IsUnread = true, PageNumber = 1, PageSize = 10 },
            CancellationToken.None);

        first.TotalCount.Should().Be(2);
        first.Items.Select(item => item.Id).Should().Equal(3);
        second.Items.Select(item => item.Id).Should().Equal(2);
        unread.Items.Select(item => item.Id).Should().Equal(3);
        first.Items.Should().OnlyContain(item => item.ThreadType == ConversationThreadType.GuardianSocialWorker);
        JsonSerializer.Serialize(first.Items).Should().NotContain("highly confidential body");
    }

    private static async Task CreateSchemaAsync(AlFalahDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE "Users" (
                "Id" TEXT NOT NULL PRIMARY KEY,
                "AccessFailedCount" INTEGER NOT NULL DEFAULT 0,
                "ConcurrencyStamp" TEXT NULL,
                "CreatedAt" TEXT NULL,
                "DeletedAt" TEXT NULL,
                "DeletedByUserId" TEXT NULL,
                "Email" TEXT NULL,
                "EmailConfirmed" INTEGER NOT NULL DEFAULT 0,
                "FirstName" TEXT NOT NULL,
                "LastName" TEXT NOT NULL,
                "IsActive" INTEGER NOT NULL,
                "IsDeleted" INTEGER NOT NULL,
                "LastLoginAt" TEXT NULL,
                "LockoutEnabled" INTEGER NOT NULL DEFAULT 0,
                "LockoutEnd" TEXT NULL,
                "NormalizedEmail" TEXT NULL,
                "NormalizedUserName" TEXT NULL,
                "PasswordHash" TEXT NULL,
                "PhoneNumber" TEXT NULL,
                "PhoneNumberConfirmed" INTEGER NOT NULL DEFAULT 0,
                "PreferredLanguage" TEXT NOT NULL DEFAULT 'ar',
                "SecurityStamp" TEXT NULL,
                "TwoFactorEnabled" INTEGER NOT NULL DEFAULT 0,
                "UpdatedAt" TEXT NULL,
                "UserName" TEXT NULL
            );
            CREATE TABLE "Roles" (
                "Id" TEXT NOT NULL PRIMARY KEY,
                "Name" TEXT NULL
            );
            CREATE TABLE "UserSchoolRoles" (
                "Id" INTEGER NOT NULL PRIMARY KEY,
                "UserId" TEXT NOT NULL,
                "SchoolId" INTEGER NOT NULL,
                "RoleId" TEXT NOT NULL,
                "IsActive" INTEGER NOT NULL,
                "IsDeleted" INTEGER NOT NULL
            );
            CREATE TABLE "StudentReferrals" (
                "Id" INTEGER NOT NULL,
                "SchoolId" INTEGER NOT NULL,
                "StudentId" INTEGER NOT NULL,
                "AssignedSocialWorkerUserId" TEXT NULL,
                "Status" INTEGER NOT NULL,
                "IsDeleted" INTEGER NOT NULL,
                PRIMARY KEY ("Id"),
                UNIQUE ("SchoolId", "Id")
            );
            CREATE TABLE "GuardianProfiles" (
                "Id" INTEGER NOT NULL,
                "SchoolId" INTEGER NOT NULL,
                "ApplicationUserId" TEXT NOT NULL,
                "IsActive" INTEGER NOT NULL,
                "IsDeleted" INTEGER NOT NULL,
                PRIMARY KEY ("Id"),
                UNIQUE ("SchoolId", "Id")
            );
            CREATE TABLE "StudentGuardians" (
                "Id" INTEGER NOT NULL PRIMARY KEY,
                "SchoolId" INTEGER NOT NULL,
                "StudentId" INTEGER NOT NULL,
                "GuardianProfileId" INTEGER NOT NULL,
                "ValidFrom" TEXT NOT NULL,
                "ValidTo" TEXT NULL,
                "IsDeleted" INTEGER NOT NULL
            );
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
            CREATE TABLE "ConversationThreads" (
                "Id" INTEGER NOT NULL,
                "SchoolId" INTEGER NOT NULL,
                "StudentId" INTEGER NULL,
                "StudentReferralId" INTEGER NULL,
                "ThreadType" INTEGER NOT NULL,
                "Subject" TEXT NOT NULL,
                "Status" INTEGER NOT NULL,
                "UpdatedAt" INTEGER NOT NULL,
                "RowVersion" BLOB NOT NULL,
                "IsDeleted" INTEGER NOT NULL,
                PRIMARY KEY ("Id"),
                UNIQUE ("SchoolId", "Id")
            );
            CREATE TABLE "ConversationParticipants" (
                "Id" INTEGER NOT NULL PRIMARY KEY,
                "SchoolId" INTEGER NOT NULL,
                "ConversationThreadId" INTEGER NOT NULL,
                "ApplicationUserId" TEXT NOT NULL,
                "ParticipantRoleSnapshot" TEXT NOT NULL,
                "IsDeleted" INTEGER NOT NULL
            );
            CREATE TABLE "ConversationMessages" (
                "Id" INTEGER NOT NULL,
                "SchoolId" INTEGER NOT NULL,
                "ConversationThreadId" INTEGER NOT NULL,
                "SenderUserId" TEXT NOT NULL,
                "Body" TEXT NOT NULL,
                "IsDeleted" INTEGER NOT NULL,
                PRIMARY KEY ("Id"),
                UNIQUE ("SchoolId", "Id")
            );
            CREATE TABLE "MessageReceipts" (
                "Id" INTEGER NOT NULL PRIMARY KEY,
                "SchoolId" INTEGER NOT NULL,
                "ConversationMessageId" INTEGER NOT NULL,
                "RecipientUserId" TEXT NOT NULL,
                "DeliveryState" INTEGER NOT NULL,
                "ReadAt" TEXT NULL
            );
            """);
    }

    private static async Task SeedAsync(AlFalahDbContext db)
    {
        var updated = Now.UtcTicks;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Users" ("Id", "FirstName", "LastName", "IsActive", "IsDeleted") VALUES ('worker', 'Social', 'Worker', 1, 0);
            INSERT INTO "Users" ("Id", "FirstName", "LastName", "IsActive", "IsDeleted") VALUES ('other-worker', 'Other', 'Worker', 1, 0);
            INSERT INTO "Users" ("Id", "FirstName", "LastName", "IsActive", "IsDeleted") VALUES ('guardian', 'Case', 'Guardian', 1, 0);
            INSERT INTO "Roles" VALUES ('social-worker-role', 'SocialWorker');
            INSERT INTO "UserSchoolRoles" VALUES (1, 'worker', 42, 'social-worker-role', 1, 0);
            INSERT INTO "StudentReferrals" VALUES (100, 42, 17, 'worker', 3, 0);
            INSERT INTO "StudentReferrals" VALUES (101, 43, 17, 'worker', 3, 0);
            INSERT INTO "GuardianProfiles" VALUES (9, 42, 'guardian', 1, 0);
            INSERT INTO "StudentGuardians" VALUES (1, 42, 17, 9, '2026-01-01', NULL, 0);
            INSERT INTO "Students" VALUES (17, 42, 'S-17', 'Student', NULL, 'One', 1, 0);
            INSERT INTO "Students" VALUES (18, 43, 'S-18', 'Foreign', NULL, 'Student', 1, 0);

            INSERT INTO "ConversationThreads" VALUES (2, 42, 17, 100, 3, 'Case Alpha', 1, {updated}, X'02', 0);
            INSERT INTO "ConversationThreads" VALUES (3, 42, 17, 100, 3, 'Case Beta', 1, {updated}, X'03', 0);
            INSERT INTO "ConversationThreads" VALUES (4, 43, 18, 101, 3, 'Case Foreign', 1, {updated}, X'04', 0);
            INSERT INTO "ConversationThreads" VALUES (5, 42, 17, 100, 3, 'Case Other Worker', 1, {updated}, X'05', 0);

            INSERT INTO "ConversationParticipants" VALUES (20, 42, 2, 'worker', 'SocialWorker', 0);
            INSERT INTO "ConversationParticipants" VALUES (21, 42, 2, 'guardian', 'Guardian', 0);
            INSERT INTO "ConversationParticipants" VALUES (30, 42, 3, 'worker', 'SocialWorker', 0);
            INSERT INTO "ConversationParticipants" VALUES (31, 42, 3, 'guardian', 'Guardian', 0);
            INSERT INTO "ConversationParticipants" VALUES (40, 43, 4, 'worker', 'SocialWorker', 0);
            INSERT INTO "ConversationParticipants" VALUES (50, 42, 5, 'other-worker', 'SocialWorker', 0);

            INSERT INTO "ConversationMessages" VALUES (300, 42, 3, 'guardian', 'highly confidential body', 0);
            INSERT INTO "MessageReceipts" VALUES (3000, 42, 300, 'worker', 2, NULL);
            """);
    }

    private sealed class SqliteAlFalahDbContext(DbContextOptions<AlFalahDbContext> options)
        : AlFalahDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            var converter = new ValueConverter<DateTimeOffset, long>(
                value => value.UtcTicks,
                value => new DateTimeOffset(value, TimeSpan.Zero));
            builder.Entity<ConversationThread>().Property(entity => entity.UpdatedAt).HasConversion(converter);
        }
    }

    private sealed class RejectingBellScheduleRepository : IBellScheduleRepository
    {
        public int PublishedCandidateCalls { get; private set; }

        public Task<IReadOnlyList<PublishedBellScheduleCandidate>> GetPublishedCandidatesAsync(
            int schoolId, DateTimeOffset instant, CancellationToken ct)
        {
            PublishedCandidateCalls++;
            throw new InvalidOperationException("Teacher schedule lookup must not run for case messaging");
        }

        public Task<IReadOnlyList<BellScheduleDto>> ListAsync(int schoolId, int yearId, TimetableSemester semester, CancellationToken ct) => throw new NotSupportedException();
        public Task<BellScheduleTemplate?> FindAsync(int schoolId, int id, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> NameExistsAsync(int schoolId, SaveBellScheduleRequest request, int? exceptId, CancellationToken ct) => throw new NotSupportedException();
        public Task<BellScheduleDependencies> GetDependenciesAsync(int schoolId, int? templateId, int? profileId, CancellationToken ct) => throw new NotSupportedException();
        public Task SaveRevisionAsync(BellScheduleTemplate template, BellScheduleRevision revision, bool isNew, CancellationToken ct) => throw new NotSupportedException();
        public Task SelectAsync(TimetableSetupProfile profile, BellScheduleTemplate template, CancellationToken ct) => throw new NotSupportedException();
        public Task<BellScheduleDto?> GetRevisionAsync(int schoolId, int revisionId, CancellationToken ct) => throw new NotSupportedException();
        public Task<BellScheduleDto?> GetSelectedAsync(int schoolId, int yearId, TimetableSemester semester, int? profileId, CancellationToken ct) => throw new NotSupportedException();
        public Task<BellScheduleDto?> GetPublishedAsync(int schoolId, DateTimeOffset instant, CancellationToken ct) => throw new NotSupportedException();
    }
}
