using System.Globalization;
using AlFalah.Application.StudentAffairs.Biometrics;
using ClosedXML.Excel;
using Microsoft.Extensions.Configuration;

namespace AlFalah.Infrastructure.Integrations.Biometrics;

public sealed class ZajelBiometricWorkbookReader : IZajelBiometricWorkbookReader
{
    private const string NationalIdHeader = "رقم الهوية";
    private const string PunchDateTimeHeader = "تاريخ ووقت الحضور";
    private const string StatusHeader = "حالة الحضور";
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
        var headerRow = worksheet.RowsUsed().FirstOrDefault(row =>
            row.CellsUsed().Any(cell => string.Equals(cell.GetString().Trim(), NationalIdHeader, StringComparison.Ordinal)));
        if (headerRow is null)
            throw new InvalidDataException("The workbook is missing the required Zajel header row");

        var columns = headerRow.CellsUsed().ToDictionary(
            cell => cell.GetString().Trim(),
            cell => cell.Address.ColumnNumber,
            StringComparer.Ordinal);
        EnsureRequiredColumn(columns, NationalIdHeader);
        EnsureRequiredColumn(columns, PunchDateTimeHeader);
        EnsureRequiredColumn(columns, StatusHeader);

        var rows = new List<ZajelBiometricPunchRow>();
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();
        for (var rowNumber = headerRow.RowNumber() + 1; rowNumber <= lastRow; rowNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = worksheet.Row(rowNumber);
            var nationalId = row.Cell(columns[NationalIdHeader]).GetFormattedString().Trim();
            var punchText = row.Cell(columns[PunchDateTimeHeader]).GetFormattedString().Trim();
            var status = row.Cell(columns[StatusHeader]).GetString().Trim();
            if (nationalId.Length == 0 && punchText.Length == 0 && status.Length == 0) continue;
            if (!TryReadLocalDateTime(row.Cell(columns[PunchDateTimeHeader]), out var localDateTime))
                throw new InvalidDataException($"Invalid تاريخ ووقت الحضور value at Excel row {rowNumber}");

            var unspecified = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
            var punchAt = new DateTimeOffset(unspecified, _schoolTimeZone.GetUtcOffset(unspecified));
            rows.Add(new ZajelBiometricPunchRow(
                rowNumber,
                nationalId,
                punchAt,
                DateOnly.FromDateTime(unspecified),
                TimeOnly.FromDateTime(unspecified),
                status));
        }

        return Task.FromResult<IReadOnlyList<ZajelBiometricPunchRow>>(rows);
    }

    private static bool TryReadLocalDateTime(IXLCell cell, out DateTime value)
    {
        if (cell.TryGetValue<DateTime>(out value)) return true;
        var text = cell.GetFormattedString().Trim();
        var formats = new[] { "yyyy-MM-dd HH:mm:ss", "yyyy-M-d H:mm:ss", "dd/MM/yyyy HH:mm:ss", "d/M/yyyy H:mm:ss" };
        return DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture,
                   DateTimeStyles.AllowWhiteSpaces, out value)
               || DateTime.TryParse(text, CultureInfo.GetCultureInfo("ar-SA"),
                   DateTimeStyles.AllowWhiteSpaces, out value);
    }

    private static void EnsureRequiredColumn(IReadOnlyDictionary<string, int> columns, string name)
    {
        if (!columns.ContainsKey(name))
            throw new InvalidDataException($"The workbook is missing the required column: {name}");
    }

    private static TimeZoneInfo ResolveTimeZone(string? configuredId)
    {
        var candidates = new[] { configuredId, "Egypt Standard Time", "Africa/Cairo" }
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate));
        foreach (var candidate in candidates)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(candidate!); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        throw new InvalidOperationException("No valid school time zone is configured for biometric imports");
    }
}
