using AlFalah.Application.DTOs.TeacherDrive;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlFalah.Tests.TestDoubles;

/// <summary>
/// Wires the REAL teacher-drive services around a real DbContext and a
/// <see cref="FakeGoogleDrive"/>. Only Drive itself is substituted, so the identity
/// resolution, the folder guard, the ledger and the matrix recalculation under test are the
/// same code that runs in production.
///
/// The shape mirrors a configured school:
/// <code>
/// school-root                 (school's evidence root — never grantable)
///   ├── folder-a              (granted to teacher 1)
///   │     ├── a-existing.pdf
///   │     └── folder-a-sub    (nested; still inside teacher 1's grant)
///   │           └── a-nested.pdf
///   ├── folder-b              (granted to teacher 2)
///   │     └── b-secret.pdf
///   └── folder-unassigned     (inside the root, granted to nobody)
/// outside-root                (a folder the credential can see but the school does not own)
///   └── outside.pdf
/// </code>
/// </summary>
public sealed class TeacherDriveHarness : IAsyncDisposable
{
    public const int SchoolId = 1;
    public const int OtherSchoolId = 2;
    public const int TeacherAId = 1;
    public const int TeacherBId = 2;
    public const string TeacherAUserId = "USER-TEACHER-A";
    public const string TeacherBUserId = "USER-TEACHER-B";
    public const string ManagerUserId = "USER-MANAGER";
    public const string SchoolRootFolderId = "school-root";
    public const string FolderA = "folder-a";
    public const string FolderASub = "folder-a-sub";
    public const string FolderB = "folder-b";
    public const string FolderUnassigned = "folder-unassigned";
    public const string OutsideRootFolderId = "outside-root";
    public const string SharedDriveId = "shared-drive-1";

    private readonly ServiceProvider _dataProtection;

    private TeacherDriveHarness(AlFalahDbContext context, FakeGoogleDrive drive, ServiceProvider dataProtection)
    {
        Context = context;
        Drive = drive;
        _dataProtection = dataProtection;
    }

    public AlFalahDbContext Context { get; }
    public FakeGoogleDrive Drive { get; }

    public static async Task<TeacherDriveHarness> CreateAsync(bool connectSchoolDrive = true, bool grantFolders = true)
    {
        var context = new AlFalahDbContext(new DbContextOptionsBuilder<AlFalahDbContext>()
            .UseInMemoryDatabase($"teacher-drive-{Guid.NewGuid()}")
            .Options);

        var school = new School { Id = SchoolId, Name = "مدرسة الفلاح", City = "الرياض", IsActive = true };
        var otherSchool = new School { Id = OtherSchoolId, Name = "مدرسة أخرى", City = "جدة", IsActive = true };
        var userA = NewUser(TeacherAUserId, "المعلم", "أ");
        var userB = NewUser(TeacherBUserId, "المعلم", "ب");
        var manager = NewUser(ManagerUserId, "مدير", "المدرسة");
        context.AddRange(
            school, otherSchool, userA, userB, manager,
            new InstructorProfile { Id = TeacherAId, UserId = userA.Id, SchoolId = SchoolId, User = userA, School = school, IsActive = true },
            new InstructorProfile { Id = TeacherBId, UserId = userB.Id, SchoolId = SchoolId, User = userB, School = school, IsActive = true },
            new AcademicYear { Id = 1, Code = "1447", NameAr = "١٤٤٧هـ", StartsOn = new DateOnly(2025, 8, 1), EndsOn = new DateOnly(2026, 7, 31), IsActive = true },
            new EvidenceTask { Id = 1, Code = "T1", NameAr = "خطة الدروس", Category = "التخطيط", CategorySortOrder = 1, SortOrder = 1, IsActive = true },
            new EvidenceTask { Id = 2, Code = "T2", NameAr = "أوراق العمل", Category = "التخطيط", CategorySortOrder = 1, SortOrder = 2, IsActive = true });
        await context.SaveChangesAsync();

        var drive = new FakeGoogleDrive()
            .AddFolder(SchoolRootFolderId, "ملفات الإنجاز")
            .AddFolder(FolderA, "المعلم أ", SchoolRootFolderId)
            .AddFolder(FolderASub, "الفصل الأول", FolderA)
            .AddFolder(FolderB, "المعلم ب", SchoolRootFolderId)
            .AddFolder(FolderUnassigned, "غير مخصص", SchoolRootFolderId)
            .AddFolder(OutsideRootFolderId, "مجلد خارج المدرسة")
            .AddFile("a-existing.pdf", "دليل قديم.pdf", FolderA)
            .AddFile("a-nested.pdf", "دليل متداخل.pdf", FolderASub)
            .AddFile("b-secret.pdf", "دليل المعلم ب.pdf", FolderB)
            .AddFile("outside.pdf", "ملف خارجي.pdf", OutsideRootFolderId);

        var dataProtection = new ServiceCollection().AddDataProtection().Services.BuildServiceProvider();
        var harness = new TeacherDriveHarness(context, drive, dataProtection);

        if (connectSchoolDrive) await harness.ConnectSchoolDriveAsync();
        if (grantFolders)
        {
            await harness.GrantFolderAsync(TeacherAId, FolderA);
            await harness.GrantFolderAsync(TeacherBId, FolderB);
        }
        return harness;
    }

