using AlFalah.Application.Interfaces;
using AlFalah.Application.Notifications;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Notifications;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace AlFalah.Tests.StudentAffairs;

public sealed class InboxSecurityTests
{
    [Fact]
    public async Task SignedCursorsCannotBeReusedAcrossUserSchoolConversationOrPurpose()
    {
        await using var db = await InboxWindowTests.SeedAsync();
        var permission = new Permission { Name = PermissionNames.MessagingViewOwn };
        db.Add(permission); await db.SaveChangesAsync();
        db.RolePermissions.Add(new() { RoleId = "instructor", PermissionId = permission.Id }); await db.SaveChangesAsync();
        var codec = new InboxCursorCodec(new EphemeralDataProtectionProvider());
        var options = Options.Create(new InboxOptions());
        var service = new InboxService(new InboxRepository(db, options, TimeProvider.System), codec, new User(), options);
        var valid = await service.WindowAsync(1, null, 50, default);
        valid.IsSuccess.Should().BeTrue(); valid.Data!.HasOlder.Should().BeTrue();
        (await service.WindowAsync(1, valid.Data.NextBeforeCursor, 50, default)).Data!.Items.Should().HaveCount(10);
        var position = new MessagePosition(DateTimeOffset.UtcNow, 60);
        foreach (var cursor in new[] {
            new InboxCursor(1, "history", 2, "staff", ConversationId: 1, Position: position),
            new InboxCursor(1, "history", 1, "other", ConversationId: 1, Position: position),
            new InboxCursor(1, "history", 1, "staff", ConversationId: 2, Position: position),
            new InboxCursor(1, "sync", 1, "staff", ConversationId: 1, Position: position) })
            (await service.WindowAsync(1, codec.Encode(cursor), 50, default)).IsSuccess.Should().BeFalse();
        (await service.WindowAsync(1, "forged", 50, default)).IsSuccess.Should().BeFalse();
        (await service.WindowAsync(1, new string('x', 4097), 50, default)).IsSuccess.Should().BeFalse();
        (await service.ChangesAsync(codec.Encode(new(1, "sync", 1, "other")), 100, default)).IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task CurrentPermissionMembershipAndStampOverrideOldTokenGrants()
    {
        await using var db = await InboxWindowTests.SeedAsync(0);
        var permission = new Permission { Name = PermissionNames.NotificationViewOwn };
        db.Add(permission); await db.SaveChangesAsync();
        db.RolePermissions.Add(new() { RoleId = "instructor", PermissionId = permission.Id }); await db.SaveChangesAsync();
        var repo = new InboxRepository(db, Options.Create(new InboxOptions()), TimeProvider.System);
        var access = await repo.GetAccessAsync(new(1, "staff"), null, default);
        access!.Messaging.Should().BeFalse(); access.Notifications.Should().BeTrue();
        (await repo.GetAccessAsync(new(2, "staff"), null, default)).Should().BeNull();
        (await repo.GetAccessAsync(new(1, "staff"), "wrong-security-stamp", default)).Should().BeNull();
        var membership = await db.UserSchoolRoles.SingleAsync(); membership.IsActive = false; await db.SaveChangesAsync();
        (await repo.GetAccessAsync(new(1, "staff"), null, default)).Should().BeNull();
    }

    private sealed class User : ICurrentUserService
    {
        public string? UserId => "staff";
        public string? Username => "staff";
        public int? ActiveSchoolId => 1;
        public string PreferredLanguage => "ar";
        public bool IsAuthenticated => true;
        public bool IsGlobalAdmin() => false;
        public bool IsSchoolScopedRole() => true;
        public bool IsInRole(string role) => role == RoleNames.Instructor;
        public bool HasPermission(string permission) => true;
        public IEnumerable<string> GetRoles() => [RoleNames.Instructor];
        public IEnumerable<string> GetPermissions() => [PermissionNames.MessagingViewOwn];
    }
}
