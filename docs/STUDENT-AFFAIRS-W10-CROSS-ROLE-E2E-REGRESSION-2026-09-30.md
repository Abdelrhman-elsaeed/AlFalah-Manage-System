# W10 — Cross-Role End-to-End Verification, Regression Hardening & Release Readiness

Date: 2026-09-30

## Outcome

W10 is complete and passing. The seven Student Affairs roles were exercised through real HTTP, relational SQL Server LocalDB state, isolated browser sessions, role-to-role handoffs, deterministic server time, and the production Angular application. Scenarios A–G, the negative authorization matrix, desktop route isolation, and the required mobile RTL journeys all pass.

No business state machine was expanded, no production test endpoint was added, and no EF model change or migration was required. The implementation retains `Controller → MediatR handler/service → repository → database`; the product defects found by E2E were corrected in repositories, notification dispatch, and middleware rather than in controllers or Angular business logic.

## Baseline verified before W10

The baseline was executed before implementation:

- Release backend build: PASS, 0 warnings and 0 errors.
- Backend tests: 721 passed, 0 failed, 0 skipped.
- Frontend unit tests: 216 passed.
- Angular production build: PASS.
- EF model: no pending changes.
- Existing production-build warnings: two CSS budget warnings and three PrimeNG selector-parser warnings.
- Existing EF design-time warnings: required relationships whose principals have global query filters, plus the `Visit.ExperienceVersion` sentinel warning.
- No browser E2E runner existed.

The W1–W9 working tree and behavior were treated as baseline and preserved. The source-of-truth documents listed in the W10 brief were reviewed before implementation.

## Test architecture added

Playwright 1.63.0 was added as a frontend dev dependency with a single `npm run test:e2e` entry point. The runner performs the whole setup automatically:

1. creates a random runtime JWT signing secret and runtime-only fixture password;
2. writes the deterministic E2E clock value;
3. resolves the EF design-time connection with `ALFALAH_MIGRATIONS_CONNECTION`;
4. dry-runs the database drop and verifies the exact database name `AlFalahW10E2E`;
5. drops only that database, allowing normal migrations and idempotent E2E seeding to recreate it;
6. starts the Release API and Angular development host;
7. validates each seeded account's exact single role, required permissions, and active school before tests begin;
8. runs serial browser/API workflows with one worker so cross-role handoffs share deliberate state but never share browser storage.

The E2E environment fails closed unless the connection is local and the database name contains `E2E`. The E2E clock and fixture metadata use files private to the test process; there is no public clock/state controller. E2E-only services are registered only when `ASPNETCORE_ENVIRONMENT=E2E`. Production DI and external notification providers are unchanged. No email, SMS, push, or other external integration is invoked.

Playwright stores access and refresh tokens only in gitignored `test-results/.auth` files with restricted file mode, then injects them into a fresh browser context's `sessionStorage`. Sessions, local storage, and cookies are not shared between roles. Screenshots and traces are retained only on failure; Playwright output and HTML reports are gitignored.

## Database, fixtures, time, and timetable

The database is SQL Server LocalDB `AlFalahW10E2E`, independent from Development and Production. The E2E seeder is idempotent and adds only isolation/negative fixtures around the existing Student Affairs seed:

- primary active school and second active cross-school isolation school;
- a same-school student with no currently valid guardian relationship;
- a second same-school Social Worker for assignment denial;
- a cross-school Officer and cross-school student;
- a dedicated Instructor whose role is changed during the session-invalidation test;
- inactive user, inactive school, inactive Guardian profile, and expired Guardian relationship fixtures.

The seeded login names are fixture identifiers, not secrets: `admin.test`, `officer.test`, `socialworker.test`, `socialworker.other.test`, `secretary.test`, `guard.test`, `teacher.test`, `substitute.teacher.test`, `parent.test`, `cross.officer.test`, `matrix.instructor.test`, `inactive.profile.guardian.test`, and `inactive.relationship.guardian.test`. Passwords, JWTs, and refresh tokens are not stored in this report or test output.

