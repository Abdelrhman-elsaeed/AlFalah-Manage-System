using System.Text;
using AlFalah.Application.Common;
using AlFalah.Application.Interfaces;
using AlFalah.Application.Storage;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Storage;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data.Seeders;
using AlFalah.Infrastructure.Repositories;
using AlFalah.Infrastructure.Services;
using AlFalah.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Xunit;

namespace AlFalah.Tests.Storage;

public sealed class StorageLibraryTests
{
    internal static async Task<TeacherDriveHarness> Setup(SaveChangesInterceptor? interceptor = null)
    {
        var h = await TeacherDriveHarness.CreateAsync(interceptor: interceptor);
        (await h.Context.Schools.SingleAsync(x => x.Id == 1)).ManagerUserId = TeacherDriveHarness.ManagerUserId;
        var manager = new ApplicationRole { Id = "storage-manager", Name = RoleNames.SchoolManager };
        var teacher = new ApplicationRole { Id = "storage-teacher", Name = RoleNames.Instructor };
        h.Context.Roles.AddRange(manager, teacher);
        foreach (var p in StoragePermissionCatalog.All)
        {
            var permission = new Permission { Name = p.Name, Group = p.Group };
            h.Context.RolePermissions.Add(new() { Role = manager, Permission = permission });
            if (p.Name is PermissionNames.StorageViewOwn or PermissionNames.StorageManageOwn)
                h.Context.RolePermissions.Add(new() { Role = teacher, Permission = permission });
        }
        h.Context.UserSchoolRoles.AddRange(new() { SchoolId = 1, UserId = TeacherDriveHarness.ManagerUserId, Role = manager },
            new() { SchoolId = 1, UserId = TeacherDriveHarness.TeacherAUserId, Role = teacher },
            new() { SchoolId = 1, UserId = TeacherDriveHarness.TeacherBUserId, Role = teacher });
        await h.Context.SaveChangesAsync();
        return h;
    }
    internal static StorageLibraryService Service(TeacherDriveHarness h, ICurrentUserService user, bool enabled = true)
    {
        var scopes = new StorageRepository(h.Context);
        return new(new StorageLibraryRepository(h.Context), scopes,
            new StorageAuthorizationService(scopes, user, new StorageDriveBoundary(new(h.Drive)), TimeProvider.System),
            user, new GoogleStorageProvider(h.Drive), h.SubmissionService(), Options.Create(new StorageOptions { ReadModelEnabled = enabled }));
    }
    internal static async Task<StorageUploadDto> Upload(IStorageLibraryService service, bool own, string key = "upload-one", int? folder = null, string bytes = "%PDF-1.7\nTest file")
    {
        using var content = new MemoryStream(Encoding.UTF8.GetBytes(bytes));
        return await service.UploadAsync(new(content, "شاهد.pdf", content.Length, folder, key, own));
    }
    [Fact]
    public async Task School_library_and_teacher_uploads_have_one_asset_and_version_and_no_evidence_link()
    {
        await using var h = await Setup();
        var school = Service(h, TeacherDriveHarness.Manager());
        var root = await school.CreateFolderAsync(new(null, "ignored", "root-key"));
        root.DisplayName.Should().Be("مكتبة المدرسة");
        var uploaded = await Upload(school, false);
        var teacher = await Upload(Service(h, TeacherDriveHarness.TeacherA()), true);
        uploaded.Status.Should().Be("Completed"); teacher.Status.Should().Be("Completed");
        (await h.Context.StoredFiles.CountAsync()).Should().Be(2);
        (await h.Context.StoredFileVersions.CountAsync()).Should().Be(2);
        (await h.Context.EvidenceLinks.CountAsync()).Should().Be(0);
        (await h.Context.TeacherTaskStatuses.CountAsync()).Should().Be(0);
        var json = System.Text.Json.JsonSerializer.Serialize(await school.DetailsAsync(uploaded.StoredFileId!.Value));
        json.Should().NotContain("DriveItemId").And.NotContain("RootItemId").And.NotContain("drive.google");
    }
    [Fact]
    public async Task Repeated_key_returns_same_asset_and_changed_bytes_conflict()
    {
        await using var h = await Setup();
        var teacher = Service(h, TeacherDriveHarness.TeacherA());
        var first = await Upload(teacher, true);
        var again = await Upload(teacher, true);
        again.StoredFileId.Should().Be(first.StoredFileId); h.Drive.Uploads.Should().HaveCount(1);
        await FluentActions.Invoking(() => Upload(teacher, true, bytes: "%PDF-1.7\nChanged")).Should().ThrowAsync<StorageConflictException>();
    }
    [Fact]
    public async Task Lost_Drive_response_reconciles_without_sending_a_second_stream()
    {
        await using var h = await Setup();
        var teacher = Service(h, TeacherDriveHarness.TeacherA());
        h.Drive.LoseNextUploadResponse = true;
        await FluentActions.Invoking(() => Upload(teacher, true)).Should().ThrowAsync<StorageUnavailableException>();
        (await h.Context.StoredFiles.CountAsync()).Should().Be(0);
        h.Context.ChangeTracker.Clear();
        var recovered = await Upload(Service(h, TeacherDriveHarness.TeacherA()), true);
        recovered.Status.Should().Be("Completed"); h.Drive.Uploads.Should().HaveCount(1);
        (await h.Context.StoredFiles.CountAsync()).Should().Be(1);
    }
    [Fact]
    public async Task SQL_failure_after_Drive_success_leaves_durable_operation_and_recovers_in_a_new_scope()
    {
        var failure = new FailAssetSave();
        await using var h = await Setup(failure);
        failure.Armed = true;
        await FluentActions.Invoking(() => Upload(Service(h, TeacherDriveHarness.TeacherA()), true)).Should().ThrowAsync<StorageUnavailableException>();
        h.Context.ChangeTracker.Clear();
        var operation = await h.Context.Set<StorageOperation>().SingleAsync();
        operation.ProviderItemId.Should().Be(h.Drive.Uploads.Single().FileId);
        operation.Status.Should().Be("NeedsAttention");
        var result = await Service(h, TeacherDriveHarness.TeacherA()).ReconcileAsync(operation.Id);
        result.Status.Should().Be("Completed"); h.Drive.Uploads.Should().HaveCount(1);
    }
    [Fact]
    public async Task Network_failure_without_an_item_never_reports_success_or_reuploads_automatically()
    {
        await using var h = await Setup();
        h.Drive.FailNextUpload = true;
        await FluentActions.Invoking(() => Upload(Service(h, TeacherDriveHarness.TeacherA()), true)).Should().ThrowAsync<StorageUnavailableException>();
        var result = await Upload(Service(h, TeacherDriveHarness.TeacherA()), true);
        result.Status.Should().Be("NeedsAttention"); result.StoredFileId.Should().BeNull();
        h.Drive.Uploads.Should().BeEmpty(); (await h.Context.StoredFiles.CountAsync()).Should().Be(0);
    }
    [Fact]
    public async Task Definitive_quota_rejection_is_Failed_and_replaying_key_does_not_create_an_item()
    {
        await using var h = await Setup(); h.Drive.RejectNextUpload = true;
        await FluentActions.Invoking(() => Upload(Service(h, TeacherDriveHarness.TeacherA()), true)).Should().ThrowAsync<StorageUnavailableException>();
        (await Upload(Service(h, TeacherDriveHarness.TeacherA()), true)).Status.Should().Be("Failed");
        h.Drive.Uploads.Should().BeEmpty();
    }
    [Fact]
    public async Task Discovery_uses_internal_folder_ids_and_labels_unindexed_files_without_exposing_Drive_ids()
    {
        await using var h = await Setup(); var service = Service(h, TeacherDriveHarness.TeacherA());
        var root = (await service.ContextAsync(true)).RootFolderId!.Value;
        var page = await service.DiscoverAsync(true, root, null);
        page.Items.Should().Contain(x => !x.IsFolder && x.State == "Unindexed" && x.StoredFileId == null);
        page.Items.Should().Contain(x => x.IsFolder && x.FolderId != null);
        System.Text.Json.JsonSerializer.Serialize(page).Should().NotContain("a-existing.pdf").And.NotContain("folder-a-sub");
    }
    [Fact]
    public async Task Deletion_is_repeatable_and_never_changes_version_identity()
    {
        await using var h = await Setup(); var service = Service(h, TeacherDriveHarness.TeacherA());
        var result = await Upload(service, true); var details = await service.DetailsAsync(result.StoredFileId!.Value);
        await service.RenameAsync(result.StoredFileId.Value, new("جديد.pdf", details.File.RowVersion));
        (await h.Context.StoredFileVersions.SingleAsync()).DriveFileName.Should().Be("شاهد.pdf");
        var renamed = await service.DetailsAsync(result.StoredFileId.Value);
        await service.DeleteAsync(result.StoredFileId.Value, new(renamed.File.RowVersion));
        await service.DeleteAsync(result.StoredFileId.Value, new(renamed.File.RowVersion));
        h.Drive.Trashed.Should().HaveCount(1);
    }
    [Fact]
    public async Task Lost_legacy_file_clears_the_legacy_matrix_status()
    {
        await using var h = await Setup(); var service = Service(h, TeacherDriveHarness.TeacherA());
        using var content = new MemoryStream("%PDF-1.7\nLegacy"u8.ToArray());
        await service.LegacyUploadAsync(new(content, "legacy.pdf", "application/pdf", content.Length, null, 1, "missing-legacy"));
        h.Drive.RemoveExternally(h.Drive.Uploads.Single().FileId);
        (await service.FilesAsync(true, new())).Items.Single().State.Should().Be("MissingFromDrive");
        (await h.Context.TeacherTaskStatuses.SingleAsync()).CellStatus.Should().Be(EvidenceCellStatus.MissingFromDrive);
    }
    [Fact]
    public async Task Teacher_cannot_read_others_or_upload_school_files_or_supply_another_internal_folder()
    {
        await using var h = await Setup();
        var teacherA = Service(h, TeacherDriveHarness.TeacherA());
        var teacherB = Service(h, TeacherDriveHarness.TeacherB());
        var victim = await Upload(teacherB, true);
        var folder = (await teacherB.ContextAsync(true)).RootFolderId;
        await teacherA.Invoking(x => x.ContentAsync(victim.StoredFileId!.Value)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        await teacherA.Invoking(x => x.DetailsAsync(victim.StoredFileId!.Value)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        await FluentActions.Invoking(() => Upload(teacherA, false)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        await FluentActions.Invoking(() => Upload(teacherA, true, folder: folder)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        await Service(h, TeacherDriveHarness.TeacherA(2)).Invoking(x => x.ContextAsync(true)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
    }
    [Fact]
    public async Task Delegate_loses_list_upload_and_reconciliation_access_immediately_after_revocation()
    {
        await using var h = await Setup();
        await Service(h, TeacherDriveHarness.Manager()).CreateFolderAsync(new(null, "", "root"));
        var delegation = new StorageDelegation { SchoolId = 1, GranteeUserId = TeacherDriveHarness.TeacherAUserId,
            GrantedByManagerUserId = TeacherDriveHarness.ManagerUserId, StartsAt = DateTimeOffset.UtcNow.AddMinutes(-1), Reason = "تكليف" };
        h.Context.StorageDelegations.Add(delegation); await h.Context.SaveChangesAsync();
        var service = Service(h, TeacherDriveHarness.TeacherA());
        var upload = await Upload(service, false);
        delegation.RevokedAt = DateTimeOffset.UtcNow; await h.Context.SaveChangesAsync();
        await service.Invoking(x => x.FilesAsync(false, new())).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        await FluentActions.Invoking(() => Upload(service, false, "after-revoke")).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        await service.Invoking(x => x.ReconcileAsync(upload.OperationId)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
    }
    [Fact]
    public async Task Removed_teacher_grant_or_inactive_teacher_stops_file_access()
    {
        await using var h = await Setup();
        var service = Service(h, TeacherDriveHarness.TeacherA());
        var upload = await Upload(service, true);
        var grant = await h.Context.TeacherDriveFolders.SingleAsync(x => x.TeacherId == 1);
        grant.IsActive = false; await h.Context.SaveChangesAsync();
        await service.Invoking(x => x.ContentAsync(upload.StoredFileId!.Value)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        grant.IsActive = true;
        (await h.Context.InstructorProfiles.SingleAsync(x => x.Id == 1)).IsActive = false; await h.Context.SaveChangesAsync();
        await service.Invoking(x => x.ContentAsync(upload.StoredFileId!.Value)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
    }
    [Fact]
    public async Task Root_overlap_and_external_moves_fail_closed()
    {
        await using var h = await Setup();
        var school = Service(h, TeacherDriveHarness.Manager());
        var root = await school.CreateFolderAsync(new(null, "", "root"));
        var rootItem = (await h.Context.StorageFolders.SingleAsync(x => x.Id == root.Id)).DriveItemId;
        await h.MappingService(TeacherDriveHarness.Manager()).Invoking(x => x.UpsertAsync(1, new(rootItem))).Should().ThrowAsync<InvalidOperationException>();
        h.Drive.MoveExternally(rootItem, TeacherDriveHarness.FolderA);
        await school.Invoking(x => x.FilesAsync(false, new())).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
    }
    [Fact]
    public async Task Folder_moves_check_cycles_and_school_scope()
    {
        await using var h = await Setup(); var service = Service(h, TeacherDriveHarness.Manager());
        var root = await service.CreateFolderAsync(new(null, "", "root"));
        var a = await service.CreateFolderAsync(new(root.Id, "مجلد أ", "folder-a"));
        var b = await service.CreateFolderAsync(new(a.Id, "مجلد ب", "folder-b"));
        await service.Invoking(x => x.MoveFolderAsync(a.Id, new(b.Id, a.RowVersion))).Should().ThrowAsync<ArgumentException>();
        await service.Invoking(x => x.MoveFolderAsync(root.Id, new(a.Id, root.RowVersion))).Should().ThrowAsync<ArgumentException>();
        await service.Invoking(x => x.MoveFolderAsync(a.Id, new(999, a.RowVersion))).Should().ThrowAsync<KeyNotFoundException>();
        (await service.MoveFolderAsync(b.Id, new(root.Id, b.RowVersion))).ParentFolderId.Should().Be(root.Id);
    }
    [Fact]
    public async Task Approved_legacy_and_historical_files_are_immutable_even_before_S3()
    {
        await using var h = await Setup();
        var legacy = await h.UploadAsync(TeacherDriveHarness.TeacherA(), 1);
        (await h.Context.TeacherEvidenceSubmissions.SingleAsync()).ReviewStatus = EvidenceReviewStatus.Approved;
        await h.Context.SaveChangesAsync();
        await h.UploadService(TeacherDriveHarness.TeacherA()).Invoking(x => x.DeleteAsync(legacy.SubmissionId)).Should().ThrowAsync<BusinessRuleException>();
        await h.UploadService(TeacherDriveHarness.TeacherA()).Invoking(x => x.RenameAsync(legacy.SubmissionId, "آخر.pdf")).Should().ThrowAsync<BusinessRuleException>();
        await new StorageBackfillService(new StorageBackfillRepository(h.Context)).RunAsync(false);
        var file = await h.Context.StoredFiles.SingleAsync();
        var service = Service(h, TeacherDriveHarness.TeacherA());
        var details = await service.DetailsAsync(file.Id);
        details.File.IsProtected.Should().BeTrue();
        await service.Invoking(x => x.DeleteAsync(file.Id, new(details.File.RowVersion))).Should().ThrowAsync<BusinessRuleException>();
        await service.Invoking(x => x.RenameAsync(file.Id, new("آخر.pdf", details.File.RowVersion))).Should().ThrowAsync<BusinessRuleException>();
        h.Drive.Trashed.Should().BeEmpty();
    }
    [Fact]
    public async Task Folder_tree_hides_nodes_moved_or_trashed_outside_the_current_root()
    {
        await using var h = await Setup(); var service = Service(h, TeacherDriveHarness.Manager());
        var root = await service.CreateFolderAsync(new(null, "", "root"));
        var child = await service.CreateFolderAsync(new(root.Id, "مجلد فرعي", "child"));
        var providerId = (await h.Context.StorageFolders.SingleAsync(x => x.Id == child.Id)).DriveItemId;
        h.Drive.MoveExternally(providerId, "external-root");
        (await service.FoldersAsync(false, root.Id, 1, 25)).Items.Should().BeEmpty();
        h.Drive.MoveExternally(providerId, (await h.Context.StorageFolders.SingleAsync(x => x.Id == root.Id)).DriveItemId);
        h.Drive.TrashExternally(providerId);
        (await service.FoldersAsync(false, root.Id, 1, 25)).Items.Should().BeEmpty();
    }
    [Fact]
    public async Task Legacy_route_adapter_has_one_writer_and_preserves_pending_matrix_and_delete_counts()
    {
        await using var h = await Setup();
        var user = TeacherDriveHarness.TeacherA(); var library = Service(h, user);
        var adapter = new GoogleDriveUploadService(h.IdentityService(user), h.MappingService(user), h.Drive, new(h.Drive),
            h.SubmissionService(), new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(), h.Context,
            library, Options.Create(new StorageOptions { ReadModelEnabled = true }));
        using var stream = new MemoryStream("%PDF-1.7\nLegacy"u8.ToArray());
        var result = await adapter.UploadAsync(new(stream, "قديم.pdf", "application/pdf", stream.Length, null, 1, "legacy-shared"));
        (await h.Context.StoredFiles.CountAsync()).Should().Be(1);
        (await h.Context.TeacherEvidenceSubmissions.CountAsync()).Should().Be(1);
        (await h.Context.TeacherTaskStatuses.SingleAsync()).CellStatus.Should().Be(EvidenceCellStatus.PendingReview);
        await adapter.DeleteAsync(result.SubmissionId);
        (await h.Context.TeacherTaskStatuses.SingleAsync()).ActiveFilesCount.Should().Be(0);
        h.Drive.Uploads.Should().HaveCount(1);
    }
    [Fact]
    public async Task Missing_from_Drive_is_marked_and_transport_errors_are_never_marked_missing()
    {
        await using var h = await Setup(); var service = Service(h, TeacherDriveHarness.TeacherA());
        var result = await Upload(service, true);
        h.Drive.UnreachableSchools.Add(1);
        await service.Invoking(x => x.FilesAsync(true, new())).Should().ThrowAsync<InvalidOperationException>();
        (await h.Context.StoredFileVersions.SingleAsync()).Availability.Should().Be(StoredFileAvailability.Available);
        h.Drive.UnreachableSchools.Clear(); h.Drive.RemoveExternally(h.Drive.Uploads.Single().FileId);
        (await service.FilesAsync(true, new())).Items.Single().State.Should().Be("MissingFromDrive");
        await service.Invoking(x => x.ContentAsync(result.StoredFileId!.Value)).Should().ThrowAsync<KeyNotFoundException>();
    }
    [Fact]
    public async Task Workspace_flags_default_off_and_page_limits_are_explicit()
    {
        await using var h = await Setup();
        (await Service(h, TeacherDriveHarness.Manager(), false).ContextAsync(false)).ConnectionState.Should().Be("Disabled");
        await Service(h, TeacherDriveHarness.TeacherA()).Invoking(x => x.FilesAsync(true, new(PageSize: 101))).Should().ThrowAsync<ArgumentException>();
    }
    [Fact]
    public async Task Externally_moved_file_cannot_restore_availability_until_back_in_its_authorized_root()
    {
        await using var h = await Setup(); var service = Service(h, TeacherDriveHarness.TeacherA());
        var result = await Upload(service, true);
        var item = h.Drive.Uploads.Single().FileId;
        h.Drive.MoveExternally(item, TeacherDriveHarness.FolderB);
        (await service.FilesAsync(true, new())).Items.Should().BeEmpty();
        (await h.Context.StoredFileVersions.SingleAsync()).Availability.Should().Be(StoredFileAvailability.Missing);
        await service.Invoking(x => x.DetailsAsync(result.StoredFileId!.Value)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        await service.Invoking(x => x.ContentAsync(result.StoredFileId!.Value)).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        (await h.Context.StoredFileVersions.SingleAsync()).Availability.Should().Be(StoredFileAvailability.Missing);
        h.Drive.MoveExternally(item, TeacherDriveHarness.FolderA);
        (await service.DetailsAsync(result.StoredFileId!.Value)).File.State.Should().Be("Managed");
        (await h.Context.StoredFileVersions.SingleAsync()).Availability.Should().Be(StoredFileAvailability.Available);
    }
    [Theory]
    [InlineData("script.exe", "MZ")]
    [InlineData("fake.pdf", "<html>unsafe</html>")]
    [InlineData("../escape.pdf", "%PDF-")]
    [InlineData("fake.docx", "PK\u0003\u0004not-a-zip")]
    [InlineData("fake.mp4", "%PDF-")]
    public async Task Invalid_names_extensions_and_signatures_never_reach_Drive(string name, string content)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        await FluentActions.Invoking(() => ValidatedStorageUpload.ReadAsync(stream, name, stream.Length, default)).Should().ThrowAsync<ArgumentException>();
    }
    [Fact]
    public async Task Stream_length_is_verified_and_spool_is_deleted()
    {
        using var stream = new MemoryStream("%PDF-1.7\nFile"u8.ToArray());
        await FluentActions.Invoking(() => ValidatedStorageUpload.ReadAsync(stream, "file.pdf", stream.Length - 1, default)).Should().ThrowAsync<ArgumentException>();
        stream.Position = 0;
        var validated = await ValidatedStorageUpload.ReadAsync(stream, "file.pdf", stream.Length, default);
        var path = validated.Content.Name; validated.SHA256.Should().HaveLength(64);
        await validated.DisposeAsync(); File.Exists(path).Should().BeFalse();
    }
    [Fact]
    public async Task Exact_250_MiB_boundary_is_allowed_with_bounded_memory()
    {
        using var stream = new GeneratedPdf(ValidatedStorageUpload.MaxFileBytes);
        await using var validated = await ValidatedStorageUpload.ReadAsync(stream, "boundary.pdf", stream.Length, default);
        validated.Size.Should().Be(262144000);
        using var tooLarge = new GeneratedPdf(ValidatedStorageUpload.MaxFileBytes + 1);
        await FluentActions.Invoking(() => ValidatedStorageUpload.ReadAsync(tooLarge, "too-big.pdf", tooLarge.Length, default)).Should().ThrowAsync<ArgumentException>();
        ValidatedStorageUpload.MaxRequestBytes.Should().BeGreaterThan(ValidatedStorageUpload.MaxFileBytes);
    }
    private sealed class FailAssetSave : SaveChangesInterceptor
    {
        public bool Armed { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Armed && eventData.Context!.ChangeTracker.Entries<StoredFile>().Any(x => x.State == EntityState.Added))
            { Armed = false; throw new InvalidOperationException("Simulated SQL outage after Drive committed"); }
            return ValueTask.FromResult(result);
        }
    }
    private sealed class GeneratedPdf(long length) : Stream
    {
        private long position;
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => length; public override long Position { get => position; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            var count = (int)Math.Min(buffer.Length, length - position); buffer[..count].Clear();
            var header = "%PDF-1.7\n"u8;
            for (var i = 0; i < count && position + i < header.Length; i++) buffer[i] = header[(int)position + i];
            position += count; return count;
        }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) { ct.ThrowIfCancellationRequested(); return ValueTask.FromResult(Read(buffer.Span)); }
        public override void Flush() => throw new NotSupportedException(); public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
