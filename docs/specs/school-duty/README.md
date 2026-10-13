# School Duty — Feature Spec Kit

**Status:** D1 implemented and verified on isolated SQL/local HTTP and rendered browser fixtures. Final client visual acceptance pending; D2/D3 unstarted.  
**Created / updated:** 2026-10-13  
**Module:** School timetable — `المناوبة`  
**Scope:** One school-wide dismissal duty teacher per operating school day.

This is a feature-sized Spec Kit: research → specification → clarification → plan → tasks → implementation → verification. It extends the existing application; it does not introduce a new platform or replace the timetable.

## Read order

| File | Purpose |
|---|---|
| [research.md](research.md) | Client evidence, image interpretation, current code and integration gaps |
| [spec.md](spec.md) | Business requirements, workflows and acceptance scenarios |
| [clarifications.md](clarifications.md) | Confirmed owner decisions and proposed defaults awaiting plan review |
| [ux.md](ux.md) | Manager and mobile teacher experience; visual acceptance |
| [plan.md](plan.md) | Layering, proposed data/API contracts, attendance and archive integration |
| [tasks.md](tasks.md) | Three implementation phases, deliverables and verification |
| [prompts/d1-implementation.md](prompts/d1-implementation.md) | Copyable first implementation prompt; authorizes D1 when submitted in a new chat and makes premium frontend quality an acceptance requirement |
| [verification/d1.md](verification/d1.md) | Delivered D1 contracts, live versus mocked checks, visual evidence, baseline limitations and rollout guidance |

Read the project [constitution](../../../.spec/constitution.md), [architecture](../../01-ARCHITECTURE.md), and [phase tracker](../../phases/PHASE-DUTY-SCHOOL-DUTY.md) before implementation. These documents are written in English; Arabic form labels and product copy are retained verbatim where relevant.

## Confirmed scope

- A duty tab in the existing school timetable workspace.
- Equal cyclic rotation over the teachers selected in duty settings, teacher exclusions, and audited manual changes; teaching load does not weight the rotation.
- Automatic absence cover by the next eligible scheduled teacher, with an exchange of the two duty dates; repeat if absence continues.
- A responsibility letter acknowledged using the teacher's saved handwritten signature.
- A daily report with late students, violating students, observations, and the last student's departure time.
- Two separate official PDFs (letter and report) archived under school root / `المناوبات` / academic year / term / actual teacher / duty date.
- The school manager operates the feature initially; teachers access their own duty workflow.

General feature assignment is a separate later feature: the manager chooses a feature and grants a school user the relevant access. Neither existing timetable editor grants nor storage delegation automatically grants duty management.

## Current progress

| Stage | Status |
|---|---|
| Evidence and code research | Complete — static review, no runtime changes |
| Business clarification | Five clarification answers confirmed on 2026-10-13, including equal selected-roster rotation and archive hierarchy |
| Specification, UX, plan and tasks | D1 authorized; implementation/evidence reflected in the kit |
| D1 — Rotation and absence integration | Implemented; final client visual acceptance pending |
| D2 — Teacher execution and signatures | Not started |
| D3 — Official documents and storage | Not started |
| Later feature assignment | Deferred by owner |

D1 plan approval and relevant documented defaults were explicitly authorized in the 2026-10-13 implementation request. The whole-feature technical plan includes future D2/D3 contracts; only the D1 surface in the verification document is delivered. Stop after D1.

## Change log

| Date | Change |
|---|---|
| 2026-10-13 | Delivered D1: equal selected-teacher rotation, date exclusions/closures, deterministic preview/publication and future regeneration, atomic effective-queue absence exchange, durable attendance reconciliation/corrections/retries, manager review/protection, cross-term compensation and teacher schedule confidentiality. Added premium RTL/LTR workspace and rendered evidence. Final verification: 278 backend checks including 22 duty tests, 69 targeted Angular checks and three rendered widths; existing SQL databases are rejected before any test migration. Original operational database/settings/credentials preserved; D2/D3 unstarted; owner visual acceptance pending. |
| 2026-10-13 | Reviewed five supplied images and client conversation; inspected timetable, staff attendance, signatures, student roster and storage code. Created the draft kit with three implementation phases. Incorporated the owner's date-exchange, saved-signature and manager-first decisions. |
| 2026-10-13 | Owner confirmed equal rotation over the explicitly selected teacher pool, exclusions through duty settings, and school/year/term/actual-teacher/date archival with separate letter/report PDFs. Updated specification, plan, tasks and project memory. |
| 2026-10-13 | Prepared the English D1 prompt for a new implementation chat and strengthened the frontend visual-quality gate. Reviewed existing standby/daily lesson-cover support versus the supplied official waiting-period register; recorded a later extension recommendation without expanding D1. |
