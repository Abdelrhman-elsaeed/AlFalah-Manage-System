# S0 — النظام الحالي وفروق التنفيذ

مراجعة ساكنة للكود الحالي وقياس مباشر لقاعدة Development؛ لم يبدأ خادم API، حتى لا تعمل migrations/seeder أو عامل reconciliation أثناء القياس. هذه ليست اختبارات E2E أو اختبار صلاحيات للنسخة الجديدة.

## نقاط الدخول الحالية — 25 endpoint

| المسار الأساسي | الفعل والمسار النسبي | بوابة الوصول الحالية | الاعتماد / أثر SFS |
|---|---|---|---|
| `/api/v1/teacher-drive` | GET `status` | مستخدم موثق؛ الحالة توضح عدم المعلم/غياب الاتصال أو المنحة | هوية InstructorProfile الفعلية |
| نفسه | GET `items` | المعلم النشط ومنحته وschool config | search/sort/pageToken؛ قائمة 50 عنصرًا |
| نفسه | GET `items/{itemId}` | هوية المعلم + containment | لا وثوق بالمعرف من العميل |
| نفسه | GET `items/{itemId}/content` | نفس guard لكل تنزيل | بث بايتات عبر API |
| نفسه | GET `breadcrumb/{itemId?}` | جذر منحة المعلم | لا كشف مسار Windows |
| نفسه | GET `recent-files` | ملفات المعلم الحالي | قراءات سجل الرفع |
| نفسه | GET `evidence-tasks` | سياق المعلم | 31 مهمة عامة |
| نفسه | POST `uploads` | هوية/منحة + taskId وIdempotency-Key | UploadOperation ثم Drive ثم submission؛ PendingReview |
| نفسه | DELETE `submissions/{id}` | ملكية submission والتحقق من Drive | لا حظر على Approved حاليًا؛ S3 يصحح |
| نفسه | PATCH `submissions/{id}/name` | ملكية + الاسم/الامتداد + containment | لا حظر على Approved حاليًا؛ S3 يصحح |
| `/api/v1/teacher-drive-admin` | GET `teachers/{id}/folder` | `Instructor.View` ثم school scope في الخدمة | قراءة المنحة الحالية |
| نفسه | PUT `teachers/{id}/folder` | `Instructor.Edit` + school scope + تحقق Drive | منع منح متداخلة بين المعلمين |
| نفسه | GET `teachers/{id}/folders` | `Instructor.Edit` + school scope | تصفح اختيار مجلد مصرح به |
| نفسه | DELETE `teachers/{id}/folder` | `Instructor.Edit` + school scope | سحب المنحة؛ ليست StorageDelegation |
| `/api/v1/school-google-drive` | GET الجذر | manager/global policy في الخدمة | DTO بلا الأسرار |
| نفسه | PUT الجذر | manager/global policy + scope | credential protector + root validation |
| نفسه | GET `auth-url` | مدير المدرسة/المسؤول المسموح | حالة OAuth مرتبطة بسياق المستخدم |
| نفسه | GET `callback` | AllowAnonymous؛ state محمي ومحدد المدة | يعيد إكمال الاتصال؛ لا يصلح كفحص S0 |
| `/api/v1/evidence-matrix` | GET `academic-years` | سياسة المشرفين في الخدمة | السنوات المتاحة |
| نفسه | GET الجذر | SuperAdmin/MainManager/SchoolManager/Moderator + نطاق المدرسة | الصلاحيات الحالية أدوار؛ ليست تفويض التخزين الجديد |
| نفسه | GET `cells/{teacherId}/{taskId}` | نفس scope وتحقق المعلم | submission واحد مرتبط بمهمة/سنة |
| نفسه | POST `submissions/{id}/review` | نفس سياسة المشرفين + scope | قرار Approved أو Rejected مع ملاحظة وتدقيق |
| نفسه | GET `submissions/{id}/content` | scope للمشرف + حالة الملف | لا كشف credential للعميل |
| نفسه | GET `export/excel` | نفس القراءة والنطاق | يعتمد IsChecked القديم |
| نفسه | GET `export/pdf` | نفس القراءة والنطاق | يعتمد IsChecked القديم |

المصدر: ملفات Controllers المقابلة في backend/AlFalah.Api/Controllers، وTeacherDriveIdentityService/TeacherDriveMappingService/TeacherDriveFolderGuard/EvidenceMatrixService في Infrastructure/Services. الملفات تُراجع قبل تعديلها في المرحلة المالكة؛ الجدول يصف الوضع الحالي لا أذونات التخزين المراد زرعها.

## التدفق والحدود

