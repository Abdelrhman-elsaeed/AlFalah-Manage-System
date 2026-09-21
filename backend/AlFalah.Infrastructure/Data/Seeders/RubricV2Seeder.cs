using AlFalah.Domain.Entities;
using AlFalah.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AlFalah.Infrastructure.Data.Seeders;

/// <summary>
/// Seeds the V2 Rubric Version with exact prototype data:
/// 5 domains, 25 standards, 66 indicators, and 5 treatment plan templates.
///
/// Guard: Only runs if no RubricVersion with VersionNumber == 2 exists (idempotent).
/// The V2 version is created as IsActive = false — activation requires explicit
/// invocation of <see cref="ActivateRubricV2Async"/> during controlled cutover (Phase 8).
///
/// Treatment plan templates are exposed as a static dictionary for use by the
/// analysis engine and report generation without DB dependency.
/// </summary>
public sealed class RubricV2Seeder
{
    private readonly AlFalahDbContext _context;
    private readonly ILogger<RubricV2Seeder> _logger;

    public RubricV2Seeder(AlFalahDbContext context, ILogger<RubricV2Seeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Treatment plan template data — verbatim from the approved prototype.
    /// Key = domain Arabic name, Value = (Goal, Actions, SuccessIndicators).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, (string Goal, string Actions, string SuccessIndicators)> TreatmentTemplates =
        new Dictionary<string, (string, string, string)>
        {
            ["بيئة التعلم"] = (
                "تحسين جودة بيئة التعلم وتنظيمها لتكون محفزة وآمنة لجميع المتعلمين",
                "1. مراجعة توزيع المقاعد بما يضمن عدالة الرؤية والمشاركة.\n2. إثراء الصف بمصادر تعلم مساندة ووسائل حسية متنوعة.\n3. ضبط إدارة زمن الحصة بين مراحل الدرس (تمهيد، تدريس، تطبيق، غلق).",
                "ارتفاع متوسط درجات بيئة التعلم إلى 80% أو أعلى في الزيارة القادمة."
            ),
            ["التدريس والتعلم"] = (
                "تطوير استراتيجيات التدريس الحديثة وتنويعها لتلبية الفروق الفردية واستثمار التقنية",
                "1. دمج استراتيجيات التعلم النشط (فكر-زاوج-شارك، التعلم التعاوني).\n2. توظيف المنصات والتطبيقات الرقمية التفاعلية المرتبطة بهدف الحصة.\n3. تقديم أمثلة ومهمات أدائية واقعية ترتبط بحياة الطلاب اليومية.",
                "تطبيق استراتيجيتين حديثتين مع توظيف التقنية وتوثيق تفاعل المتعلمين بنسبة تتجاوز 85%."
            ),
            ["تنمية المهارات"] = (
                "الارتقاء بمهارات التفكير العليا والمهارات اللغوية والعددية والشخصية للطلاب",
                "1. تضمين أسئلة تفكير ناقد وتفكير إبداعي مفتوحة النهاية (لماذا؟ كيف؟).\n2. تفعيل أنشطة القراءة السريعة والكتابة الإجرائية أثناء المهام الصفية.\n3. تشجيع العمل الجماعي وتبادل الأدوار لتعزيز الثقة والتواصل الإيجابي.",
                "مبادرة الطلاب في طرح التساؤلات والتحليل بنسبة ملحوظة ورضا المشرف بنسبة 80% فأكثر."
            ),
            ["التقويم"] = (
                "تفعيل أساليب التقويم المستمر والمتنوع وتقديم التغذية الراجعة الفورية",
                "1. تطبيق تقويم تشخيصي وتكويني خلال خطوات الحصة للتأكد من فهم كل مهارة.\n2. استخدام أدوات تقويم سريعة (بطاقات الخروج، استبانات إلكترونية، أسئلة شفهية مدرجة).\n3. تقديم تغذية راجعة تفسيرية توضح للطالب سبب الخطأ وكيفية معالجته.",
                "حصر وتشخيص مستويات جميع الطلاب وتقديم دعم فوري للمتعثرين وتوثيق ذلك كتابياً."
            ),
            ["سلوك المتعلمين"] = (
                "تعزيز الانضباط الإيجابي وغرس الهوية الوطنية والمسؤولية الذاتية لدى المتعلمين",
                "1. وضع وتذكير ميثاق وقواعد السلوك الصفي بالتراضي الإيجابي.\n2. تحفيز التحدث باللغة العربية السليمة وربط مضامين الدرس بالاعتزاز بالوطن.\n3. تمكين الطلاب من إدارة جزء من النشاط الذاتي لترسيخ الاعتماد على النفس.",
                "انضباط تام واختفاء الملاحظات السلوكية ومبادرة ذاتية من الطلاب في تنفيذ المهام."
            ),
        };

    /// <summary>
    /// Seeds Rubric V2 with exact prototype data. Existing soft-deleted or partial
    /// V2 rows are synchronized back to the approved canonical structure.
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var existingVersion = await _context.RubricVersions
            .IgnoreQueryFilters()
            .Include(v => v.Domains)
                .ThenInclude(d => d.Standards)
                    .ThenInclude(s => s.Indicators)
            .SingleOrDefaultAsync(v => v.VersionNumber == 2, cancellationToken);

        var version = new RubricVersion
        {
            VersionNumber = 2,
            IsActive = false, // Activated only during controlled cutover
            Notes = "V2 Prototype Rubric — 5 domains, 25 standards, 66 indicators (Phase 2)",
            CreatedAt = DateTimeOffset.UtcNow
        };

        // ─── Domain 1: بيئة التعلم (5 standards, 13 indicators) ───
        var d1 = new RubricDomain { Code = "D1", NameAr = "بيئة التعلم", SortOrder = 1 };
        AddStandardWithIndicators(d1, "D1-S1", 1, "تُنفّذ المدرسة برامج وأنشطة؛ لتعزيز القيم الإسلامية والهوية الوطنية لدى المتعلمين.", new[]
        {
            "توظيف أمثلة أو أنشطة مرتبطة بالقيم الإسلامية",
            "ربط المحتوى بالهوية الوطنية وتضمين أنشطة تعزز الانتماء للوطن",
        });
        AddStandardWithIndicators(d1, "D1-S2", 2, "تنفذ المدرسة إجراءات تضمن مناخا آمنا للتعلم والنمو نفسيا واجتماعيا.", new[]
        {
            "التعامل باحترام مع جميع المتعلمين",
            "شعور المتعلمين بالأمان داخل الصف والتعبير عن آرائهم دون خوف من الخطأ",
            "خلو البيئة من التنمر أو التهديد",
        });
        AddStandardWithIndicators(d1, "D1-S3", 3, "يتوافر في بيئة التعلم مصادر وأنشطة متنوعة للتعلم تلبي احتياجات المتعلمين، ومنهم ذوو الإعاقة والموهوبون.", new[]
        {
            "تنوع الأنشطة وفق مستويات المتعلمين",
            "مراعاة الفروق الفردية (الإعاقة والموهوبين).",
        });
        AddStandardWithIndicators(d1, "D1-S4", 4, "يُدار الوقت في بيئة التعلم بفاعلية؛ لدعم التعلم وتلبية احتياجات المتعلمين، ومنهم ذوو الإعاقة والموهوبون.", new[]
        {
            "الالتزام بتوقيت كل مرحلة من الدرس ( مقدمة - عرض - تطبيق – غلق )",
            "منح المتعلمين الذي يحتاجون وقتاً إضافياً فرصة إضافية دون الإخلال بجدول الحصة",
            "الانتقال السلس بين الأنشطة",
        });
        AddStandardWithIndicators(d1, "D1-S5", 5, "يُتاح للمتعلمين فرص متكافئة في الأنشطة والمناقشة الصفية، واستخدام مصادر التعلم.", new[]
        {
            "إشراك جميع المتعلمين",
            "تنوع أساليب المشاركة",
            "عدالة توزيع الفرص وتشجيع الطلاب المنعزلين عن المشاركة",
        });
        version.Domains.Add(d1);

        // ─── Domain 2: التدريس والتعلم (5 standards, 13 indicators) ───
        var d2 = new RubricDomain { Code = "D2", NameAr = "التدريس والتعلم", SortOrder = 2 };
        AddStandardWithIndicators(d2, "D2-S1", 1, "توفر المدرسة مصادر تعلم متنوعة تدعم تنفيذ المناهج؛ لتحقيق نواتج التعلم المستهدفة.", new[]
        {
            "استخدام مصادر تعلم متنوعة داخل الحصة (عرض، فيديو، أوراق عمل، منصة رقمية).",
            "توظيف المصادر بشكل مباشر لخدمة هدف الدرس وليس للعرض فقط",
            "يظهر أثر استخدام المصادر على فهم المتعلمين ومشاركتهم أثناء الحصة",
        });
        AddStandardWithIndicators(d2, "D2-S2", 2, "ينفذ المعلم أنشطة واستراتيجيات تدريس تستوفي نواتج التعلم المستهدفة في المنهج، وتتسق معها بوضوح.", new[]
        {
            "تنوع الأنشطة والاستراتيجيات المرتبطة بأهداف الدرس.",
            "وضوح ارتباط الأنشطة بنواتج التعلم.",
            "تحقق نواتج التعلم من خلال تنفيذ الأنشطة.",
        });
        AddStandardWithIndicators(d2, "D2-S3", 3, "تتنوع استراتيجيات التدريس وفقا لقدرات المتعلمين، وتراعي الفروق الفردية بينهم.", new[]
        {
            "تنوع الاستراتيجيات وفق مستويات المتعلمين",
            "مراعاة الفروق الفردية أثناء التنفيذ",
            "مشاركة جميع المتعلمين حسب قدراتهم",
        });
        AddStandardWithIndicators(d2, "D2-S4", 4, "يستخدم المعلم مصادر تعلم رقمية تلبي احتياجات المتعلمين بمختلف فئاتهم.", new[]
        {
            "استخدام مصادر رقمية متنوعة (منصة، فيديو، تطبيق) تخدم أهداف الدرس.",
            "تكييف استخدام المصادر الرقمية لتناسب مستويات واحتياجات جميع المتعلمين.",
        });
        AddStandardWithIndicators(d2, "D2-S5", 5, "تنفذ المدرسة أنشطة تعليم وتعلم تركز على تطبيقات عملية ترتبط بحياة المتعلمين:", new[]
        {
            "تنفيذ أنشطة عملية يطبق فيها الطلاب ما تعلموه في مواقف من حياتهم اليومية.",
            "ربط المهارات والدرس بتجارب واقعية (مثل أمثلة من البيت أو المجتمع) لزيادة الفهم.",
        });
        version.Domains.Add(d2);

        // ─── Domain 3: تنمية المهارات (6 standards, 18 indicators) ───
        var d3 = new RubricDomain { Code = "D3", NameAr = "تنمية المهارات", SortOrder = 3 };
        AddStandardWithIndicators(d3, "D3-S1", 1, "تشجع بيئة التعلم داخل الصف على تنمية مهارات القراءة والكتابة لدى المتعلمين.", new[]
        {
            "تضمين أنشطة قراءة وكتابة مرتبطة بالدرس",
            "تشجيع المتعلمين على التعبير والكتابة والمشاركة",
            "تصحيح الأخطاء والسلامة اللغوية",
        });
        AddStandardWithIndicators(d3, "D3-S2", 2, "تشجع بيئة التعلم داخل الصف على تنمية المهارات العددية (الحساب) لدى المتعلمين.", new[]
        {
            "دمج أرقام، إحصائيات، أو تواريخ ضمن سياق الدرس لتعزيز الفهم",
            "تشجيع المتعلمين على استخدام مهارات التقدير (مثل: تقدير زمن المهمة، تقدير المسافات)",
            "استخدام المتعلمين للمصطلحات العددية (أكثر، أقل، ضعف، نسبة مئوية، الترتيب التسلسلي)",
        });
        AddStandardWithIndicators(d3, "D3-S3", 3, "تشجع الممارسات التدريسية على تنمية مهارات التفكير والبحث والابتكار لدى المتعلمين.", new[]
        {
            "طرح أسئلة تفكير عليا (تحليل – تفسير – استنتاج) لماذا؟ كيف؟",
            "توجيه المتعلمين لاستخراج المعلومات من (الكتاب، مصادر خارجية، أو تجربة ذهنية)",
            "التشجيع على تقديم حلول غير معتادة أو اقتراح \"نهاية مختلفة\" أو \"تصميم فكرة\"",
        });
        AddStandardWithIndicators(d3, "D3-S4", 4, "تشجع بيئة التعلم تنمية المهارات العاطفية والاجتماعية لدى المتعلمين.", new[]
        {
            "تعزيز التعاون والعمل الجماعي بين الطلاب وبمهام محددة",
            "تنمية مهارات التواصل واحترام آراء الآخرين",
            "دعم الثقة بالنفس والتعبير عن المشاعر بشكل إيجابي",
        });
        AddStandardWithIndicators(d3, "D3-S5", 5, "يستخدم المعلم أساليب تحفيز تعزز الدافعية لدى المتعلمين.", new[]
        {
            "يستخدم المعلم التعزيز الإيجابي (لفظي أو معنوي) لتحفيز جميع الطلاب",
            "يوظف أساليب تشجيعية متنوعة (نقاط – مسابقات – مكافآت - أوسمة)",
            "النجاح في جعل المتعلمين يبادرون بالمشاركة والحماس للأنشطة طوال وقت الحصة",
        });
        AddStandardWithIndicators(d3, "D3-S6", 6, "يشارك المتعلمون في أنشطة التعلم بفاعلية، ويستمتعون بها.", new[]
        {
            "مبادرة المتعلمين للمشاركة وطرح الأسئلة بوضوح وحماس طوال الحصة",
            "انخراط المتعلمين في تنفيذ الأنشطة الصفية بتركيز عالٍ، مع الالتزام التام بالعمل الجماعي",
            "ظهور علامات البهجة والارتياح أثناء التعلم، مع الرغبة المستمرة في إكمال التحديات",
        });
        version.Domains.Add(d3);

        // ─── Domain 4: التقويم (3 standards, 9 indicators) ───
        var d4 = new RubricDomain { Code = "D4", NameAr = "التقويم", SortOrder = 4 };
        AddStandardWithIndicators(d4, "D4-S1", 1, "يستخدم المعلمون أساليب وأدوات تقويم متنوعة تشخيصية وبنائية وختامية؛ للكشف عن الفروق الفردية في مستويات أداء المتعلمين المختلفة.", new[]
        {
            "استخدام التقويم في كافة مراحل الحصة (قبلي، بنائي أثناء الشرح، وختامي)",
            "توظيف أدوات متنوعة (مثل: الأسئلة الشفهية، الاختبارات القصيرة أو تطبيقات التقويم التقني).",
            "تصميم أسئلة ومهام تتناسب مع مستويات الطلاب المختلفة (المحتاجين للدعم، المتوسطين، والمتميزين)",
        });
        AddStandardWithIndicators(d4, "D4-S2", 2, "يطبق المعلم أساليب وأدوات تقويم متنوعة؛ لقياس مستوى تحقق نواتج التعلم المستهدفة في المنهج لدى المتعلمين.", new[]
        {
            "يستخدم طرقاً مختلفة للقياس (مثل: الاختبارات القصيرة، الأنشطة الشفهية، أو المهام الأدائية).",
            "يتأكد أن كل سؤال أو نشاط يقيس فعلياً هدفاً محدداً من أهداف الدرس (نواتج التعلم)",
            "يستطيع من خلال التقويم تحديد مستوى كل طالب (من أتقن المهارة ومن يحتاج إلى دعم إضافي)",
        });
        AddStandardWithIndicators(d4, "D4-S3", 3, "يقدم المعلم تغذية راجعة متنوعة للمتعلمين حسب الموقف التعليمي تركز على تحسين أدائهم.", new[]
        {
            "تقديم التعليق أو التصحيح للمتعلم أثناء أو فور انتهاء المهمة مباشرة لضمان الاستفادة.",
            "عدم الاكتفاء بقول (صح أو خطأ)، بل يوضح للمتعلم \"لماذا\" أخطأ وكيف يصحح مساره للوصول للحل الصحيح.",
            "استخدام طرقاً مختلفة للتغذية الراجعة (مثل: التصحيح الذاتي، تبادل التصحيح بين الزملاء، أو التوجيه الفردي).",
        });
        version.Domains.Add(d4);

        // ─── Domain 5: سلوك المتعلمين (6 standards, 13 indicators) ───
        var d5 = new RubricDomain { Code = "D5", NameAr = "سلوك المتعلمين", SortOrder = 5 };
        AddStandardWithIndicators(d5, "D5-S1", 1, "يظهر المتعلمون الاعتزاز بالقيم والهوية الوطنية.", new[]
        {
            "التزام المتعلمين بالتحدث بلغة عربية سليمة طوال زمن الحصة",
            "اعتزاز المتعلمين بتاريخ ومنجزات الوطن ورموزه في مشاركاتهم وأثناء الأنشطة",
            "التمسك بالقيم والأخلاق الإسلامية في التعامل والسمت العام",
        });
        AddStandardWithIndicators(d5, "D5-S2", 2, "يظهر المتعلمون الاتجاهات الإيجابية نحو ذواتهم والآخرين.", new[]
        {
            "إظهار الثقة بالنفس أثناء المشاركة وطرح الآراء.",
            "يحترم المتعلمون آراء زملائهم ويتعاملون بإيجابية ويشجعون بعضهم أثناء الأنشطة",
        });
        AddStandardWithIndicators(d5, "D5-S3", 3, "يظهر المتعلمون التزاما بالممارسات الصحية السليمة.", new[]
        {
            "المحافظة على نظافة المكان والأدوات، والحرص على بيئة صفية صحية (تهوية وإضاءة).",
            "الالتزام بوضعية الجلوس الصحيحة، والاهتمام بالنظافة الشخصية والترتيب العام.",
        });
        AddStandardWithIndicators(d5, "D5-S4", 4, "يلتزم المتعلمون بقواعد السلوك والانضباط.", new[]
        {
            "الالتزام بالتعليمات والنظام داخل الحصة دون تكرار التنبيه، والمحافظة على الهدوء",
            "الحرص على آداب الاستئذان قبل المشاركة، والالتزام بمواعيد بدء ونهاية الحصة.",
        });
        AddStandardWithIndicators(d5, "D5-S5", 5, "يظهر المتعلمون الاستقلالية، والقدرة على التعلم الذاتي.", new[]
        {
            "الاعتماد على النفس في إنجاز المهام، والبحث عن المعلومة قبل طلب المساعدة.",
            "القدرة على التقييم الذاتي وتصحيح الأخطاء بناءً على التغذية الراجعة.",
        });
        AddStandardWithIndicators(d5, "D5-S6", 6, "يظهر المتعلمون الاعتزاز بثقافتهم واحترام التنوع الثقافي في المجتمع.", new[]
        {
            "التعبير عن الاعتزاز بالثقافة والهوية الوطنية، وإظهار التقدير لثقافات ولهجات الآخرين.",
            "تجنب أي تعليقات سلبية تجاه الاختلافات الثقافية، والتعامل بروح الانفتاح والاحترام.",
        });
        version.Domains.Add(d5);

        if (existingVersion is not null)
        {
            SynchronizeExistingVersion(existingVersion, version);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Rubric V2 verified and restored: Version {Version}, {Domains} domains, {Standards} standards, {Indicators} indicators.",
                existingVersion.VersionNumber,
                existingVersion.Domains.Count(d => !d.IsDeleted),
                existingVersion.Domains.Where(d => !d.IsDeleted).SelectMany(d => d.Standards).Count(s => !s.IsDeleted),
                existingVersion.Domains.Where(d => !d.IsDeleted).SelectMany(d => d.Standards)
                    .Where(s => !s.IsDeleted).SelectMany(s => s.Indicators).Count(i => !i.IsDeleted));
            return;
        }

