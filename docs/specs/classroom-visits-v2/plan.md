# Implementation Plan: Classroom Visits V2 — استبدال نظام الزيارات بالـPrototype المعتمد

**Branch:** `main`  
**Date:** 2026-09-19  
**Status:** CUTOVER IMPLEMENTED — V2 is globally configured as default and V1 is read-only; operational acceptance is blocked by 10 non-final development V1 records and the pending browser/restore rehearsal; Phase 9 requires separate approval
**Prototype source:** `C:\Users\abdelrhman\Desktop\index.html`  
**Prototype SHA-256:** `86302A58348798B5BAD6A250708B26677BC6E11ACE3CDEC5C272B2AC2FC47827` (VERIFIED MATCH ✅)  
**Method:** GitHub Spec Kit `plan.md` structure، مع دمج Research/Data Model/Contracts داخل ملف واحد بطلب صاحب المشروع  
**Related project memory:** `.spec/constitution.md`, `docs/02-DOMAIN-MODEL.md`, `docs/03-ROLES-AND-PERMISSIONS.md`, `docs/05-API-ENDPOINTS.md`, `docs/09-RUBRIC-AND-EVALUATION.md`, `docs/10-IMPROVEMENT-PLANS-AND-FOLLOWUPS.md`, `docs/phases/PHASE-04-VISITS-AND-SCORING.md`

> بدأ التنفيذ بأمر صاحب المشروع. نُفّذت التغييرات بصورة additive خلف feature flags؛ لم تُحذف جداول أو بيانات تاريخية، ولم تُنفّذ مرحلة التقاعد الاختيارية.

## Summary

المطلوب هو استبدال تجربة الزيارات الحالية بتجربة الـHTML prototype المعتمدة من العميل، مع نقل منطقها وتفاصيلها إلى منصة الفلاح الحقيقية بدل تشغيلها كتطبيق `localStorage` معزول. تم حسم القرارات التسعة الأساسية: الالتزام بنظام تقييم ومؤشرات ودرجات الـ Prototype بنسبة 100%، مع ربطه بهوية وصلاحيات وعزل مدارس وتوقيعات المنصة، والإبقاء على دورة اعتماد مدير المدرسة (`PendingApproval -> Approved`)، وحفظ الخطة العلاجية المدمجة في قاعدة البيانات، والاحتفاظ ببيانات نوع الزيارة الثرية (الفئة والتسلسل)، وإلغاء مسح الأرشيف واستيراد/تصدير JSON، وحذف زيارات التطوير التجريبية السابقة.

## Technical Context

| Item | Current / Target |
|---|---|
| Backend | ASP.NET Core Web API، target framework `net8.0` |
| Frontend | Angular 17، standalone components، signals/reactive forms |
| UI | PrimeNG 17 + PrimeFlex + منصة الفلاح RTL design tokens |
| Storage | SQL Server + EF Core 8 Code First |
| Architecture | Layered modular monolith: Controller → Application Service → Repository/Data adapter → Database |
| API envelope | `ApiResponse<T>` حسب دستور المشروع |
| Security | ASP.NET Identity + JWT + database-driven roles/permissions + backend school scoping |
| Reporting | QuestPDF server-side Arabic PDF، مع school branding و`UserSignature` |
| Existing tests | xUnit/FluentAssertions backend؛ Angular test runner frontend |
| Target platform | Multi-school authenticated web application؛ Arabic primary + RTL؛ English parity required |
| Formal performance SLA | < 200ms لمعظم الـ API endpoints، و < 1s لتوليد تقرير الـ PDF؛ منع N+1 بالكامل، pagination للأرشيف، وتجميع dashboard في SQL |
| Expected scale | نظام متعدد المدارس يدعم مئات المعلمين وآلاف الزيارات سنوياً؛ قوائم وديناميكيات حقيقية من قاعدة البيانات |
| Migration constraint | لا hard delete في جداول المنظومة الأساسية؛ إضافة أعمدة وجداول V2 بصورة additive؛ حذف الزيارات التجريبية السابقة في بيئة التطوير |

## Constitution Check

### Pre-design gate

| Constitution rule | Result | How this plan complies |
|---|---|---|
| Spec kit is project memory | PASS | الخطة داخل `docs/specs/`، مع README change-log ورابط من Phase 4 |
| Layered modular monolith؛ no microservices | PASS | نفس solution والمشروعات الحالية، ولا خدمة مستقلة جديدة |
| Thin controllers؛ no EF in controllers | PASS | كل قواعد التقييم/الحفظ/الأمان في service/repository layers |
| `ApiResponse<T>` everywhere | PASS | كل JSON endpoints تستخدم envelope الحالي؛ file downloads فقط تظل binary responses |
| Backend school scope | PASS | كل list/detail/export/import/dashboard query يفرض `ActiveSchoolId` أو global-admin bypass داخل backend |
| Database-driven permissions | PASS | إعادة استخدام `Visit.*` أو إضافة permissions مسجلة ومربوطة بالأدوار؛ لا role-only UI security |
| Arabic primary + RTL | PASS | نصوص الـprototype العربية تصبح i18n keys، مع English leaf parity |
| Historical accuracy | PASS | versioned rubric/indicators + immutable visit snapshot؛ لا تعديل للزيارات القديمة |
| Stop/report after each phase | PASS | الخطة مقسمة إلى gates؛ لا انتقال للمرحلة التالية قبل تقرير وverification |
| No destructive action before clarity | BLOCKED BY DESIGN | قرارات الإلغاء/الاعتماد/المتابعات/البيانات القديمة مطلوبة قبل التنفيذ |

### Post-design gate

التصميم المقترح يمر دستوريًا بشرط اعتماد قرارات المنتج في آخر الملف. لا يوجد مبرر دستوري لحذف الجداول الحالية في نفس إصدار cutover؛ لذلك الحذف المادي مفصول كمرحلة أخيرة اختيارية.

## Project Structure

### Documentation for this feature

```text
docs/specs/classroom-visits-v2/
└── plan.md                       # هذا الملف؛ الخطة وقرارات البحث والتصميم والعقود
```

عند اعتماد الخطة وقبل كتابة الكود، يتم — إن رغبت — فصل المحتوى حسب Spec Kit الكامل إلى:

```text
docs/specs/classroom-visits-v2/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── visits-v2-api.md
└── tasks.md
```

### Expected source-code touch points

