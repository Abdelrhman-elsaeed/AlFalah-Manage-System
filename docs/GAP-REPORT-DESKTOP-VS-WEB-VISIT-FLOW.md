# GAP REPORT — Desktop vs Web Visit Flow

**Date:** 2026-07-13
**Scope:** Read-only audit. No code changed. No web feature removed. No new phase started.

**Sources read in full (this turn):**

- Desktop: `classroom-visits/src/main/database/db.ts`, `src/renderer/src/analysis.ts`, `pages/NewVisitPage.tsx`, `pages/ObservationForm.tsx`, `pages/ReportPage.tsx`, `pages/VisitDetails.tsx`, `pages/PlanPage.tsx`, `PROJECT_MAP.md`, `SYSTEM_CONFIG.md`.
- Web: `docs/10-IMPROVEMENT-PLANS-AND-FOLLOWUPS.md`, `frontend/src/app/features/visits/visit-form/visit-form.component.html`, `frontend/src/app/features/visits/visit-detail/visit-detail.component.html`, `frontend/src/app/core/models/visit.models.ts`.

---

## CRITICAL FINDING — Premise correction required before any build

The user's original prompt assumes the desktop has a **per-standard "كتاب المعايير" mapping** ("for a weak standard/indicator, the desktop shows an automatic improvement plan drawn from a per-standard mapping — extract that mapping VERBATIM").

**The desktop source does NOT contain any such per-standard treatment/plan/improvement-action table or mapping.** A full-repo search (analysis.ts, db.ts, all pages, all components, the built bundle `out/renderer/assets/index-*.js`) for terms `كتاب المعايير`, `كتاب معايير`, `recommended_treatment`, `treatment_plan`, `perStandard`, `standard_treatment` returned no matches. The only hit for `priorityStandard` is a **list** of standards with score ≤ 1.5 (no treatment text). The only auto-suggestion source in the desktop is the per-domain template bank in `analysis.ts → generatePlanSuggestions()` (which `docs/10` already mirrors verbatim).

**The "per-standard كتاب المعايير" does not exist in the desktop repo provided.** Two possibilities:

