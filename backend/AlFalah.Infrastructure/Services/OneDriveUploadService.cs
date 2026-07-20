using System.Security.Claims;
using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Application.Interfaces;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AlFalah.Infrastructure.Services;

/// <summary>Streams a validated upload to Graph. Graph's rename conflict behavior prevents accidental overwrites.</summary>
public sealed class OneDriveUploadService : IOneDriveUploadService
{
    private static readonly HashSet<string> DefaultExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx", ".jpg", ".jpeg", ".png" };
    private readonly ITeacherMicrosoftAccountService _accounts;
    private readonly ITeacherDriveMappingService _mappings;
    private readonly OneDriveBrowserService _browser;
    private readonly IEvidenceSubmissionService _submissions;
    private readonly IConfiguration _configuration;
    private readonly AlFalahDbContext _context;

    public OneDriveUploadService(ITeacherMicrosoftAccountService accounts, ITeacherDriveMappingService mappings,
        OneDriveBrowserService browser, IEvidenceSubmissionService submissions, IConfiguration configuration, AlFalahDbContext context)
    { _accounts = accounts; _mappings = mappings; _browser = browser; _submissions = submissions; _configuration = configuration; _context = context; }

    public async Task<UploadFileResultDto> UploadAsync(ClaimsPrincipal principal, UploadFileRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        var (teacherId, schoolId, _) = await _accounts.ResolveLinkedTeacherAsync(principal, cancellationToken);
        var reservation = await _submissions.ReserveUploadAsync(teacherId, schoolId, request.TaskId, request.RequestId, cancellationToken);
        if (reservation.ExistingResult is not null) return reservation.ExistingResult;

        var mapping = await _mappings.GetForTeacherAsync(teacherId, cancellationToken);
        var destination = string.IsNullOrWhiteSpace(request.ParentItemId) ? mapping.RootItemId : request.ParentItemId!;
        await _browser.EnsureDescendantAsync(principal, mapping, destination, cancellationToken);
        DriveItemDto item;
        try
        {
            var escapedName = Uri.EscapeDataString(request.FileName.Trim());
            using var content = new StreamContent(request.Content);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType);
            var json = await _browser.SendGraphAsync(principal, HttpMethod.Put,
                $"drives/{Uri.EscapeDataString(mapping.DriveId)}/items/{Uri.EscapeDataString(destination)}:/{escapedName}:/content?@microsoft.graph.conflictBehavior=rename",
                content, cancellationToken);
            item = OneDriveBrowserService.ParseItem(json);
        }
        catch (Exception ex)
        {
            await _submissions.MarkUploadFailedAsync(reservation.OperationId, ex.Message, cancellationToken);
            throw;
        }

        // If this database transaction fails, leave the reservation Pending.
        // A retry is blocked instead of uploading a second OneDrive file.
        return await _submissions.RecordCompletedUploadAsync(reservation.OperationId, teacherId, schoolId, mapping.DriveId, destination, item, cancellationToken);
    }

    public async Task DeleteAsync(ClaimsPrincipal principal, long submissionId, CancellationToken cancellationToken = default)
    {
        var (teacherId, _, _) = await _accounts.ResolveLinkedTeacherAsync(principal, cancellationToken);
        var submission = await _context.TeacherEvidenceSubmissions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == submissionId && x.TeacherId == teacherId, cancellationToken)
            ?? throw new KeyNotFoundException("ملف الدليل غير موجود.");
        if (submission.IsDeleted) return;

        var mapping = await _mappings.GetForTeacherAsync(teacherId, cancellationToken);
        if (!string.Equals(mapping.DriveId, submission.DriveId, StringComparison.Ordinal))
            throw new InvalidOperationException("الملف لا ينتمي إلى مساحة OneDrive الحالية للمعلم.");
        await _browser.DeleteGraphItemAsync(principal, mapping.DriveId, submission.DriveItemId, cancellationToken);
        await _submissions.MarkDeletedAsync(teacherId, submissionId, null, cancellationToken);
    }

    private void Validate(UploadFileRequest request)
    {
        var maxBytes = _configuration.GetValue<long?>("TeacherDrive:MaxUploadBytes") ?? 250L * 1024 * 1024;
        if (request.Content is null || request.Length <= 0) throw new ArgumentException("لا يمكن رفع ملف فارغ.");
        if (request.Length > maxBytes) throw new ArgumentException("حجم الملف أكبر من الحد المسموح.");
        if (string.IsNullOrWhiteSpace(request.FileName) || request.FileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || request.FileName.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("اسم الملف غير صالح.");
        var extension = Path.GetExtension(request.FileName);
        var configured = _configuration.GetSection("TeacherDrive:AllowedExtensions").Get<string[]>();
        var allowed = configured is { Length: > 0 } ? new HashSet<string>(configured.Select(x => x.StartsWith('.') ? x : "." + x), StringComparer.OrdinalIgnoreCase) : DefaultExtensions;
        if (!allowed.Contains(extension)) throw new ArgumentException("نوع الملف غير مسموح.");
    }
}
