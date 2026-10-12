# Smartble authenticated feature inventory

**التاريخ:** 2026-10-12 — Africa/Cairo  
**المرحلة:** Phase 3–5؛ initial read-only discovery بعد تحليل المشروع.  
**الحالة:** CDP وauthenticated Dashboard اتأكدوا؛ المستخدم اختار قواعد الجدولة → التوليد → المراجعة، و[التحقيق المتاح دون تغيير بيانات اتوثق](deep-scheduling-investigation.md).

## Evidence and scope

REA `list_browser_targets` حدد Smartble target عند `/pro/pro_dashboard/`، و`inspect_web_page` عمل passive capture. Playwright اتصل بنفس browser session، وقرأ Dashboard/configuration DOM من غير password inputs أو login redirect.

اتفتح tab تحقيق منفصل داخل نفس Chrome context؛ الـDashboard الأصلي اتساب كما هو، وtab التحقيق اتساب عند timetable preview.

لم يُنفذ save/import/generate/publish/restore/delete أو إرسال إشعارات في Smartble. application source لم تتغير.

| Evidence | Source |
|---|---|
| [Browser discovery](evidence/browser-target-discovery-sanitized.json) | REA `list_browser_targets`؛ Evidence ID `ev_3b4bb14d4ad8a8ac4fb4dbc94c04d3287fdbf2647856ad41096caff96bbc808e` |
| [Dashboard inspection](evidence/dashboard-inspection-sanitized.json) | REA `inspect_web_page`؛ source ID `ev_3f701cca07a00a5205d7138d44c68bba3081ab9c41093d3bd20f7d53e227d4a5` |
| [Configuration inventory](evidence/authenticated-inventory-dom-sanitized.json) | Playwright read-only GET navigation + DOM projection؛ مش REA Evidence |
| [Waiting route](evidence/waiting-route-access-sanitized.json) | Visible-link GET navigation؛ redirect chain ورسالة الاشتراك |
| [Waiting subpages](evidence/waiting-subpages-inventory-sanitized.json) | اتنين links ظاهرين في Dashboard؛ لا endpoint guessing |

الـsanitized derivatives مش canonical export bundles، ومحتواها المعدّل لا يُدعى إنه يطابق digest الأصل. الـoriginal Evidence IDs provenance references فقط. مفيش full raw bundle أو screenshots اتكتبوا.

قيم fields، cookies، auth headers، tokens وstorage values لم تُقرأ. Rows وأسماء المدرسة/المعلمين/الفصول، configured times، counts وsubscription expiry اتشالوا من الـretained projections.

**حد الدليل:** controls المعروضة تحت تشمل DOM في hidden dialogs. وجود control أو dialog، HTTP 200، أو button enabled لا يثبت نجاح العملية أو validation/business rules.

## Account status

الـDashboard عرض **«أنت الآن على الخطة التجريبية»** ومعاه upgrade link، وفي نفس الوقت quick action عرض **«لا يوجد اشتراك نشط»**.

- CONFIRMED: Trial description وsubscription warning موجودين في UI.
- UNKNOWN: علاقة القيود بـTrial، paid subscription، role أو route policy.
- لا استنتاج إن التجربة انتهت أو إن كل الأقسام premium locked.

مفيش feature-specific denial اتلاحظ يثبت `PREMIUM LOCKED`. الوصول لحصص الانتظار غير متاح عبر الروابط المختبرة، لكن السبب مش موضح.

## Feature inventory

`AVAILABLE` هنا نطاقها فتح الصفحة وعرضها فقط؛ عمليات الحفظ والتوليد والاستيراد والاستعادة لم تُختبر.

