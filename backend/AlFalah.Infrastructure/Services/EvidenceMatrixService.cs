using System.Globalization;
using AlFalah.Application.DTOs.EvidenceMatrix;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AlFalah.Infrastructure.Services;

/// <summary>Read, review and export surface for the automated evidence matrix.</summary>
public sealed class EvidenceMatrixService : IEvidenceMatrixService
{
    private readonly AlFalahDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly SchoolScopeGuard _scopeGuard;
    private readonly AuditLogWriter _audit;
    private readonly EvidenceSubmissionService _submissions;

    public EvidenceMatrixService(
        AlFalahDbContext context,
        ICurrentUserService currentUser,
        SchoolScopeGuard scopeGuard,
        AuditLogWriter audit,
        EvidenceSubmissionService submissions)
    {
        _context = context;
        _currentUser = currentUser;
        _scopeGuard = scopeGuard;
        _audit = audit;
        _submissions = submissions;
    }

    public async Task<IReadOnlyList<AcademicYearDto>> GetAcademicYearsAsync(CancellationToken cancellationToken = default)
    {
        EnsureCanView();
        return await _context.AcademicYears.AsNoTracking().OrderByDescending(x => x.IsActive).ThenByDescending(x => x.StartsOn)
            .Select(x => new AcademicYearDto(x.Id, x.Code, x.NameAr, x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<EvidenceMatrixDto> GetAsync(EvidenceMatrixFilterDto filter, CancellationToken cancellationToken = default)
    {
        EnsureCanView();
        var year = await ResolveAcademicYearAsync(filter.AcademicYearId, cancellationToken);
        var allowedSchoolId = _scopeGuard.ResolveAllowedSchoolId(filter.SchoolId);
        var taskQuery = _context.EvidenceTasks.AsNoTracking().Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(filter.Category))
            taskQuery = taskQuery.Where(x => x.Category == filter.Category.Trim());
        var tasks = await taskQuery.OrderBy(x => x.CategorySortOrder).ThenBy(x => x.SortOrder)
            .Select(x => new EvidenceTaskDto(x.Id, x.Code, x.NameAr, x.Category, x.CategorySortOrder, x.SortOrder))
            .ToListAsync(cancellationToken);

        var teacherQuery = _context.InstructorProfiles.AsNoTracking()
            .Where(x => x.IsActive && !x.IsDeleted);
        if (allowedSchoolId.HasValue) teacherQuery = teacherQuery.Where(x => x.SchoolId == allowedSchoolId.Value);
        if (filter.TeacherId.HasValue) teacherQuery = teacherQuery.Where(x => x.Id == filter.TeacherId.Value);
        var teachers = await teacherQuery
            .OrderBy(x => x.User.FirstName).ThenBy(x => x.User.LastName)
            .Select(x => new { x.Id, x.SchoolId, TeacherName = x.User.FirstName + " " + x.User.LastName, SchoolName = x.School.Name })
            .ToListAsync(cancellationToken);

        var teacherIds = teachers.Select(x => x.Id).ToArray();
        var taskIds = tasks.Select(x => x.Id).ToArray();
        var statusRows = teacherIds.Length == 0 || taskIds.Length == 0
            ? []
            : await _context.TeacherTaskStatuses.AsNoTracking()
                .Where(x => x.AcademicYearId == year.Id && teacherIds.Contains(x.TeacherId) && taskIds.Contains(x.TaskId))
                .Select(x => new { x.TeacherId, x.TaskId, x.ActiveFilesCount, x.CellStatus })
                .ToListAsync(cancellationToken);
        var statuses = statusRows.ToDictionary(x => (x.TeacherId, x.TaskId));

        var rows = teachers.Select(teacher =>
        {
            var cells = tasks.Select(task => statuses.TryGetValue((teacher.Id, task.Id), out var status)
                ? new EvidenceMatrixCellDto(task.Id, status.CellStatus, status.ActiveFilesCount > 0, status.ActiveFilesCount)
                : new EvidenceMatrixCellDto(task.Id, EvidenceCellStatus.NotUploaded, false, 0)).ToList();
            return new EvidenceMatrixTeacherRowDto(
                teacher.Id,
                teacher.TeacherName.Trim(),
                teacher.SchoolId,
                teacher.SchoolName,
                cells.Count(x => x.IsChecked),
                cells);
        });

        if (filter.CompletionStatus.HasValue)
            rows = rows.Where(x => x.Cells.Any(cell => cell.Status == filter.CompletionStatus.Value));

        return new(
            new AcademicYearDto(year.Id, year.Code, year.NameAr, year.IsActive),
            tasks,
            rows.ToList(),
            tasks.Count);
    }

    public async Task<EvidenceCellFilesDto> GetCellFilesAsync(int teacherId, int taskId, int academicYearId, CancellationToken cancellationToken = default)
    {
        EnsureCanView();
        var teacher = await _context.InstructorProfiles.AsNoTracking()
            .Where(x => x.Id == teacherId && x.IsActive && !x.IsDeleted)
            .Select(x => new { x.Id, x.SchoolId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("المعلم غير موجود.");
        EnsureTeacherIsInScope(teacher.SchoolId);
        await EnsureTaskAndYearExistAsync(taskId, academicYearId, cancellationToken);

        var files = await _context.TeacherEvidenceSubmissions.AsNoTracking()
            .Where(x => x.TeacherId == teacherId && x.TaskId == taskId && x.AcademicYearId == academicYearId && !x.IsDeleted)
            .OrderByDescending(x => x.UploadedAtUtc)
            .Select(x => new EvidenceSubmissionFileDto(x.Id, x.FileName, x.FileExtension, x.SizeInBytes, x.WebUrl,
                x.ReviewStatus, x.IsDeleted, x.IsMissingFromDrive, x.UploadedAtUtc, x.ReviewNote))
            .ToListAsync(cancellationToken);
        var status = await _context.TeacherTaskStatuses.AsNoTracking()
            .Where(x => x.TeacherId == teacherId && x.TaskId == taskId && x.AcademicYearId == academicYearId)
            .Select(x => (EvidenceCellStatus?)x.CellStatus)
            .SingleOrDefaultAsync(cancellationToken) ?? EvidenceCellStatus.NotUploaded;
        return new(teacherId, taskId, academicYearId, status, files);
    }

    public async Task ReviewAsync(long submissionId, EvidenceReviewStatus reviewStatus, string? note, CancellationToken cancellationToken = default)
    {
        EnsureCanReview();
        if (reviewStatus is not (EvidenceReviewStatus.Approved or EvidenceReviewStatus.Rejected))
            throw new ArgumentException("تدعم المراجعة اعتماد الدليل أو رفضه فقط.");

        await using var transaction = await BeginTransactionIfRelationalAsync(cancellationToken);
        var submission = await _context.TeacherEvidenceSubmissions.SingleOrDefaultAsync(x => x.Id == submissionId, cancellationToken)
            ?? throw new KeyNotFoundException("ملف الدليل غير موجود.");
        if (submission.IsDeleted || submission.TaskId is null || submission.AcademicYearId is null)
            throw new InvalidOperationException("لا يمكن مراجعة هذا الملف.");
        EnsureTeacherIsInScope(submission.SchoolId);

        var oldStatus = submission.ReviewStatus;
        submission.ReviewStatus = reviewStatus;
        submission.ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        submission.ReviewedAtUtc = DateTimeOffset.UtcNow;
        submission.ReviewedByUserId = _currentUser.UserId;
        await _context.SaveChangesAsync(cancellationToken);
        await _submissions.RecalculateTaskStatusAsync(submission.TeacherId, submission.SchoolId, submission.TaskId.Value,
            submission.AcademicYearId.Value, cancellationToken, _currentUser.UserId);
        _audit.Write(submission.SchoolId, _currentUser.UserId,
            reviewStatus == EvidenceReviewStatus.Approved ? "TeacherEvidence.Approved" : "TeacherEvidence.Rejected",
            "TeacherEvidenceSubmission", submissionId.ToString(), submission.ReviewNote,
            new { ReviewStatus = oldStatus.ToString() }, new { ReviewStatus = reviewStatus.ToString(), submission.TaskId, submission.AcademicYearId });
        await _context.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
    }

    public async Task<EvidenceMatrixExportResult> ExportExcelAsync(EvidenceMatrixFilterDto filter, CancellationToken cancellationToken = default)
    {
        var matrix = await GetAsync(filter, cancellationToken);
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("مصفوفة الأدلة");
        sheet.RightToLeft = true;
        sheet.Cell(1, 1).Value = $"مصفوفة متابعة أدلة المعلمين - {matrix.AcademicYear.NameAr}";
        sheet.Range(1, 1, 1, matrix.Tasks.Count + 3).Merge();
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Cell(2, 1).Value = "المعلم";
        sheet.Cell(2, 2).Value = "المدرسة";
        sheet.Cell(2, 3).Value = "الإجمالي";
        for (var i = 0; i < matrix.Tasks.Count; i++)
            sheet.Cell(2, i + 4).Value = matrix.Tasks[i].NameAr;
        for (var rowIndex = 0; rowIndex < matrix.Rows.Count; rowIndex++)
        {
            var row = matrix.Rows[rowIndex];
            var excelRow = rowIndex + 3;
            sheet.Cell(excelRow, 1).Value = row.TeacherName;
            sheet.Cell(excelRow, 2).Value = row.SchoolName;
            sheet.Cell(excelRow, 3).Value = row.CompletedTasksCount;
            for (var col = 0; col < row.Cells.Count; col++)
            {
                var cell = row.Cells[col];
                sheet.Cell(excelRow, col + 4).Value = cell.IsChecked ? $"✓ {cell.Status}" : cell.Status.ToString();
            }
        }
        var lastColumn = matrix.Tasks.Count + 3;
        sheet.Range(2, 1, 2, lastColumn).Style.Fill.BackgroundColor = XLColor.FromHtml("#15603D");
        sheet.Range(2, 1, 2, lastColumn).Style.Font.FontColor = XLColor.White;
        sheet.Range(2, 1, 2, lastColumn).Style.Font.Bold = true;
        sheet.Range(2, 1, Math.Max(2, matrix.Rows.Count + 2), lastColumn).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Columns().AdjustToContents(8, 35);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"evidence-matrix-{matrix.AcademicYear.Code}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.xlsx");
    }

    public async Task<EvidenceMatrixExportResult> ExportPdfAsync(EvidenceMatrixFilterDto filter, CancellationToken cancellationToken = default)
    {
        var matrix = await GetAsync(filter, cancellationToken);
        var bytes = Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A3.Landscape());
                page.Margin(16);
                page.DefaultTextStyle(x => x.FontSize(7));
                page.Header().AlignCenter().Text($"مصفوفة متابعة أدلة المعلمين - {matrix.AcademicYear.NameAr}").Bold().FontSize(14);
                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(90);
                        columns.ConstantColumn(32);
                        foreach (var _ in matrix.Tasks) columns.RelativeColumn();
                    });
                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCell).Text("المعلم");
                        header.Cell().Element(HeaderCell).Text("الإجمالي");
                        foreach (var task in matrix.Tasks) header.Cell().Element(HeaderCell).Text(task.NameAr);
                    });
                    foreach (var row in matrix.Rows)
                    {
                        table.Cell().Element(BodyCell).Text(row.TeacherName);
                        table.Cell().Element(BodyCell).Text(row.CompletedTasksCount.ToString(CultureInfo.InvariantCulture));
                        foreach (var cell in row.Cells)
                            table.Cell().Element(BodyCell).Text(cell.IsChecked ? "✓" : "—");
                    }
                });
                page.Footer().AlignCenter().Text($"تم التصدير {DateTimeOffset.UtcNow:yyyy/MM/dd HH:mm} UTC");
            });
        }).GeneratePdf();
        return new(bytes, "application/pdf", $"evidence-matrix-{matrix.AcademicYear.Code}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.pdf");
    }

    private static IContainer HeaderCell(IContainer container) => container.Border(1).Background("15603D").Padding(2).AlignCenter().DefaultTextStyle(x => x.FontColor(Colors.White).Bold());
    private static IContainer BodyCell(IContainer container) => container.Border(0.5f).Padding(2).AlignCenter();

    private void EnsureCanView()
    {
        if (IsEvidenceSupervisor()) return;
        throw new UnauthorizedAccessException("ليس لديك صلاحية لعرض مصفوفة الأدلة.");
    }

    private void EnsureCanReview()
    {
        EnsureCanView();
        if (IsEvidenceSupervisor()) return;
        throw new UnauthorizedAccessException("ليس لديك صلاحية لمراجعة الأدلة.");
    }

    private bool IsEvidenceSupervisor()
    {
        var roles = _currentUser.GetRoles().ToHashSet(StringComparer.Ordinal);
        return roles.Contains(RoleNames.SuperAdmin) || roles.Contains(RoleNames.MainManager)
            || roles.Contains(RoleNames.SchoolManager) || roles.Contains(RoleNames.Moderator);
    }

    private void EnsureTeacherIsInScope(int schoolId)
    {
        var allowedSchool = _scopeGuard.ResolveAllowedSchoolId(null);
        if (allowedSchool.HasValue && allowedSchool.Value != schoolId)
            throw new UnauthorizedAccessException("بيانات هذا المعلم خارج نطاق مدرستك.");
    }

    private async Task<AlFalah.Domain.Entities.AcademicYear> ResolveAcademicYearAsync(int? academicYearId, CancellationToken cancellationToken)
    {
        var query = _context.AcademicYears.AsNoTracking();
        var year = academicYearId.HasValue
            ? await query.SingleOrDefaultAsync(x => x.Id == academicYearId.Value, cancellationToken)
            : await query.SingleOrDefaultAsync(x => x.IsActive, cancellationToken);
        return year ?? throw new KeyNotFoundException("السنة الدراسية غير موجودة.");
    }

    private async Task EnsureTaskAndYearExistAsync(int taskId, int yearId, CancellationToken cancellationToken)
    {
        if (!await _context.EvidenceTasks.AnyAsync(x => x.Id == taskId, cancellationToken)
            || !await _context.AcademicYears.AnyAsync(x => x.Id == yearId, cancellationToken))
            throw new KeyNotFoundException("المهمة أو السنة الدراسية غير موجودة.");
    }

    private async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?> BeginTransactionIfRelationalAsync(CancellationToken cancellationToken)
    {
        if (!_context.Database.IsRelational()) return null;
        return await _context.Database.BeginTransactionAsync(cancellationToken);
    }
}
