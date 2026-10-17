# Standby publication and PDF discoverability

Date: 2026-10-17. C43 / D-135. Follow-up within the existing three-phase delivery.

## Cause and resulting behavior

Weekly apply deliberately creates a draft. The independent page introduced in C41 only exposed that operation and directed users back to teaching for publication. The phase 3 PDF was already available in the daily coverage ledger; the weekly page did not expose its entry point. These were workflow/discoverability gaps, not a missing allocator or PDF renderer.

The review dialog now offers explicit **حفظ ونشر للمعلمين** as the primary action and **حفظ الانتظار كمسودة** as the secondary option. Preview still writes nothing. Save-and-publish first applies the exact accepted proposal, then calls the existing timetable publication endpoint using the returned saved revision. The dialog explains that publication includes the entire saved teaching timetable with standby slots. Existing backend hard validation, scope, concurrency, snapshots, publication event and clone restrictions remain authoritative.

The page shows the saved publication status. **نشر المحفوظ للمعلمين** publishes an existing clean draft without applying it again. If apply succeeds but publication fails, the saved draft remains, an explicit partial-success message appears, and retry only calls publication. This is two existing operations, not an atomic transaction. A conflicting revision requires reload/review; a successful publication is never reported before server confirmation. Experimental copies retain draft-only behavior. Saving/reloading still clears local undo history.

Publishing weekly slots makes them visible in teacher timetables. It does not create a dated absence assignment or send that assignment's notification. The existing daily approval/dashboard/receipt/execution flow remains separate. No new email, push, or whole-school publication notification is claimed.

The manager's weekly header now contains **سجل الانتظار PDF**. It navigates through the existing guarded route to `/intelligent-timetable/substitutions?register=true`, opening the existing QF71-B-2-1 register dialog automatically. Choose a date, issue a current copy or view a retained revision, then download the exact preview. This is the dated absence/coverage/signature register, distinct from the weekly timetable PDF available on the teaching page. Pending weekly edits still trigger the existing route guard, and an in-flight register issue retains its existing leave protection. Opening the shortcut does not issue a PDF or approve coverage.

## Verification

- Red DOM reproduction: the reviewed dialog lacked a publication action and no publish request could occur. `.audit/standby-publish/red.txt`: one failure, nine passes before the fix.
- 26 focused Angular checks pass in `.audit/standby-publish/tests.txt`: explicit reviewed publication using the saved revision, draft-only save, partial-success retry without duplicate apply, save conflict/clone restrictions, register deep-link/leave protection, and existing undo/daily/register/dashboard regressions.
- `node frontend/scripts/check-standby-publish-live.mjs` exercises the actual Angular shell and real SQL API against the guarded `AlFalahRegisterTests_<guid>` fixture. [Results](browser-results.json) cover 390/768/1440/1920: no write before confirmation; explicit draft/publication; a simulated publication outage followed by retry without a second apply; teacher-authenticated reads showing published standby; unchanged teaching entries; the PDF shortcut, retained canvas preview and downloaded SHA-256 equality. Viewing did not issue another retained artifact. Synthetic timetable revisions change as part of this test; operational data and Drive files do not.
- The synthetic timetable-only manager lacks storage grants. Unrelated shell storage requests return 403; the browser results record these explicitly. No timetable/register response errors were ignored. The one intentional publication 503 is injected by the harness before reaching the API. Captures wait for transient shell toasts and dialog animation to finish.
- A stricter browser run initially caught the verification proxy incorrectly nesting an unrelated `/api/v2/visits/availability` path under `/api/v1/`, producing a harness-only 404 (`browser-final.txt`). The proxy now preserves the complete API path; `browser-verified.txt` passes with only the recorded storage-grant denials. This was a harness correction, not an application endpoint change.
- [Phone review](review-390.png), [desktop review](review-1440.png), [phone published page](published-390.png), and [desktop register](register-1440.png) document the current UI. The older C41 save harness now explicitly selects the draft action; it was not rerun in this follow-up.

No backend/schema/PDF renderer changes, provider writes, commit or deployment. Existing production CSS-budget and PDF text-extraction limitations remain as recorded in phase 3. Normal app: `http://localhost:4200/intelligent-timetable/standby`; API: `http://localhost:5264`.
