using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class StudentAnalyzerRepository : IStudentAnalyzerRepository
{
    private readonly AlFalahDbContext _context;

    public StudentAnalyzerRepository(AlFalahDbContext context) => _context = context;

    public IQueryable<School> GetSchools() => _context.Schools.AsNoTracking();
    public IQueryable<ApplicationUser> GetUsers() => _context.Users.AsNoTracking();
    public IQueryable<UserSchoolRole> GetUserSchoolRoles() => _context.UserSchoolRoles.AsNoTracking();
    public IQueryable<StudentAnalyzerAccessGrant> GetGrants() => _context.StudentAnalyzerAccessGrants.AsNoTracking();
    public IQueryable<SchoolStudentAnalyzerSettings> GetSettings() => _context.SchoolStudentAnalyzerSettings.AsNoTracking();
    public IQueryable<StudentAnalyzerSourceFile> GetFiles() => _context.StudentAnalyzerSourceFiles.AsNoTracking();
    public IQueryable<StudentAnalyzerReport> GetReports() => _context.StudentAnalyzerReports.AsNoTracking();

    public Task<SchoolStudentAnalyzerSettings?> GetTrackedSettingsAsync(int schoolId, CancellationToken cancellationToken) =>
        _context.SchoolStudentAnalyzerSettings.AsTracking().FirstOrDefaultAsync(x => x.SchoolId == schoolId, cancellationToken);

    public Task<List<StudentAnalyzerAccessGrant>> GetTrackedGrantsAsync(int schoolId, CancellationToken cancellationToken) =>
        _context.StudentAnalyzerAccessGrants.AsTracking().Where(x => x.SchoolId == schoolId).ToListAsync(cancellationToken);

    public Task<StudentAnalyzerSourceFile?> GetTrackedFileAsync(int fileId, CancellationToken cancellationToken) =>
        _context.StudentAnalyzerSourceFiles.AsTracking().FirstOrDefaultAsync(x => x.Id == fileId, cancellationToken);

    public Task<StudentAnalyzerReport?> GetTrackedReportAsync(int reportId, CancellationToken cancellationToken) =>
        _context.StudentAnalyzerReports.AsTracking().FirstOrDefaultAsync(x => x.Id == reportId, cancellationToken);

    public Task<List<StudentAnalyzerReport>> GetTrackedReportsByFileAsync(int fileId, CancellationToken cancellationToken) =>
        _context.StudentAnalyzerReports.AsTracking().Where(x => x.SourceFileId == fileId).ToListAsync(cancellationToken);

    public void AddSettings(SchoolStudentAnalyzerSettings settings) => _context.SchoolStudentAnalyzerSettings.Add(settings);
    public void AddGrant(StudentAnalyzerAccessGrant grant) => _context.StudentAnalyzerAccessGrants.Add(grant);
    public void AddFile(StudentAnalyzerSourceFile file) => _context.StudentAnalyzerSourceFiles.Add(file);
    public void AddReport(StudentAnalyzerReport report) => _context.StudentAnalyzerReports.Add(report);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => _context.SaveChangesAsync(cancellationToken);
}
