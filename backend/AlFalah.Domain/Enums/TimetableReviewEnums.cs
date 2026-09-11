namespace AlFalah.Domain.Enums;

public enum ViolationSeverity { Error = 1, Warning = 2 }
public enum ViolationRuleCode
{
    TeacherDoubleBooking = 1, ClassroomDoubleBooking, RoomDoubleBooking, UnavailableSlot,
    NonStudyDayPlacement, MissingAssignment, ScheduleCountMismatch, BrokenPairedBlock,
    DisallowedDay, ViolatedFixedSlot, ExcessiveConsecutive, UnfairLastPeriods,
    ExcessiveGaps, SubjectConcentration, EarlyPreferenceMissed, SubjectClusterImbalance
}
public enum RepairProposalKind { SwapEntries = 1, MoveEntry = 2, ReassignTeacher = 3 }
