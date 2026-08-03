using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;

namespace AlFalah.Application.DTOs.StudentAnalyzer;

public sealed record StudentAnalyzerCapabilitiesDto(
    bool CanAccess,
    bool CanDelegate,
    bool CanManageSettings,
    int? SchoolId,
    string? SchoolName);

public sealed record StudentAnalyzerDelegateDto(
    string UserId,
    string FullName,
    string Username,
    IReadOnlyList<string> Roles,
    bool IsGranted);

public sealed record UpdateStudentAnalyzerGrantsRequest(IReadOnlyList<string> UserIds);

public sealed record StudentAnalyzerSettingsDto(
    StudentAnalyzerProvider ActiveProvider,
    bool HasGroqApiKey,
    string GroqModel,
    bool HasGeminiApiKey,
    string GeminiModel,
    bool HasOpenRouterApiKey,
    string OpenRouterModel,
    DateTimeOffset? UpdatedAt,
    string? UpdatedByFullName);

public sealed record UpdateStudentAnalyzerSettingsRequest(
    StudentAnalyzerProvider ActiveProvider,
    string? GroqApiKey,
    bool ClearGroqApiKey,
    string? GroqModel,
    string? GeminiApiKey,
    bool ClearGeminiApiKey,
    string? GeminiModel,
    string? OpenRouterApiKey,
    bool ClearOpenRouterApiKey,
    string? OpenRouterModel);

public sealed record StudentAnalyzerModelDto(
    string Id,
    string Name,
    string? Description,
    int? ContextLength,
    bool IsFree);

public sealed class StudentAnalyzerFileQuery : PagedQuery
{
    public string? Search { get; set; }
    public StudentAnalyzerFileKind? FileKind { get; set; }
    public DateTimeOffset? UploadedFrom { get; set; }
    public DateTimeOffset? UploadedTo { get; set; }
}

public sealed record StudentAnalyzerFileListItemDto(
    int Id,
    string OriginalFileName,
    string ContentType,
    string Extension,
    StudentAnalyzerFileKind FileKind,
    long SizeBytes,
    string UploadedByFullName,
    DateTimeOffset UploadedAt,
    int AnalysisCount,
    DateTimeOffset? LastAnalyzedAt);

public sealed record StudentAnalyzerStoredFileDto(
    int Id,
    string OriginalFileName,
    string ContentType,
    string Extension,
    StudentAnalyzerFileKind FileKind,
    long SizeBytes,
    string UploadedByFullName,
    DateTimeOffset UploadedAt);

public sealed record StudentAnalyzerFileContentDto(
    byte[] Bytes,
    string ContentType,
    string FileName);

public sealed record StudentAnalyzerDataPointDto(
    string Column,
    string Value,
    decimal? NumericValue);

public sealed record StudentAnalyzerSelectedDataDto(
    IReadOnlyList<StudentAnalyzerDataPointDto> Grants,
    IReadOnlyList<StudentAnalyzerDataPointDto> Deductions);

public sealed record AnalyzeStudentRequest(
    int SourceFileId,
    string StudentName,
    IReadOnlyList<StudentAnalyzerDataPointDto> Grants,
    IReadOnlyList<StudentAnalyzerDataPointDto> Deductions);

public sealed record StudentAnalyzerAnalysisDto(
    int Id,
    int SourceFileId,
    string SourceFileName,
    string StudentName,
    decimal GrantTotal,
    decimal DeductionTotal,
    StudentAnalyzerSelectedDataDto SelectedData,
    string AnalysisText,
    StudentAnalyzerProvider Provider,
    string Model,
    string CreatedByFullName,
    DateTimeOffset CreatedAt);

public sealed class StudentAnalyzerReportQuery : PagedQuery
{
    public string? Search { get; set; }
    public int? SourceFileId { get; set; }
    public StudentAnalyzerProvider? Provider { get; set; }
    public DateTimeOffset? CreatedFrom { get; set; }
    public DateTimeOffset? CreatedTo { get; set; }
}

public sealed record StudentAnalyzerReportListItemDto(
    int Id,
    int SourceFileId,
    string SourceFileName,
    string StudentName,
    decimal GrantTotal,
    decimal DeductionTotal,
    StudentAnalyzerProvider Provider,
    string Model,
    string CreatedByFullName,
    DateTimeOffset CreatedAt);

public sealed record StudentAnalyzerUpload(
    string FileName,
    string ContentType,
    long Length,
    Stream Content);

public sealed record StudentAnalyzerAiRequest(
    StudentAnalyzerProvider Provider,
    string ApiKey,
    string Model,
    string SystemPrompt,
    string UserPrompt);

public sealed record StudentAnalyzerAiResponse(string Text, string Model);
