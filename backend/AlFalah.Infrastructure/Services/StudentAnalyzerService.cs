using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlFalah.Application.Analysis;
using AlFalah.Application.Common;
using AlFalah.Application.DTOs.StudentAnalyzer;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AlFalah.Infrastructure.Services;

public sealed class StudentAnalyzerService : IStudentAnalyzerService
{
    private const long MaxFileBytes = 50L * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IStudentAnalyzerRepository _repository;
    private readonly IStudentAnalyzerAiClient _aiClient;
    private readonly ICurrentUserService _currentUser;
    private readonly StudentAnalyzerCredentialProtector _protector;
    private readonly AuditLogWriter _audit;
    private readonly IMemoryCache _cache;

    public StudentAnalyzerService(
        IStudentAnalyzerRepository repository,
        IStudentAnalyzerAiClient aiClient,
        ICurrentUserService currentUser,
        StudentAnalyzerCredentialProtector protector,
        AuditLogWriter audit,
        IMemoryCache cache)
    {
        _repository = repository;
        _aiClient = aiClient;
        _currentUser = currentUser;
        _protector = protector;
        _audit = audit;
        _cache = cache;
    }

    public async Task<StudentAnalyzerCapabilitiesDto> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        var access = await ResolveAccessAsync(cancellationToken);
        return new StudentAnalyzerCapabilitiesDto(
            access.CanAccess,
            access.IsManager,
            access.CanAccess,
            access.SchoolId,
            access.SchoolName);
    }

    public async Task<IReadOnlyList<StudentAnalyzerDelegateDto>> GetDelegatesAsync(CancellationToken cancellationToken = default)
    {
        var access = await RequireManagerAsync(cancellationToken);
        return await BuildDelegatesAsync(access, cancellationToken);
    }

    public async Task<IReadOnlyList<StudentAnalyzerDelegateDto>> UpdateDelegatesAsync(
        UpdateStudentAnalyzerGrantsRequest request,
        CancellationToken cancellationToken = default)
    {
        var access = await RequireManagerAsync(cancellationToken);
        var selectedIds = request.UserIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Where(x => !string.Equals(x, access.UserId, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        var eligibleIds = await _repository.GetUserSchoolRoles()
            .Where(x => x.SchoolId == access.SchoolId && x.IsActive && selectedIds.Contains(x.UserId))
            .Where(x => x.User.IsActive)
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (eligibleIds.Count != selectedIds.Count)
            throw new ArgumentException("تحتوي قائمة التفويض على مستخدم غير نشط أو غير تابع لهذه المدرسة.");

        var now = DateTimeOffset.UtcNow;
        var existing = await _repository.GetTrackedGrantsAsync(access.SchoolId, cancellationToken);
        var existingIds = existing.Select(x => x.UserId).ToHashSet(StringComparer.Ordinal);
        foreach (var grant in existing.Where(x => !selectedIds.Contains(x.UserId)))
        {
            grant.IsDeleted = true;
            grant.DeletedAt = now;
            grant.DeletedByUserId = access.UserId;
        }
        foreach (var userId in selectedIds.Where(x => !existingIds.Contains(x)))
        {
            _repository.AddGrant(new StudentAnalyzerAccessGrant
            {
                SchoolId = access.SchoolId,
                UserId = userId,
                GrantedByUserId = access.UserId,
                GrantedAt = now
            });
        }

        _audit.Write(
            access.SchoolId,
            access.UserId,
            "StudentAnalyzer.DelegatesUpdated",
            nameof(StudentAnalyzerAccessGrant),
            access.SchoolId.ToString(CultureInfo.InvariantCulture),
            "تحديث مستخدمي محلل تقارير الطلاب",
            new { UserIds = existingIds.OrderBy(x => x).ToArray() },
            new { UserIds = selectedIds.OrderBy(x => x).ToArray() });
        await _repository.SaveChangesAsync(cancellationToken);
        return await BuildDelegatesAsync(access, cancellationToken);
    }

    public async Task<StudentAnalyzerSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var access = await RequireAccessAsync(cancellationToken);
        var settings = await _repository.GetSettings()
            .Where(x => x.SchoolId == access.SchoolId)
            .Select(x => new SettingsProjection(
                x.ActiveProvider,
                x.ProtectedGroqApiKey != null,
                x.GroqModel,
                x.ProtectedGeminiApiKey != null,
                x.GeminiModel,
                x.ProtectedOpenRouterApiKey != null,
                x.OpenRouterModel,
                x.UpdatedAt,
                (x.UpdatedByUser.FirstName + " " + x.UpdatedByUser.LastName).Trim()))
            .FirstOrDefaultAsync(cancellationToken);
        return settings is null ? DefaultSettingsDto() : ToDto(settings);
    }

    public async Task<StudentAnalyzerSettingsDto> UpdateSettingsAsync(
        UpdateStudentAnalyzerSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var access = await RequireAccessAsync(cancellationToken);
        var settings = await _repository.GetTrackedSettingsAsync(access.SchoolId, cancellationToken);
        var oldAudit = settings is null ? null : SettingsAudit(settings);
        if (settings is null)
        {
            settings = new SchoolStudentAnalyzerSettings
            {
                SchoolId = access.SchoolId,
                UpdatedByUserId = access.UserId
            };
            _repository.AddSettings(settings);
        }

        settings.ActiveProvider = request.ActiveProvider;
        settings.GroqModel = NormalizeModel(request.GroqModel, settings.GroqModel, 200);
        settings.GeminiModel = NormalizeModel(request.GeminiModel, settings.GeminiModel, 200);
        settings.OpenRouterModel = NormalizeModel(request.OpenRouterModel, settings.OpenRouterModel, 300);
        settings.ProtectedGroqApiKey = UpdateProtectedKey(
            settings.ProtectedGroqApiKey, request.GroqApiKey, request.ClearGroqApiKey);
        settings.ProtectedGeminiApiKey = UpdateProtectedKey(
            settings.ProtectedGeminiApiKey, request.GeminiApiKey, request.ClearGeminiApiKey);
        settings.ProtectedOpenRouterApiKey = UpdateProtectedKey(
            settings.ProtectedOpenRouterApiKey, request.OpenRouterApiKey, request.ClearOpenRouterApiKey);
        settings.UpdatedAt = DateTimeOffset.UtcNow;
        settings.UpdatedByUserId = access.UserId;

        EnsureConfiguredProvider(settings);
        if (settings.ActiveProvider == StudentAnalyzerProvider.OpenRouter)
        {
            var apiKey = _protector.Unprotect(settings.ProtectedOpenRouterApiKey!);
            var availableModels = await LoadModelsAsync(StudentAnalyzerProvider.OpenRouter, apiKey, bypassCache: true, cancellationToken);
            if (!availableModels.Any(x => string.Equals(x.Id, settings.OpenRouterModel, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("موديل OpenRouter المحدد ليس ضمن الموديلات المجانية المتاحة حاليًا.");
        }

        _audit.Write(
            access.SchoolId,
            access.UserId,
            "StudentAnalyzer.SettingsUpdated",
            nameof(SchoolStudentAnalyzerSettings),
            access.SchoolId.ToString(CultureInfo.InvariantCulture),
            "تحديث إعدادات مزود التحليل",
            oldAudit,
            SettingsAudit(settings));
        await _repository.SaveChangesAsync(cancellationToken);
        return await GetSettingsAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StudentAnalyzerModelDto>> GetModelsAsync(
        StudentAnalyzerProvider provider,
        string? providerApiKey = null,
        CancellationToken cancellationToken = default)
    {
        var access = await RequireAccessAsync(cancellationToken);
        var key = providerApiKey?.Trim();
        if (key?.Length > 4000)
            throw new ArgumentException("مفتاح مزود الذكاء الاصطناعي أطول من الحد المسموح.");
        if (string.IsNullOrWhiteSpace(key))
        {
            if (provider == StudentAnalyzerProvider.OpenRouter)
            {
                key = string.Empty;
            }
            else
            {
                var settings = await _repository.GetSettings()
                    .FirstOrDefaultAsync(x => x.SchoolId == access.SchoolId, cancellationToken)
                    ?? throw new InvalidOperationException("أدخل مفتاح المزود أو احفظه أولًا حتى يمكن تحميل قائمة الموديلات.");
                key = GetProviderKey(settings, provider);
            }
        }
        return await LoadModelsAsync(provider, key, bypassCache: false, cancellationToken);
    }

    public async Task<StudentAnalyzerStoredFileDto> UploadFileAsync(
        StudentAnalyzerUpload upload,
        CancellationToken cancellationToken = default)
    {
        var access = await RequireAccessAsync(cancellationToken);
        if (upload.Length <= 0) throw new ArgumentException("الملف فارغ.");
        if (upload.Length > MaxFileBytes) throw new ArgumentException("حجم الملف أكبر من الحد الأقصى 50 ميجابايت.");

        var safeName = Path.GetFileName(upload.FileName.Trim());
        if (string.IsNullOrWhiteSpace(safeName)) throw new ArgumentException("اسم الملف غير صالح.");
        if (safeName.Length > 260) throw new ArgumentException("اسم الملف أطول من الحد المسموح.");
        var extension = Path.GetExtension(safeName).ToLowerInvariant();
        var fileKind = ResolveFileKind(extension);
        var content = await ReadWithLimitAsync(upload.Content, MaxFileBytes, cancellationToken);
        ValidateMagicBytes(content, extension);

        var entity = new StudentAnalyzerSourceFile
        {
            SchoolId = access.SchoolId,
            OriginalFileName = safeName,
            ContentType = ResolveContentType(extension, upload.ContentType),
            Extension = extension,
            FileKind = fileKind,
            SizeBytes = content.LongLength,
            Sha256 = Convert.ToHexString(SHA256.HashData(content)),
            Content = content,
            UploadedByUserId = access.UserId
        };
        _repository.AddFile(entity);
        _audit.Write(
            access.SchoolId,
            access.UserId,
            "StudentAnalyzer.FileUploaded",
            nameof(StudentAnalyzerSourceFile),
            null,
            "رفع ملف للتحليل",
            newValues: new { entity.OriginalFileName, entity.SizeBytes, entity.Sha256 });
        await _repository.SaveChangesAsync(cancellationToken);
        return new StudentAnalyzerStoredFileDto(
            entity.Id,
            entity.OriginalFileName,
            entity.ContentType,
            entity.Extension,
            entity.FileKind,
            entity.SizeBytes,
            await GetUserFullNameAsync(access.UserId, cancellationToken),
            entity.UploadedAt);
    }

    public async Task<PagedResult<StudentAnalyzerFileListItemDto>> GetFilesAsync(
        StudentAnalyzerFileQuery query,
        CancellationToken cancellationToken = default)
    {
        var access = await RequireAccessAsync(cancellationToken);
        var files = _repository.GetFiles().Where(x => x.SchoolId == access.SchoolId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            files = files.Where(x => x.OriginalFileName.Contains(search)
                || (x.UploadedByUser.FirstName + " " + x.UploadedByUser.LastName).Contains(search));
        }
        if (query.FileKind.HasValue) files = files.Where(x => x.FileKind == query.FileKind.Value);
        if (query.UploadedFrom.HasValue) files = files.Where(x => x.UploadedAt >= query.UploadedFrom.Value);
        if (query.UploadedTo.HasValue) files = files.Where(x => x.UploadedAt <= query.UploadedTo.Value);

        var totalCount = await files.CountAsync(cancellationToken);
        var items = await BuildFilePageQuery(files, query).ToListAsync(cancellationToken);
        return new PagedResult<StudentAnalyzerFileListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<StudentAnalyzerFileContentDto> GetFileContentAsync(
        int fileId,
        CancellationToken cancellationToken = default)
    {
        var access = await RequireAccessAsync(cancellationToken);
        return await _repository.GetFiles()
            .Where(x => x.Id == fileId && x.SchoolId == access.SchoolId)
            .Select(x => new StudentAnalyzerFileContentDto(x.Content, x.ContentType, x.OriginalFileName))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("ملف التحليل غير موجود.");
    }

    public async Task DeleteFileAsync(int fileId, CancellationToken cancellationToken = default)
    {
        var access = await RequireAccessAsync(cancellationToken);
        var file = await _repository.GetTrackedFileAsync(fileId, cancellationToken)
            ?? throw new KeyNotFoundException("ملف التحليل غير موجود.");
        EnsureSchool(file.SchoolId, access.SchoolId);
        var now = DateTimeOffset.UtcNow;
        file.IsDeleted = true;
        file.DeletedAt = now;
        file.DeletedByUserId = access.UserId;
        var reports = await _repository.GetTrackedReportsByFileAsync(fileId, cancellationToken);
        foreach (var report in reports)
        {
            report.IsDeleted = true;
            report.DeletedAt = now;
            report.DeletedByUserId = access.UserId;
        }
        _audit.Write(
            access.SchoolId,
            access.UserId,
            "StudentAnalyzer.FileDeleted",
            nameof(StudentAnalyzerSourceFile),
            file.Id.ToString(CultureInfo.InvariantCulture),
            "حذف ملف التحليل وتقاريره",
            new { file.OriginalFileName, ReportCount = reports.Count });
        await _repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<StudentAnalyzerAnalysisDto> AnalyzeAsync(
        AnalyzeStudentRequest request,
        CancellationToken cancellationToken = default)
    {
        var access = await RequireAccessAsync(cancellationToken);
        var sourceFile = await _repository.GetFiles()
            .Where(x => x.Id == request.SourceFileId && x.SchoolId == access.SchoolId)
            .Select(x => new { x.Id, x.OriginalFileName })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("ملف التحليل غير موجود.");
        var grants = NormalizePoints(request.Grants);
        var deductions = NormalizePoints(request.Deductions);
        if (grants.Count == 0 && deductions.Count == 0)
            throw new ArgumentException("يجب اختيار بند منح أو خصم واحد على الأقل.");

        var settings = await _repository.GetSettings()
            .FirstOrDefaultAsync(x => x.SchoolId == access.SchoolId, cancellationToken)
            ?? throw new InvalidOperationException("لم يتم إعداد مزود الذكاء الاصطناعي لهذه المدرسة.");
        EnsureConfiguredProvider(settings);
        var provider = settings.ActiveProvider;
        var apiKey = GetProviderKey(settings, provider);
        var model = GetProviderModel(settings, provider);
        if (provider == StudentAnalyzerProvider.OpenRouter)
        {
            var availableModels = await LoadModelsAsync(provider, apiKey, bypassCache: false, cancellationToken);
            if (!availableModels.Any(x => string.Equals(x.Id, model, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("موديل OpenRouter المحدد لم يعد مجانيًا أو متاحًا. اختر موديلًا مجانيًا آخر من الإعدادات.");
        }

        var prompt = StudentAnalysisPromptBuilder.Build(request.StudentName.Trim(), grants, deductions);
        var aiResponse = await _aiClient.AnalyzeAsync(new StudentAnalyzerAiRequest(
            provider,
            apiKey,
            model,
            StudentAnalysisPromptBuilder.SystemPrompt,
            prompt), cancellationToken);
        var analysisText = StudentAnalysisTextSanitizer.Sanitize(aiResponse.Text);
        if (string.IsNullOrWhiteSpace(analysisText))
            throw new InvalidOperationException("أعاد مزود الذكاء الاصطناعي استجابة غير صالحة. حاول مرة أخرى أو اختر موديلًا آخر.");
        var selectedData = new StudentAnalyzerSelectedDataDto(grants, deductions);
        var report = new StudentAnalyzerReport
        {
            SchoolId = access.SchoolId,
            SourceFileId = sourceFile.Id,
            StudentName = request.StudentName.Trim(),
            GrantTotal = grants.Sum(x => x.NumericValue ?? 0m),
            DeductionTotal = deductions.Sum(x => x.NumericValue ?? 0m),
            SelectedDataJson = JsonSerializer.Serialize(selectedData, JsonOptions),
            AnalysisText = analysisText,
            Provider = provider,
            Model = aiResponse.Model,
            PromptVersion = StudentAnalysisPromptBuilder.Version,
            CreatedByUserId = access.UserId
        };
        _repository.AddReport(report);
        _audit.Write(
            access.SchoolId,
            access.UserId,
            "StudentAnalyzer.ReportCreated",
            nameof(StudentAnalyzerReport),
            null,
            "إنشاء تحليل طالب",
            newValues: new { report.SourceFileId, report.StudentName, report.Provider, report.Model });
        await _repository.SaveChangesAsync(cancellationToken);
        return new StudentAnalyzerAnalysisDto(
            report.Id,
            report.SourceFileId,
            sourceFile.OriginalFileName,
            report.StudentName,
            report.GrantTotal,
            report.DeductionTotal,
            selectedData,
            report.AnalysisText,
            report.Provider,
            report.Model,
            await GetUserFullNameAsync(access.UserId, cancellationToken),
            report.CreatedAt);
    }

    public async Task<PagedResult<StudentAnalyzerReportListItemDto>> GetReportsAsync(
        StudentAnalyzerReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var access = await RequireAccessAsync(cancellationToken);
        var reports = _repository.GetReports().Where(x => x.SchoolId == access.SchoolId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            reports = reports.Where(x => x.StudentName.Contains(search) || x.SourceFile.OriginalFileName.Contains(search));
        }
        if (query.SourceFileId.HasValue) reports = reports.Where(x => x.SourceFileId == query.SourceFileId.Value);
        if (query.Provider.HasValue) reports = reports.Where(x => x.Provider == query.Provider.Value);
        if (query.CreatedFrom.HasValue) reports = reports.Where(x => x.CreatedAt >= query.CreatedFrom.Value);
        if (query.CreatedTo.HasValue) reports = reports.Where(x => x.CreatedAt <= query.CreatedTo.Value);

        var totalCount = await reports.CountAsync(cancellationToken);
        var items = await BuildReportPageQuery(reports, query).ToListAsync(cancellationToken);
        return new PagedResult<StudentAnalyzerReportListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<StudentAnalyzerAnalysisDto> GetReportAsync(
        int reportId,
        CancellationToken cancellationToken = default)
    {
        var access = await RequireAccessAsync(cancellationToken);
        var row = await _repository.GetReports()
            .Where(x => x.Id == reportId && x.SchoolId == access.SchoolId)
            .Select(x => new ReportProjection(
                x.Id,
                x.SourceFileId,
                x.SourceFile.OriginalFileName,
                x.StudentName,
                x.GrantTotal,
                x.DeductionTotal,
                x.SelectedDataJson,
                x.AnalysisText,
                x.Provider,
                x.Model,
                (x.CreatedByUser.FirstName + " " + x.CreatedByUser.LastName).Trim(),
                x.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("تقرير التحليل غير موجود.");
        var selectedData = JsonSerializer.Deserialize<StudentAnalyzerSelectedDataDto>(row.SelectedDataJson, JsonOptions)
            ?? throw new InvalidOperationException("تعذر قراءة بيانات التقرير المحفوظ.");
        return new StudentAnalyzerAnalysisDto(
            row.Id,
            row.SourceFileId,
            row.SourceFileName,
            row.StudentName,
            row.GrantTotal,
            row.DeductionTotal,
            selectedData,
            StudentAnalysisTextSanitizer.Sanitize(row.AnalysisText),
            row.Provider,
            row.Model,
            row.CreatedByFullName,
            row.CreatedAt);
    }

    public async Task DeleteReportAsync(int reportId, CancellationToken cancellationToken = default)
    {
        var access = await RequireAccessAsync(cancellationToken);
        var report = await _repository.GetTrackedReportAsync(reportId, cancellationToken)
            ?? throw new KeyNotFoundException("تقرير التحليل غير موجود.");
        EnsureSchool(report.SchoolId, access.SchoolId);
        report.IsDeleted = true;
        report.DeletedAt = DateTimeOffset.UtcNow;
        report.DeletedByUserId = access.UserId;
        _audit.Write(
            access.SchoolId,
            access.UserId,
            "StudentAnalyzer.ReportDeleted",
            nameof(StudentAnalyzerReport),
            report.Id.ToString(CultureInfo.InvariantCulture),
            "حذف تقرير تحليل طالب",
            new { report.StudentName, report.SourceFileId });
        await _repository.SaveChangesAsync(cancellationToken);
    }

    private async Task<AccessContext> ResolveAccessAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        var schoolId = _currentUser.ActiveSchoolId;
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(userId) || schoolId is null || _currentUser.IsGlobalAdmin())
            return AccessContext.Denied;
        var school = await _repository.GetSchools()
            .Where(x => x.Id == schoolId.Value && x.IsActive)
            .Select(x => new { x.Id, x.Name, x.ManagerUserId })
            .FirstOrDefaultAsync(cancellationToken);
        if (school is null) return AccessContext.Denied;

        var isManager = _currentUser.IsInRole(RoleNames.SchoolManager)
            && string.Equals(school.ManagerUserId, userId, StringComparison.Ordinal);
        var isDelegate = false;
        if (!isManager)
        {
            isDelegate = await _repository.GetGrants()
                .Where(x => x.SchoolId == school.Id && x.UserId == userId)
                .AnyAsync(cancellationToken)
                && await _repository.GetUserSchoolRoles()
                    .Where(x => x.SchoolId == school.Id && x.UserId == userId && x.IsActive && x.User.IsActive)
                    .AnyAsync(cancellationToken);
        }
        return new AccessContext(isManager || isDelegate, isManager, school.Id, school.Name, userId);
    }

    private async Task<AccessContext> RequireAccessAsync(CancellationToken cancellationToken)
    {
        var access = await ResolveAccessAsync(cancellationToken);
        if (!access.CanAccess)
            throw new UnauthorizedSchoolAccessException("محلل تقارير الطلاب متاح لمدير المدرسة أو المستخدم المفوّض منه فقط.");
        return access;
    }

    private async Task<AccessContext> RequireManagerAsync(CancellationToken cancellationToken)
    {
        var access = await ResolveAccessAsync(cancellationToken);
        if (!access.IsManager)
            throw new UnauthorizedSchoolAccessException("تفويض مستخدمي محلل تقارير الطلاب متاح لمدير المدرسة فقط.");
        return access;
    }

    private async Task<List<StudentAnalyzerDelegateDto>> BuildDelegatesAsync(
        AccessContext access,
        CancellationToken cancellationToken)
    {
        var granted = (await _repository.GetGrants()
            .Where(x => x.SchoolId == access.SchoolId)
            .Select(x => x.UserId)
            .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);
        var rows = await _repository.GetUserSchoolRoles()
            .Where(x => x.SchoolId == access.SchoolId && x.IsActive && x.User.IsActive && x.UserId != access.UserId)
            .Select(x => new
            {
                x.UserId,
                x.User.UserName,
                x.User.FirstName,
                x.User.LastName,
                Role = x.Role.Name!
            })
            .ToListAsync(cancellationToken);
        return rows.GroupBy(x => x.UserId, StringComparer.Ordinal)
            .Select(group => new StudentAnalyzerDelegateDto(
                group.Key,
                (group.First().FirstName + " " + group.First().LastName).Trim(),
                group.First().UserName ?? string.Empty,
                group.Select(x => x.Role).Distinct(StringComparer.Ordinal).OrderBy(x => x).ToList(),
                granted.Contains(group.Key)))
            .OrderBy(x => x.FullName)
            .ToList();
    }

    private async Task<IReadOnlyList<StudentAnalyzerModelDto>> LoadModelsAsync(
        StudentAnalyzerProvider provider,
        string apiKey,
        bool bypassCache,
        CancellationToken cancellationToken)
    {
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)));
        var cacheKey = $"student-analyzer-models:{provider}:{fingerprint}";
        if (bypassCache) _cache.Remove(cacheKey);
        var models = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = provider == StudentAnalyzerProvider.OpenRouter
                ? TimeSpan.FromMinutes(15)
                : TimeSpan.FromMinutes(30);
            return await _aiClient.GetModelsAsync(provider, apiKey, cancellationToken);
        });
        return models ?? Array.Empty<StudentAnalyzerModelDto>();
    }

    private string? UpdateProtectedKey(string? current, string? replacement, bool clear)
    {
        if (clear) return null;
        var normalized = replacement?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? current : _protector.Protect(normalized);
    }

    private static void EnsureConfiguredProvider(SchoolStudentAnalyzerSettings settings)
    {
        var (key, model) = settings.ActiveProvider switch
        {
            StudentAnalyzerProvider.Groq => (settings.ProtectedGroqApiKey, settings.GroqModel),
            StudentAnalyzerProvider.Gemini => (settings.ProtectedGeminiApiKey, settings.GeminiModel),
            StudentAnalyzerProvider.OpenRouter => (settings.ProtectedOpenRouterApiKey, settings.OpenRouterModel),
            _ => throw new ArgumentException("مزود الذكاء الاصطناعي غير مدعوم.")
        };
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("يجب إدخال مفتاح API للمزود النشط قبل حفظ الإعدادات.");
        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("يجب اختيار موديل للمزود النشط.");
    }

    private string GetProviderKey(SchoolStudentAnalyzerSettings settings, StudentAnalyzerProvider provider)
    {
        var protectedKey = provider switch
        {
            StudentAnalyzerProvider.Groq => settings.ProtectedGroqApiKey,
            StudentAnalyzerProvider.Gemini => settings.ProtectedGeminiApiKey,
            StudentAnalyzerProvider.OpenRouter => settings.ProtectedOpenRouterApiKey,
            _ => null
        };
        if (string.IsNullOrWhiteSpace(protectedKey))
            throw new InvalidOperationException("لم يتم إدخال مفتاح API لهذا المزود.");
        return _protector.Unprotect(protectedKey);
    }

    private static string GetProviderModel(SchoolStudentAnalyzerSettings settings, StudentAnalyzerProvider provider) => provider switch
    {
        StudentAnalyzerProvider.Groq => settings.GroqModel,
        StudentAnalyzerProvider.Gemini => settings.GeminiModel,
        StudentAnalyzerProvider.OpenRouter => settings.OpenRouterModel,
        _ => throw new ArgumentException("مزود الذكاء الاصطناعي غير مدعوم.")
    };

    private static List<StudentAnalyzerDataPointDto> NormalizePoints(IReadOnlyList<StudentAnalyzerDataPointDto> points)
    {
        var result = new List<StudentAnalyzerDataPointDto>();
        foreach (var point in points)
        {
            var column = point.Column.Trim();
            var value = point.Value.Trim();
            if (column.Length == 0 || value.Length == 0) continue;
            var numeric = point.NumericValue;
            if (!numeric.HasValue && decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                numeric = parsed;
            if (numeric == 0m) continue;
            result.Add(new StudentAnalyzerDataPointDto(column, value, numeric));
        }
        return result;
    }

    internal static IQueryable<StudentAnalyzerFileListItemDto> BuildFilePageQuery(
        IQueryable<StudentAnalyzerSourceFile> query,
        StudentAnalyzerFileQuery request) => ApplyFileSort(query, request)
        .Skip((request.Page - 1) * request.PageSize)
        .Take(request.PageSize)
        .Select(x => new StudentAnalyzerFileListItemDto(
            x.Id,
            x.OriginalFileName,
            x.ContentType,
            x.Extension,
            x.FileKind,
            x.SizeBytes,
            (x.UploadedByUser.FirstName + " " + x.UploadedByUser.LastName).Trim(),
            x.UploadedAt,
            x.Reports.Count(),
            x.Reports.Select(r => (DateTimeOffset?)r.CreatedAt).Max()));

    internal static IQueryable<StudentAnalyzerReportListItemDto> BuildReportPageQuery(
        IQueryable<StudentAnalyzerReport> query,
        StudentAnalyzerReportQuery request) => ApplyReportSort(query, request)
        .Skip((request.Page - 1) * request.PageSize)
        .Take(request.PageSize)
        .Select(x => new StudentAnalyzerReportListItemDto(
            x.Id,
            x.SourceFileId,
            x.SourceFile.OriginalFileName,
            x.StudentName,
            x.GrantTotal,
            x.DeductionTotal,
            x.Provider,
            x.Model,
            (x.CreatedByUser.FirstName + " " + x.CreatedByUser.LastName).Trim(),
            x.CreatedAt));

    private static IQueryable<StudentAnalyzerSourceFile> ApplyFileSort(
        IQueryable<StudentAnalyzerSourceFile> query,
        StudentAnalyzerFileQuery request) => request.SortBy?.Trim().ToLowerInvariant() switch
    {
        "name" => request.SortDesc ? query.OrderByDescending(x => x.OriginalFileName) : query.OrderBy(x => x.OriginalFileName),
        "size" => request.SortDesc ? query.OrderByDescending(x => x.SizeBytes) : query.OrderBy(x => x.SizeBytes),
        "analysiscount" => request.SortDesc ? query.OrderByDescending(x => x.Reports.Count()) : query.OrderBy(x => x.Reports.Count()),
        _ => request.SortDesc ? query.OrderBy(x => x.UploadedAt) : query.OrderByDescending(x => x.UploadedAt)
    };

    private static IQueryable<StudentAnalyzerReport> ApplyReportSort(
        IQueryable<StudentAnalyzerReport> query,
        StudentAnalyzerReportQuery request) => request.SortBy?.Trim().ToLowerInvariant() switch
    {
        "studentname" => request.SortDesc ? query.OrderByDescending(x => x.StudentName) : query.OrderBy(x => x.StudentName),
        "granttotal" => request.SortDesc ? query.OrderByDescending(x => x.GrantTotal) : query.OrderBy(x => x.GrantTotal),
        "deductiontotal" => request.SortDesc ? query.OrderByDescending(x => x.DeductionTotal) : query.OrderBy(x => x.DeductionTotal),
        _ => request.SortDesc ? query.OrderBy(x => x.CreatedAt) : query.OrderByDescending(x => x.CreatedAt)
    };

    private async Task<string> GetUserFullNameAsync(string userId, CancellationToken cancellationToken) =>
        await _repository.GetUsers()
            .Where(x => x.Id == userId)
            .Select(x => (x.FirstName + " " + x.LastName).Trim())
            .FirstOrDefaultAsync(cancellationToken) ?? _currentUser.Username ?? string.Empty;

    private static string NormalizeModel(string? requested, string current, int maxLength)
    {
        var normalized = requested?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return current;
        if (normalized.Length > maxLength) throw new ArgumentException("اسم الموديل أطول من الحد المسموح.");
        return normalized;
    }

    private static StudentAnalyzerFileKind ResolveFileKind(string extension) => extension switch
    {
        ".pdf" => StudentAnalyzerFileKind.Pdf,
        ".xlsx" or ".xls" or ".ods" => StudentAnalyzerFileKind.Spreadsheet,
        ".csv" => StudentAnalyzerFileKind.Csv,
        _ => throw new ArgumentException("نوع الملف غير مدعوم. استخدم PDF أو Excel أو CSV.")
    };

    private static string ResolveContentType(string extension, string supplied) => extension switch
    {
        ".pdf" => "application/pdf",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".xls" => "application/vnd.ms-excel",
        ".ods" => "application/vnd.oasis.opendocument.spreadsheet",
        ".csv" => "text/csv; charset=utf-8",
        _ => string.IsNullOrWhiteSpace(supplied) ? "application/octet-stream" : supplied
    };

    private static void ValidateMagicBytes(byte[] bytes, string extension)
    {
        if (extension == ".pdf" && (bytes.Length < 5 || Encoding.ASCII.GetString(bytes, 0, 5) != "%PDF-"))
            throw new ArgumentException("محتوى الملف لا يطابق صيغة PDF.");
        if ((extension == ".xlsx" || extension == ".ods") && (bytes.Length < 2 || bytes[0] != 0x50 || bytes[1] != 0x4B))
            throw new ArgumentException("محتوى الملف لا يطابق صيغة الملف المجدول.");
        if (extension == ".xls")
        {
            byte[] ole = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
            if (bytes.Length < ole.Length || !bytes.AsSpan(0, ole.Length).SequenceEqual(ole))
                throw new ArgumentException("محتوى الملف لا يطابق صيغة XLS.");
        }
        if (extension == ".csv" && bytes.Take(Math.Min(bytes.Length, 4096)).Any(x => x == 0))
            throw new ArgumentException("ملف CSV يحتوي على بيانات ثنائية غير صالحة.");
    }

    private static async Task<byte[]> ReadWithLimitAsync(Stream stream, long limit, CancellationToken cancellationToken)
    {
        await using var memory = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        long total = 0;
        while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            total += read;
            if (total > limit) throw new ArgumentException("حجم الملف أكبر من الحد الأقصى 50 ميجابايت.");
            await memory.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        return memory.ToArray();
    }

    private static void EnsureSchool(int actual, int expected)
    {
        if (actual != expected) throw UnauthorizedSchoolAccessException.OutsideScope(expected, actual);
    }

    private static StudentAnalyzerSettingsDto DefaultSettingsDto() => new(
        StudentAnalyzerProvider.Groq,
        false,
        "llama-3.3-70b-versatile",
        false,
        "gemini-2.5-flash",
        false,
        "openrouter/free",
        null,
        null);

    private static StudentAnalyzerSettingsDto ToDto(SettingsProjection x) => new(
        x.ActiveProvider,
        x.HasGroqKey,
        x.GroqModel,
        x.HasGeminiKey,
        x.GeminiModel,
        x.HasOpenRouterKey,
        x.OpenRouterModel,
        x.UpdatedAt,
        x.UpdatedByFullName);

    private static object SettingsAudit(SchoolStudentAnalyzerSettings settings) => new
    {
        settings.ActiveProvider,
        HasGroqKey = settings.ProtectedGroqApiKey != null,
        settings.GroqModel,
        HasGeminiKey = settings.ProtectedGeminiApiKey != null,
        settings.GeminiModel,
        HasOpenRouterKey = settings.ProtectedOpenRouterApiKey != null,
        settings.OpenRouterModel
    };

    private sealed record AccessContext(
        bool CanAccess,
        bool IsManager,
        int SchoolId,
        string? SchoolName,
        string UserId)
    {
        public static readonly AccessContext Denied = new(false, false, 0, null, string.Empty);
    }

    private sealed record SettingsProjection(
        StudentAnalyzerProvider ActiveProvider,
        bool HasGroqKey,
        string GroqModel,
        bool HasGeminiKey,
        string GeminiModel,
        bool HasOpenRouterKey,
        string OpenRouterModel,
        DateTimeOffset UpdatedAt,
        string UpdatedByFullName);

    private sealed record ReportProjection(
        int Id,
        int SourceFileId,
        string SourceFileName,
        string StudentName,
        decimal GrantTotal,
        decimal DeductionTotal,
        string SelectedDataJson,
        string AnalysisText,
        StudentAnalyzerProvider Provider,
        string Model,
        string CreatedByFullName,
        DateTimeOffset CreatedAt);
}