    // ─── Service factories (each builds the real implementation) ──────────────

    public GoogleDriveCredentialProtector Protector() =>
        new(_dataProtection.GetRequiredService<IDataProtectionProvider>());

    public ISchoolGoogleDriveService SchoolDriveService(ICurrentUserService user) =>
        new SchoolGoogleDriveService(Context, user, ScopeGuard(user), Audit(), Protector(), new NoOpTokenService());

    public ITeacherDriveMappingService MappingService(ICurrentUserService user) =>
        new TeacherDriveMappingService(Context, ScopeGuard(user), Drive, new TeacherDriveFolderGuard(Drive), Audit(), user);

    public ITeacherDriveIdentityService IdentityService(ICurrentUserService user) =>
        new TeacherDriveIdentityService(Context, user);

    public IGoogleDriveBrowserService BrowserService(ICurrentUserService user) =>
        new GoogleDriveBrowserService(IdentityService(user), MappingService(user), Drive,
            new TeacherDriveFolderGuard(Drive), Context, Audit());

    public IGoogleDriveUploadService UploadService(ICurrentUserService user) =>
        new GoogleDriveUploadService(IdentityService(user), MappingService(user), Drive,
            new TeacherDriveFolderGuard(Drive), SubmissionService(), Configuration(), Context);

    public EvidenceSubmissionService SubmissionService() => new(Context, Audit());

    public IEvidenceMatrixService MatrixService(ICurrentUserService user) =>
        new EvidenceMatrixService(Context, user, ScopeGuard(user), Audit(), SubmissionService(), Drive);

    public IEvidenceReconciliationService ReconciliationService() =>
        new EvidenceReconciliationService(Context, Drive, Audit(), SubmissionService(),
            NullLogger<EvidenceReconciliationService>.Instance);

    public SchoolScopeGuard ScopeGuard(ICurrentUserService user) =>
        new(Context, user, NullLogger<SchoolScopeGuard>.Instance);

    public AuditLogWriter Audit() =>
        new(Context, new HttpContextAccessor { HttpContext = new DefaultHttpContext() }, NullLogger<AuditLogWriter>.Instance);

