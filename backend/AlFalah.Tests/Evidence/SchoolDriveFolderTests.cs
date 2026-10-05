using AlFalah.Application.Common;
using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Application.Storage;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Repositories;
using AlFalah.Infrastructure.Services;
using AlFalah.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AlFalah.Tests.Evidence;

public sealed class SchoolDriveFolderTests
{
    private static async Task<Reader> Setup(TeacherDriveHarness h)
    {
        var school = await h.Context.Schools.SingleAsync(x => x.Id == TeacherDriveHarness.SchoolId);
        school.ManagerUserId = TeacherDriveHarness.ManagerUserId;
        var role = new ApplicationRole { Id = "picker-manager", Name = RoleNames.SchoolManager };
        h.Context.Roles.Add(role);
        h.Context.UserSchoolRoles.Add(new() { SchoolId = school.Id, UserId = TeacherDriveHarness.ManagerUserId, RoleId = role.Id, Role = role, School = school });
        var connection = await h.Context.SchoolGoogleDrives.SingleAsync(); connection.IsEnabled = false; connection.SharedDriveId = null;
        await h.Context.SaveChangesAsync();
        h.Drive.AddFolder("account-root", "My Drive");
        h.Drive.MoveExternally(TeacherDriveHarness.SchoolRootFolderId, "account-root");
        h.Drive.AddFile("account-document", "ملف الحساب.pdf", "account-root");
        return new(h.Drive);
    }
    private static SchoolDriveFolderService Service(TeacherDriveHarness h, Reader reader, bool teacher = false) =>
        new(new SchoolDriveSetupRepository(h.Context), teacher ? TeacherDriveHarness.TeacherA() : TeacherDriveHarness.Manager(), reader);

