using AlFalah.Domain.Enums;
using AlFalah.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AlFalah.Tests.Evidence;

/// <summary>
/// Someone can always delete a file directly in Google Drive, which would otherwise leave the
/// matrix claiming evidence that no longer exists. These tests cover the sweep that keeps the
/// sheet honest — including the case where Drive is simply unreachable, which must never be
/// mistaken for "the file is gone".
/// </summary>
public sealed class EvidenceReconciliationTests
{
    [Fact]
    public async Task A_File_Trashed_Directly_In_Drive_Is_Flagged_And_The_Cell_Changes()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        await harness.UploadAsync(TeacherDriveHarness.TeacherA(), taskId: 1);
        harness.Drive.TrashExternally(harness.Drive.Uploads.Single().FileId);

        var changed = await harness.ReconciliationService().ReconcileAsync();

        changed.Should().Be(1);
        (await harness.Context.TeacherEvidenceSubmissions.SingleAsync()).IsMissingFromDrive.Should().BeTrue();
        (await harness.Context.TeacherTaskStatuses.SingleAsync()).CellStatus.Should().Be(EvidenceCellStatus.MissingFromDrive);
        harness.Context.AuditLogs.Should().Contain(x => x.Action == "TeacherEvidence.MissingFromDrive");
    }

    [Fact]
    public async Task A_Hard_Deleted_File_Is_Also_Flagged()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        await harness.UploadAsync(TeacherDriveHarness.TeacherA(), taskId: 1);
        harness.Drive.RemoveExternally(harness.Drive.Uploads.Single().FileId);

        await harness.ReconciliationService().ReconcileAsync();

        (await harness.Context.TeacherEvidenceSubmissions.SingleAsync()).IsMissingFromDrive.Should().BeTrue();
    }

    [Fact]
    public async Task Restoring_The_File_Clears_The_Flag_And_Restores_The_Cell()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        await harness.UploadAsync(TeacherDriveHarness.TeacherA(), taskId: 1);
        var fileId = harness.Drive.Uploads.Single().FileId;
        harness.Drive.TrashExternally(fileId);
        await harness.ReconciliationService().ReconcileAsync();

        harness.Drive.RestoreExternally(fileId);
        var changed = await harness.ReconciliationService().ReconcileAsync();

        changed.Should().Be(1);
        var submission = await harness.Context.TeacherEvidenceSubmissions.SingleAsync();
        submission.IsMissingFromDrive.Should().BeFalse();
        submission.MissingFromDriveAtUtc.Should().BeNull();
        (await harness.Context.TeacherTaskStatuses.SingleAsync()).CellStatus.Should().Be(EvidenceCellStatus.PendingReview);
        harness.Context.AuditLogs.Should().Contain(x => x.Action == "TeacherEvidence.RestoredOnDrive");
    }

    [Fact]
    public async Task A_Healthy_Sweep_Changes_Nothing()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        await harness.UploadAsync(TeacherDriveHarness.TeacherA(), taskId: 1);

        (await harness.ReconciliationService().ReconcileAsync()).Should().Be(0);
        (await harness.Context.TeacherEvidenceSubmissions.SingleAsync()).IsMissingFromDrive.Should().BeFalse();
    }

    [Fact]
    public async Task An_Unreachable_Drive_Leaves_The_Flag_Untouched()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        await harness.UploadAsync(TeacherDriveHarness.TeacherA(), taskId: 1);
        // A credential or network failure says nothing about whether the file exists. Marking it
        // missing here would wrongly clear a teacher's checkmark for an outage.
        harness.Drive.UnreachableSchools.Add(TeacherDriveHarness.SchoolId);

        (await harness.ReconciliationService().ReconcileAsync()).Should().Be(0);

        (await harness.Context.TeacherEvidenceSubmissions.SingleAsync()).IsMissingFromDrive.Should().BeFalse();
        (await harness.Context.TeacherTaskStatuses.SingleAsync()).CellStatus.Should().Be(EvidenceCellStatus.PendingReview);
    }

    [Fact]
    public async Task Schools_Without_A_Connected_Drive_Are_Skipped_Entirely()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        await harness.UploadAsync(TeacherDriveHarness.TeacherA(), taskId: 1);
        harness.Drive.TrashExternally(harness.Drive.Uploads.Single().FileId);
        await harness.ConnectSchoolDriveAsync(isEnabled: false);

        // With no live connection there is nothing to compare against, so the sweep must be a
        // no-op rather than log a token error for every file.
        (await harness.ReconciliationService().ReconcileAsync()).Should().Be(0);
        (await harness.Context.TeacherEvidenceSubmissions.SingleAsync()).IsMissingFromDrive.Should().BeFalse();
    }

    [Fact]
    public async Task A_Locally_Deleted_Submission_Is_Not_Reconciled()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        var teacher = TeacherDriveHarness.TeacherA();
        var result = await harness.UploadAsync(teacher, taskId: 1);
        await harness.UploadService(teacher).DeleteAsync(result.SubmissionId);

        (await harness.ReconciliationService().ReconcileAsync()).Should().Be(0);
        (await harness.Context.TeacherEvidenceSubmissions.SingleAsync()).IsMissingFromDrive.Should().BeFalse();
    }
}
