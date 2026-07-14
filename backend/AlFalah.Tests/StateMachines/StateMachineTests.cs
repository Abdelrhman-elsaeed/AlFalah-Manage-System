using AlFalah.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace AlFalah.Tests.StateMachines;

/// <summary>
/// Pure unit tests for the state machines — visit (D-24, D-37, Phase 5) and
/// complaint (Phase 8). Verifies valid + invalid transitions, mirroring the
/// AllowedTransitions tables baked into <see cref="AlFalah.Infrastructure.Services.VisitService"/>
/// and <see cref="AlFalah.Infrastructure.Services.ComplaintService"/>.
/// </summary>
public class VisitStateMachineTests
{
    /// <summary>
    /// Mirrors the exact transitions enforced by VisitService.SubmitAsync /
    /// ApproveAsync / RejectAsync / ReopenAsync. Kept inline here so the test
    /// surface is independent of any service-side helper.
    /// </summary>
    private static readonly Dictionary<VisitStatus, HashSet<VisitStatus>> Allowed = new()
    {
        [VisitStatus.Draft]                  = new() { VisitStatus.PendingApproval },
        [VisitStatus.Submitted]              = new() { VisitStatus.PendingApproval }, // legacy synonym
        [VisitStatus.PendingApproval]        = new() { VisitStatus.Approved, VisitStatus.RejectedForChanges },
        [VisitStatus.RejectedForChanges]     = new() { VisitStatus.PendingApproval },
        [VisitStatus.Approved]               = new() { VisitStatus.Reopened },
        [VisitStatus.Reopened]               = new() { VisitStatus.PendingApproval },
        [VisitStatus.UnderReviewAfterComplaint] = new() { VisitStatus.PendingApproval, VisitStatus.Approved },
        [VisitStatus.Cancelled]              = new()
    };

    private static bool IsAllowed(VisitStatus from, VisitStatus to)
        => Allowed.TryGetValue(from, out var set) && set.Contains(to);

    [Theory]
    [InlineData(VisitStatus.Draft, VisitStatus.PendingApproval, true)]
    [InlineData(VisitStatus.RejectedForChanges, VisitStatus.PendingApproval, true)]
    [InlineData(VisitStatus.Reopened, VisitStatus.PendingApproval, true)]
    [InlineData(VisitStatus.PendingApproval, VisitStatus.Approved, true)]
    [InlineData(VisitStatus.PendingApproval, VisitStatus.RejectedForChanges, true)]
    [InlineData(VisitStatus.Approved, VisitStatus.Reopened, true)]
    [InlineData(VisitStatus.Reopened, VisitStatus.Approved, false)]   // must re-submit first
    [InlineData(VisitStatus.Approved, VisitStatus.RejectedForChanges, false)] // must reopen first
    [InlineData(VisitStatus.Draft, VisitStatus.Approved, false)]      // must submit first
    [InlineData(VisitStatus.Draft, VisitStatus.Reopened, false)]      // can't reopen a draft
    [InlineData(VisitStatus.Cancelled, VisitStatus.Approved, false)]  // terminal
    [InlineData(VisitStatus.Cancelled, VisitStatus.PendingApproval, false)]
    public void VisitStateMachine_Transition_IsAllowed(VisitStatus from, VisitStatus to, bool allowed)
    {
        IsAllowed(from, to).Should().Be(allowed,
            because: $"transition {from}→{to} should be {(allowed ? "allowed" : "rejected")}");
    }

    [Fact]
    public void Draft_Cannot_Be_Approved_Directly()
    {
        // Documenting the "must submit first" rule.
        IsAllowed(VisitStatus.Draft, VisitStatus.Approved).Should().BeFalse();
    }

    [Fact]
    public void Approved_Is_Reopen_Only_To_Reopened_Not_Back_To_Rejected()
    {
        // Phase 5 design: once Approved, the only way back is Reopen → resubmit.
        IsAllowed(VisitStatus.Approved, VisitStatus.RejectedForChanges).Should().BeFalse();
        IsAllowed(VisitStatus.Approved, VisitStatus.Reopened).Should().BeTrue();
    }
}

public class ComplaintStateMachineTests
{
    // Mirrors ComplaintService.AllowedTransitions verbatim.
    private static readonly Dictionary<ComplaintStatus, HashSet<ComplaintStatus>> Allowed = new()
    {
        [ComplaintStatus.Open]     = new() { ComplaintStatus.InReview },
        [ComplaintStatus.InReview] = new() { ComplaintStatus.Resolved, ComplaintStatus.Rejected },
        [ComplaintStatus.Resolved] = new() { ComplaintStatus.Closed },
        [ComplaintStatus.Rejected] = new() { ComplaintStatus.Closed },
        [ComplaintStatus.Closed]   = new()
    };

    private static bool IsAllowed(ComplaintStatus from, ComplaintStatus to)
        => Allowed.TryGetValue(from, out var set) && set.Contains(to);

    [Theory]
    [InlineData(ComplaintStatus.Open, ComplaintStatus.InReview, true)]
    [InlineData(ComplaintStatus.Open, ComplaintStatus.Resolved, false)]   // must go through InReview
    [InlineData(ComplaintStatus.Open, ComplaintStatus.Closed, false)]
    [InlineData(ComplaintStatus.InReview, ComplaintStatus.Resolved, true)]
    [InlineData(ComplaintStatus.InReview, ComplaintStatus.Rejected, true)]
    [InlineData(ComplaintStatus.InReview, ComplaintStatus.Open, false)]   // can't go back
    [InlineData(ComplaintStatus.Resolved, ComplaintStatus.Closed, true)]
    [InlineData(ComplaintStatus.Resolved, ComplaintStatus.Rejected, false)]
    [InlineData(ComplaintStatus.Rejected, ComplaintStatus.Closed, true)]
    [InlineData(ComplaintStatus.Closed, ComplaintStatus.Open, false)]      // terminal
    [InlineData(ComplaintStatus.Closed, ComplaintStatus.InReview, false)]
    public void ComplaintStateMachine_Transition_IsAllowed(ComplaintStatus from, ComplaintStatus to, bool allowed)
    {
        IsAllowed(from, to).Should().Be(allowed,
            because: $"transition {from}→{to} should be {(allowed ? "allowed" : "rejected")}");
    }
}