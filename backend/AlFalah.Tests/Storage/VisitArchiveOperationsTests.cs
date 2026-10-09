using System.Reflection;
using AlFalah.Application.Common;
using AlFalah.Application.Interfaces;
using AlFalah.Application.Storage;
using AlFalah.Domain.Enums;
using AlFalah.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AlFalah.Tests.Storage;

public sealed class VisitArchiveOperationsTests
{
    [Fact]
    public async Task Status_reports_configuration_only_to_authorized_visit_reader()
    {
        var storage = DispatchProxy.Create<IStorageRepository, PermissionRepository>();
        var permissions = (PermissionRepository)storage;
        var archive = DispatchProxy.Create<IVisitArchiveRepository, ArchiveSettingsRepository>();
        var settings = (ArchiveSettingsRepository)archive;
        var authorization = new ArchiveAuthorization();
        var stopped = Service(archive, storage, authorization, new StorageOptions { AdministrationEnabled = true, ReadModelEnabled = true });
        (await stopped.OperationsStatusAsync()).Should().Be(new VisitArchiveOperationsDto(true, false, false));

        var running = Service(archive, storage, authorization, new StorageOptions {
            AdministrationEnabled = true, ReadModelEnabled = true, ArchiveWorkerEnabled = true, ArchiveExternalWritesEnabled = true });
        (await running.OperationsStatusAsync()).Should().Be(new VisitArchiveOperationsDto(true, false, false));
        settings.SchoolEnabled = true;
        (await running.OperationsStatusAsync()).Should().Be(new VisitArchiveOperationsDto(true, true, true, true));

        permissions.VisitView = false;
        await FluentActions.Invoking(() => running.OperationsStatusAsync()).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
        permissions.VisitView = true;
        authorization.AllowArchive = false;
        await FluentActions.Invoking(() => running.OperationsStatusAsync()).Should().ThrowAsync<UnauthorizedSchoolAccessException>();
    }

    private static VisitArchiveService Service(IVisitArchiveRepository archive, IStorageRepository storage, IStorageAuthorizationService authorization, StorageOptions flags) =>
        new(archive, storage, authorization, TeacherDriveHarness.Manager(1), new VisitsEnabled(), null!, null!, Options.Create(flags), TimeProvider.System);

    public class ArchiveSettingsRepository : DispatchProxy
    {
        public bool SchoolEnabled { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name == nameof(IVisitArchiveRepository.SchoolEnabledAsync)
            ? Task.FromResult(SchoolEnabled) : throw new NotSupportedException(method?.Name);
    }

    public class PermissionRepository : DispatchProxy
    {
        public bool VisitView { get; set; } = true;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name == nameof(IStorageRepository.HasPermissionAsync)
            ? Task.FromResult(VisitView && (args?[2] as string) == PermissionNames.VisitView)
            : throw new NotSupportedException(method?.Name);
    }

    private sealed class ArchiveAuthorization : IStorageAuthorizationService
    {
        public bool AllowArchive { get; set; } = true;
        public Task<StorageAccessDto> AccessAsync(int schoolId, bool own, bool allowManagerWithoutView, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<StorageActorScope> RequireScopeAsync(int schoolId, CancellationToken ct = default) =>
            Task.FromResult(new StorageActorScope(TeacherDriveHarness.ManagerUserId, schoolId, TeacherDriveHarness.ManagerUserId, true));
        public Task<StorageActorScope> RequireManagerAsync(int schoolId, CancellationToken ct = default) => RequireScopeAsync(schoolId, ct);
        public Task RequireSchoolPermissionAsync(int schoolId, string permission, CancellationToken ct = default) =>
            AllowArchive && permission == PermissionNames.StorageViewArchive ? Task.CompletedTask : Task.FromException(new UnauthorizedSchoolAccessException("الأرشيف غير مخوّل."));
        public Task<StorageFileAccess> RequireFileAsync(int schoolId, int fileId, bool mutation = false, CancellationToken ct = default, bool metadataOnly = false) => throw new NotSupportedException();
    }
    private sealed class VisitsEnabled : IFeatureFlagService { public bool IsVisitsV2Enabled(int? schoolId = null) => true; }
}
