using AlFalah.Application.DTOs.StudentAnalyzer;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;

namespace AlFalah.Application.Interfaces;

public interface IStudentAnalyzerService
{
    Task<StudentAnalyzerCapabilitiesDto> GetCapabilitiesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StudentAnalyzerDelegateDto>> GetDelegatesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StudentAnalyzerDelegateDto>> UpdateDelegatesAsync(UpdateStudentAnalyzerGrantsRequest request, CancellationToken cancellationToken = default);
    Task<StudentAnalyzerSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task<StudentAnalyzerSettingsDto> UpdateSettingsAsync(UpdateStudentAnalyzerSettingsRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StudentAnalyzerModelDto>> GetModelsAsync(
        StudentAnalyzerProvider provider,
        string? providerApiKey = null,
        CancellationToken cancellationToken = default);
    Task<StudentAnalyzerStoredFileDto> UploadFileAsync(StudentAnalyzerUpload upload, CancellationToken cancellationToken = default);
    Task<PagedResult<StudentAnalyzerFileListItemDto>> GetFilesAsync(StudentAnalyzerFileQuery query, CancellationToken cancellationToken = default);
    Task<StudentAnalyzerFileContentDto> GetFileContentAsync(int fileId, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(int fileId, CancellationToken cancellationToken = default);
    Task<StudentAnalyzerAnalysisDto> AnalyzeAsync(AnalyzeStudentRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<StudentAnalyzerReportListItemDto>> GetReportsAsync(StudentAnalyzerReportQuery query, CancellationToken cancellationToken = default);
    Task<StudentAnalyzerAnalysisDto> GetReportAsync(int reportId, CancellationToken cancellationToken = default);
    Task DeleteReportAsync(int reportId, CancellationToken cancellationToken = default);
}

public interface IStudentAnalyzerRepository
{
    IQueryable<School> GetSchools();
    IQueryable<ApplicationUser> GetUsers();
    IQueryable<UserSchoolRole> GetUserSchoolRoles();
    IQueryable<StudentAnalyzerAccessGrant> GetGrants();
    IQueryable<SchoolStudentAnalyzerSettings> GetSettings();
    IQueryable<StudentAnalyzerSourceFile> GetFiles();
    IQueryable<StudentAnalyzerReport> GetReports();
    Task<SchoolStudentAnalyzerSettings?> GetTrackedSettingsAsync(int schoolId, CancellationToken cancellationToken);
    Task<List<StudentAnalyzerAccessGrant>> GetTrackedGrantsAsync(int schoolId, CancellationToken cancellationToken);
    Task<StudentAnalyzerSourceFile?> GetTrackedFileAsync(int fileId, CancellationToken cancellationToken);
    Task<StudentAnalyzerReport?> GetTrackedReportAsync(int reportId, CancellationToken cancellationToken);
    Task<List<StudentAnalyzerReport>> GetTrackedReportsByFileAsync(int fileId, CancellationToken cancellationToken);
    void AddSettings(SchoolStudentAnalyzerSettings settings);
    void AddGrant(StudentAnalyzerAccessGrant grant);
    void AddFile(StudentAnalyzerSourceFile file);
    void AddReport(StudentAnalyzerReport report);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IStudentAnalyzerAiClient
{
    Task<StudentAnalyzerAiResponse> AnalyzeAsync(StudentAnalyzerAiRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StudentAnalyzerModelDto>> GetModelsAsync(StudentAnalyzerProvider provider, string apiKey, CancellationToken cancellationToken = default);
}
