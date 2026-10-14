# School Duty — Feature Spec Kit

**Status:** D1–D3 implemented; independent isolated SQL/local HTTP, browser and actual PDF verification delivered. Real Drive delivery/read/recovery passed in the explicitly designated synthetic D3_Test destination; final client visual acceptance and physical-device review remain pending. See [D3 verification](verification/d3.md).
**Current entry (C18):** [School Duty](http://localhost:4200/school-duty) is an independent sidebar page; old timetable duty links redirect with retained context. See [navigation correction](verification/navigation.md).
**Local manual testing:** The owner subsequently authorized configured local Development migrations and startup on 2026-10-14. API5264/Angular4200 are running; global duty processing is ready, original school enablement remains off, and unrelated Visit processing is disabled; see [local operation](verification/local-manual-testing.md).
**Created / updated:** 2026-10-13 / 2026-10-14
**Module:** Independent School Duty sidebar category — `المناوبة`
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
| [prompts/d3-implementation.md](prompts/d3-implementation.md) | Prepared D3-only prompt: source-faithful official PDFs, immutable artifacts, protected archive/recovery, premium document UI and bounded synthetic verification |
| [verification/d1.md](verification/d1.md) | Delivered D1 contracts, live versus mocked checks, visual evidence, baseline limitations and rollout guidance |
| [verification/d2.md](verification/d2.md) | Delivered D2 workflows, source mapping, real internal communication, SQL/security tests, rendered evidence and remaining limits |
| [verification/d3.md](verification/d3.md) | Official PDF/source comparison, immutable artifacts, protected archive/recovery, actual SQL/HTTP/download checks, rendered UI and activation/acceptance gates |

Read the project [constitution](../../../.spec/constitution.md), [architecture](../../01-ARCHITECTURE.md), and [phase tracker](../../phases/PHASE-DUTY-SCHOOL-DUTY.md) before implementation. These documents are written in English; Arabic form labels and product copy are retained verbatim where relevant.

## Confirmed scope

- An independent School Duty sidebar category with focused schedule, settings, execution, archive and manager review pages (C18–C23).
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
| Specification, UX, plan and tasks | D1–D3 authorized; implemented contracts and evidence reflected in the kit |
| D1 — Rotation and absence integration | Implemented; final client visual acceptance pending |
| D2 — Teacher execution and signatures | Implemented and verified; final owner visual acceptance pending |
| D3 — Official documents and storage | Implemented and verified, including designated real Drive delivery/read/recovery; owner/physical acceptance pending |
| Later feature assignment | Deferred by owner |

The submitted D3 implementation request authorizes C1–C17, relevant documented defaults and the approved archive hierarchy. D3 extends D2's signed snapshots/intents with immutable official PDFs and protected delivery/recovery. [D3 verification](verification/d3.md) distinguishes actual isolated SQL/HTTP/native browser downloads and internal guardian delivery from mocked presentation/provider doubles. Earlier D1/D2-only authorization and handoff entries below are historical. Subsequent owner-authorized local operation and isolated synthetic Drive verification are recorded below; original school external activation remains an owner rollout choice. Stop after D3.

## Change log

| Date | Change |
|---|---|
| 2026-10-14 | C25 / D-117: explicitly authorized School Duty grant/revoke for every active school-account role; exact scoped Duty permissions, atomic audited idempotent/versioned grants and live identity/revocation; six administrative pages/dashboard, no recursive delegation/general storage. Dashboard width matches1440px layout including1920px.82 backend/146 Angular/16 real isolated HTTP/four-width UI passed; no schema migration or operational grants. [Evidence](verification/delegation-amendment.md). |
| 2026-10-14 | C24 owner correction: duty summary moved to the end of the general account dashboard after all header/tools/content; three-width browser/role checks passed. No backend/migration/provider change. [Evidence](verification/admin-amendment.md#owner-dashboard-placement-correction). |
| 2026-10-14 | C24 / D-116: administrator layout with separate step buttons, truthful reload protection, ten-row teacher/order/fairness/history paging, paired SQL archive, optional protected dismissal time and general dashboard for actual Duty.Manage accounts. 78 backend / 141 Angular, three widths, 14 live protected HTTP checks and18 native downloads from retained real Drive artifacts; no new provider writes/migration/operational activation. [Evidence](verification/admin-amendment.md). |
| 2026-10-14 | C20–C23 / D-114–115: focused ordered sidebar pages, explicit centered error OK, automatic school-today editing, ten-day teacher cards/filters and alerts, archive-only browsing/history, full-width review and equal desktop categories; hardened ownership/future-date checks and manager storage enable/pause. 75 backend / 138 Angular checks, three-width UI/regressions, 15 real Drive recovery checks, 10 protected archived HTTP checks and 18 actual native downloads. Owner reconnected OAuth after local callback fix; D3_Test uses fresh isolated SQL, original school activation remains off. [Evidence](verification/workspace-amendment.md). |
| 2026-10-14 | C19 / D-113: all active teachers by default, explicit permanent/date exclusions, timetable-linked study days and aligned 44px calendar controls; historical publication/execution retained; 72 affected backend / 114 focused Angular and rendered three-width checks passed; no migration/external activation. [Verification](verification/settings-amendment.md). |
| 2026-10-14 | C18 / D-112: independent top-level School Duty sidebar entry and `/school-duty` page; lesson timetable separated, old duty/document/signature links preserved by redirect, active-school/capability and unsaved-work guards retained. [Rendered/navigation evidence](verification/navigation.md). No backend/migration/Drive change. |
| 2026-10-14 | Owner-authorized local manual-test startup: verified COPY_ONLY/checksum backup; nine pending migrations applied (59 to68) through D3; preserved old counts, Drive credentials/roots/grants and config/key hashes; CHECKDB passed. Current API5264 and Angular4200 running, startup seeding/migrations disabled and external duty/visit delivery OFF. [Local evidence](verification/local-manual-testing.md), D-111. |
| 2026-10-14 | **D3 delivered:** source-inspected official roster/letter/report PDFs, frozen artifacts/provenance, independent letter/report local access, retained correction/history, protected school/year/term/actual-teacher/date archive, reserved IDs/leases/bounded retry/reconciliation and premium RTL/LTR preview/download UI. 21 D3 SQL, 96 focused Angular and 37 live local HTTP checks; expanded backend results and known baseline failures recorded; 15 actual PDF pages and 390/768/1440 captures retained. Real internal synthetic guardian delivery; provider doubles remain TestVerified. External Drive/owner/physical review pending; operational data/configuration preserved. [Evidence](verification/d3.md), D-110. |
| 2026-10-13 | Prepared the [English D3-only prompt](prompts/d3-implementation.md): official roster/letter/report source comparison, frozen D2 signatures/assets, protected duty archive, durable ID/lease/retry/reconciliation, premium 390/768/1440 UI and actual PDF inspection. Requires fresh guarded SQL and bounded authorized synthetic provider verification; preserves D1/D2 and known baseline limits. Documentation only; D3 runtime unstarted, external/owner acceptance not claimed. D-109. |
| 2026-10-13 | **D2 delivered:** own-duty letter acknowledgement, server drafts/review/saved-signature submission, frozen history and D3 intents; mobile enrolled-student picker; atomic shared dismissal facts/corrections; System guardian dashboard/internal Messages with independent retry/read state; existing manager statistics and actual student timeline/profile. 361 affected backend, 86 Angular and 20 real isolated HTTP checks; rendered 390/768/1440 evidence. Synthetic guardians only, operational data/config preserved; D3 not started, owner visual acceptance pending. See [verification](verification/d2.md), D-108. |
| 2026-10-13 | Prepared the English D2-only implementation prompt covering C1–C17, saved signatures, exact source sections, premium mobile student picker, shared lateness/history/statistics and System-authored guardian dashboard/Messages delivery. Requires actual 390/768/1440 inspection, isolated SQL/synthetic guardians and regression checks; D3 excluded. Preparation only; no runtime implementation/notification. |
| 2026-10-13 | C17 resolves guardian delivery: notice visible in guardian dashboard **and** system-authored message in existing internal Messages. Documented explicit server system provenance, existing human-sender gap, correlated/independent surface retry/read states and spoofing/isolation regression requirements. Supersedes the earlier channel ambiguity; no external provider, runtime implementation or D1 changes. |
| 2026-10-13 | Owner amended D2: no typed student names; premium mobile search/filter/classroom picker, dismissal-lateness guardian notice, student history and existing manager counts/names. Added shared-fact/retry/correction/authorization planning and exact source report division/reference gates for D2/D3. Supersedes duty-only lateness and manual-name fallback; external channel not specified. Documentation only; D1 unchanged and D2/D3 implementation unstarted. |
| 2026-10-13 | Delivered D1: equal selected-teacher rotation, date exclusions/closures, deterministic preview/publication and future regeneration, atomic effective-queue absence exchange, durable attendance reconciliation/corrections/retries, manager review/protection, cross-term compensation and teacher schedule confidentiality. Added premium RTL/LTR workspace and rendered evidence. Final verification: 278 backend checks including 22 duty tests, 69 targeted Angular checks and three rendered widths; existing SQL databases are rejected before any test migration. Original operational database/settings/credentials preserved; D2/D3 unstarted; owner visual acceptance pending. |
| 2026-10-13 | Reviewed five supplied images and client conversation; inspected timetable, staff attendance, signatures, student roster and storage code. Created the draft kit with three implementation phases. Incorporated the owner's date-exchange, saved-signature and manager-first decisions. |
| 2026-10-13 | Owner confirmed equal rotation over the explicitly selected teacher pool, exclusions through duty settings, and school/year/term/actual-teacher/date archival with separate letter/report PDFs. Updated specification, plan, tasks and project memory. |
| 2026-10-13 | Prepared the English D1 prompt for a new implementation chat and strengthened the frontend visual-quality gate. Reviewed existing standby/daily lesson-cover support versus the supplied official waiting-period register; recorded a later extension recommendation without expanding D1. |