| Feature | Location | Purpose / observed UI | Access status | Priority |
|---|---|---|---|---|
| Working days / timings / breaks | `/pro/pro_dashboard/workdays/` | Weekend selectors، period count، basic/break tabs، default/per-day timing، bulk day application | AVAILABLE: HTTP 200؛ persistence UNKNOWN | High |
| Teacher settings | `/pro/pro_dashboard/teachers/` | Management، maximum classes، groups، import وbulk editing controls | AVAILABLE: HTTP 200؛ per-teacher/save/import behavior UNKNOWN | High |
| Classrooms and subjects | `/pro/pro_dashboard/classrooms/` | Class locations، stages، subject management/groups، allocation shortcuts | AVAILABLE: HTTP 200؛ dialogs/save UNKNOWN | High |
| Teaching relations | `/pro/pro_dashboard/assignments/` | List/matrix views، subject filter، teacher focus، specialization-filter toggle، swap/reallocation controls | AVAILABLE: HTTP 200؛ apply/swap UNKNOWN | High |
| Subject requirements | `/pro/pro_dashboard/courses/` | Individual/paired weekly lessons، early preference، special location، days | AVAILABLE: HTTP 200؛ validation UNKNOWN | High |
| Timetable preview | `/pro/pro_smart_table/table-preview/` | Grid؛ generation/edit/manage/print controls؛ density/colors؛ import into current/new table controls | AVAILABLE: preview HTTP 200؛ execution UNKNOWN | High |
| Waiting root | `/pro/pro_waiting_classes/` | Nav موجودة؛ opening بيرجع Dashboard | UNKNOWN: HTTP 302 → Dashboard 200؛ cause unverified | Medium |
| Add waiting classes | `/pro/pro_waiting_classes/add-waiting-classes/` | Dashboard-visible link؛ form لم يُفتح | UNKNOWN: HTTP 302 → Dashboard 200 | Medium |
| Teacher waiting settings | `/pro/pro_waiting_classes/teacher-waiting-classes/` | Dashboard-visible settings link؛ page لم تُفتح | UNKNOWN: HTTP 302 → Dashboard 200 | Medium |
| Print templates | `/pro/pro_editor/list/` | Template listing بعمود اسم وإجراءات | AVAILABLE: list HTTP 200؛ editor/export behavior UNKNOWN | Medium |
| Advanced settings | `/pro/pro_dashboard/advanced/` | Multiple/current table controls؛ timing/teacher options؛ notification templates وwaiting visibility controls | AVAILABLE: HTTP 200؛ save/delivery UNKNOWN | Medium؛ timetable-related subset only |
| History / restore points | `/pro/pro_dashboard/table-history/` | Filters؛ save restore point؛ restore into new table أو replace current مع backup description | AVAILABLE: HTTP 200؛ restore execution UNKNOWN | High |
| Academic-year/semester workflow | No dedicated route confirmed | لم يُثبت في initial inventory | NOT FOUND within this pass؛ whole-product UNKNOWN | High if selected |
| Review / table analysis | `/pro/pro_dashboard/table-analysis/` | Preview link؛ الفصل/المادة/الحالة filters وoff/wrong/clean statuses | AVAILABLE: HTTP 200؛ server rules/repair UNKNOWN | High |
| Pre-generation report | `/pro/pro_dashboard/auto-gen-preflight/` | Report، acknowledgment، Deep check وGenerate controls؛ route معلن في client | Report AVAILABLE: HTTP 200؛ POST actions UNKNOWN | High |

Exams، Daily Tasks، Profit، external notifications وsubscription purchases خارج النطاق. ظهور links ليها لا يوسع التحقيق.

## Premium and restriction table

| Premium Feature | Observed Description | Access Status | Existing in My Project? | Evidence | Recommendation |
|---|---|---|---|---|---|
| Waiting-class management؛ premium status unconfirmed | Three visible-link routes redirect to Dashboard؛ global Trial/upgrade text | UNKNOWN؛ restriction confirmed، premium cause INFERRED only | Local daily substitution/Standby موجودين؛ weekly auto distribution لم يُثبت | Waiting root/subpages artifacts | لا unlock/hidden endpoints؛ كمل المتاح، وراجع official descriptions لو المجال اتختار |
| Generation؛ premium status unconfirmed | Start-generation control موجود، لم يُضغط | Preview AVAILABLE؛ execution UNKNOWN | Local deterministic solver موجود | Preview DOM | Trace settings/bundles/results أولًا؛ persistent generation تحتاج إذن |
| Custom print templates؛ premium status unconfirmed | Listing مفتوحة؛ editing/printing غير مختبرين | List AVAILABLE؛ remainder UNKNOWN | PDF ثابت موجود؛ custom editor غير مثبت محليًا | Editor-list inventory | Optional requirement بعد اختيار المستخدم؛ مش missing confirmed feature |

