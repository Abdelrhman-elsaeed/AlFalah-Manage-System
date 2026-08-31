using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Biometrics;

public sealed record ZajelBiometricPunchRow(
    int RowNumber,
    string? NationalId,
    DateTimeOffset PunchAt,
    DateOnly SchoolLocalDate,
    TimeOnly SchoolLocalTime,
    string Status);

public sealed record BiometricEnrollmentSnapshot(
    int StudentId,
    string NationalId,
    int AcademicTermId,
    DateOnly StartsOn,
    DateOnly EndsOn);

public sealed record BiometricImportSettingsSnapshot(TimeOnly ArrivalCutoffLocalTime, int ArrivalGraceMinutes);

public sealed record BiometricImportIssueDto(int RowNumber, string Code, string Message);

public sealed record BiometricImportResultDto(
    int TotalRows,
    int ImportedDelays,
    int SkippedOnTimeRows,
    int DuplicateRows,
    int UnmatchedRows,
    IReadOnlyList<BiometricImportIssueDto> Issues);

public sealed record ImportZajelBiometricCommand(Stream Content, string FileName)
    : IRequest<ApiResponse<BiometricImportResultDto>>;

public interface IZajelBiometricWorkbookReader
{
    Task<IReadOnlyList<ZajelBiometricPunchRow>> ReadAsync(
        Stream content,
        CancellationToken cancellationToken);
}

public interface IBiometricImportRepository
{
    Task<BiometricImportSettingsSnapshot?> GetSettingsAsync(int schoolId, CancellationToken cancellationToken);

    Task<IReadOnlyList<BiometricEnrollmentSnapshot>> GetEnrollmentsAsync(
        int schoolId,
        IReadOnlyCollection<string> nationalIds,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken);

    Task<IReadOnlySet<(int StudentId, DateOnly Date)>> GetExistingDelayKeysAsync(
        int schoolId,
        IReadOnlyCollection<int> studentIds,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken);

    void AddRange(IEnumerable<MorningArrivalDelay> delays);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
