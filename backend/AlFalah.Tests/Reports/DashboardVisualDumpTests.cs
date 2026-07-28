using System.Security.Claims;
using AlFalah.Application.Common;
using AlFalah.Application.DTOs.Dashboards;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuestPDF.Infrastructure;
using Xunit;

namespace AlFalah.Tests.Reports;

/// <summary>
/// Development aid — mirrors <see cref="PdfVisualDumpTests"/> for the dashboard
/// exports. Seeds a realistically-populated school (several moderators,
/// teachers, subjects, plans and follow-ups) and writes every role's dashboard
/// PDF to PDF_DUMP_DIR so the printed layout can be eyeballed. Skipped unless
/// the environment variable is set, so CI never writes files.
/// </summary>
public sealed class DashboardVisualDumpTests
{
    [Fact]
    public async Task Dump_dashboard_exports()
    {
        var dir = Environment.GetEnvironmentVariable("PDF_DUMP_DIR");
        if (string.IsNullOrWhiteSpace(dir)) return;

        QuestPDF.Settings.License = LicenseType.Community;

        await using var context = CreateSeededContext();

        await DumpAsync(context, dir, DashboardRole.SchoolManager, SchoolManager(), "dashboard-school-manager.pdf");
        await DumpAsync(context, dir, DashboardRole.MainManager, MainManager(), "dashboard-main-manager.pdf");
        await DumpAsync(context, dir, DashboardRole.Moderator, Moderator("MOD-1"), "dashboard-moderator.pdf");
        await DumpAsync(context, dir, DashboardRole.Instructor, Instructor("TCH-1"), "dashboard-instructor.pdf");
    }

    private static async Task DumpAsync(
        AlFalahDbContext context, string dir, DashboardRole role, ICurrentUserService currentUser, string fileName)
    {
        var service = new DashboardService(
            context,
            currentUser,
            new SchoolScopeGuard(context, currentUser, NullLogger<SchoolScopeGuard>.Instance),
            NullLogger<DashboardService>.Instance);

        var result = await service.ExportPdfAsync(role, new DashboardFilterDto());
        await File.WriteAllBytesAsync(Path.Combine(dir, fileName), result.Bytes);
    }

    // ─── Seed ────────────────────────────────────────────────────────────────

