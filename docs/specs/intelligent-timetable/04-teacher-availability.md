# Phase 04 — Teacher Availability and Load Settings

**Module:** Intelligent Timetable (الجدول الذكي)  
**Status:** Requirements draft — no implementation  
**Primary actor:** Secretary / authorized timetable editor

## Overview

This phase records when each teacher is available to teach and the teacher's weekly load limit. Availability is a hard scheduling constraint used by both automatic generation and manual timetable editing: clearing a period means the teacher cannot be assigned in that slot.

The settings are evaluated against the effective per-day bell schedule, so a teacher grid can contain a different number of periods on different study days.

## UI/UX Requirements

### Teacher selection and profile summary

- Provide searchable teacher selection and clearly identify the current teacher being configured.
- Show a header such as **تخصيص المعلم [name]** with an explanation that the page controls teacher settings and available working times.
- Display the mockup-derived profile controls:
  - **اسم الاختصار** for compact timetable/print display;
  - **أقصى عدد للحصص** per week;
  - **معلم منتدب** flag;
  - **إخفاء الطباعة** flag;
  - explicit **حفظ** action.
- Profile flags must include helper text explaining their operational effect; no flag may exist only as an unexplained checkbox.

### Availability grid

- Render an RTL matrix with study days as rows and the day's effective periods as columns.
- Each column header shows period sequence plus exact start/end time from the selected bell-schedule template.
- A checked cell means available; an unchecked cell means unavailable.
- Provide **تحديد الكل** per day and bulk select/clear controls for the whole grid.
- Visually distinguish non-study days and non-existent periods from unavailable periods; they are not editable cells.
- Show a warning when closing many periods may make generation infeasible.
- If the bell schedule has changed since availability was saved, show a remapping/review banner rather than silently guessing the intended cells.
- Preserve unsaved-change feedback and warn before navigating to another teacher.

### Accessibility and responsive behavior

- Keep teacher name and day labels sticky while horizontally scrolling large schedules.
- Availability state must be conveyed by checkbox state and text/accessible labels, not color alone.
- Keyboard users can traverse and toggle cells, with a confirmation shortcut for bulk operations.

## Backend & Data Models

### Proposed entities

`TeacherTimetableProfile`

- `Id`, `SchoolId`, `TimetableSetupProfileId`, `InstructorProfileId`.
- `ShortDisplayName`.
- `MaximumWeeklyPeriods`.
- `IsVisiting`.
- `HideFromPrint`.
- Audit fields and concurrency token.
- Unique constraint on `(TimetableSetupProfileId, InstructorProfileId)`.

`TeacherAvailabilitySlot`

- `Id`, `TeacherTimetableProfileId`.
- `BellScheduleDayId` or `DayOfWeek` plus the bell-schedule template revision.
- `PeriodSequence` or stable `BellPeriodId`.
- `IsAvailable` (or persist only unavailable exceptions if availability defaults to true).
- Optional reason/note for an unavailable slot.

Stable period IDs are preferable to raw sequence numbers so an inserted period does not accidentally transfer an availability decision to a different time. The effective grid nevertheless validates against the selected template revision.

### Relationships and derived values

- Each profile belongs to exactly one active teacher in the same school and one timetable setup.
- Weekly assigned load is derived from Phase 06 allocations and generated timetable entries; it is not manually entered.
- Remaining load is `MaximumWeeklyPeriods - allocated periods`, counting co-taught periods for each participating teacher.
- Teacher availability is consumed by generation, manual placement, review, and swap candidate evaluation.

### Validation

- Short display name is trimmed, length-limited, and optionally unique within the setup if printouts rely on it for identification.
- Maximum weekly periods is a non-negative integer and cannot exceed the sum of existing available slots.
- Availability can reference only periods in the selected effective schedule and teachers in the same school.
- A teacher marked inactive/deleted cannot remain selectable for new configuration or assignment.
- Schedule changes must identify orphaned availability records and require explicit resolution.
- Save operations use optimistic concurrency and are audited.

## Business Rules

1. Unchecking an availability cell is a hard prohibition for automatic generation and manual lesson placement.
2. A teacher cannot be assigned to or swapped into an unavailable slot, even if the teacher still has weekly capacity.
3. New effective lesson slots use the agreed default availability policy; non-study days and breaks are never availability slots.
4. A teacher's maximum weekly load is checked during assignment and generation. The UI must surface current allocated and remaining load.
5. Co-teaching counts the same period toward every participating teacher's load.
6. Changing a teacher to unavailable in a slot already used by a draft/generated timetable creates a hard review violation; it does not silently delete the lesson.
7. Timing-template changes trigger availability remapping by stable period identity/time where possible and explicit user review where not possible.
8. Teacher availability remains scoped to the timetable setup unless the user intentionally copies it to another setup.
9. The supplied warning about excessive closures is advisory during editing, but generation is blocked when no feasible schedule can satisfy hard constraints.

## ❓ Pending Questions for the User

1. Should new teacher profiles default to all periods available, all unavailable, or inherit a school-wide availability template?
2. Is **أقصى عدد للحصص** a hard limit that may never be exceeded, or a warning that an authorized user can override with a reason?
3. What exactly should **معلم منتدب** and **إخفاء الطباعة** change in generation, assignment, and printed outputs?
4. Do teachers ever manage their own availability, or is this exclusively maintained by the secretary/management team?
