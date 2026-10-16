# Standby workspace: visual direction and acceptance

**Status:** Design direction; no new UI/prototype delivered yet.
**Required skill:** frontend-design. Use prototype inside phase 1 only where a throwaway interaction would resolve uncertainty.

## Product purpose

The school manager must understand reserve coverage and teacher capacity at a glance, place standby safely, and distinguish recurring preparation from a real absence assignment. The owner explicitly requires exceptional frontend quality and consistency with the current Al-Falah platform.

The signature element is an interactive **coverage workbench**: teaching/standby cells, per-period reserve coverage and contextual teacher capacity in one aligned surface. Quality comes from clarity, precise interaction and polished states.

## Tokens grounded in the actual platform

Reuse the current design-token and component systems:

| Role | Existing value |
|---|---|
| Forest identity | `#12563a` |
| Primary action | `#0d6434` |
| Page | `#f5f4ef` |
| Work surface | `#ffffff` |
| Standby tint | `#edf7f1` |
| Review/change accent | `#946800` |
| Typography | Existing Tajawal interface family, platform type scale and Arabic shaping |

Keep restrained green/gold accents, readable neutral teaching cells, consistent radii, modest elevation and explicit focus states. Use color plus text/icon/pattern. No full-width dark-green navigation band, oversized hero or repeated large teacher cards; the owner previously rejected those layouts.

## Weekly workbench layout

```text
Existing account header and sidebar
Page title/context                         Save / review / publication actions
Completed teaching prerequisite           Weekly mode: Fair | Manual
Day/week navigation                        Required / allocated / available coverage
---------------------------------------------------------------------------
Sticky teacher identity/capacity | Teaching + standby matrix, grouped periods
Teacher teaching/standby counts  | Clear editable free cells and locked lessons
---------------------------------------------------------------------------
Proposal changes / shortages               Review actions
Contextual teacher/period inspector opens on an explicit selection
```

Use the same page width/alignment as adjacent timetable/assignment workspaces. Desktop may show the full week within a contained horizontal grid; day focus reduces visual load. Sticky row/column headers must not cover shell navigation or clip overlays. Keep body scrolling predictable.

## Distinct interaction modes

- **Before teaching completion:** calm prerequisite state explaining what is missing, with a direct generation/review action. No enabled standby operation.
- **Manual:** selecting an eligible empty cell offers placement, a full cell shows its state, teaching remains protected. The inspector shows real period times, teaching load, standby and remaining eligible capacity.
- **Fair:** compute a proposal and return focus to the grid. Show added/retained/removed placements and reasons before applying; preserve manual locks.
- **Mode switch:** changing fair/manual does not silently apply, discard or regenerate a proposal. Preserve work and expose any required review.
- **Shortage:** highlight the affected period and its missing count; selecting it explains eligible candidates and exclusions. A hypothetical success percentage cannot hide uncovered demand.
- **Concurrent change:** retain the user's work and show a clear reload/review path with meaningful differences.
- **Saved/published:** visually separate local proposal, saved draft and published state. Do not show success before server confirmation.

## Capacity language

Use concise labels such as **تدريس**, **انتظار**, **المتاح للتوزيع** and **يحتاج مراجعة**. Explain counts directly, for example five teaching periods, one standby and one eligible remaining period. Physical maximum is not a target that must be filled.

For fair allocation, show a short reason tied to teaching burden, eligible availability or a constrained period. Avoid exposing solver implementation names in product UI.

## Daily and teacher views

The daily screen is a compact absence/lesson ledger with an independent fair/manual control, explicit administrator approval and uncovered needs. Do not reuse the weekly mode state as the daily choice.

The teacher dashboard shows a concise actionable task for their approved assignment: local date, actual period/time, classroom, subject and state. The details page supports the agreed acknowledgement/execution flow; never allow the teacher to claim arbitrary unassigned cover.

The register is paginated/searchable. PDF preview uses the existing polished document viewer, sensible initial zoom, readable page width and clear download/close controls.

## Responsive and accessible behavior

- Verify 390, 768, 1440 and 1920px widths with actual rendered interactions.
- On mobile, use day/teacher focus and an accessible sheet/inspector; do not shrink the desktop week into unreadable columns.
- Keep horizontal scrolling inside the matrix. No unintended page overflow, clipped names, off-screen actions or nested-scroll traps.
- Preserve full long names through accessible details; no hover-only essential information.
- Support keyboard navigation, focus return and visible selection; use comfortably sized targets, generally 44px for primary touch controls.
- Respect reduced motion. Motion should explain a selection or result, not animate every cell or invent calculation progress.
- Verify RTL, English locale, long content, empty/error/loading/stale/saved states and contrast.

## Design review and acceptance

Design review is integrated into phase 1 weekly delivery. A small synthetic prototype may help an uncertain interaction, but is not a separate mandatory phase. Inspect actual weekly UI screenshots/interactions, compare against the platform and refine weak areas; repeat that process for the daily/teacher journey in phase 2 and PDF in phase 3.

Each production phase must repeat the applicable visual checks against the actual Angular UI. A beautiful prototype does not establish production behavior; passing logic tests does not establish visual quality. Record captures, critique, refinements and remaining owner acceptance without inventing results.
