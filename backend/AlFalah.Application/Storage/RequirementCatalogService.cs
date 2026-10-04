using AlFalah.Domain.Entities.Storage;
using AlFalah.Application.DTOs.EvidenceMatrix;
using AlFalah.Domain.Enums;

namespace AlFalah.Application.Storage;

public sealed class RequirementCatalogService(IEvidenceRepository repository, IStorageRepository transactions,
    EvidenceWorkflowContext context) : IRequirementCatalogService
{
    public async Task<IReadOnlyList<AcademicYearDto>> YearsAsync(CancellationToken ct = default)
    {
        await context.ReadAsync(ct); return await repository.YearsAsync(ct);
    }
    public async Task<IReadOnlyList<EvidenceTeacherDto>> TeachersAsync(CancellationToken ct = default)
    {
        await context.SchoolPermissionAsync(PermissionNames.StorageViewSchool, ct); return await repository.TeachersAsync(context.School, ct);
    }
    public static readonly (string Code, string Name)[] Standards =
    [ ("1.1", "التخطيط"), ("1.2", "قيادة العملية التعليمية"), ("1.3", "المجتمع المدرسي"), ("1.4", "التطوير المؤسسي"),
      ("1.5", "حقوق المتعلم وحمايته"), ("2.1", "بناء خبرات التعلم"), ("2.2", "تقويم التعلم"),
      ("3.1", "التحصيل التعليمي"), ("3.2", "التطور الشخصي والصحي"), ("4.1", "المبنى المدرسي"), ("4.2", "الأمن والسلامة") ];
    public async Task<IReadOnlyList<RequirementDto>> ListAsync(int year, string? search = null, CancellationToken ct = default)
    {
        await context.ReadAsync(ct); await context.YearAsync(year, ct);
        if (search?.Length > 200) throw new ArgumentException("البحث طويل.");
        var rows = await repository.RequirementsAsync(context.School, year, ct);
        return rows.Where(x => x.IsActive && (string.IsNullOrWhiteSpace(search) || x.DisplayName.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) || x.Code.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)))
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id).Select(Dto).ToArray();
    }
    public async Task<IReadOnlyList<RequirementDto>> InitializeAsync(int year, CancellationToken ct = default)
    {
        await context.ManageAsync(ct); await context.YearAsync(year, ct);
        return await transactions.InSerializableTransactionAsync(async () =>
        {
            var existing = await repository.RequirementsAsync(context.School, year, ct);
            var rows = Standards.Where(s => !existing.Any(x => x.Code == "STD-" + s.Code)).Select((s, i) => new EvidenceRequirement {
                SchoolId = context.School, AcademicYearId = year, Code = "STD-" + s.Code, DisplayName = s.Name,
                DomainCode = s.Code[..1], StandardCode = s.Code, SortOrder = i }).ToList();
            foreach (var task in await repository.TasksAsync(ct))
            {
                if (existing.Any(x => x.OriginalTaskId == task.Id)) continue;
                if (existing.Any(x => x.Code == task.Code)) throw new StorageConflictException();
                // Exact task identity; S0 semantic standard suggestions are not silently adopted.
                rows.Add(new() { SchoolId = context.School, AcademicYearId = year, Code = task.Code,
                    DisplayName = task.NameAr, OriginalTaskId = task.Id, SortOrder = 100 + task.SortOrder });
            }
            await repository.AddRequirementsAsync(rows, ct);
            return await ListAsync(year, ct: ct);
        }, ct);
    }
    public async Task<RequirementDto> ConfigureAsync(int id, ConfigureRequirementRequest request, CancellationToken ct = default)
    {
        await context.ManageAsync(ct);
        var row = await repository.RequirementAsync(context.School, id, ct) ?? throw new KeyNotFoundException();
        if (request.StandardCode != null && !Standards.Any(x => x.Code == request.StandardCode) ||
            request.DomainCode != null && request.DomainCode is not ("1" or "2" or "3" or "4") ||
            request.StandardCode != null && request.DomainCode != request.StandardCode[..1] ||
            !Enum.IsDefined(request.Importance) || !Enum.IsDefined(request.FulfillmentPolicy) || request.MinimumApprovedLinks is < 1 or > 100 || request.ResponsibleRole?.Length > 100)
            throw new ArgumentException("إعداد المتطلب غير صالح.");
        // Validate the selected identity in this school's live membership, independently of the caller.
        if (request.ResponsibleUserId != null && (await transactions.GetActorScopeAsync(request.ResponsibleUserId, context.School, ct)) is not { IsMember: true })
            throw new ArgumentException("المسؤول ليس عضوًا نشطًا في المدرسة.");
        var expected = EvidenceWorkflowContext.Expected(request.RowVersion, row.RowVersion);
        row.DomainCode = request.DomainCode; row.StandardCode = request.StandardCode; row.Importance = request.Importance;
        row.ResponsibleUserId = request.ResponsibleUserId; row.ResponsibleRole = request.ResponsibleRole;
        row.FulfillmentPolicy = request.FulfillmentPolicy; row.MinimumApprovedLinks = request.MinimumApprovedLinks;
        await repository.SaveAsync(context.Actor, context.School, "Storage.RequirementConfigured", id.ToString(), row, expected, ct);
        return Dto(row);
    }
    private static RequirementDto Dto(EvidenceRequirement x) => new(x.Id, x.AcademicYearId!.Value, x.Code, x.DisplayName,
        x.DomainCode, x.StandardCode, x.OriginalTaskId, x.Importance.ToString(), x.ResponsibleUserId, x.ResponsibleRole,
        x.FulfillmentPolicy.ToString(), x.MinimumApprovedLinks, Convert.ToBase64String(x.RowVersion));
}
