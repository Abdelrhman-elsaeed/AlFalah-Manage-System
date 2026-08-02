using AlFalah.Domain.Enums;

namespace AlFalah.Application.DTOs.Timetables;

public enum TimetablePdfColorMode
{
    Color = 1,
    Monochrome = 2
}

public sealed record TimetableAcademicYearDto(int Id, string Code, string NameAr, bool IsActive);
public sealed record TimetableOptionDto(int Value, string LabelAr);
public sealed record TimetableTeacherDto(
    int InstructorProfileId,
    string UserId,
    string FullName,
    string? EmployeeNumber,
    string? Subject,
    IReadOnlyList<string> Classes,
    bool IsCurrentUser);
public sealed record TimetableModeratorDto(string UserId, string FullName, bool IsGranted);
public sealed record TimetableCapabilitiesDto(bool CanManage, bool CanDelegate, bool CanViewVersions);

public sealed record TimetableCatalogDto(
    int SchoolId,
    string SchoolName,
    IReadOnlyList<TimetableAcademicYearDto> AcademicYears,
    IReadOnlyList<TimetableOptionDto> Semesters,
    IReadOnlyList<TimetableOptionDto> Days,
    int PeriodCount,
    IReadOnlyList<TimetableTeacherDto> Teachers,
    IReadOnlyList<TimetableModeratorDto> Moderators,
    TimetableCapabilitiesDto Capabilities);

public sealed record TimetableEntryDto(
    int InstructorProfileId,
    TimetableDay Day,
    byte Period,
    TimetableEntryType EntryType,
    string? ClassLabel,
    string? Subject);

public sealed record TimetableTeacherSummaryDto(
    int InstructorProfileId,
    int LessonCount,
    int StandbyCount);

public sealed record SchoolTimetableDto(
    int Id,
    int SchoolId,
    int AcademicYearId,
    string AcademicYearName,
    TimetableSemester Semester,
    string SemesterLabelAr,
    string Title,
    bool IsPublished,
    DateTimeOffset? PublishedAt,
    int Revision,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<TimetableEntryDto> Entries,
    IReadOnlyList<TimetableTeacherSummaryDto> TeacherSummaries,
    TimetableCapabilitiesDto Capabilities);

public sealed record CreateSchoolTimetableRequest(
    int AcademicYearId,
    TimetableSemester Semester,
    string Title);

public sealed record SaveTimetableEntryRequest(
    int InstructorProfileId,
    TimetableDay Day,
    byte Period,
    TimetableEntryType EntryType,
    string? ClassLabel,
    string? Subject);

public sealed record SaveSchoolTimetableRequest(
    string Title,
    int Revision,
    IReadOnlyList<SaveTimetableEntryRequest> Entries);

public sealed record TimetableRevisionRequest(int Revision);
public sealed record UpdateTimetableGrantsRequest(IReadOnlyList<string> ModeratorUserIds);

public sealed record TimetableVersionDto(
    int Id,
    int VersionNumber,
    TimetableChangeKind ChangeKind,
    string ChangeKindLabelAr,
    string Title,
    DateTimeOffset CreatedAt,
    string CreatedByFullName,
    int? RestoredFromVersionNumber);

public sealed record TimetableImportResultDto(
    SchoolTimetableDto Timetable,
    int ImportedEntryCount,
    IReadOnlyList<string> Warnings);

public sealed record TimetableFileDto(byte[] Bytes, string ContentType, string FileName);

public sealed record TimetableSnapshotDto(string Title, IReadOnlyList<TimetableEntryDto> Entries);
