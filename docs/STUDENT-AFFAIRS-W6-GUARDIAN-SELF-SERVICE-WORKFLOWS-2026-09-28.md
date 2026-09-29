# W6 — Guardian Self-Service Workflows

التاريخ المرجعي: 2026-09-28  
حالة التنفيذ: مكتمل ومتحقق منه  
النطاق: `Guardian` فقط؛ لم يبدأ W7.

## ملخص النتيجة

أصبح المسار الرسمي `/student-affairs/guardian` شاشة **أبنائي** حقيقية مبنية على projection خادمية واحدة. تعرض الروابط الحالية فقط، وقدرات كل رابط، وملخص الغياب والأعذار وطلبات الخروج وتصاريح دخول الفصل والاستدعاءات والإشادات، إلى جانب عدادات الإشعارات والمحادثات غير المقروءة.

اكتملت أسطح القراءة الخاصة بالتصاريح والاستدعاءات، وصندوق إشعارات ولي الأمر، وNew Conversation wizard الآمن. كما شُددت عقود الأعذار وGate Pass والرسائل والتنبيهات بحيث تتطلب الدور الدقيق والصلاحية والملف الفعال والرابط الحالي، مع إخفاء object-scope denial كـ404 في الموارد الحساسة.

لم يضف W6 aggregate أو جدولًا موازيًا، ولم يحتج migration جديدة.

## RBAC النهائي

| السطح/العملية | الدور الدقيق | الصلاحية | قيود إضافية |
|---|---|---|---|
| أبنائي/dashboard | `Guardian` | `StudentAffairsDashboard.Guardian` + `Guardian.ViewLinkedStudents` | ملف ولي أمر فعال وروابط حالية |
| linked students/summary | `Guardian` | `Guardian.ViewLinkedStudents` | نفس المدرسة، طالب ورابط وتسجيل فعال |
| رفع عذر | `Guardian` | `Attendance.SubmitExcuse` | `CanSubmitExcuses=true` وغياب مؤهل |
| سجل الغياب/المرفق | `Guardian` | صلاحية العرض الخاصة بالتدفق | رابط حالي؛ المرفق مملوك لنفس ولي الأمر |
| إنشاء Gate Pass | `Guardian` | `GatePass.Request` | `CanRequestGatePass=true` وLesson منشورة فعلية |
| قائمة/تفصيل/سجل Gate Pass | `Guardian` | `GatePass.ViewOwn` | الطلب مملوك لنفس ولي الأمر |
| إلغاء Gate Pass | `Guardian` | `GatePass.CancelOwn` | مملوك وحالته `Requested` فقط وRowVersion صحيحة |
| Entry Permits | `Guardian` | `ClassroomEntryPermit.View` | read-only وروابط حالية فقط |
| Summons | `Guardian` | `Guardian.ViewLinkedStudents` | الاستدعاء موجه لنفس Guardian profile |
| Notifications | `Guardian` | `Notification.ViewOwn` | current user/current school وDelivered فقط |
| بدء محادثة مع معلم | `Guardian` | `Messaging.StartGuardianTeacher` | معلم من lookup الجدول المنشور فقط |
| بدء محادثة إدارية | `Guardian` | `Messaging.StartGuardianAdministration` | Officer فعال أو Social Worker مسند لحالة فعالة |
| صندوق الرسائل/الإرسال | participant مصرح | `Messaging.ViewOwn` / `Messaging.Send` | participant + school + exact target role |

وجود permission وحدها مع دور عشوائي لا يمنح سلطة. الـcontrollers تجري gate أولي، والـApplication handlers تعيد التحقق من exact role والصلاحية، ثم تعيد الـrepositories اشتقاق object scope من بيانات المدرسة والرابط.

## قواعد StudentGuardian والقدرات

- ولي الأمر والطالب والحسابات المرتبطة يجب أن تكون فعالة وغير محذوفة ومن المدرسة النشطة.
- التاريخ المرجعي هو school-local date المستنتج من Bell Schedule منشور وحيد؛ missing/ambiguous publication أو timezone غير صالح يفشل مغلقًا.
- صلاحية الرابط: `ValidFrom <= localDate` و`ValidTo == null || ValidTo >= localDate`.
- يتطلب الطالب active dated enrollment وactive term/classroom عندما يحتاج العرض سياق الفصل.
- `CanSubmitExcuses` و`CanRequestGatePass` تتحكمان في السلطة الخادمية، وليس فقط ظهور الزر.
- `ReceivesNotifications` يظل capability إنشاء/تسليم؛ لا يمسح historical delivered notifications الصحيحة.
- لا يُعامل `StudentId` المرسل من الواجهة كسلطة؛ يعاد اشتقاقه من الرابط الفعال في كل عملية.

