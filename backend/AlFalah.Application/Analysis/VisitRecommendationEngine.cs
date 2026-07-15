namespace AlFalah.Application.Analysis;

/// <summary>Deterministic desktop-parity recommendations derived from persisted weak domains.</summary>
public static class VisitRecommendationEngine
{
    private const string ExcellenceFallback =
        "الاستمرار على نفس المستوى المتميز وتعزيز نقل الخبرات للزملاء";

    public static List<string> Build(IEnumerable<(string DomainCode, string DomainNameAr)> weakDomains)
    {
        var domains = weakDomains.ToList();
        if (domains.Count == 0) return new List<string> { ExcellenceFallback };

        return domains.Select(domain =>
            $"بخصوص {domain.DomainNameAr}: {FirstAction(domain.DomainCode)}").ToList();
    }

    private static string FirstAction(string domainCode) => domainCode switch
    {
        "D1" => "مراجعة توزيع المقاعد وترتيب الغرفة الصفية",
        "D2" => "حضور دورة تدريبية في استراتيجيات التدريس الحديثة",
        "D3" => "تصميم أنشطة تعلم تستهدف مهارات التفكير الناقد",
        "D4" => "إعداد خطة تقويم تشمل التشخيصي والبنائي والختامي",
        "D5" => "وضع قواعد صفية واضحة بمشاركة الطلاب",
        _ => "تحديد نقاط الضعف المحددة"
    };
}
