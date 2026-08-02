using AlFalah.Application.Common.Exceptions;
using AlFalah.Tests.TestDoubles;
using FluentAssertions;
using Xunit;

namespace AlFalah.Tests.Evidence;

/// <summary>
/// "Each person sees ONLY the folder they were granted."
///
/// Under OneDrive, Microsoft Graph enforced this for us because the request carried the
/// teacher's own token. On Google Drive the application holds one school-wide credential that
/// can read every teacher's folder, so <c>TeacherDriveFolderGuard</c> is the only barrier
/// left. These tests attack it from every direction a request can reach it.
/// </summary>
public sealed class TeacherDriveIsolationTests
{
    [Fact]
    public async Task A_Teacher_Sees_Their_Own_Folder_By_Default()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();

        var page = await harness.BrowserService(TeacherDriveHarness.TeacherA()).ListAsync(new(null, null, null, null, null));

        page.Items.Select(x => x.ItemId).Should().BeEquivalentTo([TeacherDriveHarness.FolderASub, "a-existing.pdf"]);
        page.Items.Should().NotContain(x => x.ItemId == "b-secret.pdf");
        // Folders first, matching the real orderBy=folder,name contract.
        page.Items[0].IsFolder.Should().BeTrue();
    }

    [Fact]
    public async Task A_Teacher_Cannot_List_Another_Teachers_Folder()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();

        var act = () => harness.BrowserService(TeacherDriveHarness.TeacherA())
            .ListAsync(new(TeacherDriveHarness.FolderB, null, null, null, null));

        await act.Should().ThrowAsync<TeacherDriveAccessDeniedException>();
    }

    [Fact]
    public async Task A_Teacher_Cannot_Read_A_File_Inside_Another_Teachers_Folder()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        var browser = harness.BrowserService(TeacherDriveHarness.TeacherA());

        await browser.Invoking(x => x.GetItemAsync("b-secret.pdf")).Should().ThrowAsync<TeacherDriveAccessDeniedException>();
        await browser.Invoking(x => x.DownloadAsync("b-secret.pdf")).Should().ThrowAsync<TeacherDriveAccessDeniedException>();
        await browser.Invoking(x => x.GetBreadcrumbAsync("b-secret.pdf")).Should().ThrowAsync<TeacherDriveAccessDeniedException>();
    }

    [Fact]
    public async Task A_Teacher_Cannot_Climb_To_The_School_Root_Or_An_Unassigned_Sibling()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        var browser = harness.BrowserService(TeacherDriveHarness.TeacherA());

        // The root is the parent of the granted folder, so this is the most likely probe.
        await browser.Invoking(x => x.ListAsync(new(TeacherDriveHarness.SchoolRootFolderId, null, null, null, null)))
            .Should().ThrowAsync<TeacherDriveAccessDeniedException>();
        await browser.Invoking(x => x.ListAsync(new(TeacherDriveHarness.FolderUnassigned, null, null, null, null)))
            .Should().ThrowAsync<TeacherDriveAccessDeniedException>();
        await browser.Invoking(x => x.ListAsync(new(TeacherDriveHarness.OutsideRootFolderId, null, null, null, null)))
            .Should().ThrowAsync<TeacherDriveAccessDeniedException>();
    }

    [Fact]
    public async Task A_Teacher_Can_Browse_Nested_Folders_Inside_Their_Own_Grant()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        var browser = harness.BrowserService(TeacherDriveHarness.TeacherA());

        var page = await browser.ListAsync(new(TeacherDriveHarness.FolderASub, null, null, null, null));

        page.Items.Should().ContainSingle(x => x.ItemId == "a-nested.pdf");
        (await browser.DownloadAsync("a-nested.pdf")).FileName.Should().Be("دليل متداخل.pdf");
    }

    [Fact]
    public async Task The_Breadcrumb_Stops_At_The_Granted_Root()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();

        var trail = await harness.BrowserService(TeacherDriveHarness.TeacherA())
            .GetBreadcrumbAsync(TeacherDriveHarness.FolderASub);

        // The teacher must not learn the name of the school root above their folder.
        trail.Select(x => x.ItemId).Should().Equal([TeacherDriveHarness.FolderA, TeacherDriveHarness.FolderASub]);
    }

    [Fact]
    public async Task A_Trashed_Item_Is_No_Longer_Reachable_By_Id()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        harness.Drive.TrashExternally("a-existing.pdf");

        // Drive still answers for a trashed id, so without an explicit check a "deleted" file
        // would stay downloadable through a direct request.
        await harness.BrowserService(TeacherDriveHarness.TeacherA())
            .Invoking(x => x.DownloadAsync("a-existing.pdf"))
            .Should().ThrowAsync<TeacherDriveAccessDeniedException>();
    }

    [Fact]
    public async Task A_Folder_Moved_Out_Of_The_Grant_Stops_Being_Reachable()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        var browser = harness.BrowserService(TeacherDriveHarness.TeacherA());
        await browser.ListAsync(new(TeacherDriveHarness.FolderASub, null, null, null, null));

        // An administrator drags the subfolder out of the teacher's folder in Drive itself.
        harness.Drive.MoveExternally(TeacherDriveHarness.FolderASub, TeacherDriveHarness.OutsideRootFolderId);

        // Containment is re-proved from Drive on every request, so the next one denies.
        await browser.Invoking(x => x.ListAsync(new(TeacherDriveHarness.FolderASub, null, null, null, null)))
            .Should().ThrowAsync<TeacherDriveAccessDeniedException>();
    }

    [Fact]
    public async Task A_Parent_Cycle_Denies_Instead_Of_Looping_Forever()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        // A corrupt parent graph must fail closed and terminate, not spin.
        harness.Drive.AddFolder("cycle-x", "X", "cycle-y");
        harness.Drive.AddFolder("cycle-y", "Y", "cycle-x");

        await harness.BrowserService(TeacherDriveHarness.TeacherA())
            .Invoking(x => x.ListAsync(new("cycle-x", null, null, null, null)))
            .Should().ThrowAsync<TeacherDriveAccessDeniedException>();
    }

    [Fact]
    public async Task A_Deep_Chain_Beyond_The_Inspection_Limit_Denies()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        // 200 links is far past the 64-ancestor ceiling, so the walk must give up and deny
        // rather than issue hundreds of Drive calls for one request.
        var previous = TeacherDriveHarness.FolderA;
        for (var i = 0; i < 200; i++)
        {
            harness.Drive.AddFolder($"deep-{i}", $"deep {i}", previous);
            previous = $"deep-{i}";
        }

        await harness.BrowserService(TeacherDriveHarness.TeacherA())
            .Invoking(x => x.ListAsync(new(previous, null, null, null, null)))
            .Should().ThrowAsync<TeacherDriveAccessDeniedException>();
    }

    [Fact]
    public async Task Requesting_A_File_As_A_Folder_Is_Rejected()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();

        await harness.BrowserService(TeacherDriveHarness.TeacherA())
            .Invoking(x => x.ListAsync(new("a-existing.pdf", null, null, null, null)))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task A_Non_Teacher_User_Reaches_Nothing()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        var manager = TeacherDriveHarness.Manager();

        var status = await harness.IdentityService(manager).GetStatusAsync();
        status.ConnectionState.Should().Be("NotATeacher");
        await harness.BrowserService(manager).Invoking(x => x.ListAsync(new(null, null, null, null, null)))
            .Should().ThrowAsync<TeacherDriveAccessDeniedException>();
    }

    [Fact]
    public async Task An_Unauthenticated_Caller_Reaches_Nothing()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();

        await harness.BrowserService(TeacherDriveHarness.Anonymous())
            .Invoking(x => x.ListAsync(new(null, null, null, null, null)))
            .Should().ThrowAsync<TeacherDriveAccessDeniedException>();
    }

    [Fact]
    public async Task A_Teacher_Whose_Session_Names_Another_School_Reaches_Nothing()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        // A stale token from before a transfer must not reach the old school's credential.
        var staleSession = TeacherDriveHarness.TeacherA(activeSchoolId: TeacherDriveHarness.OtherSchoolId);

        (await harness.IdentityService(staleSession).GetStatusAsync()).ConnectionState.Should().Be("NotATeacher");
        await harness.BrowserService(staleSession).Invoking(x => x.ListAsync(new(null, null, null, null, null)))
            .Should().ThrowAsync<TeacherDriveAccessDeniedException>();
    }

    [Fact]
    public async Task A_Teacher_Without_A_Grant_Is_Told_So_And_Reaches_Nothing()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync(grantFolders: false);
        var teacher = TeacherDriveHarness.TeacherA();

        var status = await harness.IdentityService(teacher).GetStatusAsync();
        status.IsSchoolDriveEnabled.Should().BeTrue();
        status.IsFolderAssigned.Should().BeFalse();
        status.ConnectionState.Should().Be("FolderNotAssigned");
        status.TeacherDisplayName.Should().Be("المعلم أ");

        await harness.BrowserService(teacher).Invoking(x => x.ListAsync(new(null, null, null, null, null)))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task When_The_School_Drive_Is_Disabled_Nobody_Reaches_Drive()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        await harness.ConnectSchoolDriveAsync(isEnabled: false);
        var teacher = TeacherDriveHarness.TeacherA();

        (await harness.IdentityService(teacher).GetStatusAsync()).ConnectionState.Should().Be("SchoolNotConfigured");
        await harness.BrowserService(teacher).Invoking(x => x.ListAsync(new(null, null, null, null, null)))
            .Should().ThrowAsync<TeacherDriveAccessDeniedException>();
    }

    [Fact]
    public async Task Search_Only_Ever_Searches_Inside_The_Granted_Folder()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();

        // "دليل" matches files in both teachers' folders; the query is scoped to the parent,
        // so teacher B's file can never appear in teacher A's results.
        var page = await harness.BrowserService(TeacherDriveHarness.TeacherA())
            .ListAsync(new(null, "دليل", null, null, null));

        page.Items.Should().ContainSingle().Which.ItemId.Should().Be("a-existing.pdf");
    }

    [Fact]
    public async Task Each_Teacher_Only_Sees_Their_Own_Recent_Files()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        await harness.UploadAsync(TeacherDriveHarness.TeacherA(), taskId: 1, fileName: "أ.pdf", requestId: "a-1");
        await harness.UploadAsync(TeacherDriveHarness.TeacherB(), taskId: 1, fileName: "ب.pdf", requestId: "b-1");

        var recentForA = await harness.BrowserService(TeacherDriveHarness.TeacherA()).GetRecentAsync();

        recentForA.Should().ContainSingle().Which.Name.Should().Be("أ.pdf");
    }

    [Fact]
    public async Task Opening_A_Folder_Is_Audited_Against_The_Right_Teacher()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();

        await harness.BrowserService(TeacherDriveHarness.TeacherA()).ListAsync(new(null, null, null, null, null));

        var log = harness.Context.AuditLogs.Single(x => x.Action == "TeacherDrive.FolderOpened");
        log.EntityId.Should().Be(TeacherDriveHarness.FolderA);
        log.SchoolId.Should().Be(TeacherDriveHarness.SchoolId);
    }
}