Business time comes from `E2EFileTimeProvider`, not the browser or machine clock. Clock changes use atomic file replacement and the provider tolerates the short Windows rename/read overlap. The workflows use the seeded school timezone, one published timetable and its pinned Bell Schedule revision, exact lesson boundaries, the original/substitute instructor pair, and server-owned timestamps.

## Scenario matrix

| Scenario | Preconditions and actors | Executed handoff and evidence | Result |
|---|---|---|---|
| A — Attendance and excuse | Published class roster; Secretary, Guardian, Officer, cross-school Officer | Secretary submitted only absent IDs; server persisted the complete two-student sheet; Guardian saw only the linked student, uploaded a PDF idempotently, and could not use the currently unlinked student; Officer alone accepted; metric recalculated; replay preserved `AbsentExcused`; notification/outbox did not duplicate; browser confirmed Guardian and Officer surfaces | PASS |
| B — Behavior decision | Current lesson and substitute fixture; Instructor, substitute Instructor, Officer, Guardian | Current effective Instructor recorded incidents; original Instructor was denied during substitution and wrong-roster student was denied; ten-incidence threshold remained exactly-once; Officer approved one notification and suppressed another; Guardian saw only approved data; referral trigger remained idempotent; browser confirmed the handoff | PASS |
| C — Gate Pass | Active Guardian link and substituted current lesson; Guardian, Officer, original/substitute Instructors, Security, Manager | Guardian requested; Officer approved; original Instructor was denied; substitute acknowledged; stale Security exit returned 409; fresh version executed one exit; Manager audit exposed the result without broad case data; Guardian saw final state | PASS |
| D — Entry permit | Current lesson roster; Officer, original/substitute Instructors, Guardian | Officer issued; wrong substitute was denied; current original Instructor acknowledged; stale replay returned 409 without partial transition; Guardian saw the acknowledged permit | PASS |
| E — Referral and summon | Open referral plus two same-school Social Workers; Officer, assigned/unassigned Social Workers, Guardian | Officer assigned; unassigned worker was denied; assigned worker accepted; summon creation was idempotent; scheduling produced one outbox notification; attend → observe → improved history was preserved while referral remained `InProgress`; browser confirmed role surfaces | PASS |
| F — threshold and correction | Five distinct unexcused dates; Secretary, Guardian, Officer | Attendance submissions and replays were idempotent; a new correlated 5/5 referral and summon appeared once; accepted PDF excuse lowered metric 5 → 4; history remained; summon became `RequiresOfficerReview`; Officer retained it; stale concurrent review returned 409; no second referral appeared | PASS |
| G — office hours and republish | Published timetable and eligible free slots; Instructor, Guardian, SchoolManager | Instructor selected two eligible slots; outside-hours Guardian message queued; Manager built/published a new timetable with one conflict; reconciliation produced one conflicted and one valid slot; next delivery was recalculated; stale office-hours version returned 409; messaging audit serialized metadata only; browser confirmed critical surfaces | PASS |

Scenario F initially exposed a test-correlation race: an older Scenario E summon also carried a 5/5 snapshot, so the poll could accept it before the new outbox transaction finished. The test now captures pre-existing summon IDs and waits for a newly created correlated summon. This is deterministic and does not weaken the business assertion.

## Security and negative matrix

