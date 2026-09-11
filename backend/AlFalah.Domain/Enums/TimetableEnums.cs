namespace AlFalah.Domain.Enums;

public enum TimetableSemester
{
    First = 1,
    Second = 2
}

public enum TimetableSetupStatus
{
    Draft = 1,
    ReadyForGeneration = 2,
    Generated = 3,
    Archived = 4
}

public enum TimetableDay
{
    Saturday = 1,
    Sunday = 2,
    Monday = 3,
    Tuesday = 4,
    Wednesday = 5,
    Thursday = 6,
    Friday = 7
}

public enum TimetableEntryType
{
    Lesson = 1,
    Standby = 2
}

public enum TimetableChangeKind
{
    Created = 1,
    Saved = 2,
    Published = 3,
    Imported = 4,
    Restored = 5,
    DirectSwap = 6,
    ThreeWaySwap = 7,
    Substitution = 8
}
