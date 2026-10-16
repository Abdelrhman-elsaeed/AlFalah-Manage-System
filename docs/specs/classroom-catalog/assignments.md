# C35 / D-127 — Production teaching assignment workspace

**2026-10-15. C4 delivered locally.** The approved C32 direction is now implemented in the existing Angular route `/intelligent-timetable/assignments`. C1/C2 and C3 remain the earlier deliveries; C5 saved-revision assignment PDF and C6 pilot/physical acceptance are still pending.

## Working surface

The classroom-by-subject ledger is the primary surface. Stage-local grade groups have separate navigation and completion counts. Each editable cell shows weekly periods, whole pairs, selected teachers, their allocations and teaching mode. A gold marker identifies an actual draft change. Unconfigured subjects remain visibly unavailable; filtering does not erase their assignments.

Classroom rows default to10 per page, with20/50 choices. Subject, status, classroom, stage and grade filters are retained. Search matches classroom, subject or assigned teacher names. Sticky column/subject headers and an internally scrolling region keep the page usable on narrow screens. The teacher view has independent search, name/load/remaining sorting and10-row paging; expandable rows expose the teacher's allocations and link back to the same editor.

The picker retains server search, specialization, nullable active/visiting filters, saved-capacity filtering, sorting/direction and10/25/50-result paging. Selections persist outside the current result page. The displayed load includes every saved assignment and local draft, including allocations outside the current classroom page. The capacity filter is explicitly labelled as using saved load, matching the unchanged server contract.

Single teaching, co-teaching and split quota retain their existing semantics. Co-teaching grants each member the full requirement. Split quota requires exact period/pair totals and whole pairs belonging to one teacher. Selecting multiple teachers requires an explicit co/split choice. Inactive members, unknown members and capacity violations prevent confirmation. Specialization warnings remain advisory.

## Saving and concurrent work

- Confirming a cell changes only the browser draft. Canonically unchanged cells and reverting to the saved value do not create unsaved work. Unassigning an already empty cell is also a no-op.
- Save sends the existing atomic revisioned request, containing changed cells only. Controls prevent concurrent edits or duplicate saves while it runs. The returned overview replaces the local snapshot after success.
- A409 retains the draft and explains how to explicitly reload the current revision. Reload/context changes ask before discarding real edits; failed reloads retain them. Failed teacher searches preserve selected members and offer retry.
- Setup and picker request versions ignore late responses from previous contexts/searches/closed dialogs. Component subscriptions end with the component lifecycle.
- Existing route deactivation and native unload protection remain tied to real unsaved work. Closing an untouched picker does not warn. The dialog traps keyboard focus and explicitly returns it to the originating cell/summary allocation after closing; if filtering removed it, focus returns to ledger search.
- Existing manager/role guards and school-scoped backend authorization remain authoritative. A denied assignment response hides the previous ledger.

## Projection and visual decisions

`assignment-ledger.ts` builds indexes for requirements, cells, teachers, groups and complete teacher loads once per immutable overview/draft replacement. Rendering uses indexed lookups instead of scanning the full assignment set for each visible cell. Orphaned allocations still count toward load until explicitly removed; the existing removal workflow is retained.

The surface uses the approved forest/action green, white/cream, Tajawal and restrained gold. The ledger carries the visual hierarchy; there is no new hero or repeated metric-card wall. Controls have labels and visible focus, counts isolate LTR numeric order, and reduced motion suppresses transitions/animations. This component deliberately stops using the shared `tt-workspace`/`tt-page-header` wrappers: their global boxed/gradient treatment conflicts with the approved quiet ledger. The shell and theme tokens remain shared, with feature-scoped layout styles.

Production picker results use a bounded checkbox list and allocation pane instead of the old horizontally oversized PrimeNG table. The existing picker API/filter semantics are preserved. No second assignment frontend, model, API, permission, migration or storage destination was introduced. There is no placeholder export action: the source-structured saved-revision PDF belongs to C5.

## Verification and limits

[Repeatable checks and screenshots](verification/assignments/README.md) distinguish real authenticated manager/teacher reads from synthetic editing/concurrency scenarios.19 Angular checks and50 existing backend regression checks pass. Real browser layouts and keyboard behavior pass at390/768/1440; the synthetic large school contains180 classrooms,500 teachers and2070 requirements while default rendered lists remain bounded.

Development build passes. The production build is blocked by unchanged over-budget CSS in VisitsV2 and legacy storage; assignment CSS also produces a non-blocking18.33kB warning against16kB. Budgets were not relaxed. This is local functional delivery, not production-release certification. Physical devices, actual screen-reader operation, printer acceptance, sustained server performance and the C5 PDF remain outstanding.

## C36 current statement integration

C5 is now delivered on the existing header after this C35 delivery: [saved-revision statement behavior](statement.md), [actual PDF/SQL/browser evidence](verification/statement/README.md). The new action protects assignment drafts and opens explicit scope/classification/preview controls. Existing filters do not narrow its default full-setup export. The earlier C35 pending-PDF statements above describe that earlier phase. C6 owner/assets/physical/printer acceptance remains pending.