| Case | Evidence | Result |
|---|---|---|
| Correct role + permission | Original Officer token reached the pending-excuse queue | PASS |
| Correct role without permission | A correctly signed E2E Officer token with permission claims removed received 403 | PASS |
| Wrong role with injected permissions | A correctly signed Guardian token injected with both attendance permissions passed the coarse controller check but the handler's exact-role check returned 403 | PASS |
| Same-school object | All positive A–G operations used the active primary school | PASS |
| Cross-school object | Cross-school Officer could not read/mutate primary-school workflow objects | PASS |
| Unlinked object | Guardian access to the student with no currently valid relationship was denied | PASS |
| Inactive user | School login returned 401 with a generic credential response | PASS |
| Inactive school | Correct account and password against the inactive school returned 401 | PASS |
| Inactive profile | Guardian student lookup returned scoped 404 with no data | PASS |
| Inactive relationship | Expired relationship produced an empty linked-student projection | PASS |
| Stale access after role change | Production `PUT /api/v1/users/{id}` changed a dedicated Instructor to Moderator; its old access token immediately returned 401 | PASS |
| Revoked refresh | Role change revoked the old refresh token; explicit logout also made the fresh refresh token return 401 | PASS |
| New login claims | Fresh login returned exactly `Moderator`, included Moderator permissions, and excluded `TeacherQuickAction.View` | PASS |
| Route guard and shell | Seven canonical roles reached only their own landing; Guardian direct navigation to Officer workspace was denied on desktop and mobile | PASS |
| API denial behind hidden UI | Permission-only and exact-role handler denials were called directly over HTTP | PASS |

JWT mutation is possible only inside E2E because the runner generates and passes the ephemeral signing key to both API and Playwright. No production key is stored or reused.

## Privacy evidence

- Guardian projections contain only currently linked students; the unlinked and expired-link students do not leak.
- Security receives only the gate-pass execution projection, not attendance, behavior, referral, case, or message data.
- the unassigned Social Worker cannot read or mutate another worker's referral/case.
- SchoolManager oversight and messaging audit assertions serialize the response and reject student/guardian/case identity, subject, body, evidence, attachment, and content-navigation fields.
- Officer projections used by these workflows do not expose confidential Social Worker notes.
- traces are failure-only and gitignored; runtime output contains no password, JWT, refresh token, or uploaded PDF bytes.

## Concurrency, idempotency, uncertainty, and outbox evidence

- stale Gate Pass execution, entry-permit acknowledgement, automation review, and office-hours configuration requests return 409;
- accepted transitions remain single and no partial state is left after a stale request;
- attendance sheets and absence-excuse submissions replay the original result under the same idempotency key;
- behavior, referral, summon, attendance-threshold, and notification/outbox assertions verify no duplicate aggregate or side effect;
- queued messaging is read-reconciled after timetable publication and not blindly remutated;
- Scenario F correlates the new outbox result by identity rather than accepting a pre-existing equal snapshot;
- dialogs/routes use state/response waits, not fixed sleeps, and browser contexts remain isolated.

## Product defects found and corrected

1. **Current-roster EF translation failure.** `ResolveCurrentRosterScopeAsync` used a correlated `SelectMany` query SQL Server could not translate, producing a real 500 in teacher quick actions. The repository now resolves the scoped instructor and enrollment through explicit translatable projections.
2. **Summon notification actor violated the user FK.** The dispatcher wrote a synthetic `system:...` actor into an audit column backed by the Users FK. It now preserves the real actor carried by the domain event. A backend regression test protects the constraint-backed behavior.
3. **Period office-hour slot violated its check constraint.** Period-derived slots were persisted with both period and clock range. They now persist `Period` with a null clock range and reconcile using the period representation.
4. **Office-hours replacement hit the filtered unique index.** EF could insert the replacement before deactivating the current row. The repository now saves the deactivation first inside the existing transaction, then inserts the replacement.
5. **Client disconnect was reported as application 500.** A request-aborted `OperationCanceledException` now maps to 499 and debug logging. Middleware regression coverage was added.
6. **Windows clock-file race.** A read could overlap Playwright's atomic rename. The E2E-only provider now opens with read/write/delete sharing and performs short bounded retries.
7. **Scenario F false correlation.** The test could accept an older summon with the same threshold snapshot. It now proves a newly created summon ID before asserting referral counts.

No controller gained business logic or direct DbContext access. No entity is returned raw by the new browser helpers.

