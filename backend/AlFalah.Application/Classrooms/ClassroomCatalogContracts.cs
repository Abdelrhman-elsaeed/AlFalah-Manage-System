using AlFalah.Domain.Entities;
using AlFalah.Domain.Entities.StudentAffairs;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.Classrooms;

public sealed record CatalogYear(int Id, string Name, bool IsActive, DateOnly? StartsOn = null, DateOnly? EndsOn = null);
public sealed record CatalogContext(SchoolStage Stage, bool IsManager, bool IsMember, IReadOnlyList<CatalogYear> Years);
public sealed record ClassroomOption(int Id, string Label, SchoolStage Stage, int GradeLevel, string Section,
    int? SectionNumber, int AcademicYearId, bool IsActive, string PhysicalLocation, string Revision);
public sealed record ClassroomOptionsPage(CatalogContext Context, IReadOnlyList<ClassroomOption> Items, bool HasMore);
public sealed record ClassroomBatch(int AcademicYearId, int GradeLevel, IReadOnlyList<string> Sections, string PhysicalLocation);
public sealed record ClassroomEdit(string Section, bool IsActive, string PhysicalLocation, string Revision);

public interface IClassroomCatalogRepository
{
    Task<CatalogContext> ContextAsync(int school, string actor, CancellationToken ct);
    Task<IReadOnlyList<Classroom>> ClassroomsAsync(int school, int year, CancellationToken ct);
    Task<IReadOnlyList<Classroom>> PageAsync(int school, int year, int page, bool includeInactive, CancellationToken ct);
    Task<IReadOnlyList<InstructorClass>> AssociationsAsync(int teacher, CancellationToken ct);
    Task AddAsync(IReadOnlyList<Classroom> classrooms, CancellationToken ct);
    Task<Classroom?> ForUpdateAsync(int school, int id, CancellationToken ct);
    Task<IReadOnlyList<ClassroomLabelHistory>> LabelsAsync(int school, int year, CancellationToken ct);
    void AddLabel(ClassroomLabelHistory label);
    Task SaveAsync(CancellationToken ct);
    void AddAssociation(InstructorClass association);
    void Audit(AuditLog row);
}

public interface IClassroomCatalogService
{
    Task<ClassroomOptionsPage> OptionsAsync(int? year, int page, bool includeInactive, CancellationToken ct, DateOnly? onDate = null);
    Task<IReadOnlyList<ClassroomOption>> CreateAsync(ClassroomBatch batch, CancellationToken ct);
    Task<ClassroomOption> UpdateAsync(int id, ClassroomEdit edit, CancellationToken ct);
    Task ApplyTeacherSelectionAsync(InstructorProfile profile, IReadOnlyList<int>? ids, IReadOnlyList<string>? labels,
        int? year, int? revision, CancellationToken ct);
}

public sealed record VisitClassroomReference(int Id, string Label);
public interface IVisitClassroomSelectionService
{
    Task<VisitClassroomReference> ResolveAsync(int school, DateTimeOffset date, int? classroomId,
        Visit? existing, CancellationToken ct);
}