    private static IConfiguration Configuration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TeacherDrive:MaxUploadBytes"] = "1048576",
            ["TeacherDrive:AllowedExtensions:0"] = ".pdf",
            ["TeacherDrive:AllowedExtensions:1"] = ".docx"
        })
        .Build();

    // ─── Test actors ──────────────────────────────────────────────────────────

    public static ICurrentUserService TeacherA(int? activeSchoolId = SchoolId) =>
        new TestCurrentUser(RoleNames.Instructor, TeacherAUserId, activeSchoolId);
    public static ICurrentUserService TeacherB() =>
        new TestCurrentUser(RoleNames.Instructor, TeacherBUserId, SchoolId);
    public static ICurrentUserService Manager(int? activeSchoolId = SchoolId) =>
        new TestCurrentUser(RoleNames.SchoolManager, ManagerUserId, activeSchoolId, hasPermissions: true);
    public static ICurrentUserService Moderator() =>
        new TestCurrentUser(RoleNames.Moderator, "USER-MODERATOR", SchoolId);
    public static ICurrentUserService Anonymous() =>
        new TestCurrentUser(RoleNames.Instructor, null, SchoolId);

    // ─── Setup helpers ────────────────────────────────────────────────────────

    public Task<SchoolGoogleDriveSettingsDto> ConnectSchoolDriveAsync(
        string? sharedDriveId = SharedDriveId,
        string rootFolderId = SchoolRootFolderId,
        bool isEnabled = true) =>
        SchoolDriveService(Manager()).ConfigureForCurrentSchoolAsync(new(
            GoogleDriveCredentialType.ServiceAccount,
            "evidence@alfalah.edu.sa",
            ServiceAccountJson,
            null,
            null, null, null,
            sharedDriveId,
            rootFolderId,
            "ملفات الإنجاز",
            isEnabled));

    public Task<DriveFolderMappingDto> GrantFolderAsync(int teacherId, string folderId) =>
        MappingService(Manager()).UpsertAsync(teacherId, new(folderId));

    /// <summary>Uploads through the real pipeline and returns the created submission id.</summary>
    public async Task<UploadFileResultDto> UploadAsync(
        ICurrentUserService user,
        int taskId,
        string fileName = "دليل.pdf",
        string? parentItemId = null,
        string requestId = "req-1",
        string content = "PDF-BYTES")
    {
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        return await UploadService(user).UploadAsync(
            new(stream, fileName, "application/pdf", stream.Length, parentItemId, taskId, requestId));
    }

    /// <summary>A structurally valid (but non-functional) service-account key for configuration tests.</summary>
    public const string ServiceAccountJson = """
        {
          "type": "service_account",
          "client_email": "evidence@alfalah-test.iam.gserviceaccount.com",
          "private_key": "-----BEGIN PRIVATE KEY-----\nMIIBOgIBAAJBALTEST\n-----END PRIVATE KEY-----\n",
          "token_uri": "https://oauth2.googleapis.com/token"
        }
        """;

    public ValueTask DisposeAsync()
    {
        _dataProtection.Dispose();
        return Context.DisposeAsync();
    }

    private static ApplicationUser NewUser(string id, string firstName, string lastName) => new()
    {
        Id = id, UserName = id, NormalizedUserName = id, FirstName = firstName, LastName = lastName
    };

    /// <summary>The mapping/settings services never mint a token in these tests — Drive is faked.</summary>
    private sealed class NoOpTokenService : IGoogleDriveTokenService
    {
        public Task<string> GetAccessTokenAsync(int schoolId, CancellationToken cancellationToken = default) =>
            Task.FromResult("fake-token");
        public void InvalidateCachedToken(int schoolId) { }
    }

    public sealed class TestCurrentUser : ICurrentUserService
    {
        private readonly string _role;
        private readonly bool _hasPermissions;

        public TestCurrentUser(string role, string? userId, int? schoolId, bool hasPermissions = false)
        {
            _role = role;
            _hasPermissions = hasPermissions;
            UserId = userId;
            ActiveSchoolId = schoolId;
        }

        public string? UserId { get; }
        public string? Username => UserId;
        public int? ActiveSchoolId { get; }
        public string? PreferredLanguage => "ar";
        public bool IsAuthenticated => UserId is not null;
        public bool IsInRole(string roleName) => _role == roleName;
        public bool HasPermission(string permissionName) => _hasPermissions;
        public IEnumerable<string> GetRoles() => [_role];
        public IEnumerable<string> GetPermissions() => [];
        public bool IsGlobalAdmin() => _role is RoleNames.SuperAdmin or RoleNames.MainManager;
        public bool IsSchoolScopedRole() => !IsGlobalAdmin();
    }
}