```text
backend/
├── AlFalah.Domain/
│   ├── Entities/                 # rubric indicators, observed indicators, inline treatment snapshots
│   └── Enums/                    # V2 scoring/rule-set markers if approved
├── AlFalah.Application/
│   ├── DTOs/Visits/
│   ├── Interfaces/
│   ├── Validators/Visits/
│   └── Analysis/                 # pure, golden-tested prototype parity engine
├── AlFalah.Infrastructure/
│   ├── Data/Configurations/
│   ├── Data/Migrations/
│   ├── Repositories/             # visit-specific query/persistence boundary
│   └── Services/                 # orchestration, school scope, transactions, audit
├── AlFalah.Api/Controllers/
└── AlFalah.Tests/
    ├── Analysis/
    ├── Integration/VisitsV2/
    ├── Security/
    └── Reports/

frontend/src/app/
├── core/models/
├── core/services/
├── features/visits/
│   ├── visit-workspace/          # tabs: card / archive / indicators
│   ├── observation-card/
│   ├── visit-report/
│   ├── visits-archive/
│   └── visits-dashboard/
├── shared/                       # reuse existing controls/tokens only where genuinely shared
└── assets/i18n/{ar,en}.json
```

**Structure decision:** لا يتم نسخ ملف HTML كـiframe أو إدخاله كسكربت global. تُفكك وظائفه إلى Angular components وpure TypeScript utilities، وتُنفذ قواعده authoritative في backend أيضًا لمنع التلاعب.

## Phase 0 Research — What the Prototype Actually Contains

### Fixed source data

- 5 مجالات.
- 25 معيارًا بتوزيع `5 / 5 / 6 / 3 / 6`.
- 66 مؤشر/شاهد ملاحظة بتوزيع `13 / 13 / 18 / 9 / 13`.
- 4 درجات فقط في الواجهة: 1 منخفضة، 2 متوسطة، 3 مرتفعة، 4 مرتفعة جدًا.
- 5 قوالب خطة علاجية، قالب لكل مجال.
- 3 مقيمين hardcoded مع توقيعات Base64.
- 24 معلماً hardcoded من جدول المدرسة.
- 5 زيارات seeded محفوظة كنماذج فعلية داخل الملف.
- تخزين المتصفح عبر مفاتيح `alfalah_evaluator`, `alfalah_teachers`, `alfalah_visits` مع version markers.

### Exact scoring behavior in the prototype

1. كل معيار يبدأ فعليًا بدرجة `1`، حتى لو لم يلمس المستخدم أي شيء.
2. إجمالي الزيارة = مجموع درجات 25 معيارًا، أي `25..100` في الـprototype الحالي.
3. نسبة المجال = `round(sum / (standardsCount × 4) × 100)`.
4. المستوى الكلي ومستوى المجال:
   - `>= 85`: متحقق بدرجة مرتفعة جداً.
   - `>= 65`: متحقق بدرجة مرتفعة.
   - `>= 50`: متحقق بدرجة متوسطة.
   - أقل من 50: متحقق بدرجة منخفضة.
5. نقطة قوة عندما نسبة المجال `>= 75`.
6. مجال تحسين عندما نسبة المجال `< 65`.
7. المجالات من 65 إلى 74 ليست قوة ولا تحسينًا.
8. اختيار المؤشرات يقترح الدرجة آليًا:
   - صفر مؤشرات → 1.
   - كل المؤشرات → 4.
   - معيار بثلاثة مؤشرات: واحد → 2، اثنان → 3.
   - معيار بمؤشرين: واحد → 3، اثنان → 4.
9. الدرجة اليدوية مسموحة، لكن أي تغيير لاحق في checkbox يعيد تطبيق الاقتراح الآلي.
10. الرصد السريع:
    - 4: يحدد كل المؤشرات.
    - 3: يحدد أول مؤشرين.
    - 2: يحدد أول مؤشر.
    - 1: يلغي المؤشرات.
11. `completedStandardsCount` يزيد لأي درجة `>=1`. لأن البداية 1، الكود الحالي يعرض عمليًا 25/25 مكتملة بعد initialization، رغم أن markup الابتدائي يقول 0/25.

### Exact feature inventory

| Prototype feature | Existing platform equivalent | V2 requirement |
|---|---|---|
| اختيار المقيم modal | JWT current user + roles | قرار مطلوب: derive from login موصى به، لا hardcoded identities |
| إدارة المعلمين داخل modal | Teachers/InstructorProfile module | reuse directory؛ لا localStorage أو parallel teacher table |
| بيانات الحصة | Visit metadata موجودة جزئيًا | إضافة رقم الحصة؛ حسم category/sequence mapping |
| 66 checkboxes | غير موجودة في schema | versioned indicators + persisted selected observations |
| درجات 1–4 | VisitScore يدعم 0–4، UI يستخدم 1–4 | V2 contract 1–4؛ legacy 0 يبقى history-only |
| live total/domain score | form الحالي يعرض completion فقط | pure UI preview + server recomputation عند save/finalize |
| quick bulk rate | غير موجود | parity implementation مع tests للحالات ذات 2/3 مؤشرات |
| printable report | detail + preview + QuestPDF | إعادة تصميمهما على شكل prototype مع server-side PDF |
| strengths/weaknesses | VisitAnalysis موجود بقواعد مختلفة | rule-set versioning؛ لا recompute للقديم |
| inline treatment plan | ImprovementPlan/FollowUp module منفصل | قرار مطلوب: snapshot داخل التقرير أم استمرار module الحالي |
| archive search/filter/delete | visits list موجود بفلاتر مختلفة | UI مطابق، server pagination/scope، soft delete فقط |
| CSV | bulk ZIP/PDF موجود | server-generated UTF-8 BOM CSV حسب أعمدة prototype |
| JSON backup/import | غير موجود | قرار أمني مطلوب؛ لا browser overwrite مباشر |
| prototype dashboard | dashboards الحالية مختلفة وأوسع | workspace dashboard scoped + تحديث dashboards العامة |

### Important mismatches with the current production design

1. الـprototype توزيع مجالاته `5/5/6/3/6`؛ النظام الحالي seeded rubric هو `6/4/6/3/6`. المعيار «توفر المدرسة مصادر تعلم متنوعة…» انتقل في الـprototype من D1 إلى D2.
2. الـprototype يحفظ المؤشرات المرصودة؛ النظام الحالي لا يملك entity للمؤشرات أصلاً، ويحفظ `EvidenceNote` فقط.
3. الـprototype يستخدم مستويات 50/65/85 على مقياس 100؛ النظام الحالي يستخدم ستة مستويات على متوسط 0–4.
4. الـprototype يبدأ كل شيء بدرجة 1؛ النظام الحالي يبدأ score = null ويمنع submit حتى اكتمال الرصد.
5. الـprototype ينهي الزيارة ويحفظها فور توليد التقرير؛ النظام الحالي Draft → PendingApproval → Approved/Rejected/Reopened.
6. الـprototype لا يملك multi-school security، permissions، instructor visibility، complaints، audit trail، أو report view logs.
7. الـprototype يملك نوع زيارة واحد بأربع قيم «أولى/ثانية/ثالثة/متابعة»؛ النظام الحالي يفصل 9 VisitCategory values عن 7 VisitSequence values.
8. تعديلات `contenteditable` على الخطة العلاجية داخل التقرير لا تُحفظ في prototype؛ تختفي بعد إعادة فتح التقرير.
9. الزيارات الجديدة في prototype لا تملك input عام لملاحظات الزائر، رغم أن الـseeded visits تعرض `notes` في التقرير.
10. JSON import في prototype يسمح merge أو overwrite من المتصفح، وهو غير آمن كتصرف مباشر في منصة متعددة المدارس.

