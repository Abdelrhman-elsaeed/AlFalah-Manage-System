using AlFalah.Application.Common;
using AlFalah.Application.DTOs.Visits;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Services;
using AlFalah.Shared.Models;
using FluentAssertions;
using System.IO.Compression;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AlFalah.Tests.Services;

public sealed class VisitV2WorkflowTests
{
    [Theory]
    [InlineData("approve", VisitStatus.PendingApproval, VisitStatus.Approved)]
    [InlineData("reject", VisitStatus.PendingApproval, VisitStatus.RejectedForChanges)]
    [InlineData("reopen", VisitStatus.Approved, VisitStatus.Reopened)]
    public async Task Workflow_transitions_are_owned_by_v2_and_audited(
        string action,
        VisitStatus initial,
        VisitStatus expected)
    {
        await using var db = Database();
        var visit = Visit(initial, ExperienceVersion.PrototypeV2, schoolId: 7);
        var repository = new WorkflowRepository(visit, db);
        var service = Service(repository, db, new CurrentUser(7));

        var result = action switch
        {
            "approve" => await service.ApproveAsync(visit.Id),
            "reject" => await service.RejectAsync(visit.Id, "سبب آمن"),
            "reopen" => await service.ReopenAsync(visit.Id, "سبب آمن"),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };

        result.Status.Should().Be((int)expected);
        visit.Status.Should().Be(expected);
        repository.TransactionCount.Should().Be(1);
        var expectedAuditAction = $"VisitV2.{char.ToUpperInvariant(action[0])}{action.Substring(1)}";
        db.AuditLogs.Should().ContainSingle(a => a.Action == expectedAuditAction);
    }

    [Fact]
    public async Task Invalid_transition_returns_a_safe_business_message()
    {
        await using var db = Database();
        var service = Service(new WorkflowRepository(
            Visit(VisitStatus.Draft, ExperienceVersion.PrototypeV2, 7), db), db, new CurrentUser(7));

        var act = () => service.ApproveAsync(41);

        var error = await act.Should().ThrowAsync<BusinessRuleException>();
        error.Which.Message.Should().NotContain("System.").And.NotContain(" at ");
    }

