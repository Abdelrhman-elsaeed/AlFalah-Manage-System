# S3 — الشواهد والمراجعة والنسخ: التنفيذ والتحقق والرجوع

**التاريخ:** 2026-10-04 · **النطاق:** S3 فقط، توقف قبل S4. التنفيذ التقني مكتمل؛ قبول Drive الحي وcutover معلّقان. `AdministrationEnabled=false` و`ReadModelEnabled=false`.

## التنفيذ ومصدر الحقيقة

Controller → Application Service → Repository → SQL مع `ApiResponse<T>`. خدمات Application: RequirementCatalogService، EvidenceLinkService، EvidenceReviewService، FileChangeRequestService، StorageEvidenceReadService؛ السياسة المشتركة في EvidenceWorkflowContext. EF والاستعلامات والحفظ في EvidenceRepository وواجهات مستودعات S1/S2. Controller لا يتخذ قرار مراجعة أو يكتب DB مباشرة.

كتالوج المدرسة/السنة ينشئ 11 متطلب معيار STD ضمن المجالات الأربعة، وmapping واحدًا لكل EvidenceTask نشط بنفس OriginalTaskId/Code، دون إضافة EvidenceTask أو افتراض التطابق الدلالي مع معيار. تهيئته قابلة للإعادة؛ الإعداد الصريح يحفظ المجال/المعيار والأهمية والمسؤول العضو/الدور وسياسة AnyApprovedLink أو MinimumApprovedLinks وحدها. المسؤول ليس منحة صلاحية. اختيار السنة لا يوسع المدرسة المحددة من هوية المستخدم.

ربط أصل موجود ينشئ EvidenceLink فقط، دون نسخ/رفع Drive. ملكية المعلم مأخوذة من الأصل؛ اختلاف TeacherId يرفض، وown endpoints لا تعرض ملف زميل ولو كان المعلم يملك معرفه. الرابط النشط فريد للأصل/المتطلب/السنة/المعلم، بما في ذلك TeacherId=NULL. الفهارس المركبة تمنع نسخة أصل/مدرسة أخرى.

حالات الرابط: Draft → PendingReview → Approved/Rejected، ثم Rejected → Resubmitted → Approved/Rejected. سبب الرفض إلزامي، ورمز rowversion مطلوب للتقديم/المراجعة. قرار الرابط لا يؤثر على روابط الملف الأخرى. كل قرار يحتفظ بالنسخة والمراجع واسمه ووقته وملاحظته، وهو append-only في EF وSQL. سجلات Storage.* audit محمية من التعديل والحذف في SQL، وتتضمن انتقالات حالة/نسخة الروابط. أسماء المراجعين الجديدة snapshots؛ أسماء السجلات القديمة الناقصة تعرض من العضو الحالي دون اختلاق اسم تاريخي.

`EvidenceLink` وقراراته هما مصدر المراجعة للملف mapped. `TeacherEvidenceSubmission.ReviewStatus/ReviewNote/ReviewedBy/ReviewedAt` تصبح baseline توافق مجمدة، مع trigger يمنع كتابتها، وخدمة legacy ترفض مراجعة submission mapped حتى عند إغلاق الراية. rename/delete القديمة ترفض الملف mapped أيضًا، وحماية S2 تمنع تعديل أصل سبق اعتماده أو متعدد النسخ. unmapped legacy يظل في مساره القائم لحين تحضيره.

## النسخ وطلبات التغيير

طلب FileChangeRequest يسجل الأصل والنسخة الأصلية والفاعل والسبب وReplace/Delete والنية ReplaceBeforeReview، ثم نسخة مرشحة اختيارية وحالة Pending/Approved/Rejected. سبب/أصل/فاعل/نية الطلب لا تتغير؛ المرشح لا يستبدل بعد تثبيته؛ الطلب والقرار السابق لا يحذفان. طلب واحد Pending لكل أصل وقرار نهائي واحد لكل طلب. rowversion يمنع قرارًا مبنيًا على حالة قبل رفع المرشح أو قرارًا آخر.

رفع المرشح يمر بحد 250 MiB والتحقق من البايتات/signature/SHA256/spool ومفتاح idempotency ومعرف Google المحجوز والمصالحة في S2. عملية واحدة لكل ChangeRequestId؛ اختلاف bytes/key identity أو تكرار عملية ثانية يتعارض. رفع المرشح ينشئ StoredFileVersion جديدًا **للأصل نفسه** ويكمل عملية الرفع والـaudit في معاملة SQL، ويترك CurrentVersionId القديم حتى القرار. فقد الرد أو فشل SQL يحافظ على العملية ويُصالح العنصر المحجوز نفسه دون إعادة رفع تلقائية.

