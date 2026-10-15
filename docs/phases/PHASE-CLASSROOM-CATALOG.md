# Classroom catalogue and teaching assignments

**C33 / D-125 · 2026-10-15 · Catalogue/teacher-selection foundation implemented locally; remaining consumers and assignment UI/PDF pending.**

The owner chose to review the impact inventory and design before implementation. Direct teacher confirmation of multiple catalogue sections and stage-local grade numbering/numeric sections are explicitly confirmed. The plan reuses existing Classroom IDs, extends the existing teacher-class bridge, removes live name-based identity comparisons through a staged migration and retains historical snapshots.

Delivered: repository impact map with a repeatable354-file lexical inventory, proposed model/interfaces/migration/authorization/export rules, implementation acceptance checklist, theme-aligned standalone interactive prototype and source-structured sample PDF. The prototype covers manager catalogue, teacher multi-select, assignment matrix/picker/co/split/load, dirty-state handling and paper preview at390/768/1440 with no API calls. It is served independently on loopback4780 and is not wired into the production Angular routes.

| Phase | State |
|---|---|
| C0 Discovery/model/visual prototype | Delivered for owner review |
| C1 Read-only live mapping and additive schema | Delivered locally; exact-only mapping, schema70, no guessed letters |
| C2 Shared catalogue and ID-based teacher association | Foundation delivered; manager page, shared selector and revisioned self/manager saves |
| C3 Consumer/authorization cutover | Teacher class-scope joins use IDs; full consumer conversion pending |
| C4 Production assignment UI | Not started |
| C5 Production saved-revision assignment PDF | Not started |
| C6 Pilot/physical/printer acceptance | Not started |

C32 changed no production behavior. C33 now changes the catalogue and teacher-selection runtime with an additive local migration; existing signed artifacts, old classroom fields, archive enablement and provider roots were verified unchanged. [C33 implementation](../specs/classroom-catalog/implementation.md). [Package](../specs/classroom-catalog/README.md), [implementation gates](../specs/classroom-catalog/implementation-plan.md), [verification](../specs/classroom-catalog/verification/README.md).
