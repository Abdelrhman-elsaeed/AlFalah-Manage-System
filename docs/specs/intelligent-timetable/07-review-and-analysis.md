# Phase 07 — Timetable Review and Quality Analysis

**Module:** Intelligent Timetable (الجدول الذكي)  
**Status:** Implemented — see [Phase 7 delivery notes](../../phases/PHASE-TT-07-REVIEW-AND-REPAIR.md)  
**Primary actors:** Secretary, School Manager, authorized timetable editor/reviewer

## Overview

This phase presents the generated timetable and evaluates its correctness and distribution quality. Users can review the school-wide schedule, inspect one class or teacher, search/highlight a subject or teacher, filter detected issues, and launch controlled repair/edit actions.

Analysis separates hard violations—which make the timetable operationally invalid—from soft quality findings such as undesirable concentration, late-period imbalance, or avoidable consecutive teaching.

## UI/UX Requirements

### General timetable preview

- Display a day selector with previous/next navigation and sticky day context while scrolling.
- Render classrooms as rows and effective periods as columns; each header includes period number and exact start/end time.
- Each lesson card shows subject and teacher; include room/location when it differs from the classroom default.
- Give each subject a consistent accessible color across the timetable. Do not use color as the only identifier.
- Render breaks as named non-teaching columns/segments when applicable and clearly distinguish unavailable/non-existent periods.
- Provide density modes equivalent to the mockups: compact, normal, and wide.
- Provide a search box accepting teacher or subject names. Matching cells remain emphasized and non-matches are dimmed, without altering data.
- Provide actions for automatic generation, timetable editing, analysis/review, timetable management/versioning, and printing according to permission.

### Analysis page

- Title the page **مراجعة الجدول** and explain that it reviews class and teacher timetables and identifies repairable errors.
- Provide tabs/toggles for **الفصول** and **المعلمين**, with counts.
- Show summary metrics such as weekly period count, classroom count, teacher count, and issue counts by severity.
- Provide filters:
  - specific classroom or teacher;
  - subject;
  - status/severity;
  - closed/unavailable periods;
  - missing/unassigned periods.
- Include a legend. The reference uses yellow for closed teacher slots and red/pink for unassigned lessons; final colors must meet contrast requirements and be paired with labels/icons.
- For each classroom, show total scheduled periods, subject count, subject filter chips, and its day-by-period grid.
- For each teacher, show total teaching periods, subject count, number of last-period assignments, subject/class filter chips, and the day-by-period grid.
- Clicking a subject/class chip highlights every matching occurrence and dims unrelated cells.
- Empty slots and unavailable slots must be visually distinct.

### Findings and repair

- Display each finding with severity, rule name, affected teacher/class/subject/day/period, explanation, and recommended action.
- Provide **إصلاح الجداول** only to authorized users.
- Every repair proposal must show proposed movements, constraints affected, and predicted error/warning counts before explicit confirmation.
- A repair is never applied implicitly while the user is merely reviewing/filtering.

## Backend & Data Models

### Analysis service and result model

Analysis is primarily derived from a timetable revision plus the exact setup/template revisions used by it. A persisted result is useful for reproducibility and comparison:

`TimetableAnalysisRun`

- `Id`, `SchoolId`, `SchoolTimetableId`, `TimetableRevision`.
- `SetupRevision`, `BellScheduleTemplateRevision`.
- `StartedAt`, `CompletedAt`, `RequestedByUserId`.
- `HardViolationCount`, `WarningCount`; no numeric quality score.
- Analyzer/ruleset version.

`TimetableAnalysisFinding`

- `Id`, `AnalysisRunId`.
- `RuleCode`, `Severity` (Error/Warning/Info), message.
- Optional `ClassroomId`, `InstructorProfileId`, `SubjectId`, `LocationId`, day, and period.
- Structured evidence/related occurrence IDs and suggested repair metadata.

Analysis records are immutable except for recorded soft-violation override metadata. A timetable or setup revision change creates a new run; it never changes old finding evidence or severity counts.

