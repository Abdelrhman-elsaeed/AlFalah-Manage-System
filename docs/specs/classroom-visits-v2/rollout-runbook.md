# Classroom Visits V2 rollout and rollback runbook

**Implementation state:** release-ready behind flags. The rollout remains OFF by default until a named pilot school and client acceptance window are supplied.

**Local manual-QA state (2026-09-21):** `appsettings.Development.json` and the Angular development environment have V2 enabled globally. Production configuration remains OFF.

## Pre-deployment checks

1. Back up the target database and record the restore point.
2. Apply migration `20260920233213_ClassroomVisitsV2AdditiveSchema`.
3. Start the API once with V2 disabled. Confirm rubric version 2 exists with 5 domains, 25 standards, and 66 indicators and remains inactive.
4. Confirm `FeatureFlags:VisitsV2=false` and `FeatureFlags:VisitsV2SchoolIds=[]`.
5. Run the backend and frontend verification commands listed at the end of this document.

## Pilot by school

Keep the global flag off and add only the approved school IDs:

```json
"FeatureFlags": {
  "VisitsV2": false,
  "VisitsV2SchoolIds": [123]
}
```

The navigation asks `/api/v2/visits/availability`; enabled schools are routed to `/visits-v2`, while every other school continues using `/visits`. The V2 service resolves rubric version 2 directly, so the shared active V1 rubric is not changed during a school pilot.

Pilot acceptance must cover SchoolManager, Moderator, and Instructor accounts; create/save/finalize; approval/reject/reopen; indicator and live-score parity; teacher history; report-view logs; complaints; CSV; branded/signature PDF; archive scope; dashboard; desktop/mobile; and Arabic/English.

## Global cutover

After written acceptance:

1. Execute [`activate-rubric-v2.sql`](activate-rubric-v2.sql) against the backed-up target database.
2. Set `FeatureFlags:VisitsV2=true` and clear `VisitsV2SchoolIds`.
3. Deploy API and frontend together.
4. Keep `/api/v1/visits` and `/visits` operational during the monitoring window. Do not drop or rewrite legacy data.

## Fast rollback

1. Set `FeatureFlags:VisitsV2=false` and clear `VisitsV2SchoolIds`; redeploy/restart configuration.
2. Confirm `/api/v2/visits/availability` returns `isEnabled:false` and the shell routes users to `/visits`.
3. Do not reverse the additive migration during an incident. V2 records remain preserved for diagnosis.
4. If the rubric active marker must also be reverted after global cutover, execute [`reactivate-legacy-rubric.sql`](reactivate-legacy-rubric.sql). This changes active markers only and does not delete V2 records.

## Monitoring

- Watch 4xx/5xx rates and latency separately for `/api/v2/visits`.
- Alert on cross-school authorization failures, PDF failures, and finalize transaction failures.
- Compare browser live totals with persisted `VisitAnalysis.TotalScore`, `MaximumScore`, and `OverallPercentage` for the five golden fixtures.
- Exit only after the agreed window has no unresolved severity-1/2 defects and backup restoration is confirmed.

## Verification commands

```powershell
dotnet build backend/AlFalah.slnx -c Release
dotnet test backend/AlFalah.slnx -c Release
Set-Location frontend
npm run build
npm test -- --watch=false --browsers=ChromeHeadless
```

Physical retirement is not part of this release. Legacy route removal, write shutdown, archival, or schema drops require the separate Phase 9 approval defined in the plan.
