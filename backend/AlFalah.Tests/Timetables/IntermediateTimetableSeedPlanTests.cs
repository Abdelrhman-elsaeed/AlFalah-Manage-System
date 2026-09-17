using AlFalah.Infrastructure.Data.Seeders;
using FluentAssertions;
using Xunit;

namespace AlFalah.Tests.Timetables;

public sealed class IntermediateTimetableSeedPlanTests
{
    [Fact]
    public void Every_class_week_fills_all_study_slots_with_the_expected_curriculum()
    {
        for (var classroom = 0; classroom < 8; classroom++)
        {
            var week = IntermediateTimetableSeedPlan.BuildWeek(classroom);

            week.Should().HaveCount(35);
            week.Select(x => (x.Day, x.Period)).Should().OnlyHaveUniqueItems();
            week.Select(x => x.SubjectName).Distinct().Should().HaveCount(12);

            foreach (var subject in IntermediateTimetableSeedPlan.Subjects)
            {
                week.Count(x => x.SubjectName == subject.Name)
                    .Should().Be(subject.IndividualPeriods + 2 * subject.PairedBlocks);
            }
        }
    }

    [Fact]
    public void Every_declared_pair_stays_adjacent_on_one_study_day()
    {
        var week = IntermediateTimetableSeedPlan.BuildWeek(4);

        foreach (var pair in week.Where(x => x.PairKey.HasValue).GroupBy(x => x.PairKey))
        {
            pair.Should().HaveCount(2);
            pair.Select(x => x.Day).Distinct().Should().ContainSingle();
            (pair.Max(x => x.Period) - pair.Min(x => x.Period)).Should().Be(1);
        }
    }
}
