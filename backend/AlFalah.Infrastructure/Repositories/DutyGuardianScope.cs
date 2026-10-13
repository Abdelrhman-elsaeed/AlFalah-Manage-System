using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.Duty;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

internal static class DutyGuardianScope
{
    public static async Task<DateOnly> CurrentDate(AlFalahDbContext db, int school, CancellationToken ct)
    {
        var zone = await db.Set<DutyPlan>().AsNoTracking().Where(x => x.SchoolId == school).OrderByDescending(x => x.PublishedAt).Select(x => x.TimeZoneId).FirstOrDefaultAsync(ct) ?? "Africa/Cairo";
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(zone)).DateTime);
    }
    public static IQueryable<Notification> Filter(AlFalahDbContext db, IQueryable<Notification> source, int school, string user, DateOnly date) =>
        source.Where(n => n.Type != "DismissalLateness" || db.StudentGuardians.Any(g => g.SchoolId == school && g.StudentId == n.StudentId
            && g.GuardianProfile.ApplicationUserId == user && g.GuardianProfile.IsActive && g.GuardianProfile.ApplicationUser.IsActive
            && g.ValidFrom <= date && (g.ValidTo == null || g.ValidTo >= date)));
}