        _context.RubricVersions.Add(version);
        await _context.SaveChangesAsync(cancellationToken);

        var totalIndicators = version.Domains.SelectMany(d => d.Standards).SelectMany(s => s.Indicators).Count();
        _logger.LogInformation(
            "Rubric V2 seeded: Version {Version} (IsActive={Active}), {Domains} domains, {Standards} standards, {Indicators} indicators.",
            version.VersionNumber, version.IsActive,
            version.Domains.Count,
            version.Domains.SelectMany(d => d.Standards).Count(),
            totalIndicators);
    }

    /// <summary>
    /// Controlled cutover: deactivates V1 and activates V2 in a single transaction.
    /// Should be called explicitly during Phase 8 pilot — NOT during startup.
    /// </summary>
    public async Task ActivateRubricV2Async(CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var activeV1 = await _context.RubricVersions
                .Where(v => v.IsActive && v.VersionNumber != 2)
                .ToListAsync(cancellationToken);

            foreach (var v in activeV1)
                v.IsActive = false;

            var v2 = await _context.RubricVersions
                .FirstOrDefaultAsync(v => v.VersionNumber == 2, cancellationToken)
                ?? throw new InvalidOperationException("Rubric V2 not found. Run SeedAsync first.");

            v2.IsActive = true;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Rubric V2 activated. V1 deactivated.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static void AddStandardWithIndicators(
        RubricDomain domain,
        string standardCode,
        int sortOrder,
        string textAr,
        string[] indicatorTexts)
    {
        var standard = new RubricStandard
        {
            Code = standardCode,
            SortOrder = sortOrder,
            TextAr = textAr,
        };

        for (int i = 0; i < indicatorTexts.Length; i++)
        {
            standard.Indicators.Add(new RubricIndicator
            {
                Code = $"{standardCode}-I{i + 1}",
                TextAr = indicatorTexts[i],
                SortOrder = i + 1,
            });
        }

        domain.Standards.Add(standard);
    }

    private static void SynchronizeExistingVersion(RubricVersion existing, RubricVersion canonical)
    {
        Restore(existing);
        existing.Notes = canonical.Notes;

        foreach (var domain in existing.Domains)
            domain.IsDeleted = true;

        foreach (var expectedDomain in canonical.Domains)
        {
            var domain = existing.Domains.FirstOrDefault(d => d.Code == expectedDomain.Code);
            if (domain is null)
            {
                existing.Domains.Add(expectedDomain);
                continue;
            }

            Restore(domain);
            domain.NameAr = expectedDomain.NameAr;
            domain.SortOrder = expectedDomain.SortOrder;
            foreach (var standard in domain.Standards)
                standard.IsDeleted = true;

            foreach (var expectedStandard in expectedDomain.Standards)
            {
                var standard = domain.Standards.FirstOrDefault(s => s.Code == expectedStandard.Code);
                if (standard is null)
                {
                    domain.Standards.Add(expectedStandard);
                    continue;
                }

                Restore(standard);
                standard.TextAr = expectedStandard.TextAr;
                standard.SortOrder = expectedStandard.SortOrder;
                foreach (var indicator in standard.Indicators)
                    indicator.IsDeleted = true;

                foreach (var expectedIndicator in expectedStandard.Indicators)
                {
                    var indicator = standard.Indicators.FirstOrDefault(i => i.Code == expectedIndicator.Code);
                    if (indicator is null)
                    {
                        standard.Indicators.Add(expectedIndicator);
                        continue;
                    }

                    Restore(indicator);
                    indicator.TextAr = expectedIndicator.TextAr;
                    indicator.SortOrder = expectedIndicator.SortOrder;
                }
            }
        }
    }

    private static void Restore(RubricVersion version)
    {
        version.IsDeleted = false;
        version.DeletedAt = null;
        version.DeletedByUserId = null;
    }

    private static void Restore(RubricDomain domain)
    {
        domain.IsDeleted = false;
        domain.DeletedAt = null;
        domain.DeletedByUserId = null;
    }

    private static void Restore(RubricStandard standard)
    {
        standard.IsDeleted = false;
        standard.DeletedAt = null;
        standard.DeletedByUserId = null;
    }

    private static void Restore(RubricIndicator indicator)
    {
        indicator.IsDeleted = false;
        indicator.DeletedAt = null;
        indicator.DeletedByUserId = null;
    }
}