## Proposed Target Design

### 1. Safe coexistence, not delete-first

- إضافة `VisitsV2` feature flag قابل للتفعيل حسب المدرسة/البيئة.
- بناء V2 بواجهات وعقود مستقلة مؤقتًا، مع بقاء `/visits` الحالي فعالًا أثناء التطوير.
- الزيارات القديمة تظل مرتبطة بـrubric/version/rule-set القديم وتفتح read-only بنفس نتائجها المخزنة.
- المدرسة التجريبية تستخدم V2 للزيارات الجديدة فقط.
- بعد acceptance، تتحول navigation/routes إلى V2؛ old UI يصبح legacy read-only.
- حذف old code/tables ليس جزءًا من cutover. يوضع في release منفصل بعد مدة احتفاظ وموافقة وbackup.

### 2. Source-of-truth boundaries

- الـprototype هو source of truth للنصوص الـ25/66، ترتيب العرض، score suggestion، thresholds، report layout، archive columns، dashboard formulas، CSV columns.
- منصة الفلاح هي source of truth للمستخدم الحالي، المدرسة الحالية، المعلمين، المواد/الفصول، permissions، signatures، school branding، audit، soft delete، والتخزين.
- backend يعيد حساب كل score/analysis ولا يثق في total/domain values المرسلة من frontend.
- frontend يعرض live preview فقط؛ النتيجة المحفوظة تأتي من server response.

### 3. Layer ownership

| Concern | Layer |
|---|---|
| Request/response + HTTP status + permission attribute | Controller |
| workflow، validation، rule-set selection، transactions، audit | Application/Infrastructure service |
| EF queries، projections، atomic persistence | visit-specific repository/data adapter |
| score/domain/strength/weakness calculation | pure `VisitV2AnalysisEngine` in Application |
| indicator-to-score suggestion | pure shared rule implemented authoritatively in backend ومكرر للـUX في frontend مع golden tests |
| view state، reactive form، accessibility | Angular components |
| PDF/CSV/JSON streaming | report/export services؛ لا logic داخل controller |

## Phase 1 Design — Data Model

> الأسماء أدناه مقترحة، وتثبت فقط بعد إجابات المنتج. العلاقات مقصودة وليست تخمينًا نهائيًا.

### RubricIndicator (new, versioned)

| Field | Rule |
|---|---|
| Id | PK |
| RubricStandardId | required FK؛ indicator belongs to exactly one standard version |
| Code | stable within its standard version |
| TextAr | exact Arabic prototype text، Unicode + Arabic collation |
| SortOrder | preserves prototype order |
| IsDeleted/DeletedAt/DeletedByUserId | soft delete metadata |

`RubricStandard` gains `ICollection<RubricIndicator>`. Rubric copy-on-write must clone indicators with the standard. The active V2 rubric is a new version; V1 is never edited in place.

### VisitObservedIndicator (new bridge/event entity)

| Field | Rule |
|---|---|
| Id | PK |
| VisitScoreId | required FK |
| RubricIndicatorId | required FK |
| IndicatorTextArSnapshot | immutable exact text used in the report |
| CreatedAt | UTC |
| IsDeleted/... | soft delete if needed by edit/reopen workflow |

- Unique index on `(VisitScoreId, RubricIndicatorId)`.
- Only selected indicators require rows; unselected indicators are read from the visit's rubric snapshot.
- Service validates that every submitted indicator belongs to the submitted score's standard and same rubric version.
- No query-inside-loop; validate all IDs in one set query.

### Visit additions (additive)

| Field | Purpose |
|---|---|
| ClassroomPeriod | 1..7 for prototype period selector |
| ExperienceVersion | e.g. `1=Legacy`, `2=PrototypeV2` |
| ScoringRuleSetVersion | preserves which thresholds/formulas produced the report |
| EvaluatorNameSnapshot | optional but recommended for historical report stability |
| EvaluatorRoleSnapshot | optional but recommended |

Existing `InstructorId`, `CreatedByUserId`, `VisitDate`, `Subject`, `GradeClass`, `LessonTitle`, `PresentCount`, `AbsentCount`, `Notes`, soft-delete and audit fields remain. `CreatedByUserId` is the authenticated evaluator; no free evaluator ID is accepted from the client.

`VisitCategory` and `VisitSequence` are not removed in the additive migration. New V2 write contracts either map prototype type to `VisitSequence` or introduce a dedicated enum after product decision. Legacy values remain readable.

### VisitScore additions

- Existing `Score` remains persisted as 1..4 for V2; value 0 remains legal only for legacy rows/contracts until retirement.
- Existing `EvidenceNote` remains the free-form «ملاحظات وشواهد إضافية» field.
- Navigation to `VisitObservedIndicator` rows is added.
- Updates replace the selected-indicator set transactionally with the score/evidence change.

### VisitAnalysis additions

| Field | Purpose |
|---|---|
| RuleSetVersion | prevents interpreting old analysis with V2 thresholds |
| OverallPercentage | explicit 0..100 result used by V2 |

Existing `TotalScore`, `MaximumScore`, domain rows and JSON snapshots remain useful. For V2:

- `TotalScore = sum(scores)`.
- `MaximumScore = standardsCount × 4`.
- `OverallPercentage = round(TotalScore / MaximumScore × 100)`.
- With the locked 25-standard prototype, `TotalScore == OverallPercentage` numerically.
- `PerformanceLevelAr` uses the V2 label thresholds.
- Persisted legacy `OverallScore` is never rewritten.

### VisitTreatmentRecommendation (conditional new entity)

يوصى بإنشاء entity مملوكة للزيارة بدل ربط التقرير بنظام Follow-up إذا كان المطلوب إلغاء المتابعات:

| Field | Rule |
|---|---|
| Id | PK |
| VisitId | required FK |
| RubricDomainId | nullable للهدف المخصص |
| DomainNameArSnapshot | exact report text |
| Goal | required |
| Actions | required، multiline |
| SuccessIndicators | required |
| Source | generated/manual |
| SortOrder | report order |
| Created/Updated + soft-delete metadata | audit-safe |

