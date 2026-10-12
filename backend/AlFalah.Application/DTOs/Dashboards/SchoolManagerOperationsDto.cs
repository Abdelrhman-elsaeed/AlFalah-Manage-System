namespace AlFalah.Application.DTOs.Dashboards;

// Bounded, read-only overview contracts. No attendance notes, case contents,
// guardian contact information, or messaging contents cross this boundary.
public sealed record AttendanceCountsDto(int Total, int Present, int Absent, int Excused)
{
    public int Recorded => Present + Absent + Excused;
    public int Unrecorded => Math.Max(0, Total - Recorded);
    public decimal? AttendanceRate => Recorded == 0 ? null : Math.Round(100m * Present / Recorded, 1);
    public decimal? RecordingRate => Total == 0 ? null : Math.Round(100m * Recorded / Total, 1);
}
public sealed record ClassroomPulseDto(int ClassroomId, string Label, int Total, int Present, int Absent, int Excused);
public sealed record AttendanceTrendDto(DateOnly Date, int Present, int Recorded);
public sealed record StaffAbsenceDto(string Name, string Role, int Status);
public sealed record StaffPulseDto(AttendanceCountsDto Counts, IReadOnlyList<StaffAbsenceDto> Absences,
    IReadOnlyList<AttendanceTrendDto> Trend);
public sealed record StudentPulseDto(AttendanceCountsDto Counts, IReadOnlyList<ClassroomPulseDto> Classrooms,
    IReadOnlyList<AttendanceTrendDto> Trend);
public sealed record SummonAppointmentDto(int Id, string StudentName, DateTimeOffset ScheduledAt, int Priority);
public sealed record SummonPulseDto(int Pending, int Attended, int UnderObservation, int Improved,
    IReadOnlyList<SummonAppointmentDto> Appointments);
public sealed record SchoolActivityDto(string Category, string Title, DateTimeOffset At, string Route);
public sealed record RecentSchoolVisitDto(int Id, string Teacher, string Evaluator, string? Subject,
    DateTimeOffset Date, int Status, decimal? Score);
public sealed record SchoolDaySlotDto(string Label, string Kind, TimeOnly StartsAt, TimeOnly EndsAt);
public sealed record SchoolDayPulseDto(string Name, string TimeZone, IReadOnlyList<SchoolDaySlotDto> Slots);
public sealed record SchoolManagerOperationsDto(DateTimeOffset GeneratedAt, DateOnly Date, string TimeZone,
    StudentPulseDto? Students, StaffPulseDto Staff, SummonPulseDto? Summons,
    SchoolDayPulseDto? SchoolDay, IReadOnlyList<SchoolActivityDto> Activities,
    int? GatePasses, int? Conversations, IReadOnlyList<RecentSchoolVisitDto> RecentVisits);