## Files added or materially changed

Principal additions:

- `backend/AlFalah.Api/Testing/E2EFileTimeProvider.cs`
- `backend/AlFalah.Api/Testing/E2EIsolationDataSeeder.cs`
- `backend/AlFalah.Api/appsettings.E2E.json`
- `frontend/playwright.config.ts`
- `frontend/scripts/run-student-affairs-e2e.mjs`
- `frontend/e2e/global-setup.ts`
- `frontend/e2e/support/api-session.ts`
- `frontend/e2e/support/role-session.ts`
- `frontend/e2e/student-affairs/cross-role-workflows.spec.ts`
- `frontend/e2e/student-affairs/role-isolation.spec.ts`
- `frontend/e2e/student-affairs/security-matrix.spec.ts`

Material hardening changes:

- `backend/AlFalah.Api/Program.cs`
- `backend/AlFalah.Api/Middlewares/GlobalExceptionMiddleware.cs`
- `backend/AlFalah.Infrastructure/Data/Seeders/StudentAffairsDataSeeder.cs` (uses the runner-provided random password in E2E while retaining the existing deterministic Development fallback)
- `backend/AlFalah.Infrastructure/Repositories/TeacherActionWorkflowRepository.cs`
- `backend/AlFalah.Infrastructure/Repositories/MessagingWorkflowRepository.cs`
- `backend/AlFalah.Infrastructure/Notifications/StudentAffairsNotificationDispatcher.cs`
- backend regression tests, frontend package files, and `.gitignore`.

## Final verification

The final commands were executed, not merely documented:

- `dotnet build backend/AlFalah.slnx -c Release`: PASS, 0 warnings, 0 errors.
- `dotnet test backend/AlFalah.slnx -c Release --no-build`: PASS, 723/723, 0 failed, 0 skipped.
- `npm test -- --watch=false --browsers=ChromeHeadless`: PASS, 216/216.
- `npm run build -- --configuration production`: PASS.
- `npm run test:e2e`: PASS, 15/15 in approximately 2.4 minutes (13 desktop executions and 2 mobile executions), including a clean-database run with a newly generated fixture password.
- `dotnet ef migrations has-pending-model-changes ... --no-build`: PASS, `No changes have been made to the model since the last migration.`
- `git diff --check`: PASS; only Git's LF→CRLF working-copy notices were printed.

The production build retains the same two CSS budget warnings and three PrimeNG selector-parser warnings from baseline. The EF command retains the existing global-query-filter relationship warnings and `Visit.ExperienceVersion` sentinel warning. The E2E host also reports the known intermediate timetable seed skip because its unrelated sample school is intentionally absent, and may log a LocalDB connection message during Playwright server teardown; neither affected a request assertion or the passing exit code. Expected 401 negative tests are currently logged by the existing global exception middleware at error level, but their messages are generic and contain no credentials or tokens.

## Migration decision

No migration was created. W10 changes test infrastructure and correct repository/middleware behavior against the existing schema. The model snapshot is unchanged and the pending-model check is clean.

## Safety incident and recovery

During initial runner verification, an EF database-drop command was invoked before the design-time factory override was wired correctly. The factory ignored the normal runtime connection override and dropped the local scratch database `AlFalahMigrations`, not a Development or Production database. Execution was stopped immediately. The runner was corrected to set `ALFALAH_MIGRATIONS_CONNECTION`, dry-run the resolved target, require the exact `AlFalahW10E2E` name, and only then use `--force`.

The `AlFalahMigrations` schema was recreated with `dotnet ef database update`. Any prior scratch data in that database was not recoverable. This incident is disclosed here because it materially affected local data even though it did not affect source files or the final isolated E2E database.

## Business ambiguities and release decision

No unresolved business ambiguity was encountered and no new rule was invented. W10 is PASS for the specified A–G and release-readiness scope.

No commit or push was performed.

