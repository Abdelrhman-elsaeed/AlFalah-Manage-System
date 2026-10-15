# Classroom catalogue and teaching assignments

**C34 / D-126 · 2026-10-15 · Catalogue/teacher selection and C3 consumers implemented locally; assignment UI/PDF pending.**

The owner chose to review the impact inventory and design before implementation. Direct teacher confirmation of multiple catalogue sections and stage-local grade numbering/numeric sections are explicitly confirmed. The plan reuses existing Classroom IDs, extends the existing teacher-class bridge, removes live name-based identity comparisons through a staged migration and retains historical snapshots.

Delivered: repository impact map with a repeatable354-file lexical inventory, proposed model/interfaces/migration/authorization/export rules, implementation acceptance checklist, theme-aligned standalone interactive prototype and source-structured sample PDF. The prototype covers manager catalogue, teacher multi-select, assignment matrix/picker/co/split/load, dirty-state handling and paper preview at390/768/1440 with no API calls. It is served independently on loopback4780 and is not wired into the production Angular routes.

| Phase | State |
|---|---|
| C0 Discovery/model/visual prototype | Delivered for owner review |
| C1 Read-only live mapping and additive schema | Delivered locally; exact-only mapping, schema70, no guessed letters |
| C2 Shared catalogue and ID-based teacher association | Foundation delivered; manager page, shared selector and revisioned self/manager saves |
| C3 Consumer/authorization cutover | Delivered locally: V2/manual selectors, retained-name current lesson/roster/gate resolution, complete lookup paging; V1 writes remain410; schema71 |
| C4 Production assignment UI | Not started |
| C5 Production saved-revision assignment PDF | Not started |
| C6 Pilot/physical/printer acceptance | Not started |

C32 changed no production behavior. C33 now changes the catalogue and teacher-selection runtime with an additive local migration; existing signed artifacts, old classroom fields, archive enablement and provider roots were verified unchanged. [C33 implementation](../specs/classroom-catalog/implementation.md). [Package](../specs/classroom-catalog/README.md), [implementation gates](../specs/classroom-catalog/implementation-plan.md), [verification](../specs/classroom-catalog/verification/README.md).

C34 preserves signed/report/version history and provider settings while adding classroom consumer references. See [consumer contracts, semantic inventory disposition and evidence](../specs/classroom-catalog/consumers.md). C4/C5 remain separate delivery gates.
