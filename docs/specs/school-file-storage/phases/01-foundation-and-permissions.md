# S1 — نموذج البيانات والصلاحيات والترحيل التحضيري

- **الحالة:** محددة، لم يبدأ التنفيذ
- **الاعتماد:** مخرجات [S0](00-baseline-and-parity.md) وقواعد [الخطة الرئيسية](../plan.md)
- **النتيجة:** مخطط additive، تفويض آمن، وسجل ملفات/روابط قابل للتعبئة مع بقاء سلوك المعلم الحالي

## الهدف وحدود المرحلة

إنشاء أساس المجال والبيانات والأذونات دون تبديل واجهة المكتبة أو احتساب جاهزية جديدة. بايتات المعلمين تبقى في Google Drive الحالي، وجدول `TeacherDriveFolder` لا يُستبدل أو يُحذف. توسيع التخزين إلى ملفات المدرسة وأرشيف الزيارات يتم عبر نماذج جديدة، لا عبر `LocalFileStorageService` الخاص بمرفقات أعذار الطلاب.

## نموذج البيانات المطلوب

| الكيان | الحقول/القواعد الملزمة |
|---|---|
| `StorageFolder` | `Id`, `SchoolId`, `ParentFolderId?`, `Kind`, `DisplayName`, `DriveId`, `DriveItemId`, `IsActive`, تدقيق الإنشاء/التعديل؛ جذر لكل مساحة مدرسة/معلم/أرشيف، ومنع دورات الشجرة وربط مدرسة بأخرى. |
| `StoredFile` | `Id`, `SchoolId`, `FolderId`, `OwnerTeacherId?`, `SourceKind`, `DisplayName`, `CurrentVersionId?`, حذف منطقي وتواريخه؛ لا يُستخدم الاسم أو المسار كمفتاح. |
| `StoredFileVersion` | `Id`, `StoredFileId`, `SchoolId`, `VersionNumber`, معرف Drive، `SHA256`, `SizeInBytes`, `MimeType`, `UploadedByUserId`, حالة التوفر والتواريخ؛ نسخة معتمدة لا تُستبدل. |
| `EvidenceRequirement` | `Id`, `SchoolId?` للقالب أو المدرسة، `AcademicYearId?`, `TemplateVersion`, `Code`, مجال/معيار/مهمة أصلية، الأهمية والمسؤول وسياسة الاستيفاء والترتيب والنشاط؛ أكواد مستقرة، ولا تكرار لقالب المدرسة/السنة. |
| `EvidenceLink` | `Id`, `SchoolId`, `AcademicYearId`, `StoredFileId`, `RequirementId`, `TeacherId?`, `VersionId`, حالة مستقلة، توقيت الإنشاء/التقديم؛ قيد فريد للرابط النشط. |
| `EvidenceReviewDecision` | `Id`, `EvidenceLinkId`, `VersionId`, قرار/مراجع/ملاحظة/تاريخ؛ سجل إضافة فقط يحفظ إعادة التقديم والقرار السابق. |
| `StorageDelegation` | `SchoolId`, `GranteeUserId`, `GrantedByManagerUserId`, `StartsAt`, `ExpiresAt?`, `RevokedAt?`, `Reason`, تدقيق؛ القائم بالمنح مدير المدرسة الفعلي وقت العملية، ولا تفويض متسلسل. |
| `VisitArchiveOperation` و`VisitArchiveArtifact` | مخطط وتجهيز فقط في هذه المرحلة، مع `(VisitId, ApprovalRevision)` مفتاح idempotency؛ السلوك في S5. |
| `PrototypeImportBatch` و`PrototypeImportRow` | مخطط وتجهيز فقط، مع بصمة مصدر وحالة وملاحظة الاستثناء؛ الاستيراد التشغيلي في S6. |

كل سجل قابل للقراءة أو تنزيل ملف يحمل `SchoolId` صريحًا، والعلاقات تتحقق من تساوي مدارس الطرفين. أضف فهارس البحث `(SchoolId, OwnerTeacherId)`, `(SchoolId, DriveItemId)`, `(SchoolId, AcademicYearId, RequirementId)`، ومفاتيح فريدة مناسبة للعمليات والروابط النشطة، و`rowversion` للأوامر المتزامنة. تُعالج قيم FK الاختيارية في القيد الفريد بـ filtered indexes أو مفتاح مطبّع، لأن تكرار `NULL` في SQL Server قد يختلف عن قصد قاعدة المجال؛ وثّق الصيغة الفعلية في migration.

