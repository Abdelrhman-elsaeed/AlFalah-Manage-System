# S2 — المكتبة والرفع: التنفيذ والتحقق والقيود

**التاريخ:** 2026-10-03 · **النطاق:** S2 فقط · **التفعيل:** OFF · **التحقق الحي:** متعذر بقيد S0.

## التنفيذ

Controller رفيع → `IStorageLibraryService` في Application → repositories في Infrastructure → SQL Server، و`IStorageProvider` يعزل عميل Google الحالي. `StorageOperation` يسجل الطلب والفاعل والمدرسة والبصمة ومعرف العنصر المحجوز والحالة؛ لا اعتماد على اسم الملف وحده للمصالحة. DTOs العامة تعرض المعرفات الداخلية فقط. واجهتا Angular تستعملان shell وPrimeNG وألوان المنصة وRTL والترجمة العربية/الإنجليزية.

- مدير المدرسة الفعلي والمفوّض المباشر: تهيئة «مكتبة المدرسة» تحت الجذر الحالي، إنشاء المجلدات ونقلها، شجرة بتحميل تدريجي، breadcrumbs، بحث في المجلد/عام، فرز ثابت بالاسم/الحجم/الوقت ثم ID، صفحات وقائمة/بطاقات، رفع وتفاصيل ونسخ ومعاينة/تنزيل.
- المعلم: `/instructor/my-files`، مجلده الممنوح فقط وملفاته، رفع مستقل عن المهمة، تقدم وإلغاء قبل الإرسال وإعادة محاولة بالمفتاح نفسه. المسار القديم لملفات الإنجاز والمصفوفة محفوظان.
- الحالات: اتصال غير متاح، لا منحة، مكتبة لم تُهيّأ، تحميل/فراغ/خطأ، `Managed`، `Unindexed`، `MissingFromDrive`، وعمليات `Pending/Completed/Failed/NeedsAttention`. 403 يمحو بيانات الملف والمعاينة المعروضة.
- المجلدات المكتشفة داخل النطاق تُفهرس كعقد SQL؛ ملف Drive بلا ledger يظهر «غير مفهرس» بلا raw ID/رابط عام/تنزيل/احتساب. فهرسة وربط الملفات بإذن مستقل عمل لاحق؛ لم يبدأ S3 أو S6.

## عقود HTTP المنفذة

كل JSON داخل `ApiResponse<T>`، والبايتات `FileStreamResult` مصرح. المدرسة من `ActiveSchoolId` والعضوية الحية، وليست من body/query.

| الطريقة والمسار تحت `/api/v1/storage` | المدخل/النتيجة |
|---|---|
| `GET context?own=true/false` | سياق المدرسة والسنة النشطة، `canManage`, `isTeacher`, `connectionState`, `rootFolderId` داخلي |
| `GET folders?own=&parentFolderId=&page=&pageSize=` | صفحات مجلدات؛ null parent يعرض الجذر المفهرس المصرح |
| `POST folders` | `{parentFolderId?,displayName,requestKey}`؛ null parent يهيئ الجذر باسمه المعتمد؛ مفتاح داخلي ثابت للمدرسة/الوجهة/الاسم يمنع ازدواج الإنشاء |
| `PATCH folders/{id}/parent` | `{parentFolderId,rowVersion}`؛ منع نقل الجذر والدورات وتجاوز النطاق/المجلدات المحمية |
| `GET folders/{id}/drive-items?own=&pageToken=` | اكتشاف Drive بصفحات 25 ورمز Google opaque؛ `Managed/Unindexed` ومعرفات داخلية عند وجودها |
| `GET files` أو `GET me/files` | `folderId,search,global,sort=name/size/date,descending,page,pageSize`؛ `items,total,page,pageSize` |
| `GET files/{id}` | بطاقة الملف ونسخه؛ `isProtected,rowVersion` وحالة التوفر |
| `GET files/{id}/content?preview=true/false` | تحقق وصول حي ثم stream؛ inline للأنواع المصرحة فقط، وإلا attachment |
| `POST files` أو `POST me/files` | header `Idempotency-Key`؛ multipart: `parentFolderId` اختياري ثم `length` ثم `file` واحد؛ metadata تسبق المحتوى |
| `POST operations/{id}/reconcile` | المالك الأصلي للعملية وبنفس المدرسة والصلاحية الحالية؛ لا رفع جديد؛ الحالة ومعرف الأصل/النسخة عند اكتمالها |
| `PATCH files/{id}/name` | `{displayName,rowVersion}`؛ الاسم فقط دون تغيير الامتداد أو هوية النسخة |
| `DELETE files/{id}` | body `{rowVersion}`؛ مهملات Drive وحذف منطقي في SQL، وتكرار الحذف المثبت آمن |

