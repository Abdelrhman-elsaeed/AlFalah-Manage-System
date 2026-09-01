using System.Globalization;
using AlFalah.Application.StudentAffairs.Biometrics;
using ClosedXML.Excel;
using Microsoft.Extensions.Configuration;

namespace AlFalah.Infrastructure.Integrations.Biometrics;

public sealed class ZajelBiometricWorkbookReader : IZajelBiometricWorkbookReader
{
    private static readonly string[] IdentityHeaders = { "رقم الهوية", "الهوية", "رقم السجل المدني", "السجل المدني", "Identity", "NationalId" };
    private static readonly string[] PunchDateTimeHeaders = { "تاريخ ووقت الحضور", "وقت الحضور", "تاريخ الحضور", "الوقت", "التاريخ والوقت", "PunchDateTime", "DateTime", "Time" };
    private static readonly string[] StatusHeaders = { "حالة الحضور", "الحالة", "Status" };

    private readonly TimeZoneInfo _schoolTimeZone;

    public ZajelBiometricWorkbookReader(IConfiguration configuration)
    {
        var configuredId = configuration["StudentAffairsIntegrations:SchoolTimeZoneId"];
        _schoolTimeZone = ResolveTimeZone(configuredId);
    }

    public Task<IReadOnlyList<ZajelBiometricPunchRow>> ReadAsync(
        Stream content,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var workbook = new XLWorkbook(content);
        var worksheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new InvalidDataException("The Zajel workbook has no worksheets");

        var firstUsedRow = worksheet.FirstRowUsed();
        if (firstUsedRow is null)
            throw new InvalidDataException("The workbook contains no biometric data");

        // Find header row containing an identity column, or default to the first used row
        var headerRow = worksheet.RowsUsed().FirstOrDefault(row =>
            row.CellsUsed().Any(cell => IdentityHeaders.Any(h => string.Equals(cell.GetString().Trim(), h, StringComparison.OrdinalIgnoreCase))))
            ?? firstUsedRow;

        var headerCells = headerRow.CellsUsed().ToList();
        int? identityColNum = null;
        int? punchColNum = null;
        int? statusColNum = null;

        foreach (var cell in headerCells)
        {
            var text = cell.GetString().Trim();
            if (identityColNum is null && IdentityHeaders.Any(h => string.Equals(text, h, StringComparison.OrdinalIgnoreCase)))
                identityColNum = cell.Address.ColumnNumber;
            else if (punchColNum is null && PunchDateTimeHeaders.Any(h => string.Equals(text, h, StringComparison.OrdinalIgnoreCase)))
                punchColNum = cell.Address.ColumnNumber;
            else if (statusColNum is null && StatusHeaders.Any(h => string.Equals(text, h, StringComparison.OrdinalIgnoreCase)))
                statusColNum = cell.Address.ColumnNumber;
        }

        // Column 1 is explicitly the Identity Number column if no named header matched
        identityColNum ??= 1;

        var rows = new List<ZajelBiometricPunchRow>();
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();
        var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _schoolTimeZone);
        var defaultDate = DateOnly.FromDateTime(nowLocal.DateTime);
        var defaultTime = TimeOnly.FromDateTime(nowLocal.DateTime);

        for (var rowNumber = headerRow.RowNumber() + 1; rowNumber <= lastRow; rowNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = worksheet.Row(rowNumber);
            var identityCell = row.Cell(identityColNum.Value);
            var identityNumber = identityCell.GetFormattedString().Trim();

            if (string.IsNullOrEmpty(identityNumber))
            {
                // If the entire row is empty, skip quietly
                if (row.CellsUsed().All(c => string.IsNullOrWhiteSpace(c.GetFormattedString())))
                    continue;
            }

            DateTimeOffset punchAt;
            DateOnly schoolLocalDate;
            TimeOnly schoolLocalTime;

            if (punchColNum.HasValue && TryReadLocalDateTime(row.Cell(punchColNum.Value), defaultDate, out var localDateTime))
            {
                var unspecified = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
                punchAt = new DateTimeOffset(unspecified, _schoolTimeZone.GetUtcOffset(unspecified));
                schoolLocalDate = DateOnly.FromDateTime(unspecified);
                schoolLocalTime = TimeOnly.FromDateTime(unspecified);
            }
            else
            {
                punchAt = nowLocal;
                schoolLocalDate = defaultDate;
                schoolLocalTime = defaultTime;
            }

            var status = statusColNum.HasValue ? row.Cell(statusColNum.Value).GetString().Trim() : "حاضر";
            if (string.IsNullOrWhiteSpace(status)) status = "حاضر";

            rows.Add(new ZajelBiometricPunchRow(
                rowNumber,
                identityNumber,
                punchAt,
                schoolLocalDate,
                schoolLocalTime,
                status));
        }

        return Task.FromResult<IReadOnlyList<ZajelBiometricPunchRow>>(rows);
    }

    private static bool TryReadLocalDateTime(IXLCell cell, DateOnly fallbackDate, out DateTime value)
    {
        if (cell.TryGetValue<DateTime>(out value)) return true;
        var text = cell.GetFormattedString().Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            value = default;
            return false;
        }

        var dateFormats = new[]
        {
            "yyyy-MM-dd HH:mm:ss", "yyyy-M-d H:mm:ss",
            "yyyy-MM-dd HH:mm", "yyyy-M-d H:mm",
            "dd/MM/yyyy HH:mm:ss", "d/M/yyyy H:mm:ss",
            "dd/MM/yyyy HH:mm", "d/M/yyyy H:mm",
            "yyyy/MM/dd HH:mm:ss", "yyyy/MM/dd HH:mm",
            "yyyy-MM-dd", "dd/MM/yyyy", "yyyy/MM/dd"
        };

        if (DateTime.TryParseExact(text, dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out value)
            || DateTime.TryParse(text, CultureInfo.GetCultureInfo("ar-SA"), DateTimeStyles.AllowWhiteSpaces, out value)
            || DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out value))
        {
            return true;
        }

        var timeFormats = new[] { "HH:mm:ss", "H:mm:ss", "HH:mm", "H:mm", "hh:mm:ss tt", "hh:mm tt" };
        if (TimeOnly.TryParseExact(text, timeFormats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsedTime)
            || TimeOnly.TryParse(text, CultureInfo.InvariantCulture, out parsedTime))
        {
            value = fallbackDate.ToDateTime(parsedTime);
            return true;
        }

        value = default;
        return false;
    }

    private static TimeZoneInfo ResolveTimeZone(string? configuredId)
    {
        var candidates = new[] { configuredId, "Arab Standard Time", "Arabic Standard Time", "Egypt Standard Time", "Asia/Riyadh", "Africa/Cairo" }
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate));
        foreach (var candidate in candidates)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(candidate!); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.Utc;
    }
}
