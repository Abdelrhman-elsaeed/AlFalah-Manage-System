# C28 / D-120 — Unsubmitted-report diagnosis and intentional draft departure

Owner-requested follow-up, 2026-10-15. No new migration, Drive write, signing, report submission, notification or operational data change was performed during this diagnosis/UI amendment.

## Actual missing-report diagnosis

An authenticated read of the existing school's archive/execution/documents endpoints showed the 2026-10-15 E2E Teacher assignment has one available, verified **Archived Letter**, and no signed Report. Archive readiness was enabled, processing enabled and Connected. Direct **read-only SQL**, independently repeated after the UI changes, confirmed assignment 2 has draft version 6, saved 2026-10-14, `SubmittedAt = NULL`, one signed Letter, zero signed Reports and **zero report archive intents**. The original diagnostic session later expired; a subsequent authenticated read correctly returned401, so it was not presented as a new successful probe. The independently repeated SQL diagnosis still confirmed the unsubmitted state.

The report is absent from Drive because this actual report has not been committed through teacher signing/submission; there is no report delivery operation to retry. `SaveDraftAsync` only stores editable content. `SubmitAsync` freezes the author's signed Report, creates its archive intent in the existing transaction and sets the draft's submission timestamp. Letter acknowledgement remains independent. The prior C27 real five-file generation evidence used an isolated synthetic test report; it was cleaned afterward and does not mean this teacher's existing draft was submitted. No test or agent signed the existing teacher's report to manufacture a second file.

To complete this actual draft, the teacher opens **Edit duty → Daily duty report**, completes actual observations/empty categories/last departure, selects **Review report → Sign and submit report**, and waits for submission success. Existing actual-time/ownership/signature rules still apply. The report then archives automatically when enabled; successful submission and verified Drive delivery remain separate states.

## Delivered behavior

Only an actual editable, acknowledged, unsubmitted report **worked on during this editing session** triggers the new centered draft notice on departure. Actual content edits, a restored unsaved cache, or intentionally reviewing the report count as work. Merely opening/viewing an existing saved draft, loading the page, searching, manager/historical inspection, autosave itself and already submitted reports do not open it. This preserves the earlier no-random-refresh-warning decision.

Internal duty navigation, leaving the whole duty workspace, its Back action and an explicit execution refresh all use the same departure check. Parent/child guards and duplicate clicks share one pending decision. Pending save/sign operations cannot dispose the editor. Dirty content must save successfully before the dialog states that changes are saved; failure/conflict retains input and blocks departure with the existing error presentation.

The dialog explains **saved draft ≠ submitted report ≠ Drive delivery**, gives three concrete completion steps and offers **Continue report** plus **OK**. Continue (or Escape) keeps the editor and input; OK proceeds with the saved draft. No button signs/submits/notifies or retries an archive. The browser's refresh/tab-close boundary cannot display arbitrary application HTML or run an awaited save; the existing native browser warning/cache protection remains limited to genuinely unsaved input. The custom notice is for application-controlled navigation, not an invented guarantee about OS/browser closing.

Arabic/English copy, RTL/LTR, native modal semantics, focus entry/return, a complete Tab/Shift+Tab loop, 48px controls, reduced motion and bounded phone scrolling are implemented. Template disabled-state checks remain pure; they never execute departure prompts during rendering.

## Verification

- **155/155 Angular checks** passed, including the first nine new departure tests plus existing timetable/settings/picker/guardian/internal-messaging regressions. Final **32/32 focused checks** passed after the keyboard-focus and newer-input-during-save refinements, including the tenth departure test; these overlap the 155 except for the added save-race case.
- Actual running Angular/Chromium at **390/768/1440px Arabic**, and **1440px English** with the real translation service explicitly switched: untouched draft leaves silently; autosave does not show a notice; edited saved report warns; dialog centered with three steps; keyboard focus cycles; Continue retains input; OK leaves a draft; report submission suppresses the warning; full workspace departure uses one custom dialog; failed save preserves input and never falsely claims it is saved. These use intercepted synthetic APIs, **zero provider writes**, and do not establish physical-device acceptance.
- Development frontend build passed in 45.351 seconds; the final live-server incremental bundle also compiled successfully.
- All four retained dialog captures were visually inspected. An initial English fixture only changed storage preferences while app bootstrap still selected Arabic; it was corrected to switch the actual translation service and assert English text/LTR before claiming English coverage. A browser focus-cycle finding was corrected and rechecked.
- Current backend application behavior was read only. The real original draft remains unsubmitted. Earlier SQL/storage/production CSS baseline limitations remain documented in D3/C27 evidence; no budgets or unrelated tests were relaxed.

Evidence: [browser results](draft-exit-screens/browser-results.json), [Arabic phone](draft-exit-screens/draft-exit-ar-390.png), [Arabic tablet](draft-exit-screens/draft-exit-ar-768.png), [Arabic desktop](draft-exit-screens/draft-exit-ar-1440.png), [English desktop](draft-exit-screens/draft-exit-en-1440.png), [155-check log](draft-exit-checks/draft-exit-angular.txt), [final focused log](draft-exit-checks/draft-exit-final-tests.txt), [browser log](draft-exit-checks/draft-exit-browser.txt), [development build](draft-exit-checks/draft-exit-development.txt) and [independent read-only SQL diagnosis](draft-exit-checks/missing-report-sql.txt). Owner visual/physical-device acceptance remains pending.

```powershell
Set-Location frontend
npm.cmd test -- --watch=false --browsers=ChromeHeadless --ts-config=tsconfig.d2-spec.json --include='src/app/features/timetables/duty/duty-draft-exit.spec.ts' --include='src/app/features/timetables/duty/duty-workspace.spec.ts' --include='src/app/features/timetables/duty/school-duty-navigation.spec.ts'
node scripts/check-duty-draft-exit.mjs
npm.cmd run build -- --configuration development
```

No backend rebuild/restart, configuration/credential/root/grant/enablement change, schema migration, new archive operation or deployment is needed for this UI amendment. Angular4200 serves the current source. Rollback removes the notice/guard integration while keeping existing drafts, signed history and archive intents intact.