يتم توليد العناصر من المجالات `<65%`، ثم يسمح للمستخدم بالتعديل/الإضافة/الحذف مع حفظها في DB قبل PDF. إذا كان المطلوب هو التطابق الحرفي حتى في عدم الحفظ، لا تُنشأ هذه entity ويُعامل التعديل كـprint-only؛ لكن هذا غير موصى به.

### Entities deliberately reused

- `ApplicationUser`, `InstructorProfile`, `InstructorClass` بدل teacher table جديدة.
- `School`, `SchoolReportSettings`, `UserSignature` بدل أسماء/شعارات/توقيعات hardcoded.
- `AuditLog` لكل finalize/edit/delete/import/approval action.
- `ReportViewLog` إذا استمر instructor report workflow.
- لا يتم نسخ prototype Base64 signatures إلى source أو DB production.

## Draft API Contracts

المسار الآمن أثناء coexistence هو `/api/v2/visits`. يمكن بعد cutover إبقاء frontend route `/visits` كما هي وتغيير service فقط. كل JSON response يستخدم `ApiResponse<T>`.

| Method | Proposed route | Purpose |
|---|---|---|
| GET | `/api/v2/visits/observation-card` | active V2 rubric tree: domains → standards → indicators + score labels/rules |
| POST | `/api/v2/visits` | create a V2 draft from current school/current user; snapshot active V2 rubric |
| PUT | `/api/v2/visits/{id}` | save metadata, scores, evidence, selected indicator IDs atomically |
| POST | `/api/v2/visits/{id}/finalize` | validate/recompute; generate analysis and treatment recommendations; transition per approved workflow decision |
| GET | `/api/v2/visits` | paged scoped archive with search/evaluator/date/type filters |
| GET | `/api/v2/visits/{id}` | full card/report detail with snapshot indicators |
| DELETE | `/api/v2/visits/{id}` | soft delete only، with current permission/workflow gates |
| GET | `/api/v2/visits/dashboard` | scoped KPIs/evaluator/domain/top-bottom aggregates |
| GET | `/api/v2/visits/export/csv` | UTF-8 BOM CSV matching the 22 prototype columns |
| GET | `/api/v2/visits/{id}/report/pdf` | official server PDF matching prototype report layout |
| PUT | `/api/v2/visits/{id}/treatment-recommendations` | persist edited/generated report plan items if approved |
| GET | `/api/v2/visits/export/backup` | optional، permission-gated server export if JSON backup is retained |
| POST | `/api/v2/visits/import/backup` | optional dry-run then commit؛ never direct client overwrite |

### Write contract invariants

- Client never sends `SchoolId`, evaluator identity, evaluator role, signatures, totals, domain percentages, strengths or weaknesses as trusted data.
- Instructor must be active and assigned as Instructor in target school.
- Every score standard ID must belong to the visit's immutable rubric version.
- Every selected indicator ID must belong to its standard.
- No duplicate score or indicator IDs.
- V2 score must be 1..4 after completion; draft behavior depends on the default-score decision.
- Attendance and absence are non-negative.
- Period is 1..7.
- All writes use `CancellationToken`, validation, transaction, and audit.

### Read/report contract additions

- Standard DTO returns ordered `indicators[]` with `isObserved`.
- Analysis returns `totalScore`, `maximumScore`, `overallPercentage`, exact V2 label, domain percentages, strengths and improvement areas.
- Archive returns evaluator name/role snapshot, period label, teacher/subject/class, and total out of maximum.
- Dashboard response returns denominators; frontend must not hardcode `/24` teachers.

## Frontend Integration Design

### Workspace shell

داخل shell الحالي للمنصة، صفحة الزيارات V2 تحتوي tabs شبيهة بالـprototype:

1. `بطاقة الزيارة`.
2. `سجل الزيارات` مع count.
3. `لوحة المؤشرات`.

لا يتم تكرار navbar المدرسة العامة داخل feature. اسم المدرسة والشعار والمستخدم الحالي يؤخذون من platform header/context. الشكل يستخدم ألوان ومسافات المنصة مع الحفاظ على hierarchy والـRTL والتفاصيل الوظيفية للـprototype.

### Observation card

- live score banner ثابت/واضح مع الإجمالي والمستوى وعدد المعايير المكتملة.
- metadata grid: teacher، subject، class، lesson، date، period، prototype visit type، attendance، absence.
- teacher selection from scoped API، مع subject/class auto-fill الحالي.
- five domain cards، 25 standard rows، 66 checkboxes، free evidence input، four rating buttons.
- quick bulk actions 4/3/2/1.
- unsaved-changes guard عند الخروج.
- keyboard focus، labels، aria-pressed/checked، mobile stacking، وRTL.
- live calculation في pure utility، ثم reconciliation مع server response بعد save.

### Report

- نفس ترتيب prototype: header، metadata table، overall score، five-domain summary، strengths/improvement، treatment recommendations، general notes إن تقرر input لها، detailed standards + observed checkboxes + evidence، evaluator/teacher signatures.
- النسخة التفاعلية تسمح بتعديل treatment recommendation ثم تحفظها إن اعتمد قرار persistence.
- الطباعة الرسمية من server PDF؛ `window.print()` يمكن أن يبقى preview convenience فقط.
- branding/signatures من المنصة، مع fallback آمن؛ لا أسماء مدرسة أو مستخدمين ثابتة.

### Archive

- search في teacher/subject/class/evaluator server-side.
- evaluator filter ديناميكي من المستخدمين الذين لديهم زيارات ضمن scope، لا ثلاثة أسماء ثابتة.
- pagination؛ لا تحميل كل الزيارات للمتصفح.
- soft-delete action permission-gated؛ لا «مسح الأرشيف» hard delete.
- CSV export مطابق.
- JSON backup/import لا يظهر إلا إذا اعتمد المنتج عقده الآمن.

### Dashboard

- total visits.
- average overall percentage.
- high achievement rate where score `>=65%`.
- unique visited teachers / total active teachers in scope (denominator ديناميكي).
- evaluator workload dynamically.
- averages for five prototype domains.
- top 3 and bottom 3 standards from persisted V2 scores.
- exclude deleted visits؛ status inclusion depends on approval decision and is stated visibly in UI.

## Prototype Parity Matrix

