using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.GatePasses;
using AlFalah.Application.StudentAffairs.Attendance;
using AlFalah.Application.StudentAffairs.MorningDelays;
using AlFalah.Application.StudentAffairs.Referrals;
using AlFalah.Application.StudentAffairs.Summons;
using AlFalah.Application.StudentAffairs.TeacherActions;
using AlFalah.Application.StudentAffairs.TeacherContext;
using AlFalah.Application.StudentAffairs.Biometrics;
using AlFalah.Application.StudentAffairs.Notifications;
using AlFalah.Application.StudentAffairs.Settings;
using AlFalah.Application.StudentAffairs.Messaging;
using AlFalah.Application.StudentAffairs.Students;
using AlFalah.Domain.Entities;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Data.Seeders;
using AlFalah.Infrastructure.Services;
using AlFalah.Infrastructure.Repositories;
using AlFalah.Infrastructure.Integrations.Biometrics;
using AlFalah.Infrastructure.Integrations.Noor;
using AlFalah.Infrastructure.Automations;
using AlFalah.Infrastructure.Notifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;

namespace AlFalah.Infrastructure;

/// <summary>
/// Registers all Infrastructure layer services with the DI container.
/// Called from AlFalah.Api's Program.cs.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ─── EF Core ─────────────────────────────────────────────────────────
        services.AddDbContext<AlFalahDbContext>(options =>
        {
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sqlOptions => sqlOptions.MigrationsAssembly(typeof(AlFalahDbContext).Assembly.FullName)
            );
        });

        // ─── ASP.NET Core Identity ────────────────────────────────────────────
        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
        {
            // Password policy — simplified: letters and numbers, min 6 chars, no required symbols or uppercase
            options.Password.RequireDigit = false;
            options.Password.RequiredLength = 6;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireNonAlphanumeric = false;

            // Account lockout
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;

            // User settings
            options.User.RequireUniqueEmail = false; // Username is primary
            options.SignIn.RequireConfirmedEmail = false;
        })
        .AddEntityFrameworkStores<AlFalahDbContext>()
        .AddDefaultTokenProviders();

        // ─── QuestPDF Community license (Phase 6 Stage 1) ────────────────────
        // QuestPDF 2024.x switched to a community license that requires explicit
        // acknowledgment. Without this, the library throws an exception when
        // generating PDFs. See Phase 6 / Stage 1 PDF report feature.
        try
        {
            QuestPDF.Settings.License = LicenseType.Community;
            if (Environment.GetEnvironmentVariable("QUESTPDF_DEBUG") == "1")
                QuestPDF.Settings.EnableDebugging = true;
        }
        catch
        {
            // Suppress if native dependencies cannot be loaded in 32-bit environments
        }

        // ─── Services ─────────────────────────────────────────────────────────
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddSingleton(TimeProvider.System);
        var teacherContextOptions = BuildTeacherContextOptions(configuration);
        services.AddSingleton(teacherContextOptions);
        services.AddSingleton<TeacherContextSchedule>();
        services.AddScoped<AuditLogWriter>();
        services.AddScoped<SchoolScopeGuard>();
        services.AddScoped<SchoolLookupService>();
        services.AddScoped<ISchoolLocationRepository, SchoolLocationRepository>();
        services.AddScoped<ISchoolLocationService, SchoolLocationService>();
        services.AddScoped<IParentSurveyRepository, ParentSurveyRepository>();
        services.AddScoped<IParentSurveyService, ParentSurveyService>();
        services.AddScoped<ISchoolService, SchoolService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IUserSchoolRoleService, UserSchoolRoleService>();
        services.AddScoped<IRubricService, RubricService>();
        services.AddScoped<IVisitService, VisitService>();
        services.AddScoped<ITeacherService, TeacherService>();
        services.AddScoped<IComplaintService, ComplaintService>();
        services.AddScoped<IAttendanceService, AttendanceService>();
        services.AddScoped<IAttendancePdfService, AttendancePdfService>();
        services.AddScoped<ISchoolTimetableRepository, SchoolTimetableRepository>();
        services.AddScoped<ISchoolTimetableDocumentService, SchoolTimetableDocumentService>();
        services.AddScoped<ISchoolTimetableService, SchoolTimetableService>();
        services.AddScoped<IGatePassWorkflowRepository, GatePassWorkflowRepository>();
        services.AddScoped<IAttendanceWorkflowRepository, AttendanceWorkflowRepository>();
        services.AddScoped<IMorningDelayWorkflowRepository, MorningDelayWorkflowRepository>();
        services.AddScoped<ITeacherActionWorkflowRepository, TeacherActionWorkflowRepository>();
        services.AddScoped<ITeacherContextRepository, TeacherContextRepository>();
        services.AddScoped<IReferralWorkflowRepository, ReferralWorkflowRepository>();
        services.AddScoped<ISummonWorkflowRepository, SummonWorkflowRepository>();
        services.AddScoped<IBiometricImportRepository, BiometricImportRepository>();
        services.AddScoped<IZajelBiometricWorkbookReader, ZajelBiometricWorkbookReader>();
        services.AddScoped<INoorExportRepository, NoorExportRepository>();
        services.AddSingleton<INoorWorkbookWriter, NoorWorkbookWriter>();
        services.AddScoped<INotificationWorkflowRepository, NotificationWorkflowRepository>();
        services.AddScoped<IStudentAffairsSettingsRepository, StudentAffairsSettingsRepository>();
        services.AddScoped<IMessagingWorkflowRepository, MessagingWorkflowRepository>();
        services.AddScoped<IStudentWorkflowRepository, StudentWorkflowRepository>();
        services.AddScoped<StudentAffairsAutomationRuleEngine>();
        services.AddScoped<StudentAffairsNotificationDispatcher>();
        services.AddScoped<StudentAffairsOutboxProcessor>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<IStudentAnalyzerRepository, StudentAnalyzerRepository>();
        services.AddScoped<IStudentAnalyzerAiClient, StudentAnalyzerAiClient>();
        services.AddScoped<IStudentAnalyzerService, StudentAnalyzerService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IImprovementPlanService, ImprovementPlanService>();
        services.AddScoped<IPdfReportService, PdfReportService>();
        // D-41 / Task 6 — bulk ZIP export of visits (uses System.IO.Compression.ZipArchive;
        // no extra NuGet package required — the assembly ships with .NET 8).
        services.AddScoped<IVisitsBulkExportService, VisitsBulkExportService>();
        services.AddScoped<IAccountService, AccountService>();
        // ─── Teacher evidence files on Google Drive ───────────────────────────
        // The whole feature reaches Drive through ONE school-owned credential, so the
        // credential protector, the token minter and the folder guard are all required
        // for a request to be authorized — see TeacherDriveFolderGuard for why.
        services.AddMemoryCache();
        services.AddScoped<GoogleDriveCredentialProtector>();
        services.AddScoped<StudentAnalyzerCredentialProtector>();
        services.AddScoped<IGoogleDriveTokenService, GoogleDriveTokenService>();
        services.AddScoped<IGoogleDriveClient, GoogleDriveClient>();
        services.AddScoped<TeacherDriveFolderGuard>();
        services.AddScoped<ITeacherDriveIdentityService, TeacherDriveIdentityService>();
        services.AddScoped<ISchoolGoogleDriveService, SchoolGoogleDriveService>();
        // Reuses the "GoogleOAuth" HttpClient below — the authorization-code exchange and the
        // refresh-token grant both post to the same Google token endpoint.
        services.AddScoped<IGoogleDriveOAuthService, GoogleDriveOAuthService>();
        services.AddScoped<ITeacherDriveMappingService, TeacherDriveMappingService>();
        services.AddScoped<IGoogleDriveBrowserService, GoogleDriveBrowserService>();
        services.AddScoped<IGoogleDriveUploadService, GoogleDriveUploadService>();
        services.AddScoped<EvidenceSubmissionService>();
        services.AddScoped<IEvidenceSubmissionService>(provider => provider.GetRequiredService<EvidenceSubmissionService>());
        services.AddScoped<IEvidenceMatrixService, EvidenceMatrixService>();
        services.AddScoped<IEvidenceReconciliationService, EvidenceReconciliationService>();

        // Phase 6 / Stage 2: safe loader for the school logo / signature images
        // (URL → bytes, with content-type sniffing + 2 MB cap, no exceptions on
        // failure — every image is best-effort with a neutral PDF fallback).
        services.AddHttpClient("PdfAssetLoader");
        // Absolute URLs are built per request (the Drive API and its upload host differ),
        // so only the timeout matters here — it must tolerate a 250 MB upload.
        services.AddHttpClient("GoogleDrive", client => client.Timeout = TimeSpan.FromMinutes(10));
        services.AddHttpClient("GoogleOAuth", client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddHttpClient("StudentAnalyzerAi", client => client.Timeout = TimeSpan.FromMinutes(3));
        services.AddHostedService<EvidenceReconciliationBackgroundService>();
        services.AddHostedService<StudentAffairsOutboxWorker>();
        services.AddSingleton<ImageAssetLoader>();

        services.AddScoped<DatabaseSeeder>();

        return services;
    }

    private static TeacherContextScheduleOptions BuildTeacherContextOptions(
        IConfiguration configuration)
    {
        var configuredStart = configuration["TeacherContext:FirstPeriodStartsAt"];
        var firstPeriodStartsAt = TimeOnly.TryParse(
            configuredStart,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out var parsedStart)
            ? parsedStart
            : new TimeOnly(7, 0);

        return new TeacherContextScheduleOptions
        {
            SchoolTimeZoneId = configuration["TeacherContext:SchoolTimeZoneId"]
                ?? "Africa/Cairo",
            FirstPeriodStartsAt = firstPeriodStartsAt,
            PeriodDurationMinutes = configuration.GetValue<int?>(
                "TeacherContext:PeriodDurationMinutes") ?? 45,
            PassingTimeMinutes = configuration.GetValue<int?>(
                "TeacherContext:PassingTimeMinutes") ?? 5,
            AllowOffHoursFallback = configuration.GetValue<bool>(
                "TeacherContext:AllowOffHoursFallback")
        };
    }
}
