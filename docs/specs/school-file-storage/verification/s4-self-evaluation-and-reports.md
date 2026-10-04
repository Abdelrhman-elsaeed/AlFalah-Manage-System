# S4 — التقويم الذاتي والنواقص والتقارير

**التاريخ:** 2026-10-04 · **النطاق:** S4 فقط · **الحالة:** منفذة تقنيًا، الرايات OFF، قبول التشغيل الحي مؤجل.

## النتيجة المنفذة

قالب v1 ثابت: أربعة مجالات، أحد عشر معيارًا، 36 بندًا بنفس أكواد وترتيب S0، و145 بصمة صف/مورد ReferenceOnly. البيانات المنقحة تحفظ النص الوظيفي والدور والأهمية ومسارًا مرجعيًا وإجراء تجهيز وربط ومراجعة. لا أشخاص المصدر أو completed أو بايتات أو أعداد ملفات تاريخية. item-11-1/2 تبقيان 2.2؛ 1.5 يظهر بلا متطلب متتبع مخترع.

نسخة المتتبع لكل SchoolId/AcademicYearId/TemplateVersion تنشأ مرة واحدة. 36 بندًا إلزاميًا أوليًا؛ كتالوج S3 يحتفظ بالمعايير والمهام الأصلية اختيارية، مع OriginalTaskId/Code الدقيقين. لا EvidenceTask أو دليل أو قرار جديد بالتنقيح. CandidateTaskCodes ترشيحات معلنة تحتاج مراجعة المحتوى، ولا تتبعها موافقة أو ربط تلقائي. المسؤول الحقيقي وتاريخ الاستحقاق غير محددين أوليًا؛ المدير/المفوّض يختار عضوًا نشطًا من المدرسة.

Controller رفيع → Application service → repository → SQL. الأدوات الخارجية ومعالجة CSV/Excel/PDF في Infrastructure خلف interfaces. JSON هو ApiResponse<T>؛ التصدير الناجح bytes مصرح بها، private/no-store/nosniff. لا اعتماد على SchoolId أو صلاحيات من العميل.

## الحساب والفلاتر

المقام: المتطلبات النشطة الإلزامية في النطاق البنيوي. البسط: عدد المتطلبات التي تستوفي AnyApprovedLink أو MinimumApprovedLinks، مرة واحدة مهما زادت الروابط. المصدر المشترك S3 EvidenceLinkQueries يشترط رابطًا Approved وقرارًا على نفس النسخة الحالية، وملفًا متاحًا غير محذوف، واتصال/منحة/عضوية/صلاحية حالية. archive/import مستبعدان. الرفع والتقديم والرفض والنسخة المرشحة غير المعتمدة وTeacherEvidenceSubmission.ReviewStatus لا تزيد الجاهزية.

النسبة decimal = 100×البسط÷المقام، منزلتان AwayFromZero. 0/0 = null، NoRequirements، «لا متطلبات». أعداد الملفات الفريدة والروابط والروابط المعتمدة والمتطلبات والنواقص مستقلة. يشمل الناتج وقت الحساب وبصمة القالب والنطاق والفلاتر ومجاميع المدرسة وأربعة مجالات وأحد عشر معيارًا.

domainCode/standardCode/responsibleUserId/importance/search/trackerOnly تحدد نطاق الحساب. status/criticalOnly/hideCompleted/gapsOnly تحدد صفوف العرض والتصدير؛ لا تخفض مقام ذلك النطاق. بنود السلامة 4.2 الثلاثة Critical ولها عداد مستقل. gaps تعرض مطالب إلزامية غير مستوفاة فقط. pageSize افتراضي 25 وأقصى 100، مع SortOrder/Code/Id ثابتة. صفحة العرض تحدد IDs محدودة في SQL قبل تجميع روابطها؛ المجاميع الكلية تجمع في SQL دون تحميل graph كامل.

أسباب النقص قد تجتمع: NoFile، Unavailable (بما فيه النسخة القديمة/المنحة المسحوبة)، AwaitingReview، Rejected، InsufficientApprovedLinks. كل صف يقدم إجراء ربط/مراجعة ورابط المهمة ومعرف السنة. سحب ملكية/عضوية المعلم يخفض الاستيفاء والفهرس. قبل الرد تُفحص ملفات النطاق الحالية بدفعات 200 projections، وتُحفظ تغيرات availability دفعة واحدة دون SQL لكل ملف. missing/trash/خروج من الجذر والاستعادة يغيرون الحساب؛ فشل النقل لا يتحول إلى أرقام مكتملة أو missing مصطنع. التفويض يفحص قبل وبعد I/O.

