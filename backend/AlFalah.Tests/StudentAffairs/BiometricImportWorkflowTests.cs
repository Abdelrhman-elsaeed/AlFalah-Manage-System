using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.Biometrics;
using AlFalah.Application.StudentAffairs.Biometrics.Handlers;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Domain.Enums.StudentAffairs;
using AlFalah.Infrastructure.Integrations.Biometrics;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AlFalah.Tests.StudentAffairs;

public sealed class BiometricImportWorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ImportZajelBiometricCommandHandler_WhenStudentsLate_CreatesDelaysUsingIdentityNumber()
    {
        var repository = new FakeBiometricImportRepository();
        var reader = new FakeBiometricReader(new[]
        {
            new ZajelBiometricPunchRow(2, "1020304050", Now, new DateOnly(2026, 9, 1), new TimeOnly(7, 30), "حاضر"),
            new ZajelBiometricPunchRow(3, "1020304051", Now, new DateOnly(2026, 9, 1), new TimeOnly(6, 40), "حاضر") // On time (cutoff 6:45)
        });

        var handler = new ImportZajelBiometricCommandHandler(
            reader,
            repository,
            CreateUser(RoleNames.StudentAffairsOfficer, PermissionNames.BiometricImport),
            new FixedTimeProvider(Now));

        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var result = await handler.Handle(
            new ImportZajelBiometricCommand(stream, "zajel.xlsx"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.TotalRows.Should().Be(2);
        result.Data.ImportedDelays.Should().Be(1);
        result.Data.SkippedOnTimeRows.Should().Be(1);
        result.Data.DuplicateRows.Should().Be(0);
        result.Data.UnmatchedRows.Should().Be(0);

        repository.Delays.Should().HaveCount(1);
        repository.Delays[0].StudentId.Should().Be(101);
        repository.Delays[0].DelayMinutes.Should().Be(45);
    }

    [Fact]
    public async Task ImportZajelBiometricCommandHandler_WhenReuploadedOnSameDay_UpdatesExistingDelaysIdempotently()
    {
        var repository = new FakeBiometricImportRepository();
        var reader = new FakeBiometricReader(new[]
        {
            new ZajelBiometricPunchRow(2, "1020304050", Now, new DateOnly(2026, 9, 1), new TimeOnly(7, 30), "حاضر")
        });

        var handler = new ImportZajelBiometricCommandHandler(
            reader,
            repository,
            CreateUser(RoleNames.StudentAffairsOfficer, PermissionNames.BiometricImport),
            new FixedTimeProvider(Now));

        // First upload
        using var stream1 = new MemoryStream(new byte[] { 1, 2, 3 });
        var firstResult = await handler.Handle(
            new ImportZajelBiometricCommand(stream1, "zajel.xlsx"),
            CancellationToken.None);

        firstResult.IsSuccess.Should().BeTrue();
        firstResult.Data!.ImportedDelays.Should().Be(1);
        repository.Delays.Should().HaveCount(1);
        repository.Delays[0].DelayMinutes.Should().Be(45);

        // Second upload with updated time (7:40 instead of 7:30)
        var reader2 = new FakeBiometricReader(new[]
        {
            new ZajelBiometricPunchRow(2, "1020304050", Now.AddMinutes(10), new DateOnly(2026, 9, 1), new TimeOnly(7, 40), "حاضر")
        });
        var handler2 = new ImportZajelBiometricCommandHandler(
            reader2,
            repository,
            CreateUser(RoleNames.StudentAffairsOfficer, PermissionNames.BiometricImport),
            new FixedTimeProvider(Now));

        using var stream2 = new MemoryStream(new byte[] { 1, 2, 3 });
        var secondResult = await handler2.Handle(
            new ImportZajelBiometricCommand(stream2, "zajel.xlsx"),
            CancellationToken.None);

        secondResult.IsSuccess.Should().BeTrue();
        secondResult.Data!.DuplicateRows.Should().Be(1);
        secondResult.Data.ImportedDelays.Should().Be(1);
        repository.Delays.Should().HaveCount(1); // No duplicate delay entity created
        repository.Delays[0].DelayMinutes.Should().Be(55); // Updated
    }

    [Fact]
    public async Task ZajelBiometricWorkbookReader_ParsesFlexibleHeadersAndArabicDigits()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("تقرير الحضور");
        worksheet.Cell(1, 1).Value = "رقم الهوية";
        worksheet.Cell(1, 2).Value = "الاسم";
        worksheet.Cell(1, 3).Value = "وقت الحضور";
        worksheet.Cell(1, 4).Value = "حالة الحضور";

        worksheet.Cell(2, 1).Value = "١٠٢٠٣٠٤٠٥٠";
        worksheet.Cell(2, 2).Value = "طالب اختباري";
        worksheet.Cell(2, 3).Value = "07:35:00";
        worksheet.Cell(2, 4).Value = "حاضر";

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        var configuration = new ConfigurationBuilder().Build();
        var reader = new ZajelBiometricWorkbookReader(configuration);

        var rows = await reader.ReadAsync(ms, CancellationToken.None);
        rows.Should().HaveCount(1);
        rows[0].IdentityNumber.Should().Be("١٠٢٠٣٠٤٠٥٠");
        rows[0].SchoolLocalTime.Should().Be(new TimeOnly(7, 35));
        ImportZajelBiometricCommandHandler.NormalizeIdentityNumber(rows[0].IdentityNumber).Should().Be("1020304050");
    }

    private sealed class FakeBiometricReader : IZajelBiometricWorkbookReader
    {
        private readonly IReadOnlyList<ZajelBiometricPunchRow> _rows;
        public FakeBiometricReader(IReadOnlyList<ZajelBiometricPunchRow> rows) => _rows = rows;

        public Task<IReadOnlyList<ZajelBiometricPunchRow>> ReadAsync(Stream content, CancellationToken cancellationToken) =>
            Task.FromResult(_rows);
    }

    private sealed class FakeBiometricImportRepository : IBiometricImportRepository
    {
        public List<MorningArrivalDelay> Delays { get; } = new();

        public Task<BiometricImportSettingsSnapshot?> GetSettingsAsync(int schoolId, CancellationToken cancellationToken) =>
            Task.FromResult<BiometricImportSettingsSnapshot?>(new BiometricImportSettingsSnapshot(new TimeOnly(6, 30), 15));

        public Task<IReadOnlyList<BiometricEnrollmentSnapshot>> GetEnrollmentsAsync(
            int schoolId,
            IReadOnlyCollection<string> identityNumbers,
            DateOnly fromDate,
            DateOnly toDate,
            CancellationToken cancellationToken)
        {
            var list = new List<BiometricEnrollmentSnapshot>();
            if (identityNumbers.Contains("1020304050"))
            {
                list.Add(new BiometricEnrollmentSnapshot(101, "1020304050", 1, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));
            }
            if (identityNumbers.Contains("1020304051"))
            {
                list.Add(new BiometricEnrollmentSnapshot(102, "1020304051", 1, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));
            }
            return Task.FromResult<IReadOnlyList<BiometricEnrollmentSnapshot>>(list);
        }

        public Task<Dictionary<(int StudentId, DateOnly Date), MorningArrivalDelay>> GetExistingDelaysForUpdateAsync(
            int schoolId,
            IReadOnlyCollection<int> studentIds,
            DateOnly fromDate,
            DateOnly toDate,
            CancellationToken cancellationToken)
        {
            var dict = Delays
                .Where(d => d.SchoolId == schoolId && studentIds.Contains(d.StudentId) && d.SchoolLocalDate >= fromDate && d.SchoolLocalDate <= toDate)
                .GroupBy(d => (d.StudentId, d.SchoolLocalDate))
                .ToDictionary(g => g.Key, g => g.First());
            return Task.FromResult(dict);
        }

        public void AddRange(IEnumerable<MorningArrivalDelay> delays) => Delays.AddRange(delays);

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }

    private static ICurrentUserService CreateUser(string roleName, params string[] permissions) =>
        new FakeCurrentUserService("officer-user-1", 1, roleName, permissions);

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        private readonly HashSet<string> _roles;
        private readonly HashSet<string> _permissions;

        public FakeCurrentUserService(string userId, int schoolId, string role, params string[] permissions)
        {
            UserId = userId;
            ActiveSchoolId = schoolId;
            _roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { role };
            _permissions = new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase);
        }

        public string? UserId { get; }
        public string? Username => "test.user";
        public int? ActiveSchoolId { get; }
        public string? PreferredLanguage => "ar";
        public bool IsAuthenticated => true;

        public bool IsInRole(string roleName) => _roles.Contains(roleName);
        public bool HasPermission(string permissionName) => _permissions.Contains(permissionName);
        public IEnumerable<string> GetRoles() => _roles;
        public IEnumerable<string> GetPermissions() => _permissions;
        public bool IsGlobalAdmin() => false;
        public bool IsSchoolScopedRole() => true;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        public FixedTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}