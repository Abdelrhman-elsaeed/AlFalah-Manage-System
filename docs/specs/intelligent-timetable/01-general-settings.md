# Phase 01 — General Settings and Setup

**Module:** Intelligent Timetable (الجدول الذكي)  
**Status:** Requirements draft — no implementation  
**Primary actor:** Secretary, with access governed by timetable permissions

## Overview

This phase establishes the school-scoped timetable workspace and guides the user through the prerequisites required before a timetable can be generated. The reference UI presents the process as a seven-step setup journey: school days, teachers, classrooms, subjects, teaching assignments, subject scheduling rules, and timetable generation.

The secretary is the owner of the academic master data used by the module. In particular, the secretary creates classrooms, places students in their classrooms, records the physical floor/location of each classroom, and creates the subject catalog. Later phases consume this data; they must not maintain disconnected copies.

## UI/UX Requirements

### Module shell and navigation

- Use an Arabic RTL layout.
- Provide a dedicated Intelligent Timetable navigation group. The supplied sidebar concept orders the primary destinations as: **الجدول الدراسي**, **الحصص**, **الاستراحة**, **المعلمون**, **المواد**, and **الطباعة**. The complete product navigation must also expose teaching assignments, review/analysis, and lesson swaps even though those items are absent from the rough sidebar sketch.
- Show the current school prominently and retain the application's existing global shell, authorization, and school context.
- Show the selected academic year, term/semester, and timetable setup/template in a persistent context header so the user cannot unknowingly configure the wrong scope.

### Guided setup

- Present a setup progress indicator and the ordered steps below:

  1. **الأيام الدراسية** — study days and daily periods.
  2. **المعلمين** — teachers and their availability.
  3. **الفصول** — classrooms used in the timetable.
  4. **المواد** — subject catalog/readiness.
  5. **إسنادات** — assign subjects/classes to teachers.
  6. **تخصيص المواد** — quotas and scheduling constraints.
  7. **الجدول** — generate or edit the timetable.

- Each step displays one of: not started, incomplete, valid/complete, or blocked by an earlier prerequisite.
- A blocked step explains the missing prerequisite instead of merely disabling the control.
- Provide **متابعة الإعداد** to open the first incomplete step and **سأفعلها لاحقاً** to close the guide without losing progress.
- Recalculate completion from valid saved data; a step must not remain “complete” after its data becomes invalid or is deleted.

### Master-data readiness

- Provide links or embedded summaries for the secretary to review:
  - active classrooms for the selected academic year;
  - students enrolled in each classroom;
  - classroom stage, grade, section, and physical floor/location;
  - active teachers;
  - available subjects.
- Show actionable empty states, such as “No classrooms exist for this academic year,” with navigation to the owning CRUD screen.
- Display counts and validation warnings before allowing automatic generation.

### Save and concurrency behavior

- Use an explicit save action for scope-level changes and show a persistent unsaved-changes indicator.
- Warn before leaving a page with unsaved changes.
- If another authorized user changes the same setup, reject stale saves and offer reload/review rather than silently overwriting data.

## Backend & Data Models

### Existing models to retain and extend

- `SchoolTimetable` remains the school/year/semester timetable aggregate and retains its draft/published state, revision, audit fields, entries, and immutable versions.
- `Classroom` remains the academic class entity. It already identifies school, academic year, stage, grade, section, and label; it needs a normalized physical location/floor reference or, at minimum, a validated floor/location field.
- `StudentEnrollment` remains the source for which students belong to a classroom for a term.
- `InstructorProfile` remains the teacher identity used by timetable entries and assignments.
- `SchoolStudentAffairsSettings` is an existing school-scoped settings aggregate. Bell schedules affect student-affairs workflows, but reusable, per-day templates should be modeled as timetable entities rather than embedded as an opaque string.

### Proposed setup aggregate

Introduce a `TimetableSetupProfile` (name is provisional) with:

| Field | Purpose |
|---|---|
| `Id`, `SchoolId` | Tenant-safe identity. |
| `AcademicYearId`, `Semester` | Academic scope. |
| `Name` | User-visible setup/template name. |
| `BellScheduleTemplateId` | Selected timing template from Phase 02. |
| `Status` | Draft, ReadyForGeneration, Generated, or Archived. |
| `Revision` / concurrency token | Prevent lost updates. |
| Audit fields | Created/updated user and timestamps. |

Relationships:

- One school has many setup profiles over academic years and semesters.
- A setup profile references one bell-schedule template and supplies the scope for teacher availability, subject rules, teaching assignments, generation, analysis, and swaps.
- A generated `SchoolTimetable` references the setup profile/revision used to create it, so later settings changes can be detected as timetable staleness.
- Setup progress should be computed by a readiness service from saved records, not stored as independent booleans that can drift from reality.

### Classroom location model

- Preferred model: `SchoolLocation` with school, display name, floor/order, optional building/room code, and active status; `Classroom` references its default location.
- A timetable entry may later override the classroom's default location for a subject requiring a lab or specialist room.
- Prevent references to another school's location and preserve historical display values in published timetable snapshots.

### Validation and authorization

- Every query and mutation must enforce `SchoolId` from the authenticated context, never from a trusted client-side filter.
- The academic year must belong to the school, and the semester must be valid for that year.
- Setup names must be trimmed, length-limited, and unique within the agreed scope.
- Only users with timetable-management/master-data permissions may modify setup data. Viewing and printing use separate permissions.
- Audit creation, update, generation, publication, archive, and later swap actions.

## Business Rules

1. The setup is school-, academic-year-, and semester-scoped; data from one school must never be selectable in another.
2. The secretary creates classrooms, assigns students to classrooms, records classroom locations/floors, and creates subjects before those records can be used by the timetable.
3. A later step may be viewed while incomplete, but generation is blocked until all hard prerequisites are valid.
4. Readiness requires, at minimum: study days and periods, active classrooms, active teachers, class-subject requirements, and complete teaching assignments.
5. Physical classroom location is operational data: it must appear in applicable teacher/print views so teachers can reach the correct room quickly.
6. A change to a timing template, teacher availability, class-subject requirement, or assignment after generation marks the generated timetable as needing revalidation; it must not be silently treated as valid.
7. Publishing is separate from saving/generating. Consumers that need the live timetable resolve only the published version.
8. Existing timetable revision/version behavior remains authoritative: successful material changes create an auditable version and stale revisions are rejected.

## ❓ Pending Questions for the User

1. Can a school maintain multiple setup profiles for the same academic year and semester (for example, regular and Ramadan schedules), or only one active setup at a time? yes provide multible setup profiles
2. Should classroom location be a controlled hierarchy (**building → floor → room**) or is a single free-text floor/location value sufficient? single free-text
3. Besides the secretary, which roles may edit each setup step: School Manager, Moderator, or specifically delegated timetable editors?by default secretary and make manager can give the permissions but dont inclllude "instructors and parents rules"
4. Must every classroom have students before generation, or may an empty but active classroom still receive a timetable? empty but active classroom still receive a timetable
