# S5 — تحقق أرشفة تقرير الزيارة المعتمدة

**التاريخ:** 2026-10-05. **الحالة:** تنفيذ S5 فقط؛ التحقق على SQL Server جديد ومعزول وDrive محاكاة وAngular بعقود API محاكاة. الرايات الأربع OFF. لا S6 أو cutover أو backfill لاعتمادات قديمة. [النتائج الآلية](s5-checks.json) تسجل الأعداد والبصمات النهائية؛ [المرحلة](../phases/05-approved-visit-pdf-archive.md)، [القرار D-98](../../../14-DECISIONS-AND-DEVIATIONS.md).

## ما نُفذ

امتداد VisitArchiveOperation / VisitArchiveArtifact القائمة، مع Controller → Application Service → Repository → Database وDrive adapter القائمة؛ لم تُنشأ منظومة تخزين موازية. JSON داخل ApiResponse<T>، والبايتات عبر endpoint مصرح مع private/no-store وnosniff وsandbox. المصادقة وrate limit الحالية مطبقان.

VisitV2Service.ApproveAsync وFinalizeAsync يزيدان ApprovalRevision فقط عند الانتقال الحقيقي إلى Approved. rowversion يمنع اعتمادين متزامنين من حفظ نفس الانتقال، وunique `(VisitId, ApprovalRevision)` يمنع عملية مكررة. visit/analysis/outbox/snapshot/audit تحفظ في نفس معاملة SQL. فشل إدراج outbox يرجع الانتقال والعداد والتحليل معًا. إنهاء PendingApproval والرفض وإعادة الفتح لا تنشئ اعتمادًا. قواعد اعتماد V2 الحالية وصلاحياتها محفوظة.

تجمد العلامة وإعدادات التقرير وصور التواقيع المخولة قبل المعاملة؛ snapshot يحتوي VisitV2DetailDto كاملًا وVisitV2PdfAssetSources المجمدة ووقت الاعتماد الثابت. لا PDF أو Google في طلب الاعتماد، ولا قراءة للزيارة المعدلة بواسطة العامل. روابط صور Google المباشرة محجوبة، وHTTP الأصول لا يتبع redirects؛ أصل غير متاح يستخدم fallback محرك V2. صور PNG/بيانات محلية تدعم التجميد مع حد ImageAssetLoader القائم 2 MiB لكل صورة. snapshot وبصمته والبايتات المعدة والبصمة SQL immutable؛ لا deletion. SnapshotSchemaVersion=1؛ لا استنتاج snapshot للاعتمادات القديمة.

الفردي وZIP الرسميان يستخدمان اللقطة للاعتمادات الجديدة دون انتظار Drive أو العامل. القديم بلا snapshot يبقى على مسار V2 السابق. توليد الأرشيف يستعمل مولد VisitV2DocumentService المشترك، مع Amiri والتقرير الكامل والتحليل وخطط التحسين والتواقيع والعلامة وترقيم الصفحات؛ لا قالب مختزل جديد.

## العامل وحدود الاستعادة

- كل 15 ثانية، batch حتى 25 عملية. المطالبة SQL conditional update بحالة Processing وtoken وlease دقيقتين؛ heartbeat عبر اتصال مستقل كل 20 ثانية. انتهاء الحجز يسمح بالاستعادة بعد crash.
- قفل SQL `sp_getapplock` من نوع Session لكل عملية يمسك طوال العمل والرفع، حتى إن انتهى lease؛ عامل ثانٍ/retry لا يتداخلان. فقد SQL ينهي قفل الجلسة، لكن معرف Drive المحجوز ذاته يبقى سياج منع ملف ثانٍ عند انتهاء الطلب البعيد لاحقًا.
- PDF بحد 50 MiB، وبايتاته وبصمته تحفظ أولًا في SQL. حجز المجلد تحت الجذر المضبوط ثم معرف الملف وuploadIdentity وSchoolId/VisitId/revision/generation/hash محفوظة قبل البايتات. appProperties خاصة بالتطبيق؛ لا هوية بالاسم.
- قبل كل إرسال/إقرار: metadata والجذر والمنح وMIME والحجم وappProperties والبصمة الفعلية لمحتوى الملف. نجاح Drive وفشل SQL أو فقد الرد يعالجان بفحص المعرف المحفوظ، ثم إقرار SQL دون ملف ثانٍ. lookup غير معلوم/transport failure لا يطلق upload. إذا أكد المزود غياب المعرف، تجرّب العملية نفس المعرف؛ existing/conflicting identity لا تعتبر نجاحًا ولا تستخدم معرفًا جديدًا تلقائيًا.
- إقرار StoredFile/StoredFileVersion/Artifact/current pointer/Completed/audit في معاملة واحدة. قفل صف Visits UPDLOCK/HOLDLOCK يقرأ الحالة والrevision الحاليين؛ عامل قديم متأخر يبقى تاريخيًا ولا يمحو Current أحدث.
- خمس محاولات كحد أقصى؛ backoff يبدأ 30 ثانية ويتضاعف بسقف ساعة. بعدها NeedsAttention؛ IdentityMismatch يذهب مباشرة إلى NeedsAttention. retry مصرح يعيد عداد المحاولات دون اعتماد جديد. Pending/Processing والمحجوز وCompleted المتاح يرفضون retry بـ409.

