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

        var normalizedRows = rows.Select(row => (Row: row, IdentityNumber: NormalizeIdentityNumber(row.IdentityNumber))).ToArray();
        var ids = normalizedRows.Where(row => row.IdentityNumber.Length > 0)
            .Select(row => row.IdentityNumber).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var fromDate = rows.Min(row => row.SchoolLocalDate);
        var toDate = rows.Max(row => row.SchoolLocalDate);
        var enrollments = await _repository.GetEnrollmentsAsync(
            schoolId.Value, ids, fromDate, toDate, cancellationToken).ConfigureAwait(false);
        var enrollmentsByIdentityNumber = enrollments
            .GroupBy(item => NormalizeIdentityNumber(item.IdentityNumber), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var studentIds = enrollments.Select(item => item.StudentId).Distinct().ToArray();
        var existingDelays = await _repository.GetExistingDelaysForUpdateAsync(
            schoolId.Value, studentIds, fromDate, toDate, cancellationToken).ConfigureAwait(false);

        var effectiveCutoff = settings.ArrivalCutoffLocalTime.AddMinutes(settings.ArrivalGraceMinutes);
        var now = _timeProvider.GetUtcNow();
        var newDelays = new List<MorningArrivalDelay>();
        var issues = new List<BiometricImportIssueDto>();
        var skippedOnTime = 0;
        var duplicateRows = 0;
        var unmatchedRows = 0;
        var updatedDelays = 0;

        foreach (var item in normalizedRows)
        {
            if (item.IdentityNumber.Length == 0)
            {
                unmatchedRows++;
                issues.Add(new(item.Row.RowNumber, "MissingIdentityNumber", "رقم الهوية is empty"));
                continue;
            }

            if (!enrollmentsByIdentityNumber.TryGetValue(item.IdentityNumber, out var candidates))
            {
                unmatchedRows++;
                issues.Add(new(item.Row.RowNumber, "StudentNotFound", $"No active student enrollment matches identity number {item.IdentityNumber}"));
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
            var delayMinutes = Math.Max(0, (int)Math.Ceiling(
                (item.Row.SchoolLocalTime.ToTimeSpan() - effectiveCutoff.ToTimeSpan()).TotalMinutes));

            if (existingDelays.TryGetValue(key, out var existingDelay))
            {
                // Idempotent Upsert: Update existing delay record
                existingDelay.ArrivalAt = item.Row.PunchAt;
                existingDelay.CutoffTimeSnapshot = effectiveCutoff;
                existingDelay.DelayMinutes = delayMinutes;
                existingDelay.Reason = $"Zajel biometric import; status={item.Row.Status.Trim()}; row={item.Row.RowNumber}";
                existingDelay.UpdatedAt = now;
                existingDelay.UpdatedByUserId = userId;
                duplicateRows++;
                updatedDelays++;
                continue;
            }

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

            newDelays.Add(delay);
            existingDelays[key] = delay;
        }

        if (newDelays.Count > 0)
        {
            _repository.AddRange(newDelays);
        }

        if (newDelays.Count > 0 || updatedDelays > 0)
        {
            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var totalRecordedDelays = newDelays.Count + updatedDelays;
        var result = new BiometricImportResultDto(
            rows.Count, totalRecordedDelays, skippedOnTime, duplicateRows, unmatchedRows, issues);
        return ApiResponse<BiometricImportResultDto>.Success(result, "Zajel biometric workbook processed successfully");
    }

    public static string NormalizeIdentityNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var trimmed = value.Trim();
        var chars = new char[trimmed.Length];
        for (int i = 0; i < trimmed.Length; i++)
        {
            var c = trimmed[i];
            if (c is >= '٠' and <= '٩') chars[i] = (char)('0' + c - '٠');
            else if (c is >= '۰' and <= '۹') chars[i] = (char)('0' + c - '۰');
            else chars[i] = c;
        }
        return new string(chars).Trim();
    }

    public static string NormalizeNationalId(string? value) => NormalizeIdentityNumber(value);
}
