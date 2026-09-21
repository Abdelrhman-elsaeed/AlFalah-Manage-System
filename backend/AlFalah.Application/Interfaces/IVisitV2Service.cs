using AlFalah.Application.DTOs.Visits;

namespace AlFalah.Application.Interfaces;

public interface IVisitV2Service
{
    VisitV2AvailabilityDto GetAvailability();
    Task<VisitV2ObservationCardDto> GetObservationCardAsync(CancellationToken cancellationToken = default);
    Task<VisitV2DetailDto> CreateAsync(CreateVisitV2RequestDto request, CancellationToken cancellationToken = default);
    Task<VisitV2DetailDto> UpdateAsync(int id, UpdateVisitV2RequestDto request, CancellationToken cancellationToken = default);
    Task<VisitV2DetailDto> FinalizeAsync(int id, CancellationToken cancellationToken = default);
    Task<VisitV2DetailDto> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<VisitV2ArchiveResultDto> ListAsync(VisitV2ArchiveQuery query, CancellationToken cancellationToken = default);
    Task SoftDeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VisitV2TreatmentDto>> UpdateTreatmentsAsync(
        int id,
        UpdateVisitV2TreatmentsDto request,
        CancellationToken cancellationToken = default);
    Task<VisitV2DashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<VisitV2CsvExportDto> ExportCsvAsync(VisitV2ArchiveQuery query, CancellationToken cancellationToken = default);
    Task<VisitV2PdfExportDto> ExportPdfAsync(int id, CancellationToken cancellationToken = default);
}
