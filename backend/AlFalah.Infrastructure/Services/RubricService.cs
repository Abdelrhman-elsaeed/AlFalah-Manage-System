using AlFalah.Application.DTOs.Rubric;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AlFalah.Infrastructure.Services;

/// <summary>
/// Rubric management service (Phase 3).
///
/// Key invariants:
///  - Only ONE active version at a time (filtered unique DB index + service logic).
///  - Copy-on-write: creating a new version clones the FULL tree as new rows with new Ids.
///    Historical rows are NEVER mutated so old visits retain accuracy.
///  - Rubric is GLOBAL — not school-scoped (D-21).
///  - Score-scale values are compile-time constants matching docs/09 verbatim (MOD-5).
/// </summary>
public class RubricService : IRubricService
{
    private readonly AlFalahDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<RubricService> _logger;

    public RubricService(
        AlFalahDbContext context,
        ICurrentUserService currentUser,
        ILogger<RubricService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    // ─── Queries ──────────────────────────────────────────────────────────────

    public async Task<RubricVersionDto> GetActiveVersionAsync(CancellationToken cancellationToken = default)
    {
        var version = await _context.RubricVersions
            .AsNoTracking()
            .Include(v => v.Domains.OrderBy(d => d.SortOrder))
                .ThenInclude(d => d.Standards.OrderBy(s => s.SortOrder))
            .FirstOrDefaultAsync(v => v.IsActive, cancellationToken);

        if (version == null)
            throw new KeyNotFoundException("لا يوجد إصدار نشط من أداة التقييم.");

        return MapToVersionDto(version);
    }

    public async Task<List<RubricVersionListDto>> GetVersionsAsync(CancellationToken cancellationToken = default)
    {
        var versions = await _context.RubricVersions
            .AsNoTracking()
            .Include(v => v.Domains)
                .ThenInclude(d => d.Standards)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(cancellationToken);

        return versions.Select(v => new RubricVersionListDto
        {
            Id = v.Id,
            VersionNumber = v.VersionNumber,
            IsActive = v.IsActive,
            CreatedAt = v.CreatedAt,
            Notes = v.Notes,
            DomainCount = v.Domains.Count,
            StandardCount = v.Domains.Sum(d => d.Standards.Count)
        }).ToList();
    }

    public async Task<RubricVersionDto> GetVersionByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var version = await _context.RubricVersions
            .AsNoTracking()
            .Include(v => v.Domains.OrderBy(d => d.SortOrder))
                .ThenInclude(d => d.Standards.OrderBy(s => s.SortOrder))
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

        if (version == null)
            throw new KeyNotFoundException($"إصدار أداة التقييم رقم {id} غير موجود.");

        return MapToVersionDto(version);
    }

    // ─── Commands ─────────────────────────────────────────────────────────────

    /// <summary>
    /// MOD-4: Copy-on-write. Creates brand-new rows for every domain and standard.
    /// The previous active version is deactivated BEFORE the new one is saved,
    /// so the filtered unique index is never violated.
    /// Historical rows are never re-parented or mutated.
    /// </summary>
    public async Task<RubricVersionDto> CreateNewVersionAsync(
        CreateRubricVersionDto request,
        CancellationToken cancellationToken = default)
    {
        // Determine next version number
        var maxVersion = await _context.RubricVersions
            .IgnoreQueryFilters() // include soft-deleted so the number keeps incrementing
            .MaxAsync(v => (int?)v.VersionNumber, cancellationToken) ?? 0;

        // Deactivate all currently active versions (service-level safety; DB index is the real guard)
        var activeVersions = await _context.RubricVersions
            .Where(v => v.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var av in activeVersions)
            av.IsActive = false;

        // Build the new version (new Id = 0 → EF assigns)
        var newVersion = new RubricVersion
        {
            VersionNumber = maxVersion + 1,
            IsActive = true,
            Notes = request.Notes,
            CreatedByUserId = _currentUser.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Clone domains + standards as new rows (copy-on-write)
        foreach (var domainDto in request.Domains.OrderBy(d => d.SortOrder))
        {
            var domain = new RubricDomain
            {
                Code = domainDto.Code,
                NameAr = domainDto.NameAr,
                SortOrder = domainDto.SortOrder
            };

            foreach (var stdDto in domainDto.Standards.OrderBy(s => s.SortOrder))
            {
                domain.Standards.Add(new RubricStandard
                {
                    Code = stdDto.Code,
                    TextAr = stdDto.TextAr,
                    SortOrder = stdDto.SortOrder
                });
            }

            newVersion.Domains.Add(domain);
        }

        _context.RubricVersions.Add(newVersion);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "New RubricVersion {VersionNumber} (Id={Id}) created by {UserId}.",
            newVersion.VersionNumber, newVersion.Id, _currentUser.UserId);

        // Reload with full navigation for the response
        return await GetVersionByIdAsync(newVersion.Id, cancellationToken);
    }