- TeacherDriveIdentityService يحل UserId إلى المعلم النشط غير المحذوف ويرفض اختلاف المدرسة النشطة. FolderGuard يتتبع الأصل/الآباء حتى 64 مستوى ويمنع missing/trashed/cycles/unreadable.
- TeacherDriveMappingService يمنع تداخل منح المعلمين ويشترط جذر المدرسة. إضافة مجلدي المكتبة والأرشيف تحتاج اختبار تعارض grant مع school root: لا نفترض أن التنظيم الجديد آمن لمجرد أنه تحت الجذر.
- GoogleDriveBrowserService.ListAsync يسجل audit ويحفظ SQL؛ قياس S0 يتجاوز خدمة التصفح ويستخدم DriveClient مباشرة للقراءة فقط.
- GoogleDriveUploadService يضبط 250 MiB والامتدادات الحالية، ولا يثبت MIME من بايتات الملف. S2 يضيف الأنواع المعتمدة ويفصل حد الملف عن حجم الطلب multipart.
- EvidenceSubmissionService يشترط مهمة وسنة نشطتين، ويحفظ PendingReview بعد الرفع. هوية Drive فريدة؛ النموذج الحالي لا يمثل أصلًا مشتركًا بمراجعات مستقلة متعددة.
- EvidenceReconciliationService يفحص الشواهد المرتبطة المكتملة غير المحذوفة ويحدّث flags/cells/audit، ولا يكتشف كل ملفات Drive غير المفهرسة ولا يسترد كل عملية رفع معلقة. خطأ النقل/الاعتماد لا يعني missing؛ تبقى flags السابقة. العامل الخلفي افتراضيًا كل 30 دقيقة.
- LocalFileStorageService لمرفقات أعذار الطلاب ومساره StudentAffairs:ExcuseStoragePath، خارج نظام SFS. لا يدخل backfill أو المكتبة.

## فروق ذات أثر وظيفي — مراحل مالكة محددة

| الفرق المؤكد في الكود | المرحلة والإجراء | تحقق مطلوب |
|---|---|---|
| EvidenceMatrixService يبني IsChecked من ActiveFilesCount > 0، وCompletedTasksCount من عدد الخلايا المحددة | S3 يشتق من روابط Approved لنسخة موجودة وغير محذوفة؛ S4 يعرض الجاهزية الجديدة | رفع PendingReview ورفض/فقد/حذف لا يحقق المطلب |
| Rename/Delete يجيزان المعالجة لملف معتمد إذا كان مملوكًا | S3 يفرض طلب تغيير عند وجود أي رابط معتمد | direct request يرفض حتى لو زر UI مخفي |
| سياسات evidence-matrix تمنح Moderator مراجعة بالأدوار | S1/S3 تثبت Storage permissions ومدير/تفويض المدرسة؛ توافق الوصول القديم قرار انتقال واضح | Moderator غير مفوض لا يكتسب إدارة مكتبة المدرسة من دوره |
| بعض Infrastructure services تتعامل مباشرة مع DbContext | S1/S2 تطبق interfaces/repositories على مجال التخزين وفق skill؛ لا refactor عام في S0 | Controller رفيع، قواعد في الخدمة، استعلامات مدرسة في repository |
| رفع Drive ثم فشل حفظ SQL قد يترك ملفًا بلا ledger | S2 سجل عمليات متين واسترداد يتفادى الرفع الثاني؛ S6 reconciliation/backfill | نجاح Drive وفشل SQL ثم إعادة تشغيل لا يكرر البايتات |
| سجل task الحالي لا يحمل نطاق مدرسة/سنة؛ submission يحملها | S1 يضيف requirements scoped ولا يحذف catalog | يبقى نفس task catalog وsubmission بعد backfill |

تطبق ApiResponse<T> طبقًا للدستور بدل تغيير envelope إلى ResponseViewModel في skill. لا تغيير معماري غير مرتبط بالمراحل المعتمدة.

## اعتماد زيارة V2 وتوليد PDF

المسارات `/api/v2/visits/{id}/finalize` و`/{id}/approve` تمر عبر VisitV2Service؛ FinalizeAsync يمنح Approved تلقائيًا عند IsManager && Visit.Approve، وإلا PendingApproval. ApproveAsync يحفظ الاعتماد الصريح. ExportPdfAsync يستدعي VisitV2DocumentService.BuildPdfAsync عند الطلب؛ لا توجد عملية أرشفة Drive تلقائية الآن.

S5 يضيف عملية outbox فريدة لـ (VisitId, ApprovalRevision) داخل نفس transaction لمساري الاعتماد، ثم worker يستخدم مولد PDF الحالي. الأصول الحالية تشمل الشعار وتوقيعات المعلم/المقيّم/المدير حسب قواعد ظهورها. لابد أن تمثل النسخة المؤرشفة محتوى الاعتماد وتوقيعاته وقت المراجعة، ولا تتغير إذا عدّل أحد توقيعه قبل تنفيذ retry. فشل Google لا يلغي الاعتماد؛ إعادة الاعتماد تحتفظ بالأصل والمراجعة السابقة. إعادة فتح الزيارة لا تعني حذف PDF التاريخي.

## القياس الحقيقي وحدوده

[runtime-baseline.json](runtime-baseline.json) يوثق SQL المحلي: 18 صف مدرسة إجمالًا، منها 3 غير محذوفة؛ اتصال واحد مفعّل، grant نشط واحد، submission واحد وDrive item مسجل واحد، 31 مهمة، سنة واحدة 2026-2027، صفر عمليات رفع Pending. التفصيل school/teacher/year/task محفوظ بمفاتيح رقمية بلا أسماء أو معرفات Drive.

submission الوحيد Approved/Completed وغير محذوف ولا يحمل missing flag في السجل. هذا **ليس إثبات وجود بايتاته على Drive**. كل بيانات الاتصال المحلية مؤشرات إعداد فقط؛ القياس الحي منفصل في drive-baseline.json. لا يجوز نقل حجم هذه العينة إلى تقدير أداء آلاف ملفات بيئة التشغيل؛ S2/S6 يختبران paging والأداء بعينة ممثلة.
