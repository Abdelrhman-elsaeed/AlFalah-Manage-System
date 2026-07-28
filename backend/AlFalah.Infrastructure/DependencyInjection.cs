using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Data.Seeders;
using AlFalah.Infrastructure.Services;
using AlFalah.Infrastructure.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Web;
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
            // Password policy
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = true;

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
        QuestPDF.Settings.License = LicenseType.Community;
        // Stage 2: enable layout debugging in Development to surface
        // conflicting-size diagnostics when a layout regression is reported.
        // (Falls back to no-op in Production where the env var is absent.)
        if (Environment.GetEnvironmentVariable("QUESTPDF_DEBUG") == "1")
            QuestPDF.Settings.EnableDebugging = true;

        // ─── Services ─────────────────────────────────────────────────────────
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
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
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IImprovementPlanService, ImprovementPlanService>();
        services.AddScoped<IPdfReportService, PdfReportService>();
        // D-41 / Task 6 — bulk ZIP export of visits (uses System.IO.Compression.ZipArchive;
        // no extra NuGet package required — the assembly ships with .NET 8).
        services.AddScoped<IVisitsBulkExportService, VisitsBulkExportService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IMicrosoftGraphTokenService>(provider =>
        {
            var tokens = provider.GetService<ITokenAcquisition>();
            return tokens is null
                ? new UnavailableMicrosoftGraphTokenService()
                : new MicrosoftGraphTokenService(tokens, provider.GetRequiredService<IConfiguration>());
        });
        services.AddScoped<ITeacherMicrosoftAccountService, TeacherMicrosoftAccountService>();
        services.AddScoped<ISchoolMicrosoftDriveService, SchoolMicrosoftDriveService>();
        services.AddScoped<ITeacherDriveMappingService, TeacherDriveMappingService>();
        services.AddScoped<OneDriveBrowserService>();
        services.AddScoped<IOneDriveBrowserService>(provider => provider.GetRequiredService<OneDriveBrowserService>());
        services.AddScoped<IOneDriveUploadService, OneDriveUploadService>();
        services.AddScoped<EvidenceSubmissionService>();
        services.AddScoped<IEvidenceSubmissionService>(provider => provider.GetRequiredService<EvidenceSubmissionService>());
        services.AddScoped<IEvidenceMatrixService, EvidenceMatrixService>();
        services.AddScoped<IEvidenceReconciliationService, EvidenceReconciliationService>();

        // Phase 6 / Stage 2: safe loader for the school logo / signature images
        // (URL → bytes, with content-type sniffing + 2 MB cap, no exceptions on
        // failure — every image is best-effort with a neutral PDF fallback).
        services.AddHttpClient("PdfAssetLoader");
        services.AddHttpClient("MicrosoftGraph", client =>
        {
            client.BaseAddress = new Uri(configuration["MicrosoftGraph:BaseUrl"] ?? "https://graph.microsoft.com/v1.0/");
            client.Timeout = TimeSpan.FromSeconds(100);
        });
        services.AddHttpClient("MicrosoftGraphToken");
        services.AddHostedService<EvidenceReconciliationBackgroundService>();
        services.AddSingleton<ImageAssetLoader>();

        services.AddScoped<DatabaseSeeder>();

        return services;
    }
}