مجلد v1 واحد «أرشيف الزيارات» تحت جذر المدرسة المضبوط؛ لا وجهة من العميل ولا شجرة سنة/معلم. يتحقق من الجذر والمنح الحالية ويرفض التداخل في الاتجاهين بين الأرشيف ومنحة المعلم. نقل المجلد خارج الجذر أو حذفه/إرساله لسلة المهملات أو فقد الملف/تحريف هويته يمنع نجاحًا زائفًا.

المصالحة كل 15 دقيقة بصفحات 100، وكذلك عند قراءة الأرشيف؛ MissingFromDrive مبنية على تحقق خارجي، بينما فشل النقل لا يساوي إثبات فقد. لا استعادة تلقائية للمكتمل المفقود. المسؤول المصرح يرسل RecreateMissing وسببًا؛ نفس snapshot والبايتات تستخدم مع generation جديدة ونسخة StoredFileVersion جديدة، وOriginalStoredFileVersionId يبقى ثابتًا. تدقيق الاستعادة يحتفظ بالمسؤول والسبب. رجوع الملف الأصلي إلى مكانه المصرح يظهره Available ويمنع استعادة زائدة. إعادة فتح/اعتماد تحتفظ بكل artifacts/history؛ أثناء انتظار الجديد لا يظهر القديم Current.

## العقود والصلاحيات

SchoolId يستنتج من ActiveSchoolId على الخادم. لا بارامتر مدرسة/DriveId/مجلد/URL يقبله العميل.

| العقد | السلوك |
|---|---|
| `GET /api/v1/storage/visits?From&To&TeacherId&Status&Page&PageSize` | إدارة المدير/المفوّض؛ صفحة 1..100000، حجم 1..100، الافتراضي25؛ تنازلي بأحدث وقت اعتماد ثم VisitId. فترة UTC وTeacherId حقيقي وحالة enum أو MissingFromDrive، والتصفية SQL. الزيارة تظهر إذا طابقت إحدى revisions الفلاتر؛ يعرض صفها التاريخ كاملًا. |
| `GET /api/v1/storage/visits/teachers` | مدير/مفوّض، معلمون من زيارات المدرسة المؤرشفة فقط، مميزون ومرتبون، سقف1000. |
| `GET /api/v1/storage/visits/{visitId}/archive` | الحالة ووقت الاعتماد/المحاولة/النجاح/المحاولة التالية، سبب آمن، Current وversions. المدير/المفوّض يرى التاريخ؛ المعلم يقتصر على اعتماد زيارته الحالية Approved. |
| `POST /api/v1/storage/visits/{visitId}/archive/retry` | JSON `{ approvalRevision, recreateMissing: false, reason: null }`، 202؛ ViewArchive وRetryArchive وصلاحية الزيارة الحالية. الاستعادة تتطلب true وسببًا غير فارغ حتى1000 حرف. لا revision جديد. |
| `GET /api/v1/storage/visits/{visitId}/archive/{revision}/content?versionId` | PDF stream بعد التحقق من الزيارة والمدرسة والجذر والهوية والبصمة. versionId داخلي لنفس StoredFile والاعتماد؛ القديم للمدير/المفوّض فقط. المعلم لا يحدد اعتمادًا سابقًا أو نسخة استعادة سابقة. |

المدير الحالي/المفوّض المباشر يحتاج Storage.ViewArchive وVisit.View، وStorage.RetryArchive للإعادة. يفحص العضوية والصلاحيات والتفويض/الانتهاء/السحب في DB على الطلب ثم بعد I/O. Moderator ذو صلاحية الأرشيف يقتصر على زياراته المنشأة وفق بوابة V2، ولا يرى قائمة الإدارة؛ Instructor يرى زيارته Approved فقط مع Storage.ViewOwn، بلا إدارة أو تقارير زملاء. لا shortcut لدور منصة عام.