    [Fact]
    public async Task V1_visit_cannot_enter_a_v2_mutation()
    {
        await using var db = Database();
        var service = Service(new WorkflowRepository(
            Visit(VisitStatus.PendingApproval, ExperienceVersion.Legacy, 7), db), db, new CurrentUser(7));

        await FluentActions.Invoking(() => service.ApproveAsync(41))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Cross_school_transition_is_rejected()
    {
        await using var db = Database();
        var service = Service(new WorkflowRepository(
            Visit(VisitStatus.PendingApproval, ExperienceVersion.PrototypeV2, 99), db), db, new CurrentUser(7));

        await FluentActions.Invoking(() => service.ApproveAsync(41))
            .Should().ThrowAsync<UnauthorizedSchoolAccessException>();
    }

    [Fact]
    public async Task V2_zip_export_uses_batch_repository_data_and_v2_documents()
    {
        await using var db = Database();
        var repository = new WorkflowRepository(
            Visit(VisitStatus.Approved, ExperienceVersion.PrototypeV2, 7), db);
        var service = Service(repository, db, new CurrentUser(7), new Documents());

        var result = await service.ExportZipAsync(new VisitV2ArchiveQuery());

        result.VisitCount.Should().Be(1);
        repository.ExportListCalls.Should().Be(1);
        repository.BatchAssetCalls.Should().Be(1);
        repository.SingleAssetCalls.Should().Be(0);
        using var archive = new ZipArchive(new MemoryStream(result.ZipBytes), ZipArchiveMode.Read);
        archive.Entries.Should().ContainSingle().Which.Name.Should().Be("visit-v2-41.pdf");
    }

    [Fact]
    public async Task School_manager_finalize_is_approved_atomically()
    {
        await using var db = Database();
        var visit = Visit(VisitStatus.Draft, ExperienceVersion.PrototypeV2, 7);
        var repository = new WorkflowRepository(visit, db);
        var service = Service(repository, db, new CurrentUser(7));

        var result = await service.FinalizeAsync(visit.Id);

        result.Status.Should().Be((int)VisitStatus.Approved);
        visit.Status.Should().Be(VisitStatus.Approved);
        visit.ApprovedByUserId.Should().Be("manager");
        visit.ApprovedAt.Should().NotBeNull();
        repository.TransactionCount.Should().Be(1);
        db.AuditLogs.Should().Contain(a => a.Action == "VisitV2.Finalize");
        db.AuditLogs.Should().Contain(a => a.Action == "VisitV2.AutoApprove");
    }

    private static VisitV2Service Service(
        IVisitV2Repository repository,
        AlFalahDbContext db,
        ICurrentUserService currentUser,
        IVisitV2DocumentService? documents = null) => new(
            repository,
            documents: documents!,
            currentUser,
            new EnabledFlags(),
            new SchoolScopeGuard(db, currentUser, NullLogger<SchoolScopeGuard>.Instance),
            new AuditLogWriter(db, new HttpContextAccessor(), NullLogger<AuditLogWriter>.Instance),
            NullLogger<VisitV2Service>.Instance);

    private static AlFalahDbContext Database() => new(
        new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseInMemoryDatabase($"visit-v2-workflow-{Guid.NewGuid()}")
            .Options);

    private static Visit Visit(VisitStatus status, ExperienceVersion version, int schoolId)
    {
        var domain = new RubricDomain { Id = 11, Code = "D1", NameAr = "بيئة التعلم", SortOrder = 1 };
        var standard = new RubricStandard
        {
            Id = 21,
            Code = "D1-S1",
            TextAr = "يوفر المعلم بيئة تعلم آمنة.",
            SortOrder = 1,
            Domain = domain,
            RubricDomainId = domain.Id
        };
        domain.Standards.Add(standard);

        return new Visit
        {
            Id = 41,
            SchoolId = schoolId,
            School = new School { Id = schoolId, Name = "School" },
            InstructorId = "teacher",
            Instructor = new ApplicationUser { Id = "teacher", FirstName = "Teacher" },
            CreatedByUserId = "moderator",
            CreatedByUser = new ApplicationUser { Id = "moderator", FirstName = "Moderator" },
            RubricVersionId = 2,
            RubricVersion = new RubricVersion { Id = 2, VersionNumber = 2 },
            ExperienceVersion = version,
            Status = status,
            VisitCategory = VisitCategory.ClassroomOrPeriodic,
            VisitSequence = VisitSequence.First,
            VisitDate = DateTimeOffset.UtcNow,
            ClassroomPeriod = 1,
            Subject = "رياضيات",
            GradeClass = "2/أ",
            LessonTitle = "درس",
            Scores = new List<VisitScore>
            {
                new() { Id = 31, RubricStandardId = standard.Id, RubricStandard = standard, Score = 3 }
            }
        };
    }

    private sealed class EnabledFlags : IFeatureFlagService
    {
        public bool IsVisitsV2Enabled(int? schoolId = null) => true;
    }

    private sealed class Documents : IVisitV2DocumentService
    {
        public VisitV2CsvExportDto BuildCsv(IReadOnlyList<VisitV2CsvRow> rows) =>
            new(Array.Empty<byte>(), "visits.csv");

        public Task<VisitV2PdfExportDto> BuildPdfAsync(
            VisitV2DetailDto visit,
            VisitV2PdfAssetSources assets,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new VisitV2PdfExportDto(new byte[] { 1, 2, 3 }, $"visit-{visit.Id}.pdf"));
    }

    private sealed class CurrentUser(int schoolId) : ICurrentUserService
    {
        public string? UserId => "manager";
        public string? Username => "manager";
        public int? ActiveSchoolId => schoolId;
        public string? PreferredLanguage => "ar";
        public bool IsAuthenticated => true;
        public bool IsInRole(string roleName) => roleName == RoleNames.SchoolManager;
        public bool HasPermission(string permissionName) =>
            permissionName is PermissionNames.VisitApprove or PermissionNames.VisitReopen;
        public IEnumerable<string> GetRoles() => new[] { RoleNames.SchoolManager };
        public IEnumerable<string> GetPermissions() => new[] { PermissionNames.VisitApprove, PermissionNames.VisitReopen };
        public bool IsGlobalAdmin() => false;
        public bool IsSchoolScopedRole() => true;
    }

    private sealed class WorkflowRepository(Visit visit, AlFalahDbContext db) : IVisitV2Repository
    {
        public int TransactionCount { get; private set; }
        public int ExportListCalls { get; private set; }
        public int BatchAssetCalls { get; private set; }
        public int SingleAssetCalls { get; private set; }
        public Task<Visit?> GetAsync(int id, bool tracking, CancellationToken cancellationToken = default) =>
            Task.FromResult<Visit?>(id == visit.Id && visit.ExperienceVersion == ExperienceVersion.PrototypeV2 ? visit : null);
        public async Task SaveChangesAsync(CancellationToken cancellationToken = default) => await db.SaveChangesAsync(cancellationToken);
        public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
        {
            TransactionCount++;
            await action(cancellationToken);
        }

        public Task<RubricVersion?> GetRubricAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ApplicationUser?> GetUserAsync(string userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> IsActiveInstructorAsync(string userId, int schoolId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> GetEvaluatorRoleAsync(string userId, int schoolId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(Visit item, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Dictionary<int, List<RubricIndicator>>> GetIndicatorsByStandardAsync(int rubricVersionId, IReadOnlyCollection<int> standardIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PagedResult<VisitV2ArchiveItemDto>> ListAsync(VisitV2ArchiveQuery query, int? schoolId, string? creatorUserId, string? instructorUserId, bool approvedOnly, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<VisitV2EvaluatorFilterDto>> ListEvaluatorsAsync(int? schoolId, string? creatorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Visit>> ListForExportAsync(VisitV2ArchiveQuery query, int? schoolId, string? creatorUserId, string? instructorUserId, bool approvedOnly, CancellationToken cancellationToken = default)
        {
            ExportListCalls++;
            return Task.FromResult<IReadOnlyList<Visit>>(new[] { visit });
        }
        public Task<int> CountActiveInstructorsAsync(int? schoolId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<string, string?>> GetEmployeeNumbersAsync(IReadOnlyCollection<string> instructorIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<VisitV2DashboardDto> GetDashboardAsync(int? schoolId, string? creatorUserId, int totalActiveTeachers, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<VisitV2PdfAssetSources> GetPdfAssetSourcesAsync(int visitId, CancellationToken cancellationToken = default)
        {
            SingleAssetCalls++;
            throw new NotSupportedException();
        }
        public Task<IReadOnlyDictionary<int, VisitV2PdfAssetSources>> GetPdfAssetSourcesAsync(IReadOnlyCollection<int> visitIds, CancellationToken cancellationToken = default)
        {
            BatchAssetCalls++;
            IReadOnlyDictionary<int, VisitV2PdfAssetSources> result = new Dictionary<int, VisitV2PdfAssetSources>
            {
                [visit.Id] = new("School", string.Empty, "#0F7132", null, null, null, null, true, true)
            };
            return Task.FromResult(result);
        }
        public Task AddReportViewAsync(int visitId, string instructorUserId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