| Capability | Parity acceptance |
|---|---|
| Arabic content | exact 25 standards, 66 indicators, 5 template texts copied verbatim and snapshot-tested |
| Ordering | 5 domains and all nested items preserve prototype order |
| Indicator suggestion | every 0/1/2/3 checked-count case produces prototype score |
| Manual rating | 1..4 buttons and checkbox recalculation precedence match approved behavior |
| Bulk rating | all four actions verified, including two-indicator standards |
| Totals | each of the 5 seeded prototype visits reproduces total/domain percentages |
| Classification | 49/50/64/65/74/75/84/85 boundary tests |
| Report | same sections/labels/order and print-safe RTL at A4 |
| Treatment plan | weak-domain template generation and custom item behavior match approved persistence decision |
| Archive | search/filter/order/delete semantics match, adjusted only for server security/soft delete |
| CSV | exact header order, Arabic encoding, values and escaping |
| Dashboard | same formulas; teacher/evaluator denominators become dynamic platform data |
| Signatures | visual position matches؛ identity/image come from authorized platform records |

## Implementation Phases

### Phase 0 — Decision lock and immutable acceptance fixture (COMPLETED ✅)

**Goal:** منع اختلاف الفهم قبل لمس runtime وتثبيت البيانات القياسية كـ Golden Test Fixtures غير قابلة للتغيير.

1. إجابة وحسم الأسئلة التسعة رسميًا مع صاحب المشروع واعتمادها في هذا المستند. (COMPLETED ✅)
2. تثبيت ومطابقة البصمة الرقمية للـ Prototype: `86302A58348798B5BAD6A250708B26677BC6E11ACE3CDEC5C272B2AC2FC47827`. (COMPLETED ✅)
3. استخراج وتثبيت الـ 25 معياراً والـ 66 مؤشراً والـ 5 قوالب علاجية والـ 5 زيارات التجريبية والـ 24 معلماً والـ 3 مقيمين والـ 22 عموداً لملف CSV كـ Golden Test Fixtures داخل مجلد الاختبارات:
   - `backend/AlFalah.Tests/Fixtures/ClassroomVisitsV2/ClassroomVisitsV2GoldenFixtures.cs`
   - `backend/AlFalah.Tests/Fixtures/ClassroomVisitsV2/Data/domains_standards_indicators.json`
   - `backend/AlFalah.Tests/Fixtures/ClassroomVisitsV2/Data/treatment_plans.json`
   - `backend/AlFalah.Tests/Fixtures/ClassroomVisitsV2/Data/seeded_visits.json`
   - `backend/AlFalah.Tests/Fixtures/ClassroomVisitsV2/Data/default_teachers.json`
   - `backend/AlFalah.Tests/Fixtures/ClassroomVisitsV2/Data/official_signatures.json`
   - `frontend/src/app/core/fixtures/visits-v2-golden-fixtures.ts`
4. كتابة وتمرير 17 اختبار قبول آلي في `ClassroomVisitsV2GoldenFixturesTests.cs` للتحقق من:
   - صحة الـ SHA-256 للـ Prototype.
   - توزيع المجالات الـ 5 والمعايير الـ 25 والمؤشرات الـ 66 وقواعد الاقتراح والبداية 1 والإجمالي 25/100 والمستويات 85/65/50.
   - تطابق كافة حسابات ونقاط قوة وتحسين الزيارات الـ 5 مع قيم الـ Prototype الحرفية.
   - تطابق أعمدة ملف الـ CSV الـ 22 وقوالب الخطط العلاجية الـ 5.
5. اعتماد مصفوفة (Keep / Replace / Retire) بالكامل وتوثيقها. (COMPLETED ✅)

**Exit gate:** تم اجتياز بوابة الخروج بنجاح (497 اختبار backend ناجح، 110 اختبار frontend ناجح، وبناء خالي من الأخطاء).

### Phase 1 — Baseline protection and characterization tests (COMPLETED ✅)

1. تسجيل baseline لكافة اختبارات وبناء الـ backend والـ frontend. (COMPLETED ✅)
2. إضافة feature flags للـ VisitsV2 في كل من الباك إند (`FeatureFlags:VisitsV2` في `appsettings.json` و `appsettings.Development.json` و `IFeatureFlagService`) والفرونت إند (`environment.featureFlags.visitsV2` و `FeatureFlagService`) بقيمة افتراضية معطلة (OFF / false). (COMPLETED ✅)
3. بناء محرك التقييم النقي `VisitV2AnalysisEngine` داخل `AlFalah.Application/Analysis/` بنسبة تطابق 100% مع حسابات ومعادلات الـ Prototype المعتمدة ومستويات التحقق ونقاط القوة والتحسين واقتراح الدرجات والرصد السريع. (COMPLETED ✅)
4. كتابة Unit Tests شاملة في `VisitV2AnalysisEngineTests.cs` لكافة الحدود والحالات الشاذة والزيارات الخمس النموذجية. (COMPLETED ✅)
5. كتابة اختبارات حماية وتوصيف للنظام القديم في `LegacyVisitEngineCharacterizationTests.cs` لضمان عدم حدوث أي تراجع (Zero Regression) وعزل تام بين V1 و V2. (COMPLETED ✅)
6. اجتياز كافة أوامر الفحص الأربعة الإلزامية بنجاح تام وبدون أي تعديلات على schema أو قاعدة البيانات. (COMPLETED ✅)

**Exit gate:** تم اجتياز بوابة الخروج للمرحلة 1 بنجاح (547 اختبار backend ناجح، 112 اختبار frontend ناجح، وبناء backend و frontend خالي من الأخطاء، صفر Migrations).

### Phase 2 — Additive schema and active rubric V2 (COMPLETED ✅)

1. إضافة `RubricIndicator` و`VisitObservedIndicator` والعلاقات/indexes/collation.
2. إضافة visit/analysis version markers وperiod/percentage fields.
3. إضافة treatment snapshot entity فقط إذا اعتمدت.
4. إنشاء migration additive؛ لا drop/rename destructive.
5. Seed/create a new RubricVersion from exact prototype content؛ deactivate old active version only at controlled cutover، وليس بمجرد startup العادي.
6. تحديث rubric copy-on-write/editor contracts لدعم indicators أو قفل تحرير V2 حسب القرار.

**Exit gate:** migrate up on production-like copy، legacy data byte/row counts محفوظة، downgrade/rollback procedure موثق.

### Phase 3 — Domain, repository and analysis services (COMPLETED ✅)

1. pure V2 scoring/suggestion engine.
2. repository queries with projections، pagination، aggregates، no N+1.
3. visit service create/update/finalize with one transaction.
4. set-based ID validation for rubric/indicator/instructor scope.
5. audit events and soft delete cascade behavior.
6. rule-set-aware legacy/V2 mapping; never recompute persisted V1 analyses.

**Exit gate:** unit + integration tests for scoring، transactions، concurrency، school scope.

### Phase 4 — API contracts (COMPLETED ✅)

