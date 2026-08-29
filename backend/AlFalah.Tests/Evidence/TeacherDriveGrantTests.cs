using AlFalah.Application.Common;
using AlFalah.Domain.Enums;
using AlFalah.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AlFalah.Tests.Evidence;

/// <summary>
/// The manager side of the flow: connecting the school's Google Drive and granting each
/// teacher a folder. Because the application now reaches Drive with one school-wide
/// credential, a bad grant is the only way a teacher could see files that are not theirs —
/// so every rejection below is a security boundary, not a validation nicety.
/// </summary>
public sealed class TeacherDriveGrantTests
{
    [Fact]
    public async Task Manager_Connects_The_School_Drive_And_The_Credential_Is_Never_Returned()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync(connectSchoolDrive: false, grantFolders: false);

        var settings = await harness.ConnectSchoolDriveAsync();

        settings.IsConfigured.Should().BeTrue();
        settings.IsEnabled.Should().BeTrue();
        settings.HasStoredCredential.Should().BeTrue();
        settings.CredentialType.Should().Be(GoogleDriveCredentialType.ServiceAccount);
        settings.RootFolderId.Should().Be(TeacherDriveHarness.SchoolRootFolderId);

        // The DTO has no field that could carry the key, and the stored value is ciphertext.
        var stored = await harness.Context.SchoolGoogleDrives.SingleAsync();
        stored.ProtectedCredential.Should().NotContain("private_key");
        stored.ProtectedCredential.Should().NotBe(TeacherDriveHarness.ServiceAccountJson);
        harness.Protector().Unprotect(stored.ProtectedCredential).Should().Be(TeacherDriveHarness.ServiceAccountJson.Trim());
    }

    [Fact]
    public async Task Reconfiguring_Without_Resending_The_Key_Keeps_The_Stored_Credential()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync(grantFolders: false);

        // Renaming the root folder must not force the manager to re-paste the JSON key.
        var settings = await harness.SchoolDriveService(TeacherDriveHarness.Manager())
            .ConfigureForCurrentSchoolAsync(new(
                GoogleDriveCredentialType.ServiceAccount, "evidence@alfalah.edu.sa",
                ServiceAccountJson: null, ImpersonatedUserEmail: null,
                OAuthClientId: null, OAuthClientSecret: null, OAuthRefreshToken: null,
                TeacherDriveHarness.SharedDriveId, TeacherDriveHarness.SchoolRootFolderId,
                "أدلة الإنجاز ١٤٤٧", IsEnabled: true));

        settings.HasStoredCredential.Should().BeTrue();
        settings.RootFolderDisplayName.Should().Be("أدلة الإنجاز ١٤٤٧");
        harness.Protector().Unprotect((await harness.Context.SchoolGoogleDrives.SingleAsync()).ProtectedCredential)
            .Should().Be(TeacherDriveHarness.ServiceAccountJson.Trim());
    }

    [Fact]
    public async Task ServiceAccount_Without_SharedDrive_Or_Impersonation_Is_Accepted()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync(connectSchoolDrive: false, grantFolders: false);

        // SharedDriveId and ImpersonatedUserEmail are both optional: a service account may be
        // pointed at an ordinary My Drive folder shared with it. Whether an UPLOAD then succeeds
        // is Google's call (see the storageQuotaExceeded mapping in GoogleDriveClientTests), not
        // something this validation pre-empts.
        var settings = await harness.ConnectSchoolDriveAsync(sharedDriveId: null);

        settings.IsConfigured.Should().BeTrue();
        settings.SharedDriveId.Should().BeNull();
        settings.ImpersonatedUserEmail.Should().BeNull();
        settings.HasStoredCredential.Should().BeTrue();
    }

    [Fact]
    public async Task A_My_Drive_Configuration_Grants_Folders_With_An_Empty_DriveId()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync(connectSchoolDrive: false, grantFolders: false);
        await harness.ConnectSchoolDriveAsync(sharedDriveId: null);

        var grant = await harness.GrantFolderAsync(TeacherDriveHarness.TeacherAId, TeacherDriveHarness.FolderA);

        // Empty rather than a sentinel: everything downstream treats blank as "no shared drive"
        // and omits the driveId/corpora parameters from the Drive call.
        grant.DriveId.Should().BeEmpty();
    }

    [Fact]
    public async Task A_My_Drive_Configuration_Still_Browses_And_Uploads_Through_The_Same_Path()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync(connectSchoolDrive: false, grantFolders: false);
        await harness.ConnectSchoolDriveAsync(sharedDriveId: null);
        await harness.GrantFolderAsync(TeacherDriveHarness.TeacherAId, TeacherDriveHarness.FolderA);
        var teacher = TeacherDriveHarness.TeacherA();

        var page = await harness.BrowserService(teacher).ListAsync(new(null, null, null, null, null));
        page.Items.Should().NotBeEmpty();

        await harness.UploadAsync(teacher, taskId: 1);

        // No shared-drive id reaches Drive for a My Drive configuration.
        harness.Drive.Uploads.Should().ContainSingle().Which.SharedDriveId.Should().BeNull();
        (await harness.Context.TeacherTaskStatuses.SingleAsync()).ActiveFilesCount.Should().Be(1);
    }

    [Fact]
    public async Task An_Invalid_Impersonation_Email_Is_Still_Rejected()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync(connectSchoolDrive: false, grantFolders: false);

        // Optional does not mean unvalidated: a malformed value would be sent to Google as the
        // JWT `sub` claim and fail there with an opaque invalid_grant.
        var act = () => harness.SchoolDriveService(TeacherDriveHarness.Manager())
            .ConfigureForCurrentSchoolAsync(new(
                GoogleDriveCredentialType.ServiceAccount, "evidence@alfalah.edu.sa",
                TeacherDriveHarness.ServiceAccountJson, ImpersonatedUserEmail: "not-an-email",
                null, null, null, null,
                TeacherDriveHarness.SchoolRootFolderId, "ملفات الإنجاز", true));

        // Matched on the ASCII part: the Arabic wording carries diacritics, so asserting on it
        // would break on an invisible character rather than on a behaviour change.
        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*Impersonated User*");
    }

    [Fact]
    public async Task Malformed_ServiceAccount_Key_Is_Rejected_At_Configuration_Time()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync(connectSchoolDrive: false, grantFolders: false);

        var act = () => harness.SchoolDriveService(TeacherDriveHarness.Manager())
            .ConfigureForCurrentSchoolAsync(new(
                GoogleDriveCredentialType.ServiceAccount, "evidence@alfalah.edu.sa",
                ServiceAccountJson: "{ \"client_email\": \"a@b.c\" }", ImpersonatedUserEmail: null,
                null, null, null, TeacherDriveHarness.SharedDriveId,
                TeacherDriveHarness.SchoolRootFolderId, "ملفات الإنجاز", true));

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*private_key*");
    }

    [Fact]
    public async Task RefreshToken_Connection_Requires_Client_Id_And_Secret()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync(connectSchoolDrive: false, grantFolders: false);
        var service = harness.SchoolDriveService(TeacherDriveHarness.Manager());

        var act = () => service.ConfigureForCurrentSchoolAsync(new(
            GoogleDriveCredentialType.OAuthRefreshToken, "evidence@alfalah.edu.sa",
            null, null,
            OAuthClientId: null, OAuthClientSecret: "secret", OAuthRefreshToken: "1//token",
            null, TeacherDriveHarness.SchoolRootFolderId, "ملفات الإنجاز", true));

        await act.Should().ThrowAsync<InvalidOperationException>();

        var settings = await service.ConfigureForCurrentSchoolAsync(new(
            GoogleDriveCredentialType.OAuthRefreshToken, "evidence@alfalah.edu.sa",
            null, null, "client.apps.googleusercontent.com", "secret", "1//token",
            null, TeacherDriveHarness.SchoolRootFolderId, "ملفات الإنجاز", true));

        settings.CredentialType.Should().Be(GoogleDriveCredentialType.OAuthRefreshToken);
        settings.HasStoredCredential.Should().BeTrue();
        var stored = await harness.Context.SchoolGoogleDrives.SingleAsync();
        harness.Protector().Unprotect(stored.ProtectedCredential).Should().Be("1//token");
        harness.Protector().Unprotect(stored.ProtectedOAuthClientSecret!).Should().Be("secret");
        // Switching away from a service account must clear the impersonation field, otherwise a
        // stale value would silently apply to the new grant type.
        stored.ImpersonatedUserEmail.Should().BeNull();
    }

    [Fact]
    public async Task Replacing_OAuth_Client_Settings_Clears_The_Previous_Refresh_Token()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync(connectSchoolDrive: false, grantFolders: false);
        var service = harness.SchoolDriveService(TeacherDriveHarness.Manager());

        await service.ConfigureForCurrentSchoolAsync(new(
            GoogleDriveCredentialType.OAuthRefreshToken, "evidence@alfalah.edu.sa",
            null, null, "old-client.apps.googleusercontent.com", "old-secret", "1//old-token",
            null, TeacherDriveHarness.SchoolRootFolderId, "ملفات الإنجاز", true));

        var settings = await service.ConfigureForCurrentSchoolAsync(new(
            GoogleDriveCredentialType.OAuthRefreshToken, "evidence@alfalah.edu.sa",
            null, null, "new-client.apps.googleusercontent.com", "new-secret", null,
            null, TeacherDriveHarness.SchoolRootFolderId, "ملفات الإنجاز", true));

        settings.HasStoredCredential.Should().BeFalse();
        var stored = await harness.Context.SchoolGoogleDrives.SingleAsync();
        stored.ProtectedCredential.Should().BeEmpty();
        harness.Protector().Unprotect(stored.ProtectedOAuthClientSecret!).Should().Be("new-secret");
    }

    [Fact]
    public async Task Moderator_Cannot_Read_Or_Configure_The_School_Drive()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        var service = harness.SchoolDriveService(TeacherDriveHarness.Moderator());

        await service.Invoking(x => x.GetForCurrentSchoolAsync())
            .Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        await harness.Invoking(x => x.ConnectSchoolDriveAsync()).Should().NotThrowAsync();
    }

    [Fact]
    public async Task Granting_A_Folder_Takes_The_Drive_Id_From_The_School_Not_The_Request()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync(grantFolders: false);

        var grant = await harness.GrantFolderAsync(TeacherDriveHarness.TeacherAId, TeacherDriveHarness.FolderA);

        grant.RootItemId.Should().Be(TeacherDriveHarness.FolderA);
        grant.DriveId.Should().Be(TeacherDriveHarness.SharedDriveId);
        grant.SchoolId.Should().Be(TeacherDriveHarness.SchoolId);
        grant.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Manager_Browses_Only_Folders_And_Sees_Their_Assignment_State()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        var service = harness.MappingService(TeacherDriveHarness.Manager());

        var page = await service.BrowseFoldersAsync(
            TeacherDriveHarness.TeacherAId, new(ParentItemId: null, PageToken: null));

        page.IsSchoolRoot.Should().BeTrue();
        page.Folders.Select(x => x.ItemId).Should().BeEquivalentTo(
            TeacherDriveHarness.FolderA,
            TeacherDriveHarness.FolderB,
            TeacherDriveHarness.FolderUnassigned);
        page.Folders.Should().NotContain(x => x.ItemId == "a-existing.pdf");
        page.Folders.Single(x => x.ItemId == TeacherDriveHarness.FolderA)
            .IsAssignedToCurrentTeacher.Should().BeTrue();
        page.Folders.Single(x => x.ItemId == TeacherDriveHarness.FolderB)
            .AssignedTeacherName.Should().NotBeNullOrWhiteSpace();
        page.Folders.Single(x => x.ItemId == TeacherDriveHarness.FolderUnassigned)
            .IsAssigned.Should().BeFalse();
    }

    [Fact]
    public async Task Manager_Can_Browse_Nested_Folders_But_Not_Outside_The_School_Root()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        var service = harness.MappingService(TeacherDriveHarness.Manager());

        var nested = await service.BrowseFoldersAsync(
            TeacherDriveHarness.TeacherAId, new(TeacherDriveHarness.FolderA, null));

        nested.IsSchoolRoot.Should().BeFalse();
        nested.CurrentFolderId.Should().Be(TeacherDriveHarness.FolderA);
        nested.Folders.Should().ContainSingle(x => x.ItemId == TeacherDriveHarness.FolderASub);

        await service.Invoking(x => x.BrowseFoldersAsync(
                TeacherDriveHarness.TeacherAId, new(TeacherDriveHarness.OutsideRootFolderId, null)))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Granting_A_Folder_Outside_The_School_Root_Is_Rejected()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync(grantFolders: false);

        // The school credential can see this folder, but the school does not own it. Granting it
        // would hand a teacher a window onto unrelated files.
        var act = () => harness.GrantFolderAsync(TeacherDriveHarness.TeacherAId, TeacherDriveHarness.OutsideRootFolderId);

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*داخل المجلد الرئيسي*");
        harness.Context.TeacherDriveFolders.Should().BeEmpty();
    }

    [Fact]
    public async Task Granting_The_School_Root_Itself_Is_Rejected()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync(grantFolders: false);

        // The root contains every teacher's folder, so granting it would expose all of them.
        var act = () => harness.GrantFolderAsync(TeacherDriveHarness.TeacherAId, TeacherDriveHarness.SchoolRootFolderId);

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*المجلد الرئيسي*");
    }

    [Fact]
    public async Task Granting_A_File_Or_A_Missing_Folder_Is_Rejected()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync(grantFolders: false);

        await harness.Invoking(x => x.GrantFolderAsync(TeacherDriveHarness.TeacherAId, "a-existing.pdf"))
            .Should().ThrowAsync<InvalidOperationException>();
        await harness.Invoking(x => x.GrantFolderAsync(TeacherDriveHarness.TeacherAId, "does-not-exist"))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task The_Same_Folder_Cannot_Be_Granted_To_Two_Teachers()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();

        // Sharing one folder would let each teacher read — and delete — the other's evidence.
        var act = () => harness.GrantFolderAsync(TeacherDriveHarness.TeacherBId, TeacherDriveHarness.FolderA);

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*لمعلم آخر*");
    }

    [Fact]
    public async Task Nested_Grants_Cannot_Overlap_Between_Teachers()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync(grantFolders: false);
        await harness.GrantFolderAsync(TeacherDriveHarness.TeacherAId, TeacherDriveHarness.FolderASub);

        var act = () => harness.GrantFolderAsync(TeacherDriveHarness.TeacherBId, TeacherDriveHarness.FolderA);

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*لمعلم آخر*");
    }

    [Fact]
    public async Task Revoked_Folder_Can_Be_Assigned_To_Another_Teacher()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        var service = harness.MappingService(TeacherDriveHarness.Manager());
        await service.RevokeAsync(TeacherDriveHarness.TeacherAId);

        var grant = await service.UpsertAsync(
            TeacherDriveHarness.TeacherBId, new(TeacherDriveHarness.FolderA));

        grant.RootItemId.Should().Be(TeacherDriveHarness.FolderA);
        grant.TeacherId.Should().Be(TeacherDriveHarness.TeacherBId);
        grant.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task A_Nested_Subfolder_Of_The_School_Root_Is_A_Valid_Grant()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync(grantFolders: false);

        var grant = await harness.GrantFolderAsync(TeacherDriveHarness.TeacherAId, TeacherDriveHarness.FolderASub);

        grant.RootItemId.Should().Be(TeacherDriveHarness.FolderASub);
    }

    [Fact]
    public async Task Granting_Requires_The_School_Drive_To_Be_Connected()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync(connectSchoolDrive: false, grantFolders: false);

        var act = () => harness.GrantFolderAsync(TeacherDriveHarness.TeacherAId, TeacherDriveHarness.FolderA);

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*حساب Google Drive الخاص بالمدرسة*");
    }

    [Fact]
    public async Task A_Manager_From_Another_School_Cannot_Read_A_Teachers_Folder_Grant()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        var foreignManager = TeacherDriveHarness.Manager(activeSchoolId: TeacherDriveHarness.OtherSchoolId);

        // Reading a grant returns DriveId/RootItemId — a real Google folder identifier, not just
        // a display label — so cross-school admin lookups must be denied, not merely filtered.
        var act = () => harness.MappingService(foreignManager).FindForTeacherAsync(TeacherDriveHarness.TeacherAId);

        await act.Should().ThrowAsync<UnauthorizedSchoolAccessException>();
    }

    [Fact]
    public async Task A_Manager_From_Another_School_Cannot_Grant_A_Folder()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync(grantFolders: false);
        var foreignManager = TeacherDriveHarness.Manager(activeSchoolId: TeacherDriveHarness.OtherSchoolId);

        var act = () => harness.MappingService(foreignManager)
            .UpsertAsync(TeacherDriveHarness.TeacherAId, new(TeacherDriveHarness.FolderA));

        await act.Should().ThrowAsync<UnauthorizedSchoolAccessException>();
    }

    [Fact]
    public async Task Revoking_A_Grant_Blocks_Access_But_Keeps_The_Evidence()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        var teacher = TeacherDriveHarness.TeacherA();
        await harness.UploadAsync(teacher, taskId: 1);

        await harness.MappingService(TeacherDriveHarness.Manager()).RevokeAsync(TeacherDriveHarness.TeacherAId);

        // Access is gone…
        var status = await harness.IdentityService(teacher).GetStatusAsync();
        status.IsFolderAssigned.Should().BeFalse();
        status.ConnectionState.Should().Be("FolderNotAssigned");
        await harness.BrowserService(teacher).Invoking(x => x.ListAsync(new(null, null, null, null, null)))
            .Should().ThrowAsync<InvalidOperationException>();

        // …but what the teacher already submitted still counts in the matrix.
        (await harness.Context.TeacherEvidenceSubmissions.CountAsync(x => !x.IsDeleted)).Should().Be(1);
        var cell = await harness.Context.TeacherTaskStatuses.SingleAsync();
        cell.ActiveFilesCount.Should().Be(1);
    }

    [Fact]
    public async Task Every_Grant_And_Connection_Change_Is_Audited()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();

        var actions = await harness.Context.AuditLogs.Select(x => x.Action).ToListAsync();

        actions.Should().Contain("SchoolGoogleDrive.Configured");
        actions.Should().Contain("TeacherDriveFolder.Granted");
    }
}