لا صف مصنف `PREMIUM LOCKED` أو `ROLE RESTRICTED` كحقيقة بدون denial attributable للفئة دي.

## Initial comparison candidates

دي potential improvements من UI observation، مش verified scheduling-rule gaps.

| Candidate | Reference observation | Local counterpart | Provisional conclusion |
|---|---|---|---|
| Default/per-day timing and breaks | Basic/break tabs وdaily controls | BellScheduleTemplate/revisions، timings/break editor | Already exists؛ preserve integrated organization |
| Subject single/paired/early/room configuration | Course headers/controls | SubjectRulesRequest والـconstraints | Already largely supported؛ exact semantics pending |
| Matrix/list assignments and teacher focus | Assignments controls | TeachingAssignmentsComponent والـallocation services | UX candidate؛ لا new domain model مبرر لحد دلوقتي |
| Restore snapshot into a new table | Explicit new-table vs replace-current descriptions | RestoreAsync creates a new version of current timetable؛ create/setup flows موجودة | Trace clone-from-snapshot؛ version restore وside-by-side clone مختلفين |
| Print-template listing | Dedicated list accessible | PDF renderer + placeholder print route | Local navigation gap confirmed؛ templates optional |
| Preview density / color toggles | Preview controls | Customized grid/fullscreen/subject colors | Preserve layout؛ assess benefit later |
| Weekly standby allocation | Links ظاهرة، لكن routes بتعمل redirect | Daily cover وStandby entries confirmed | UNKNOWN؛ لا نفترض equivalence أو algorithm |

## Evidence cycle

| Finding | Observation → hypothesis → experiment → result → conclusion | Confidence / uncertainty |
|---|---|---|
| Authenticated access | Dashboard target → session likely active → read config routes → 200 without login form → authenticated read access confirmed | High؛ write permission untested |
| Waiting restriction | Visible links → page may be unavailable → GET root and two children → three redirects → access restriction confirmed | High for redirect؛ cause UNKNOWN |
| Subscription cause | Trial/no-active texts → possible plan gating → search visible feature-specific denial → none observed → premium attribution remains inference | Low for cause |
| Snapshot clone UI | New-table/replace descriptions → likely two restore modes → read DOM only → UI copy confirmed | High for UI؛ backend semantics UNKNOWN |
| Functional overlap | Similar timing/subject controls → capability may exist locally → compare local contracts → overlap candidate supported | Medium؛ matching fields don't prove matching rules |

## Target selection — completed

المستخدم وافق نبدأ بالاختيار الأول. [Deep scheduling investigation](deep-scheduling-investigation.md) هو current result: client flows وGET observations ومقارنة source، مع preferred-room defect مُعاد إنتاجه ببيانات synthetic. الخيارات تحت محفوظة كسجل للاختيار؛ لم يطلب تنفيذ تغييرات application.

المستخدم يختار scope للـdeep investigation حسب Phase 5.2:

1. **قواعد الجدولة → التوليد → المراجعة**: أعلى قيمة لتحديد missing business logic؛ نبدأ بالإعدادات والـbundles والنتائج الحالية، من غير توليد محفوظ بدون إذن.
2. **التوقيتات → المعلمين → المواد والإسنادات**: مقارنة dependencies، validation، bulk workflows وإتاحة المعلمين؛ saves تحتاج إذن منفصل.
3. **النسخ والاستعادة → الطباعة وUX**: clone-vs-restore، template capabilities والتنقل؛ restore actions تحتاج إذن منفصل.

حصص الانتظار ممكن investigation منفصلة لما سبب redirects يتحدد من دليل مسموح. عدم إتاحتها مش سبب لإيقاف المجالات المفتوحة.

الخطوة التالية مراجعة نتائج النطاق المختار والـroadmap. لا implementation أو layout replacement دون موافقة منفصلة. نتائج التوليد المرجعي والفحص العميق POST غير مختبرة.
