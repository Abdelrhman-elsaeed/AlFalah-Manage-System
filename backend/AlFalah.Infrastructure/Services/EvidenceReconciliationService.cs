using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AlFalah.Infrastructure.Services;

/// <summary>
/// Compares the evidence ledger with OneDrive using the application identity.
/// It does not infer a task from a file name: it only verifies already-linked
/// DriveId/DriveItemId pairs.
/// </summary>
public sealed class EvidenceReconciliationService : IEvidenceReconciliationService
{
    private readonly AlFalahDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly AuditLogWriter _audit;
    private readonly EvidenceSubmissionService _submissions;
    private readonly ILogger<EvidenceReconciliationService> _logger;

    public EvidenceReconciliationService(
        AlFalahDbContext context,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        AuditLogWriter audit,
        EvidenceSubmissionService submissions,
        ILogger<EvidenceReconciliationService> logger)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _audit = audit;
        _submissions = submissions;
        _logger = logger;
    }

    public async Task<int> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        var token = await TryGetApplicationTokenAsync(cancellationToken);
        if (token is null)
        {
            _logger.LogDebug("Evidence reconciliation skipped because AzureAd application credentials are not configured.");
            return 0;
        }

        var candidates = await _context.TeacherEvidenceSubmissions
            .Where(x => x.UploadStatus == EvidenceUploadStatus.Completed && !x.IsDeleted && x.TaskId != null && x.AcademicYearId != null)
            .ToListAsync(cancellationToken);
        var graph = _httpClientFactory.CreateClient("MicrosoftGraph");
        var changed = new List<(int TeacherId, int SchoolId, int TaskId, int AcademicYearId)>();

        foreach (var submission in candidates)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"drives/{Uri.EscapeDataString(submission.DriveId)}/items/{Uri.EscapeDataString(submission.DriveItemId)}?$select=id,eTag");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await graph.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
            {
                _logger.LogWarning("OneDrive reconciliation could not read {DriveId}/{ItemId}: {StatusCode}", submission.DriveId, submission.DriveItemId, response.StatusCode);
                continue;
            }

            var missing = response.StatusCode == HttpStatusCode.NotFound;
            if (submission.IsMissingFromDrive == missing) continue;
            submission.IsMissingFromDrive = missing;
            submission.MissingFromDriveAtUtc = missing ? DateTimeOffset.UtcNow : null;
            changed.Add((submission.TeacherId, submission.SchoolId, submission.TaskId!.Value, submission.AcademicYearId!.Value));
            _audit.Write(submission.SchoolId, null,
                missing ? "TeacherEvidence.MissingFromDrive" : "TeacherEvidence.RestoredOnDrive",
                "TeacherEvidenceSubmission", submission.Id.ToString(), null,
                new { submission.TeacherId, submission.TaskId, submission.AcademicYearId, submission.DriveId, submission.DriveItemId });
        }

        if (changed.Count == 0) return 0;
        await _context.SaveChangesAsync(cancellationToken);
        foreach (var group in changed.Distinct())
            await _submissions.RecalculateTaskStatusAsync(group.TeacherId, group.SchoolId, group.TaskId, group.AcademicYearId, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return changed.Count;
    }

    private async Task<string?> TryGetApplicationTokenAsync(CancellationToken cancellationToken)
    {
        var tenantId = _configuration["AzureAd:TenantId"];
        var clientId = _configuration["AzureAd:ClientId"];
        var clientSecret = _configuration["AzureAd:ClientSecret"];
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret)) return null;

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["scope"] = "https://graph.microsoft.com/.default",
            ["grant_type"] = "client_credentials"
        });
        using var response = await _httpClientFactory.CreateClient("MicrosoftGraphToken")
            .PostAsync($"https://login.microsoftonline.com/{Uri.EscapeDataString(tenantId)}/oauth2/v2.0/token", content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Unable to acquire OneDrive reconciliation application token: {StatusCode}", response.StatusCode);
            return null;
        }
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<GraphTokenResponse>(stream, cancellationToken: cancellationToken);
        return payload?.AccessToken;
    }

    private sealed record GraphTokenResponse([property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string? AccessToken);
}
