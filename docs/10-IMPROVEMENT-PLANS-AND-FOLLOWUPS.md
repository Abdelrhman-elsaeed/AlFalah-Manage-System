# 10 — Improvement Plans & Follow-ups

**Status:** Reference (not implemented in Phase 1) · **Last updated:** 2026-07-10

> This module must follow the **old desktop system logic EXACTLY** when implemented later.
> **Do not add new restrictions unless explicitly requested.**

## Old behavior to preserve
- Plans are created **manually**.
- Suggestions are generated **automatically** based on weak domains.
- **Multiple plans per visit/domain are allowed.**
- Plan status values: `active`, `completed`, `cancelled`.
- Default plan status on create is **active**.
- Plans can be **edited regardless of status**.
- Follow-ups can be added **regardless of plan status**.
- Follow-ups can be **edited/deleted**.
- Plan deletion removed follow-ups in the old system, but in the new web app
  **prefer soft delete** for audit unless explicitly hard delete is requested.

## Definitions
- **Weak domain:** domain average **< 2.5**.
- **Priority standard:** score **<= 1.5**.

## Plan fields
- Id
- SchoolId
- TeacherId / InstructorId
- VisitId
- DomainId (nullable)
- Goal (required)
- Actions (required)
- StartDate (required)
- EndDate (required)
- SuccessIndicators (required)
- Status: active / completed / cancelled
- CreatedAt
- CreatedByUserId
- UpdatedAt
- UpdatedByUserId
- IsDeleted
- DeletedAt
- DeletedByUserId

## Plan validation
- Goal required and non-empty.
- Actions required and non-empty.
- StartDate required.
- EndDate required.
- SuccessIndicators required and non-empty.
- Keep old behavior: **no strict `EndDate >= StartDate` enforcement** (non-blocking warning only).
- Keep old behavior: **no uniqueness** on `VisitId + DomainId`.

## Default dates
- StartDate = **today**.
- EndDate = **today + 2 months**.

## Suggestion behavior
- On the Improvement Plan page, after visit analysis, show **weak domains**.
- User can click a weak-domain chip.
- System fills: Goal, Actions, SuccessIndicators, DomainId, StartDate (today), EndDate (today + 2 months).
- User **manually saves**.

## Suggestion templates (EXACT Arabic — reproduce verbatim)

### Domain: بيئة التعلم
**Goal:**
تحسين جودة بيئة التعلم وجعلها أكثر إثراءً وفاعلية للمتعلمين
**Actions:**
- مراجعة توزيع المقاعد وترتيب الغرفة الصفية
- إضافة مصادر تعلم متنوعة ومناسبة
- تعزيز جانب القيم والهوية الوطنية في الديكور التعليمي
- تطبيق استراتيجيات إدارة الوقت الصفي
**SuccessIndicators:**
ارتفاع متوسط درجات نطاق بيئة التعلم إلى 3.0 أو أعلى في الزيارة القادمة

### Domain: التدريس والتعلم
**Goal:**
تطوير استراتيجيات التدريس وتنويعها لتحقيق نواتج التعلم المستهدفة
**Actions:**
- حضور دورة تدريبية في استراتيجيات التدريس الحديثة
- تطبيق التعلم التعاوني في الحصص
- استخدام التقنية الرقمية في شرح المفاهيم
- ربط المحتوى بحياة الطلاب اليومية
**SuccessIndicators:**
تنفيذ 3 استراتيجيات تدريس مختلفة خلال شهر والحصول على تغذية راجعة إيجابية من المشرف

### Domain: تنمية المهارات
**Goal:**
تعزيز تنمية مهارات التفكير العليا والمهارات الحياتية لدى المتعلمين
**Actions:**
- تصميم أنشطة تعلم تستهدف مهارات التفكير الناقد
- إدراج مشاريع بحثية صغيرة ضمن الخطة الدراسية
- تشجيع المتعلمين على طرح الأسئلة والتساؤل
- دمج التعلم الذاتي في الأنشطة اليومية
**SuccessIndicators:**
لاحظ المشرف زيادة ملموسة في مشاركة الطلاب وأسئلتهم التحليلية خلال الزيارة القادمة

### Domain: التقويم
**Goal:**
تنويع أساليب التقويم وتفعيل التغذية الراجعة البنائية
**Actions:**
- إعداد خطة تقويم تشمل التشخيصي والبنائي والختامي
- استخدام بطاقات الخروج والاستبانات القصيرة
- تقديم تغذية راجعة فورية وبنائية لكل طالب
- توثيق نتائج التقويم وتحليلها
**SuccessIndicators:**
تطبيق ثلاثة أدوات تقويم مختلفة في كل وحدة دراسية وتوثيق نتائجها

### Domain: سلوك المتعلمين
**Goal:**
تعزيز الانضباط الإيجابي وتنمية الاستقلالية والمسؤولية لدى المتعلمين
**Actions:**
- وضع قواعد صفية واضحة بمشاركة الطلاب
- تطبيق نظام تحفيز إيجابي ومتنوع
- تعزيز مفهوم التعلم الذاتي والمسؤولية
- إجراء أنشطة تعزز الهوية الوطنية والانتماء
**SuccessIndicators:**
انخفاض ملحوظ في سلوكيات الإزعاج وزيادة مشاركة الطلاب الطوعية في الأنشطة

### Fallback template
**Goal:**
تحسين الأداء في مجال [DomainName]
**Actions:**
- تحديد نقاط الضعف المحددة
- وضع خطة عمل واضحة
- الالتزام بالتطبيق والمتابعة
**SuccessIndicators:**
ارتفاع متوسط درجات نطاق [DomainName] في الزيارة القادمة

## Follow-up old logic
- User creates follow-up **manually**.
- Default **FollowDate = today**.
- **ProgressNote required**.
- **EvidenceNote** optional free text.
- **ProgressScore** optional; if present it must be between **0 and 100**.
- Follow-ups ordered by **FollowDate descending**.
- **Latest progress** = first follow-up with non-null ProgressScore in descending FollowDate order.
- **Chart data** = follow-ups reversed to chronological order, only rows with non-null ProgressScore.
- Chart appears only if **at least 2 scored follow-ups** exist.
- **Progress colors:**
  - >= 75 → green / success
  - >= 50 and < 75 → yellow / warning
  - < 50 → red / danger

> Do not implement Improvement Plans or Follow-ups in Phase 1.
