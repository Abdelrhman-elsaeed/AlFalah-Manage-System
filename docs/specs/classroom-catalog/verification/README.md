# C32 design verification

Status: standalone prototype verification only. No production workflow, API, SQL migration, permission or provider write is claimed.

## Reproducible checks

1. Run `python docs/specs/classroom-catalog/scripts/audit-classroom-surfaces.py` from the repository root. [Inventory](source-inventory.json) records354 matched source files, including220 runtime files. The [impact map](../impact-map.md) adds semantic review and consumers named only `classes`.
2. Serve only `docs/specs/classroom-catalog/prototype` on loopback4780, as documented in the package README. The dedicated server keeps the demonstration independent of the running Angular application and authenticated session.
3. From `frontend`, run `node scripts/check-classroom-design.mjs`. [Results](browser-results.json) cover actual rendered390/768/1440 layouts with synthetic in-memory records and **zero application API requests**. [Console receipt](browser-check.txt).
4. The browser prints the saved prototype allocation set into [assignment-statement-sample.pdf](assignment-statement-sample.pdf). [PDF inspection](pdf-check.json) records page count, A4 landscape geometry and embedded font details. [Rendered paper](assignment-statement-page.png).

The source reference was viewed directly at the owner-specified local `2.jpeg`. [Reference receipt](source-reference.json) records the image dimensions/hash and observed column hierarchy. The school photo and its personal names/signature were not copied into the prototype or PDF.

## Verified interactions

- Grade-grouped assignment matrix, explicit single/co/split modes, split quota3+2 with a whole pair retained, teacher selection and draft confirmation.
- Explicit mode required for multiple teachers; unsaved assignment export blocked until save inside the prototype.
- Co-teaching and split annotations in the statement; an outside-specialization core subject appears in the appendix rather than disappearing from the total.
- Full24-teacher statement independent of current screen paging; separate teacher-load view with8-row pages.
- Bulk numeric section creation; overlapping section range rejects the entire attempted batch. Two new sections appear in the teacher's shared catalogue selection.
- Multiple teacher section selection and direct confirmation with no approval queue, matching the owner's explicit answer.
- Dialog Escape/focus return, no page-wide horizontal overflow and no application API calls. The matrix/paper have deliberate internal scrolling on small screens.

## Captures

| View | Phone390 | Tablet768 | Desktop1440 |
|---|---|---|---|
| Assignment matrix | [Image](assignments-390.png) | [Image](assignments-768.png) | [Image](assignments-1440.png) |
| Teacher picker / split quota | [Image](picker-390.png) | [Image](picker-768.png) | [Image](picker-1440.png) |
| Teacher load / pagination | [Image](teacher-load-390.png) | [Image](teacher-load-768.png) | [Image](teacher-load-1440.png) |
| Manager classroom catalogue | [Image](catalog-390.png) | [Image](catalog-768.png) | [Image](catalog-1440.png) |
| Teacher multi-selection | [Image](teacher-selection-390.png) | [Image](teacher-selection-768.png) | [Image](teacher-selection-1440.png) |
| Paper preview | [Image](export-390.png) | [Image](export-768.png) | [Image](export-1440.png) |

Principal matrix, phone picker and rendered PDF were visually inspected. The prototype is a design artifact:24 finite synthetic teachers, no production picker pagination/concurrency/authorization simulation, no durable saving and no PDF signature. The production plan retains existing server filters/paging, import/history and all capacity/pair rules. Sample print annotations deliberately extend the reference where current co/split semantics require clarity. Approved institutional header assets and final source/printer parity need owner review.

Physical devices, assistive-technology acceptance, production load/performance, SQL backfill and any actual school data conversion remain future implementation gates. Application builds/backend tests were not rerun because this phase changes only documentation and standalone prototype assets/scripts. No result here is represented as a production release.


## C33 runtime foundation

The original C32 artifacts above remain design-only evidence. Production catalogue and teacher-selection implementation now has separate [SQL, Angular, live-browser and migration verification](implementation/README.md).

## C34 runtime consumers

C3 now has separate [consumer, retained-history, SQL archive, Angular and real-browser evidence](consumers/README.md). Assignment UX/PDF remain future C4/C5 work.

## C35 runtime assignments

C4 now has separate [Angular/backend, real role-based browser, dense fixture and large-school verification](assignments/README.md), with screenshots at390/768/1440. Development build passes; production CSS budget blockers and physical/assistive-technology limits are disclosed there. C5 saved-revision assignment PDF remains pending.
