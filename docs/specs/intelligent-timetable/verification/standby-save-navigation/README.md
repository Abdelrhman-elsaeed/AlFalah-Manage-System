# Standby save and navigation follow-up

Date: 2026-10-17. C41 / D-133. Owner-requested usability correction; this is not an additional delivery phase.

## Reproduced cause

The manual planner's Save button required an accepted preview, but the page presented that disabled button before clearly guiding the user through review. A DOM regression test reproduced the exact symptom: a valid manual selection still left the apparent save action disabled and made no preview/apply request. This was a frontend state/interaction problem, not a rejected server save or a missing manager permission.

The generic amber message compared required slots with allocated slots. Ten required slots and zero selections meant ten unallocated slots, not proof that teacher capacity was exhausted. The wording and placement incorrectly suggested a capacity problem before a reviewed allocation existed.

## Current behavior

- **الجدول الذكي → حصص الانتظار** opens `/intelligent-timetable/standby` as an independent sidebar page. The embedded teaching/standby tab switch is removed from `/timetable`. Both pages reuse the same timetable context/controller and existing weekly standby component/API; no parallel allocation model is created. The new page links back to the school timetable for publication.
- An enabled **مراجعة وحفظ التوزيع** action prepares/reopens a review dialog. The dialog shows required/allocated/unfilled counts, actual additions/removals and the draft/publication effect. Only its explicit final confirmation calls the existing apply endpoint with the exact reviewed fingerprint.
- Before review, neutral contextual guidance explains manual selection or fair proposal generation and the unallocated count. After review, any remaining shortage is explained accurately and does not bypass existing hard rules or invent a new publication restriction. Period counters use “allocated of required” instead of an ambiguous RTL fraction.
- Editing invalidates the accepted review. Stale save closes the obsolete dialog while retaining local work and showing recovery instructions. In-flight work and unsaved changes retain existing navigation/reload protection; clean reads do not acquire a warning.
- Backend authorization, readiness, hard capacity rules, publication, retained coverage/signatures and PDF behavior remain unchanged.

## Verification

-20 focused Angular checks pass, including the previously failing DOM save-review scenario, no apply before confirmation, exact reviewed payload, initial neutral guidance, edit invalidation, distinct sidebar/route and dirty/in-flight navigation guards. Logs: `.audit/standby-followup/save-red.txt` and `coverage-tests.txt`.
- `node frontend/scripts/check-standby-save-live.mjs` exercises the actual Angular shell and real SQL API on the guarded synthetic `AlFalahRegisterTests_<guid>` fixture. [Results](browser-results.json) cover390/768/1440/1920px, manual/fair preview and confirmed save, cancellation without writes, dirty-navigation cancellation/acceptance, stale-save recovery and return to the teaching page. Teaching entries remain byte-for-byte equal in the API response before/after these standby-only writes.
- Development build passes; details in `.audit/standby-followup/build.txt`. The earlier18-teacher harness `check-weekly-standby-live.mjs` has updated route/review selectors but was not rerun against its separate historical fixture in this follow-up.
- [Phone review](review-390.png), [desktop review](review-1440.png), [desktop planner](workspace-1440.png) and [wide planner](workspace-1920.png) were visually inspected. The existing green/gold platform theme, bounded grid and clear confirmation are retained. Phone footer controls fill the available width after review refinement.

Only isolated fixture data is mutated for verification; operational records and Drive files are untouched. The normal frontend/API remain available at `http://localhost:4200` and `http://localhost:5264`. No schema migration, backend implementation, commit or deployment was needed. Previously recorded production CSS budget and PDF text-extraction limits remain outside this change.
