# C30 / D-122 — Download transport and one school file browser

Owner follow-up,2026-10-15: fix the error shown while a file downloads, restore administrator tools/preview/details, make map cards clickable, remove the administrator's personal-files entry, and eliminate the second school-library browsing interface. These instructions extend the earlier D3/storage work; they do not authorize new signing, submission or archive obligations.

## Diagnosis and final behavior

| Observed problem | Cause and correction |
|---|---|
| A PDF downloads through IDM while the application reports connection/unavailable | Application Blob requests were advertised as ordinary PDF attachments (`Content-Type: application/pdf` plus attachment filename). The supplied screenshots show IDM taking that exact request; the awaiting HttpClient path treats the interrupted request as status0. An authenticated live header probe reproduced that contract and failed the application-transfer assertion. Application transport is now separate from the final browser download. Genuine HTTP/network failures remain errors. |
| Upload/new folder disappeared after native school-root browsing | The native browser was not connected to the existing library upload/create services. Scoped DTOs now expose an eligible registered library folder; controls reuse `StorageApiService` and `StorageLibraryService`, including reserved operations, validation and the same upload retry key. No second ledger or upload system was created. |
| Map folders opened a visibly different old library page | `StorageFilesEntryComponent.library` selected the complete older `StoragePageComponent` for `folder`, `file`, year or search query parameters. That fallback/import/render path is removed from **School files**. Registered folder/file links resolve through the server into the same native browser; details/history/evidence appear in its own inspector. |
| School administrator sees Personal files and is denied | The menu trusted a generic own-storage permission, while the URL sat beneath the Instructor role guard and the personal API requires an actual teacher grant. Live school management now selects school navigation and hides Personal files. The personal URL is outside the Instructor role subtree and redirects a live storage administrator to School files; ordinary teacher authorization stays separate. |
| Only the small Open link on map cards navigated | The actual anchor now covers each available root/domain/standard card. No visible Open label remains. Independent collapse buttons retain their action and keyboard focus. |

The diagnosis tested the attachment-takeover hypothesis ahead of extra-request/permission and CORS/connectivity hypotheses. Actual authenticated downloads succeeded with the new transport, no follow-up failure or mutation request, correct CORS metadata and matching bytes. A synthetic browser replay aborts attachment-style requests while permitting negotiated transfers and also checks that an actual aborted request still shows an error. **An installed IDM extension was not present in the automated browser.** The screenshot/old header contract and deterministic policy replay explain the code defect; owner desktop verification with that extension remains pending. IDM's documented [automatic takeover by file type/extension](https://www.internetdownloadmanager.com/support/starting.html) supports the interpretation; no promise is made that application code can control arbitrary extension settings.

## One interface, existing services

`StorageFilesEntryComponent` now hosts only the native school browser or visual map. Old numeric `folder`/`file` links use GET `/api/v1/storage/drive/resolve?folderId=&fileId=`. The Application service requires live school access, uses a scoped repository lookup of the existing library record/current version, verifies live provider-root containment and parent membership, and issues existing actor/root/school-bound handles. Unknown, cross-school, mismatched or moved records are not silently adopted. The controller remains a thin response adapter.

`StorageFileInspectorComponent` presents versions, evidence links and file changes inside the native browser's dialog. It reuses existing details, version-content, rename and `EvidenceWorkspaceComponent` services/policies. It is not a second file browser. Switching preview/details does not navigate away or recreate evidence forms. Permission denial clears retained details; historical-year contexts remain read only. An unsuccessful200 business envelope does not become rename success. Images/PDFs preview; other formats show metadata and an explicit download action rather than downloading on the initial click.

Uploads/new folders are available in eligible registered school-library folders; automatic archive branches remain protected. Browser-supplied IDs cannot bypass the existing server upload/folder authorization and root checks. Native rename/move/confirmed deletion/recovery keep the prior C27 policy. Teacher private storage and the separate evidence-review workspace retain their existing policies; neither is embedded as an alternative **School files** browser. School Duty delegation alone still grants no storage administration.

## Binary HTTP contract

- API Blob requests negotiate `Accept: application/vnd.alfalah.transfer`. The MVC result filter preserves the original binary bytes or stream, range metadata and authorization. It emits the application media type, no attachment filename, `private, no-store` and `nosniff`; original media type/encoded filename travel in `X-AlFalah-Content-Type` / `X-AlFalah-File-Name`.
- The shared client interceptor restores Blob type and local response filename metadata after receipt. The existing final browser download uses that Blob. There is no JSON/base64 expansion, public provider URL or token-bearing download link.
- The application roster request uses extension-free `/api/v1/school-duty/plans/roster/content`. Existing direct `plans/roster.pdf` remains available with its attachment contract. Ordinary direct downloads, external requests, JSON responses and real error envelopes are unchanged.
- CORS exposes only the needed response metadata, including the existing document hash header. The native storage/roster server/download hashes match. No status0 exception is suppressed or treated as proof that a download completed elsewhere.

## Actual verification