1. thin V2 controller and validators.
2. observation-card، CRUD/finalize، archive، dashboard، CSV، PDF endpoints.
3. approval/reject/reopen endpoints reused or adapted only after workflow decision.
4. Swagger examples and Arabic errors.
5. JSON import, if retained, implemented as two-step `dry-run → confirmed commit` with school scope, schema/version validation, duplicate policy, size limit and audit.

**Exit gate:** contract tests for 200/400/401/403/404/409 and cross-school isolation.

### Phase 5 — Angular observation workspace (COMPLETED ✅)

1. typed models/services.
2. workspace tabs and exact RTL visual hierarchy using platform tokens.
3. metadata + teacher auto-fill.
4. indicators، evidence، ratings، live score، quick bulk.
5. create/edit/read-only states and unsaved-changes protection.
6. Arabic/English i18n parity; no inline duplicated product strings.

**Exit gate:** component tests + desktop/mobile browser acceptance against golden scenarios.

### Phase 6 — Report, archive, exports and dashboard (COMPLETED ✅)

1. interactive report visual parity.
2. server PDF visual parity and signature/branding fallbacks.
3. archive search/evaluator filter/paging/soft delete.
4. CSV parity.
5. V2 dashboard aggregates/top-bottom lists.
6. optional safe backup/import.

**Exit gate:** PDF snapshots، CSV fixture diff، aggregate SQL tests، accessibility/print checks.

### Phase 7 — Dependent-module integration (COMPLETED ✅)

1. teacher history carries `experienceVersion`; progress charts select one version (V2 when present, otherwise V1) and never combine scales.
2. role dashboards use V2 percentage safely and do not mix 0–4 with 0–100.
3. instructor reports، view logs، complaints، approval banners updated if retained.
4. improvement-plan/follow-up navigation hidden or adapted exactly as approved.
5. bulk ZIP and report-preview routes point to V2 report service.

**Exit gate:** full dependency map green؛ no broken deep links or unauthorized data exposure.

### Phase 8 — Global cutover (CODE COMPLETE; OPERATIONAL REHEARSAL BLOCKED ⏸)

1. Production and Development flags are globally ON; `/visits` owns V2 and `/visits-v2` remains an alias.
2. V1 is exposed only at `/visits-legacy`; all seven V1 visit mutations return `410 Gone`.
3. Teacher history, instructor reports, complaint workflow, dashboards, CSV/PDF/ZIP and deep links dispatch by explicit experience version.
4. The 2026-09-22 development readiness query found 2 Draft + 8 PendingApproval V1 records. No automatic transition was performed.
5. Production deployment remains blocked until each pending V1 record has a documented disposition, a production-like restore is proven, and the role/browser matrix is completed with screenshots.
6. Rollback requires the previous approved API/frontend pair; disabling the V2 flag alone does not restore legacy writes.

**Exit gate:** client acceptance + backup confirmation + monitoring window with no unresolved severity-1/2 defect.

### Phase 9 — Optional retirement (NOT EXECUTED — separate approval required)

- remove dead frontend components/routes only after route telemetry and dependency scan.
- stop writes to legacy endpoints; keep reads for retention period.
- archive—not delete—legacy data where legally/business appropriate.
- dropping tables/columns requires a separate SQL script, verified backup, restore drill, and written approval.
- remove ImprovementPlan/PlanFollowUp/Complaint tables only if the product owner explicitly confirms the modules and historical records are no longer required.

## Delete / Keep Safety Matrix

| Area | First V2 release | Later optional retirement |
|---|---|---|
| Existing Visit rows | KEEP, read-only by version | archive per retention policy |
| Existing VisitScore/Analysis/DomainAverage | KEEP unchanged | never reinterpret; archive only |
| Current `/api/v1/visits` | KEEP historical GETs; writes return 410 | deprecate only after retention approval |
| Current Angular visit pages | KEEP behind legacy route/flag | delete after dependency proof |
| ImprovementPlan/PlanFollowUp data | KEEP even if UI hidden | removal needs explicit approval |
| Complaints/ViewLogs/AuditLogs | KEEP | preserve for audit unless retention policy says otherwise |
| Rubric V1 | KEEP inactive | never hard delete while referenced |
| Prototype seeded teachers/signatures | TEST FIXTURES ONLY by default | import only after owner mapping/approval |
| Prototype 5 seeded visits | GOLDEN TEST FIXTURES by default | production import only by explicit request |

## Testing and Verification Strategy

### Backend unit tests

- indicator suggestion truth table for 2- and 3-indicator standards.
- manual score/indicator precedence after product decision.
- bulk rating 1/2/3/4.
- total, maximum, percentage and domain percentage rounding.
- boundary labels at 49/50/64/65/74/75/84/85.
- strengths `>=75` and weaknesses `<65`.
- treatment-template exact text and fallback.
- all five prototype seeded visits as golden cases.

### Backend integration/security tests

- create/update/finalize round trip persists selected indicators and evidence.
- malicious total/percentage in request is ignored/rejected.
- indicator from another standard/version rejected.
- cross-school teacher/visit/report/export/import rejected.
- Moderator cannot see another Moderator's visits if D-37 retained.
- Instructor cannot see non-approved/other-teacher report if approval retained.
- soft-delete does not physically remove history.
- transaction rollback leaves no partial scores/indicators/analysis.
- old visits still return their original analysis and PDF.

### Frontend tests

- metadata validation and teacher auto-fill/fallback.
- 66 checkbox rendering from API, not hardcoded component arrays.
- live total/domain badges and completed count.
- bulk buttons and manual override behavior.
- tab navigation and unsaved-change guard.
- treatment edits persist/reload if approved.
- archive filters and paging.
- dashboard empty/non-empty states.
- ar/en key parity and RTL/mobile layout.

### Visual/report tests

- A4 PDF sections and order.
- Arabic font shaping and RTL tables.
- missing logo/signature fallback.
- evaluator + teacher signature placement.
- long evidence/treatment text pagination.
- draft/approved watermark behavior if approval remains.

### Required commands at implementation checkpoints

```powershell
dotnet build backend\AlFalah.slnx -c Release
dotnet test backend\AlFalah.slnx -c Release --no-build
npm --prefix frontend test -- --watch=false
npm --prefix frontend run build
```

Migration verification runs against a disposable/restored database before any shared environment. Production migration is deployed as a reviewed SQL script per project policy.

## Rollout and Rollback

### Rollout

1. Backup + restore proof.
2. Additive migration.
3. deploy code with V2 flag OFF.
4. pilot one school.
5. compare golden calculations and sample reports.
6. gradual enablement.
7. switch navigation only after acceptance.

### Rollback