الاستبدال قبل أول اعتماد مسموح بنية صريحة `replaceBeforeReview=true`: الإكمال يعيد فحص غياب كل تاريخ اعتماد ويطبق النسخة مع طلب/قرار سياسة مسجلين. لا يمنح اعتماد شاهد. بعد أي اعتماد يلزم قرار مدير/مفوّض؛ لا يستطيع المعلم استعمال هذا المسار المختصر. قبول الاستبدال يغير المؤشر ويعيد **كل رابط نشط** إلى PendingReview على النسخة الجديدة داخل المعاملة، مع حفظ القرارات السابقة المرتبطة بالنسخ القديمة. رفض المرشح يبقي المؤشر والاعتماد القديمين ويحفظ المرشح للقراءة المصرحة.

Delete هنا سحب منطقي مع حفظ البايتات والنسخ والقرارات. لا trash أو purge على Drive؛ السحب يوقف الاحتساب والتنزيل العادي ويترك history/version-content للمصرح وفق العضوية والمنحة الحالية. هذه سياسة حفظ S3، وليست تنفيذ أرشيف زيارات S5.

## القراءة والاحتساب والتصدير

قارئ المصفوفة الجديد خلف `SchoolFileStorage.ReadModelEnabled`. اختيار السنة وقراءة الخلية وExcel/PDF تشترك في صلاحية القراءة المدرسية والمصدر الجديد؛ المعلم يرى روابطه في صفحة ملفاته ولا يكتسب مراجعة مدرسية. الصفوف تحتفظ بهويات EvidenceTask الحالية، لا صفوف معايير مكررة في المصفوفة القديمة.

الرابط المحتسب: نشط، متطلبه نشط بنفس المدرسة/السنة، Approved وله قرار Approved للنسخة نفسها، النسخة هي CurrentVersionId وتوفرها Available، الأصل ليس مسحوبًا/أرشيفًا/استيرادًا، اتصال المدرسة مفعّل مطابق، ومعلم الأصل وعضويته وصلاحية ViewOwn ومنحته نشطة. القراءة تعيد التحقق من Drive للأصول المعتمدة قبل احتسابها؛ missing/trash/خروج من الجذر يوقفها، والاستعادة داخل الجذر تعيد التوفر. فشل النقل/الاتصال يرفض القراءة دون اختلاق Missing. قبول نسخة جديدة يوقف اعتماد السابقة حتى اعتماد الرابط من جديد.

FulfilledRequirements يحسب هوية المتطلب مرة واحدة، ويطبق الحد الأدنى عند اختياره؛ matrix IsChecked يطبق السياسة نفسها لكل معلم/مهمة. Files عدد الأصول المتاحة في المكتبة (أو ملكية المعلم)، Links عدد الروابط المؤهلة للسنة، ApprovedLinks مستقل، ومتطلبات السنة ومكتملها مستقلان. أصل المكتبة لا يملك سنة بذاته؛ ارتباطاته هي المحددة بالسنة. لا تستخدم هذه الأعداد كتقييم ذاتي S4.

## provenance وbackfill والمقارنة

كاتب S2 المشترك الجديد يحفظ LegacyProvenanceJson/Fingerprint وSharedWriterProvenanceJson/Fingerprint قبل إكمال الأصل، ويضيف رابط PendingReview mapped بالمهمة الأصلية. إعداد أي baseline مرة واحدة لا يغير snapshot S1 الأصلي. الإصلاح المستقل `--repair-shared-writer` يستهدف كتابات S2 القديمة ذات Completed operation وأصل/نسخة أولى بلا provenance/link؛ يتحقق من الهوية ثم يسجل baseline جديدًا منفصلًا. لا يدّعي إعادة بناء snapshot أصل لم يكن محفوظًا.

backfill يفحص identity والبايتات والتواريخ الأصلية وbaseline قرار legacy؛ تغييرات توفر/اسم/سحب الأصل وقرارات الرابط التشغيلية لا تسبب drift زائفًا. فقد البنية/اختلاف baseline يبقى استثناء واضحًا؛ لا overwrite لقرار تاريخي. الأداة ترفض اسم Development الفعلي وتقبل قاعدة محلية معزولة بالبادئات الموثقة. [أوامر الإصلاح](../scripts/README.md) لا تبدأ API أو migrate أو Drive/Data Protection.