## الصلاحيات والتفويض

1. أضف `Storage.ViewSchool`, `Storage.ManageSchool`, `Storage.ReviewEvidence`, `Storage.ViewOwn`, `Storage.ManageOwn`, `Storage.ViewArchive`, `Storage.RetryArchive`, `Storage.Delegate` إلى مصدر الأذونات وزرع قاعدة البيانات وفق نمط المشروع. لا تُعدّل تعريفات `Instructor.View/Edit` القديمة لتمنح دخول المكتبة العامة تلقائيًا.
2. اربط أذونات المدير ضمن مدرسته، والمعلم بملفاته ومجلده الممنوح فقط. المفوّض يكتسب الأذونات التشغيلية الكاملة للمدرسة المحددة ما دام السجل نشطًا؛ لا يحصل على `Storage.Delegate`.
3. خدمة التفويض تعيد فحص `School.ManagerUserId`/الهوية المعتمدة في كل منح أو سحب. لا يكفي `HasPermission(Storage.Delegate)` وحده. تحقق من نشاط الممنوح، عضويته في المدرسة، تاريخ التفويض، وعدم تعارض تفويض نشط؛ سجّل الفاعل والسبب.
4. افحص المدرسة والملكية داخل Application service لكل list/detail/content/export/mutation، بالإضافة إلى حارس جذر Drive. واجهة Angular guard مساعدة فقط.

## ترحيل البيانات دون فقد التاريخ

1. أنشئ migration additive فقط؛ لا drop أو rename مدمر. جهّز rollback لميزة القراءة الجديدة دون إسقاط الجداول أو عكس بيانات أعمال.
2. نفّذ backfill قابلًا للإعادة: لكل `TeacherEvidenceSubmission` أنشئ `StoredFile` ونسخة تشير إلى **معرف Drive نفسه** ورابطًا واحدًا إلى `EvidenceTask` الأصلي متى وُجد. احفظ `ReviewStatus`, `ReviewedAtUtc`, `ReviewedByUserId`, `ReviewNote`, `IsDeleted`, `IsMissingFromDrive`, `UploadedAtUtc` في تمثيل التاريخ الجديد أو سجل provenance.
3. صفوف legacy بلا `TaskId`/`AcademicYearId` تصبح ملفات قابلة للعرض لصاحبها وموسومة «تحتاج ربطًا»، ولا تُنشئ شاهدًا وهميًا. لا تستعمل `WebUrl` عامًا كدليل صلاحية.
4. نفّذ dry run وتقرير مقارنة حسب المدرسة/المعلم/المهمة/السنة والحالة قبل تفعيل أي قراءة جديدة؛ جرّب إعادة backfill مرتين دون مضاعفة صفوف.
5. احتفظ بالـ APIs القديمة حتى S2/S3، ولا تغيّر نتيجة المصفوفة أثناء S1. حدد خطة مصدر حقيقة واحد عند cutover: سجل/رابط جديد للقراءة والكتابة، مع تكييف endpoints القديمة بالخدمة نفسها.

## طبقات التنفيذ والملفات المتوقعة

- Domain entities/configurations/migration في `backend/AlFalah.Domain` و`backend/AlFalah.Infrastructure/Data`، مع repositories للقراءة والكتابة؛ لا EF داخل Controller.
- Application DTOs/interfaces وخدمات تفويض ومدرسة بحدود واضحة، و`ApiResponse<T>` للأوامر الإدارية الجديدة.
- seed الأذونات وتوثيق تغير العقود قبل استهلاكها في الواجهة؛ راجع `docs/02`, `03`, `07`, `08`, `11` عند التنفيذ.

## اختبار القبول وبوابة الخروج

- migration على قاعدة تجريبية وbackfill مرتان ينتجان المجاميع نفسها، وكل submission قديم إما مرتبط بأصل واحد أو مسجل كاستثناء مفسر. لا عناصر Drive جديدة ناتجة عن backfill.
- اختبارات خدمة تثبت رفض معلم يطلب ملف زميل، رفض مدرسة أخرى، رفض المفوّض عند منح تفويض، ومنع التفويض بعد سحبه أو انتهائه.
- تظل واجهة المعلم القديمة والرفع والمصفوفة الحالية تعمل على البيانات الحقيقية قبل cutover؛ حالة النشر الافتراضية للمساحة الجديدة مغلقة.
- تقرير مطابقة البيانات وخطة رجوع feature flag موثقان. بعد تحديث سجل المرحلة والـ spec kit، يبدأ [S2](02-library-and-uploads.md).
