# Phase 7 — Timetable review, quality and repair

Implemented at `/intelligent-timetable/review`. The selector includes saved drafts and published timetables in the active school.

## Architecture and rules

- `TimetableReviewController` sends MediatR requests to Application handlers and `TimetableReviewService`. The Application validation and repair engines are stateless; `TimetableReviewRepository` owns EF and transactions.
- The ten hard checks cover teacher/classroom/room conflicts, real-time overlaps, availability, study periods and breaks, assignments, quotas and maximum teacher load, paired blocks, allowed days and fixed slots. Invalid, inactive and foreign references are blocking assignment findings.
- Six soft checks use the agreed thresholds: more than three consecutive lessons, two last periods per week, two idle periods per day, one individual subject lesson per day, early preferences and unbalanced early/late placement. Declared breaks interrupt teaching runs. Paired lessons require temporal adjacency. Cluster warnings require a feasible alternative.
- Co-teaching counts once toward class requirements and individually toward each teacher's allocation. Split quotas and paired ownership are checked independently.
- Quality is expressed only as error and warning counts. Soft warnings do not prevent publication. Management can record a reason for accepting a warning; hard errors have no override path.
- The existing publication endpoint now uses the Application review gate. Saving an edited published timetable returns it to draft and requires publication again.

## Review and repairs

Runs preserve timetable, setup and timing revisions. The analyzer version includes an input fingerprint, so an old analysis cannot authorize a changed timetable or changed constraints. Unchanged evaluations reuse the current run, preserving its override metadata. Original finding evidence is immutable; only soft-override metadata can be updated.

Repairs present up to three ranked, simulated alternatives. Available strategies include moves, swaps and exchanges between teachers already assigned to a split quota. Simulation must remove the selected conflict and introduce no new hard finding. The bounded search can return fewer proposals, or none; changing configuration then remains an explicit user action. Adjacent paired runs and co-teacher occurrences move together.

Application re-generates the proposal before accepting it and rejects stale revisions or modified movements. Movement persistence, snapshot, audit and fresh analysis share a serializable SQL transaction. Swaps temporarily exclude affected rows from filtered unique indexes inside that transaction.

The RTL PrimeNG dashboard includes severity cards, filters, the findings table, reason and repair dialogs, the publication bar, class/teacher previews, day navigation, density settings, break segments, unavailable slots and subject highlighting.

## Persistence and verification

The workspace already contained `20260909143029_AddTimetableReview`; its missing domain and configuration files were restored. EF reports no pending schema changes. `database update AddTimetableReview` confirmed the migration is applied to the configured development database, `AlFalahDb`. No production migration was run. The obsolete Infrastructure engine placeholders were already absent.

Verification includes the Phase 7 rule and workflow tests, the complete timetable test group, backend and frontend builds, and a Playwright workflow against the local SQL-backed API. The browser workflow covers publication blocking, hard-override rejection, ranked previews, explicit repair application, management-only soft overrides, version creation, publication and reload. Local QA evidence and screenshots are under `.audit/review-*`; the dedicated QA timetable is ID 3 in the existing E2E test school.
