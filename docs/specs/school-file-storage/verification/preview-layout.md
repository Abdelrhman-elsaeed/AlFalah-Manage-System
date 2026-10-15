# C31 / D-123 — Usable write controls and full-width file preview

Owner follow-up, 2026-10-15: Upload/New folder appear unusable inside a visit archive; the PDF preview is a tiny column in a wide dialog. This follow-up fixes presentation and an independently reproduced destination-loading race. It extends the single native School files browser, with no second browsing interface or backend permission change.

## Diagnosis

1. **Tiny preview, reproduced in the actual Angular renderer.** The native dialog reused the retired library's global `.preview-stage` class. Its `display:grid;place-items:center` made the custom PDF component shrink to its intrinsic width. At390 the parent measured312px, host242px and canvas220px; at1440 the parent measured1044px, host254px and canvas220px. Changing only the host width to100% widened the desktop canvas to1008px. This ruled out the PDF bytes and established a layout collision. The initial command was `node scripts/check-storage-preview-layout.mjs`; its width assertion failed before the fix.
2. **Disabled buttons in an automatic archive.** The browser advertised writes based on manager status, then disabled them because the server supplied no writable library folder. Existing visit/duty archive protection is intentional. The write group now appears only for a server-resolved writable folder. Managers elsewhere receive a clear explanation and **Open school library**, resolved through the existing school context and ledger lookup. Historical years keep their read-only explanation without the write shortcut.
3. **Real upload into the previous folder, reproduced during live verification.** Clicking a new folder left the previous folder's enabled write controls visible while its destination loaded. An immediate file selection used the previous ledger folder ID. A real synthetic PDF reached the parent library rather than the newly created verification folder. A regression test failed with the old controls/handlers; loading now disables Upload/New folder and guards create, late file selection and upload retry. No backend or destination policy was relaxed. The exact first-run PDF hash was checked before removing only that generated test file through the system; the first test folder was also cleaned.

## Final behavior

- The native dialog uses its own `drive-document-stage` class. The shared PDF host explicitly fills available width, has a distinct toolbar class, derives available width from actual padding, and waits for a usable width when hidden behind the inspector.
- Compact metadata and a bounded viewer reserve space for the document. The pages scroll inside the viewer; the header and download/close actions remain visible. Phone preview occupies the available screen. Expand/Restore changes dialog size within the browser viewport; it does not invoke the operating system's fullscreen API.
- Zoom, Fit width, page count, authenticated download, retained file details/evidence/versions, Escape and focus return remain available. Fit width resets zoom. Expansion and returning from hidden details trigger width-aware rendering.
- Library writes use the existing endpoints and identity/retry rules. Signed archives remain generated-document destinations. Neither local PDF bytes nor archive settings, grants, school roots, signed snapshots or migration history were changed.

## Verification

| Check | Evidence / outcome |
|---|---|
| Focused Angular suite | **27/27 passed**, including the new loading-destination regression, archive controls, library resolution and historical-year guard; existing transfer/navigation/inspector/upload-retry/export checks retained. [Log](preview-layout-checks/angular.txt). The red destination run is retained separately in the checks folder. |
| Development build | Passed; no budget changes. [Log](preview-layout-checks/build.txt). |
| Rendered layout at390/768/1440, synthetic intercepted API | Fit-width page fills more than85% of the document area, meaningful vertical space, no scrolling of the dialog itself, zoom/reset, expand/restore, hidden inspector return, Escape/focus, archive-to-library navigation and enabled library tools. [Measurements](preview-layout-screens/results.json). |
| Existing UI workflows at390/768/1440, synthetic intercepted API | Actual rendering with create/upload/lost-response retry, same key/body/destination, details/evidence/version/change panels, whole map cards, legacy links, delegate menu and genuine network-error presentation. [Results](preview-layout-workflows/results.json). These are fixtures, not provider writes or an installed IDM extension. |
| Real authenticated reads and downloads at390/768/1440 | No intercepted API; real existing visit PDF, roster downloads, byte hashes, CORS transfer metadata and unauthenticated401. Final canvas widths342/657/1124px in document areas364/691/1158px. [Results](preview-layout-live/results.json). |
| Real create/upload/read-back/cleanup | Actual manager UI created one new verification folder in the current school library, uploaded a synthetic PDF into that exact ledger folder, previewed it and downloaded byte-identical content. Cleanup reviewed zero signed documents/evidence links, trashed only this run's new folder/file through system mutations, and verified disappearance from the parent listing. [Receipt](preview-layout-live/write-results.json), [uploaded file](preview-layout-live/real-library-upload.png). |

The two live write attempts left retained audit/operation records and trashed only their generated test objects. No original school files were targeted. The first attempt exposed the loading race; its generated parent-folder PDF and empty folder were both cleaned. The final attempt verified the intended folder and passed. No new school-duty acknowledgement, report submission, visit approval or external message was performed.

## Principal views

| View | Phone390 | Tablet768 | Desktop1440 |
|---|---|---|---|
| Real archived PDF | [Preview](preview-layout-live/file-preview-390.png) | [Preview](preview-layout-live/file-preview-768.png) | [Preview](preview-layout-live/file-preview-1440.png) |
| Library controls, real API | [Tools](preview-layout-live/library-tools-390.png) | [Tools](preview-layout-live/library-tools-768.png) | [Tools](preview-layout-live/library-tools-1440.png) |
| Preview with inspector, synthetic API | [Preview](preview-layout-screens/preview-390.png) | [Preview](preview-layout-screens/preview-768.png) | [Preview](preview-layout-screens/preview-1440.png) |
| Expanded viewer, synthetic API | [Expanded](preview-layout-screens/expanded-390.png) | [Expanded](preview-layout-screens/expanded-768.png) | [Expanded](preview-layout-screens/expanded-1440.png) |
| Archive guidance, synthetic API | [Guidance](preview-layout-screens/archive-390.png) | [Guidance](preview-layout-screens/archive-768.png) | [Guidance](preview-layout-screens/archive-1440.png) |

Principal phone/desktop captures were visually inspected. Headless viewport checks are not physical-device acceptance. The owner's preferred final appearance and installed desktop IDM behavior remain owner checks; this follow-up does not claim those gates passed. Historical C30 captures remain intact; C31 captures supersede their preview appearance.

## Reproduction and rollback

Run from `frontend`, with the local app/API running:

```powershell
node scripts/check-storage-preview-layout.mjs
$env:ALFALAH_STORAGE_BROWSER_EVIDENCE='../docs/specs/school-file-storage/verification/preview-layout-workflows'
node scripts/check-storage-download-ui.mjs
node scripts/check-storage-preview-live.mjs
```

The read-only live script uses the private existing school18 manager session. `check-storage-preview-write-live.mjs` is deliberately separate: it performs bounded real writes in that exact test school, retains a private recovery receipt and cleans only its own new objects after impact checks. It must not be repointed to an operational school. Never publish its private session, handles or recovery receipt.

Local frontend4200 and API5264 remain running. Backend/schema69 are unchanged. Rollback consists of reverting only C31 frontend presentation/loading guards and its Fit width translation; retain C30 binary transfer, scoped backend policies and existing history. No schema rollback or external folder reset is needed.