## المتابعة والحكم والتاريخ

المتابعة: مسؤول عضو/دور، أهمية، إلزامية، سياسة/عدد مطلوب، DateOnly للاستحقاق، NotStarted/InProgress/ReadyForReview وملاحظة. سبب وrowversion مطلوبان؛ save/history/audit في نفس SaveChanges. الحالة ليست completed ولا تعتمد دليلًا. المصدر والنطاق وأسماء المطالب المنقحة ثابتة.

التقويم اليدوي: حكم أو قيمة عددية أو كلاهما، سبب إلزامي، هوية المقيّم واسمه، وقت، ونطاق مدرسة/مجال/معيار. القيمة بدقتين وبحدود decimal(9,2). التعديل يزيد Revision ويضيف SnapshotJson وStorage.* audit داخل Serializable transaction؛ rowversion قديم يعاد 409. لا يدخل في نسبة أو عداد نقص، ولا يغير قرار S3. APIs تعرض أحدث 100 مراجعة لكل سجل، وجميع المراجعات محفوظة في SQL.

SelfEvaluationTemplates وSchoolEvaluationScopes ثابتان. نسخة لاحقة تهيّأ صراحة، لا تستبدل سنة سابقة. SQL يمنع إعادة كتابة/حذف revisions أو provenance/scopes، مع FKs restrictive وrowversion. migration S4 additive فقط؛ sentinel قديم يبقى IsMandatory=false وNotStarted دون تغير الاعتماد/البايتات. السكربت idempotent طبّق مرتين على SQL عند S3؛ النموذج لا يحمل تغييرات معلقة.

## العقود والواجهة

العقود الدقيقة في [API spec](../../../05-API-ENDPOINTS.md): requirements/readiness/gaps/exports، templates، evaluation-members، initialize، follow-up/history، manual/history، digital-index. GET requirements الجديد paginated ومخصص للتقويم العام. كتالوج S3 القديم انتقل إلى requirement-catalog مع تحديث مستهلك Angular، ويبقى متاحًا للمعلم في مسار الربط الخاص. لا يكتسب المعلم قراءة التقويم العام من Catalog أو التكليف. القديم PATCH لا يعدل بيانات مصدر القالب. 400/403/404/409/503 تبقى أخطاء ApiResponse الآمنة.

Angular lazy RTL: readiness، standards/:code لجميع 11، gaps، tracker، manual، reports، digital-index. فلاتر URL وسنة/نسخة وتبديل حسب المسؤول/المجال/جدول/بطاقات، reset عرض دون حذف، بحث في صفحات المعايير، تحميل/فراغ/أخطاء. أربعة أعمدة مقارنة بديل الرادار، وروابط مستقلة بديل الشرائح؛ التجميع يخص الصفحة الحالية ويبين ذلك. بطاقات المتطلبات تفرق الإلزامي والاختياري وحالة الاستيفاء/المتابعة. لا stock media أو أرقام أو أشخاص ثابتون في المنتج.

المكتبة/المراجعة/الصور/الفيديو والتنزيل تعاد من S2/S3، مع السنة والمتطلب محفوظين عند الانتقال، ومحتوى عبر API المصرح فقط. الفهرس من ملفات مؤهلة فعلية، وليس 145 مراجع المصدر. 403 يمسح الملفات والأحكام والمؤشرات والنوافذ؛ 409 يجلب أحدث البيانات ويحفظ شرح التعارض. [50 صف parity مسندًا لـS4](s4-parity.json) و[الفروق المبررة](../baseline/prototype-parity.md) يغطيان الدوال المعاد استخدامها والبدائل.

## التصدير الفعلي والتحقق