## object scope والخصوصية

| مورد | ما يراه ولي الأمر | ما لا يرجع إليه |
|---|---|---|
| الطفل | الاسم، الرقم، الفصل، capabilities والملخصات التشغيلية | رقم الهوية الكامل وروابط Guardians الآخرين |
| Summon | السبب العام، الأولوية، الموعد، المكان، التعليمات والحالة العامة | referral IDs/count snapshots/case actions/notes/review internals/worker identity |
| Entry Permit | الطالب والفصل والسبب والفترة والحالة | صورة الطالب وبيانات تسليم الموظفين/المعلم غير اللازمة |
| Notification | إشعاراته المسلمة فقط وread state | pending/suppressed/cross-user/cross-school |
| Conversation | threads التي هو participant فيها فقط | bodies أو headers لأي thread غير مرتبط |
| Gate Pass/Excuse | موارد ولي الأمر/الطفل المرتبط فقط | موارد Guardian آخر ولو كان الطالب نفسه |

الروابط المنتهية أو المستقبلية والملفات غير الفعالة تختفي من القوائم، والـdeep link خارج النطاق يعامل كغير موجود حيث يلزم عدم كشف وجود السجل.

## landing والتنقل

- canonical landing: `/student-affairs/guardian` بعنوان **أبنائي**.
- login role landing الموجود أصبح متوافقًا مع المسار الرسمي.
- أضيفت روابط Guardian المقيدة للصلاحيات إلى shell: أبنائي، الأعذار، طلب الخروج، التصاريح والاستدعاءات، الإشعارات، الرسائل.
- لا تظهر للـGuardian روابط Officer أو Security أو Social Worker mutations.
- route guard الخاص بأبنائي يشترط الصلاحيتين معًا عبر `requireAllPermissions`.

## Dashboard وMy Children

`GuardianStudentAffairsDashboardDto` يعيد projection آمنة ومحددة تشمل:

- الطلاب المرتبطين بترتيب ثابت.
- capabilities لكل رابط.
- الرسمي `Absent` و`AbsentExcused`.
- أعداد أعذار `Pending/Accepted/Rejected`.
- Gate Passes النشطة، Entry Permits النشطة، الاستدعاءات المعلقة، والإشادات الحديثة المسموحة.
- unread notifications وunread delivered messaging threads.
- `GeneratedAt` من الخادم.

التجميع يتم server-side عبر استعلامات set-based؛ لا توجد query داخل loop ولا تحميل entities كاملة للعد في الذاكرة. الواجهة تستهلك projection canonical واحدة وتعرض loading/empty/error/manual refresh وروابط الإجراءات حسب capabilities.

## الأعذار والغياب

- أبقي التدفق الحالي ولم ينشأ flow موازٍ.
- يقبل PDF فقط بامتداد وcontent type صحيحين، حد أقصى 10 MiB، وحجم فعلي مطابق، وتوقيع `%PDF-` فعلي قبل التخزين.
- يحسب SHA-256 للمحتوى ويقارنه في idempotent replay.
- نفس المفتاح ونفس payload يعيدان الأصل؛ نفس المفتاح مع payload مختلف يرجع 409.
- failure بعد التخزين يحذف الملف المخزن، ولا يترك DB row كاذبة.
- تنزيل المرفق يتطلب الرابط الحالي وملكية Guardian profile الدقيقة.
- قبول العذر لا يلغي حقيقة الغياب؛ الحالة الرسمية تصبح `AbsentExcused`.
- واجهة W5 الحالية للأعذار استمرت مع linked-student options وPDF validation/history/preview/download وإعادة الجلب.

## Gate Pass

