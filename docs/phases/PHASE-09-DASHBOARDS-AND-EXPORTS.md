# Phase 9 — Dashboards & Exports

**Status:** Not started · **Last updated:** 2026-07-10

## Goal
Role-based dashboards with analytics and Excel/PDF exports.

## Scope
### In
- Main Manager dashboard
- School Manager dashboard
- Moderator dashboard
- Instructor dashboard
- Excel/PDF exports
- Filters
### Out
- Hardening (Phase 10)

## Filters
- Academic year, Semester, School, Subject, Stage, Moderator.

## Dashboard contents
Per-role dashboard contents are defined in
[../03-ROLES-AND-PERMISSIONS.md](../03-ROLES-AND-PERMISSIONS.md).
> Remember: Main Manager dashboard must not expose complaint details.

## Acceptance criteria
- Each role dashboard renders its defined metrics.
- Exports (Excel/PDF) work with filters applied.

## Dependencies
Phases 4–8 (data to aggregate).
