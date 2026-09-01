using System.Text.Json;
using AlFalah.Application.StudentAffairs.DTOs.Settings;
using AlFalah.Application.StudentAffairs.DTOs.Shared;
using AlFalah.Application.StudentAffairs.Settings;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using AlFalah.Infrastructure.Services;
using AlFalah.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Repositories;

public sealed class StudentAffairsSettingsRepository : IStudentAffairsSettingsRepository
{
    private readonly AlFalahDbContext _context;
    private readonly AuditLogWriter _audit;

    public StudentAffairsSettingsRepository(AlFalahDbContext context, AuditLogWriter audit)
    {
        _context = context;
        _audit = audit;
    }

    public Task<SchoolStudentAffairsSettings?> GetSettingsAsync(
        int schoolId,
        CancellationToken cancellationToken) =>
        _context.SchoolStudentAffairsSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SchoolId == schoolId, cancellationToken);

    public Task<SchoolStudentAffairsSettings?> GetSettingsForUpdateAsync(
        int schoolId,
        CancellationToken cancellationToken) =>
        _context.SchoolStudentAffairsSettings
            .AsTracking()
            .FirstOrDefaultAsync(s => s.SchoolId == schoolId, cancellationToken);

    public async Task<SchoolStudentAffairsSettingsDto?> GetSettingsDtoAsync(
        int schoolId,
        CancellationToken cancellationToken)
    {
        var settings = await _context.SchoolStudentAffairsSettings
            .AsNoTracking()
            .Where(s => s.SchoolId == schoolId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (settings is null) return null;

        return new SchoolStudentAffairsSettingsDto(
            settings.Id,
            settings.MorningDelayThresholdPerTerm,
            settings.BehaviorIncidentMultiplePerTerm,
            settings.AcademicConcernThresholdPerTerm,
            settings.ClassroomEntryPermitThresholdPerTerm,
            settings.AbsenceVisualAlertThresholdPerTerm,
            settings.AbsenceReferralThresholdPerTerm,
            settings.AbsenceChildRightsThresholdPerTerm,
            settings.BehaviorCountabilityPolicy,
            settings.ArrivalCutoffLocalTime,
            settings.ArrivalGraceMinutes,
            settings.Version,
            settings.EffectiveFrom,
            false,
            Convert.ToBase64String(settings.RowVersion));
    }

    public async Task<PagedResult<StudentAffairsSettingsHistoryDto>> GetHistoryAsync(
        int schoolId,
        StudentAffairsPageQuery query,
        CancellationToken cancellationToken)
    {
        var source = _context.AuditLogs
            .AsNoTracking()
            .Where(log => log.SchoolId == schoolId
                && (log.EntityName == "SchoolStudentAffairsSettings" || log.Action.StartsWith("StudentAffairs.Settings")));

        var total = await source.CountAsync(cancellationToken).ConfigureAwait(false);
        var page = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var auditLogs = await source
            .OrderByDescending(log => log.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var userIds = auditLogs
            .Where(l => !string.IsNullOrEmpty(l.UserId))
            .Select(l => l.UserId!)
            .Distinct()
            .ToList();

        var users = await _context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(
                u => u.Id,
                u => $"{u.FirstName} {u.LastName}".Trim(),
                cancellationToken)
            .ConfigureAwait(false);

        var items = new List<StudentAffairsSettingsHistoryDto>(auditLogs.Count);
        foreach (var log in auditLogs)
        {
            var displayName = log.UserId != null && users.TryGetValue(log.UserId, out var name)
                ? name
                : "النظام";

            var actor = new ActorSummaryDto(
                log.UserId ?? string.Empty,
                displayName,
                RoleNames.StudentAffairsOfficer);

            var settingsDto = ParseSettingsFromAudit(log);

            items.Add(new StudentAffairsSettingsHistoryDto(
                settingsDto.EffectiveVersion,
                settingsDto,
                actor,
                log.Reason ?? string.Empty,
                log.CreatedAt));
        }

        return new PagedResult<StudentAffairsSettingsHistoryDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public void AddSettings(SchoolStudentAffairsSettings settings) =>
        _context.SchoolStudentAffairsSettings.Add(settings);

    public void SetExpectedRowVersion(SchoolStudentAffairsSettings settings, byte[] rowVersion) =>
        _context.Entry(settings).Property(entity => entity.RowVersion).OriginalValue = rowVersion;

    public void WriteAudit(
        int schoolId,
        string userId,
        string action,
        string? entityId,
        string? reason,
        object? oldValues,
        object? newValues) =>
        _audit.Write(schoolId, userId, action, "SchoolStudentAffairsSettings", entityId, reason, oldValues, newValues);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        _context.SaveChangesAsync(cancellationToken);

    private static SchoolStudentAffairsSettingsDto ParseSettingsFromAudit(Domain.Entities.AuditLog log)
    {
        if (!string.IsNullOrWhiteSpace(log.NewValues))
        {
            try
            {
                using var doc = JsonDocument.Parse(log.NewValues);
                var root = doc.RootElement;
                int? id = root.TryGetProperty("Id", out var idProp) && idProp.ValueKind == JsonValueKind.Number
                    ? idProp.GetInt32()
                    : null;
                int delay = root.TryGetProperty("MorningDelayThresholdPerTerm", out var delayProp)
                    ? delayProp.GetInt32() : 10;
                int behavior = root.TryGetProperty("BehaviorIncidentMultiplePerTerm", out var bhProp)
                    ? bhProp.GetInt32() : 10;
                int academic = root.TryGetProperty("AcademicConcernThresholdPerTerm", out var acProp)
                    ? acProp.GetInt32() : 3;
                int permit = root.TryGetProperty("ClassroomEntryPermitThresholdPerTerm", out var cpProp)
                    ? cpProp.GetInt32() : 5;
                int visual = root.TryGetProperty("AbsenceVisualAlertThresholdPerTerm", out var visProp)
                    ? visProp.GetInt32() : 3;
                int referral = root.TryGetProperty("AbsenceReferralThresholdPerTerm", out var refProp)
                    ? refProp.GetInt32() : 5;
                int childRights = root.TryGetProperty("AbsenceChildRightsThresholdPerTerm", out var crProp)
                    ? crProp.GetInt32() : 10;
                string policy = root.TryGetProperty("BehaviorCountabilityPolicy", out var polProp)
                    ? polProp.GetString() ?? "all-upheld" : "all-upheld";
                TimeOnly cutoff = root.TryGetProperty("ArrivalCutoffLocalTime", out var cutProp)
                    && TimeOnly.TryParse(cutProp.GetString(), out var parsedCut)
                    ? parsedCut : new TimeOnly(7, 0);
                int grace = root.TryGetProperty("ArrivalGraceMinutes", out var grcProp)
                    ? grcProp.GetInt32() : 0;
                int version = root.TryGetProperty("Version", out var verProp)
                    ? verProp.GetInt32()
                    : (root.TryGetProperty("EffectiveVersion", out var effVerProp) ? effVerProp.GetInt32() : 1);
                DateTimeOffset effectiveFrom = root.TryGetProperty("EffectiveFrom", out var effFromProp)
                    && DateTimeOffset.TryParse(effFromProp.GetString(), out var parsedEff)
                    ? parsedEff : log.CreatedAt;
                bool usesDefaults = root.TryGetProperty("UsesLockedDefaults", out var defProp)
                    ? defProp.GetBoolean() : (log.Action.EndsWith("Reset", StringComparison.OrdinalIgnoreCase));

                return new SchoolStudentAffairsSettingsDto(
                    id,
                    delay,
                    behavior,
                    academic,
                    permit,
                    visual,
                    referral,
                    childRights,
                    policy,
                    cutoff,
                    grace,
                    version,
                    effectiveFrom,
                    usesDefaults,
                    string.Empty);
            }
            catch
            {
                // fallback on parse failure
            }
        }

        return new SchoolStudentAffairsSettingsDto(
            null,
            10,
            10,
            3,
            5,
            3,
            5,
            10,
            "all-upheld",
            new TimeOnly(7, 0),
            0,
            1,
            log.CreatedAt,
            true,
            string.Empty);
    }
}
