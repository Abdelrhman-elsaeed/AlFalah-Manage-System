using AlFalah.Application.Storage;
using AlFalah.Domain.Entities.Storage;
using AlFalah.Infrastructure.Repositories;
using AlFalah.Infrastructure.Services;
using AlFalah.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace AlFalah.Tests.Storage;

public sealed class StorageSetupTests
{
    private static StorageSetupService Service(TeacherDriveHarness h)
    {
        var actor = TeacherDriveHarness.Manager();
        var scopes = new StorageRepository(h.Context);
        return new(new StorageSetupRepository(h.Context), scopes, new StorageLibraryRepository(h.Context),
            new StorageAuthorizationService(scopes, actor, new StorageDriveBoundary(new TeacherDriveFolderGuard(h.Drive)), TimeProvider.System),
            actor, new GoogleStorageProvider(h.Drive), h.MappingService(actor),
            Options.Create(new StorageOptions { AdministrationEnabled = true, ReadModelEnabled = true }));
    }

    [Fact]
    public async Task Setup_adopts_existing_library_and_repeated_apply_creates_no_duplicates()
    {
        await using var h = await StorageLibraryTests.Setup();
        h.Drive.AddFolder("existing-library", "مكتبة المدرسة", TeacherDriveHarness.SchoolRootFolderId);
        await h.MappingService(TeacherDriveHarness.Manager()).RevokeAsync(TeacherDriveHarness.TeacherBId);
        var service = Service(h);

        var preview = await service.PlanAsync();
        preview.Folders.Single(f => f.Key == "library").State.Should().Be("Exists");
        preview.Teachers.Single(t => t.TeacherId == TeacherDriveHarness.TeacherBId).State.Should().Be("Exists");

        var first = await service.ApplyAsync(true, true);
        first.Warnings.Should().BeEmpty();
        first.Plan.Teachers.Should().OnlyContain(t => t.State == "Linked");
        (await h.Context.TeacherDriveFolders.SingleAsync(m => m.TeacherId == TeacherDriveHarness.TeacherBId)).RootItemId
            .Should().Be(TeacherDriveHarness.FolderB);
        (await h.Context.StorageFolders.SingleAsync(f => f.Kind == AlFalah.Domain.Enums.StorageFolderKind.SchoolLibrary && f.ParentFolderId == null))
            .DriveItemId.Should().Be("existing-library");
        var rootChildren = (await h.Drive.ListChildrenAsync(TeacherDriveHarness.SchoolId,
            new(TeacherDriveHarness.SchoolRootFolderId, null, "name", 100, null, TeacherDriveHarness.SharedDriveId))).Files;
        var teachersRoot = rootChildren.Single(f => f.Name == "ملفات المعلمين");
        (await h.Drive.GetFileAsync(TeacherDriveHarness.SchoolId, TeacherDriveHarness.FolderA))!.Parents.Should().Contain(teachersRoot.Id);
        (await h.Drive.GetFileAsync(TeacherDriveHarness.SchoolId, TeacherDriveHarness.FolderA))!.Name.Should().Be("المعلم أ");
        var archive = await h.Context.StorageFolders.SingleAsync(f => f.Kind == AlFalah.Domain.Enums.StorageFolderKind.VisitArchive && f.ParentFolderId == null);
        (await h.Context.StorageFolders.Where(f => f.Kind == AlFalah.Domain.Enums.StorageFolderKind.VisitArchive &&
            f.ParentFolderId == archive.Id).CountAsync()).Should().Be(2);
        var archiveDrive = new VisitArchiveDriveService(new StorageRepository(h.Context),
            new StorageLibraryRepository(h.Context), new VisitArchiveRepository(h.Context),
            new StorageSetupRepository(h.Context), new GoogleStorageProvider(h.Drive));
        (await archiveDrive.TeacherFolderAsync(TeacherDriveHarness.SchoolId, archive,
            TeacherDriveHarness.TeacherAUserId, "المعلم أ", default)).OwnerTeacherId.Should().Be(TeacherDriveHarness.TeacherAId);

        var second = await service.ApplyAsync(true, true);
        second.Created.Should().Be(0);
        (await h.Drive.ListChildrenAsync(TeacherDriveHarness.SchoolId,
            new(TeacherDriveHarness.SchoolRootFolderId, null, "name", 100, null, TeacherDriveHarness.SharedDriveId)))
            .Files.Count(f => f.Name == "مكتبة المدرسة").Should().Be(1);
    }

