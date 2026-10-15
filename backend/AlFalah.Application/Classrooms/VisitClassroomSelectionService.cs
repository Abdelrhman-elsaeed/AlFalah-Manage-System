using AlFalah.Application.Common;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;

namespace AlFalah.Application.Classrooms;

/// <summary>Resolves a new/editable observation's classroom without rewriting retained report labels.</summary>
public sealed class VisitClassroomSelectionService(IClassroomCatalogRepository repository, ICurrentUserService user)
    : IVisitClassroomSelectionService
{
    public async Task<VisitClassroomReference> ResolveAsync(int school, DateTimeOffset date, int? classroomId,
        Visit? existing, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.UserId is null || (!user.IsGlobalAdmin() && user.ActiveSchoolId != school))
            throw new UnauthorizedSchoolAccessException("الزيارة خارج نطاق المدرسة الحالية.");
        var context = await repository.ContextAsync(school, user.UserId, ct);
        if (!context.IsMember && !user.IsGlobalAdmin()) throw new UnauthorizedSchoolAccessException("حسابك غير مرتبط بهذه المدرسة.");
        if (classroomId is null or <= 0) throw new ArgumentException("اختر فصل الزيارة من سجل فصول المدرسة.");
        var year = ClassroomCatalogService.YearAt(context, DateOnly.FromDateTime(date.DateTime));
        var row = (await repository.ClassroomsAsync(school, year, ct)).SingleOrDefault(c => c.Id == classroomId && !c.IsDeleted);
        if (row is null || (!row.IsActive && existing?.ClassroomId != row.Id))
            throw new ArgumentException("الفصل غير متاح أو لا ينتمي إلى المدرسة والعام الموافقين لتاريخ الزيارة.");
        // An unchanged reference keeps the recorded label, including after a catalogue rename.
        return new(row.Id, existing?.ClassroomId == row.Id && !string.IsNullOrWhiteSpace(existing.GradeClass)
            ? existing.GradeClass : row.ClassLabel);
    }
}
