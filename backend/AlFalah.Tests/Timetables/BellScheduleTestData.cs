using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;

namespace AlFalah.Tests.Timetables;

internal static class BellScheduleTestData
{
    public static async Task<BellScheduleRevision> SeedAsync(AlFalahDbContext db, int schoolId = 1, string actor = "manager", bool published = false)
    {
        var template = new BellScheduleTemplate { SchoolId = schoolId, AcademicYearId = 1, Semester = TimetableSemester.First,
            Name = "التوقيت المعتاد", CreatedByUserId = actor, UpdatedByUserId = actor };
        var revision = new BellScheduleRevision { SchoolId = schoolId, Template = template, Revision = 1,
            Name = template.Name, SchoolTimeZoneId = "Africa/Cairo", CreatedByUserId = actor };
        revision.Days.Add(new BellScheduleDay { Day = 0, IsStudyDay = true, Periods = new List<BellPeriod> {
            new() { Sequence = 1, StartLocalTime = new(7, 0), EndLocalTime = new(7, 45) },
            new() { Sequence = 2, StartLocalTime = new(7, 50), EndLocalTime = new(8, 35) } } });
        foreach (var day in Enumerable.Range(1, 7)) revision.Days.Add(new() { Day = day, IsStudyDay = day != 7, UsesDefaultSchedule = true });
        db.Add(revision);
        var profile = new TimetableSetupProfile { SchoolId = schoolId, AcademicYearId = 1, Semester = TimetableSemester.First,
            Name = "الإعداد", CreatedByUserId = actor, UpdatedByUserId = actor, BellScheduleTemplate = template };
        db.Add(profile);
        db.AcademicTerms.Add(new AcademicTerm { SchoolId = schoolId, AcademicYearId = 1, Semester = TimetableSemester.First,
            StartsOn = new(2026, 8, 1), EndsOn = new(2027, 1, 31), IsActive = true });
        if (published) db.SchoolTimetables.Add(new() { SchoolId = schoolId, AcademicYearId = 1, Semester = TimetableSemester.First,
            Title = "الجدول", IsPublished = true, BellScheduleRevision = revision, TimetableSetupProfile = profile,
            CreatedByUserId = actor, UpdatedByUserId = actor });
        await db.SaveChangesAsync();
        return revision;
    }
}