[مقارنة SQL](s3-backfill-comparison.json) تستخدم ثلاثة submissions اصطناعية Approved/PendingReview/Rejected وكتابة S2 مشتركة قديمة، إضافة sentinel الموروث من fixture. dry-run repair=1، apply=1، rerun=0. backfill الأول أنشأ بقية الأصول/الروابط/القرارات بنفس Drive IDs، والإعادة أضافت صفرًا وانتهت بلا drift. هوية المهام وحالات الخلايا وعدد الملفات متطابقة قبل/بعد؛ الاستيفاء القديم 3 والجديد 1، وكذلك Excel 3→1: الفرق المقصود هو إلغاء tick للرفع PendingReview/Rejected. هذه ليست إعادة قياس أو cutover على Development الفعلي.

[مقارنة الفقد والاستعادة](s3-matrix-comparison.json) تثبت matrix وExcel 1→0 عند فقد النسخة؛ النقل خارج الجذر ثم الاستعادة اختُبرا أيضًا. Excel قورن فعليًا باستخدام ClosedXML؛ PDF تم تجهيزه ليقرأ GetAsync نفسه، دون ادعاء مقارنة بصرية لمخرجات PDF مع Drive الحقيقي.

## عقود HTTP والواجهات

المسارات والـDTOs في [سجل API](../../../05-API-ENDPOINTS.md#school-file-storage-s3-evidence-api-2026-10-04) وEvidenceContracts.cs. JSON camelCase، rowVersion base64، enums في الأوامر أرقام، وحالات DTO نصوص. 400 إدخال/سبب غير صالح، 403 منع صلاحية/ملكية/تفويض، 404 مساحة مغلقة/معرف غير متاح/بايتات مفقودة، 409 تكرار/تزامن/rowversion، 503 عدم تأكد رفع SQL/Drive. content/versions عبر streams مصرح بها وno-store/nosniff/sandbox، دون WebUrl عام.

Angular داخل المكتبة وصفحة المعلم الحالية: كتالوج قابل للبحث وسنة، ربط ملف موجود، شارات مستقلة وسبب رفض وتقديم، تاريخ القرارات وتنزيل النسخ المصرح، قائمة انتظار بفلاتر المتطلب/المعيار/المعلم/السنة/الحالة وترقيم خادم، إعداد السياسة/الأهمية/دور المسؤول، قائمة طلبات تغيير منفصلة، مرشح قابل للرفع/الإعادة بنفس المفتاح/المصالحة، وقرار الطلب. حالات loading/empty/error و409 وتصفير بيانات/preview عند403. UI RTL وألوان/clearable-select من المنصة، ولا يعرض معرفات Google أو تفاصيل التخزين للمستخدم.

## التحقق وإعادة التشغيل

النتائج النهائية: **835 backend ناجحة، صفر فشل/تخطٍّ، منها44 على SQL Server و17 منها خاصة بـS3؛ Angular32؛ Playwright24؛ build إنتاجي ناجح**. التفاصيل في [s3-checks.json](s3-checks.json). SQL Server فعلي على LocalDB جديد ومعزول لكل collection، مع حفظ sentinel وcredential المشفر؛ لا reset/drop أو API startup. اختبارات S1/S2 السابقة تبقى ضمن التشغيل الكامل. S3 يغطي عزل المدرسة/السنة/المالك وسحب التفويض والمنحة/عضوية المالك، استقلال الروابط ومنع تكرارها وسباقات إنشاء/مراجعة/rowversion، أسباب الرفض والتقديم، حماية القرار/audit/request/provenance/version، قبول/رفض/سحب النسخ، SQL-after-Drive/lost-response/replay، مقارنة المصادر/Excel وسياسة الحد الأدنى، وآلاف الروابط. SQL S3 idempotent يُطبَّق ويُعاد على قاعدة S2 جديدة؛ triggers مغلفة dynamic EXEC لتعمل ضمن شروط السكربت.

[الأداء](s3-performance.json): 5000 رابط، page100/pageSize25، warmup ثم10 قياسات، median23.77ms وmax31.06ms؛ الهدف median<200ms. لا N+1 لبيانات الروابط/قرارات الصفحة، ولا content calls في قائمة الانتظار. القياس محلي SQL مع provider محاكاة، ولا يثبت Google/HTTP أو الإنتاج. التشغيل الهادئ النهائي نجح بعد فشل مؤقت لحد توقيت اختبار timetable قائم أثناء build Angular متزامن؛ لم يغير كود timetable أو يخفف حد الاختبار. Angular unit+browser وbuild إنتاجي مسجلان؛ chunk التخزين lazy145.60kB raw/24.08kB estimated transfer. تحذيرات CSS budget القائمة في visits-v2/dashboard-live و3 selectors PrimeNG باقية خارج S3. المتصفح Chromium desktop وPixel5 بواجهات API محاكاة، وAngular server فقط. الصور في frontend/test-results/storage (ignored) روجعت للهاتف والسطح، مع التحقق من overflow؛ PDF المحاكى يثبت مسار blob المصرح وحاوية المعاينة، وليس rendering بايتات PDF حقيقية.

```powershell
$env:DOTNET_ROLL_FORWARD='LatestMajor'
$env:ALFALAH_STORAGE_TEST_CONNECTION='Server=(localdb)\MSSQLLocalDB;Database=AlFalahS1Tests_S3_'+[guid]::NewGuid().ToString('N')+';Integrated Security=true;TrustServerCertificate=true'
$env:ALFALAH_S3_PERFORMANCE_REPORT=(Join-Path $PWD 'docs/specs/school-file-storage/verification/s3-performance.json')
$env:ALFALAH_S3_COMPARISON_REPORT=(Join-Path $PWD 'docs/specs/school-file-storage/verification/s3-matrix-comparison.json')
$env:ALFALAH_S3_BACKFILL_REPORT=(Join-Path $PWD 'docs/specs/school-file-storage/verification/s3-backfill-comparison.json')
dotnet test backend/AlFalah.Tests/AlFalah.Tests.csproj --verbosity minimal
# من frontend؛ لا backend:
npx ng test --watch=false --browsers=ChromeHeadless --include='src/app/features/storage/**/*.spec.ts' --include='src/app/shared/layout/shell/*.spec.ts'
npx playwright test --config playwright.storage.config.ts
npm run build
```

## القيود وبوابات الإطلاق وخطة الرجوع

قيد S0 قائم: credential المسجلة لا تُفك بالمفاتيح الحالية. **لم تغير بيانات الاعتماد أو مفاتيح Data Protection، ولا كتابة/طلب Google حي، ولا migrate/backfill على Development الفعلية، ولا تشغيل API فيها.** لم يبدأ S4 أو أرشيف S5 أو import S6. الرايتان بقيتا false في defaults والإعدادات.

التحقق الحي المتعذر: root/grant/Shared Drive الفعلية، تنزيل/معاينة واعتماد كل رابط، استبدال/سحب ملف معتمد وتاريخ بايتاته، quota وانقطاع Google ثم SQL/reconciliation، واستجابة الواجهة والأداء المتكامل. SQL والـfake/browser لا يغلقان بوابة Google أو قبول parity S0. لا ندعي مقارنة جميع بيانات المدرسة التشغيلية أو عرض PDF الحقيقي.

قبل أي cutover: اتصال قابل للفك بالمفاتيح الأصلية ضمن تكليف تشغيل مستقل، تدقيق المنح والعضويات والجذور، backup/restore rehearsal، migration SQL مراجعة، repair dry/apply/backfill مرتين مع drift=0، مقارنة المدرسة/السنة الفعلية والمصفوفة/التصدير وتسوية فرق ticks المتوقع، قبول المالك للـparity، QA حي لكل الأدوار والنسخ والفشل، ومراقبة المصالحة. [SQL S3](s3-migration.sql) additive ولا يحذف سجلًا قديمًا.

الرجوع الآن يبقي الرايتين مغلقتين. بعد أي تشغيل فعلي لاحق: أغلقهما لإيقاف workspace ثم احتفظ بإصدار الكود المتوافق مع triggers وبكل schema/الأصول/النسخ/الروابط/القرارات/audit/العمليات، واعزل التقارير mapped عن اعتبار cache legacy مصدرًا حاليًا بعد قرارات S3. لا تعكس القرار بالكتابة في ReviewStatus القديم ولا تعِد تفعيل كاتبه؛ استخدم snapshots والتقرير المصرح في خطة التشغيل. لا تُرجع binary قديمًا لا يعرف triggers الجديدة؛ الرجوع هنا بالرايات مع حفظ حماية S3. Down لكل migrations S3 يرفض حذف التاريخ؛ لا drop/purge أو إعادة بيانات اعتماد/مفاتيح أو تعويض trash للأصل أو للمرشح تلقائيًا. أصل النسخة القديمة يبقى للإصلاح/التدقيق. توقف العمل بعد S3؛ S4 يحتاج تكليفًا مستقلًا.