- الإنشاء يعيد `Requested` فقط.
- desired exit يجب أن يكون في المستقبل، في study date منشور، وداخل Lesson فعلية للصف في الجدول المنشور؛ break/gap/missing publication لا يمر.
- استمرت overlap policy الحالية ±30 دقيقة.
- same key/same payload يعيد الأصل، والمفتاح نفسه مع payload مختلف يرجع 409.
- القائمة والتفصيل والسجل تخص ولي الأمر فقط.
- الإلغاء يتطلب الملكية و`Requested` وسببًا وRowVersion؛ لا يسمح بعد الاعتماد أو acknowledgement.
- الواجهة لا تعرض approve/reject/acknowledge/exit controls للـGuardian وتحتفظ بعقد إعادة المحاولة والتعارض الحالي.

## Entry Permits وSummons

- شاشة `/student-affairs/guardian/activity` read-only وتستخدم server-side paging منفصلًا للتصاريح والاستدعاءات.
- Entry Permit list/detail يعيدان فقط أبناء الرابط الحالي وفق school-local date، دون issue/revoke/acknowledge controls.
- أضيف `GuardianSummonDto` آمن ومسارا `GET /api/v1/summons/mine` و`GET /api/v1/summons/mine/{id}`.
- الاستدعاءات مقيدة بـGuardian profile الموجه إليه الاستدعاء، وليس فقط بكونه Guardian آخر للطالب نفسه.
- DTO العام الداخلي لم يعد مستخدمًا في سطح ولي الأمر.

## الإشعارات

- شاشة `/student-affairs/guardian/notifications` تدعم paging خادميًا وفلتر غير المقروء وunread count وmark-one/mark-all.
- عدد غير المقروء منشور في state مشتركة ويظهر badge في shell، ويتجدد من الخادم عند الدخول والتنقل وبعد عمليات القراءة دون polling مكرر.
- الاستعلامات تقيد current user/current school وتعيد delivered notifications فقط؛ pending approval وsuppressed لا يظهران.
- mark-read يعمل داخل owner scope ولا يرفع delivery state.
- الـdashboard يعرض العدد فقط ولا يحمل أجسام الإشعارات.

## Safe recipient lookup وNew Conversation

- teacher lookup الموجود يعيد `InstructorProfileId` فقط من unique effective published timetable للطالب المرتبط.
- أضيف `GET /api/v1/conversations/recipient-options/staff?studentId=...`.
- Officer options: حسابات فعالة، نفس المدرسة، exact `StudentAffairsOfficer` role.
- Social Worker options: exact active role ومُسند حاليًا إلى referral فعال متعلق بالطالب.
- server يعيد التحقق من الرابط، التاريخ المحلي، target type/role/user، ولا يقبل raw free IDs كسلطة.
- wizard يختار الابن ثم نوع المحادثة ثم مستلمًا من lookup فقط، مع subject/body required وحماية double-submit.
- مفتاح إنشاء المحادثة يبقى ثابتًا عند network retry ما دامت البيانات نفسها؛ تغيير payload يولد محاولة جديدة.
- نجاح الإنشاء لا يعرض optimistic success؛ يفتح thread المرجعة من الخادم.

## Messaging وOffice Hours

- لم تتغير سياسة W4: Guardian→Teacher خارج slot مؤهل يصبح `QueuedUntilOfficeHours`، وداخل slot يصبح `SentImmediately`.
- Guardian→Officer وGuardian→assigned Social Worker فوريان.
- لا urgent override ولا WebSocket/SignalR.
- pending لا يعرض Delivered، والـqueued disposition و`nextEligibleSendAt` يظهران للمستخدم.
- closed thread read-only، والرسائل plain text، والنتائج الدورية تدمج حسب message ID لمنع التكرار.

## idempotency وconcurrency

- Absence excuse وGate Pass وinitial conversation/message تستخدم stable idempotency keys.
- same key/different payload يصنفه `FromResponse` كـ409.
- Gate Pass cancellation وبقية transitions تحافظ على RowVersion؛ stale/concurrency يرجع 409.
- create endpoints التي لمسها W6 ترجع 201 عند النجاح.
- read/mutation endpoints التي لمسها W6 تستخدم `FromResponse` بدل HTTP 200 مع `IsSuccess=false`.

## API status mapping

- 200: قراءة/تحديث ناجح.
- 201: إنشاء ناجح.
- 400: validation أو policy/state violation.
- 401: غياب authentication/active context.
- 403: نقص exact role أو permission أو profile authority العامة.
- 404: سجل غير موجود أو object-scope denial الحساس.
- 409: RowVersion/concurrency/idempotency conflict.

