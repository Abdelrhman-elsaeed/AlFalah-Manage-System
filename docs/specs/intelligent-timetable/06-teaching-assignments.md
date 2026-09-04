# Phase 06 — Teaching Assignments

**Module:** Intelligent Timetable (الجدول الذكي)  
**Status:** Requirements draft — no implementation  
**Primary actor:** Secretary / authorized timetable editor

## Overview

This phase defines the teaching relationship between a class-subject requirement and one or more teachers. It supports a normal single-teacher assignment, multiple teachers co-teaching the same periods, and splitting the subject's weekly quota between teachers.

The final UI should follow the requested revision: the class-by-subject assignment matrix is the main page. Teacher cards do not occupy a permanent side column; selecting **إضافة** in a matrix cell opens a searchable, filterable, paginated teacher table.

## UI/UX Requirements

### Assignment matrix

- Render classrooms as rows and configured subjects as columns.
- Each eligible cell shows the class's weekly subject quota and its assignment state.
- A class that does not study a subject renders a disabled/non-interactive cell rather than an empty assignable cell.
- Assigned teachers appear as compact chips inside the cell, with assignment mode and allocated period count where relevant.
- Show an unassigned/pending count at the matrix header and an unsaved-changes banner.
- Provide filters by classroom, subject, stage/grade, and assignment status, plus a teacher/subject search.
- Provide a reset-filter action and sticky row/column headings for large matrices.
- Keep explicit **حفظ التغييرات** and **تجاهل** actions. Navigating away with unsaved changes requires confirmation.

### Add-teacher dialog

- Clicking an eligible empty cell or **إضافة** opens a modal containing a teacher table.
- The table supports server-side search, relevant filters, sorting, and pagination.
- Each row shows at least teacher name, specialization, active status, current allocated load, maximum load, remaining load, and availability/constraint warnings.
- Selecting a teacher returns the assignment to the originating class-subject cell.
- Multiple selection is allowed when co-teaching or split allocation is enabled.
- Do not require the user to increase page size to find a teacher; pagination and search must preserve the current assignment draft.

### Multiple-teacher modes

- When more than one teacher is selected, require an explicit mode:
  - **Co-teaching / معلم مساعد:** all selected teachers teach all occurrences together.
  - **Split quota / تقسيم الحصص:** divide the total weekly periods among teachers.
- For split quota, open **تقسيم الحصص** showing the requirement total and an integer input per teacher. Show remaining/unallocated count live.
- The save/confirm action remains disabled until the allocation is valid.
- Removing a teacher from a cell recalculates the remaining allocation and requires correction before save.

### Teacher load feedback

- Show per-teacher subject count, classroom count, allocated-period total, maximum, and remaining capacity in the selection dialog and assignment summary.
- Capacity at or below zero is visually prominent and includes text; exceeding a hard maximum blocks save.
- Provide a teacher-centric list view as an optional secondary view, while the matrix remains the default required view.

## Backend & Data Models

### Proposed entities

`TeachingAssignment`

- `Id`, `SchoolId`, `TimetableSetupProfileId`, `ClassSubjectRequirementId`.
- `Mode`: SingleTeacher, CoTeaching, or SplitQuota.
- Audit fields and concurrency token.
- One active assignment aggregate per class-subject requirement.

`TeachingAssignmentMember`

- `Id`, `TeachingAssignmentId`, `InstructorProfileId`.
- `AllocatedPeriodCount` (required for split mode; equals the requirement total for single/co-teaching modes).
- `Role`: Primary or Assistant where co-teaching role labels are required.
- Optional effective dates if mid-term assignment changes are in scope.
- Unique constraint on `(TeachingAssignmentId, InstructorProfileId)`.

### Relationships

- `TeachingAssignment` has one or more members and belongs to one `ClassSubjectRequirement`.
- A generated lesson references the assignment and the participating teacher(s). Because the existing `SchoolTimetableEntry` represents one teacher slot, co-teaching may produce one entry per participating teacher linked by a shared lesson occurrence ID.
- Introduce a logical `TimetableLessonOccurrence` (or equivalent grouping key) for one class/subject/day/period/location, with one-to-many instructor entries. This avoids treating co-teachers as conflicting duplicate lessons.
- Split assignments allocate distinct occurrences to different members; one occurrence normally has only its allocated teacher unless co-teaching is additionally modeled.

### Validation

- The class-subject requirement must exist and be active in the same setup/school.
- Every selected instructor must be active and belong to the same school.
- No duplicate teacher member is allowed within one assignment.
- Single mode requires exactly one teacher.
- Co-teaching requires at least two teachers; every member receives the full requirement count for load calculation.
- Split mode requires at least two teachers, non-negative integer allocations, and `sum(member allocations) = TotalWeeklyPeriods`.
- Teacher capacity must be recalculated on the server across all assignments in the setup.
- Assignment save uses the setup revision/concurrency token and is atomic for all changed cells submitted together.

## Business Rules

1. Only configured class-subject cells can receive an assignment.
2. Every active class-subject requirement must have a complete valid assignment before automatic generation.
3. Selecting one teacher assigns the subject's complete weekly quota to that teacher.
4. Co-teaching means participating teachers teach the same class, subject, and periods simultaneously; the full period count contributes to each teacher's load.
5. Split quota means different occurrences are divided among teachers; the allocated counts must add up exactly to the class-subject weekly total.
6. The example split of a five-period requirement into three periods for one teacher and two for another is valid.
7. A teacher's remaining capacity is based on all assignment memberships in the same timetable setup, not only the currently filtered matrix.
8. Assignment does not itself choose exact day/period slots; availability and subject constraints are enforced when generating/placing occurrences.
9. Removing or replacing an assigned teacher after generation creates review violations for affected occurrences until they are reassigned/regenerated.
10. The server remains authoritative for capacity, tenant, duplicate, and allocation validation regardless of UI feedback.
11. Successful assignment changes are audited with old/new teachers, mode, allocations, actor, and setup revision.

## ❓ Pending Questions for the User

1. Can an assignment contain more than two teachers, or should both co-teaching and split quota be limited to exactly two?
2. Must a teacher's subject specialization match the assigned subject as a hard rule, or is it only a search preference/warning?
3. When a requirement includes paired blocks, may the two periods of one pair be split between different teachers, or must one teacher own the complete pair?
4. In co-teaching mode, do you need explicit Primary/Assistant roles, and should assistant teachers appear on all printed/student-facing timetables?