الرد لا يفضح snapshot أو بايتات أو Drive/provider IDs أو أسرار أو روابط عامة. ErrorCode مقيد بـArchiveUnavailable / StorageUnavailable / IdentityMismatch؛ الواجهة تعرض ترجمة آمنة. الـAPI يعيد403 للسحب/النطاق،404 للغائب/المعطل،409 للتعارض. تفاصيل/versions/content في storage/files وEvidenceLink/history/create محجوبة للأرشيف، وLibrary lists وreadiness/index/export لا تضمه. لا EvidenceLink أو قرار أو جاهزية تلقائية نتيجة الأرشفة. الربط اللاحق يحتاج سياسة تقرير ومراجعة مستقلة؛ لم توسع S5 المكتبة أو الربط.

متصفح teacher-drive القديم يستبعد هوية الأرشيف من القائمة ويرفض details/content/breadcrumb، حتى لو نقل Google الملف إلى مجلد المعلم أو نزعت appProperties. مستودع الأرشيف يجمع IDs المجلد والعمليات المحجوزة وجميع StoredFileVersions في استعلام صفحة واحد؛ guard تستخدم metadata الخاصة كذلك للملف غير المقر بعد. فحص ancestry البحت المستخدم للتحقق من منح المجلدات يبقى منفصلًا حتى لا يخفي تداخلًا مع مجلد محمي. الاختبار الفعلي ينقل PDF ثم يزيل properties ثم ينقل مجلد الأرشيف إلى grant ويثبت عدم الإفشاء.

هذا lookup للمنع فقط على IDs التي رآها المزود الحالي يشمل هويات الأرشيف المعروفة حتى من مدرسة أخرى: نقل خارجي بين المدارس لا يحولها إلى ملف معلم عادي. لا يعيد school/revision/records أو بيانات تقرير؛ كل قراءة/إدارة إيجابية تبقى مقيدة بالمدرسة وصلاحية الزيارة الحالية.

الاكتشاف native في مكتبة S2 يستخدم نفس predicate قبل فهرسة أي مجلد أو عرض الملف Unindexed/Managed؛ نقل الأرشيف إلى مكتبة المدرسة لا يكشف اسمه/هويته ولا يعيد تصنيفه كمجلد مكتبة. الاختبار ينقل الملف والمجلد إلى المكتبة مع properties منزوعة ويحصل على discovery فارغ مع بقاء Kind=VisitArchive.

## الواجهة

`/school-manager/storage/visits`: RTL وهوية المنصة، صفوف مرقمة حسب الصفحة، فلاتر فترة/معلم/حالة محفوظة في URL وترتيب ثابت، حالات العملية والوقت والمحاولات والنجاح والتعثر الآمن، إعادة محاولة وتاريخ approvals/versions وتنزيل مصرح. MissingFromDrive يفتح سبب استعادة إلزاميًا. Pending approval archive جديد يميز السابق التاريخي. تحميل/فراغ/خطأ/403 يمسح البيانات؛ 403 يمسح lookup المعلمين أيضًا. شارة في report تفاصيل V2 تعرض الحالة وتنزيل الأثر الحالي المتاح؛ زر التقرير الرسمي مستقل عن تأخر الأرشفة. الواجهة لا تصطنع حالة نجاح بعد مجرد POST.

## التحقق والأداء

[s5-checks.json](s5-checks.json) المرجع النهائي لأعداد الانحدار/builds. VisitArchiveSqlTests تستخدم **SQL Server LocalDB جديدًا** بخدمات V2 والمولد والمستودعات الحقيقية؛ المزود فقط fake. تشمل اليدوي/التلقائي وatomic rollback والتكرار والتزامن وworkerين وانتهاء lease أثناء upload والrestart وتجديده وGoogle failure/lost response وSQL failure بعد الرفع وlate old worker/reopen/reapprove وفقد/trash/move/folder move والاستعادة وimmutable history وبواباتOFF والمنح المتداخلة والمدرسة والمعلم وسحب التفويض والمسارات العامة والفهرس/بيانات التصدير. سكربت S5 يطبق مرتين من S4 على قاعدة جديدة مع sentinel محفوظ وpending-model=false؛ regression S3/S4 محدث لحدوده التاريخية مع migration اللاحقة.

