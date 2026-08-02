using AlFalah.Application.DTOs.Timetables;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.Interfaces;

public interface ISchoolTimetableService
{
    Task<TimetableCatalogDto> GetCatalogAsync(int? schoolId, CancellationToken cancellationToken = default);
    Task<SchoolTimetableDto?> GetCurrentAsync(int academicYearId, TimetableSemester semester, int? schoolId, CancellationToken cancellationToken = default);
    Task<SchoolTimetableDto> GetByIdAsync(int timetableId, CancellationToken cancellationToken = default);
    Task<SchoolTimetableDto> CreateAsync(CreateSchoolTimetableRequest request, int? schoolId, CancellationToken cancellationToken = default);
    Task<SchoolTimetableDto> SaveAsync(int timetableId, SaveSchoolTimetableRequest request, CancellationToken cancellationToken = default);
    Task<SchoolTimetableDto> PublishAsync(int timetableId, TimetableRevisionRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimetableVersionDto>> GetVersionsAsync(int timetableId, CancellationToken cancellationToken = default);
    Task<SchoolTimetableDto> RestoreAsync(int timetableId, int versionNumber, TimetableRevisionRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimetableModeratorDto>> UpdateGrantsAsync(UpdateTimetableGrantsRequest request, int? schoolId, CancellationToken cancellationToken = default);
    Task<TimetableImportResultDto> ImportAsync(int timetableId, Stream stream, int revision, CancellationToken cancellationToken = default);
    Task<TimetableFileDto> BuildImportTemplateAsync(int timetableId, CancellationToken cancellationToken = default);
    Task<TimetableFileDto> BuildPdfAsync(
        int timetableId,
        TimetablePdfColorMode colorMode,
        CancellationToken cancellationToken = default);
}
