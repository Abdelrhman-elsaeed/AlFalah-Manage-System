using AlFalah.Shared.Models;
using MediatR;

namespace AlFalah.Application.IntelligentTimetable.DTOs;

public sealed record AvailabilityTeacherDto(int Id, string Name);
public sealed record AvailabilityCellDto(int Day, int BellPeriodId, int Sequence, TimeOnly StartLocalTime, TimeOnly EndLocalTime, bool IsAvailable);
public sealed record AvailabilitySlotRequest(int Day, int BellPeriodId, bool IsAvailable);
public sealed record TeacherAvailabilityDto(int InstructorProfileId, string TeacherName, int TimetableSetupProfileId,
    int Revision, int BellScheduleRevisionId, string ScheduleName, string ShortDisplayName, int MaximumWeeklyPeriods,
    bool IsVisiting, bool HideFromPrint, int AllocatedPeriods, int RemainingPeriods, bool RequiresScheduleReview,
    IReadOnlyList<AvailabilityCellDto> Slots, IReadOnlyList<AvailabilityCellDto> OrphanedSlots, IReadOnlyList<string> Violations);
public sealed record UpdateTeacherProfileRequest(int Revision, int BellScheduleRevisionId, string ShortDisplayName,
    int MaximumWeeklyPeriods, bool IsVisiting, bool HideFromPrint, bool ConfirmScheduleReview, IReadOnlyList<AvailabilitySlotRequest> Slots);
public sealed record GetAvailabilityTeachersQuery(int SetupId) : IRequest<ApiResponse<IReadOnlyList<AvailabilityTeacherDto>>>;
public sealed record GetTeacherAvailabilityQuery(int SetupId, int TeacherId) : IRequest<ApiResponse<TeacherAvailabilityDto>>;
// One atomic aggregate update includes both profile fields and the entire bulk-edited matrix.
public sealed record UpdateTeacherProfileCommand(int SetupId, int TeacherId, UpdateTeacherProfileRequest Request)
    : IRequest<ApiResponse<TeacherAvailabilityDto>>;
