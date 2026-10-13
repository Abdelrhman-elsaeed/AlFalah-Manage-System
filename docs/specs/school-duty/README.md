# School Duty — Feature Spec Kit

**Status:** D1 and D2 implemented and verified on isolated SQL/local HTTP and rendered browser fixtures. Final client visual acceptance pending; D3 not started.
**Created / updated:** 2026-10-13  
**Module:** School timetable — `المناوبة`  
**Scope:** One school-wide dismissal duty teacher per operating school day.

This is a feature-sized Spec Kit: research → specification → clarification → plan → tasks → implementation → verification. It extends the existing application; it does not introduce a new platform or replace the timetable.

## Read order

| File | Purpose |
|---|---|
| [research.md](research.md) | Client evidence, image interpretation, current code and integration gaps |
| [references.md](references.md) | Mandatory original image paths, fingerprints, exact report division and D2/D3 visual review gates |
| [dismissal-lateness.md](dismissal-lateness.md) | Owner-confirmed D2 amendment: mobile student picker, guardian notice, student history and existing manager statistics |
| [spec.md](spec.md) | Business requirements, workflows and acceptance scenarios |
| [clarifications.md](clarifications.md) | Confirmed owner decisions and proposed defaults awaiting plan review |
| [ux.md](ux.md) | Manager and mobile teacher experience; visual acceptance |
| [plan.md](plan.md) | Layering, proposed data/API contracts, attendance and archive integration |
| [tasks.md](tasks.md) | Three implementation phases, deliverables and verification |
| [prompts/d1-implementation.md](prompts/d1-implementation.md) | Copyable first implementation prompt; authorizes D1 when submitted in a new chat and makes premium frontend quality an acceptance requirement |
| [prompts/d2-implementation.md](prompts/d2-implementation.md) | Next-phase implementation prompt: D2 only, confirmed amendments/System communication, premium mobile frontend and actual rendered verification |
| [verification/d1.md](verification/d1.md) | Delivered D1 contracts, live versus mocked checks, visual evidence, baseline limitations and rollout guidance |
| [verification/d2.md](verification/d2.md) | Delivered D2 workflows, source mapping, real internal communication, SQL/security tests, rendered evidence and remaining limits |

Read the project [constitution](../../../.spec/constitution.md), [architecture](../../01-ARCHITECTURE.md), and [phase tracker](../../phases/PHASE-DUTY-SCHOOL-DUTY.md) before implementation. These documents are written in English; Arabic form labels and product copy are retained verbatim where relevant.

## Confirmed scope

- A duty tab in the existing school timetable workspace.
- Equal cyclic rotation over the teachers selected in duty settings, teacher exclusions, and audited manual changes; teaching load does not weight the rotation.
- Automatic absence cover by the next eligible scheduled teacher, with an exchange of the two duty dates; repeat if absence continues.
- A responsibility letter acknowledged using the teacher's saved handwritten signature.
- A daily report with late students, violating students, observations, and the last student's departure time.
- Selected-student entry through a modern mobile search/filter/classroom picker; dismissal-lateness notices to the student's guardian and one shared fact projected into student history and existing manager count/name statistics (D2 implemented).
- Both guardian-dashboard notice and a message in the existing Messages tab, automatically authored by `النظام` / `System`; the internal channel is confirmed, with durable deduplication/retry and no personal impersonation (C17, D2 implemented).
- Exact source report division and mandatory reference review: `C:\Users\abdelrhman\Desktop\المناوبة\New folder`, especially `4.jpeg`/`5.jpeg`; D2 section mapping and D3 real PDF visual comparison.
- Two separate official PDFs (letter and report) archived under school root / `المناوبات` / academic year / term / actual teacher / duty date.
- The school manager operates the feature initially; teachers access their own duty workflow.

General feature assignment is a separate later feature: the manager chooses a feature and grants a school user the relevant access. Neither existing timetable editor grants nor storage delegation automatically grants duty management.

## Current progress

