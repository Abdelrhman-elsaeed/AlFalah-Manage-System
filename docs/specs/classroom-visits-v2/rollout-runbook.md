# Classroom Visits V2 cutover, readiness, and rollback runbook

**Implementation state (2026-09-22):** V2 is the default globally in backend and frontend configuration. `/visits` is V2, `/visits-v2` is a compatibility alias, and `/visits-legacy` is the read-only historical surface. All V1 visit write endpoints return `410 Gone`.

Physical retirement is explicitly out of scope. No V1 row, table, column, foreign key, enum value, or migration may be deleted by this runbook.

## Mandatory pre-deployment gate

1. Put the application in a controlled deployment window and take a full SQL Server backup with checksum.
2. Record the backup identifier, target database, UTC timestamp, operator, and restore destination in the release ticket.
3. Restore that backup to an isolated production-like database and run `DBCC CHECKDB` plus an authenticated V1 historical report read and a V2 report read. A backup that has not been restored is not considered verified.
4. Run the pending-V1 query below against the target database. Cutover is blocked unless it returns zero rows, or every returned group has a named owner and a written disposition approved in the release ticket. Never auto-delete or auto-transition these rows.
5. Confirm the additive V2 migration and rubric seed are already reviewed/applied through the normal deployment process. Do not apply production migrations from an interactive Codex session.
6. Run all verification commands and the role-based browser matrix below.

```sql
SELECT Status, COUNT_BIG(*) AS RecordCount
FROM dbo.Visits
WHERE ExperienceVersion = 1
  AND IsDeleted = 0
  AND Status IN (1, 2, 3, 5, 6, 7)
GROUP BY Status
ORDER BY Status;
```

Status mapping: `1 Draft`, `2 Submitted`, `3 PendingApproval`, `5 RejectedForChanges`, `6 Reopened`, `7 UnderReviewAfterComplaint`.

### Development readiness snapshot

Read-only query executed on 2026-09-22:

| Status | Count |
|---|---:|
| Draft | 2 |
| PendingApproval | 8 |
| Approved | 6 |
| Total legacy, non-deleted | 16 |
| Non-final legacy blocker total | **10** |

The development database is therefore **not cutover-ready without a documented decision for the 10 non-final V1 records**. No status or data was changed by the check.

## Deployment and routing contract

- Backend production: `FeatureFlags:VisitsV2=true`, `VisitsV2SchoolIds=[]`.
- Frontend production: `featureFlags.visitsV2=true`.
- Deploy API and frontend together.
- `/api/v2/visits` owns all new create/update/finalize/approve/reject/reopen/delete operations.
- `/api/v1/visits` retains only historical reads; every write is logged and returns a safe `410 Gone` response.
- V2 reports, CSV, PDF, ZIP, report-view logging, complaints, teacher history, and dashboards must stay version-aware and school-scoped.

## Smoke and acceptance matrix

Exercise SchoolManager, Moderator, and Instructor accounts at desktop (~1440 px) and mobile (~390 px): create/save/finalize; reject and resubmit; approve; normal and complaint reopen; teacher history; instructor report and view log; PDF/CSV/ZIP; V1 historical read; rejected V1 mutation; Arabic/English and RTL. Attach screenshots and request/response evidence to the release ticket.

## Monitoring

- Separate `/api/v2/visits` 4xx/5xx and latency from `/api/v1/visits` 410 counts.
- Alert on cross-school authorization failures, PDF/ZIP failures, and transaction failures.
- Verify report-view logs and audit rows for approve/reject/reopen.
- Exit the monitoring window only with no unresolved severity-1/2 issue and a proven restore.

## Rollback

The cutover makes V1 writes permanently read-only in this release, so toggling the flag off alone is not an operational rollback: `/visits` remains the V2 route. Roll back by redeploying the last approved pre-cutover API/frontend pair, then set its flags according to that release's runbook. Do not reverse the additive migration and do not delete V2 records. If rubric activation must be reverted, use the reviewed `reactivate-legacy-rubric.sql` script only after database-owner approval.

## Verification commands

```powershell
dotnet build backend/AlFalah.slnx -c Release
dotnet test backend/AlFalah.slnx -c Release --no-build
npm --prefix frontend test -- --watch=false --browsers=ChromeHeadless
npm --prefix frontend run build -- --configuration production
```

Phase 9 physical cleanup remains a separate, explicitly approved release.