التقرير جامع: سياق المدرسة/السنة/نسخة وبصمة/وقت/فلاتر، 16 نطاق حساب (مدرسة+4+11)، صفوف المتطلبات وخطة الاستكمال، الأحكام اليدوية، والفهرس الرقمي. مرشح gapsOnly ينتقل مع صفحة النواقص. بعد فحص الوصول والتوفر يجمع SQL داخل Serializable transaction واحدة؛ التصدير يقرأ جميع الصفحات المطابقة بحدود ثابتة، ولا يقتصر على الصفحة المرئية.

CSV UTF-8 BOM واقتباس صحيح للعربية والفواصل والأسطر، مع منع formula injection حتى بعد whitespace. Excel خمس أوراق RTL وخلايا نصية دون صيغ ومدرسة وفلاتر ومقاييس مطابقة. PDF Amiri مضمن، RTL، A4، هوية المدرسة وترويسة إعداداتها إن وجدت، جداول وعناوين وصفحات وخطة استكمال وأحكام وفهرس وروابط تحتفظ بالسنة. لا بيانات مصادر 4116/4617/4994 داخل مؤشرات حية.

الاختبارات تفتح CSV الناتج وXLSX بClosedXML وتولد PDF فعليًا. السكربت [verify-s4-exports.py](../scripts/verify-s4-exports.py) يقرأ الملفات من bytes: يقارن 16 بسط/مقام/نسبة في CSV/Excel/صفحة PDF، وثلاثة صفوف عربية بعد إخفاء المستوفى، والفلاتر والحكم، وRTL لكل أوراق Excel، وتضمين الخط وحدود كل حرف داخل صفحات PDF. العينات filtered=1/4=25.00%، full=1/36=2.78%، empty=0/0 بلا 100%. [نتائج المقارنة والبصمات](s4-export-comparison.json).

رُسمت وراجعت بصريًا صفحات التقرير المصفى، وأول/آخر تقرير كامل (10 صفحات)، والتقرير الفارغ: العربية متصلة واتجاه النص والأرقام المختلطة والرموز صحيحة، النواقص والأحكام مستقلة، لا overflow. الملفات/PNG المحلية تحت `.audit/sfs-s4/exports` مستبعدة من Git؛ بيانات الاختبار اصطناعية في SQL/مزود محاكى فقط. لا ملفات وهمية على Google.

**قيد PDF مثبت:** QuestPDF 2024.3.4 الحالي يولد ToUnicode=0000 لعدد من glyphs العربية المشكلة. الصورة صحيحة، لكن استخراج/نسخ/بحث العربية غير موثوق. سجلت المقارنة عدد تلك mappings؛ لا ادعاء بنجاح البحث العربي. لم يغير S4 نسخة مكتبة التقارير لكل المنصة أو خطوط التقارير التاريخية. هذا قيد مخرجات باقٍ قبل مطالبة التشغيل ببحث عربي موثوق داخل PDF.

## الاختبارات والأداء

الانحدار الكامل: 858 نجاحًا، صفر فشل/تخطٍّ، بما فيه اختبارات S1–S3 على LocalDB جديد. اختبارات S4 الجديدة: 15 SQL فعليًا + 8 قواعد عقد؛ تشمل المدارس/السنوات/الإصدارات، 31 رمز مهام أصلية مع sentinel، الحد الأدنى والأصل المشترك، pending/rejected، الفقد/trash/root/restore، استبدال نسخة وإعادة اعتماد مستقلة، سحب المنحة/العضوية/التفويض، منع المعلم وكل صيغ التصدير، manual مستقل وتاريخ/rowversion، تكليف/أهمية/0/0/نواقص حرجة، SQL history/schema/script/model، ملفات التصدير وحدوده.

Angular storage/shell: 40 نجاحًا. Playwright: 40 رحلة ناجحة = 20 لكل هاتف/سطح (S2/S3/S4)، API محاكاة وAngular فقط. بناء API Release صفر أخطاء/تحذيرات؛ Angular production ناجح مع التحذيرات السابقة لميزانية CSS في visits-v2/dashboard-live وثلاثة محددات PrimeNG. لا تعديل لهذه المساحات من S4.

