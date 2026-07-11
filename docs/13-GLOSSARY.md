# 13 — Glossary (Bilingual)

**Status:** Baseline · **Last updated:** 2026-07-10

| English | Arabic | Notes |
|---------|--------|-------|
| Super Admin / Developer | مدير النظام / المطوّر | Technical owner, whole platform |
| Main Manager | مدير المدارس العام | Global scope, cannot see complaint details |
| School Manager | مدير المدرسة | One school only, exactly one per school |
| Moderator | مشرف | Selected school context; can serve multiple schools |
| Instructor / Teacher | معلم | Own account only |
| School | مدرسة | Identity = Name + Stage + City + LocationDetails |
| Visit | زيارة | Classroom visit / evaluation event |
| Domain | نطاق | One of the 5 rubric domains |
| Standard | معيار | One of the 25 rubric standards |
| Rubric | أداة / بطاقة التقييم | 5 domains × standards, versioned |
| Improvement Plan | خطة تحسين / علاجية | Created for weak domains |
| Follow-up | متابعة | Progress tracking on a plan |
| Complaint / Review Request | شكوى / طلب مراجعة | Submitted by instructor after viewing report |
| Stage | مرحلة | Primary=ابتدائي, Intermediate=متوسط, Secondary=ثانوي |
| Performance Level | مستوى الأداء | Derived from scores (see file 09) |
| ActiveSchoolId | معرّف المدرسة النشطة | JWT claim for school users |
| UserSchoolRole | ربط المستخدم بالمدرسة والدور | Enables per-school role assignment |
