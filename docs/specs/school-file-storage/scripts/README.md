# أدوات الجرد والقياس وتجهيز تجربة Drive

## تجهيز حساب مدرسة الاختبار

`DrivePilotSetup` أداة offline مقيدة بـ LocalDB واسم مدرسة `Al-Falah E2E Test School` ومديرها الفعلي. لا migrations/seeding أو Google HTTP أو توليد مفاتيح، ولا استبدال اتصال نشط أو مجلد موجود. تعتمد مفاتيح Data Protection الحالية، ولا تطبع الأسرار. اقرأ [تقرير التجربة وحدودها](../verification/drive-settings-and-pilot.md).

```powershell
dotnet run --project docs/specs/school-file-storage/scripts/DrivePilotSetup -- --repository . --school-id 18 --client-file 'C:\private\oauth-web-client.json' --email 'pilot@example.com'
# أضف --apply لحفظ draft مشفّر غير مفعّل بعد dry-run.
```

لبدء API محلي بعد إيقاف النسخة القديمة من طرفيتها:

```powershell
powershell -NoProfile -File docs/specs/school-file-storage/scripts/start-drive-pilot.ps1
```

يرفض السكربت المنفذ المستخدم ويترك عمليته كما هي. يضبط callback على 5264 وcompletion على Angular 4200 للعملية فقط، ويمنع startup migrations/seeding ويغلق رايات التخزين الأربع. إعداد الاتصال محفوظ في المدرسة؛ موافقة Google ثم اختيار المجلد تتمان من الواجهة. لا تُشغّل سكربت الجرد التالي باعتباره بديلًا لاختبار Google الحي.

## أدوات S0 الأصلية

تشغل من جذر المستودع. اقرأ [تقرير القيود والنتائج](../baseline/README.md) قبل استخدام الأرقام. لا تشغّل البروتوتايب أو API للحصول على هذه القياسات: بدء API قد يهاجر/يزرع SQL ويشغّل reconciliation.

## جرد المصادر

```powershell
python docs/specs/school-file-storage/scripts/inventory-prototype.py --source 'C:\Users\abdelrhman\Downloads\Telegram Desktop\[DEVELOPER_SYSTEM_CORE - DO_NOT_MODIFY]\[DEVELOPER_SYSTEM_CORE - DO_NOT_MODIFY]'
```

Python 3.9+، standard library فقط. يقرأ ملفات .py/.html/.json/.csv/.md في المجلد المرجعي قراءة ساكنة ولا يستورد كودها. الأصل المفحوص BASE_DIR هو والد core وفق تنظيم الحزمة المقدمة. المخرج `prototype-inventory.json` و`source-manifest.md`، مع فحص أن ملفات المصدر لم تتغير أثناء القراءة. الملفات الأصلية ومساراتها وأسماء المعلمين لا تُنسخ للمخرجات؛ التفصيل يتتبع source ordinal/hash. التوقيت في المخرجات سيتغير عند إعادة القياس.

## قاعدة Development المحلية

```powershell
powershell -NoProfile -File docs/specs/school-file-storage/scripts/measure-runtime-baseline.ps1
```

يقرأ appsettings.Development.json المحلي، يرفض SQL بعيدًا، ويستخدم fixed SELECT statements فقط. المخرج `runtime-baseline.json`؛ لا startup أو audit writes أو migrations/seeding. لا يطبع connection string أو أسماء أشخاص أو محتوى ملفات أو معرفات Google. `SchoolKey/TeacherKey/YearKey/TaskKey` مفاتيح SQL محلية للفصل بين المجموعات؛ ليست معرفات Drive.

## مراقب Drive

```powershell
dotnet build docs/specs/school-file-storage/scripts/DriveBaseline --verbosity quiet
dotnet --roll-forward Major docs/specs/school-file-storage/scripts/DriveBaseline/bin/Debug/net8.0/DriveBaseline.dll 'D:\AlFalah-Manage-System'
```

على جهاز يملك ASP.NET Core 8 يمكن تشغيل `dotnet run --project docs/specs/school-file-storage/scripts/DriveBaseline -- 'D:\AlFalah-Manage-System'` مباشرة. المشروع أداة S0 خارج solution المنتج ويستعمل المكتبات الموجودة؛ لا حزم جديدة أو تغيير target/runtime للتطبيق.