[قياس SQL](s4-performance.json): 5000 رابط، 250 ملفًا، 5036 متطلبًا إلزاميًا مع كتالوج اختياري مستقل، صفحة 100 بحجم25، warmup ثم 10 عينات، 18 استعلامًا ثابتًا لكل طلب؛ وسيط 97.54ms وأقصى 130.08ms في التشغيل النهائي، دون هدف 200ms. القياس يشمل service والتفويض وSQL والمزود المحاكى والتحقق من 250 ملفًا؛ لا يقيس latency Google الفعلي. حد التصدير 5000 صف و5000 ملف، التحقق المباشر حتى 10000 ملف مختلف بدفعات 200؛ تجاوز الحد يطلب تضييق المرشحات. لا job system جديد لأن القياس لا يثبت الحاجة.

## إعادة التحقق دون تشغيل API الفعلية

```powershell
$env:DOTNET_ROLL_FORWARD = 'LatestMajor' # جهاز التحقق يملك SDK10 لتشغيل net8
$env:ALFALAH_STORAGE_TEST_CONNECTION = 'Server=(localdb)\MSSQLLocalDB;Database=AlFalahS1Tests_S4_' + [guid]::NewGuid().ToString('N') + ';Integrated Security=true;TrustServerCertificate=true'
$env:ALFALAH_S4_PERFORMANCE_REPORT = (Join-Path $PWD 'docs/specs/school-file-storage/verification/s4-performance.json')
$env:ALFALAH_S4_EXPORT_DIRECTORY = (Join-Path $PWD '.audit/sfs-s4/exports')
$env:ALFALAH_S4_MIGRATION_SCRIPT = (Join-Path $PWD 'docs/specs/school-file-storage/verification/s4-migration.sql')
dotnet test backend/AlFalah.Tests/AlFalah.Tests.csproj --verbosity quiet
python docs/specs/school-file-storage/scripts/verify-s4-exports.py .audit/sfs-s4/exports
# من frontend:
npx ng test --watch=false --browsers=ChromeHeadless --include='src/app/features/storage/**/*.spec.ts' --include='src/app/shared/layout/shell/*.spec.ts'
npx playwright test --config playwright.storage.config.ts
npm run build
```

fixture يرفض قاعدة غير LocalDB/غير جديدة/باسم غير AlFalahS1Tests_*، ويحتفظ بها للفحص دون drop. Readiness وEvidence والمكتبة لها قواعد منفصلة مشتقة من الاسم؛ لا تستخدم قاعدة سابقة لإعادة migration test. لا API startup أو seeder/auto-migration على Development الفعلية.

## حدود التشغيل والرجوع

ReadModelEnabled وAdministrationEnabled افتراضيًا false؛ قسم SchoolFileStorage غير موجود في إعدادات base/Development الحالية، فتطبق defaults. لم تعدل هذه الملفات أو credentials أو Data Protection keys. لم يبدأ API الفعلي ولم تنفذ أي migration/backfill على أصل Development/production أو HTTP Google أو cutover. S0 credential غير قابل للفك بالمفاتيح الحالية يبقى عائق live QA، لا يُسجل اتصالًا ناجحًا أو عدد ملفات صفرًا.

قبل إطلاق منفصل يلزم اتصال صالح وتحقق حقيقي للجذور والمنح وملف مفقود/مستعاد ونسخة/صلاحيات/تصدير، قبول المالك للبدائل، تسوية drift/backfill والكاتب/القارئ المشترك، مراجعة SQL وbackup/restore ورصد الأداء الفعلي. أرشيف الزيارات محظور حتى S5؛ لا تغيير في ذلك. الاستيراد التاريخي العام S6 غير منفذ.

الرجوع: أغلق الرايتين، أعد المسارات/النسخة السابقة المتوافقة عند الحاجة، واحتفظ بجميع جداول/أعمدة S4 والنسخ والقرارات والتكليفات/revisions/audit. migration Down يرفض الحذف؛ لا truncate/drop أو إعادة كتابة سنة بv1 آخر. ثبّت شكل requirement-catalog المتوافق إذا أبقيت واجهة S3 الجديدة. الربط والمراجعة القائمة ومصدر الحقيقة الموحد لا يستبدلان بكاتب legacy مستقل. لا تحذف ملف Drive لإرجاع قالب أو حكم يدوي.

**التوقف:** انتهى نطاق S4؛ لا S5 أو S6 أو cutover. قيد Google، قبول المالك، بحث/نسخ PDF العربي والأداء الحي بوابات/حدود معلنة، وليست تحققًا حيًا مكتملًا.