    [Fact]
    public async Task Duplicate_remote_name_is_reported_without_creating_a_third_folder()
    {
        await using var h = await StorageLibraryTests.Setup();
        h.Drive.AddFolder("library-a", "مكتبة المدرسة", TeacherDriveHarness.SchoolRootFolderId)
            .AddFolder("library-b", "مكتبة المدرسة", TeacherDriveHarness.SchoolRootFolderId);
        var service = Service(h);

        (await service.PlanAsync()).Folders.Single(f => f.Key == "library").State.Should().Be("Conflict");
        var result = await service.ApplyAsync(true, false);
        result.Warnings.Should().Contain(w => w.Contains("مكتبة المدرسة"));
        result.Plan.Folders.Single(f => f.Key == "library").State.Should().Be("Conflict");
    }

    [Fact]
    public async Task Setup_resumes_an_older_reserved_folder_id_instead_of_allocating_another()
    {
        await using var h = await StorageLibraryTests.Setup();
        h.Context.Set<StorageOperation>().Add(new StorageOperation { SchoolId = TeacherDriveHarness.SchoolId,
            ActorUserId = TeacherDriveHarness.ManagerUserId, Action = "CreateFolder", RequestKey = "folder:older-request",
            ParentItemId = TeacherDriveHarness.SchoolRootFolderId, ProviderItemId = "reserved-library",
            DisplayName = "مكتبة المدرسة", DriveId = TeacherDriveHarness.SharedDriveId });
        await h.Context.SaveChangesAsync();

        var result = await Service(h).ApplyAsync(true, false);
        result.Warnings.Should().BeEmpty();
        (await h.Context.StorageFolders.SingleAsync(f => f.Kind == AlFalah.Domain.Enums.StorageFolderKind.SchoolLibrary && f.ParentFolderId == null))
            .DriveItemId.Should().Be("reserved-library");
        (await h.Context.Set<StorageOperation>().SingleAsync(o => o.RequestKey == "folder:older-request")).Status.Should().Be("Completed");
    }

