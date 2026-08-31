using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Events;
using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.StudentAffairs.Biometrics.Handlers;

public sealed class ImportZajelBiometricCommandHandler
    : IRequestHandler<ImportZajelBiometricCommand, ApiResponse<BiometricImportResultDto>>
{
    private const string LateStatus = "متأخر";
    private const string NotificationPolicy = "ImmediateGuardian";
    private readonly IZajelBiometricWorkbookReader _reader;
    private readonly IBiometricImportRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public ImportZajelBiometricCommandHandler(
        IZajelBiometricWorkbookReader reader,
        IBiometricImportRepository repository,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _reader = reader;
        _repository = repository;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<BiometricImportResultDto>> Handle(
        ImportZajelBiometricCommand command,
        CancellationToken cancellationToken)
    {
        var schoolId = _currentUser.ActiveSchoolId;
        var userId = _currentUser.UserId;
        if (schoolId is null || string.IsNullOrWhiteSpace(userId))
            return ApiResponse<BiometricImportResultDto>.Fail("An authenticated actor and active school are required");
        if (!command.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return ApiResponse<BiometricImportResultDto>.Fail("The Zajel import must be an .xlsx workbook");

        IReadOnlyList<ZajelBiometricPunchRow> rows;
        try
        {
            rows = await _reader.ReadAsync(command.Content, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidDataException exception)
        {
            return ApiResponse<BiometricImportResultDto>.Fail(exception.Message);
        }

        if (rows.Count == 0)
            return ApiResponse<BiometricImportResultDto>.Fail("The workbook contains no biometric rows");

        var settings = await _repository.GetSettingsAsync(schoolId.Value, cancellationToken).ConfigureAwait(false);
        if (settings is null)
            return ApiResponse<BiometricImportResultDto>.Fail("Student Affairs arrival cutoff settings are not configured for this school");

        var normalizedRows = rows.Select(row => (Row: row, NationalId: NormalizeNationalId(row.NationalId))).ToArray();
        var ids = normalizedRows.Where(row => row.NationalId.Length > 0)
            .Select(row => row.NationalId).Distinct(StringComparer.Ordinal).ToArray();
        var fromDate = rows.Min(row => row.SchoolLocalDate);
        var toDate = rows.Max(row => row.SchoolLocalDate);
        var enrollments = await _repository.GetEnrollmentsAsync(
            schoolId.Value, ids, fromDate, toDate, cancellationToken).ConfigureAwait(false);
        var enrollmentsByNationalId = enrollments
            .GroupBy(item => NormalizeNationalId(item.NationalId), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var studentIds = enrollments.Select(item => item.StudentId).Distinct().ToArray();
        var existing = await _repository.GetExistingDelayKeysAsync(
            schoolId.Value, studentIds, fromDate, toDate, cancellationToken).ConfigureAwait(false);
        var acceptedKeys = new HashSet<(int StudentId, DateOnly Date)>(existing);

        var effectiveCutoff = settings.ArrivalCutoffLocalTime.AddMinutes(settings.ArrivalGraceMinutes);
        var now = _timeProvider.GetUtcNow();
        var delays = new List<MorningArrivalDelay>();
        var issues = new List<BiometricImportIssueDto>();
        var skippedOnTime = 0;
        var duplicateRows = 0;
        var unmatchedRows = 0;

        foreach (var item in normalizedRows)
        {
            if (item.NationalId.Length == 0)
            {
                unmatchedRows++;
                issues.Add(new(item.Row.RowNumber, "MissingNationalId", "رقم الهوية is empty"));
                continue;
            }

            if (!enrollmentsByNationalId.TryGetValue(item.NationalId, out var candidates))
            {
                unmatchedRows++;
                issues.Add(new(item.Row.RowNumber, "StudentNotFound", $"No active student enrollment matches national ID {item.NationalId}"));
                continue;
            }

            var enrollment = candidates.SingleOrDefault(candidate =>
                candidate.StartsOn <= item.Row.SchoolLocalDate && candidate.EndsOn >= item.Row.SchoolLocalDate);
            if (enrollment is null)
            {
                unmatchedRows++;
                issues.Add(new(item.Row.RowNumber, "EnrollmentNotFound", "The student has no active enrollment on the punch date"));
                continue;
            }

            var isLate = string.Equals(item.Row.Status.Trim(), LateStatus, StringComparison.Ordinal)
                || item.Row.SchoolLocalTime > effectiveCutoff;
            if (!isLate)
            {
                skippedOnTime++;
                continue;
            }

            var key = (enrollment.StudentId, item.Row.SchoolLocalDate);
            if (!acceptedKeys.Add(key))
            {
                duplicateRows++;
                continue;
            }

            var delayMinutes = Math.Max(0, (int)Math.Ceiling(
                (item.Row.SchoolLocalTime.ToTimeSpan() - effectiveCutoff.ToTimeSpan()).TotalMinutes));
            var delay = new MorningArrivalDelay
            {
                SchoolId = schoolId.Value,
                StudentId = enrollment.StudentId,
                AcademicTermId = enrollment.AcademicTermId,
                ArrivalAt = item.Row.PunchAt,
                SchoolLocalDate = item.Row.SchoolLocalDate,
                CutoffTimeSnapshot = effectiveCutoff,
                DelayMinutes = delayMinutes,
                Reason = $"Zajel biometric import; status={item.Row.Status.Trim()}; row={item.Row.RowNumber}",
                NotificationPolicySnapshot = NotificationPolicy,
                CreatedAt = now,
                CreatedByUserId = userId,
                UpdatedAt = now,
                UpdatedByUserId = userId
            };
            delay.AppendDomainEvent(new MUaCqczw28YRmuXBYNYtWgMhWwXe7qmYC3(
                Guid.NewGuid(), 0, delay.StudentId, delay.SchoolId, delay.AcademicTermId,
                delay.ArrivalAt, delay.SchoolLocalDate, delay.CutoffTimeSnapshot,
                delay.DelayMinutes, delay.NotificationPolicySnapshot, now));
            delays.Add(delay);
        }

        if (delays.Count > 0)
        {
            _repository.AddRange(delays);
            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var result = new BiometricImportResultDto(
            rows.Count, delays.Count, skippedOnTime, duplicateRows, unmatchedRows, issues);
        return ApiResponse<BiometricImportResultDto>.Success(result, "Zajel biometric workbook processed successfully");
    }

    internal static string NormalizeNationalId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var result = new char[value.Length];
        var length = 0;
        foreach (var character in value.Trim())
        {
            if (character is >= '0' and <= '9') result[length++] = character;
            else if (character is >= '٠' and <= '٩') result[length++] = (char)('0' + character - '٠');
            else if (character is >= '۰' and <= '۹') result[length++] = (char)('0' + character - '۰');
        }
        return new string(result, 0, length);
    }
}
