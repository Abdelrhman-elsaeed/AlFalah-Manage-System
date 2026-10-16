# Classroom catalogue and teaching assignments

**C36 / D-128 · 2026-10-16 · Catalogue, consumers, assignment UI and C5 statement implemented locally; C6 pilot/physical acceptance pending.**

The owner chose to review the impact inventory and design before implementation. Direct teacher confirmation of multiple catalogue sections and stage-local grade numbering/numeric sections are explicitly confirmed. The plan reuses existing Classroom IDs, extends the existing teacher-class bridge, removes live name-based identity comparisons through a staged migration and retains historical snapshots.

Delivered: repository impact map with a repeatable354-file lexical inventory, proposed model/interfaces/migration/authorization/export rules, implementation acceptance checklist, theme-aligned standalone interactive prototype and source-structured sample PDF. The prototype covers manager catalogue, teacher multi-select, assignment matrix/picker/co/split/load, dirty-state handling and paper preview at390/768/1440 with no API calls. It is served independently on loopback4780 and is not wired into the production Angular routes.

| Phase | State |
|---|---|
| C0 Discovery/model/visual prototype | Delivered for owner review |
| C1 Read-only live mapping and additive schema | Delivered locally; exact-only mapping, schema70, no guessed letters |
| C2 Shared catalogue and ID-based teacher association | Foundation delivered; manager page, shared selector and revisioned self/manager saves |
| C3 Consumer/authorization cutover | Delivered locally: V2/manual selectors, retained-name current lesson/roster/gate resolution, complete lookup paging; V1 writes remain410; schema71 |
| C4 Production assignment UI | Delivered locally: grouped ledger, bounded picker/summary, real-edit drafts, guards and accessible keyboard/focus; production CSS budget blockers disclosed |
| C5 Production saved-revision assignment PDF | Delivered locally: explicit classification/scope, frozen revisioned PDF, source-structured rows/appendices, real SQL/HTTP/viewport/download checks; schema72 |
| C6 Pilot/physical/printer acceptance | Not started |

C32 changed no production behavior. C33 now changes the catalogue and teacher-selection runtime with an additive local migration; existing signed artifacts, old classroom fields, archive enablement and provider roots were verified unchanged. [C33 implementation](../specs/classroom-catalog/implementation.md). [Package](../specs/classroom-catalog/README.md), [implementation gates](../specs/classroom-catalog/implementation-plan.md), [verification](../specs/classroom-catalog/verification/README.md).

C34 preserves signed/report/version history and provider settings while adding classroom consumer references. See [consumer contracts, semantic inventory disposition and evidence](../specs/classroom-catalog/consumers.md). C4/C5 remain separate delivery gates.

## C35 assignment workspace

C4 now uses the approved ledger direction on the existing Angular assignment route/API.19 Angular and50 backend regression checks pass, with real manager/teacher and synthetic dense/large-school browser checks at390/768/1440. Development build passes; production build remains blocked by unchanged VisitsV2/storage style budgets. No budget relaxation or backend/schema/provider changes. [Behavior](../specs/classroom-catalog/assignments.md), [evidence](../specs/classroom-catalog/verification/assignments/README.md). C5 PDF and C6 pilot/physical acceptance remain separate gates.

## C36 / D-128 - Saved assignment statement (2026-10-16)

C5 now exports the saved assignment revision through the existing page/API and C30 transfer policy. The manager explicitly classifies specialization/other loads; no text guessing or automatic timetable rewrite. Frozen school-scoped snapshots/PDFs, A4 merged headings, whole teacher rows, co/split/pair annotations, stage-aware grades and an other-material appendix are implemented. Fresh SQL checks preceded additive local schema72;37 original-table column hashes are preserved.67 final backend plus one distinct SQL check and26 Angular checks pass, with real PDF pixels and isolated SQL browser downloads at390/768/1440. Development build passes; unchanged VisitsV2/storage CSS budgets still block Production. C6 appearance/assets/physical/printer/pilot acceptance remains. [Behavior](../specs/classroom-catalog/statement.md), [evidence](../specs/classroom-catalog/verification/statement/README.md).
