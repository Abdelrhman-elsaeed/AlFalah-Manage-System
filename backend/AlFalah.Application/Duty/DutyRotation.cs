using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlFalah.Domain.Entities.Duty;

namespace AlFalah.Application.Duty;

/// <summary>Deterministic equal cyclic rotation. Closed dates consume no turn.</summary>
public static class DutyRotation
{
    public static string Digest<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    public static bool Eligible(DutySettings s, string userId, DateOnly date) => s.TeacherIds.Contains(userId)
        && !(s.PermanentlyExcludedTeacherIds?.Contains(userId) ?? false)
        && !s.Exclusions.Any(x => x.UserId == userId && x.From <= date && x.To >= date);
    public static bool Operating(DutySettings s, DateOnly date) => date >= s.From && date <= s.To
        && s.Weekdays.Contains((int)date.DayOfWeek) && !s.Closures.Any(x => x.From <= date && x.To >= date);
    public static IReadOnlyList<(DateOnly Date, string? Teacher)> Generate(DutySettings s)
    {
        var result = new List<(DateOnly, string?)>();
        var cursor = 0;
        for (var date = s.From; date <= s.To; date = date.AddDays(1))
        {
            if (!Operating(s, date)) continue;
            string? teacher = null;
            for (var offset = 0; offset < s.TeacherIds.Count; offset++)
            {
                var index = (cursor + offset) % s.TeacherIds.Count;
                if (!Eligible(s, s.TeacherIds[index], date)) continue;
                teacher = s.TeacherIds[index]; cursor = (index + 1) % s.TeacherIds.Count; break;
            }
            result.Add((date, teacher));
        }
        return result;
    }
    public static DutySettings Settings(DutyPlan plan) => JsonSerializer.Deserialize<DutySettings>(plan.SettingsJson)!;
    public static bool Unresolved(DutyAssignmentChange c, IEnumerable<DutyAssignmentChange> changes) =>
        c.Kind == "UnresolvedCompensation" && !changes.Any(x => x.RelatedChangeId == c.Id && x.Kind is "ResolveCompensation" or "Undo");
}