المراقب يقرأ LocalDB الحالي ومفاتيح Data Protection الموجودة باستخدام نفس application name/purpose، مع DisableAutomaticKeyGeneration. NullLogger يمنع تسريب رسائل provider/Google. لا SaveChanges أو استضافة API أو seed. gate شبكة يسمح فقط HTTPS GET إلى www.googleapis.com/drive/v3 وPOST token exchange إلى oauth2.googleapis.com/token؛ يمنع الرفع/التعديل/الحذف. لا تنزيل محتوى الملفات. OAuth token exchange يكتب cache في الذاكرة فقط.

يفحص جذر كل اتصال مفعّل ويقرأ كل صفحات المجلدات حتى 300 طلب HTTP و90 ثانية لكل مدرسة؛ يوقف الدوران بالمجلدات. يطابق IDs في الذاكرة مع ledger ويحسب الملفات غير المفهرسة ومجموعات منح المعلمين. لا يستنتج سنة لملف غير مفهرس؛ تفاصيل المدرسة/المعلم/السنة للشواهد المسجلة في قياس SQL. أسماء الملفات ومعرفات Drive وأجسام HTTP والاستثناءات التفصيلية لا تخرج. الحساب Complete فقط، وما تعذر أو اكتمل جزئيًا تكون أرقامه null.

المخرج `drive-baseline.json`. Unavailable/Partial نتيجة صالحة للمراقبة لكنها ليست اتصالًا ناجحًا أو أرقامًا صفرية. النتيجة الحالية فشل فك تشفير credential قبل HTTP؛ إعادة التشغيل وحدها لا تصلح الاتصال. تتطلب رؤية شاملة أن credential نفسها تستطيع قراءة كل شجرة الجذر؛ لا يمكن للأداة عد ملفات لا يسمح Google لها برؤيتها.

## S1 — backfill تحضيري مستقل

اقرأ [تقرير S1 وخطة الرجوع](../verification/README.md). أنشئ قاعدة SQL محلية تجريبية مستقلة من نسخة COPY_ONLY، ولا تستخدم اسم قاعدة التشغيل في أمثلة الاختبار. عيّن `ALFALAH_MIGRATIONS_CONNECTION` صراحة إلى القاعدة المستهدفة قبل migration؛ design-time factory لا يبدأ API ولا seeder أو reconciliation. راجع SQL الناتج قبل أي نشر خارج الاختبار؛ لم تُطبق migration على قاعدة التشغيل في S1.

```powershell
dotnet build docs/specs/school-file-storage/scripts/StorageBackfill --verbosity quiet
# بعد تطبيق migration على قاعدة الاختبار المعزولة:
dotnet --roll-forward Major docs/specs/school-file-storage/scripts/StorageBackfill/bin/Debug/net8.0/StorageBackfill.dll --repository 'D:\AlFalah-Manage-System' --database 'AlFalahSFS_EXAMPLE' --dry-run --report '.audit/sfs-s1/dry-run.json'
dotnet --roll-forward Major docs/specs/school-file-storage/scripts/StorageBackfill/bin/Debug/net8.0/StorageBackfill.dll --repository 'D:\AlFalah-Manage-System' --database 'AlFalahSFS_EXAMPLE' --apply --report '.audit/sfs-s1/apply-1.json'
dotnet --roll-forward Major docs/specs/school-file-storage/scripts/StorageBackfill/bin/Debug/net8.0/StorageBackfill.dll --repository 'D:\AlFalah-Manage-System' --database 'AlFalahSFS_EXAMPLE' --apply --report '.audit/sfs-s1/apply-2.json'
```

اسم EXAMPLE مثال يجب استبداله باسم قاعدة الاختبار الموجودة، وليس أمر إنشاء/restore. الأداة تقرأ إعداد خادم Development المحلي فقط ثم تختار `--database` المحدد؛ ترفض خادمًا بعيدًا. يتطلب التشغيل schema S3. لا migration أو seed أو Drive/token/decryption داخل الأداة. `--dry-run` لا يحفظ صفوفًا أو audit؛ `--apply` يحفظ graph في معاملة Serializable، مع عدم تحديث legacy. التقارير المجردة تضم مفاتيح SQL وحالات/عدادات وأكواد الاستثناء؛ لا اسم شخص أو معرف Google. خروج 2 يعني وجود استثناء/انحراف يستلزم مراجعة قبل cutover، وليس نجاحًا كاملًا. SHA256/UploadedBy غير المعروفين لا يخمّنان.

