namespace AlFalah.Infrastructure.Data.Seeders;

internal sealed record TimetableSeedSubject(
    string Name,
    string Color,
    int IndividualPeriods,
    int PairedBlocks,
    string TimePreference = "None",
    int? EarliestPeriod = null,
    int? LatestPeriod = null,
    string? RoomKind = null,
    bool RoomIsRequired = false);

internal sealed record TimetableSeedOccurrence(string SubjectName, int Day, int Period, int? PairKey = null);

/// <summary>
/// Pure, deterministic curriculum used by the development seeder. Keeping the
/// weekly shape independent from EF makes its 35-period invariant cheap to test.
/// </summary>
internal static class IntermediateTimetableSeedPlan
{
    internal static readonly IReadOnlyList<TimetableSeedSubject> Subjects =
    [
        new("اللغة العربية", "#dc2626", 3, 1, "Early", 1, 3),
        new("الرياضيات", "#2563eb", 3, 1, "Early", 1, 3),
        new("اللغة الإنجليزية", "#16a34a", 2, 1),
        new("العلوم", "#0891b2", 2, 1, RoomKind: "science"),
        new("الدراسات الإسلامية", "#7c3aed", 3, 0, "Early", 1, 4),
        new("الدراسات الاجتماعية", "#b45309", 3, 0, "Late", 4, 7),
        new("المهارات الرقمية", "#4f46e5", 0, 1, RoomKind: "computer", RoomIsRequired: true),
        new("التربية البدنية", "#059669", 2, 0, RoomKind: "gym"),
        new("التربية الفنية", "#db2777", 0, 1, RoomKind: "art"),
        new("المهارات الحياتية", "#64748b", 1, 0),
        new("القرآن الكريم", "#65a30d", 2, 0, "Early", 1, 4),
        new("النشاط المدرسي", "#ea580c", 2, 0, "Late", 5, 7)
    ];

    private static readonly (string Subject, int Day, int Period, int? PairKey)[] BaseWeek =
    [
        ("اللغة العربية", 0, 1, 1), ("اللغة العربية", 0, 2, 1),
        ("الرياضيات", 0, 3, null),
        ("العلوم", 0, 4, 2), ("العلوم", 0, 5, 2),
        ("الدراسات الإسلامية", 0, 6, null), ("المهارات الحياتية", 0, 7, null),

        ("الرياضيات", 1, 1, 3), ("الرياضيات", 1, 2, 3),
        ("اللغة الإنجليزية", 1, 3, null),
        ("المهارات الرقمية", 1, 4, 4), ("المهارات الرقمية", 1, 5, 4),
        ("اللغة العربية", 1, 6, null), ("الدراسات الاجتماعية", 1, 7, null),

        ("اللغة الإنجليزية", 2, 1, 5), ("اللغة الإنجليزية", 2, 2, 5),
        ("اللغة العربية", 2, 3, null),
        ("التربية الفنية", 2, 4, 6), ("التربية الفنية", 2, 5, 6),
        ("القرآن الكريم", 2, 6, null), ("النشاط المدرسي", 2, 7, null),

        ("العلوم", 3, 1, null), ("الرياضيات", 3, 2, null),
        ("اللغة العربية", 3, 3, null), ("التربية البدنية", 3, 4, null),
        ("الدراسات الإسلامية", 3, 5, null), ("الدراسات الاجتماعية", 3, 6, null),
        ("اللغة الإنجليزية", 3, 7, null),

        ("العلوم", 4, 1, null), ("الرياضيات", 4, 2, null),
        ("القرآن الكريم", 4, 3, null), ("التربية البدنية", 4, 4, null),
        ("الدراسات الإسلامية", 4, 5, null), ("الدراسات الاجتماعية", 4, 6, null),
        ("النشاط المدرسي", 4, 7, null)
    ];

    /// <summary>
    /// Sunday is timetable day 2. Each classroom rotates whole days, preserving
    /// paired blocks while avoiding identical room demand across all classes.
    /// </summary>
    internal static IReadOnlyList<TimetableSeedOccurrence> BuildWeek(int classroomIndex)
    {
        var shift = Math.Abs(classroomIndex) % 5;
        return BaseWeek
            .Select(slot => new TimetableSeedOccurrence(
                slot.Subject,
                Day: 2 + (slot.Day + shift) % 5,
                slot.Period,
                slot.PairKey))
            .OrderBy(slot => slot.Day)
            .ThenBy(slot => slot.Period)
            .ToArray();
    }
}
