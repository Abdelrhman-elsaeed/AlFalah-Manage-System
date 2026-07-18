using AlFalah.Application.DTOs.Attendance;

namespace AlFalah.Application.Interfaces;

public interface IAttendanceService
{
    Task<AttendanceSheetDto> GetSheetAsync(DateOnly date, int? requestedSchoolId, CancellationToken cancellationToken = default);
    Task<AttendanceSheetDto> SaveSheetAsync(SaveAttendanceSheetRequestDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AttendanceRecordItemDto>> GetRecordsAsync(
        DateOnly? fromDate,
        DateOnly? toDate,
        string? name,
        int? requestedSchoolId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MyAttendanceItemDto>> GetMyAttendanceAsync(
        DateOnly? fromDate,
        DateOnly? toDate,
        int? requestedSchoolId,
        CancellationToken cancellationToken = default);
}