اختبارات SQL الاختيارية تحتاج اسم قاعدة **جديدة** لأن fixture يتحقق من migration السابقة ويحفظ شاهدًا قديمًا قبل S1. لا تحذف fixture قواعد التشغيل أو قواعد الاختبار:

```powershell
$env:ALFALAH_STORAGE_TEST_CONNECTION = 'Server=(localdb)\mssqllocaldb;Database=AlFalahS1Tests_' + [Guid]::NewGuid().ToString('N') + ';Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=False'
dotnet test backend/AlFalah.Tests --filter FullyQualifiedName~Storage
```

بدون المتغير تُعلَّم اختبارات SQL skipped صراحة، وتعمل اختبارات الخدمات العادية. تحقق S1 المسجل شغّلها بالمتغير ولم يتخطّ أي اختبار. لا تشغّل API للحصول على baseline أو backfill؛ بدء API يغير البيئة القديمة.

## S3 — إصلاح الكاتب المشترك ومقارنة القراءة

الأداة الآن ترفض اسم قاعدة Development المعرّف في الإعداد، وتقبل فقط قاعدة محلية معزولة باسم `AlFalahSFS_*` أو `AlFalahS1Tests_*`. أمثلة EXAMPLE تشير إلى قاعدة معزولة مُجهزة مسبقًا، ولا تنشئ قاعدة أو تشغّل migration. تشغيل إصلاح S2 يتطلب مخطط S3 كاملًا. لا تسجيل Drive/token/Data Protection أو فتح API.

```powershell
# بعد تجهيز قاعدة معزولة ومراجعة/تطبيق SQL S3 عليها فقط:
dotnet build docs/specs/school-file-storage/scripts/StorageBackfill --verbosity minimal
dotnet --roll-forward Major docs/specs/school-file-storage/scripts/StorageBackfill/bin/Debug/net8.0/StorageBackfill.dll --repository 'D:\AlFalah-Manage-System' --database 'AlFalahSFS_EXAMPLE' --repair-shared-writer --dry-run --report '.audit/sfs-s3/repair-dry.json'
dotnet --roll-forward Major docs/specs/school-file-storage/scripts/StorageBackfill/bin/Debug/net8.0/StorageBackfill.dll --repository 'D:\AlFalah-Manage-System' --database 'AlFalahSFS_EXAMPLE' --repair-shared-writer --apply --report '.audit/sfs-s3/repair-apply.json'
```

شكل التقرير الحالي `{backfill, sharedWriterRepairTargets, dryRun}`. dry-run يكشف أهداف الإصلاح ولا يغيرها، ولذلك قد يبقى `LegacyOrTargetDrift` في backfill حتى apply مستقل صريح. الإصلاح يتحقق من مصدر legacy وعملية S2 Completed والأصل والنسخة الأولى، ويحفظ baseline جديدًا مستقلًا دون إعادة كتابة الأصل الموروث. ثم backfill يحفظ الروابط والقرارات الناقصة بنفس Drive IDs؛ الإعادة لا تضاعفها. أي تعارض هو توقف للمراجعة، وليس سماحًا بإعادة كتابة قرار. الاختبار `Legacy_and_S2_shared_writer_repair_backfill_matrix_and_excel_are_compared_offline` يسجل المقارنة في [S3 backfill comparison](../verification/s3-backfill-comparison.json)، ببيانات SQL اصطناعية معزولة وDrive محاكاة؛ ليس قبول اتصال حقيقي أو cutover.

## S4 — تنقيح القالب وفحص المخرجات

`python docs/specs/school-file-storage/scripts/build-s4-template.py` يعيد القالب المضمن من مصادر S0 بعد مطابقة SHA256. يقرأ JSON/مقطع البيانات من HTML دون تشغيل JavaScript أو مصدر تنفيذي. يحتفظ بالنص الوظيفي والبصمات والأكواد، ويزيل الأشخاص من المسارات، ولا ينسخ completed أو بايتات أو ملفات Drive أو قرارات. المصفوفة 145 بصمة ReferenceOnly فقط. هذا تنقيح ثابت للقالب؛ ليس أداة الاستيراد التاريخي S6.

