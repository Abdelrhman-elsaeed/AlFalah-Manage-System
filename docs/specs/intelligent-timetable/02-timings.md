# Phase 02 — Study Days and Bell-Schedule Timings

**Module:** Intelligent Timetable (الجدول الذكي)  
**Status:** Requirements draft — no implementation  
**Primary actor:** Secretary / authorized timetable editor

## Overview

This phase defines reusable bell-schedule templates: the study days, non-study days, number of periods per day, and exact local start/end time of every period. One default timing can be applied to all study days, while any individual day can override its period count or times.

This phase replaces the current fixed assumption of eight equally calculated periods. Its saved time boundaries become the single source of truth for generation, timetable display, “current lesson” resolution, and time-sensitive student-affairs workflows.

## UI/UX Requirements

### General settings

- Provide a template selector and the ability to create more than one timing template.
- Required field: **اسم التوقيت / اسم القالب**.
- Provide explicit study-day and holiday/non-study-day selection. The reference UI shows two holiday dropdowns, but the domain should display the resulting study days clearly.
- Show the template's academic scope and whether it is currently selected by a timetable setup.

### Timing editor

- Use two top-level tabs: **الحصص** and **الاستراحة**. This file covers the lesson tab; breaks are specified in Phase 03.
- Within **الحصص**, show a tab for **كل الأيام** followed by one tab per study day (for example Saturday, Sunday, Monday).
- **كل الأيام** edits the default schedule shared by all selected study days.
- A day tab edits only that day's override; changes there do not mutate other days.
- Allow selecting several day tabs via checkboxes and provide **تطبيق هذا التوقيت على الأيام المحددة**.
- Provide a numeric stepper (+/−) for the period count. Changing the count immediately adds/removes visible period rows, with confirmation before discarding configured rows.
- Each period row/card displays:
  - sequence/period number;
  - optional user-visible period label;
  - start time;
  - end time;
  - copy/apply affordance where useful.
- Lay out period cards in a responsive two-column grid on wide screens and a single column on narrow screens.
- Show day tabs with their effective period count; visually distinguish inherited defaults from explicit day overrides.
- Keep **حفظ جميع التغييرات** and **تجاهل** in a sticky action bar and show whether unsaved changes exist.
- Use 12-hour Arabic display if desired by locale, but store/transport unambiguous time values and clearly distinguish AM/PM.

### Validation feedback

- Validate while editing and again on save.
- Highlight both rows involved when two periods overlap.
- Give precise messages such as “Period 3 starts before Period 2 ends.”
- Do not rely on color alone; include icons/text for inherited, invalid, and overridden states.

## Backend & Data Models

### Proposed normalized model

`BellScheduleTemplate`

- `Id`, `SchoolId`, `Name`.
- Optional `AcademicYearId` / `Semester` scope if templates are not school-global.
- `SchoolTimeZoneId`.
- `IsActive`, audit fields, soft-delete fields, and concurrency token/revision.

`BellScheduleDay`

- `Id`, `BellScheduleTemplateId`, `DayOfWeek`.
- `IsStudyDay`.
- `UsesDefaultSchedule` or equivalent inheritance marker.
- Unique constraint on `(TemplateId, DayOfWeek)`.

`BellPeriod`

- `Id`, `BellScheduleDayId` (or default schedule owner), `Sequence`.
- `Name` / `DisplayLabel`.
- `StartLocalTime`, `EndLocalTime` using time-only semantics.
- Unique constraint on `(BellScheduleDayId, Sequence)`.

The effective schedule resolver combines the template default with any day override and returns ordered, materialized period windows. API responses should include inheritance/override metadata so the UI can explain the effective result.

### Relationships and integration

- A `TimetableSetupProfile` selects one bell-schedule template.
- `SchoolTimetable` records the template ID and revision used for generation/publication.
- `SchoolTimetableEntry.Period` remains the sequence key, but validation must use the selected day's effective periods rather than a hard-coded range.
- Published timetable snapshots retain enough period label/time information to remain historically intelligible if a template later changes.

### Existing impact areas that must be changed during implementation

1. **School settings/API/UI:** expose the effective bell schedule through the school settings context. If `SchoolStudentAffairsSettings` needs a direct link, prefer a relation to the active template/revision over duplicated JSON.
2. **Teacher context:** `TeacherContextSchedule`, `GetTeacherTopPriorityQueryHandler`, `GetTeacherCurrentContextQueryHandler`, and `GetTeacherPeriodRosterQueryHandler` currently use configured fixed durations. They must resolve the school's published effective period with half-open boundaries: `start <= local time < end`.
3. **Gate-pass workflow:** teacher resolution for a student's approved exit must use the same effective bell-schedule resolver so breaks and gaps do not map to the wrong teacher. The current flow is represented by `ApproveGatePassCommandHandler` and timetable lookup repository logic; the opaque handler identifier in the source note should not be treated as a stable code name.
4. **Seeder:** `StudentAffairsDataSeeder` must create realistic templates and targeted timetable entries instead of relying on fixed/all-period test data.
5. **Current timetable service:** remove the hard-coded `PeriodCount = 8` validation and derive each day's valid sequences from the active template.

### Validation

- Template name is required, trimmed, length-limited, and unique in its agreed scope.
- At least one study day and at least one period on each study day are required for a usable template.
- `StartLocalTime < EndLocalTime` for every period.
- Periods on the same day may touch at a boundary but may not overlap.
- Sequence numbers must be unique, positive, and contiguous within a day.
- Times must not cross midnight unless the product explicitly elects to support overnight schools.
- A non-study day must not expose an effective lesson schedule.
- Saving a changed template referenced by a generated/published timetable must use versioning or create a new revision; historical schedules cannot mutate silently.

## Business Rules

1. A user can create and retain multiple timing templates.
2. Editing **كل الأيام** establishes or updates the default schedule for all study days that still inherit it.
3. Editing an individual day creates/updates only that day's override.
4. Applying a schedule to selected days intentionally replaces those days' effective period definitions after confirmation.
5. Different study days may have different numbers of periods and different time boundaries.
6. No two lesson periods on the same day may overlap; equality at the previous end/next start is permitted.
7. All time evaluation uses the school's configured time zone, not the browser's or server machine's implicit zone.
8. During a break, passing interval, holiday, or time outside any period, “current period” is `null`; the system must not guess a teacher unless a separately approved fallback rule exists.
9. A timing change invalidates dependent availability grids, constraints, generated schedules, and current-context caches until they are remapped/revalidated.
10. The selected template revision used by a published timetable is immutable for operational lookups.

## ❓ Pending Questions for the User

1. Are templates school-global and reusable across academic years, or must each template belong to one academic year/semester?
2. Can any of the seven weekdays be a study day, including Friday, or is the supported week permanently Saturday–Thursday with exactly two holidays?
3. What are the allowed minimum/maximum number of periods and the minimum period duration?
4. When “apply to selected days” overwrites existing overrides, should the user receive a simple confirmation or a before/after comparison of affected periods and breaks?