    [Fact]
    public async Task Changing_the_school_drive_root_disables_archive_until_it_is_reviewed_again()
    {
        await using var h = await TeacherDriveHarness.CreateAsync();
        var connection = await h.Context.SchoolGoogleDrives.SingleAsync();
        connection.VisitArchiveEnabled = true;
        await h.Context.SaveChangesAsync();

        await h.ConnectSchoolDriveAsync(rootFolderId: TeacherDriveHarness.FolderUnassigned);
        (await h.Context.SchoolGoogleDrives.SingleAsync()).VisitArchiveEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Archive_button_creates_only_missing_folder_and_can_stop_without_drive()
    {
        await using var h = await StorageLibraryTests.Setup();
        var service = Service(h); // Legacy deployment archive flags are deliberately false.

        (await service.SetArchiveEnabledAsync(true)).Enabled.Should().BeTrue();
        (await h.Context.SchoolGoogleDrives.SingleAsync()).VisitArchiveEnabled.Should().BeTrue();
        (await h.Context.StorageFolders.SingleAsync(f => f.Kind == AlFalah.Domain.Enums.StorageFolderKind.VisitArchive))
            .ParentFolderId.Should().BeNull();
        (await service.SetArchiveEnabledAsync(true)).Enabled.Should().BeTrue();
        (await h.Drive.ListChildrenAsync(TeacherDriveHarness.SchoolId,
            new(TeacherDriveHarness.SchoolRootFolderId, null, "name", 100, null, TeacherDriveHarness.SharedDriveId)))
            .Files.Count(f => f.Name == "أرشيف الزيارات").Should().Be(1);

        h.Drive.UnreachableSchools.Add(TeacherDriveHarness.SchoolId);
        (await service.ArchiveStatusAsync()).Enabled.Should().BeTrue();
        (await service.SetArchiveEnabledAsync(false)).Enabled.Should().BeFalse();
        (await service.ArchiveStatusAsync()).Enabled.Should().BeFalse();
        (await h.Context.SchoolGoogleDrives.SingleAsync()).VisitArchiveEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Deleted_linked_library_is_recreated_and_all_children_are_provisioned()
    {
        await using var h = await StorageLibraryTests.Setup();
        var original = await StorageLibraryTests.Service(h, TeacherDriveHarness.Manager())
            .CreateFolderAsync(new(null, "", "original-library"));
        var oldId = (await h.Context.StorageFolders.SingleAsync(f => f.Id == original.Id)).DriveItemId;
        h.Drive.RemoveExternally(oldId);

        var result = await Service(h).ApplyAsync(true, false);

        result.Warnings.Should().BeEmpty();
        result.Plan.Folders.Should().OnlyContain(f => f.State == "Exists");
        (await h.Context.StorageFolders.SingleAsync(f => f.Id == original.Id)).DriveItemId.Should().NotBe(oldId);
        h.Drive.ChildIdsOf(TeacherDriveHarness.SchoolRootFolderId).Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task Deleted_child_folder_is_rebound_without_duplicate_database_rows()
    {
        await using var h = await StorageLibraryTests.Setup();
        var service = Service(h);
        (await service.ApplyAsync(true, false)).Warnings.Should().BeEmpty();
        var original = await h.Context.StorageFolders.SingleAsync(f => f.DisplayName == "1.1 - التخطيط");
        var oldId = original.DriveItemId;
        h.Drive.RemoveExternally(oldId);

        var repaired = await service.ApplyAsync(true, false);

        repaired.Warnings.Should().BeEmpty();
        repaired.Plan.Folders.Should().OnlyContain(f => f.State == "Exists");
        (await h.Context.StorageFolders.SingleAsync(f => f.Id == original.Id)).DriveItemId.Should().NotBe(oldId);
        (await h.Context.StorageFolders.CountAsync(f => f.DisplayName == original.DisplayName)).Should().Be(1);
    }

    [Fact]
    public async Task Deleted_teacher_folder_is_replaced_without_reusing_old_grant()
    {
        await using var h = await StorageLibraryTests.Setup();
        var service = Service(h);
        var oldId = TeacherDriveHarness.FolderA;
        h.Drive.RemoveExternally(oldId);

        var repaired = await service.ApplyAsync(false, true);

        repaired.Warnings.Should().BeEmpty();
        var grant = await h.Context.TeacherDriveFolders.SingleAsync(f => f.TeacherId == TeacherDriveHarness.TeacherAId);
        grant.RootItemId.Should().NotBe(oldId);
        repaired.Plan.Teachers.Single(t => t.TeacherId == TeacherDriveHarness.TeacherAId).State.Should().Be("Linked");
    }

    [Fact]
    public async Task Deleted_library_with_registered_upload_is_held_for_review()
    {
        await using var h = await StorageLibraryTests.Setup();
        var original = await StorageLibraryTests.Service(h, TeacherDriveHarness.Manager())
            .CreateFolderAsync(new(null, "", "protected-library"));
        var oldId = (await h.Context.StorageFolders.SingleAsync(f => f.Id == original.Id)).DriveItemId;
        h.Context.Set<StorageOperation>().Add(new StorageOperation { SchoolId = TeacherDriveHarness.SchoolId,
            ActorUserId = TeacherDriveHarness.ManagerUserId, Action = "Upload", Status = "Pending",
            FolderId = original.Id, RequestKey = "protected-upload", Fingerprint = "protected-upload",
            ParentItemId = oldId, ProviderItemId = "protected-file", DisplayName = "important.pdf",
            DriveId = TeacherDriveHarness.SharedDriveId });
        await h.Context.SaveChangesAsync();
        h.Drive.RemoveExternally(oldId);

        var result = await Service(h).ApplyAsync(true, false);

        result.Warnings.Should().Contain(w => w.Contains("ملفات أو عمليات محفوظة"));
        (await h.Context.StorageFolders.SingleAsync(f => f.Id == original.Id)).DriveItemId.Should().Be(oldId);
        (await h.Drive.ListChildrenAsync(TeacherDriveHarness.SchoolId,
            new(TeacherDriveHarness.SchoolRootFolderId, null, "name", 100, null, TeacherDriveHarness.SharedDriveId)))
            .Files.Should().NotContain(f => f.Name == "مكتبة المدرسة");
    }

    [Fact]
    public async Task Explicit_recovery_detaches_old_links_and_creates_a_new_library_without_erasing_history()
    {
        await using var h = await StorageLibraryTests.Setup();
        var oldRoot = await StorageLibraryTests.Service(h, TeacherDriveHarness.Manager())
            .CreateFolderAsync(new(null, "", "old-library"));
        var original = await h.Context.StorageFolders.SingleAsync(f => f.Id == oldRoot.Id);
        var oldId = original.DriveItemId;
        h.Context.Set<StorageOperation>().Add(new StorageOperation { SchoolId = TeacherDriveHarness.SchoolId,
            ActorUserId = TeacherDriveHarness.ManagerUserId, Action = "Upload", Status = "Pending",
            FolderId = original.Id, RequestKey = "old-upload", Fingerprint = "old-upload",
            ParentItemId = oldId, ProviderItemId = "old-file", DisplayName = "old.pdf",
            DriveId = TeacherDriveHarness.SharedDriveId });
        await h.Context.SaveChangesAsync();
        h.Drive.RemoveExternally(oldId);
        var service = Service(h);
        (await service.PlanAsync()).RecoveryIssues.Should().ContainSingle(i => i.Key == "library" && i.OperationCount == 1);

        var result = await service.ApplyAsync(true, false, recoveryKeys: ["library"]);

        result.Plan.Folders.Should().OnlyContain(f => f.State == "Exists");
        (await h.Context.StorageFolders.SingleAsync(f => f.Id == original.Id)).IsActive.Should().BeFalse();
        (await h.Context.StorageFolders.SingleAsync(f => f.Kind == AlFalah.Domain.Enums.StorageFolderKind.SchoolLibrary &&
            f.ParentFolderId == null && f.IsActive)).DriveItemId.Should().NotBe(oldId);
        (await h.Context.Set<StorageOperation>().SingleAsync(o => o.RequestKey == "old-upload")).FolderId.Should().Be(original.Id);
    }

    [Fact]
    public async Task Confirmed_recovery_keeps_old_links_when_replacement_name_is_ambiguous()
    {
        await using var h = await StorageLibraryTests.Setup();
        var oldRoot = await StorageLibraryTests.Service(h, TeacherDriveHarness.Manager())
            .CreateFolderAsync(new(null, "", "old-library"));
        var old = await h.Context.StorageFolders.SingleAsync(f => f.Id == oldRoot.Id);
        h.Context.Set<StorageOperation>().Add(new StorageOperation { SchoolId = TeacherDriveHarness.SchoolId,
            ActorUserId = TeacherDriveHarness.ManagerUserId, Action = "Upload", Status = "Pending",
            FolderId = old.Id, RequestKey = "protected-conflict", Fingerprint = "protected-conflict",
            ParentItemId = old.DriveItemId, ProviderItemId = "old-file", DisplayName = "old.pdf",
            DriveId = TeacherDriveHarness.SharedDriveId });
        await h.Context.SaveChangesAsync();
        h.Drive.RemoveExternally(old.DriveItemId);
        h.Drive.AddFolder("candidate-a", "مكتبة المدرسة", TeacherDriveHarness.SchoolRootFolderId)
            .AddFolder("candidate-b", "مكتبة المدرسة", TeacherDriveHarness.SchoolRootFolderId);

        var result = await Service(h).ApplyAsync(true, false, recoveryKeys: ["library"]);

        result.Warnings.Should().Contain(w => w.Contains("مكتبة المدرسة"));
        (await h.Context.StorageFolders.SingleAsync(f => f.Id == old.Id)).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Deleted_library_with_data_in_a_descendant_is_not_rebound()
    {
        await using var h = await StorageLibraryTests.Setup();
        var service = Service(h);
        (await service.ApplyAsync(true, false)).Warnings.Should().BeEmpty();
        var library = await h.Context.StorageFolders.SingleAsync(f => f.DisplayName == "مكتبة المدرسة");
        var child = await h.Context.StorageFolders.SingleAsync(f => f.DisplayName == "1.1 - التخطيط");
        h.Context.Set<StorageOperation>().Add(new StorageOperation { SchoolId = TeacherDriveHarness.SchoolId,
            ActorUserId = TeacherDriveHarness.ManagerUserId, Action = "Upload", Status = "Pending",
            FolderId = child.Id, RequestKey = "nested-upload", Fingerprint = "nested-upload",
            ParentItemId = child.DriveItemId, ProviderItemId = "nested-file", DisplayName = "evidence.pdf",
            DriveId = TeacherDriveHarness.SharedDriveId });
        await h.Context.SaveChangesAsync();
        var originalId = library.DriveItemId;
        h.Drive.RemoveExternally(originalId);

        var result = await service.ApplyAsync(true, false);

        result.Warnings.Should().Contain(w => w.Contains("ملفات أو عمليات محفوظة"));
        (await h.Context.StorageFolders.SingleAsync(f => f.Id == library.Id)).DriveItemId.Should().Be(originalId);
    }

    [Fact]
    public async Task Moving_a_tracked_folder_to_a_live_parent_requires_review()
    {
        await using var h = await StorageLibraryTests.Setup();
        var service = Service(h);
        (await service.ApplyAsync(true, false)).Warnings.Should().BeEmpty();
        var child = await h.Context.StorageFolders.SingleAsync(f => f.DisplayName == "1.1 - التخطيط");
        var oldId = child.DriveItemId;
        h.Drive.MoveExternally(oldId, TeacherDriveHarness.OutsideRootFolderId);

        var result = await service.ApplyAsync(true, false);

        result.Warnings.Should().Contain(w => w.Contains("1.1 - التخطيط") && w.Contains("نُقل"));
        (await h.Context.StorageFolders.SingleAsync(f => f.Id == child.Id)).DriveItemId.Should().Be(oldId);
    }

    [Fact]
    public async Task Progress_reports_real_order_and_finishes_at_total_after_recovery()
    {
        await using var h = await StorageLibraryTests.Setup();
        var service = Service(h);
        (await service.ApplyAsync(true, false)).Warnings.Should().BeEmpty();
        var library = await h.Context.StorageFolders.SingleAsync(f => f.DisplayName == "مكتبة المدرسة");
        h.Drive.RemoveExternally(library.DriveItemId);
        var updates = new List<StorageSetupProgress>();

        var result = await service.ApplyWithProgressAsync(true, false, progress =>
        { updates.Add(progress); return Task.CompletedTask; });

        result.Warnings.Should().BeEmpty();
        updates.First().Stage.Should().Be("scan");
        updates.Should().Contain(update => update.Stage == "folders" && update.Label == "مكتبة المدرسة");
        updates.Should().Contain(update => update.Stage == "verify");
        updates.Last().Stage.Should().Be("complete");
        updates.Last().Completed.Should().Be(updates.Last().Total);
        updates.Select(update => update.Completed).Should().BeInAscendingOrder();
    }
}
