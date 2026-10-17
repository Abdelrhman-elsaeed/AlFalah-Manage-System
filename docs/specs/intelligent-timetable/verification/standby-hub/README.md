# One standby workspace: weekly planning and daily coverage

Date: 2026-10-17. C44 / D-136. Owner-requested navigation and presentation follow-up within the existing three phases.

## Current workflow

Open **الجدول الذكي → حصص الانتظار** at `/intelligent-timetable/standby`. The page contains **التوزيع الأسبوعي** and **التغطية اليومية**. Daily links use `?tab=daily`; refreshing that URL retains the active section. The daily ledger, weekly editor, publication and dated register reuse their existing components/services/APIs. This is one navigation host, not a new allocation engine, coverage model or file renderer.

Each section loads on first visit and remains mounted while hidden. Weekly selections, settings, proposal/undo state, academic context and the daily selected date survive tab switching. Switching itself writes nothing. In-flight weekly save/publication or daily issue/approval prevents switching/leaving. Leaving the page still checks the existing weekly dirty guard, including when the weekly panel is hidden. Existing reload and register-issue protection remain active.

**سجل الانتظار PDF** lives in the shared header. It selects the daily section and opens its existing register using the current daily date when already loaded. Previous `/intelligent-timetable/substitutions?register=true` and explicit `?tab=coverage` links redirect to this host; their query context is preserved. Plain `/intelligent-timetable/substitutions` now contains only structural swaps and retains `sourceEntryId`, timetable and date deep links. The sidebar label is **تبديل حصص الجدول**. Manager-dashboard daily actions point to the new daily URL; dashboard navigation now parses query-bearing URLs through `navigateByUrl` instead of treating them as a path segment.

## Visual design

Applied `frontend-design` using the established Tajawal, forest/mint/gold/neutral platform palette. The command surface joins the title, restrained identity mark, PDF/teaching actions, distinct section buttons, academic controls, saved revision/status and four weekly counters. Remove the repeated weekly title/eyebrow/publication sentence, detached decorative header and patterned background from the embedded planner. The daily section uses the same header and connected date/status summary; its old standalone heading and detached register button are suppressed only when embedded. No full-width green navigation band or new decorative hero.

The teaching page keeps its existing header/context and behavior. Standalone components retain their headings where applicable. CSS is scoped to the new host or explicit embedded classes, so the existing intelligent-timetable theme is not globally rewritten.

Actual screenshot review exposed a mobile academic selector escaping its grid cell even though the document had no horizontal overflow. Explicit minimum widths and contained selects fixed the overflow; below 480px the selectors stack to keep their selected values readable. The browser harness now checks control bounds as well as document width. Primary controls retain visible keyboard focus; hidden panels do not participate in layout or focus. Tabs are ordinary labelled buttons with `aria-pressed` and controlled panel references, rather than an incomplete ARIA tab widget.

## Verification

- 28 focused Angular checks pass (`.audit/standby-hub/tests.txt`), including actual hub-state/query handling, retained child identity/dirty confirmation, in-flight switching/leaving, legacy PDF redirection and preserved swap links, plus existing allocation/undo/publication/daily/register/dashboard behavior.
- Development build passes in 49.453 seconds (`.audit/standby-hub/build.txt`). This is not a passing production-budget claim; previously documented unrelated VisitsV2/storage CSS blockers remain.
- `node frontend/scripts/check-standby-hub-live.mjs` uses the actual Angular shell and a guarded real SQL fixture. [Four-width results](browser-results.json): 390/768/1440/1920, one visible heading, weekly/daily switching, retained unsaved selections/undo, cancelled dirty leave, retained daily date, empty and signed daily rows, PDF preview/download SHA-256 equality, refresh/deep-link compatibility and separate swaps. The verification makes no write requests except read-only preview, and the stored workbench remains unchanged. Unrelated storage 403s for the timetable-only synthetic manager are explicitly recorded.
- The updated publication browser harness also passes all four widths (`.audit/standby-hub/publication-regression.txt`), covering real draft/publication, teacher reads, injected publication outage/retry and retained PDF bytes. Only the guarded synthetic timetable changes; operational data and provider files remain untouched. It updates the existing [publication evidence](../standby-publish/browser-results.json).
- [Weekly desktop](weekly-1440.png), [weekly phone](weekly-390.png), [daily desktop](daily-1440.png), [daily phone](daily-390.png) and [empty daily state](daily-empty-1440.png) were captured from the actual app. Earlier C41 harness selectors were adapted to the new header/reload controls but that older full save/conflict harness was not rerun in this follow-up.

No backend/schema/authorization/allocation/signature changes, Drive writes, commit or deployment. Existing PDF branding, text-extraction and physical-print limitations remain recorded in phase 3. Normal app remains on `http://localhost:4200`; API on `http://localhost:5264`.
