using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.IntelligentTimetable;

public sealed class CoverageAccess(ITimetableSubstitutionRepository repository, ICurrentUserService user)
{
    public int School => user.IsAuthenticated && user.UserId != null && user.ActiveSchoolId is int school && !user.IsInRole(RoleNames.Guardian)
        ? school : throw new UnauthorizedAccessException();
    public string User => user.UserId ?? throw new UnauthorizedAccessException();
    public async Task<bool> CanManage(CancellationToken ct)
    {
        var school = School;
        return !user.IsInRole(RoleNames.Instructor) && (user.IsGlobalAdmin() || user.IsInRole(RoleNames.SchoolManager) ||
            user.HasPermission(PermissionNames.TimetableManage) || user.IsInRole(RoleNames.Moderator) && await repository.HasEditorGrantAsync(school, User, ct));
    }
    public async Task Manage(CancellationToken ct) { if (!await CanManage(ct)) throw new UnauthorizedAccessException(); }
    public bool CanOverride => user.IsGlobalAdmin() || user.IsInRole(RoleNames.SchoolManager) || user.IsInRole(RoleNames.Secretary) && user.HasPermission(PermissionNames.TimetableManage);
}
