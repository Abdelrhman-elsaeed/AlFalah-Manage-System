# N2 implementation record

Date: 2026-10-17. Implementation and local verification complete; actual-device acceptance remains pending. N3 is not started.

## Design and contract decisions (T012A)

Use an additive `PersonalNotification` projection, with one independent read flag per recipient/canonical source. A released message is one entry; a legacy message notification reuses `message:{id}`. Duty dismissal surfaces reuse their persisted correlation. Other workflow transitions use source identity and meaningful revision. Attendance uses `students:{classroom}:{date}` or `staff:{date}`: corrections refresh the existing entry without resetting read or producing another completion banner. A Duty report uses its draft identity; corrected signed submissions refresh the same entry. Source state is always read live.

Source writes, projection, recipient journal and a snapshot of currently enrolled push devices share the source transaction. No historical conversion, dashboard-read producer or generic N1 invalidation is a push source. Disabling push closes new events without creating a catch-up backlog. Existing source notification/message counts retain their meanings.

Routes: `/api/v1/notification-center` (GET, bounded protected cursor, category/unread), `/state` (GET, independent unread count), `/{id}/read` and `/read-all` (POST, owned explicit intent), `/{id}/target` (GET, authenticated allowlisted route and exact selector). Targets do not mutate domain state. An unavailable target returns a neutral result. Current active school, membership, database grants, guardian links, feature-specific roles and assignments apply equally to list/count/target/push.

CP-01: implement complaint submission for existing entitled SchoolManager only. Preserve MainManager/Moderator hard denials, including mixed roles except SuperAdmin support. Do not enroll global support automatically or introduce complaint delegation. New administrator classes remain blocked pending owner policy. CP-04: manager approval means submitted visit evaluation with `Visit.Approve`; dispatch/excuse/gate-pass decisions stay with their existing officer permissions. Duty administration uses existing active grants and requires ordinary membership too.

Engineering defaults for staging: one active user/school per browser endpoint; account changes require owned revocation and local unsubscribe before new enrollment. Installation IDs are correlation only. Generic Arabic lock-screen payload, normal urgency, maximum 24-hour expiry, capped by existing task expiry. No new reminder cadence. Production origin, VAPID identity, durable Data Protection key directory and actual device evidence remain deployment gates. External channels default off.

## Visual direction

Use the application's Arabic typeface and school green. Tokens: forest `#145c43`, ink `#183c32`, mint `#eaf5ef`, white `#ffffff`, muted `#62776e`, amber `#936313`. The bell opens an RTL correspondence panel: clear Arabic heading, quiet category controls, a continuous list with readable previews, explicit read actions and restrained status chips. Mobile uses a full-width bottom sheet with safe-area padding; full history is a spacious reading column. Right alignment, generous Arabic line height, visible focus, reduced motion and a short user-triggered opening transition. Avoid decorative statistic cards or unrelated brand styles.

```text
top bar                         [bell + unread] [logout]
                                 ┌ الإشعارات       × ┐
                                 │ الكل | غير المقروءة │
                                 │ category selector │
                                 │ icon title · time │
                                 │      preview      │
                                 │ status   read/open│
                                 │ سجل الإشعارات    │
                                 └──────────────────┘
```

Review: retain the app identity and put emphasis on the correspondence list and readable previews. Avoid a separate dashboard aesthetic; motion only explains panel opening or an explicit read action.

## Evidence

See [retained local results and remaining gates](verification/n2/README.md), [concrete catalog](notification-catalog.md#n2-concrete-transition-inventory--2026-10-17), [schema](data-model.md#n2-implemented-schema-and-transport-settings) and [API](contracts/api.md#n2-implemented-contracts). Real iOS/Android, live provider acceptance and HTTPS staging are not established by local browser tests. All external channel flags remain off by default; the operational database was not migrated.

## Implementation refinements

- Existing SchoolManager grants include the school-oversight dashboard, not secretary/student attendance access. Student completion entries therefore use `StudentAffairsDashboard.SchoolOversight` with a manager-only read-only exact sheet; no role grants or original attendance commands are broadened. Staff completion remains separately labeled and manager scoped. Empty staff sheets are explicitly rejected in addition to existing full-roster validation.
- Actual summons statuses are Pending, Attended, UnderObservation and Improved. NoShow is an appointment-history row and leaves Pending; its committed history has a separate safe identity. Cancel/Close in automation-impact review is a recorded review decision, not a newly invented summons cancellation workflow.
- Current conversation types are GuardianTeacher, GuardianStudentAffairs, GuardianSocialWorker and SystemNotice. There is no existing group/broadcast/announcement authoring model to implement; delivered receipt integration covers all currently supported types.
- Per-page task state uses one projected SQL query. Exact source detail uses explicit DTO fields; the guardian cannot obtain raw case entities. Wrong-school/deleted/revoked targets remain neutral. A click never posts a decision or reads the center entry.
- Preserve the existing Data Protection application identity and key ring, rather than introducing a competing configuration that could invalidate stored integration credentials. Push material has a separate protection purpose. Startup requires an explicit durable existing `DataProtection:KeysPath` whenever push registration/dispatch is enabled.
- `pwa-verification` builds the optimized worker artifact for local inspection while leaving the original production CSS budget failure visible. It is not a deployable release sign-off. API/hub responses receive private/no-store headers in ASP.NET middleware; the local worker fixture checks cache policy with synthetic HTTP responses rather than claiming an actual deployed API check.
- Center history begins with new source events after activation. Legacy history stays accessible through its existing screens. No historical conversion or backlog push was performed.

Context changes clear rendered center entries synchronously with the service context revision, before waiting for new HTTP responses. Same-context refresh retains rows/focus; a rendered-component regression distinguishes the two behaviors.
