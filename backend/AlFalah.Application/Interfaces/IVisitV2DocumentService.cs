using AlFalah.Application.DTOs.Visits;

namespace AlFalah.Application.Interfaces;

public interface IVisitV2DocumentService
{
    VisitV2CsvExportDto BuildCsv(IReadOnlyList<VisitV2CsvRow> rows);
    Task<VisitV2PdfExportDto> BuildPdfAsync(
        VisitV2DetailDto visit,
        VisitV2PdfAssetSources assets,
        CancellationToken cancellationToken = default);
}

public sealed record VisitV2PdfAssetSources(
    string HeaderText,
    string FooterText,
    string PrimaryColor,
    string? LogoSource,
    string? InstructorSignatureSource,
    string? EvaluatorSignatureSource,
    string? ManagerSignatureSource,
    bool ShowEvaluatorSignature,
    bool ShowManagerSignature);

public sealed record VisitV2CsvRow(
    int VisitId,
    DateTimeOffset VisitDate,
    int ClassroomPeriod,
    string InstructorName,
    string EmployeeNumber,
    string Subject,
    string GradeClass,
    string LessonTitle,
    string VisitType,
    int PresentCount,
    int AbsentCount,
    string EvaluatorName,
    string EvaluatorRole,
    int TotalScore,
    string PerformanceLevelAr,
    IReadOnlyDictionary<string, int> DomainPercentages,
    string Strengths,
    string ImprovementAreas);
