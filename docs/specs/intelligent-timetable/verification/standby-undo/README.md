# Standby lock explanation and visible undo

Date: 2026-10-17. C42 / D-134. Owner-requested usability follow-up, not another implementation phase.

A lock retains the same teacher/day/period during fair weekly allocation, subject to existing validity rules. It is not publication or a dated absence assignment. Manual selections already start locked; unlocking permits redistribution without immediately removing the placement. Updated mode/legend/tooltips explain this existing policy.

The existing undo stack was reachable only through a small icon above the grid, and unavailable inside the newly introduced review dialog. A DOM regression reproduced the missing review action after a real changed fair proposal. Undo is now a labelled **تراجع خطوة** action in the toolbar, next to review/save when local history exists, and inside review. It restores the preceding settings/placements, preserves previous manual locks, closes invalidated review and never calls apply. **استكمال التعديل** keeps the proposal; the dialog explains the distinction.

A second regression reproduced an unchanged preview inserting an empty undo step. Preview now records history only when its settings/placements actually differ, so the first undo reaches the prior edit. A successful save/reload still clears local history; this change does not promise post-save or cross-session undo. Existing retained timetable history remains a separate workflow with its normal safety constraints.

## Evidence

- 22 focused Angular checks pass: the existing coverage/registration/navigation suite plus fair-review exact-state restoration and unchanged-preview undo behavior. Both new reproductions failed before their respective fixes; ignored logs are `.audit/standby-undo/red.txt`, `no-op-red.txt`, `green.txt` and `tests.txt`.
- `node frontend/scripts/check-standby-undo-live.mjs` drives the actual Angular shell and real SQL preview API using the guarded synthetic `AlFalahRegisterTests_<guid>` fixture. [Browser results](browser-results.json) cover 390/768/1440/1920: remove one local placement, generate fair redistribution, undo to the exact manual state, undo again to the loaded state, compare settings/locks and confirm clean unload/no page overflow. No apply request occurs; stored revision/settings/placements remain unchanged.
- [Phone review](review-390.png) and [desktop review](review-1440.png) show the explicit Undo, Continue editing and Save actions. Toolbar/footer labels, mobile wrapping and pre-save scope were visually reviewed.

The save-flow browser harness now selects Continue editing explicitly rather than any secondary dialog action. It was not rerun in this undo follow-up; C41's actual save/conflict evidence remains separately recorded. No backend code, migration, operational data or provider files changed. The normal app remains at `http://localhost:4200/intelligent-timetable/standby` with API on 5264.