الـcontrollers رفيعة؛ business/time/state rules في handlers، وEF/projections في Infrastructure repositories، ولا يخرج `IQueryable` من Infrastructure.

## الملفات

### ملفات جديدة

- `backend/AlFalah.Application/IntelligentTimetable/SchoolLocalDateResolver.cs`
- `backend/AlFalah.Application/StudentAffairs/Messaging/Handlers/GetGuardianStaffOptionsQueryHandler.cs`
- `backend/AlFalah.Application/StudentAffairs/Summons/Handlers/GetMySummonByIdQueryHandler.cs`
- `frontend/src/app/core/models/guardian-self-service.models.ts`
- `frontend/src/app/core/services/guardian-self-service.service.ts`
- `frontend/src/app/core/services/guardian-self-service.service.spec.ts`
- `frontend/src/app/features/student-affairs/guardian-activity/*`
- `frontend/src/app/features/student-affairs/guardian-notifications/*`
- `frontend/src/app/features/student-affairs/messaging-chat/messaging-chat.component.spec.ts`

### مجموعات الملفات المعدلة

- API المعدلة: `GuardianController` و`StudentAffairsDashboardController` و`GatePassesController` و`SummonsController` و`NotificationsController` و`ConversationsController`. استُخدم عقد `ClassroomEntryPermitsController` الحالي دون تعديل controller.
- Application: contracts/interfaces/handlers الخاصة بـDashboard، Attendance، GatePasses، Guardians، Messaging، Notifications، Students، Summons، وتسجيل local-date resolver.
- Infrastructure: repositories الخاصة بـStudentWorkflow، Attendance، GatePass، ClassroomEntryPermit، Messaging، Notification، Summon، وDI.
- Tests: `AttendanceAndDelayWorkflowTests`، `AttendanceMediatRTests`، `GatePassWorkflowTests`، `GatePassAndMessagingMediatRTests`، `StudentWorkflowAndGuardianTests`، والفakes المتأثرة في SocialWorker/Teacher summons.
- Frontend: routes، shell navigation/tests، dashboard/phase5/Guardian models والخدمات، Guardian dashboard، messaging chat.

قائمة Git التفصيلية تبقى ظاهرة في `git status --short`؛ لم تنشأ ملفات build داخل tracked changes.

## الاختبارات والتحقق النهائي

| التحقق | النتيجة |
|---|---|
| `dotnet build AlFalah.slnx -c Release --no-restore` | ناجح، 0 errors، تحذير قديم واحد |
| `dotnet test AlFalah.slnx -c Release --no-build --no-restore` | 689 passed، 0 failed، 0 skipped |
| `npm run build -- --configuration production` | ناجح |
| `npm test -- --watch=false --browsers=ChromeHeadless` | 196 passed، 0 failed |
| `dotnet ef migrations has-pending-model-changes ...` | لا توجد model changes بعد آخر migration |
| `git diff --check` | ناجح |

التغطية المضافة مباشرة تشمل PDF magic validation، excuse/Gate Pass idempotency conflict، cross-Guardian Gate Pass isolation، thread-type permission، handler registration، Guardian shell navigation، safe recipient options، ثبات conversation idempotency key، notification/permit/summon routes، والفلاتر والـserver paging.

## Schema وMigration

لا توجد schema changes ولا migration جديدة. W6 أعاد استخدام aggregates وقيود W1–W5، وأكد EF أن الـmodel مطابق لآخر migration `20260928181703_AddW5OfficerReferralIdempotency`.

## التحذيرات والمخاطر المتبقية

- تحذير backend القديم فقط: `SocialWorkerWorkflowTests.cs:41` (`CS8602`).
- تحذيرات frontend القديمة فقط: CSS budgets في `dashboard-live` و`visit-workspace`، وثلاثة third-party selector parse warnings.
- EF يعرض تحذيرات model قديمة عن required navigations مع global query filters وتحذير `Visit.ExperienceVersion` sentinel؛ لا ترتبط بتغيير W6 ولا توجد pending changes.
- لم يُنفذ commit أو push.

## المؤجل عمدًا

- W7: Security redesign/acknowledgement/exit execution.
- W8: Social Worker CRM expansion.
- W9: Manager oversight وOffice Hours Manager UI.
- W10 وما بعده: التحسينات الأوسع غير المرتبطة مباشرة بـGuardian self-service.

W6 يتوقف هنا انتظارًا للموافقة قبل بدء W7.