- turn V2 feature flag OFF؛ routes return to legacy UI immediately.
- stop V2 writes؛ additive tables/columns remain harmless and preserve pilot data.
- do not attempt destructive down migration in production during incident response.
- restore DB only for confirmed data corruption, using the rehearsed backup procedure.
- replay/audit pilot writes if business decides to re-enter them later.

## Acceptance Criteria

1. العميل يطابق V2 بصريًا ووظيفيًا مع الـprototype في card/report/archive/dashboard.
2. exact 25 standards، 66 indicators، five domains and templates pass snapshot tests.
3. approved score/default/workflow decisions are demonstrated with boundary cases.
4. no browser-local source of truth؛ refresh/new device sees the same authorized data.
5. no cross-school or cross-moderator leak.
6. all destructive operations are soft delete in first release.
7. old reports retain their exact historical results.
8. PDF/CSV Arabic output is correct.
9. all dependent pages and deep links remain valid or intentionally redirected.
10. feature-flag rollback is rehearsed successfully.
11. backend/frontend build and tests are green.
12. spec kit, API docs, data model, decisions log and phase status are updated after each accepted phase.

## Risks and Mitigations

| Risk | Mitigation |
|---|---|
| Delete-first breaks reports/plans/complaints | additive V2 + feature flag + legacy read-only |
| Mixing old 0–4 averages with new 0–100 totals | explicit rule-set/version fields and typed DTOs |
| Missing indicator persistence | normalized versioned indicator + observation entities |
| Hardcoded prototype identities leak into production | map to authenticated users/directory; fixtures only |
| Client-side calculations are tampered | server authoritative recomputation |
| JSON overwrite corrupts multi-school data | omit or implement dry-run/validated/import transaction with permission |
| Rubric edits rewrite history | copy-on-write version + visit snapshot |
| Treatment edits disappear like prototype | persist visit-owned recommendation snapshot if approved |
| Dashboard becomes slow | SQL aggregates, pagination, indexes, no N+1 |
| Old data cannot supply indicators | never invent them; label legacy report and retain old renderer |

## Complexity Tracking

| Complexity | Why needed | Simpler alternative rejected because |
|---|---|---|
| Side-by-side V2 + feature flag | rollback and historical safety | replacing `/visits` in place makes failures and schema mismatches irreversible during rollout |
| Versioned indicators | exact 66-item form + historical report fidelity | storing only checkbox text/JSON loses relationships, validation and rubric copy-on-write integrity |
| Rule-set version | old and new thresholds/scales conflict | globally changing analysis would silently rewrite interpretation of historical visits |
| Explicit treatment snapshot entity (conditional) | edited report plan must survive reload/PDF | prototype DOM-only `contenteditable` loses user work; reusing follow-up workflow contradicts requested detachment |

## Approved Product Decisions (حسم قرارات المنتج المعتمدة)

تم عقد جلسة حسم القرارات رسميًا مع صاحب المشروع، وحُسمت جميع المسائل التسعة بالتفصيل كما يلي:

| # | المسألة / القرار | القرار النهائي المعتمد | التأثير المعماري والتنفيذي |
|---|---|---|---|
| **1** | **نطاق إلغاء المتابعات** | **إلغاء واجهات `Improvement Plans` و `Plan Follow-ups` فقط**، واستبدالها بالخطة العلاجية المدمجة بالتقرير. الإبقاء التام على دورة الاعتماد (`Approval/Reject/Reopen`) وظهور التقرير للمعلم (`Instructor Reports`) وسجل المشاهدة (`ReportViewLog`) ونظام الشكاوى (`Complaints`). | إخفاء موديول خطط التحسين المستقلة من القائمة للزيارات الجديدة، وتوجيه الخطة العلاجية لتكون كياناً تابعاً للزيارة V2 مباشرة مع الحفاظ على حوكمة الاعتماد والشكاوى. |
| **2** | **اعتماد مدير المدرسة** | **اعتماد مدير المدرسة إلزامي.** زر «إنهاء وتوليد التقرير» يحفظ الزيارة ويرسلها كـ `PendingApproval`، ولا تظهر النتيجة للمعلم المزار إلا بعد اعتماد المدير الرسمي (`Approved`). | الحفاظ الكامل على حوكمة الأمان وصلاحيات المدرسة وعزل الأدوار؛ المشرف يرصد وينشئ التقرير والمدير يعتمد أو يرفض للتعديل. |
| **3** | **نظام التقييم والدرجات والبداية الافتراضية** | **تطابق حرفي 100% مع الـ Prototype الجديد («نظام التقييم مثل البروتوتايب الجديد بالضبط، وإلغاء القديم»)**. تبدأ المعايير الـ 25 بدرجة 1، والإجمالي 25/100، والاكتمال 25/25، والدرجات 1–4، مع نفس معادلات ونسب المجالات وتصنيفات المستويات ونقاط القوة والتحسين. | محرك تقييم V2 (`VisitV2AnalysisEngine`) يطابق الـ Prototype بدقة مطلقة مع كافة Golden Tests، دون استخدام معادلات أو مقاييس التقييم القديمة للزيارات الجديدة. |
| **4** | **المقيمون والمعلمون والبيانات التجريبية** | **ربط كامل مع بيانات منصة الفلاح الحقيقية:** المقيم يؤخذ من الحساب المسجل دخوله وتوقيعه من `UserSignature`. المعلمون من قاعدة بيانات المدرسة الحالية مع الإكمال التلقائي. بيانات الـ Prototype الثابتة (3 مقيمين، 24 معلماً، 5 زيارات، التوقيعات الثابتة) تُحفظ كـ **Golden Test Fixtures** للاختبارات فقط ولا تُزرع في Production. | حماية استقلالية وعزل المدارس في المنصة ومنع تلوث بيانات الإنتاج بأسماء ثابتة، مع ضمان اختبار المطابقة الكامل للحسابات. |
| **5** | **نوع وتسلسل الزيارة** | **الاحتفاظ ببيانات المنصة الثرية كاملة وعدم اختزالها:** دعم حقلين في بطاقة الزيارة والتقرير والأرشيف: **فئة الزيارة (`VisitCategory` - 9 فئات)** و **تسلسل الزيارة (`VisitSequence` - التسلسلات الكاملة)**. | عدم تفريغ البيانات الإشرافية، وتمكين المدارس من تسجيل نوع الزيارة بدقة (تشخيصية، استطلاعية، علاجية، إلخ) وتسلسلها. |
| **6** | **أداة التقييم (Rubric V2)** | **اعتماد أداة الـ Prototype بالحرف كنظام رسمي ثابت ومجمد لـ V2:** 5 مجالات، 25 معياراً، و 66 مؤشر أداء بتوزيعها ونصوصها المطابقة. قفل تعديل المؤشرات في هذه المرحلة وتأجيل محرر المؤشرات الديناميكي لما بعد استقرار المنظومة. | إنشاء Rubric Version رسمي جديد مطابق لـ Prototype كمرجع وحيد لزيارات V2؛ واستقرار تام لهياكل الرصد والتقارير. |
| **7** | **الخطة العلاجية وتعديلاتها** | **حفظ الخطة العلاجية وتعديلاتها في قاعدة البيانات (`Visit-Owned Snapshot`):** توليد التوصيات آلياً للمجالات < 65%، وحفظ أي تعديل أو حذف أو إضافة لأهداف مخصصة في السيرفر لتظهر عند إعادة فتح التقرير وفي ملف الـ PDF الرسمي. | حل ثغرة فقدان البيانات الموجودة في كود الـ Prototype؛ المشرف لا يفقد تعديلاته وتطبع في تقرير السيرفر بدقة. |
| **8** | **إدارة الأرشيف والتصدير والنسخ الاحتياطي** | **إلغاء زر «مسح كامل الأرشيف» نهائياً.** الحذف فردي آمن فقط (`Soft Delete`). **إلغاء أزرار (JSON Backup / Import).** الإبقاء على تصدير Excel/CSV (22 عموداً UTF-8 BOM) وتصدير تقارير PDF الرسمية، والاعتماد على نسخ السيرفر الاحتياطية. | حماية البيانات من الحذف الشامل أو التلف؛ توفير تصديرات رسمية معتمدة فقط. |
| **9** | **مصير الزيارات التاريخية السابقة** | **مسح زيارات التطوير التجريبية السابقة («امسحها احنا فى حالة تطوير ودى كانت تجارب عشوائية»).** | تنظيف بيئة التطوير من الزيارات العشوائية السابقة، والبدء بقاعدة بيانات نقية تماماً بنظام V2 المعتمد. |

