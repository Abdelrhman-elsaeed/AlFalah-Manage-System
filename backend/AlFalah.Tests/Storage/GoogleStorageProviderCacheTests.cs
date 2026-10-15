using AlFalah.Infrastructure.Services;
using AlFalah.Tests.TestDoubles;
using FluentAssertions;
using Xunit;

namespace AlFalah.Tests.Storage;

public sealed class GoogleStorageProviderCacheTests
{
    [Fact]
    public async Task Listed_metadata_is_reused_in_request_but_next_request_detects_external_move()
    {
        var drive = new FakeGoogleDrive().AddFolder("root", "Root").AddFolder("child", "Child", "root").AddFolder("outside", "Outside");
        var provider = new GoogleStorageProvider(drive);
        await provider.MetadataAsync(1, "root", default);
        await provider.ChildrenAsync(1, "root", "", null, default);
        var reads = drive.GetFileCalls;
        (await provider.IsWithinAsync(1, "root", "child", default)).Should().BeTrue();
        drive.GetFileCalls.Should().Be(reads);
        drive.MoveExternally("child", "outside"); provider.ResetRequestCache();
        (await provider.IsWithinAsync(1, "root", "child", default)).Should().BeFalse();
    }
    [Fact]
    public async Task Successful_create_invalidates_reserved_ID_404_and_lost_response_is_reconciled()
    {
        var drive = new FakeGoogleDrive().AddFolder("root", "Root"); var provider = new GoogleStorageProvider(drive);
        (await provider.MetadataAsync(1, "reserved", default)).Should().BeNull();
        drive.LoseNextFolderResponse = true;
        await FluentActions.Invoking(() => provider.CreateFolderAsync(1, "reserved", "root", "New", default)).Should().ThrowAsync<HttpRequestException>();
        (await provider.MetadataAsync(1, "reserved", default))!.Name.Should().Be("New");
        drive.ChildIdsOf("root").Should().ContainSingle();
    }
}