صفحة SQL افتراضية 25 وحدها 100؛ page بين 1 و100000، search حتى 200 حرف، token اكتشاف حتى 2048، مفتاح الرفع ASCII ظاهر بين 1 و128 حرفًا. أخطاء المدخل 400، الوصول 401/403، المورد الغائب/الراية المغلقة 404، اختلاف بصمة المفتاح/rowversion/تعارض SQL 409، الاتصال/حفظ الرفع غير المتاح 503. الرفع المكتمل 200؛ حالة غير مكتملة عند إعادة المحاولة 202 داخل envelope، ولا رسالة نجاح قبل تثبيت الأصل والنسخة.

## stream والمصالحة

1. التحقق من المدرسة والفاعل والصلاحية والمنحة وحدود المجلد قبل قراءة المحتوى أو الاتصال بالكتابة.
2. `MultipartReader` في API الجديدة يمرر stream، دون `IFormFile`/تحميل المحتوى الكامل في الذاكرة. spool مؤقت `DeleteOnClose` وbuffer 64 KiB للتحقق من طول المحتوى الفعلي والتوقيع وSHA256 قبل إرساله إلى Google. تنظيف spool في النجاح والفشل والإلغاء.
3. الملف حتى **262144000 bytes (250 MiB)**؛ الطلب حتى **263192576 bytes** لفصل multipart overhead. PDF/Office/JPEG/PNG/MP4/MOV/WEBP/HEIC بحسب قرارات S0؛ MIME تحدده الخدمة، لا ثقة بنوع العميل. فحص magic bytes وOOXML container والمكون المناسب ومنع `vbaProject.bin`؛ منع أسماء المسارات/control/bidi formatting. هذا تحقق نوع وحجم، وليس خدمة فحص فيروسات.
4. Google `generateIds` يحجز ID، ثم SQL يثبت العملية بهذا ID وبصمة الاسم/الحجم/المحتوى/الوجهة/المهمة قبل إرسال stream. Google تدعم المعرف المحجوز للملفات الثنائية والمجلدات وفق [دليل إنشاء الملفات](https://developers.google.com/workspace/drive/api/guides/create-file) و[مرجع generateIds](https://developers.google.com/workspace/drive/api/reference/rest/v3/files/generateIds).
5. SQL يثبت الأصل والنسخة والمؤشر والعملية والتدقيق في transaction واحدة؛ الفهارس الفريدة وrowversion يمنعان أصلين لنفس النتيجة. عند فشل SQL بعد نجاح Drive يبقى `Pending` المحفوظ مسبقًا أو `NeedsAttention`؛ رد ضائع لا يؤدي لنسخة جديدة.
6. إعادة نفس المفتاح تتحقق من البصمة ثم تبحث عن **ID المحفوظ**؛ اختلاف المحتوى 409. مصالحة عنصر موجود وصحيح الحجم/النوع/الاسم/الوجهة تُكمل SQL. عنصر غائب يظل `NeedsAttention` دون إعادة إرسال تلقائية؛ مراجعة تشغيلية مطلوبة قبل مفتاح جديد. رفض قطعي كامل من provider، مثل quota/auth/rate limit، يسجل `Failed` ولا يعاد إنشاؤه تلقائيًا بالمفتاح ذاته. لا worker خلفي في S2.

إنشاء المجلد له سجل متين أيضًا. لا يوجد transaction موزع بين SQL وGoogle. rename/delete/move تتحقق من الحماية وrowversion داخل transaction SQL وتعيد فشلًا إن لم يثبت الحفظ؛ أثر Google قد يسبق فشل SQL ويحتاج مراجعة/إعادة إجراء مأذونة. سجل عمليات الرفع يغطي المصالحة الآلية هنا؛ لا يدّعي S2 مصالحة خلفية عامة لكل تعديل خارجي.

## الوصول والحماية والتوافق

الصلاحيات من DB والعضوية/النشاط والمدير الحقيقي والتفويض الجاري في كل طلب. Teacher grant الحالي وجذر المدرسة والعنصر تُفحص حيًا؛ ancestry bounded بـ64 عقدة ومخزن metadata خلال الطلب فقط، فلا يحمل المنح القديمة بين الطلبات. إدارة مكتبة المدرسة تمنع تداخل جذورها مع منح المعلمين؛ تعديل منحة المعلم يمنع احتواء المكتبة/الأرشيف. لا وصول تلقائي لدور عام ولا وصول لأرشيف الزيارة قبل سياسة S5.

Missing/trashed أو نقل العنصر خارج الجذر يمنع البايتات ويحدّث التوفر؛ فشل الشبكة وحده لا يوسم الملف مفقودًا. استعادة `Available` تستلزم إثبات وجوده **داخل الجذر المصرح**. للملف الموروث يُصحح `TeacherTaskStatus` القديم دون إعادة كتابة قرار المراجعة. القائمة لا تكشف العنصر المنقول خارج النطاق؛ إجمالي SQL قد يبقى أكبر من عدد العناصر المتاحة في الصفحة حتى التدقيق التشغيلي.

يحظر تعديل/حذف HistoricalImport وVisitArchive وأي أصل له قرار Approved سابق أو رابط Approved أو submission قديمة Approved أو أكثر من نسخة. نقل مجلد يحتوي أصلًا محميًا مرفوض. الاسم المرئي للأصل فقط يتغير قبل الاعتماد؛ `DriveFileName` وهوية النسخة وملاحظات المراجع والقرارات لا تُعاد كتابتها. طلب التغيير واستبدال النسخة غير منفذين حتى S3.

`ReadModelEnabled=true` يمرر كاتب `teacher-drive` القديم (رفع/rename/delete) إلى الخدمة المشتركة مع نفس العقود القديمة؛ reservation القديمة وStorage operation قبل Drive في transaction واحدة. رفع المهمة القديمة يحافظ على submission/mصفوفتها PendingReview، ولا ينشئ رابط S3. الملفات الموروثة يلزم backfill قبل تعديلها عبر الفرع الجديد. `teacher-drive-admin` يبقى إعداد الاتصال/المنح الحالي ويستخدم قواعد الحارس نفسها مع منع تداخل المكتبة. عندما الراية OFF تبقى الكتابة القديمة كما كانت، مع إصلاح حظر تعديل/حذف Approved مباشرة. لا كاتبين نشطين لنفس الطلب.

الرفع المستقل الجديد `NeedsLink=true` وبلا EvidenceLink/TeacherTaskStatus؛ لا يُحتسب شاهدًا. S1 backfill ليس dual-write tool: تشغيله مجددًا بعد كتابات الفرع الجديد قد يبلّغ drift بدل كتابة provenance/قرارات جديدة. لذلك cutover يتطلب توافق المصفوفة/الروابط وترحيلها في S3 وتدقيق نهائي مستقل.

المعاينة ببايتات API مصرح بها، لا رابط Google عام؛ PDF/JPEG/PNG/WEBP/MP4 حتى 20 MiB في Angular لتقييد blob preview. Office/MOV/HEIC أو الأكبر لها تنزيل ورسالة بديل. blob URLs تُلغى عند الإغلاق/المغادرة/403، ونسخ الرابط ينتج URL داخليًا يتطلب الدخول. `private,no-store`, `nosniff`, CSP sandbox وPDF iframe sandbox. التنزيل forward-only stream؛ HTTP range غير مفعّل، فلا ادعاء seek/استئناف للوسائط الطويلة. Angular download يستقبل blob عبر HTTP المصادق؛ حد الرفع ليس حدًا للذاكرة على جهاز المستخدم عند التنزيل.

## التحقق المسجل

- الاختبارات الكاملة للـbackend: **818 ناجحة، صفر فشل/تخطٍّ، بينها 27 اختبار SQL Server فعليًا**؛ تفاصيل العدد النهائي في [ملخص التحقق](s2-checks.json). تشمل S1 backfill/history/FKs/rowversion/التفويض مع S2 العزل والملكية وسحب المنح، حدود Drive والدورات، Approved قديم، المصفوفة، replay/اختلاف بصمة المفتاح، فقد الرد، quota، انقطاع SQL بعد Drive، مصالحة بسياق جديد، race بنفس المفتاح، فقد/نقل الملف، 250 MiB exact ورفض الزيادة/signature/name.
- SQL S2 في collection وقاعدة مستقلة عن S1؛ كل تشغيل يحتاج قاعدة `AlFalahS1Tests_*` جديدة، والـLibrary suffix مستقل. migrate من مخطط legacy ثم S1/S2، مع sentinel مشفر محفوظ كما هو. لا reset/drop ضمن الاختبارات.
- Angular: 22 اختبارًا لعميل storage وصفحته وshell. Playwright: 12 رحلة ناجحة، 6 لكل من Chromium سطح المكتب وPixel 5؛ RTL وعدم تجاوز العرض، delegated Secretary بلا دور مدير، own endpoints، preview عبر API، إلغاء قبل الإرسال، حفظ المفتاح بعد 503/202، غياب المنحة/الاتصال، Office محمي بلا تعديل، و403 يمحو البيانات. صور سطح المكتب/الهاتف روجعت بصريًا في `frontend/test-results/storage/` (ignored).
- build الإنتاجي Angular ناجح؛ chunk الصفحة lazy نحو 114.78 kB raw /18.65 kB estimated transfer. تحذيرات CSS budget الحالية في visits-v2/dashboard-live و3 selectors من PrimeNG خارج S2؛ لا أخطاء build. تبعية .NET8 للمشروع ثابتة؛ المضيف المحلي .NET10 يستخدم `DOTNET_ROLL_FORWARD=LatestMajor` للتحقق دون تغيير packages.
- [قياس الأداء](s2-performance.json): 5000 أصل/نسخة في SQL معزول، بحث باسم وترقيم الصفحة 100 بحجم25 وفرز ثابت، warmup ثم10 قياسات. الهدف<200ms تحقق محليًا؛ `serviceWithFakeDriveMs` قياس خدمة مع provider محاكاة **لا Google حي**. لا يُستنتج زمن HTTP/Google أو حمل الإنتاج من هذا القياس.

## إعادة التحقق بأمان

```powershell
$env:DOTNET_ROLL_FORWARD='LatestMajor'
$env:ALFALAH_STORAGE_TEST_CONNECTION='Server=(localdb)\MSSQLLocalDB;Database=AlFalahS1Tests_S2_'+[guid]::NewGuid().ToString('N')+';Integrated Security=true;TrustServerCertificate=true'
$env:ALFALAH_STORAGE_PERF_REPORT=(Join-Path $PWD 'docs/specs/school-file-storage/verification/s2-performance.json')
dotnet test backend/AlFalah.Tests/AlFalah.Tests.csproj --verbosity minimal
```

```powershell
# من frontend، دون تشغيل API أو تعديل SQL الفعلية:
npx ng test --watch=false --browsers=ChromeHeadless --include='src/app/features/storage/**/*.spec.ts' --include='src/app/shared/layout/shell/*.spec.ts'
npx playwright test --config playwright.storage.config.ts
npm run build
```

كل API في Playwright محاكاة؛ devserver Angular فقط. لا تُشغّل backend الحقيقي لأجل هذه الرحلات، إذ startup Development يطبق migrations تلقائيًا.

## قيد Drive والتفعيل والرجوع

نتيجة S0 باقية: اتصال Development لا يفك بيانات الاعتماد بالمفاتيح الحالية؛ المراقب الحي أرسل **صفر طلب Google**. لم تُغيّر بيانات الاعتماد أو Data Protection keys أو إعدادات الاتصال، ولم تُجر كتابة Drive حقيقية أو migrate/backfill لأصل Development خلال S2. لم يُعد تشغيل probe مع مفاتيح بديلة لتجاوز القيد.

**التحقق الحي المتعذر:** initialize/list/discovery/upload250MiB/preview/download/rename/trash/move على اتصال المدرسة الحقيقي، metadata/Shared Drive وquota، فقد رد Google وحفظ SQL المتكامل، ancestry/root grants الفعلية، زمن القوائم والتنزيل والمصالحة الحي. اختبارات البدائل/SQL والمتصفح لا تستوفي هذه البوابة.

`SchoolFileStorage:AdministrationEnabled=false` و`SchoolFileStorage:ReadModelEnabled=false` في defaults؛ لا cutover. قبل التفعيل: اتصال صالح ومفاتيح التشغيل الأصلية دون استبدال الاعتماد ضمن هذه المهمة، تدقيق الجذور والمنح المتداخلة، نسخة احتياطية، migrate/backfill/dry-run/drift ومطابقة Approved، قبول parity S0، تحقق Google حي وQA متكامل، وإنجاز بوابات كاتب/قارئ المصفوفة المشترك S3. تهيئة المكتبة عملية صريحة للمدير/المفوّض وليست كتابة Drive عند startup.

الرجوع يغلق الرايتين ويحفظ schema/ledger/audit/القرارات والملفات. migrations Down ترفض حذف التاريخ؛ لا drop أو purge أو تعويض يحذف أصلًا مجهولًا تلقائيًا. [SQL الترحيل القابل للمراجعة](s2-migration.sql) additive فقط من نهاية S1. التوقف بعد S2؛ S3–S6 لم تبدأ.