Angular unit transport/filters/safety مع انحدار S2–S4 وV2، و50 رحلة Playwright (25 سطح/25 موبايل، منها10 S5) بعقود API محاكاة. يبدأ Angular وحده؛ **لم يبدأ API الحقيقي**. الشاشات `.audit` و`frontend/test-results/storage` مخرجات محلية ignored، لا بيانات تشغيلية في git.

[مقارنة PDF](s5-pdf-comparison.json): تقريرا اعتماد يدوي وتلقائي فعليان، كل منهما4 صفحات، مع archive/official/ZIP. مقارنة pixels لكل صفحة عند1.5x، embedded Amiri Regular/Bold، logo وثلاث PNG fixtures مخولة للتواقيع، school/header/footer و25/100 وخمسة مجالات وخطة المعالجة وترقيم1..4. فحص bounding boxes دون overflow، ومراجعة أول/آخر صفحة بصريًا لكلا المسارين. اختلاف SHA256 بين renders سببه metadata PDF؛ الصور متطابقة. **المحرك الحالي QuestPDF2024.3.4 يصور العربية صحيحًا لكنه يولد shaped ToUnicode0000؛ البحث/النسخ العربي غير موثوق، ولم يسجل كناجح.** لا تغيير package أو إصلاح محرك خارج S5.

[الأداء](s5-performance.json) يقيس10 طلبات بعد warmup على5000 عملية metadata اصطناعية Pending، page100/25. لا SnapshotJson أوPdfBytes مختارة في القراءة ولا توليد5000PDF؛ query count ثابت. قياسات generation/upload simulated وحجم PDF في comparison. القياس ليس throughput إنتاجيًا: قراءة completed history تتحقق من المحتوى والبصمة لدى المزود وقد تعيد تحميل PDF؛ latency وحجم الشبكة وquotas وعدد teacher grants وعمق التاريخ الفعلي في Google لم تقاس. لا ادعاء أداء Google من fake upload. بايتات PDF المجمدة محفوظة SQL لتحقيق استعادة حتمية؛ التخطيط لسعة SQL والنسخ الاحتياطي لازم قبل التشغيل.

| القياس النهائي المحلي | النتيجة |
|---|---|
| قائمة 5000 عملية metadata، صفحة100/25، 10 عينات | median32.4545ms، max46.395ms، 21 SQL queries ثابتة، لا report blobs |
| توليد PDF الاعتماد اليدوي / التلقائي | 409.8688ms / 404.6459ms |
| upload محاكى يدوي / تلقائي | 0.1354ms / 0.0755ms؛ لا network latency حقيقية |
| حجم PDF يدوي / تلقائي | 279616 / 279926 bytes، 4 صفحات لكل تقرير |
| انحدار backend / Angular / browser | 883 / 55 / 50 ناجحة، دون تخطي backend؛ 25 حالة SQL جديدة لـS5 |

## التشغيل والمراقبة والرجوع

`SchoolFileStorage` defaults: AdministrationEnabled=false، ReadModelEnabled=false، ArchiveWorkerEnabled=false، ArchiveExternalWritesEnabled=false. العامل لا ينشئ scope/SQL/Drive وهوOFF. تفعيل العامل والكتابة يتطلب الأربع؛ التحقق شغّل الخيارات في الاختبارات المعزولة فقط. snapshot/outbox يحفظان للانتقالات الجديدة بعد تطبيق S5 حتى معOFF؛ يتراكم Pending دون توليد أو رفع. لا queue/backfill جماعي للأقدم.

تطبق إعدادات العامل عند بدء الخدمة؛ خطة تغيير الرايات التشغيلية تشمل إعادة تشغيل مضبوطة وتحققًا من القيم الأربع. لا يعتمد التفعيل أو الرجوع على تغيير ملف إعداد أثناء رفع قائم؛ أوقف الدورة/الخدمة وانتظر الطلب الجاري ثم صالح المعرف المحفوظ.

قبل تشغيل فعلي بتكليف مستقل: حل قيد اعتماد S0 دون تغيير credentials/keys ضمن هذا العمل، تحقق حي مصرح من جذر المدرسة والمنح والحدود وسلوك reserved IDs/appProperties/content والquotas، مراجعة snapshot assets/fallback والتواقيع، سعة SQL والنسخ الاحتياطي وتطبيق migration في نافذة معتمدة، قبول المالك للواجهة/parity، ثم بوابة العامل/الكتابة الصريحة ومراقبة عينة اعتماد مصرح بها. لا تستخدم تشغيل Development API كأداة migration أو ملفات اختبار وهمية على Google. لا فتح تلقائي لهذه البوابات.

