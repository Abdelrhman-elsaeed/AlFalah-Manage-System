using AlFalah.Application.Common;
using AlFalah.Application.DTOs.Attendance;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;
using AlFalah.Domain.Enums;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AlFalah.Infrastructure.Services;

/// <summary>
/// School-staff attendance. The secretary can record one complete daily sheet;
/// every other staff member can read only their own history.
/// </summary>
public class AttendanceService : IAttendanceService
{
    private static readonly string[] AttendedStaffRoles =
    {
        RoleNames.SchoolManager, RoleNames.Moderator, RoleNames.Instructor
    };

    private readonly AlFalahDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly SchoolScopeGuard _scopeGuard;

    public AttendanceService(
        AlFalahDbContext context,
        ICurrentUserService currentUser,
        SchoolScopeGuard scopeGuard)
    {
        _context = context;
        _currentUser = currentUser;
        _scopeGuard = scopeGuard;
    }

    public async Task<AttendanceSheetDto> GetSheetAsync(
        DateOnly date,
        int? requestedSchoolId,
        CancellationToken cancellationToken = default)
    {
        EnsureSchoolWorkDay(date);
        var schoolId = ResolveSchoolId(requestedSchoolId);
        await EnsureManageAccessAsync(schoolId, cancellationToken);
        return await BuildSheetAsync(schoolId, date, cancellationToken);
    }

