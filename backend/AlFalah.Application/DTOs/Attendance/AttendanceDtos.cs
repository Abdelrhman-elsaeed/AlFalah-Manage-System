using AlFalah.Domain.Enums;

namespace AlFalah.Application.DTOs.Attendance;

public record AttendanceSheetDto(DateOnly Date, IReadOnlyList<AttendanceSheetRowDto> Rows);

public record AttendanceSheetRowDto(
    string UserId,
    string FullName,
    string Role,
    AttendanceStatus? Status,
    string? Notes,
    DateTimeOffset? RecordedAt);

public record SaveAttendanceSheetRequestDto(
    DateOnly Date,
    IReadOnlyList<SaveAttendanceEntryDto> Entries,
    int? SchoolId = null);

public record SaveAttendanceEntryDto(string UserId, AttendanceStatus Status, string? Notes);

public record MyAttendanceItemDto(
    DateOnly Date,
    AttendanceStatus Status,
    string? Notes,
    DateTimeOffset RecordedAt);

public record AttendanceRecordItemDto(
    string UserId,
    string FullName,
    string Role,
    DateOnly Date,
    AttendanceStatus Status,
    string? Notes,
    DateTimeOffset RecordedAt);