    private static AlFalahDbContext CreateSeededContext()
    {
        var options = new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseInMemoryDatabase($"dashboard-dump-{Guid.NewGuid()}")
            .Options;
        var context = new AlFalahDbContext(options);

        context.AddRange(
            new ApplicationRole { Id = "ROLE-INSTRUCTOR", Name = RoleNames.Instructor, NormalizedName = RoleNames.Instructor.ToUpperInvariant() },
            new ApplicationRole { Id = "ROLE-MODERATOR", Name = RoleNames.Moderator, NormalizedName = RoleNames.Moderator.ToUpperInvariant() });

        context.AddRange(
            new School { Id = 1, Name = "مدرسة الفلاح النموذجية", City = "جدة", IsActive = true },
            new School { Id = 2, Name = "مدرسة الفلاح الثانوية بالحمراء", City = "جدة", IsActive = true },
            new School { Id = 3, Name = "مدرسة الفلاح المتوسطة بالصفا", City = "مكة المكرمة", IsActive = true });

        var teachers = new[]
        {
            ("TCH-1", "عبدالرحمن", "السعيد"),
            ("TCH-2", "ماجد", "المصري"),
            ("TCH-3", "ناصر", "الشمري"),
            ("TCH-4", "خالد", "القحطاني"),
            ("TCH-5", "سارة", "الحربي")
        };
        var moderators = new[]
        {
            ("MOD-1", "محمود", "السعيد"),
            ("MOD-2", "أحمد", "العمري")
        };

        foreach (var (id, first, last) in teachers.Concat(moderators))
            context.Add(User(id, first, last));

        var seq = 1;
        foreach (var (id, _, _) in teachers)
            context.Add(new UserSchoolRole { Id = seq++, UserId = id, SchoolId = 1, RoleId = "ROLE-INSTRUCTOR", IsActive = true });
        foreach (var (id, _, _) in moderators)
            context.Add(new UserSchoolRole { Id = seq++, UserId = id, SchoolId = 1, RoleId = "ROLE-MODERATOR", IsActive = true });

        // Visits: a spread of statuses, subjects, moderators and scores so every
        // table in the export has several rows to lay out.
        var subjects = new[] { "اللغة العربية", "الرياضيات", "اللغة الإنجليزية", "العلوم", "الدراسات الإسلامية" };
        var statuses = new[]
        {
            VisitStatus.Approved, VisitStatus.Approved, VisitStatus.Approved,
            VisitStatus.PendingApproval, VisitStatus.Draft, VisitStatus.RejectedForChanges
        };

        var visitId = 100;
        for (var i = 0; i < 24; i++)
        {
            var teacher = teachers[i % teachers.Length];
            var moderator = moderators[i % moderators.Length];
            var status = statuses[i % statuses.Length];
            var overall = 1.1m + 0.12m * (i % 24);
            var visit = new Visit
            {
                Id = visitId,
                SchoolId = 1,
                InstructorId = teacher.Item1,
                CreatedByUserId = moderator.Item1,
                RubricVersionId = 1,
                Subject = subjects[i % subjects.Length],
                GradeClass = $"{1 + i % 6}/{1 + i % 3}",
                VisitCategory = VisitCategory.ClassroomOrPeriodic,
                VisitSequence = VisitSequence.First,
                Status = status,
                VisitDate = new DateTimeOffset(2026, 7, 1 + i % 27, 8, 0, 0, TimeSpan.Zero),
                Analysis = new VisitAnalysis
                {
                    Id = visitId,
                    VisitId = visitId,
                    OverallScore = Math.Round(Math.Min(4m, overall), 3),
                    TotalScore = 25m * Math.Min(4m, overall),
                    MaximumScore = 100m,
                    PerformanceLevelAr = VisitAnalysisEngineLevel(Math.Min(4m, overall)),
                    ComputedAt = new DateTimeOffset(2026, 7, 1 + i % 27, 9, 0, 0, TimeSpan.Zero),
                    DomainAverages = Enumerable.Range(1, 5).Select(d => new VisitDomainAverage
                    {
                        Id = visitId * 10 + d,
                        VisitAnalysisId = visitId,
                        RubricDomainId = d,
                        DomainCode = $"D{d}",
                        DomainNameAr = DomainName(d),
                        AverageScore = Math.Round(Math.Clamp(overall + (d - 3) * 0.4m, 0m, 4m), 3)
                    }).ToList()
                }
            };
            context.Add(visit);
            visitId++;
        }

        // Second/third school rows so the Main Manager comparison table is real.
        foreach (var (schoolId, count) in new[] { (2, 9), (3, 5) })
        {
            for (var i = 0; i < count; i++)
            {
                context.Add(new Visit
                {
                    Id = visitId,
                    SchoolId = schoolId,
                    InstructorId = teachers[i % teachers.Length].Item1,
                    CreatedByUserId = moderators[i % moderators.Length].Item1,
                    RubricVersionId = 1,
                    Subject = subjects[i % subjects.Length],
                    VisitCategory = VisitCategory.ClassroomOrPeriodic,
                    VisitSequence = VisitSequence.First,
                    Status = i % 3 == 0 ? VisitStatus.PendingApproval : VisitStatus.Approved,
                    VisitDate = new DateTimeOffset(2026, 7, 1 + i, 8, 0, 0, TimeSpan.Zero),
                    Analysis = new VisitAnalysis
                    {
                        Id = visitId,
                        VisitId = visitId,
                        OverallScore = 2.0m + 0.2m * i,
                        TotalScore = 60m,
                        MaximumScore = 100m,
                        PerformanceLevelAr = VisitAnalysisEngineLevel(2.0m + 0.2m * i),
                        ComputedAt = new DateTimeOffset(2026, 7, 1 + i, 9, 0, 0, TimeSpan.Zero)
                    }
                });
                visitId++;
            }
        }

        for (var i = 0; i < 9; i++)
        {
            var planId = 500 + i;
            context.Add(new ImprovementPlan
            {
                Id = planId,
                SchoolId = 1,
                InstructorId = teachers[i % teachers.Length].Item1,
                VisitId = 100 + i,
                Goal = "رفع مستوى تنويع استراتيجيات التدريس بما يراعي الفروق الفردية.",
                Actions = "ورش تدريبية + زيارات تبادلية + متابعة أسبوعية.",
                SuccessIndicators = "تحسن درجة المحور بنسبة ١٥٪ في الزيارة القادمة.",
                StartDate = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
                EndDate = new DateTimeOffset(2026, 9, 30, 0, 0, 0, TimeSpan.Zero),
                Status = i % 5 == 0 ? PlanStatus.Completed : PlanStatus.Active,
                CreatedByUserId = moderators[i % moderators.Length].Item1
            });

            context.Add(new PlanFollowUp
            {
                Id = 700 + i,
                ImprovementPlanId = planId,
                FollowDate = new DateTimeOffset(2026, 7, 10 + i, 0, 0, 0, TimeSpan.Zero),
                ProgressNote = "تم تنفيذ ورشة داخلية عن التقويم البنائي وحضرها المعلم.",
                ProgressScore = 40 + i * 5,
                CreatedByUserId = moderators[i % moderators.Length].Item1
            });
        }

        context.Add(new Complaint
        {
            Id = 900,
            SchoolId = 1,
            VisitId = 100,
            InstructorUserId = "TCH-1",
            ModeratorUserId = "MOD-1",
            Subject = "اعتراض على درجة محور التقويم",
            Body = "أرى أن الدرجة لا تعكس ما تم تنفيذه في الحصة.",
            CreatedByUserId = "TCH-1"
        });

        context.SaveChanges();
        return context;
    }

