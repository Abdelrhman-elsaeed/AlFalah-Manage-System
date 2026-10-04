using AlFalah.Application.DTOs.EvidenceMatrix;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.Storage;

public sealed class StorageEvidenceReadService(IEvidenceRepository repository, EvidenceWorkflowContext context) : IStorageEvidenceReadService
{
    public async Task<IReadOnlyList<AcademicYearDto>> YearsAsync(CancellationToken ct = default)
    {
        await context.SchoolPermissionAsync(PermissionNames.StorageViewSchool, ct);
        return await repository.YearsAsync(ct);
    }
    public async Task<EvidenceMatrixDto> MatrixAsync(EvidenceMatrixFilterDto filter, CancellationToken ct = default)
    {
        await context.SchoolPermissionAsync(PermissionNames.StorageViewSchool, ct);
        if (filter.SchoolId != null && filter.SchoolId != context.School) throw new UnauthorizedAccessException("المدرسة خارج النطاق.");
        var matrix = await repository.MatrixAsync(context.School, filter, ct);
        foreach (var file in await repository.ApprovedFileIdsAsync(context.School, matrix.AcademicYear.Id, filter.TeacherId, ct))
        {
            try { await context.FileAsync(file, false, true, ct); }
            catch (KeyNotFoundException) { /* Missing Drive bytes are no longer approved evidence. */ }
        }
        return await repository.MatrixAsync(context.School, filter, ct);
    }
    public async Task<EvidenceCellFilesDto> CellAsync(int teacher, int task, int year, CancellationToken ct = default)
    {
        await context.SchoolPermissionAsync(PermissionNames.StorageViewSchool, ct);
        if (!await repository.ActiveTeacherAsync(context.School, teacher, ct)) throw new KeyNotFoundException();
        await context.YearAsync(year, ct);
        await MatrixAsync(new EvidenceMatrixFilterDto { AcademicYearId = year, TeacherId = teacher }, ct);
        return await repository.CellAsync(context.School, teacher, task, year, ct);
    }
}