---

## Final Keep / Replace / Retire List

| الفئة (Category) | المكون / الخاصية | المصير المعتمد | ملاحظات التنفيذ |
|---|---|---|---|
| **KEEP** | حوكمة واعتماد مدير المدرسة (`PendingApproval`, `Approved`, `RejectedForChanges`, `Reopened`) | **KEEP** | الزيارة تُرسل للاعتماد ولا تظهر للمعلم إلا بعد موافقة المدير |
| **KEEP** | تقارير المعلم وسجل المشاهدة (`Instructor Reports` + `ReportViewLog`) | **KEEP** | المعلم يرى تقريره بعد الاعتماد ويسجل النظام توقيت الاطلاع |
| **KEEP** | نظام الشكاوى وطلبات المراجعة (`Complaints`) | **KEEP** | متاح للمعلم بعد قراءة التقرير المعتمد حصراً |
| **KEEP** | فئات وتسلسلات الزيارة الإشرافية الثرية (`VisitCategory` 9 فئات + `VisitSequence`) | **KEEP** | مدمجة في بطاقة الزيارة الجديدة والتقرير وسجل الأرشيف |
| **KEEP** | هوية المنصة وعزل المدارس والتوقيعات الرقمية (`SchoolScopeGuard` + `UserSignature`) | **KEEP** | المقيم والمعلم والتوقيع تؤخذ ديناميكياً من حسابات المنصة |
| **KEEP** | تصدير الأرشيف إلى إكسل (`CSV Export` بـ 22 عموداً مع UTF-8 BOM) | **KEEP** | مطابق للـ Prototype ويولد من السيرفر |
| **KEEP** | تصدير تقرير الزيارة إلى (`Server-side QuestPDF`) | **KEEP** | مطابق لتنسيق تقرير الـ Prototype وهوية المدرسة |
| **REPLACE** | شاشة وطريقة رصد الزيارة القديمة | **REPLACE** | استبدالها بـ Observation Workspace (بطاقة الزيارة + 66 مؤشر + درجات 1-4 + رصد سريع) |
| **REPLACE** | محرك حساب الدرجات والمستويات القديم | **REPLACE** | استبداله بـ `VisitV2AnalysisEngine` المطابق للـ Prototype (بداية 1، درجات 25-100، مستويات 50/65/85) |
| **REPLACE** | أداة التقييم القديمة (Rubric V1 - بدون مؤشرات) | **REPLACE** | تفعيل Rubric Version جديد يحتوي على الـ 5 مجالات والـ 25 معياراً والـ 66 مؤشراً الرسمية |
| **REPLACE** | شكل تقرير الزيارة في الواجهة | **REPLACE** | استبداله بتقرير الـ Prototype التفاعلي المكتمل مع الخطة العلاجية والتوقيعات |
| **REPLACE** | سجل الزيارات (الأرشيف) | **REPLACE** | استبداله بجدول أرشيف الـ Prototype مع البحث، والفلترة بالمقيم، والحذف الآمن |
| **REPLACE** | لوحة مؤشرات الزيارات المدمجة | **REPLACE** | إضافة Dashboard الـ Prototype بمتوسطات المجالات والمعايير الأعلى والأدنى |
| **RETIRE** | موديول وصفحات خطط التحسين والمتابعات المستقلة (`Improvement Plans & Follow-ups` UI) | **RETIRE** | إخفاء/إلغاء الواجهات المستقلة واستبدالها بالخطة العلاجية المدمجة بالتقرير |
| **RETIRE** | زر «مسح كامل الأرشيف» (`clearAllArchiveVisits`) | **RETIRE** | ملغي تماماً لحماية أمان البيانات |
| **RETIRE** | أدوات النسخ الاحتياطي والاستيراد المحلية (`JSON Backup / Import`) | **RETIRE** | ملغاة من الواجهة؛ الاعتماد على CSV/PDF والـ Database Backups |
| **RETIRE** | زيارات التطوير التجريبية العشوائية السابقة | **RETIRE** | تنظيفها ومسحها من بيئة التطوير للبدء على نظافة بنظام V2 |
| **RETIRE** | زر رصد الدرجة صفر (`Score = 0`) | **RETIRE** | نظام V2 يستخدم مقياس 1 إلى 4 حصراً |

---

## Remaining Conflicts & Ambiguities

> **النتيجة:** **لا توجد أي تعارضات متبقية (Zero Unresolved Conflicts / Zero NEEDS CLARIFICATION)**.  
> تم حسم كافة النقاط التقنية والوظيفية والتوافقية بنسبة 100% مع صاحب المشروع.

---

## Implementation Authorization Gate

لا يبدأ أي تعديل runtime أو migration قبل صدور أمر صريح من المستخدم بعبارة:  
**«ابدأ التنفيذ»**
