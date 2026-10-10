using AlFalah.Application.DTOs.Visits;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Entities;

namespace AlFalah.Infrastructure.Services;

public sealed class VisitFeedbackBankService(
    IVisitFeedbackBankRepository repository,
    ICurrentUserService currentUser) : IVisitFeedbackBankService
{
    private static readonly string[] DefaultStrengths =
    [
        "أظهر المعلم تخطيطًا واضحًا للدرس وربط أنشطته بنواتج التعلم.",
        "نوّع المعلم أساليب الشرح بما راعى الفروق الفردية بين المتعلمين.",
        "أدار المعلم وقت الحصة بفاعلية ووفّر فرصًا مناسبة لمشاركة المتعلمين.",
        "وظّف المعلم أسئلة محفزة للتفكير وتابع إجابات المتعلمين بتغذية راجعة واضحة.",
        "استخدم المعلم وسائل ومصادر تعلم مناسبة دعمت فهم المتعلمين.",
        "هيّأ المعلم بيئة صفية آمنة ومحفزة للتفاعل والتعلم.",
        "تحقق المعلم من فهم المتعلمين بأكثر من أسلوب أثناء الحصة."
    ];

    private static readonly string[] DefaultImprovements =
    [
        "يُوصى بربط أنشطة الدرس بنواتج تعلم محددة وقابلة للقياس.",
        "يُوصى بتنويع استراتيجيات التدريس لزيادة مشاركة جميع المتعلمين.",
        "يُوصى بتخصيص وقت كافٍ للتقويم البنائي والتأكد من تحقق التعلم.",
        "يُوصى بتقديم تغذية راجعة محددة تساعد المتعلمين على تحسين أدائهم.",
        "يُوصى بتوظيف مصادر تعلم إضافية تراعي الفروق الفردية.",
        "يُوصى بتوسيع فرص الحوار والأسئلة التي تنمي التفكير الناقد.",
        "يُوصى بتحسين إدارة وقت الحصة والانتقال بين الأنشطة بسلاسة."
    ];

    public async Task<IReadOnlyList<VisitFeedbackTemplateDto>> ListAsync(CancellationToken cancellationToken)
    {
        var schoolId = SchoolId();
        if (!await repository.HasAnyIncludingDeletedAsync(schoolId, cancellationToken))
        {
            await repository.AddRangeAsync(
                DefaultStrengths.Select(text => NewTemplate(schoolId, 1, text))
                    .Concat(DefaultImprovements.Select(text => NewTemplate(schoolId, 2, text))), cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
        }
        return (await repository.ListAsync(schoolId, cancellationToken)).Select(Map).ToArray();
    }

    public async Task<VisitFeedbackTemplateDto> CreateAsync(SaveVisitFeedbackTemplateDto request, CancellationToken cancellationToken)
    {
        var schoolId = SchoolId();
        var text = Validate(request);
        await EnsureUniqueAsync(schoolId, request.Kind, text, null, cancellationToken);
        var item = NewTemplate(schoolId, request.Kind, text);
        await repository.AddRangeAsync([item], cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(item);
    }

    public async Task<VisitFeedbackTemplateDto> UpdateAsync(int id, SaveVisitFeedbackTemplateDto request, CancellationToken cancellationToken)
    {
        var schoolId = SchoolId();
        var text = Validate(request);
        var item = await repository.GetAsync(schoolId, id, cancellationToken)
            ?? throw new KeyNotFoundException("العبارة غير موجودة في بنك هذه المدرسة.");
        await EnsureUniqueAsync(schoolId, request.Kind, text, id, cancellationToken);
        item.Kind = request.Kind;
        item.Text = text;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);
        return Map(item);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var item = await repository.GetAsync(SchoolId(), id, cancellationToken)
            ?? throw new KeyNotFoundException("العبارة غير موجودة في بنك هذه المدرسة.");
        item.IsDeleted = true;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureUniqueAsync(int schoolId, int kind, string text, int? excludingId, CancellationToken cancellationToken)
    {
        if ((await repository.ListAsync(schoolId, cancellationToken)).Any(x =>
                x.Id != excludingId && x.Kind == kind && string.Equals(x.Text, text, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("هذه العبارة موجودة بالفعل في بنك المدرسة.");
    }

    private int SchoolId() => currentUser.ActiveSchoolId
        ?? throw new InvalidOperationException("اختر مدرسة قبل إدارة بنك الزيارات.");

    private static string Validate(SaveVisitFeedbackTemplateDto request)
    {
        if (request.Kind is not (1 or 2)) throw new ArgumentException("نوع العبارة غير صالح.");
        var text = request.Text?.Trim() ?? string.Empty;
        if (text.Length is < 3 or > 1000) throw new ArgumentException("العبارة يجب أن تكون بين 3 و1000 حرف.");
        return text;
    }

    private static VisitFeedbackTemplate NewTemplate(int schoolId, int kind, string text) =>
        new() { SchoolId = schoolId, Kind = kind, Text = text };

    private static VisitFeedbackTemplateDto Map(VisitFeedbackTemplate item) => new(item.Id, item.Kind, item.Text);
}