| Stage | Status |
|---|---|
| Evidence and code research | Complete — static review, no runtime changes |
| Business clarification | Five clarification answers confirmed on 2026-10-13, including equal selected-roster rotation and archive hierarchy |
| Specification, UX, plan and tasks | D1/D2 authorized; implementation/evidence reflected in the kit |
| D1 — Rotation and absence integration | Implemented; final client visual acceptance pending |
| D2 — Teacher execution and signatures | Implemented and verified; final owner visual acceptance pending |
| D3 — Official documents and storage | Not started |
| Later feature assignment | Deferred by owner |

The submitted D2 implementation request explicitly authorized C1–C17 and the documented relevant defaults, superseding the earlier prompt-preparation status. D2 extends D1 and stops at locally signed snapshots/archive intents. The [D2 verification report](verification/d2.md) distinguishes real isolated HTTP/SQL communication from mocked browser presentation, preserves known baseline failures and records pending owner visual acceptance. D3 remains unstarted.

## Change log

| Date | Change |
|---|---|
| 2026-10-13 | **D2 delivered:** own-duty letter acknowledgement, server drafts/review/saved-signature submission, frozen history and D3 intents; mobile enrolled-student picker; atomic shared dismissal facts/corrections; System guardian dashboard/internal Messages with independent retry/read state; existing manager statistics and actual student timeline/profile. 361 affected backend, 86 Angular and 20 real isolated HTTP checks; rendered 390/768/1440 evidence. Synthetic guardians only, operational data/config preserved; D3 not started, owner visual acceptance pending. See [verification](verification/d2.md), D-108. |
| 2026-10-13 | Prepared the English D2-only implementation prompt covering C1–C17, saved signatures, exact source sections, premium mobile student picker, shared lateness/history/statistics and System-authored guardian dashboard/Messages delivery. Requires actual 390/768/1440 inspection, isolated SQL/synthetic guardians and regression checks; D3 excluded. Preparation only; no runtime implementation/notification. |
| 2026-10-13 | C17 resolves guardian delivery: notice visible in guardian dashboard **and** system-authored message in existing internal Messages. Documented explicit server system provenance, existing human-sender gap, correlated/independent surface retry/read states and spoofing/isolation regression requirements. Supersedes the earlier channel ambiguity; no external provider, runtime implementation or D1 changes. |
| 2026-10-13 | Owner amended D2: no typed student names; premium mobile search/filter/classroom picker, dismissal-lateness guardian notice, student history and existing manager counts/names. Added shared-fact/retry/correction/authorization planning and exact source report division/reference gates for D2/D3. Supersedes duty-only lateness and manual-name fallback; external channel not specified. Documentation only; D1 unchanged and D2/D3 implementation unstarted. |
| 2026-10-13 | Delivered D1: equal selected-teacher rotation, date exclusions/closures, deterministic preview/publication and future regeneration, atomic effective-queue absence exchange, durable attendance reconciliation/corrections/retries, manager review/protection, cross-term compensation and teacher schedule confidentiality. Added premium RTL/LTR workspace and rendered evidence. Final verification: 278 backend checks including 22 duty tests, 69 targeted Angular checks and three rendered widths; existing SQL databases are rejected before any test migration. Original operational database/settings/credentials preserved; D2/D3 unstarted; owner visual acceptance pending. |
| 2026-10-13 | Reviewed five supplied images and client conversation; inspected timetable, staff attendance, signatures, student roster and storage code. Created the draft kit with three implementation phases. Incorporated the owner's date-exchange, saved-signature and manager-first decisions. |
| 2026-10-13 | Owner confirmed equal rotation over the explicitly selected teacher pool, exclusions through duty settings, and school/year/term/actual-teacher/date archival with separate letter/report PDFs. Updated specification, plan, tasks and project memory. |
| 2026-10-13 | Prepared the English D1 prompt for a new implementation chat and strengthened the frontend visual-quality gate. Reviewed existing standby/daily lesson-cover support versus the supplied official waiting-period register; recorded a later extension recommendation without expanding D1. |