`verify-s4-exports.py` يفحص CSV/XLSX/PDF الناتجة من اختبارات SQL، ويطابق الأرقام والصفوف والعربية والخطوط وحدود النص ويرسم صفحات PDF للفحص البصري. يعتمد PyMuPDF، ويقرأ XLSX مباشرة من ZIP/XML. [أوامر إعادة التحقق والحدود](../verification/s4-self-evaluation-and-reports.md).

## S5 — فحص PDF اللقطة الفعلية

`verify-s5-pdfs.py` يقرأ archive/official/ZIP PDFs الناتجة من VisitArchiveSqlTests عبر ALFALAH_S5_EXPORT_DIRECTORY، ويقارن pixels لكل صفحة لنفس snapshot ويفحص embedded Amiri وبقاء النص داخل الورق والشعار وPNG التواقيع المجمدة والأرقام. يرسم أول/آخر صفحة للمراجعة، ويسجل shaped ToUnicode0000 وقيد البحث/النسخ دون ادعاء نجاحه. لا يبدأ API أو Drive ولا يقرأ credentials. [أوامر SQL الجديد المعزول والقيود](../verification/s5-approved-visit-pdf-archive.md). النتائج في s5-pdf-comparison.json؛ تشغيل الفحص لا يجيز worker/Drive أو S6.

## S6 — المصادر والاستيراد والتحقق والاسترجاع

[تقرير النتائج والقيود](../verification/s6-import-and-rollout.md) و[خطة التشغيل المحددة](../s6-rollout-runbook.md). الرايات الأربع الأصلية OFF، لا تطبيق migrations على الأصل أو تدوير keys/credentials أو حذف Drive. الاختبارات أدناه تستخدم قاعدة جديدة صريحة؛ لا تستخدم اسم قاعدة التشغيل. مصادر S0 الحقيقية وبصماتها من inventory؛ لا تشغيل ملفاتها التنفيذية. ملفات المصدر الخام والنسخ الاحتياطية المشفرة تبقى محلية في `.audit`، لا تُنشر.

```powershell
New-Item -ItemType Directory -Path .audit/sfs-s6 -Force | Out-Null
python -X utf8 docs/specs/school-file-storage/scripts/prepare-s6-sources.py --output .audit/sfs-s6/tracker-36.json
dotnet build backend/AlFalah.Tests --configuration Release --no-restore --verbosity quiet
$testDatabase='AlFalahS1Tests_S6_'+[Guid]::NewGuid().ToString('N')
$env:ALFALAH_STORAGE_TEST_CONNECTION="Server=(localdb)\MSSQLLocalDB;Database=$testDatabase;Trusted_Connection=True;TrustServerCertificate=True"
$env:ALFALAH_S6_REPOSITORY=(Get-Location).Path
$env:ALFALAH_S6_EXPORT_DIRECTORY=(Join-Path (Get-Location) '.audit/sfs-s6')
$env:ALFALAH_S4_EXPORT_DIRECTORY=(Join-Path (Get-Location) '.audit/sfs-s6/s4-exports')
$env:ALFALAH_S5_EXPORT_DIRECTORY=(Join-Path (Get-Location) '.audit/sfs-s6/s5-exports')
dotnet test backend/AlFalah.Tests --configuration Release --no-build --logger 'trx;LogFileName=s6-full.trx' --results-directory .audit/sfs-s6
python -X utf8 docs/specs/school-file-storage/scripts/verify-s4-exports.py .audit/sfs-s6/s4-exports .audit/sfs-s6/s4-export-comparison.json
python -X utf8 docs/specs/school-file-storage/scripts/verify-s5-pdfs.py .audit/sfs-s6/s5-exports .audit/sfs-s6/s5-pdf-comparison.json
python -X utf8 docs/specs/school-file-storage/scripts/build-s6-parity.py
```

The S6 collection appends `_S6Import` to that explicit test database, avoiding pollution of S1/S3 test fixtures. The script-twice test generates `.audit/sfs-s6/s6-migration.sql`, applies it twice over retained S5 import records and checks model/migration parity. Never start the original Development API with schema initialization for these tests. Running an API from Release holds its assemblies on Windows: build tools/tests before starting it, or build to an isolated output.