Meter `AlFalah.Storage.Archive`: counters archive.claims / archive.failures (safe code) / archive.expired_leases، histograms archive.pdf.ms / archive.upload.ms / archive.pending.seconds. Audit `Storage.Archive.*` يشمل Created/Claimed/PdfPrepared/UploadReserved/UploadStarted/Completed/Failed/Retry/RecreateAuthorized/Reconciled/MissingFromDrive/Current/Historical، مع school/visit/revision/operation فقط وactor/reason للإجراء البشري؛ لا OAuth أو PDF. السجلات تعرض operation/revision/code فقط. مراقب DB يستطيع حساب Pending/RetryScheduled/NeedsAttention وأقدم CreatedAtUtc وعددAttempts وحجوزاتProcessing المنتهية دون قراءة snapshots/bytes، وإنذار تأخرالطابور/تكرار failures؛ التشغيلOFF يفسر Pending ولا يعد فشل اعتماد.

الرجوع: أغلق ArchiveExternalWritesEnabled وArchiveWorkerEnabled ثم رايتي القراءة/الإدارة، وأوقف الخدمة/انتظر الرفع الجاري ليكمل المصالحة بالهوية المحفوظة قبل صيانة SQL. لا حذف للمجلد أو الملف أوالعملية أواللقطة أوالنسخ/التاريخ، ولا تغيير Approved. Down يرفض الحذف. إعادة التشغيل تستخدم الحجز والهوية المحفوظين؛ لا تعديل SQL عشوائي لإعلانCompleted. استعادة المفقود من API مسؤول معللة فقط؛ السجل الناجح المتاح لا يعاد.

## إعادة التحقق الآمنة

من جذر المستودع، اختر قاعدة جديدة فريدة؛ fixture يرفض قاعدة موجودة/اسم غير معزول وIntegrated Security غير محلي. لا يفتح Development config/API؛ تبقى قواعد الاختبار للفحص ولا تحذف تلقائيًا.

```powershell
$env:DOTNET_ROLL_FORWARD='LatestMajor'
$env:ALFALAH_STORAGE_TEST_CONNECTION='Server=(localdb)\MSSQLLocalDB;Database=AlFalahS1Tests_S5_'+[guid]::NewGuid().ToString('N')+';Integrated Security=true;TrustServerCertificate=true'
New-Item -ItemType Directory -Force .audit/sfs-s5/exports | Out-Null
$env:ALFALAH_S5_EXPORT_DIRECTORY=(Join-Path $PWD '.audit/sfs-s5/exports')
$env:ALFALAH_S5_MIGRATION_SCRIPT=(Join-Path $PWD 'docs/specs/school-file-storage/verification/s5-migration.sql')
$env:ALFALAH_S5_PERFORMANCE_REPORT=(Join-Path $PWD 'docs/specs/school-file-storage/verification/s5-performance.json')
dotnet test backend/AlFalah.Tests/AlFalah.Tests.csproj --logger 'trx;LogFileName=s5.trx' --results-directory .audit/sfs-s5
python docs/specs/school-file-storage/scripts/verify-s5-pdfs.py
dotnet build backend/AlFalah.Api/AlFalah.Api.csproj -c Release
```

```powershell
# من frontend، Angular فقط وAPI محاكاة:
npx playwright test --config playwright.storage.config.ts
npx ng test --watch=false --browsers=ChromeHeadless --include='src/app/features/storage/**/*.spec.ts' --include='src/app/shared/layout/shell/shell-navigation.spec.ts' --include='src/app/features/visits-v2/visit-workspace/visit-workspace.component.spec.ts'
npm run build -- --configuration production
```

سكربت PDF يعتمد PyMuPDF المحلي. [إرشادات Google الرسمية للمعرفات المحجوزة والـretry](https://developers.google.com/workspace/drive/api/guides/manage-uploads)، [appProperties](https://developers.google.com/workspace/drive/api/guides/properties) تدعم العقد؛ الاختبارات محاكاة لا إثبات Google حي. الأصل Development والمفاتيح والاعتماد لم تتغير، لاGoogle HTTP/write ضمن التحقق. انتهى S5؛ S6 وcutover غير منفذين.
