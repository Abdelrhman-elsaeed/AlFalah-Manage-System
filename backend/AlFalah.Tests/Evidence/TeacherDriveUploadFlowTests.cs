using System.Text;
using AlFalah.Application.Common.Exceptions;
using AlFalah.Application.DTOs.EvidenceMatrix;
using AlFalah.Domain.Enums;
using AlFalah.Tests.TestDoubles;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AlFalah.Tests.Evidence;

/// <summary>
/// The teacher side of the flow, and the promise the manager cares about: when a teacher
/// uploads a file it lands in THEIR folder on Google Drive, is attributed to THEM in the
/// ledger, and the matching cell in the evidence matrix ("the sheet") ticks — verifiably, so a
/// manager can check that this teacher really did upload that evidence.
/// </summary>
public sealed class TeacherDriveUploadFlowTests
{
    [Fact]
    public async Task An_Upload_Lands_In_The_Teachers_Folder_And_Ticks_Their_Matrix_Cell()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();

        var result = await harness.UploadAsync(TeacherDriveHarness.TeacherA(), taskId: 1, fileName: "خطة الدروس.pdf");

        // 1. The bytes reached Drive, inside teacher A's granted folder and shared drive.
        var upload = harness.Drive.Uploads.Should().ContainSingle().Subject;
        upload.ParentFolderId.Should().Be(TeacherDriveHarness.FolderA);
        upload.SharedDriveId.Should().Be(TeacherDriveHarness.SharedDriveId);
        upload.FileName.Should().Be("خطة الدروس.pdf");

        // 2. The ledger attributes the file to teacher A — this is the proof of authorship,
        //    because Drive itself only ever shows the school credential as the uploader.
        var submission = await harness.Context.TeacherEvidenceSubmissions.SingleAsync();
        submission.Id.Should().Be(result.SubmissionId);
        submission.TeacherId.Should().Be(TeacherDriveHarness.TeacherAId);
        submission.SchoolId.Should().Be(TeacherDriveHarness.SchoolId);
        submission.TaskId.Should().Be(1);
        submission.AcademicYearId.Should().Be(1);
        submission.DriveItemId.Should().Be(upload.FileId);
        submission.ParentItemId.Should().Be(TeacherDriveHarness.FolderA);
        submission.UploadStatus.Should().Be(EvidenceUploadStatus.Completed);
        submission.ReviewStatus.Should().Be(EvidenceReviewStatus.PendingReview);

        // 3. The matrix cell for task 1 — and only task 1 — is now ticked.
        var matrix = await harness.MatrixService(TeacherDriveHarness.Manager()).GetAsync(new EvidenceMatrixFilterDto());
        var rowA = matrix.Rows.Single(x => x.TeacherId == TeacherDriveHarness.TeacherAId);
        rowA.CompletedTasksCount.Should().Be(1);
        rowA.Cells.Single(x => x.TaskId == 1).Should().BeEquivalentTo(
            new EvidenceMatrixCellDto(1, EvidenceCellStatus.PendingReview, true, 1));
        rowA.Cells.Single(x => x.TaskId == 2).IsChecked.Should().BeFalse();

        // 4. The other teacher's row is untouched — the sheet attributes it to the right person.
        var rowB = matrix.Rows.Single(x => x.TeacherId == TeacherDriveHarness.TeacherBId);
        rowB.CompletedTasksCount.Should().Be(0);

