using AlFalah.Application.StudentAffairs.Attendance;
using ClosedXML.Excel;

namespace AlFalah.Infrastructure.Integrations.Noor;

public sealed class NoorWorkbookWriter : INoorWorkbookWriter
{
    public byte[] Write(IReadOnlyList<NoorWorkbookRow> rows)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Absence Corrections");
        var headers = new[] { "Student Name", "National ID", "Date", "Excuse Status" };
        for (var column = 1; column <= headers.Length; column++)
            worksheet.Cell(1, column).Value = headers[column - 1];

        for (var index = 0; index < rows.Count; index++)
        {
            var excelRow = index + 2;
            var row = rows[index];
            worksheet.Cell(excelRow, 1).Value = row.StudentName;
            worksheet.Cell(excelRow, 2).Value = row.NationalId;
            worksheet.Cell(excelRow, 2).Style.NumberFormat.Format = "@";
            worksheet.Cell(excelRow, 3).Value = row.Date.ToDateTime(TimeOnly.MinValue);
            worksheet.Cell(excelRow, 3).Style.DateFormat.Format = "yyyy-MM-dd";
            worksheet.Cell(excelRow, 4).Value = row.ExcuseStatus;
        }

        var headerRange = worksheet.Range(1, 1, 1, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
        worksheet.SheetView.FreezeRows(1);
        worksheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
