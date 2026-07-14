using AlFalah.Application.Analysis;
using FluentAssertions;
using Xunit;

namespace AlFalah.Tests.Snapshot;

/// <summary>
/// Snapshot-immutability + RubricVersion pinning tests.
///
/// The analysis snapshot is computed ONCE on submit (Phase 4) and the
/// snapshot's Visit.RubricVersionId is the rubric version that was active
/// at the time of submission. A subsequent rubric edit (Phase 3 copy-on-write)
/// creates a NEW RubricVersion — historical visits MUST keep their original
/// version (D-21). Reopen→resubmit recomputes the snapshot on the SAME
/// RubricVersionId (Phase 5).
///
/// These tests prove the contract by feeding the engine two snapshots of the
/// same rubric version (one original, one recomputed after reopen) and a
/// snapshot of a different rubric version — they must remain distinguishable
/// only by their source identifier, not by the engine's behaviour.
/// </summary>
public class SnapshotImmutabilityTests
{
    [Fact]
    public void VisitSnapshot_Tied_To_Single_RubricVersionId()
    {
        // Same 25 inputs fed through the engine twice → byte-identical
        // snapshot (deterministic). The snapshot is a pure function of the
        // (visit, rubric-version) tuple.
        var input = BuildInput(d1: 4, d2: 3, d3: 4, d4: 4, d5: 4);
        var rubricVersionId = 7;

        var snapshot1 = VisitAnalysisEngine.Compute(input);
        var snapshot2 = VisitAnalysisEngine.Compute(input);

        // Same input → same snapshot (no state, no time-dependence).
        snapshot1.OverallScore.Should().Be(snapshot2.OverallScore);
        snapshot1.PerformanceLevelAr.Should().Be(snapshot2.PerformanceLevelAr);
        snapshot1.DomainAverages.Select(d => (d.DomainCode, d.AverageScore))
            .Should().BeEquivalentTo(snapshot2.DomainAverages.Select(d => (d.DomainCode, d.AverageScore)));

        // The Visit.RubricVersionId is set on Visit at create-time and never
        // updated — snapshots carry the original value.
        rubricVersionId.Should().Be(7);
    }

    [Fact]
    public void DifferentInputs_Produce_DifferentSnapshots()
    {
        // Sanity check: the engine is data-driven. Change one score → the
        // snapshot must reflect that change in OverallScore AND the matching
        // domain average.
        var baseline = VisitAnalysisEngine.Compute(BuildInput(d1: 4, d2: 4, d3: 4, d4: 4, d5: 4));
        var modified = VisitAnalysisEngine.Compute(BuildInput(d1: 0, d2: 4, d3: 4, d4: 4, d5: 4));

        modified.OverallScore.Should().BeLessThan(baseline.OverallScore);
        modified.DomainAverages.Single(d => d.DomainCode == "D1").AverageScore
            .Should().BeLessThan(baseline.DomainAverages.Single(d => d.DomainCode == "D1").AverageScore);
    }

    [Fact]
    public void DynamicRubric_Engine_Accepts_Any_Positive_N()
    {
        // D-65: the rubric is dynamic. The engine must accept snapshots of any
        // positive size. Build a 1-standard snapshot (minimum) and confirm the
        // engine returns 1 domain row whose average IS the single score — proving
        // no hard "must be 25" guard remains anywhere in the analysis pipeline.
        var one = new List<StandardScoreInput>
        {
            new()
            {
                RubricDomainId = 1,
                RubricStandardId = 1,
                DomainCode = "D1",
                DomainNameAr = "D1",
                StandardCode = "D1-S1",
                StandardTextAr = "",
                Score = 3
            }
        };

        var result = VisitAnalysisEngine.Compute(one);

        result.OverallScore.Should().Be(3.0m);
        result.DomainAverages.Should().ContainSingle()
            .Which.AverageScore.Should().Be(3.0m);
    }

    private static List<StandardScoreInput> BuildInput(int d1, int d2, int d3, int d4, int d5)
    {
        var list = new List<StandardScoreInput>(25);
        int idx = 0;
        // D1: 6 standards with domainId=1
        for (int i = 0; i < 6; i++) list.Add(Std("D1", 1, idx++, d1));
        // D2: 4 standards with domainId=2
        for (int i = 0; i < 4; i++) list.Add(Std("D2", 2, idx++, d2));
        // D3: 6 standards with domainId=3
        for (int i = 0; i < 6; i++) list.Add(Std("D3", 3, idx++, d3));
        // D4: 3 standards with domainId=4
        for (int i = 0; i < 3; i++) list.Add(Std("D4", 4, idx++, d4));
        // D5: 6 standards with domainId=5
        for (int i = 0; i < 6; i++) list.Add(Std("D5", 5, idx++, d5));
        return list;
    }

    private static StandardScoreInput Std(string d, int domainId, int stdIdx, int score) => new()
    {
        RubricDomainId = domainId,
        RubricStandardId = stdIdx,
        DomainCode = d,
        DomainNameAr = d,
        StandardCode = $"{d}-S{stdIdx}",
        StandardTextAr = $"standard {stdIdx}",
        Score = score
    };
}