using System.Security.Claims;
using AlFalah.Application.Common;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Repositories;
using AlFalah.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AlFalah.Tests.Security;

public sealed class AccountAccessTests
{
    private readonly UserPasswordProtector protector = new(new EphemeralDataProtectionProvider());

    [Fact]
    public async Task Identity_create_change_and_reset_persist_only_the_current_encrypted_password()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddDbContext<AlFalahDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddIdentityCore<ApplicationUser>().AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AlFalahDbContext>().AddDefaultTokenProviders();
        services.AddScoped<UserPasswordProtector>();
        services.AddScoped<IPasswordHasher<ApplicationUser>, ManagedPasswordHasher>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var protection = scope.ServiceProvider.GetRequiredService<UserPasswordProtector>();
        var db = scope.ServiceProvider.GetRequiredService<AlFalahDbContext>();
        var user = new ApplicationUser { UserName = "identity-fixture", FirstName = "First", LastName = "Last" };
        (await manager.CreateAsync(user, "Create@Secret2026")).Succeeded.Should().BeTrue();
        var created = await db.Users.AsNoTracking().SingleAsync();
        protection.Read(created.Id, created.ProtectedPassword, created.PasswordHash).Should().Be("Create@Secret2026");
        var oldCiphertext = created.ProtectedPassword;
        (await manager.ChangePasswordAsync(user, "Create@Secret2026", "Changed@Secret2026")).Succeeded.Should().BeTrue();
        var changed = await db.Users.AsNoTracking().SingleAsync();
        protection.Read(changed.Id, changed.ProtectedPassword, changed.PasswordHash).Should().Be("Changed@Secret2026");
        protection.Read(changed.Id, oldCiphertext, changed.PasswordHash).Should().BeNull();
        var token = await manager.GeneratePasswordResetTokenAsync(user);
        (await manager.ResetPasswordAsync(user, token, "Reset@Secret2026")).Succeeded.Should().BeTrue();
        var reset = await db.Users.AsNoTracking().SingleAsync();
        protection.Read(reset.Id, reset.ProtectedPassword, reset.PasswordHash).Should().Be("Reset@Secret2026");
        (await manager.CheckPasswordAsync(user, "Reset@Secret2026")).Should().BeTrue();
        (await manager.CheckPasswordAsync(user, "Changed@Secret2026")).Should().BeFalse();
    }

    [Fact]
    public void Managed_passwords_keep_a_valid_identity_hash_and_an_independent_encrypted_copy()
    {
        var user = new ApplicationUser { Id = "teacher" };
        var hasher = new ManagedPasswordHasher(protector, Options.Create(new PasswordHasherOptions()));
        var hash = hasher.HashPassword(user, "Example@Secret2026");
        hasher.VerifyHashedPassword(user, hash, "Example@Secret2026").Should().Be(PasswordVerificationResult.Success);
        hasher.VerifyHashedPassword(user, hash, "wrong").Should().Be(PasswordVerificationResult.Failed);
        user.ProtectedPassword.Should().NotContain("Example@Secret2026");
        protector.Read(user.Id, user.ProtectedPassword, hash).Should().Be("Example@Secret2026");
        protector.Read("other-user", user.ProtectedPassword, hash).Should().BeNull();
        protector.Read(user.Id, user.ProtectedPassword, "changed hash").Should().BeNull();
        protector.Read(user.Id, "tampered", hash).Should().BeNull();
        protector.Read(user.Id, null, hash).Should().BeNull();
    }

    [Fact]
    public async Task Own_school_password_reveal_is_audited_without_recording_secrets()
    {
        await using var db = Database();
        await Seed(db);
        var service = Service(db, "manager", RoleNames.SchoolManager, 1);
        var revealed = await service.RevealPasswordAsync("teacher");
        revealed.Should().Be(new PasswordRevealDto(true, "Example@Secret2026"));
        var audit = await db.AuditLogs.SingleAsync();
        audit.UserId.Should().Be("manager");
        audit.EntityId.Should().Be("teacher");
        audit.Action.Should().Be("User.PasswordRevealed");
        audit.NewValues.Should().BeNull();
        audit.OldValues.Should().BeNull();
    }

    [Theory]
    [InlineData("manager", "SchoolManager", 2, "teacher")]
    [InlineData("manager", "SchoolManager", 1, "other-teacher")]
    [InlineData("teacher", "Instructor", 1, "teacher")]
    [InlineData("manager", "SchoolManager", 1, "global-admin")]
    public async Task Password_reveal_rejects_another_school_a_non_manager_and_global_accounts(
        string actor, string role, int schoolId, string target)
    {
        await using var db = Database();
        await Seed(db);
        var action = () => Service(db, actor, role, schoolId).RevealPasswordAsync(target);
        await action.Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        (await db.AuditLogs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task A_revoked_manager_assignment_cannot_reveal_passwords()
    {
        await using var db = Database();
        await Seed(db);
        (await db.UserSchoolRoles.SingleAsync(x => x.UserId == "manager")).IsActive = false;
        await db.SaveChangesAsync();
        var action = () => Service(db, "manager", RoleNames.SchoolManager, 1).RevealPasswordAsync("teacher");
        await action.Should().ThrowAsync<UnauthorizedSchoolAccessException>();
    }

    [Fact]
    public async Task Existing_hash_only_accounts_are_unavailable_instead_of_reset_or_fabricated()
    {
        await using var db = Database();
        await Seed(db);
        var teacher = await db.Users.SingleAsync(x => x.Id == "teacher");
        teacher.ProtectedPassword = null;
        await db.SaveChangesAsync();
        var result = await Service(db, "manager", RoleNames.SchoolManager, 1).RevealPasswordAsync("teacher");
        result.Should().Be(new PasswordRevealDto(false, null));
        teacher.PasswordHash.Should().Be("stored hash");
    }

    [Theory]
    [InlineData(RoleNames.Instructor)]
    [InlineData(RoleNames.Secretary)]
    [InlineData(RoleNames.Moderator)]
    [InlineData(RoleNames.SocialWorker)]
    [InlineData(RoleNames.StudentAffairsOfficer)]
    [InlineData(RoleNames.SecurityGuard)]
    [InlineData(RoleNames.Guardian)]
    [InlineData(RoleNames.MainManager)]
    [InlineData(RoleNames.SuperAdmin)]
    public async Task Every_non_school_manager_needs_a_signature_only_until_it_is_saved(string role)
    {
        await using var db = Database();
        await Seed(db);
        var service = Service(db, "teacher", role, 1);
        (await service.GetSignatureRequirementAsync()).Required.Should().BeTrue();
        db.Add(new UserSignature { UserId = "teacher", SignatureDrawnData = "saved PNG data" });
        await db.SaveChangesAsync();
        (await service.GetSignatureRequirementAsync()).Should().Be(new SignatureRequirementDto(false, true));
        db.Set<UserSignature>().Should().ContainSingle();
    }

    [Fact]
    public async Task School_manager_is_exempt_from_signature_onboarding()
    {
        await using var db = Database();
        await Seed(db);
        (await Service(db, "manager", RoleNames.SchoolManager, 1).GetSignatureRequirementAsync())
            .Should().Be(new SignatureRequirementDto(false, false));
    }

    private static AlFalahDbContext Database() => new(new DbContextOptionsBuilder<AlFalahDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private AccountAccessService Service(AlFalahDbContext db, string actor, string role, int schoolId)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, actor), new Claim(ClaimTypes.Role, role),
            new Claim("active_school_id", schoolId.ToString()), new Claim("permission", PermissionNames.UserEdit) };
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        return new(new AccountAccessRepository(db), new CurrentUserService(new HttpContextAccessor { HttpContext = context }), protector);
    }

    private async Task Seed(AlFalahDbContext db)
    {
        db.AddRange(new School { Id = 1, Name = "Own school" }, new School { Id = 2, Name = "Other school" },
            new ApplicationRole { Id = "manager-role", Name = RoleNames.SchoolManager },
            new ApplicationRole { Id = "teacher-role", Name = RoleNames.Instructor },
            new ApplicationRole { Id = "global-role", Name = RoleNames.SuperAdmin });
        foreach (var id in new[] { "manager", "teacher", "other-teacher", "global-admin" })
        {
            db.Add(new ApplicationUser { Id = id, UserName = id, FirstName = id, PasswordHash = "stored hash",
                ProtectedPassword = protector.Protect(id, "Example@Secret2026", "stored hash") });
            db.Add(new UserSchoolRole { UserId = id, SchoolId = id == "other-teacher" ? 2 : 1,
                RoleId = id == "manager" ? "manager-role" : "teacher-role" });
        }
        db.UserRoles.Add(new IdentityUserRole<string> { UserId = "global-admin", RoleId = "global-role" });
        await db.SaveChangesAsync();
    }
}
