# Connected Google versus library status — 2026-10-05

The owner's screenshot shows a saved Google credential and selected/enabled school folder. Neither requires re-linking. The running API separately returns `connectionState=Disabled` for the actual school-18 manager. Original storage flags remain OFF and its storage schema has not been migrated; the operational file-upload/link workflow cannot run there yet.

The settings page incorrectly described an enabled connection as “school files enabled,” and provided no onward library link. The regression command reproduced that exact misleading sentence before the fix:

```powershell
# frontend working directory; mocked API, Angular only
npx playwright test --config playwright.storage.config.ts drive-settings.spec.ts --grep 'linked Google folder' --project desktop
```

It failed at `p.status` containing `ملفات المدرسة مفعّلة` despite Disabled storage context. The corrected settings now describe Google/folder connection only, read the existing authorized storage context, explain the actual Disabled/initialization/unavailable state and link `/school-manager/storage`. Guidance distinguishes OAuth client JSON from evidence bytes. Once authorized library activation is complete, the normal flow is upload → open file details → select year and requirement → link Draft evidence → submit for independent review. Existing Drive-only/reference rows must not be blindly adopted as evidence; verified import and normal library rules remain.

Validation: the full Drive settings browser suite passes **14/14**, desktop/mobile, including the added regression and existing consent/draft/folder/search/revocation/save-failure behaviors. Production frontend build passes with the existing CSS budget/PrimeNG selector warnings. Actual browser against existing Angular 4200 and API 5264 verifies the real manager sees connected Google plus disabled library, the onward link works, and no POST/PUT/PATCH/DELETE API requests occur. Safe results are in [drive-library-guidance-checks.json](drive-library-guidance-checks.json); authentication material/screenshots remain ignored locally.

No backend behavior, new endpoint, migration, Google credential/key/root, storage flag or operational cutover changed. Current authorization scope guards late context responses. Deployment/activation remains governed by the [S6 operating plan](../s6-rollout-runbook.md). D-101 is a follow-up usability correction within S6.
