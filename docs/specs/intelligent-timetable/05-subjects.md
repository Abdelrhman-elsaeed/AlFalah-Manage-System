# Phase 05 — Subjects, Class Quotas, and Scheduling Constraints

**Module:** Intelligent Timetable (الجدول الذكي)  
**Status:** Requirements draft — no implementation  
**Primary actor:** Secretary / authorized timetable editor

## Overview

This phase connects the secretary-managed subject catalog to classrooms and defines how many weekly lessons each class receives, how those lessons are grouped, where they may occur, and any day/time preferences.

The revised requirement supersedes the reference product's free-text “add subject” behavior: the user selects an existing subject from a dropdown and configures it. The same subject may have different quotas and constraints for different classes or stages.

## UI/UX Requirements

### Subject overview

- Show summary cards for total subjects, classes, early-timing rules, single/non-consecutive groups, and paired/consecutive groups.
- Provide a searchable, filterable table with:
  - selection checkbox;
  - subject name;
  - status badges (for example configured/not configured, early preference, fixed rule);
  - number of linked classes;
  - edit/remove actions.
- Provide filters by configuration state and relevant subject attributes.
- A remove action distinguishes “remove this subject from selected classes” from deleting the secretary's master subject.

### Configure subject flow

- Open a modal or full-page form titled **إضافة/تخصيص مادة**.
- Replace manual subject-name entry with a required searchable dropdown of active secretary-created subjects.
- Provide class selection with **تحديد الكل**, stage/grade filtering, and multi-select. Show selected-class count.
- Required quota fields:
  - **الحصص الفردية للفصل** — individual periods distributed across days;
  - **الحصص الزوجية للفصل** — number of two-period consecutive blocks, not number of physical periods.
- Display the computed weekly total prominently:  
  `total periods = individual periods + (paired blocks × 2)`.
- Provide **تفضيل الحصة المبكرة**. When enabled, display a permitted early-period window such as “from period 1 through period 3.”
- Provide **الأيام المسموحة** as day chips. Leaving the selection empty means all study days.
- Support fixed day/period constraints where shown by the reference UI (**تثبيت مادة في الجدول**), with a clear distinction between a hard fixed slot and an early-time preference.
- Provide optional location selection for subjects that must run in a specific room/lab/gym. Allow a default location and, if required, class-specific overrides.
- On save, report which classes were created, updated, or skipped due to validation.

### Editing different class quotas

- The same subject can be selected again for a different set of classes with different quotas.
- Editing from a subject row shows per-class configurations, making differences visible rather than collapsing them into one misleading value.
- If selected classes already have settings, the UI must explicitly offer update/replace; it must not create duplicate class-subject requirements.

## Backend & Data Models

### Proposed normalized entities

`Subject`

- `Id`, `SchoolId`, `Code`, Arabic name, optional English name, active status, audit/soft-delete fields.
- Owned by the secretary's school master data and reused across timetable setups.

`ClassSubjectRequirement`

- `Id`, `TimetableSetupProfileId`, `ClassroomId`, `SubjectId`.
- `IndividualPeriodCount`.
- `PairedBlockCount`.
- Derived `TotalWeeklyPeriods` (prefer calculation over independent mutable storage).
- `PreferEarlyPeriods`, nullable `EarliestPeriodSequence`, nullable `LatestPreferredPeriodSequence`.
- Optional `DefaultLocationId`.
- Audit fields and concurrency token.
- Unique constraint on `(TimetableSetupProfileId, ClassroomId, SubjectId)`.

`ClassSubjectAllowedDay`

- `ClassSubjectRequirementId`, `DayOfWeek`.
- Zero rows means all study days; API contracts should expose that meaning explicitly.

`ClassSubjectFixedSlot`

- `ClassSubjectRequirementId`, effective day/period identity, and hard/soft rule type if both are supported.

`ClassSubjectLocationRule`

- Links the requirement to one or more allowed `SchoolLocation` records and identifies a preferred/default location when applicable.

### Existing model impact

- `SchoolTimetableEntry.Subject` is currently free text. The target model needs `SubjectId` for integrity and `ClassSubjectRequirementId` or an equivalent source link. A denormalized subject display name may remain in immutable snapshots for historical rendering.
- `SchoolTimetableEntry.ClassroomId` should be required for lesson entries generated from a class-subject requirement.
- Location/resource identity must be persisted on the entry when a lesson is placed outside the classroom's default room.

### Validation

- Subject, classroom, setup, location, and allowed days must belong to the same school/scope.
- Counts are non-negative integers and the computed total must be greater than zero for an active requirement.
- A paired block consumes two adjacent lesson periods on one permitted day; the two periods cannot be separated by a break.
- Preferred/fixed period sequences must exist on each applicable day's effective bell schedule.
- Allowed days must be a subset of study days and sufficient to host the requested distribution.
- A required location must be active and available to the school.
- Removing a requirement used by a generated/published timetable must be blocked or versioned; no historical entry may be orphaned silently.

## Business Rules

1. The secretary creates the subject master list; timetable configuration selects from that list instead of creating ad hoc text subjects.
2. A subject can apply to all classrooms or a selected subset.
3. The same subject can carry different quotas and constraints for different classrooms/stages.
4. An individual-period count represents separate periods intended to be distributed across study days.
5. A paired-block count represents pairs: one paired block equals two adjacent timetable periods. For example, 4 individual periods plus 1 paired block equals 6 weekly periods.
6. Paired periods must be consecutive, taught to the same class by the assigned teacher(s), and use a compatible location.
7. Empty allowed-day selection means every study day; explicit selection prohibits placement on all unselected days.
8. An early-period preference directs generation toward the configured window but is a soft constraint unless explicitly promoted to a hard rule.
9. A fixed slot is a hard constraint and must be checked for class, teacher, and location conflicts before generation.
10. If a subject requires a laboratory or other specific place, every generated/manual occurrence must use an allowed location and participate in location-conflict validation.
11. Removing a subject from one classroom does not remove it from other classrooms or delete the master subject.
12. Requirement changes after generation mark the timetable stale and require review/regeneration or a controlled manual reconciliation.

## ❓ Pending Questions for the User

1. Is an “early lesson” merely a soft preference, or must the generator fail when it cannot keep every occurrence inside the selected early-period window?
2. May a paired block span a break, or must its two periods be directly adjacent in real time as well as consecutive by sequence?
3. Can one subject requirement allow several interchangeable rooms, and does each room have a capacity or equipment type that the generator must consider?
4. When bulk-configuring classes that already have this subject, should save replace all existing rules, merge only changed fields, or ask per class?