1. It lives in a **separate/older repo** not provided here. Command 7 only seeded the 25 standards' texts, not per-standard treatments. The web's `docs/10` confirms this hypothesis (it's per-domain, not per-standard).
2. It exists only in Ministry/printed material the user remembers, not in this code.

**Recommendation:** Confirm with the user before any "verbatim per-standard book" work. Below I have captured the desktop's **per-domain** suggestion templates (which are real and verbatim). If a per-standard book does exist elsewhere, the user must supply its location/text — I will not invent it.

---

## TABLE 1 — Visit-form fields

| Desktop label (verbatim)     | Type                                        | Req?                                | Desktop placeholder / default | Web status        | Web label (translated, from `VISITS.*` keys)                              | Web file                                                                                                                                       |
| ---------------------------- | ------------------------------------------- | ----------------------------------- | ----------------------------- | ----------------- | ------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------- |
| المعلم \*                    | select                                      | required                            | "اختر المعلم..."              | EXISTS            | `VISITS.INSTRUCTOR`                                                       | `visit-form.component.html:59-67`                                                                                                              |
| تاريخ الزيارة \*             | date                                        | required                            | today                         | EXISTS            | `VISITS.VISIT_DATE`                                                       | `visit-form.component.html:81-85`                                                                                                              |
| نوع الزيارة \*               | select (4 options: أولى/ثانية/ثالثة/متابعة) | required                            | "اختر نوع الزيارة"            | EXISTS (extended) | `VISITS.VISIT_SEQUENCE` (1=أولى, 2=ثانية, 3=ثالثة, 4=متابعة)              | `visit-form.component.html:76-79`, `visit.models.ts:188-193`                                                                                   |
| — (no equivalent in desktop) | —                                           | —                                   | —                             | **NEW in web**    | `VISITS.VISIT_CATEGORY` (9 values: استطلاعية/تبادلية/تثبيت/متابعة/طارئة…) | `visit-form.component.html:70-73`, `visit.models.ts:176-186`                                                                                   |
| المادة الدراسية \*           | input                                       | required (auto-filled from teacher) | "مثال: الرياضيات"             | EXISTS            | `VISITS.SUBJECT`                                                          | `visit-form.component.html:87-91`                                                                                                              |
| الصف الدراسي \*              | input                                       | required (auto-filled from teacher) | "مثال: الصف الأول"            | EXISTS            | `VISITS.GRADE_CLASS`                                                      | `visit-form.component.html:93-97`                                                                                                              |
| عنوان الدرس \*               | input                                       | required                            | "عنوان الدرس أو الوحدة"       | **MISSING**       | —                                                                         | Not in `visit-form.component.html`, not in `VisitDetail.notes`/`subject`/`gradeClass`, not in `CreateVisitRequest` (`visit.models.ts:119-128`) |
| عدد الحاضرين \*              | number                                      | required                            | "0"                           | **MISSING**       | —                                                                         | Not in form, not in `CreateVisitRequest`, not in `VisitDetail`                                                                                 |
| عدد الغائبين                 | number                                      | optional                            | "0"                           | **MISSING**       | —                                                                         | Not in form, not in `CreateVisitRequest`, not in `VisitDetail`                                                                                 |
| ملاحظات المشرف العامة        | textarea                                    | optional, 3 rows                    | "ملاحظات عامة حول الزيارة..." | EXISTS            | `VISITS.NOTES`                                                            | `visit-form.component.html:99-103`, `VisitDetail.notes` at `:155-158`                                                                          |

**Web-only fields (KEEP):** `VISIT_CATEGORY` (9-value enum), Phase 5 rejection/reopen reason banners, rubric version snapshot hint, school context, `createdByFullName`, `submittedAt`, `approvedAt`/`By`, `reopenedAt`/`By`.

---

## TABLE 2 — Buttons & Flow

| Desktop step / button label (verbatim)                                                        | Action                                                                                         | Web equivalent                                                                                                                 | Difference                                                                                                                                                                                                              |
| --------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Header: "📋 إنشاء زيارة صفية جديدة"                                                           | Page title                                                                                     | `VISITS.NEW` / `VISITS.EDIT_DRAFT` (translated)                                                                                | Title structure preserved; web distinguishes new vs edit-draft (Phase 5 addition — KEEP)                                                                                                                                |
| "← رجوع" (in header)                                                                          | navigate(-1)                                                                                   | `COMMON.BACK` ("رجوع")                                                                                                         | Equivalent                                                                                                                                                                                                              |
| "إنشاء الزيارة والانتقال إلى بطاقة الملاحظة ←" (primary)                                      | save visit → navigate to `/visits/:id/observation`                                             | `VISITS.SAVE_DRAFT` (saves draft) + `VISITS.FINISH_AND_GENERATE` (saves + submits for approval)                                | **DIFFERENT** — desktop: create-and-jump-to-scoring on one page; web: scoring is on the SAME form (combined create+score page), and "finish" submits for manager approval rather than auto-generating the report inline |
| "إلغاء"                                                                                       | navigate(-1)                                                                                   | `COMMON.CANCEL` ("إلغاء") in dialogs                                                                                           | Equivalent                                                                                                                                                                                                              |
| —                                                                                             | —                                                                                              | `VISITS.APPROVE` / `VISITS.REJECT` / `VISITS.REOPEN` (Phase 5)                                                                 | **NEW in web** — approval workflow (KEEP)                                                                                                                                                                               |
| **ObservationForm** header: "📝 بطاقة الملاحظة الصفية"                                        | Page title                                                                                     | Inline section title `VISITS.SCORES_TITLE` ("بطاقة تقييم المعايير") within the same form                                       | **DIFFERENT** — desktop separates "إنشاء الزيارة" page and "بطاقة الملاحظة" page; web consolidates both into one page                                                                                                   |
| Desktop Obs-form header: "حفظ مسودة"                                                          | save scores as draft, navigate to `/visits/:id` (details)                                      | `VISITS.SAVE_DRAFT` (sticky footer, always visible)                                                                            | Equivalent intent                                                                                                                                                                                                       |
| Desktop Obs-form header & footer: "إنهاء وتوليد التقرير ←" / "إنهاء التقييم وتوليد التقرير ←" | finalize (must score ALL 25 standards), set status=completed, navigate to `/visits/:id/report` | `VISITS.FINISH_AND_GENERATE` — disabled until `allScored() && isEdit()`; submits for approval instead of generating the report | **DIFFERENT** — web does not auto-generate the printable report on submit; it sends to approval queue                                                                                                                   |
| Desktop: scale legend row "مفتاح الدرجات:" with 0..4 buttons + Arabic labels                  | score buttons always visible                                                                   | Tooltip on hover only (`[pTooltip]="scoreLabels[j]"`)                                                                          | **DIFFERENT** — desktop shows persistent legend; web uses tooltips. Less discoverable.                                                                                                                                  |
| Desktop Obs-form footer: same two buttons repeated                                            | duplicate CTAs                                                                                 | Single sticky actions bar with progress + both buttons                                                                         | Cleaner UX on web (KEEP)                                                                                                                                                                                                |
| Desktop: "📄 التقرير" (VisitDetails, when completed) → `/visits/:id/report`                   | open report                                                                                    | `VISITS.DOWNLOAD_PDF` button (Phase 6)                                                                                         | **DIFFERENT** — desktop: in-app HTML preview + `window.print()`; web: PDF download endpoint                                                                                                                             |
| Desktop: "📌 خطة التحسين" (VisitDetails) → `/visits/:id/plan`                                 | open plan page                                                                                 | `PLANS.TITLE` (improvement plans) — navigates to plans                                                                         | Equivalent                                                                                                                                                                                                              |
| Desktop PlanPage: "＋ إضافة خطة"                                                              | open add modal                                                                                 | Plans feature                                                                                                                  | Web Phase 7 mirrors desktop per `docs/10`                                                                                                                                                                               |
| Desktop PlanPage weak-domain chips: domain name + average, click → `openAdd(domainId)`        | prefill from template                                                                          | `docs/10` says same: weak-domain chips prefilling Goal/Actions/SuccessIndicators                                               | Equivalent (Phase 7)                                                                                                                                                                                                    |

---

## TABLE 3 — Standards book (suggestion templates)

### 3a. Desktop's actual source — VERBATIM (per-domain, from `analysis.ts → generatePlanSuggestions`, lines 172-224)

These ARE the desktop's verbatim templates. There is **no per-standard "كتاب المعايير"** in the repo.

**بيئة التعلم**

- Goal: `تحسين جودة بيئة التعلم وجعلها أكثر إثراءً وفاعلية للمتعلمين`
- Actions: `1. مراجعة توزيع المقاعد وترتيب الغرفة الصفية\n2. إضافة مصادر تعلم متنوعة ومناسبة\n3. تعزيز جانب القيم والهوية الوطنية في الديكور التعليمي\n4. تطبيق استراتيجيات إدارة الوقت الصفي`
- SuccessIndicators: `ارتفاع متوسط درجات نطاق بيئة التعلم إلى 3.0 أو أعلى في الزيارة القادمة`

**التدريس والتعلم**

- Goal: `تطوير استراتيجيات التدريس وتنويعها لتحقيق نواتج التعلم المستهدفة`
- Actions: `1. حضور دورة تدريبية في استراتيجيات التدريس الحديثة\n2. تطبيق التعلم التعاوني في الحصص\n3. استخدام التقنية الرقمية في شرح المفاهيم\n4. ربط المحتوى بحياة الطلاب اليومية`
- SuccessIndicators: `تنفيذ 3 استراتيجيات تدريس مختلفة خلال شهر والحصول على تغذية راجعة إيجابية من المشرف`

**تنمية المهارات**

- Goal: `تعزيز تنمية مهارات التفكير العليا والمهارات الحياتية لدى المتعلمين`
- Actions: `1. تصميم أنشطة تعلم تستهدف مهارات التفكير الناقد\n2. إدراج مشاريع بحثية صغيرة ضمن الخطة الدراسية\n3. تشجيع المتعلمين على طرح الأسئلة والتساؤل\n4. دمج التعلم الذاتي في الأنشطة اليومية`
- SuccessIndicators: `لاحظ المشرف زيادة ملموسة في مشاركة الطلاب وأسئلتهم التحليلية خلال الزيارة القادمة`

**التقويم**

- Goal: `تنويع أساليب التقويم وتفعيل التغذية الراجعة البنائية`
- Actions: `1. إعداد خطة تقويم تشمل التشخيصي والبنائي والختامي\n2. استخدام بطاقات الخروج والاستبانات القصيرة\n3. تقديم تغذية راجعة فورية وبنائية لكل طالب\n4. توثيق نتائج التقويم وتحليلها`
- SuccessIndicators: `تطبيق ثلاثة أدوات تقويم مختلفة في كل وحدة دراسية وتوثيق نتائجها`

**سلوك المتعلمين**

- Goal: `تعزيز الانضباط الإيجابي وتنمية الاستقلالية والمسؤولية لدى المتعلمين`
- Actions: `1. وضع قواعد صفية واضحة بمشاركة الطلاب\n2. تطبيق نظام تحفيز إيجابي ومتنوع\n3. تعزيز مفهوم التعلم الذاتي والمسؤولية\n4. إجراء أنشطة تعزز الهوية الوطنية والانتماء`
- SuccessIndicators: `انخفاض ملحوظ في سلوكيات الإزعاج وزيادة مشاركة الطلاب الطوعية في الأنشطة`

**Fallback** (for unknown domain name):

- Goal: `تحسين الأداء في مجال [DomainName]`
- Actions: `1. تحديد نقاط الضعف المحددة\n2. وضع خطة عمل واضحة\n3. الالتزام بالتطبيق والمتابعة`
- SuccessIndicators: `ارتفاع متوسط درجات نطاق [DomainName] في الزيارة القادمة`

### 3b. Auto-suggest threshold logic (desktop, `analysis.ts`)

```
THRESHOLD_WEAK = 2.5      // domain average < 2.5 → weak domain → suggest plan
THRESHOLD_PRIORITY = 1.5  // standard score <= 1.5 → priority standard (list only, no template)
```

- Weak domain → suggestion chip appears on PlanPage with pre-filled Goal/Actions/SuccessIndicators from `generatePlanSuggestions(domain.name)`; user must **manually save**.
- Priority standard → listed as a table (standard text, score, level) in `VisitDetails.tsx` and `ReportPage.tsx`; **no per-standard treatment text** is suggested. Only a flag for the supervisor.
- No score threshold varies by domain or by standard; thresholds are global constants.

### 3c. Web status of TABLE 3

| Item                                        | Desktop                              | Web                                                                             | Gap                                       |
| ------------------------------------------- | ------------------------------------ | ------------------------------------------------------------------------------- | ----------------------------------------- |
| Per-domain templates (5 + fallback)         | EXISTS in `analysis.ts`              | PRESERVED verbatim in `docs/10-IMPROVEMENT-PLANS-AND-FOLLOWUPS.md` lines 65-128 | None — web docs mirror desktop            |
| Auto-suggest chip on weak domain            | EXISTS (`PlanPage.tsx:189-211`)      | SPEC'd for Phase 7 (`docs/10`:60-61)                                            | Not implemented yet — but spec is correct |
| Threshold < 2.5 for weak domain             | EXISTS                               | SPEC'd (`docs/10`:21)                                                           | None                                      |
| Threshold ≤ 1.5 for priority standard       | EXISTS                               | SPEC'd (`docs/10`:22)                                                           | None                                      |
| Per-standard "كتاب المعايير" treatment text | **DOES NOT EXIST in desktop source** | Not present                                                                     | **N/A — premise correction above**        |
| Per-standard auto-suggest when score ≤ 1.5  | Not in desktop                       | Not in web spec                                                                 | N/A unless user supplies source           |

---

## TABLE 4 — Report / Output content

| Desktop ReportPage section (Arabic label, verbatim)                                                                                                                                            | Web equivalent                                                                                                                                                    | Difference                                                                                                                                                                                                                                                                         |
| ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Header: "المملكة العربية السعودية — وزارة التعليم" + "تقرير الزيارة الصفية" + "وفق معايير الرخصة المهنية للمعلم السعودي" + "#رقم الزيارة — زيارة [نوع] \| تاريخ الإصدار" + school logo (80×80) | Phase 6 PDF endpoint (not read in this audit)                                                                                                                     | Web Phase 6 produces PDF; desktop produces HTML + `window.print()`. Watermark & logo exist on both.                                                                                                                                                                                |
| "بيانات المعلم" table: الاسم، الرقم الوظيفي، المدرسة، المادة، المرحلة                                                                                                                          | Exists on `visit-detail.component.html:101-160` as a meta grid                                                                                                    | Web adds: `createdByFullName`, `submittedAt`, `approvedAt`/`By`, `reopenedAt`/`By`, `rubricVersionNumber` (Phase 5/6 fields). **KEEP web extras.**                                                                                                                                 |
| "بيانات الزيارة" table: تاريخ الزيارة، نوع الزيارة، الصف، عنوان الدرس، الحضور/الغياب                                                                                                           | Exists in detail view; **MISSING fields:** عنوان الدرس, الحضور/الغياب are not stored on web visit                                                                 | See Table 1 gap                                                                                                                                                                                                                                                                    |
| Overall score circle (70×70 colored circle, "X.X / من 4" + level badge + "الدرجة الكلية للزيارة الصفية")                                                                                       | `VISITS.OVERALL_SCORE` + `PERFORMANCE_LEVEL` in `visit-detail.component.html:240-249`                                                                             | Equivalent (web has tag instead of full circle; minor visual)                                                                                                                                                                                                                      |
| "أولاً: نتائج النطاقات" table (النطاق، المتوسط، المستوى، النسبة)                                                                                                                               | `VISITS.DOMAIN_AVERAGES` averages grid at `:251-263`                                                                                                              | Equivalent (web uses cards; desktop uses table in print report)                                                                                                                                                                                                                    |
| "ثالثاً: نقاط القوة" (list "Domain (avg — level)", green color)                                                                                                                                | `VISITS.STRENGTHS` bullet list `:265-278`                                                                                                                         | Equivalent                                                                                                                                                                                                                                                                         |
| "رابعاً: مجالات التحسين والتوصيات" (list, amber color)                                                                                                                                         | `VISITS.IMPROVEMENT_AREAS` bullet list `:280-293`                                                                                                                 | Equivalent                                                                                                                                                                                                                                                                         |
| "خامساً: التوصيات" (blue list — generated from `generatePlanSuggestions` per weak domain, first action item)                                                                                   | No per-weak-domain "recommendations" string in web `VisitAnalysis` (`visit.models.ts:68-78` has only strengths/improvementAreas/priorityStandards/domainAverages) | **PARTIAL** — web has strengths + improvement areas + priority standards; the desktop's "recommendations" string is folded into `improvementAreas` text. Per `docs/06` the web surfaces domain chip → auto-prefills plan via Phase 7. Plan text for report would need to be added. |
| "🔴 المعايير ذات الأولوية للتحسين" table                                                                                                                                                       | `VISITS.PRIORITY_STANDARDS` bullet list `:295-308`                                                                                                                | Equivalent                                                                                                                                                                                                                                                                         |
| "سادساً: ملاحظات المشرف" (only if present)                                                                                                                                                     | `VISITS.NOTES` at `:155-158`                                                                                                                                      | Equivalent                                                                                                                                                                                                                                                                         |
| Signatures: "المشرف التربوي" / "المعلم / المعلمة" each with "التوقيع" line                                                                                                                     | Not visible in web detail HTML                                                                                                                                    | Web's PDF report (Phase 6) likely has signatures — **verify in `AlFalah.Application` PDF template**                                                                                                                                                                                |
| Recommendation chips in visit-details view ("✅ نقاط القوة", "📈 مجالات التحسين", "💡 التوصيات المقترحة", "🔴 المعايير ذات الأولوية")                                                          | Same sections present                                                                                                                                             | Equivalent                                                                                                                                                                                                                                                                         |

---

## Prioritized "to re-add" list (core-flow parity, desktop → web)

### P0 — pure data loss, fix first (no impact on web enhancements)

1. **Add `lessonTitle` (عنوان الدرس / عنوان الدرس أو الوحدة) to Visit** — DB column + DTO + form field (full width) + display in detail/PDF. Desktop field was required.
2. **Add `presentCount` (عدد الحاضرين) to Visit** — DB column + DTO + form field (number, required, placeholder "0") + display. Desktop field was required.
3. **Add `absentCount` (عدد الغائبين) to Visit** — DB column + DTO + form field (number, optional, placeholder "0") + display. Desktop field was optional.
4. **Restore the persistent score-key legend** — desktop Obs-form shows "مفتاح الدرجات: 0 غير مشاهد / 1 يحتاج تحسين / 2 متحقق جزئياً / 3 متحقق بدرجة جيدة / 4 متميز" inline above the standards. Web currently hides labels behind tooltips (`scoreLabels[j]` only shown on hover at `visit-form.component.html:151`). Re-add as a small always-visible legend strip OR keep tooltips but add `aria-label` + persistent label of the **current best** ("متميز" already exists at `:157` — extend to all 5 or to a legend).

### P1 — flow parity (desktop behavior the web lost in redesign)

5. **Restore "إنهاء التقييم وتوليد التقرير" semantics.** Desktop: finalize = set status=completed + auto-navigate to printable report. Web: finalize = submit-for-approval queue (no inline report). Two options:
   - (a) Add a "معاينة التقرير" preview (HTML, desktop-style) accessible to managers even pre-approval.
   - (b) Document this is intentional Phase-5 redesign (approval gating is a web enhancement, KEEP).
   - **Decision needed.**
6. **Restore per-visit domain progress counter on the scoring card.** Desktop `ObservationForm.tsx:199-201` shows `{domainScored} / {domain.standards.length}` per domain header. Web only shows a single overall counter `scoredCount() / totalCount()` in the sticky bar (`visit-form.component.html:185`). Add per-domain `x/n` counter on each `.domain-header`.

### P2 — about the "per-standard كتاب المعايير" — BLOCKED, see CRITICAL FINDING

7. Do **NOT** build any per-standard treatment mapping until user confirms where the source text lives. The desktop does not contain it. If the user has a separate Ministry document, it must be supplied.

### P3 — nice-to-have, low risk

8. Add the per-visit "خامساً: التوصيات" recommendation string (desktop surfaces the first auto-suggest action per weak domain). Currently web has strengths/improvementAreas/priorityStandards but no synthesized "recommendations" string. Add to `VisitAnalysis` DTO; populate from existing `docs/10` templates.
9. Verify the Phase 6 PDF report still renders signatures row "المشرف التربوي / المعلم / المعلمة" (desktop had it).

## "Keep as-is" — web enhancements we do NOT touch

- Phase 5 approval workflow (approve/reject/reopen) with reasons, banners, role-aware visibility.
- Phase 8 instructor complaint / review request.
- 9-value `VISIT_CATEGORY` enum (web enhancement over desktop's 4-value `visit_type`).
- Per-school context (`schoolId`, `schoolName`).
- Multi-role permissions, instructor dashboard.
- Audit fields (`createdBy/At`, `approvedBy/At`, `reopenedBy/At`).
- Rubric version snapshot (`rubricVersionId`/`rubricVersionNumber`) — desktop had a single frozen 5/25 rubric.
- Soft-delete preference for plans (`docs/10`:17-18).
- The combined create+score page layout (vs desktop's two pages) — this is a UX upgrade.
- Angular PrimeNG component library, RTL, accessibility attributes.

---

## Proposed (NOT applied) data-model additions

For the user's review/approval only. Do not implement until approved.

### Visits table — add 3 columns

```
ALTER TABLE Visits
  ADD LessonTitle      NVARCHAR(300) NULL,    -- corresponds to desktop lesson_title
  ADD PresentCount     INT NOT NULL DEFAULT 0, -- corresponds to desktop attendance_count
  ADD AbsentCount      INT NOT NULL DEFAULT 0; -- corresponds to desktop absence_count
```

- `LessonTitle`: required going forward (Phase 7 retro fill: keep nullable for existing rows to avoid blocking).
- `PresentCount`: required (default 0 for existing rows).
- `AbsentCount`: optional (default 0).
- Affects: `VisitDetail`, `CreateVisitRequest`, `UpdateVisitRequest`, `InstructorReport` DTOs, `VisitFormComponent` template + FormGroup, all PDF report templates (header table + visit-info table), `visit-detail.component.html` meta grid.

### Standards / Rubric — NO change proposed for now

- The desktop has no per-standard treatment text in code or DB. Do not invent one.
- If user supplies a per-standard "كتاب المعايير" source (printed Ministry doc / external repo), propose a new table:

```
CREATE TABLE StandardTreatmentTemplates (
  RubricStandardId INT NOT NULL REFERENCES RubricStandards(Id),
  ThresholdScore   DECIMAL(3,1) NOT NULL DEFAULT 1.5,  -- when score <= this, suggest
  Goal             NVARCHAR(MAX) NOT NULL,
  Actions          NVARCHAR(MAX) NOT NULL,
  SuccessIndicators NVARCHAR(MAX) NOT NULL,
  IsActive         BIT NOT NULL DEFAULT 1,
  CONSTRAINT PK_StandardTreatmentTemplates PRIMARY KEY (RubricStandardId)
);
```

- This is **proposal only — do not create migration until the source text is provided and approved.**

### No other schema changes required for parity

The web already has equivalents for: `visit_type` (as `visit_sequence`), `visit_date`, `subject`, `grade` (`grade_class`), `supervisor_notes` (`notes`), `scores` (25-standard scoring card), `improvement_plans`/`follow_ups` (Phase 7 per `docs/10`).

---

## Summary of PART B status flags

| Desktop element                                              | Status                        | Notes                                                                                                                         |
| ------------------------------------------------------------ | ----------------------------- | ----------------------------------------------------------------------------------------------------------------------------- |
| المعلم                                                       | EXISTS                        | with extra school context                                                                                                     |
| تاريخ الزيارة                                                | EXISTS                        | —                                                                                                                             |
| نوع الزيارة                                                  | EXISTS                        | as `visit_sequence` (4 values); web adds `visit_category` (9 values, KEEP)                                                    |
| المادة الدراسية                                              | EXISTS                        | —                                                                                                                             |
| الصف الدراسي                                                 | EXISTS                        | —                                                                                                                             |
| **عنوان الدرس/الوحدة**                                       | **MISSING**                   | Add as P0                                                                                                                     |
| **عدد الحاضرين**                                             | **MISSING**                   | Add as P0                                                                                                                     |
| **عدد الغائبين**                                             | **MISSING**                   | Add as P0                                                                                                                     |
| ملاحظات المشرف العامة                                        | EXISTS                        | as `notes`                                                                                                                    |
| "إنشاء الزيارة والانتقال إلى بطاقة الملاحظة ←"               | PARTIAL                       | web split into save-draft + finish-and-generate; combined page                                                                |
| "حفظ مسودة"                                                  | EXISTS                        | sticky bar                                                                                                                    |
| "إنهاء وتوليد التقرير ←"                                     | PARTIAL                       | submits-for-approval, no auto-report preview                                                                                  |
| "إنهاء التقييم وتوليد التقرير ←"                             | PARTIAL                       | same as above                                                                                                                 |
| "إلغاء" / "← رجوع"                                           | EXISTS                        | —                                                                                                                             |
| Scale legend (0..4 with Arabic labels)                       | **PARTIAL/MISSING**           | only best-label visible; tooltips only; add persistent legend                                                                 |
| Per-domain progress `n/N`                                    | **MISSING**                   | only overall counter exists                                                                                                   |
| Evidence field per standard ("الشواهد والملاحظات (اختياري)") | EXISTS                        | web hides input behind note-toggle (icon-only); desktop shows inline input. UX downgrade but not data-loss — flag for review. |
| Per-domain suggestion templates                              | EXISTS in spec (docs/10)      | Phase 7 implementation pending                                                                                                |
| Per-standard "كتاب المعايير" treatment                       | **DOES NOT EXIST in desktop** | Premise correction required                                                                                                   |
| Priority standards list                                      | EXISTS                        | —                                                                                                                             |
| Report sections (6 sections + signatures)                    | PARTIAL                       | missing عنوان الدرس / حضور-غياب in print, possibly signatures row in PDF                                                      |

---

**STOP — awaiting your review.** No implementation performed. Decide which P0/P1 items to approve next, and confirm whether a per-standard "كتاب المعايير" source exists outside the provided desktop repo (and where).
