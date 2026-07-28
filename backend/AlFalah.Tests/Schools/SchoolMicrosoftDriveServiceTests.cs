using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AlFalah.Tests.Schools;

public sealed class SchoolMicrosoftDriveServiceTests
{
    [Fact]
    public async Task Manager_Configures_Only_Their_Active_School_Drive()
    {
        await using var db = Context();
        db.Schools.AddRange(new School { Id = 1, Name = "One", City = "Riyadh" }, new School { Id = 2, Name = "Two", City = "Jeddah" });
        await db.SaveChangesAsync();
        var service = Service(db, new User(RoleNames.SchoolManager, 1));

        var result = await service.ConfigureForCurrentSchoolAsync(new("11111111-1111-1111-1111-111111111111", "school@contoso.edu", "drive-1", "root-1", "Evidence", true));

        result.SchoolId.Should().Be(1);
        (await db.SchoolMicrosoftDrives.SingleAsync()).SchoolId.Should().Be(1);
    }

    [Fact]
    public async Task NonManager_Cannot_Read_School_Drive_Settings()
    {
        await using var db = Context();
        var service = Service(db, new User(RoleNames.Moderator, 1));
        var action = () => service.GetForCurrentSchoolAsync();
        await action.Should().ThrowAsync<AlFalah.Application.Common.UnauthorizedSchoolAccessException>();
    }

    private static AlFalahDbContext Context() => new(new DbContextOptionsBuilder<AlFalahDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static ISchoolMicrosoftDriveService Service(AlFalahDbContext db, ICurrentUserService user) => new SchoolMicrosoftDriveService(db, user,
        new SchoolScopeGuard(db, user, NullLogger<SchoolScopeGuard>.Instance),
        new AuditLogWriter(db, new HttpContextAccessor { HttpContext = new DefaultHttpContext() }, NullLogger<AuditLogWriter>.Instance));

    private sealed class User(string role, int schoolId) : ICurrentUserService
    {
        public string? UserId => "manager"; public string? Username => "manager"; public int? ActiveSchoolId => schoolId; public string? PreferredLanguage => "ar"; public bool IsAuthenticated => true;
        public bool IsInRole(string roleName) => role == roleName; public bool HasPermission(string permissionName) => false;
        public IEnumerable<string> GetRoles() => [role]; public IEnumerable<string> GetPermissions() => [];
        public bool IsGlobalAdmin() => false; public bool IsSchoolScopedRole() => true;
    }
}
