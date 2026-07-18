using AlFalah.Application.DTOs.Attendance;

namespace AlFalah.Application.Interfaces;

public interface IAttendancePdfService
{
    Task<byte[]> RenderAsync(IReadOnlyList<AttendanceRecordItemDto> records, CancellationToken cancellationToken = default);
}
