using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Data.Seeders;
using AlFalah.Infrastructure.Services;
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
        services.AddScoped<SchoolScopeGuard>();
        services.AddScoped<SchoolLookupService>();
        services.AddScoped<ISchoolService, SchoolService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IUserSchoolRoleService, UserSchoolRoleService>();
        services.AddScoped<IRubricService, RubricService>();
        services.AddScoped<IVisitService, VisitService>();
        services.AddScoped<IImprovementPlanService, ImprovementPlanService>();
        services.AddScoped<IPdfReportService, PdfReportService>();
        // D-41 / Task 6 — bulk ZIP export of visits (uses System.IO.Compression.ZipArchive;
        // no extra NuGet package required — the assembly ships with .NET 8).
        services.AddScoped<IVisitsBulkExportService, VisitsBulkExportService>();
        services.AddScoped<IAccountService, AccountService>();

        // Phase 6 / Stage 2: safe loader for the school logo / signature images
        // (URL → bytes, with content-type sniffing + 2 MB cap, no exceptions on
        // failure — every image is best-effort with a neutral PDF fallback).
        services.AddHttpClient("PdfAssetLoader");
        services.AddSingleton<ImageAssetLoader>();

        services.AddScoped<DatabaseSeeder>();

        return services;
    }
}
