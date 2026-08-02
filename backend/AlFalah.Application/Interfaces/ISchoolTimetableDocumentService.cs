using AlFalah.Application.DTOs.Timetables;

namespace AlFalah.Application.Interfaces;

public interface ISchoolTimetableDocumentService
{
    TimetableFileDto BuildPdf(
        SchoolTimetableDto timetable,
        TimetableCatalogDto catalog,
        TimetablePdfColorMode colorMode);
    TimetableFileDto BuildImportTemplate(SchoolTimetableDto timetable, TimetableCatalogDto catalog);
    TimetableImportRows ParseImport(Stream stream, TimetableCatalogDto catalog);
}

public sealed record TimetableImportedRow(
    int InstructorProfileId,
    IReadOnlyList<SaveTimetableEntryRequest> Entries);

public sealed record TimetableImportRows(
    IReadOnlyList<TimetableImportedRow> Rows,
    IReadOnlyList<string> Warnings);
