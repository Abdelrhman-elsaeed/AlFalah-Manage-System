# Unified Personal Notification Center

**Status:** N2 implemented locally on 2026-10-17; [evidence and device gates](verification/n2/README.md).  
**Coverage:** [Required event catalog and open policy definitions](notification-catalog.md).  
**Requirements:** FR-015-FR-018 / US5 in [spec.md](spec.md).

Implemented record: [N2 design/contract choices](n2-implementation.md), [transition-level catalog](notification-catalog.md#n2-concrete-transition-inventory--2026-10-17), [concrete APIs](contracts/api.md#n2-implemented-contracts) and [retained screenshots/tests](verification/n2/README.md). The descriptions below remain the UX contract; acceptance on actual installed iOS/Android is still pending.

## User experience

Provide a bell in the shared top bar for all users entitled to personal notifications. Its badge is an authoritative unread **center-entry count**, not an arbitrary sum of unread message counts and duplicate System notices. Document its relationship to the existing conversation badge; opening a conversation and reading a center alert are distinct actions.

Click/tap opens a responsive Arabic/RTL panel with recent entries, unread distinction, category, safe authorized text, timestamp, pending/resolved status where relevant and a clear destination. Include loading, empty, offline, retry and permission-unavailable states. Offer All / Unread and useful category filters, explicit mark-read/mark-all-read and a paginated full-history view. Use keyboard navigation, appropriate focus management, accessible labels and Escape/close behavior. Opening or closing the panel does not bulk-read it.

"All notifications" means all currently authorized personal entries in the active school context, with bounded history pagination; it never means another recipient's entries or all historical student records. Existing data-retention rules apply; no unlimited retention guarantee is introduced. Push permission is not needed to use the bell or history.

Pending work remains discoverable until the domain resolves it even if its notification is read. A resolved, expired, cancelled or reassigned task has an honest state; it must not continue suggesting an unavailable action. A popup or count is not the school's only record of actionable work.

Use the same canonical recipient entry for the bell and its optional phone notification. Do not emit another external notification when read state, counters or channel delivery state changes. Existing guardian notifications, chat and dashboards consume/reconcile with this identity rather than introducing competing bell timers or duplicated unread counts.

## Navigation and decisions

Every center row and push click uses an authenticated, server-resolved target. A registered target type plus scoped source identity determines a known application route. Never trust a producer's arbitrary URL, construct a route from notification message text or use a client-supplied recipient.

Resolve to the exact conversation/message, complaint/response, approval, permit, Duty assignment/signed submitted report, summons, case or attendance sheet. Student-attendance targets include school/classroom/date; staff attendance uses a separate school/date target and label. Where only a queue exists, carry an authorized selection/filter and visibly identify the correct item. Missing selection support is implementation work, not a successful deep link merely because the list loads.

Login expiry preserves only a safe locator; another school requires the existing explicit switch flow. Missing/deleted/no-longer-authorized items show a neutral unavailable result without revealing hidden names or content. Recheck access at target resolution and again at the destination read/command. Reassignment and delegation revocation apply to existing unread entries too.

Opening a target may offer a deliberate read action, or reuse an existing visible-content read behavior only where already specified. Service-worker clicks never invoke read, approve, reject, acknowledge, sign or execute endpoints. In this initial design the notification navigates to the domain screen for its authorized decision; no lock-screen decision buttons are introduced.

## Persistence and service boundaries

Reuse `Notification` and conversation/receipt records as authoritative sources where their policies fit. Before schema work, choose a normalized recipient center projection or an additive projection over those sources; do not silently rewrite every existing notification into one universal business type. Any persisted center identity needs school/user, category, canonical source/revision, target reference, creation/visibility timestamps and independent read state. Task status is derived from current domain state, not inferred from read/delivery state.

Message projections must specify aggregation: default proposal is one source entry per visible message, with UI grouping permitted only if individual messages remain discoverable and counts have explicit semantics. A UI group is not a new business event. Resolve Duty System-message/notice duplication before center counts and Web Push fan-out. Resolve whether existing notification IDs can be reused and how message entries store independent read state before final migration/API design.

Keep generic N1 change journal/intents intact as synchronization infrastructure. They contain no actionable resource metadata and cannot act as the user-facing history or source-aware push backlog. N2 adds the center/category/source seam and triggers N1 resynchronization for committed center/read changes. Prove consistent counts/versioning across new projections, without weakening existing snapshot/claim guarantees.

Application services own recipient policy, canonical alert projection, target resolution and current task eligibility. Infrastructure implements bounded queries, transactions/claims and channel adapters. Controllers/hub remain thin; use existing `ApiResponse<T>` conventions and database-driven permissions. A personal-inbox grant never bypasses feature/resource restrictions.

Finalize exact center list/count/read/read-all/target-resolution routes and DTOs in `contracts/api.md` before implementation. Preserve existing notification/message APIs during transition, and distinguish center unread count from existing message and notification source counts. New endpoints/schema mentioned here are design requirements, not delivered APIs.

## Delivery within the existing three phases

1. **N1 completed baseline:** reliable messaging windows, scoped counts, durable generic invalidation and authenticated realtime. Its 43 backend, 2 Angular and 8 browser checks do not prove expanded event coverage or a general bell.
2. **N2 expanded scope, still unstarted:** T012A resolves and inventories required events; T012B adds the source-aware center and missing approved producers; T012C builds the top-bar bell/history and exact-item resolver; T012D verifies all mandatory rows. Complete these before source fan-out T016 and integrate with PWA/opt-in/click work T013-T020. Unresolved recipient/completion definitions block their own producer. Do not start runtime implementation during this documentation task.
3. **N3 verification and rollout:** verify role/event coverage at customer scale and on real devices, test stale tasks and permission changes, include center fan-out in Gulf/load/recovery drills, then document pilot readiness.

This is additional N2 work, not a fourth phase or a claim that N1 already implemented a school-wide notification system. Optional NS suggestions and new scheduled reminders require their own product selection.

## Acceptance scenarios

- A teacher receives a released message or entry-permit acknowledgement alert through the bell and opens the exact authorized item. A pending/unreleased message never becomes a recipient phone alert.
- A manager sees their required approval, takes the decision in the original workflow and the pending state resolves across sessions. Another manager or unentitled administrator sees neither content nor an identifying count.
- A complaint submission reaches its entitled reviewer; a teacher-visible response reaches only its owning teacher. Moderator/MainManager hard-denial tests still pass unless an explicit separately documented policy change is approved.
- Each completed classroom attendance sheet notifies the manager independently, including all-present rosters; staff attendance has its own school/date completion. Partial staff batches create no false completion. Only committed signing/submission of a Duty report notifies its manager/entitled administrators, independently of archive success; draft/letter signature/last-student departure do not. Retrying a canonical submission creates one center entry per entitled recipient/revision; corrections follow the recorded CP identity policy.
- A dashboard warning and its source notice produce one canonical alert; loading the dashboard ten times creates zero new alerts. A read task remains pending until its business decision is made.
- Bell history, category/unread filters, independent counts, read/read-all, reconnect and cross-device refresh work on RTL mobile/desktop. An old school/account response cannot repopulate the current panel.
- Expired login, wrong active school, deleted source, cancelled task, revoked delegation and resolved approval all navigate safely. Push opt-out leaves the complete personal center usable.
