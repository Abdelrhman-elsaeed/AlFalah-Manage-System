# 11 — Constants & Enums

**Status:** Baseline · **Last updated:** 2026-07-10

> Create enums/constants for future use, but do not fully implement workflows yet.
> Roles are **database-driven** — do **not** depend only on a `UserRole` enum for authorization.

## SchoolStage
- Primary
- Intermediate
- Secondary

## VisitStatus
- Draft
- Submitted
- PendingApproval
- Approved
- RejectedForChanges
- Reopened
- UnderReviewAfterComplaint
- Cancelled

## PlanStatus
- active
- completed
- cancelled

## ComplaintStatus
- Open
- InReview
- Resolved
- Rejected
- Closed

## Visit Category (Arabic)
- استطلاعية / توجيهية
- زيارة صفية أو دورية
- زيارة تبادلية
- زيارة التثبيت / الترسيم للمعلمين الجدد
- زيارة المتابعة والدعم
- زيارة مفاجئة / تفتيشية
- زيارة طارئة
- زيارة التحقق / متابعة قانونية
- زيارة اللجان المركزية

## Visit Sequence (Arabic)
- أولى
- ثانية
- ثالثة
- متابعة

## Score labels & performance levels
See [09-RUBRIC-AND-EVALUATION.md](09-RUBRIC-AND-EVALUATION.md) for the exact Arabic
score labels (0–4) and performance-level thresholds.