    private static string DomainName(int d) => d switch
    {
        1 => "بيئة التعلم",
        2 => "التخطيط والتنفيذ",
        3 => "تنمية المهارات",
        4 => "التقويم",
        _ => "سلوك المتعلمين"
    };

    private static string VisitAnalysisEngineLevel(decimal overall) =>
        AlFalah.Application.Analysis.VisitAnalysisEngine.MapPerformanceLevel(overall);

    private static ApplicationUser User(string id, string firstName, string lastName) => new()
    {
        Id = id,
        UserName = id,
        NormalizedUserName = id,
        FirstName = firstName,
        LastName = lastName
    };

    // ─── Callers ─────────────────────────────────────────────────────────────

    private static ICurrentUserService Principal(string userId, string role, string permission, int? activeSchoolId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Role, role),
            new("permission", permission)
        };
        if (activeSchoolId is not null)
            claims.Add(new Claim("active_school_id", activeSchoolId.Value.ToString()));

        return new CurrentUserService(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "visual-dump"))
            }
        });
    }

    private static ICurrentUserService MainManager() =>
        Principal("MAIN-MANAGER-1", RoleNames.MainManager, PermissionNames.DashboardMainManager, null);

    private static ICurrentUserService SchoolManager() =>
        Principal("SCHOOL-MANAGER-1", RoleNames.SchoolManager, PermissionNames.DashboardSchoolManager, 1);

    private static ICurrentUserService Moderator(string userId) =>
        Principal(userId, RoleNames.Moderator, PermissionNames.DashboardModerator, 1);

    private static ICurrentUserService Instructor(string userId) =>
        Principal(userId, RoleNames.Instructor, PermissionNames.DashboardInstructor, 1);
}
