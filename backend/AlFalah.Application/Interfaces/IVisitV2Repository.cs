using AlFalah.Application.DTOs.Visits;
using AlFalah.Domain.Entities;
using AlFalah.Shared.Models;

namespace AlFalah.Application.Interfaces;

public interface IVisitV2Repository
{
    Task<RubricVersion?> GetRubricAsync(CancellationToken cancellationToken = default);
    Task<ApplicationUser?> GetUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> IsActiveInstructorAsync(string userId, int schoolId, CancellationToken cancellationToken = default);
    Task<string> GetEvaluatorRoleAsync(string userId, int schoolId, CancellationToken cancellationToken = default);
    Task<Visit?> GetAsync(int id, bool tracking, CancellationToken cancellationToken = default);
    Task AddAsync(Visit visit, CancellationToken cancellationToken = default);
    Task<Dictionary<int, List<RubricIndicator>>> GetIndicatorsByStandardAsync(
        int rubricVersionId,
        IReadOnlyCollection<int> standardIds,
        CancellationToken cancellationToken = default);
    Task<PagedResult<VisitV2ArchiveItemDto>> ListAsync(
        VisitV2ArchiveQuery query,
        int? schoolId,
        string? creatorUserId,
        string? instructorUserId,
        bool approvedOnly,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VisitV2EvaluatorFilterDto>> ListEvaluatorsAsync(
        int? schoolId,
        string? creatorUserId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Visit>> ListForExportAsync(
        VisitV2ArchiveQuery query,
        int? schoolId,
        string? creatorUserId,
        string? instructorUserId,
        bool approvedOnly,
        CancellationToken cancellationToken = default);
    Task<int> CountActiveInstructorsAsync(int? schoolId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, string?>> GetEmployeeNumbersAsync(
        IReadOnlyCollection<string> instructorIds,
        CancellationToken cancellationToken = default);
    Task<VisitV2DashboardDto> GetDashboardAsync(
        int? schoolId,
        string? creatorUserId,
        int totalActiveTeachers,
        CancellationToken cancellationToken = default);
    Task<VisitV2PdfAssetSources> GetPdfAssetSourcesAsync(int visitId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, VisitV2PdfAssetSources>> GetPdfAssetSourcesAsync(
        IReadOnlyCollection<int> visitIds,
        CancellationToken cancellationToken = default);
    Task AddReportViewAsync(int visitId, string instructorUserId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);
}