| Check | Result / boundary |
|---|---|
| Backend | **15/15**:12 storage-browser checks using real fresh isolated SQL with provider doubles, plus3 transfer-filter checks. Includes existing protection/deletion/SQL rollback/race/read-integrity tests, current record mapping, cross-school/mismatched resolver denial and immutable byte/stream preservation. [TRX](download-access-checks/storage-download.trx), [log](download-access-checks/backend-tests.txt). Existing EF1002 warnings concern the pre-existing isolated fault-injection SQL; not new product queries. |
| Focused Angular | **23/23**: negotiated metadata/bytes, real network error preservation, own/admin navigation, same-operation upload retry, read-only archives/year context, old folder/file links into the native browser, inspector denial clearing and false200 rename rejection. [Log](download-access-checks/angular-focused.txt). |
| Additional shell checks | **32/33** in an earlier overlapping run. The existing People navigation assertion expects6 routes while the unchanged current navigation has10, including Student Affairs/Social Work/Security/Guardians. This unrelated assertion was not rewritten. [Retained log](download-access-checks/angular-tests.txt). Existing broad storage/SQLite fixtures and production CSS-budget limitations remain unresolved; no budget was relaxed. |
| Build | Debug API built with zero warnings/errors. Final Development frontend build passed. [API build](download-access-checks/backend-build-final.txt), [frontend build](download-access-checks/frontend-build-final.txt). No production deployment. |
| Real browser / Drive reads | Actual local authenticated API and real existing synthetic-school Drive file, **390/768/1440**, no intercepted APIs: six native downloads (storage+roster), PDF.js preview/metadata, legacy folder links using only the native browser, enabled library tools, canceled folder dialog/focus return, administrator personal-bookmark redirect and no personal tab. HTTP401 still denies unauthenticated reads; negotiated response has no attachment header and exposes needed CORS metadata. [Results](download-access-live/results.json). No verification write to SQL business records or Drive. |
| Synthetic Angular workflows | **390/768/1440**: complete-card links/collapse, keyboard Enter on a standard card, native browsing for legacy folder/file links with zero old school-browser components, metadata/versions/evidence/change panels, create folder through the existing endpoint, lost upload response with identical retry key/body/destination, and visible genuine network failure. Instructor-role storage delegate receives school controls and no personal tab. These APIs are intercepted fixtures, not real provider delivery. [Results](download-access-screens/results.json). |

Principal views were visually reviewed; a first phone toolbar wrapped Upload over three lines and was refined into two full-width action buttons above search/history/refresh. The older full-page inspector screenshot is superseded by the native dialog captures below. Viewport/headless keyboard checks are not physical-device or OS-keyboard acceptance.

| View | Phone | Tablet | Desktop |
|---|---|---|---|
| Real library controls / same browser for numeric folder link | [390](download-access-live/library-tools-390.png) | [768](download-access-live/library-tools-768.png) | [1440](download-access-live/library-tools-1440.png) |
| Real authenticated archived PDF preview | [390](download-access-live/file-preview-390.png) | [768](download-access-live/file-preview-768.png) | [1440](download-access-live/file-preview-1440.png) |
| Native dialog / retained versions (synthetic API) | [390](download-access-screens/existing-ledger-details-390.png) | [768](download-access-screens/existing-ledger-details-768.png) | [1440](download-access-screens/existing-ledger-details-1440.png) |
| Clickable visual map (synthetic API) | [390](download-access-screens/clickable-map-390.png) | [768](download-access-screens/clickable-map-768.png) | [1440](download-access-screens/clickable-map-1440.png) |
| Same native folder screen after map navigation | [390](download-access-screens/unified-folder-390.png) | [768](download-access-screens/unified-folder-768.png) | [1440](download-access-screens/unified-folder-1440.png) |
| Real network failure replay remains visible | [390](download-access-screens/real-network-error-390.png) | [768](download-access-screens/real-network-error-768.png) | [1440](download-access-screens/real-network-error-1440.png) |

## Reproduce, runtime and rollback

```powershell
# Use only a new synthetic test catalog. The fixture rejects an existing catalog.
$env:ALFALAH_DUTY_TEST_CONNECTION='Server=(localdb)\MSSQLLocalDB;Database=AlFalahDutyTests_StorageC30_'+[guid]::NewGuid().ToString('N')+';Integrated Security=true;TrustServerCertificate=true'
dotnet test backend/AlFalah.Tests --configuration Debug --filter 'FullyQualifiedName~StorageDriveBrowserSqlTests|FullyQualifiedName~BrowserTransferFilterTests'
Set-Location frontend
npm.cmd test -- --watch=false --browsers=ChromeHeadless --ts-config=tsconfig.storage-followup-spec.json --include='src/app/core/interceptors/browser-transfer.interceptor.spec.ts' --include='src/app/features/storage/storage-navigation.spec.ts' --include='src/app/features/storage/storage-drive-browser.component.spec.ts' --include='src/app/features/storage/storage-file-inspector.component.spec.ts' --include='src/app/features/storage/storage-api.service.spec.ts' --include='src/app/features/timetables/duty/duty-roster-export.component.spec.ts'
node scripts/check-storage-download-ui.mjs
node scripts/check-storage-download-live.mjs
npm.cmd run build -- --configuration development
```

The live script requires a private current authorized synthetic-school18 session retained under ignored `.audit/roster-source-20261015/session-private.json`, an existing archived PDF and the configured authorized provider root. It makes bounded reads and cancels the create dialog; do not replace its school/destination or turn it into an upload/reset script. No session, credentials, provider handles, private source photograph or real personal data are included in the public receipts. Synthetic UI fixtures are explicitly labeled.

Angular4200 and the current Debug API5264 are running. Startup initialization remains false; the owner's existing duty/visit processor and school activation choices are retained. **C30 adds no migration**; local schema remains69 from C29. No credentials, keys, grants, roots, signed artifacts or external-school settings were changed. Reads/retries do not submit/sign reports, confirm lateness or send notices.

Rollback is an application-only change: restore the matching frontend/backend version while retaining SQL69 and every file/version/snapshot/operation. The direct legacy PDF routes remain supported. Do not remove artifact history, run Down, reset school data or change archive enablement as a UI rollback. Owner final visual review, physical-device/assistive-technology checks and the actual desktop IDM integration remain pending.