    public async Task<AttendanceSheetDto> SaveSheetAsync(
        SaveAttendanceSheetRequestDto request,
        CancellationToken cancellationToken = default)
    {
        EnsureSchoolWorkDay(request.Date);
        var schoolId = ResolveSchoolId(request.SchoolId);
        await EnsureManageAccessAsync(schoolId, cancellationToken);

        var roster = await GetRosterAsync(schoolId, cancellationToken);
        var rosterUserIds = roster.Select(x => x.UserId).ToHashSet(StringComparer.Ordinal);
        var submittedUserIds = request.Entries.Select(x => x.UserId).ToHashSet(StringComparer.Ordinal);

        if (submittedUserIds.Count != request.Entries.Count)
            throw new ArgumentException("لا يمكن تسجيل نفس الموظف أكثر من مرة في نفس اليوم.");
        if (!submittedUserIds.SetEquals(rosterUserIds))
            throw new ArgumentException("يجب حفظ الحضور لكافة موظفي المدرسة الحاليين.");

        var currentUserId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("يجب تسجيل الدخول أولاً.");
        var existingRecords = await _context.AttendanceRecords
            .Where(x => x.SchoolId == schoolId && x.AttendanceDate == request.Date)
            .ToDictionaryAsync(x => x.UserId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var entry in request.Entries)
        {
            if (existingRecords.TryGetValue(entry.UserId, out var existing))
            {
                existing.Status = entry.Status;
                existing.Notes = NormalizeNotes(entry.Notes);
                existing.RecordedByUserId = currentUserId;
                existing.UpdatedAt = now;
                continue;
            }

            _context.AttendanceRecords.Add(new AttendanceRecord
            {
                SchoolId = schoolId,
                UserId = entry.UserId,
                AttendanceDate = request.Date,
                Status = entry.Status,
                Notes = NormalizeNotes(entry.Notes),
                RecordedByUserId = currentUserId,
                RecordedAt = now,
                UpdatedAt = now
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        return await BuildSheetAsync(schoolId, request.Date, cancellationToken);
    }

    public async Task<IReadOnlyList<MyAttendanceItemDto>> GetMyAttendanceAsync(
        DateOnly? fromDate,
        DateOnly? toDate,
        int? requestedSchoolId,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.HasPermission(PermissionNames.AttendanceView))
            throw new UnauthorizedAccessException("ليس لديك صلاحية عرض الحضور.");

        if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
            throw new ArgumentException("تاريخ البداية يجب أن يكون قبل تاريخ النهاية.");

        var userId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("يجب تسجيل الدخول أولاً.");
        var schoolId = ResolveSchoolId(requestedSchoolId);

        var query = _context.AttendanceRecords.AsNoTracking()
            .Where(x => x.UserId == userId && x.SchoolId == schoolId);
        if (fromDate.HasValue) query = query.Where(x => x.AttendanceDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(x => x.AttendanceDate <= toDate.Value);

        return await query
            .OrderByDescending(x => x.AttendanceDate)
            .Select(x => new MyAttendanceItemDto(x.AttendanceDate, x.Status, x.Notes, x.RecordedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AttendanceRecordItemDto>> GetRecordsAsync(
        DateOnly? fromDate,
        DateOnly? toDate,
        string? name,
        int? requestedSchoolId,
        CancellationToken cancellationToken = default)
    {
        if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
            throw new ArgumentException("ØªØ§Ø±ÙŠØ® Ø§Ù„Ø¨Ø¯Ø§ÙŠØ© ÙŠØ¬Ø¨ Ø£Ù† ÙŠÙƒÙˆÙ† Ù‚Ø¨Ù„ ØªØ§Ø±ÙŠØ® Ø§Ù„Ù†Ù‡Ø§ÙŠØ©.");

        var schoolId = ResolveSchoolId(requestedSchoolId);
        await EnsureManageAccessAsync(schoolId, cancellationToken);

        var query = from record in _context.AttendanceRecords.AsNoTracking()
                    join user in _context.Users.AsNoTracking() on record.UserId equals user.Id
                    join assignment in _context.UserSchoolRoles.AsNoTracking() on new { record.UserId, record.SchoolId }
                        equals new { UserId = assignment.UserId, assignment.SchoolId }
                    join role in _context.Roles.AsNoTracking() on assignment.RoleId equals role.Id
                    where record.SchoolId == schoolId && assignment.IsActive && AttendedStaffRoles.Contains(role.Name!)
                    select new { record, user, role };

        if (fromDate.HasValue) query = query.Where(x => x.record.AttendanceDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(x => x.record.AttendanceDate <= toDate.Value);
        if (!string.IsNullOrWhiteSpace(name))
        {
            var term = name.Trim();
            query = query.Where(x => (x.user.FirstName + " " + x.user.LastName).Contains(term));
        }

        return await query
            .OrderByDescending(x => x.record.AttendanceDate)
            .ThenBy(x => x.user.FirstName)
            .Select(x => new AttendanceRecordItemDto(
                x.record.UserId,
                x.user.FirstName + " " + x.user.LastName,
                x.role.Name!,
                x.record.AttendanceDate,
                x.record.Status,
                x.record.Notes,
                x.record.RecordedAt))
            .ToListAsync(cancellationToken);
    }

    private async Task<AttendanceSheetDto> BuildSheetAsync(int schoolId, DateOnly date, CancellationToken cancellationToken)
    {
        var roster = await GetRosterAsync(schoolId, cancellationToken);
        var records = await _context.AttendanceRecords.AsNoTracking()
            .Where(x => x.SchoolId == schoolId && x.AttendanceDate == date)
            .ToDictionaryAsync(x => x.UserId, cancellationToken);

        var rows = roster.Select(staff =>
        {
            records.TryGetValue(staff.UserId, out var record);
            return new AttendanceSheetRowDto(
                staff.UserId,
                staff.FullName,
                staff.Role,
                record?.Status,
                record?.Notes,
                record?.RecordedAt);
        }).ToList();

        return new AttendanceSheetDto(date, rows);
    }

    private async Task<List<StaffRosterItem>> GetRosterAsync(int schoolId, CancellationToken cancellationToken)
    {
        var assignments = await (
            from assignment in _context.UserSchoolRoles.AsNoTracking()
            join role in _context.Roles.AsNoTracking() on assignment.RoleId equals role.Id
            join user in _context.Users.AsNoTracking() on assignment.UserId equals user.Id
            where assignment.SchoolId == schoolId
                && assignment.IsActive
                && user.IsActive
                && AttendedStaffRoles.Contains(role.Name!)
            select new StaffRosterItem(assignment.UserId, user.FirstName + " " + user.LastName, role.Name!))
            .ToListAsync(cancellationToken);

        return assignments
            .GroupBy(x => x.UserId)
            .Select(group => group
                .OrderBy(x => RolePriority(x.Role))
                .ThenBy(x => x.FullName)
                .First())
            .OrderBy(x => RolePriority(x.Role))
            .ThenBy(x => x.FullName)
            .ToList();
    }

    private int ResolveSchoolId(int? requestedSchoolId)
    {
        var resolvedSchoolId = _scopeGuard.ResolveAllowedSchoolId(requestedSchoolId);
        if (resolvedSchoolId.HasValue) return resolvedSchoolId.Value;
        throw new ArgumentException("يجب تحديد المدرسة لمستخدم الدخول العام.");
    }

    private async Task EnsureManageAccessAsync(int schoolId, CancellationToken cancellationToken)
    {
        if (!_currentUser.HasPermission(PermissionNames.AttendanceManage))
            throw new UnauthorizedAccessException("إدارة الحضور متاحة للسكرتير المخوّل فقط.");

        if (_currentUser.IsGlobalAdmin()) return;

        var userId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("يجب تسجيل الدخول أولاً.");
        var isActiveSecretary = await (
            from assignment in _context.UserSchoolRoles.AsNoTracking()
            join role in _context.Roles.AsNoTracking() on assignment.RoleId equals role.Id
            where assignment.UserId == userId
                && assignment.SchoolId == schoolId
                && assignment.IsActive
                && role.Name == RoleNames.Secretary
            select assignment.Id)
            .AnyAsync(cancellationToken);

        if (!isActiveSecretary)
            throw new UnauthorizedAccessException("إدارة الحضور متاحة للسكرتير المُعيّن حالياً في هذه المدرسة فقط.");
    }

    private static void EnsureSchoolWorkDay(DateOnly date)
    {
        if (date.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday)
            throw new ArgumentException("الحضور يُسجّل في أيام العمل من الأحد إلى الخميس فقط.");
    }

    private static string? NormalizeNotes(string? notes) =>
        string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

    private static int RolePriority(string role) => role switch
    {
        RoleNames.SchoolManager => 0,
        RoleNames.Moderator => 1,
        RoleNames.Instructor => 2,
        _ => 99
    };

    private sealed record StaffRosterItem(string UserId, string FullName, string Role);
}