    public async Task<RubricVersionDto> ActivateVersionAsync(int id, CancellationToken cancellationToken = default)
    {
        var target = await _context.RubricVersions
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

        if (target == null)
            throw new KeyNotFoundException($"إصدار أداة التقييم رقم {id} غير موجود.");

        if (target.IsActive)
            return await GetVersionByIdAsync(id, cancellationToken); // already active — idempotent

        // Deactivate all others
        var activeVersions = await _context.RubricVersions
            .Where(v => v.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var av in activeVersions)
            av.IsActive = false;

        target.IsActive = true;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "RubricVersion {VersionNumber} (Id={Id}) activated by {UserId}.",
            target.VersionNumber, target.Id, _currentUser.UserId);

        return await GetVersionByIdAsync(id, cancellationToken);
    }

    // ─── Score scale (compile-time constants, MOD-5) ──────────────────────────

    /// <summary>
    /// Returns the global score scale exactly as documented in docs/09-RUBRIC-AND-EVALUATION.md.
    /// Arabic labels and thresholds are verbatim — any change here breaks Phase 4 analysis.
    /// </summary>
    public ScoreScaleDto GetScoreScale() => new()
    {
        Scores = new List<ScoreScaleEntryDto>
        {
            new() { Score = 0, LabelAr = "غير مشاهد" },
            new() { Score = 1, LabelAr = "يحتاج تحسين" },
            new() { Score = 2, LabelAr = "متحقق جزئياً" },
            new() { Score = 3, LabelAr = "متحقق بدرجة جيدة" },
            new() { Score = 4, LabelAr = "متميز" }
        },
        PerformanceLevels = new List<PerformanceLevelDto>
        {
            // Ordered highest → lowest, matching docs/09 table
            new() { LabelAr = "متميز",          MinScore = 3.5m, IsLessThan = false },
            new() { LabelAr = "جيد جداً",        MinScore = 3.0m, IsLessThan = false },
            new() { LabelAr = "جيد",             MinScore = 2.5m, IsLessThan = false },
            new() { LabelAr = "متحقق جزئياً",    MinScore = 2.0m, IsLessThan = false },
            new() { LabelAr = "يحتاج تحسين",     MinScore = 1.0m, IsLessThan = false },
            new() { LabelAr = "غير مشاهد",       MinScore = 1.0m, IsLessThan = true  }
        }
    };

    // ─── Mapper ───────────────────────────────────────────────────────────────

    private static RubricVersionDto MapToVersionDto(RubricVersion v) => new()
    {
        Id = v.Id,
        VersionNumber = v.VersionNumber,
        IsActive = v.IsActive,
        CreatedAt = v.CreatedAt,
        Notes = v.Notes,
        Domains = v.Domains
            .OrderBy(d => d.SortOrder)
            .Select(d => new RubricDomainDto
            {
                Id = d.Id,
                Code = d.Code,
                NameAr = d.NameAr,
                SortOrder = d.SortOrder,
                Standards = d.Standards
                    .OrderBy(s => s.SortOrder)
                    .Select(s => new RubricStandardDto
                    {
                        Id = s.Id,
                        Code = s.Code,
                        TextAr = s.TextAr,
                        SortOrder = s.SortOrder
                    }).ToList()
            }).ToList()
    };
}