### Required views/read models

- School-by-day grid grouped by logical lesson occurrence.
- Classroom timetable view with subject/teacher/location.
- Teacher timetable view with class/subject/location and unavailable-slot overlays.
- Aggregates for teacher load, last-period count, consecutive-period runs, subject distribution by day, and unmet class-subject quotas.

### Hard validations

- Teacher double-booking at intersecting real-time windows.
- Classroom double-booking.
- Specialist location/room double-booking.
- Placement in a teacher-unavailable slot.
- Placement on a non-study day, break, missing period, or invalid bell-schedule window.
- Missing teacher assignment or unassigned required occurrence.
- Scheduled count different from the class-subject requirement.
- Broken paired block, disallowed day, or violated hard fixed slot.
- Cross-school or inactive/deleted references.

### Soft-quality checks

- Early-period preference missed.
- Individual subject periods unnecessarily concentrated on too few days.
- Excessive consecutive teaching periods for a teacher.
- Subject/class lessons clustered only early or only late when a balanced alternative exists.
- Unequal distribution of last-period duties among comparable teachers/classes.
- Excessive idle gaps in a teacher's day, if this becomes a configured objective.

### Existing model impact

- Analysis needs normalized `SubjectId`, logical lesson-occurrence grouping, location identity, setup revision, and bell-schedule revision; the current free-text subject/period-only entry is insufficient for reliable rule evaluation.
- Existing immutable `SchoolTimetableVersion` snapshots should be created after accepted repairs and swaps.
- Publication should reference the most recent analysis run for the same timetable revision.

## Business Rules

1. Review may run against a saved draft or the published timetable, but the UI must state which revision is being analyzed.
2. Hard constraints determine validity; soft constraints determine quality and prioritization.
3. The generator must honor teacher availability, fixed slots, allowed days, paired blocks, class/teacher/location exclusivity, and required quotas.
4. Individual lessons should be distributed across permitted days where feasible rather than clustered on one day.
5. Paired blocks remain consecutive and intact.
6. Teacher schedules should avoid long uninterrupted runs; the source expectation specifically flags three or more consecutive lessons as undesirable, subject to the final threshold.
7. Late/last-period assignments should be distributed fairly instead of repeatedly burdening the same teacher or class.
8. Early-period subject preferences should be satisfied when feasible and reported when missed.
9. A closed/unavailable teacher slot is shown even when empty; a lesson placed there is a hard violation.
10. Missing assignments/occurrences are prominently shown and block “ready to publish” status.
11. Search and filters change presentation only; they never change analysis results or timetable data.
12. Repair operations are previewed, authorized, atomic, audited, versioned, and followed by a fresh analysis run.
13. A timetable cannot claim a clean result from an older revision after any edit, assignment change, timing change, or swap.

## ❓ Pending Questions for the User

1. Which findings must block publication, and may an authorized role override any hard violation with a recorded reason? Hard violations (like double-booking a teacher or exceeding maximum load) MUST strictly block publication and cannot be overridden. Soft violations (like too many consecutive periods) can be overridden by an authorized role (Management) with a recorded reason.
2. What exact thresholds define excessive consecutive lessons, unfair last periods, excessive gaps, and acceptable subject distribution? Use these default thresholds: Excessive consecutive lessons = more than 3. Unfair last periods = more than 2 per week. Excessive gaps (waiting periods) = more than 2 per day. Acceptable subject distribution = max 1 period per day per class (unless it's a paired block)
3. Should quality be shown as a numeric weighted score, severity counts only, or both—and who defines the rule weights?Show severity counts only (e.g., 3 Critical Errors, 5 Warnings). Avoid numeric weighted scores as they are subjective and overly complex for the user.
4. Should **إصلاح الجداول** automatically choose and apply one best repair after preview, or present several ranked repair proposals for the user to select?Present several ranked repair proposals for the user to select. The system must NEVER automatically apply a repair without explicit user confirmation
