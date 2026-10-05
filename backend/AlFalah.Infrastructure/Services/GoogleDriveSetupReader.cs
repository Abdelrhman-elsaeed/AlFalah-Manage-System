using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Application.Interfaces;
using AlFalah.Application.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AlFalah.Infrastructure.Services;

public sealed class GoogleDriveSetupReader : IGoogleDriveSetupReader
{
    private readonly GoogleDriveClient client;
    public GoogleDriveSetupReader(IGoogleDriveTokenService tokens, IHttpClientFactory http,
        IConfiguration configuration, ILogger<GoogleDriveClient> logger) =>
        client = new(new SetupTokens(tokens), http, configuration, logger);
    public Task<GoogleDriveFile?> FolderAsync(int schoolId, string itemId, CancellationToken ct) => client.GetFileAsync(schoolId, itemId, ct);
    public Task<GoogleDriveFileList> ChildrenAsync(int schoolId, GoogleDriveListRequest request, CancellationToken ct) => client.ListChildrenAsync(schoolId, request, ct);
    private sealed class SetupTokens(IGoogleDriveTokenService tokens) : IGoogleDriveTokenService
    {
        public Task<string> GetAccessTokenAsync(int schoolId, CancellationToken ct = default) => tokens.GetSetupAccessTokenAsync(schoolId, ct);
        public void InvalidateCachedToken(int schoolId) => tokens.InvalidateCachedToken(schoolId);
    }
}
