using AlFalah.Application.StudentAffairs.Attendance;
using AlFalah.Application.StudentAffairs.Biometrics;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Domain.Events;
using AlFalah.Infrastructure.Automations;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Integrations.Biometrics;
using AlFalah.Infrastructure.Integrations.Noor;
using AlFalah.Infrastructure.Notifications;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AlFalah.Tests.StudentAffairs;

public sealed class Phase5AutomationsAndIntegrationsTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ZajelReader_ParsesApprovedArabicSchema()
    {
        using var source = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.Worksheets.Add("تقرير الحضور بالبصمة");
            sheet.Cell(1, 1).Value = "رقم الهوية";
            sheet.Cell(1, 2).Value = "الاسم";
            sheet.Cell(1, 3).Value = "الجهاز";
            sheet.Cell(1, 4).Value = "تاريخ ووقت الحضور";
            sheet.Cell(1, 5).Value = "حالة الحضور";
            sheet.Cell(2, 1).Value = "0123456789";
            sheet.Cell(2, 4).Value = new DateTime(2026, 8, 31, 6, 54, 13);
            sheet.Cell(2, 5).Value = "متأخر";
            workbook.SaveAs(source);
        }
        source.Position = 0;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["StudentAffairsIntegrations:SchoolTimeZoneId"] = "Egypt Standard Time"
            }).Build();

        var rows = await new ZajelBiometricWorkbookReader(configuration)
            .ReadAsync(source, CancellationToken.None);

        rows.Should().ContainSingle();
        rows[0].NationalId.Should().Be("0123456789");
        rows[0].Status.Should().Be("متأخر");
        rows[0].SchoolLocalTime.Should().Be(new TimeOnly(6, 54, 13));
    }

    [Fact]
    public void NoorWriter_WritesRequiredColumnsAndKeepsNationalIdAsText()
    {
        var bytes = new NoorWorkbookWriter().Write(new[]
        {
            new NoorWorkbookRow("أحمد محمد", "0123456789", new DateOnly(2026, 8, 31), "Accepted")
        });

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheet(1);
        sheet.Cell(1, 1).GetString().Should().Be("Student Name");
        sheet.Cell(1, 2).GetString().Should().Be("National ID");
        sheet.Cell(1, 3).GetString().Should().Be("Date");
        sheet.Cell(1, 4).GetString().Should().Be("Excuse Status");
        sheet.Cell(2, 2).GetString().Should().Be("0123456789");
    }

    [Fact]
    public async Task BehaviorRule_TenthIncident_CreatesOneLedgerReferralAndPendingSummon()
    {
        await using var context = CreateContext();
        await SeedAutomationContextAsync(context);
        for (var index = 1; index <= 10; index++)
            context.BehaviorIncidents.Add(NewBehavior(index));
        await context.SaveChangesAsync();
        var domainEvent = new BehaviorIncidentLoggedEvent(
            Guid.NewGuid(), 10, 10, 1, 20, 30, 1, 1, 1, "conduct",
            BehaviorSeverity.Medium, Now, null, "officer", GuardianDispatchDecision.PendingOfficerDecision, Now);

        var engine = new StudentAffairsAutomationRuleEngine(context, new FixedTimeProvider(Now));
        await engine.ProcessAsync(domainEvent, CancellationToken.None);
        await context.SaveChangesAsync();

        (await context.StudentTermMetrics.SingleAsync()).Count.Should().Be(10);
        (await context.AutomationTriggerLedgers.CountAsync()).Should().Be(1);
        (await context.StudentReferrals.CountAsync()).Should().Be(1);
        var summon = await context.GuardianSummons.SingleAsync();
        summon.Status.Should().Be(GuardianSummonStatus.Pending);

        await engine.ProcessAsync(domainEvent, CancellationToken.None);
        await context.SaveChangesAsync();
        (await context.StudentReferrals.CountAsync()).Should().Be(1);
        (await context.GuardianSummons.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task NotificationPolicy_ImmediateIsDelivered_BehaviorRequiresApproval()
    {
        await using var context = CreateContext();
        await SeedAutomationContextAsync(context);
        var dispatcher = new StudentAffairsNotificationDispatcher(context, new FixedTimeProvider(Now));
        await dispatcher.ProcessAsync(new StudentAbsentRecordedEvent(
            Guid.NewGuid(), 50, 10, 1, 20, 30, new DateOnly(2026, 8, 31), "officer", Now, Now),
            CancellationToken.None);
        await dispatcher.ProcessAsync(new BehaviorIncidentLoggedEvent(
            Guid.NewGuid(), 60, 10, 1, 20, 30, 1, 1, 1, "conduct", BehaviorSeverity.Medium,
            Now, null, "officer", GuardianDispatchDecision.PendingOfficerDecision, Now),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var notifications = await context.Notifications.OrderBy(item => item.Id).ToListAsync();
        notifications.Should().HaveCount(2);
        notifications.Single(item => item.RelatedEntityType == nameof(DailyStudentAttendance))
            .DeliveryStatus.Should().Be(NotificationDeliveryStatus.Delivered);
        var pending = notifications.Single(item => item.RelatedEntityType == nameof(BehaviorIncident));
        pending.RequiresApproval.Should().BeTrue();
        pending.DeliveredAt.Should().BeNull();
    }

    [Fact]
    public async Task AbsenceRule_UsesDistinctUnexcusedDays_AndTriggersThreeFiveTenActions()
    {
        await using var context = CreateContext();
        await SeedAutomationContextAsync(context);
        for (var day = 1; day <= 10; day++)
            context.DailyStudentAttendances.Add(NewAttendance(day, StudentAttendanceStatus.Absent));
        context.DailyStudentAttendances.Add(NewAttendance(20, StudentAttendanceStatus.AbsentExcused));
        await context.SaveChangesAsync();

        var engine = new StudentAffairsAutomationRuleEngine(context, new FixedTimeProvider(Now));
        await engine.ProcessAsync(new StudentAbsentRecordedEvent(
            Guid.NewGuid(), 10, 10, 1, 20, 30, new DateOnly(2026, 8, 10), "officer", Now, Now),
            CancellationToken.None);
        await context.SaveChangesAsync();

        (await context.StudentTermMetrics.SingleAsync()).Count.Should().Be(10);
        (await context.AutomationTriggerLedgers.CountAsync()).Should().Be(3);
        (await context.StudentReferrals.CountAsync()).Should().Be(2);
        (await context.GuardianSummons.CountAsync()).Should().Be(2);
        (await context.StudentCaseActions.SingleAsync()).ActionType
            .Should().Be(StudentCaseActionType.ChildRightsCommitteeReferral);
        (await context.Notifications.SingleAsync()).UserId.Should().Be("officer");
    }

    [Fact]
    public async Task MorningDelayRule_TenthOccurrence_CreatesReferralAndSummonOnce()
    {
        await using var context = CreateContext();
        await SeedAutomationContextAsync(context);
        for (var day = 1; day <= 10; day++)
            context.MorningArrivalDelays.Add(new MorningArrivalDelay
            {
                Id = day, SchoolId = 1, StudentId = 10, AcademicTermId = 20,
                ArrivalAt = Now.AddDays(day), SchoolLocalDate = new DateOnly(2026, 8, day),
                CutoffTimeSnapshot = new TimeOnly(6, 30), DelayMinutes = 10,
                CreatedByUserId = "officer", UpdatedByUserId = "officer"
            });
        await context.SaveChangesAsync();

        var domainEvent = new MUaCqczw28YRmuXBYNYtWgMhWwXe7qmYC3(
            Guid.NewGuid(), 10, 10, 1, 20, Now, new DateOnly(2026, 8, 10),
            new TimeOnly(6, 30), 10, "ImmediateGuardian", Now);
        var engine = new StudentAffairsAutomationRuleEngine(context, new FixedTimeProvider(Now));
        await engine.ProcessAsync(domainEvent, CancellationToken.None);
        await context.SaveChangesAsync();
        await engine.ProcessAsync(domainEvent, CancellationToken.None);
        await context.SaveChangesAsync();

        (await context.StudentTermMetrics.SingleAsync()).Count.Should().Be(10);
        (await context.AutomationTriggerLedgers.CountAsync()).Should().Be(1);
        (await context.StudentReferrals.CountAsync()).Should().Be(1);
        (await context.GuardianSummons.CountAsync()).Should().Be(1);
    }

    private static AlFalahDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseInMemoryDatabase($"phase5-{Guid.NewGuid()}").Options);

    private static async Task SeedAutomationContextAsync(AlFalahDbContext context)
    {
        var school = new School { Id = 1, Name = "School", City = "Makkah", Stage = SchoolStage.Intermediate };
        var officer = new ApplicationUser { Id = "officer", UserName = "officer", FirstName = "Officer", IsActive = true };
        var guardianUser = new ApplicationUser { Id = "guardian", UserName = "guardian", FirstName = "Guardian", IsActive = true };
        var role = new ApplicationRole { Id = "officer-role", Name = RoleNames.StudentAffairsOfficer, NormalizedName = RoleNames.StudentAffairsOfficer.ToUpperInvariant() };
        context.AddRange(school, officer, guardianUser, role);
        context.UserSchoolRoles.Add(new UserSchoolRole { SchoolId = 1, UserId = officer.Id, RoleId = role.Id, IsActive = true });
        context.Students.Add(new Student
        {
            Id = 10, SchoolId = 1, StudentNumber = "S10", IdentityNumber = "1000000010", FirstName = "Student", LastName = "Ten",
            IsActive = true, CreatedByUserId = officer.Id, UpdatedByUserId = officer.Id
        });
        context.AcademicTerms.Add(new AcademicTerm
        {
            Id = 20, SchoolId = 1, AcademicYearId = 1, Semester = TimetableSemester.First,
            StartsOn = new DateOnly(2026, 8, 1), EndsOn = new DateOnly(2026, 12, 31), IsActive = true,
            CreatedByUserId = officer.Id, UpdatedByUserId = officer.Id
        });
        context.GuardianProfiles.Add(new GuardianProfile
        {
            Id = 40, SchoolId = 1, ApplicationUserId = guardianUser.Id, IsActive = true,
            CreatedByUserId = officer.Id, UpdatedByUserId = officer.Id
        });
        context.StudentGuardians.Add(new StudentGuardian
        {
            SchoolId = 1, StudentId = 10, GuardianProfileId = 40, IsPrimary = true,
            ReceivesNotifications = true, ValidFrom = new DateOnly(2026, 1, 1),
            CreatedByUserId = officer.Id, UpdatedByUserId = officer.Id
        });
        context.SchoolStudentAffairsSettings.Add(new SchoolStudentAffairsSettings
        {
            Id = 70, SchoolId = 1, ArrivalCutoffLocalTime = new TimeOnly(6, 30),
            MorningDelayThresholdPerTerm = 10, BehaviorIncidentMultiplePerTerm = 10,
            AcademicConcernThresholdPerTerm = 3, ClassroomEntryPermitThresholdPerTerm = 5,
            AbsenceVisualAlertThresholdPerTerm = 3, AbsenceReferralThresholdPerTerm = 5,
            AbsenceChildRightsThresholdPerTerm = 10, BehaviorCountabilityPolicy = "all-upheld",
            Version = 1, CreatedByUserId = officer.Id, UpdatedByUserId = officer.Id
        });
        await context.SaveChangesAsync();
    }

    private static BehaviorIncident NewBehavior(int id) => new()
    {
        Id = id, SchoolId = 1, StudentId = 10, AcademicTermId = 20, CategoryCode = "conduct",
        Severity = BehaviorSeverity.Medium, Description = "incident", OccurredAt = Now.AddMinutes(id),
        ReportedByStaffUserId = "officer", IsUpheld = true,
        CreatedByUserId = "officer", UpdatedByUserId = "officer"
    };

    private static DailyStudentAttendance NewAttendance(int day, StudentAttendanceStatus status) => new()
    {
        Id = day, SchoolId = 1, StudentId = 10, AcademicTermId = 20, ClassroomId = 30,
        AttendanceDate = new DateOnly(2026, 8, day), Status = status,
        ExcuseStatus = status == StudentAttendanceStatus.AbsentExcused ? AbsenceExcuseStatus.Accepted : null,
        Source = StudentAttendanceSource.SecretaryRoster, RecordedByUserId = "officer", RecordedAt = Now,
        CreatedByUserId = "officer", UpdatedByUserId = "officer"
    };

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