        // 5. The upload is auditable.
        harness.Context.AuditLogs.Should().Contain(x => x.Action == "TeacherEvidence.UploadCompleted");
    }

    [Fact]
    public async Task A_Manager_Can_Open_The_Uploaded_File_To_Verify_It()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        var result = await harness.UploadAsync(TeacherDriveHarness.TeacherA(), taskId: 1, content: "REAL-EVIDENCE");

        // Checking "did this teacher really upload this?" means reading the bytes, and the
        // reviewer has no Google session — so the API must stream them.
        var file = await harness.MatrixService(TeacherDriveHarness.Manager()).DownloadSubmissionAsync(result.SubmissionId);

        using var reader = new StreamReader(file.Content, Encoding.UTF8);
        (await reader.ReadToEndAsync()).Should().Be("REAL-EVIDENCE");
        file.FileName.Should().Be("دليل.pdf");
    }

    [Fact]
    public async Task The_Cell_Files_View_Names_The_Uploading_Teacher_And_Its_Files()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        await harness.UploadAsync(TeacherDriveHarness.TeacherA(), 1, "أول.pdf", requestId: "r1");
        await harness.UploadAsync(TeacherDriveHarness.TeacherA(), 1, "ثانٍ.pdf", requestId: "r2");

        var cell = await harness.MatrixService(TeacherDriveHarness.Manager())
            .GetCellFilesAsync(TeacherDriveHarness.TeacherAId, taskId: 1, academicYearId: 1);

        cell.Files.Select(x => x.FileName).Should().BeEquivalentTo(["أول.pdf", "ثانٍ.pdf"]);
        cell.Status.Should().Be(EvidenceCellStatus.PendingReview);
        // Two files for one task must still count as one completed task.
        (await harness.Context.TeacherTaskStatuses.SingleAsync()).ActiveFilesCount.Should().Be(2);
    }

    [Fact]
    public async Task The_Excel_Export_Shows_The_Checkmark_For_The_Uploading_Teacher()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        await harness.UploadAsync(TeacherDriveHarness.TeacherA(), taskId: 1);

        var export = await harness.MatrixService(TeacherDriveHarness.Manager()).ExportExcelAsync(new EvidenceMatrixFilterDto());

        export.Bytes.Should().NotBeEmpty();
        export.FileName.Should().StartWith("evidence-matrix-");
        using var workbook = new ClosedXML.Excel.XLWorkbook(new MemoryStream(export.Bytes));
        var sheet = workbook.Worksheets.First();
        var teacherRow = Enumerable.Range(3, 4).First(row => sheet.Cell(row, 1).GetString().Contains("المعلم أ"));
        sheet.Cell(teacherRow, 3).GetString().Should().Be("1");
        sheet.Cell(teacherRow, 4).GetString().Should().StartWith("✓");
    }

    [Fact]
    public async Task Uploading_Into_A_Subfolder_Of_The_Grant_Is_Allowed()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();

        await harness.UploadAsync(TeacherDriveHarness.TeacherA(), taskId: 1, parentItemId: TeacherDriveHarness.FolderASub);

        harness.Drive.Uploads.Single().ParentFolderId.Should().Be(TeacherDriveHarness.FolderASub);
    }

    [Fact]
    public async Task Uploading_Into_Another_Teachers_Folder_Is_Denied_And_Leaves_No_Blocking_Reservation()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        var teacher = TeacherDriveHarness.TeacherA();

        await harness.Invoking(x => x.UploadAsync(teacher, 1, parentItemId: TeacherDriveHarness.FolderB, requestId: "attack"))
            .Should().ThrowAsync<TeacherDriveAccessDeniedException>();

        harness.Drive.Uploads.Should().BeEmpty();
        harness.Context.TeacherEvidenceSubmissions.Should().BeEmpty();
        // The destination is authorized BEFORE the operation is reserved, so a rejected attempt
        // must not leave a Pending row that blocks the teacher's next legitimate upload.
        harness.Context.EvidenceUploadOperations.Should().BeEmpty();
        await harness.Invoking(x => x.UploadAsync(teacher, 1, requestId: "attack")).Should().NotThrowAsync();
    }

    [Fact]
    public async Task Uploading_Outside_The_School_Tree_Is_Denied()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();

        await harness.Invoking(x => x.UploadAsync(TeacherDriveHarness.TeacherA(), 1, parentItemId: TeacherDriveHarness.OutsideRootFolderId))
            .Should().ThrowAsync<TeacherDriveAccessDeniedException>();
        await harness.Invoking(x => x.UploadAsync(TeacherDriveHarness.TeacherA(), 1, parentItemId: TeacherDriveHarness.SchoolRootFolderId))
            .Should().ThrowAsync<TeacherDriveAccessDeniedException>();

        harness.Drive.Uploads.Should().BeEmpty();
    }

    [Fact]
    public async Task Repeating_The_Same_Idempotency_Key_Does_Not_Upload_Twice()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        var teacher = TeacherDriveHarness.TeacherA();

        var first = await harness.UploadAsync(teacher, taskId: 1, requestId: "same-key");
        var retry = await harness.UploadAsync(teacher, taskId: 1, requestId: "same-key");

        retry.SubmissionId.Should().Be(first.SubmissionId);
        harness.Drive.Uploads.Should().HaveCount(1);
        (await harness.Context.TeacherEvidenceSubmissions.CountAsync()).Should().Be(1);
        (await harness.Context.TeacherTaskStatuses.SingleAsync()).ActiveFilesCount.Should().Be(1);
    }

    [Fact]
    public async Task Reusing_An_Idempotency_Key_For_A_Different_Task_Is_Rejected()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        var teacher = TeacherDriveHarness.TeacherA();
        await harness.UploadAsync(teacher, taskId: 1, requestId: "shared-key");

        // Otherwise one key could quietly tick a second task's cell.
        await harness.Invoking(x => x.UploadAsync(teacher, taskId: 2, requestId: "shared-key"))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task A_Disallowed_Extension_Or_An_Oversized_File_Never_Reaches_Drive()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        var teacher = TeacherDriveHarness.TeacherA();

        await harness.Invoking(x => x.UploadAsync(teacher, 1, fileName: "script.exe"))
            .Should().ThrowAsync<ArgumentException>();
        await harness.Invoking(x => x.UploadAsync(teacher, 1, fileName: "../escape.pdf"))
            .Should().ThrowAsync<ArgumentException>();

        await using var big = new MemoryStream(new byte[2 * 1024 * 1024]);
        await harness.UploadService(teacher)
            .Invoking(x => x.UploadAsync(new(big, "كبير.pdf", "application/pdf", big.Length, null, 1, "big")))
            .Should().ThrowAsync<ArgumentException>();

        harness.Drive.Uploads.Should().BeEmpty();
        harness.Context.EvidenceUploadOperations.Should().BeEmpty();
    }

    [Fact]
    public async Task An_Unknown_Task_Is_Rejected_Before_Anything_Is_Uploaded()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();

        await harness.Invoking(x => x.UploadAsync(TeacherDriveHarness.TeacherA(), taskId: 999))
            .Should().ThrowAsync<KeyNotFoundException>();

        harness.Drive.Uploads.Should().BeEmpty();
    }

    [Fact]
    public async Task A_Drive_Failure_Marks_The_Operation_Failed_And_Ticks_No_Cell()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        var teacher = TeacherDriveHarness.TeacherA();
        // The destination passes the guard, then Drive itself becomes unreachable.
        harness.Drive.UnreachableSchools.Add(TeacherDriveHarness.SchoolId);

        await harness.Invoking(x => x.UploadAsync(teacher, 1)).Should().ThrowAsync<Exception>();

        harness.Context.TeacherEvidenceSubmissions.Should().BeEmpty();
        harness.Context.TeacherTaskStatuses.Should().BeEmpty();
    }

    [Fact]
    public async Task Approving_A_File_Turns_The_Cell_Green()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        var result = await harness.UploadAsync(TeacherDriveHarness.TeacherA(), taskId: 1);

        await harness.MatrixService(TeacherDriveHarness.Manager())
            .ReviewAsync(result.SubmissionId, EvidenceReviewStatus.Approved, "دليل مكتمل");

        var cell = await harness.Context.TeacherTaskStatuses.SingleAsync();
        cell.CellStatus.Should().Be(EvidenceCellStatus.Approved);
        var submission = await harness.Context.TeacherEvidenceSubmissions.SingleAsync();
        submission.ReviewStatus.Should().Be(EvidenceReviewStatus.Approved);
        submission.ReviewNote.Should().Be("دليل مكتمل");
        submission.ReviewedByUserId.Should().Be(TeacherDriveHarness.ManagerUserId);
    }

    [Fact]
    public async Task The_Teacher_Sees_The_Review_Status_Next_To_Their_File()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        var teacher = TeacherDriveHarness.TeacherA();
        var result = await harness.UploadAsync(teacher, taskId: 1);
        await harness.MatrixService(TeacherDriveHarness.Manager())
            .ReviewAsync(result.SubmissionId, EvidenceReviewStatus.Rejected, "غير واضح");

        var page = await harness.BrowserService(teacher).ListAsync(new(null, null, null, null, null));

        page.Items.Single(x => x.ItemId == harness.Drive.Uploads.Single().FileId)
            .SubmissionStatus.Should().Be(nameof(EvidenceReviewStatus.Rejected));
    }

    [Fact]
    public async Task Deleting_The_Last_File_Trashes_It_On_Drive_And_Clears_The_Cell()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        var teacher = TeacherDriveHarness.TeacherA();
        var result = await harness.UploadAsync(teacher, taskId: 1);
        var fileId = harness.Drive.Uploads.Single().FileId;

        await harness.UploadService(teacher).DeleteAsync(result.SubmissionId);

        harness.Drive.Trashed.Should().Contain(fileId);
        (await harness.Context.TeacherEvidenceSubmissions.SingleAsync()).IsDeleted.Should().BeTrue();
        var cell = await harness.Context.TeacherTaskStatuses.SingleAsync();
        cell.ActiveFilesCount.Should().Be(0);
        cell.CellStatus.Should().Be(EvidenceCellStatus.NotUploaded);

        var matrix = await harness.MatrixService(TeacherDriveHarness.Manager()).GetAsync(new EvidenceMatrixFilterDto());
        matrix.Rows.Single(x => x.TeacherId == TeacherDriveHarness.TeacherAId).CompletedTasksCount.Should().Be(0);
    }

    [Fact]
    public async Task A_Teacher_Cannot_Delete_Another_Teachers_Submission()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        var victim = await harness.UploadAsync(TeacherDriveHarness.TeacherB(), taskId: 1, requestId: "b-1");

        await harness.UploadService(TeacherDriveHarness.TeacherA())
            .Invoking(x => x.DeleteAsync(victim.SubmissionId))
            .Should().ThrowAsync<KeyNotFoundException>();

        harness.Drive.Trashed.Should().BeEmpty();
        (await harness.Context.TeacherEvidenceSubmissions.SingleAsync()).IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Deleting_A_File_That_Left_The_Grant_Is_Denied()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        var teacher = TeacherDriveHarness.TeacherA();
        var result = await harness.UploadAsync(teacher, taskId: 1);

        // The file is moved out of the teacher's folder in Drive. Owning the ledger row must not
        // be enough on its own to delete a file that is no longer inside the current grant.
        harness.Drive.MoveExternally(harness.Drive.Uploads.Single().FileId, TeacherDriveHarness.OutsideRootFolderId);

        await harness.UploadService(teacher).Invoking(x => x.DeleteAsync(result.SubmissionId))
            .Should().ThrowAsync<TeacherDriveAccessDeniedException>();
        harness.Drive.Trashed.Should().BeEmpty();
    }

    [Fact]
    public async Task Deleting_Twice_Is_Harmless()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        var teacher = TeacherDriveHarness.TeacherA();
        var result = await harness.UploadAsync(teacher, taskId: 1);

        await harness.UploadService(teacher).DeleteAsync(result.SubmissionId);
        await harness.UploadService(teacher).Invoking(x => x.DeleteAsync(result.SubmissionId)).Should().NotThrowAsync();

        (await harness.Context.TeacherTaskStatuses.SingleAsync()).ActiveFilesCount.Should().Be(0);
    }

    [Fact]
    public async Task Deleting_One_Of_Two_Files_Keeps_The_Cell_Ticked()
    {
        await using var harness = await TeacherDriveHarness.CreateAsync();
        var teacher = TeacherDriveHarness.TeacherA();
        var first = await harness.UploadAsync(teacher, 1, "أول.pdf", requestId: "r1");
        await harness.UploadAsync(teacher, 1, "ثانٍ.pdf", requestId: "r2");

        await harness.UploadService(teacher).DeleteAsync(first.SubmissionId);

        var cell = await harness.Context.TeacherTaskStatuses.SingleAsync();
        cell.ActiveFilesCount.Should().Be(1);
        cell.CellStatus.Should().Be(EvidenceCellStatus.PendingReview);
    }
}