From `frontend/`, final executed commands:

```powershell
npm test -- --watch=false --browsers=ChromeHeadless --include='src/app/features/storage/**/*.spec.ts' --include='src/app/features/evidence-settings/**/*.spec.ts'
npm run build
npx playwright test --config playwright.storage.config.ts
```

Playwright starts Angular and mocks enabled API flows; it is separate from real disabled manager checks on 4200/5264. Those checks used a temporary locally signed test token retaining the existing manager/session identity, stored only in ignored scratch. No reusable token or signing key is published.

`rehearse-s6.ps1` reads original local settings, COPY_ONLY/checksum-backs up Development, restores to a fresh isolated clone, migrates/backfills **only that clone**, repeats the backfill, backs up/restores to another new DB and CHECKDB. Existing databases and original flags/keys/credentials are never overwritten/deleted. It requires LocalDB and Release build/tools and retains encrypted backups locally:

```powershell
powershell -NoProfile -File docs/specs/school-file-storage/scripts/rehearse-s6.ps1
# Use isolatedDatabase from the resulting local restore-rehearsal.json:
powershell -NoProfile -File docs/specs/school-file-storage/scripts/snapshot-retained-s6.ps1 -Database 'AlFalahSFS_S6_<clone>' -Output '.audit/sfs-s6'
powershell -NoProfile -File docs/specs/school-file-storage/scripts/monitor-s6.ps1 -SchoolId 18 -Database 'AlFalahSFS_S6_<clone>' -OutputPath '.audit/sfs-s6/monitor.json'
```

`snapshot-retained-s6.ps1` should be rerun after new synthetic writes. It backs up/restores to a **new** target and compares retained ledgers and version identities/hashes. Zero archives in the old-data clone cannot prove archived-data restore; the separate S5 SQL suites do that. `monitor-s6.ps1` is scoped SELECT-only; missing schema and unobserved Google/drift are unavailable/null, not zero. Alerts/owners/correction are in the runbook.

`DriveBaseline` now accepts a second output-path argument to preserve historical S0 reports. It only observes metadata; school 18 works and school 1 cannot decrypt with current keys. Original credential/key repair is outside these scripts.

`S6LiveSmoke` is explicitly opt-in and **writes one harmless synthetic PDF plus protected folders to the already selected Google root for school 18**, using existing credentials/keys and a migrated isolated clone. It retains these artifacts; do not auto-adopt them into the original ledger or delete them during rollback. No permissions/PATCH/delete requests are allowed. It verifies source replay, upload/download SHA256, same bytes reused for two independent links, explicit synthetic review, search/readiness/CSV. This is not real historical-byte import, live archive-worker recovery or cutover. Build before running any local Release API that locks shared outputs:

```powershell
dotnet build docs/specs/school-file-storage/scripts/S6LiveSmoke --configuration Release --verbosity quiet
dotnet --roll-forward Major docs/specs/school-file-storage/scripts/S6LiveSmoke/bin/Release/net8.0/S6LiveSmoke.dll --synthetic-live-school-18 'D:\AlFalah-Manage-System' 'AlFalahSFS_S6_<clone>' '.audit/sfs-s6/live-smoke.json'
```

The current owner's connected-account test request authorized the recorded synthetic run; do not run this writer as part of routine read-only monitoring.

Normalized source example (also accepts actual source aliases):

```json
[{"key":"original-1","name":"original.pdf","domainCode":"1","standardCode":"1.1","requirementCode":"1.1.1","referencePath":"C:\\old\\original.pdf","size":1234,"extension":"pdf","responsibleName":"اسم مرجعي","sourceStatus":"completed"}]
```

CSV uses the same column names, quoting commas/newlines. Use only actual existing requirement codes/SourceKeys; preview classifications and member suggestions require human review. Multipart preview includes school scope from the authenticated session plus academicYearId/templateVersion/sourceVersion. Review and commit each require latest rowVersion/digest/reason, and cannot silently remap committed rows. Original byte input is separate and must match the reviewed filename/extension/size; source completed never grants approval. A correction to immutable provenance needs a new source version/hash and reviewed batch, not direct SQL editing. Existing old source-only rows are displayed as legacy references.