    [Fact]
    public async Task Disabled_connection_can_browse_account_files_and_nested_folders_without_enabling_or_writes()
    {
        await using var h = await TeacherDriveHarness.CreateAsync(); var reader = await Setup(h); var service = Service(h, reader);
        var root = await service.BrowseAsync(new());
        root.IsAccountRoot.Should().BeTrue(); root.CanSelectCurrent.Should().BeFalse();
        root.Items.Should().Contain(x => !x.IsFolder && !x.CanSelect && x.Name == "ملف الحساب.pdf");
        var children = await service.BrowseAsync(new(TeacherDriveHarness.SchoolRootFolderId));
        children.CanSelectCurrent.Should().BeTrue(); children.Breadcrumbs.Select(x => x.Name).Should().Equal("ملفاتي", "ملفات الإنجاز");
        var selected = await service.ValidateRootAsync(TeacherDriveHarness.SchoolRootFolderId);
        selected.Name.Should().Be("ملفات الإنجاز");
        (await h.Context.SchoolGoogleDrives.SingleAsync()).IsEnabled.Should().BeFalse();
        h.Drive.Uploads.Should().BeEmpty(); h.Drive.Trashed.Should().BeEmpty();
    }
    [Fact]
    public async Task Pending_consent_returns_a_business_error_before_any_Drive_request()
    {
        await using var h = await TeacherDriveHarness.CreateAsync(); var reader = await Setup(h);
        (await h.Context.SchoolGoogleDrives.SingleAsync()).ProtectedCredential = "";
        await h.Context.SaveChangesAsync();
        await Service(h, reader).Invoking(s => s.BrowseAsync(new())).Should().ThrowAsync<BusinessRuleException>();
        reader.Calls.Should().Be(0);
    }
    [Fact]
    public async Task Teacher_and_revoked_manager_are_denied_before_Drive_and_after_external_IO()
    {
        await using var h = await TeacherDriveHarness.CreateAsync(); var reader = await Setup(h);
        await Service(h, reader, true).Invoking(s => s.BrowseAsync(new())).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        reader.Calls.Should().Be(0);
        reader.AfterList = async () => { (await h.Context.UserSchoolRoles.SingleAsync()).IsActive = false; await h.Context.SaveChangesAsync(); };
        await Service(h, reader).Invoking(s => s.BrowseAsync(new())).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
    }
    [Fact]
    public async Task Foreign_school_roots_are_hidden_and_direct_IDs_or_overlapping_roots_are_rejected()
    {
        await using var h = await TeacherDriveHarness.CreateAsync(); var reader = await Setup(h);
        h.Drive.AddFolder("other-school", "مدرسة أخرى", "account-root").AddFolder("other-child", "بيانات مدرسة أخرى", "other-school");
        h.Context.SchoolGoogleDrives.Add(new() { SchoolId = TeacherDriveHarness.OtherSchoolId, RootFolderId = "other-school", IsEnabled = false });
        await h.Context.SaveChangesAsync(); var s = Service(h, reader);
        (await s.BrowseAsync(new())).Items.Should().NotContain(x => x.ItemId == "other-school");
        await s.Invoking(x => x.BrowseAsync(new("other-child"))).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        await s.Invoking(x => x.ValidateRootAsync("other-school")).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        h.Drive.MoveExternally("other-school", TeacherDriveHarness.SchoolRootFolderId);
        await s.Invoking(x => x.ValidateRootAsync(TeacherDriveHarness.SchoolRootFolderId)).Should().ThrowAsync<ArgumentException>();
    }
    [Theory]
    [InlineData("root")]
    [InlineData("account-document")]
    [InlineData("missing-folder")]
    [InlineData("folder-a")]
    [InlineData("outside-root")]
    public async Task Account_root_file_missing_and_teacher_grant_or_root_excluding_grants_cannot_be_selected(string id)
    {
        await using var h = await TeacherDriveHarness.CreateAsync(); var reader = await Setup(h);
        await Service(h, reader).Invoking(x => x.ValidateRootAsync(id)).Should().ThrowAsync<Exception>();
    }
    [Fact]
    public async Task Trashed_read_only_folders_and_changed_connection_cannot_be_selected()
    {
        await using var h = await TeacherDriveHarness.CreateAsync(); var reader = await Setup(h); var s = Service(h, reader);
        reader.ReadOnly.Add(TeacherDriveHarness.SchoolRootFolderId);
        await s.Invoking(x => x.ValidateRootAsync(TeacherDriveHarness.SchoolRootFolderId)).Should().ThrowAsync<ArgumentException>();
        reader.ReadOnly.Clear(); h.Drive.TrashExternally(TeacherDriveHarness.SchoolRootFolderId);
        await s.Invoking(x => x.ValidateRootAsync(TeacherDriveHarness.SchoolRootFolderId)).Should().ThrowAsync<KeyNotFoundException>();
        h.Drive.RestoreExternally(TeacherDriveHarness.SchoolRootFolderId);
        reader.AfterList = async () => { (await h.Context.SchoolGoogleDrives.SingleAsync()).UpdatedAtUtc = DateTimeOffset.UtcNow.AddSeconds(10); await h.Context.SaveChangesAsync(); };
        await s.Invoking(x => x.BrowseAsync(new())).Should().ThrowAsync<StorageConflictException>();
    }
    [Fact]
    public async Task Search_is_bounded_and_queries_the_current_folder_only()
    {
        await using var h = await TeacherDriveHarness.CreateAsync(); var reader = await Setup(h); var s = Service(h, reader);
        var r = await s.BrowseAsync(new(Search: "ملف الحساب")); r.Items.Should().ContainSingle();
        reader.LastRequest!.ParentFolderId.Should().Be("account-root"); reader.LastRequest.PageSize.Should().Be(50);
        await s.Invoking(x => x.BrowseAsync(new(Search: new string('x', 201)))).Should().ThrowAsync<ArgumentException>();
        await s.Invoking(x => x.BrowseAsync(new("https://example.test/folder"))).Should().ThrowAsync<ArgumentException>();
    }
    [Fact]
    public async Task OAuth_client_can_be_saved_before_a_root_and_is_not_reported_as_connected_or_enabled()
    {
        await using var h = await TeacherDriveHarness.CreateAsync(connectSchoolDrive: false, grantFolders: false);
        var dto = await h.SchoolDriveService(TeacherDriveHarness.Manager()).ConfigureForCurrentSchoolAsync(new(
            GoogleDriveCredentialType.OAuthRefreshToken, "test@example.test", null, null, "id.apps.googleusercontent.com", "fixture-secret", null,
            null, "", "", false));
        dto.IsConfigured.Should().BeTrue(); dto.HasStoredOAuthClientSecret.Should().BeTrue(); dto.HasStoredCredential.Should().BeFalse(); dto.IsEnabled.Should().BeFalse();
        var stored = await h.Context.SchoolGoogleDrives.SingleAsync(); stored.ProtectedOAuthClientSecret.Should().NotContain("fixture-secret");
        await h.SchoolDriveService(TeacherDriveHarness.Manager()).Invoking(x => x.ConfigureForCurrentSchoolAsync(new(
            GoogleDriveCredentialType.OAuthRefreshToken, "test@example.test", null, null, "id.apps.googleusercontent.com", null, null, null, "", "", true)))
            .Should().ThrowAsync<InvalidOperationException>();
    }
    [Fact]
    public async Task Configure_validates_live_root_uses_its_real_name_and_prevents_raw_ID_bypass()
    {
        await using var h = await TeacherDriveHarness.CreateAsync(); var reader = await Setup(h); var folderService = Service(h, reader);
        var manager = TeacherDriveHarness.Manager();
        var service = new SchoolGoogleDriveService(h.Context, manager, h.ScopeGuard(manager), h.Audit(), h.Protector(), new NoTokens(), folderService);
        ConfigureSchoolGoogleDriveRequest Request(string id) => new(GoogleDriveCredentialType.ServiceAccount, "test@example.test", null, null, null, null, null,
            "ignored-drive", id, "client-supplied-name", true);
        var dto = await service.ConfigureForCurrentSchoolAsync(Request(TeacherDriveHarness.SchoolRootFolderId));
        dto.RootFolderDisplayName.Should().Be("ملفات الإنجاز"); dto.SharedDriveId.Should().BeNull();
        await service.Invoking(x => x.ConfigureForCurrentSchoolAsync(Request(TeacherDriveHarness.FolderA))).Should().ThrowAsync<ArgumentException>();
        (await h.Context.SchoolGoogleDrives.SingleAsync()).RootFolderId.Should().Be(TeacherDriveHarness.SchoolRootFolderId);
    }
    private sealed class NoTokens : AlFalah.Application.Interfaces.IGoogleDriveTokenService
    {
        public Task<string> GetAccessTokenAsync(int schoolId, CancellationToken ct = default) => throw new NotSupportedException();
        public void InvalidateCachedToken(int schoolId) { }
    }
    private sealed class Reader(FakeGoogleDrive drive) : IGoogleDriveSetupReader
    {
        public int Calls { get; private set; }
        public HashSet<string> ReadOnly { get; } = [];
        public Func<Task>? AfterList { get; set; }
        public GoogleDriveListRequest? LastRequest { get; private set; }
        public async Task<GoogleDriveFile?> FolderAsync(int school, string id, CancellationToken ct)
        {
            Calls++; var file = await drive.GetFileAsync(school, id == "root" ? "account-root" : id, ct);
            return file is null ? null : file with { CanAddChildren = !ReadOnly.Contains(file.Id) };
        }
        public async Task<GoogleDriveFileList> ChildrenAsync(int school, GoogleDriveListRequest request, CancellationToken ct)
        {
            Calls++; LastRequest = request;
            var page = await drive.ListChildrenAsync(school, request, ct);
            if (AfterList != null) await AfterList();
            return page with { Files = page.Files.Select(x => x with { CanAddChildren = x.IsFolder && !ReadOnly.Contains(x.Id) }).ToList() };
        }
    }
}
