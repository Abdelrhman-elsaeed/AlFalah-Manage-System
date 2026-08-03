using AlFalah.Application.Common;
using AlFalah.Application.DTOs.StudentAnalyzer;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using AlFalah.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AlFalah.Tests.Security;

public sealed class StudentAnalyzerAuthorizationTests
{
    [Fact]
    public async Task Manager_can_grant_any_active_school_user_and_delegate_cannot_redelegate()
    {
        await using var harness = await Harness.CreateAsync();
        var manager = harness.Service(Harness.ManagerId, RoleNames.SchoolManager);

        await manager.UpdateDelegatesAsync(new(new[] { Harness.InstructorId }));

        var delegateService = harness.Service(Harness.InstructorId, RoleNames.Instructor);
        var capabilities = await delegateService.GetCapabilitiesAsync();
        capabilities.CanAccess.Should().BeTrue();
        capabilities.CanManageSettings.Should().BeTrue();
        capabilities.CanDelegate.Should().BeFalse();
        (await delegateService.GetSettingsAsync()).ActiveProvider.Should().Be(StudentAnalyzerProvider.Groq);
        await delegateService.Invoking(service => service.UpdateDelegatesAsync(new(Array.Empty<string>())))
            .Should().ThrowAsync<UnauthorizedSchoolAccessException>();
    }

    [Fact]
    public async Task Non_granted_user_and_global_admin_are_denied()
    {
        await using var harness = await Harness.CreateAsync();

        (await harness.Service(Harness.InstructorId, RoleNames.Instructor).GetCapabilitiesAsync())
            .CanAccess.Should().BeFalse();
        (await harness.Service("global-admin", RoleNames.SuperAdmin, global: true).GetCapabilitiesAsync())
            .CanAccess.Should().BeFalse();
    }

    [Fact]
    public async Task Manager_can_load_models_with_an_unsaved_provider_key()
    {
        await using var harness = await Harness.CreateAsync();
        var manager = harness.Service(Harness.ManagerId, RoleNames.SchoolManager);

        await manager.GetModelsAsync(StudentAnalyzerProvider.OpenRouter, "typed-openrouter-key");

        harness.AiClient.LastApiKey.Should().Be("typed-openrouter-key");
    }

    [Fact]
    public async Task Manager_can_load_the_public_openrouter_catalog_without_a_saved_key()
    {
        await using var harness = await Harness.CreateAsync();
        var manager = harness.Service(Harness.ManagerId, RoleNames.SchoolManager);

        await manager.GetModelsAsync(StudentAnalyzerProvider.OpenRouter);

        harness.AiClient.LastApiKey.Should().BeEmpty();
    }

    private sealed class Harness : IAsyncDisposable
    {
        public const string ManagerId = "manager";
        public const string InstructorId = "instructor";
        private readonly AlFalahDbContext _context;
        public StubAiClient AiClient { get; } = new();

        private Harness(AlFalahDbContext context) => _context = context;

        public static async Task<Harness> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<AlFalahDbContext>()
                .UseInMemoryDatabase($"student-analyzer-{Guid.NewGuid()}")
                .Options;
            var context = new AlFalahDbContext(options);
            var managerRole = Role("manager-role", RoleNames.SchoolManager);
            var instructorRole = Role("instructor-role", RoleNames.Instructor);
            var manager = User(ManagerId, "مدير", "المدرسة");
            var instructor = User(InstructorId, "أحمد", "محمد");
            context.AddRange(managerRole, instructorRole, manager, instructor);
            context.Schools.Add(new School
            {
                Id = 1,
                Name = "مدرسة الفلاح",
                City = "القاهرة",
                Stage = SchoolStage.Primary,
                IsActive = true,
                ManagerUserId = ManagerId
            });
            context.UserSchoolRoles.AddRange(
                new UserSchoolRole { SchoolId = 1, UserId = ManagerId, RoleId = managerRole.Id, IsActive = true },
                new UserSchoolRole { SchoolId = 1, UserId = InstructorId, RoleId = instructorRole.Id, IsActive = true });
            await context.SaveChangesAsync();
            return new Harness(context);
        }

        public IStudentAnalyzerService Service(string userId, string role, bool global = false)
        {
            var current = new TestCurrentUser(userId, role, global);
            return new StudentAnalyzerService(
                new StudentAnalyzerRepository(_context),
                AiClient,
                current,
                new StudentAnalyzerCredentialProtector(new EphemeralDataProtectionProvider()),
                new AuditLogWriter(_context, new HttpContextAccessor(), NullLogger<AuditLogWriter>.Instance),
                new MemoryCache(new MemoryCacheOptions()));
        }

        public ValueTask DisposeAsync() => _context.DisposeAsync();

        private static ApplicationUser User(string id, string firstName, string lastName) => new()
        {
            Id = id,
            UserName = id,
            NormalizedUserName = id.ToUpperInvariant(),
            FirstName = firstName,
            LastName = lastName,
            IsActive = true
        };

        private static ApplicationRole Role(string id, string name) => new()
        {
            Id = id,
            Name = name,
            NormalizedName = name.ToUpperInvariant()
        };
    }

    private sealed class TestCurrentUser(string userId, string role, bool global) : ICurrentUserService
    {
        public string? UserId => userId;
        public string? Username => userId;
        public int? ActiveSchoolId => 1;
        public string? PreferredLanguage => "ar";
        public bool IsAuthenticated => true;
        public bool IsInRole(string roleName) => roleName == role;
        public bool HasPermission(string permissionName) => true;
        public IEnumerable<string> GetRoles() => new[] { role };
        public IEnumerable<string> GetPermissions() => Array.Empty<string>();
        public bool IsGlobalAdmin() => global;
        public bool IsSchoolScopedRole() => !global;
    }

    private sealed class StubAiClient : IStudentAnalyzerAiClient
    {
        public string? LastApiKey { get; private set; }

        public Task<StudentAnalyzerAiResponse> AnalyzeAsync(StudentAnalyzerAiRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new StudentAnalyzerAiResponse("analysis", request.Model));

        public Task<IReadOnlyList<StudentAnalyzerModelDto>> GetModelsAsync(StudentAnalyzerProvider provider, string apiKey, CancellationToken cancellationToken = default)
        {
            LastApiKey = apiKey;
            return Task.FromResult<IReadOnlyList<StudentAnalyzerModelDto>>(Array.Empty<StudentAnalyzerModelDto>());
        }
    }
}
