# Phase 03 — Breaks and Non-Teaching Intervals

**Module:** Intelligent Timetable (الجدول الذكي)  
**Status:** Implemented — Phase 03 (2026-09-06)  
**Primary actor:** Secretary / authorized timetable editor

## Overview

This phase configures named non-teaching intervals—such as recess, prayer, or meal breaks—within each bell-schedule template. Breaks have exact start and end times, can apply to all study days or selected days, and appear by name in the timetable.

Breaks are first-class schedule intervals. They are not lesson entries, do not consume a teacher assignment, and must never overlap a lesson period or another break on the same day.

## UI/UX Requirements

### Editor structure

- Reuse the Phase 02 schedule editor and its RTL layout.
- Show top-level tabs **الحصص** and **الاستراحة**, with **الاستراحة** active for this phase.
- Show **كل الأيام** plus one tab per study day, with the same inheritance and multi-day apply behavior as lesson timings.
- Provide a break-count stepper (+/−) and render one editable block per break in chronological order.
- Each break block contains:
  - required name (example: **الفسحة** or **صلاة الظهر**);
  - start time;
  - end time;
  - optional “after period” sequence aid for easier placement;
  - applied-day indicators;
  - delete action.
- The UI may offer a dropdown/sequence representation containing both periods and breaks, as shown in the hand-drawn concept, but exact times remain the authoritative values.
- Provide **تطبيق هذا التوقيت على الأيام المحددة** when one or more day checkboxes are selected.
- Keep **حفظ** / **حفظ جميع التغييرات** visible and show unsaved state.

### Add/edit flow

- **أوقات الراحة** opens an inline editor or modal listing existing breaks and a new-break row.
- The add row includes name, optional placement after a period, start, end, and **أضف**.
- If “after period” is selected, prefill the start from that period's end and, where a following period exists, help align the break end with the next period's start. The user may edit the times subject to validation.
- Deleting a break requires confirmation when the template is already referenced by a generated timetable.
- After save, render the break's user-defined name in general, class, teacher, and print timetable views wherever the interval is shown.

### Validation feedback

- If a break intersects a lesson period, highlight both intervals and block save.
- If two breaks intersect, highlight both and block save.
- Explain gaps separately from errors; an intentional passing interval between a lesson and break is valid.
- A break copied to selected days must be validated against each target day's effective periods before any target is saved.

## Backend & Data Models

### Proposed entities

`ScheduleBreakDefinition`

- `Id`, `BellScheduleTemplateId`, `Name`, optional description/category.
- Audit fields and soft-delete fields.
- Represents the logical named break across the template.

`ScheduleBreakWindow`

- `Id`, `ScheduleBreakDefinitionId`, `DayOfWeek`.
- `StartLocalTime`, `EndLocalTime`.
- No period ID or sequence is persisted. “After period” exists only in the UI and fills the start time once.
- `IsInheritedFromDefault` or an equivalent override mechanism.
- Unique constraint appropriate to `(BreakDefinitionId, DayOfWeek)`.

An alternative aggregate may store default windows and explicit day overrides. Whichever physical model is chosen, the effective-schedule API must return a unified ordered stream of lesson periods and breaks without duplicating inherited records.

### Relationships

- Break definitions belong to a bell-schedule template and cannot be reused across schools through an unscoped ID.
- Break windows are evaluated against the effective `BellPeriod` windows for the same template/day.
- A generated/published timetable records the template revision so historical break names and times remain reproducible.
- `SchoolTimetableEntry` is not created for a break. Current-context lookup returns no active teacher during a break.

### Validation

- Name is required, trimmed, and length-limited.
- `StartLocalTime < EndLocalTime`.
- A break must apply only to study days in the owning template.
- On each effective day, a break may touch but not intersect a lesson or another break.
- The optional “after period” helper lists effective lesson periods for the displayed day; it is never sent to the backend.
- Break operations use template revision/concurrency protection.
- Updating/removing a break referenced by a published schedule creates a new template revision and marks dependent drafts for revalidation.

## Business Rules

1. The user chooses the displayed name of every break; that exact display name is surfaced in timetable views and printouts.
2. A break can be defined once for all study days, for selected days, or as a one-day override.
3. Changes made in **كل الأيام** affect days inheriting the default; a deliberate day override remains isolated unless explicitly overwritten via multi-day apply.
4. Multiple breaks per day are allowed and are ordered by time.
5. No break may overlap any lesson period or another break on the same day.
6. Boundary adjacency is valid: a break may start exactly when a lesson ends and may end exactly when the next lesson starts.
7. Breaks do not count toward classroom subject quotas, teacher loads, consecutive teaching periods, or teacher availability consumption.
8. Current-period and gate-pass teacher resolution must return no lesson/teacher when local time falls inside a break.
9. Applying a break to multiple days is atomic: if any target day is invalid, no target day is changed.

## Finalized User Decisions

1. Must a break always sit directly between two lesson periods, or can it occur before the first period, after the last period, or with intentional gaps? 3 cenarioos can be work its ok
2. Should “after period” remain a required business field, or only a UI helper derived from the exact start/end times? Make it only a UI helper based on exact start and end times. Breaks must be strictly independent time intervals. If we tie a break to a specific period number, future features like 'Swapping Periods' would accidentally shift the break's position or cause logical errors.
3. Can the same named break have different times on different days while remaining one logical break, or should each variation be a separate break record? separate break i think better because maybe i make diffrent period time for each break
4. Should break names be free text only, or should there also be predefined categories such as recess, prayer, meal, and assembly? make it free text and categories will be fine also

## Implemented Physical Model

`BellScheduleTemplate → BellScheduleRevision → BellScheduleDay → ScheduleBreakDefinition → ScheduleBreakWindow` scopes every break to its school and immutable revision through existing relationships. Day `0` owns default breaks; each actual study day independently chooses `UsesDefaultBreaks` or its own definitions. Each definition has one exact-time window, with a unique foreign key. Different daily variations create separate definitions and windows, even when their names match. Period inheritance and break inheritance are independent.

The editor saves the complete schedule atomically; the dedicated breaks endpoint also supports replacing the break collection without sending lesson edits. Both paths validate the final effective days and share the template concurrency token. Removed breaks disappear from the new revision while historical definitions remain immutable. No break creates a lesson/teacher entry. The ordered effective API and existing general/teacher timetable grid and PDF render break names and times. Excel assignment import remains lesson-only.

Implementation and verification: [Phase 03 report](../../phases/PHASE-TT-03-BREAKS.md).
