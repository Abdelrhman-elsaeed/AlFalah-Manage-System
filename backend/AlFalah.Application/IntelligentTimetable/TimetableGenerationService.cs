using AlFalah.Application.DTOs.Timetables;
using AlFalah.Application.IntelligentTimetable.Handlers;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.IntelligentTimetable;

public sealed class TimetableGenerationService(
    ITimetableReviewRepository repository,
    TimetableGenerationEngine engine,
    ICurrentUserService currentUser)
{
    public async Task<IReadOnlyList<TimetableEntryDto>> GenerateAsync(
        int timetableId,
        int expectedRevision,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId) || currentUser.ActiveSchoolId is null ||
            TimetableSettingsHandlerSupport.IsExcludedRole(currentUser) ||
            !(currentUser.HasPermission(PermissionNames.TimetableManage) || currentUser.IsGlobalAdmin() || currentUser.IsInRole(RoleNames.SchoolManager)))
            throw new UnauthorizedAccessException(TimetableSettingsHandlerSupport.PermissionDenied);

        var context = await repository.GetValidationContextAsync(currentUser.ActiveSchoolId.Value, timetableId, cancellationToken)
            ?? throw new KeyNotFoundException("الجدول غير موجود.");
        if (context.Timetable.Revision != expectedRevision)
            throw new BellScheduleConflictException("Stale timetable");
        return engine.Generate(context, cancellationToken);
    }
}
