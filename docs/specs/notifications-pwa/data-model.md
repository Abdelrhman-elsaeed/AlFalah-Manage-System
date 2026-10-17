# Data Model and State Semantics

**Status:** N1 implemented by `20261017125332_AddRecipientInboxRealtime`; N2 by `20261017144014_AddPersonalCenterAndBrowserPush` and `20261017150426_AddBrowserPushAttemptAudit`.
**Implementation:** N1 foundations, N2 browser subscriptions, N3 operational verification.

## Existing records remain authoritative

Retain `Notification`, conversation threads/messages/receipts, approval fields, school membership, guardian relationships and existing automation outbox records. Retain enum numeric values. Existing `Delivered` values mean available in the application's corresponding surface; they must not be relabeled as phone delivery.

`IsRead`/`ReadAt` and message read receipts remain explicit user actions. A provider response, realtime send, background push, notification click or permission grant never changes them automatically.

Legacy nullable `Notification.SchoolId` rows are not eligible for external delivery until an existing authorized policy resolves their school. Do not infer school from a client parameter or mass-update historical rows to make them pushable.

## Proposed persistence

### N2 center extension: source-aware identity before device delivery

The owner-required [catalog](notification-catalog.md) and [center design](notification-center.md) add a durable personal-center projection in N2. T012A must select its concrete reuse/additive schema and migration before T012B. Record recipient school/user, category, canonical source/revision, target reference, creation/visibility and independent read state. Reuse existing `Notification` IDs when appropriate; explicitly define message-entry read storage, message grouping/count semantics and Duty System-message deduplication. Source task status remains independent of notification read/delivery state.

Preserve N1's generic journal and realtime tables; they are not the center's actionable history. Extend authoritative counts and transactionally journal new center/read transitions without redefining existing message/notification counts. The center badge counts canonical center entries, not the sum of two overlapping source counts. Revalidate resource access for lists/counts as well as targets. No schema change is implemented by this design update.

N1 records below are implemented; N2 records remain design targets. N1 concurrency protection uses transaction-held stream locks and conditional lease updates rather than row-version columns. Store UTC instants; preserve existing school-time-zone policy for business release.

### 1. RecipientInboxStream - N1

| Field | Purpose |
|---|---|
| `SchoolId`, `UserId` | Composite stream key and authorization scope |
| `CommittedSequence` | Last transactionally allocated sequence; serialized per recipient |
| `MinimumRetainedSequence` | Cursor-retention boundary |
| SQL key/range locks | Concurrency protection; the composite primary key is nonclustered to support the existing 450-character user identifier |

This is a synchronization index, not a replacement message store. Every visibility/read/count-affecting transition appends to the affected recipients' streams in the same transaction. Serialize sequence allocation using a SQL Server row/key lock held until commit; initialize rows safely under concurrent first use. Acquire multiple recipient stream locks in deterministic order and retry deadlocks with the existing transaction policy.

Do not use a global SQL identity or `MAX(MessageId)` as a committed watermark. An earlier allocated identity can commit after a later one, and an older queued message can become visible much later. The stream's lock/transaction design must pass real SQL Server commit-order tests before it is treated as reliable.

### 2. InboxChange - N1

| Field | Purpose |
|---|---|
| `Id` | SQL storage identity; never a synchronization watermark |
| `SchoolId`, `UserId`, `Sequence` | Unique scoped order, logically bound to the stream in the same transaction |
| `EventId`, `OccurredAtUtc` | Generic invalidation identity and timestamp |

Unique `(SchoolId, UserId, Sequence)` prevents duplicate positions. N1 deliberately stores no resource IDs, bodies or event category in this generic cache-invalidation journal. Repeated hints are harmless; canonical delivery identity is deduplicated in the intent table. Cleanup advances the retention boundary safely; stale cursors receive `resyncRequired`, not silent truncation.

For initial resync, N1 reads the recipient's committed version before and after the counts in an explicit Read Committed transaction. Equal versions prove no journaled count transition crossed those reads; a changed version retries the complete read. Three optimistic attempts are followed by a Serializable fallback under sustained contention. With the journal disabled, use the Serializable fallback directly. Bounded deadlock retry protects the read; no SQL snapshot-isolation database option is required. A real SQL interleaving test commits between the message and notification counts and proves the returned pair/watermark is recomputed consistently.

### 3. NotificationDeliveryIntent - N1

| Field | Purpose |
|---|---|
| `Id`, `EventId` | Storage and generic event identity |
| `SchoolId`, `UserId` | Required recipient scope |
| `SourceKey` | `fact:{correlation}` for a newly visible Duty fact/revision/recipient; otherwise `change:{eventId}` |
| `Channel` | `Realtime` only in N1 |
| `CreatedAtUtc`, `ExpiresAtUtc`, `NextAttemptAtUtc` | Scheduling and useful lifetime |
| `CompletedAtUtc`, `Attempts` | Completion and bounded retry progress |
| `LeaseId`, `LeaseUntilUtc` | Conditional claim/complete/recovery |

Uniqueness: `(SchoolId, UserId, SourceKey, Channel)`. Duty's persisted correlation already identifies its fact/revision/recipient and is shared by both surfaces. Pending approval and unreleased recipient receipts create no recipient intent; the sender can receive an invalidation for their own queued message. Read-state changes have a new change identity. Startup seeding explicitly disables projection. N1 has no device targets or external notification provider.

Before N2 creates any Web Push intent, add source/category/revision references and live source-eligibility checks. An N1 generic invalidation row is not sufficient evidence to send a phone alert, and must never be replayed as a historical push backlog.

Realtime intent completion means the invalidation hint was submitted to the connection layer. It is not proof that a tab received it. REST resynchronization recovers missed hints.

Duty's second surface may commit after the first hint was already submitted. Re-arm the same canonical realtime intent with the latest journal event and invalidate its old lease, so the later surface also refreshes promptly. This may send another generic cache invalidation, while preserving one intent identity and fencing stale completion. It does not authorize two external push banners; N2 must deduplicate its source alert separately.

### 4. PushSubscription - N2

| Field | Purpose |
|---|---|
| `Id`, `InstallationId` | Server identity and client installation correlation; the latter is not authorization |
| `UserId`, `SchoolId`, `SessionBindingVersion` | Server-controlled current binding; one active user/school per installation |
| `Endpoint`, `EndpointHash` | Encrypted sensitive endpoint and unique lookup hash |
| `P256dh`, `AuthSecret`, `VapidKeyId` | Protected subscription material and signing-key association |
| `CreatedAtUtc`, `LastConfirmedAtUtc`, `RevokedAtUtc` | Lifecycle |
| `State`, `RevocationReason`, `RowVersion` | Active, revoked or invalid; ownership/concurrency protection |

Protect endpoint/key material using the deployment's managed secret/data-protection design, with durable keys available across restart/restore. Never put the VAPID private key or subscription secrets in logs, source control, frontend bundles or client-visible administrative lists. The VAPID public key is intentionally public.

An active endpoint has one owning binding. Concurrent registration/account change must not leave the endpoint active for two users. Rebinding requires an authorized lifecycle operation; guessing an installation ID cannot revoke another user's endpoint. Revoking increments the binding version and cancels pending targets. A future renewal creates/reconciles a current binding rather than resurrecting revoked queued work.

VAPID key rotation is not merely replacing a server secret: retain the key association needed by existing subscriptions and use a tested re-subscription/re-enrollment path for the new public key. Do not assume old subscriptions accept a different signing identity. Key retirement and emergency revocation are distinct operational procedures.

### 5. PushDeliveryTarget - N2

| Field | Purpose |
|---|---|
| `IntentId`, `PushSubscriptionId`, `BindingVersion` | Unique intended device binding |
| `State`, `AttemptCount`, `NextAttemptAtUtc`, `ExpiresAtUtc` | Independent per-device progress |
| `LeaseToken`, `LeaseExpiresAtUtc` | Race-safe claim and completion |
| `ProviderAcceptedAtUtc`, `LastErrorCode`, `LastHttpStatus` | Transport observations, sanitized |

One failed endpoint does not retry successful targets on another phone. On first eligible fan-out, snapshot active authorized subscriptions transactionally; do not add a newly registered device to an old event's target list. No active subscription means `NoActiveSubscription`, with the inbox unaffected. Defaults do not send a historical backlog after opt-in.

### 6. DeliveryAttempt - N1/N2

N1 implements `InboxDeliveryAttempts`: `Id`, `IntentId`, `LeaseId`, `CompletedAtUtc`, `Succeeded`; unique intent/lease. Only a still-owned, unexpired lease can append a completion. No bodies, tokens, endpoints, names or exception text. N2 adds BrowserPushAttempts separately; see concrete schema below.

N1 technical defaults: five-minute invalidation expiry, 30-second lease, eight maximum attempts, capped exponential retry to 60 seconds; 25 claims per sweep, five concurrent deliveries and a three-second transport deadline. Journal/attempt/intents use a configurable seven-day recovery buffer (clamped 1-90 days). Cleanup is bounded to 500 old intents and one recipient journal per maintenance sweep. These are recovery settings, not an approved school-record/legal retention policy; original messages/notifications are not deleted.

## Channel state machine

N1 only submits generic cache invalidations to currently authorized connections; suppression/deletion can itself require an invalidation to remove cached content. N2 implements the richer source/device cancellation and provider state machine below; provider acceptance remains distinct from delivery or read.

```text
Pending -> Claimed -> Submitted (realtime) / ProviderAccepted (push)
                  -> RetryScheduled -> Claimed
                  -> InvalidSubscription / Cancelled / Expired / DeadLetter
Pending ----------> NoActiveSubscription (push)
```

Successful provider submission is terminal for that target's normal retries; it does not imply device arrival. An unknown outcome after a timeout remains eligible for a bounded retry and possible duplicate presentation. A stable notification tag/event ID helps presentation deduplication where supported but cannot establish exactly-once delivery.

Before each attempt, revalidate school/user activity, relevant permissions/relationships, binding version, current source visibility, cancellation/suppression and expiry. Eligibility loss cancels work rather than indefinitely retrying. A changed school binding cannot claim an old school's target. Already in-flight or provider-accepted generic notifications may still arrive after revocation; opening them must reauthorize.

## Retry classification

| Observation | Planned behavior |
|---|---|
| Accepted provider submission | Record `ProviderAccepted`; do not mark inbox content read. |
| Provider-confirmed expired/invalid subscription | Invalidate that subscription and cancel its queued targets. RFC 8030 specifies 404 for expired subscription sends; verify adapter/provider handling of 410 and other responses. |
| Authentication/signing/configuration error | Alert operator and pause affected channel/key as appropriate; do not erase every subscription or spin rapidly. |
| 429, transient 5xx, network timeout | Bounded exponential backoff with jitter; honor valid `Retry-After` within expiry and attempt limits. |
| Invalid payload / oversized payload | Terminal actionable failure until corrected; never repeatedly submit the same invalid payload. |
| Past event expiry / source cancelled | Expire/cancel before submission; preserve inbox policy and attempt audit. |

Final numeric retry/lease/retention settings are documented and tested in N2/N3; do not copy the business outbox's defaults without considering HTTP timeouts and lease behavior.

## Migration and rollout constraints

- Additive tables/indexes first; no enum renumbering, destructive history rewrite or mass push backfill.
- Introduce journal/intent writes behind flags, then enable readers after a consistent baseline is available. Rows created before journal activation are covered by a full inbox resync, not fabricated historical changes.
- Batch cleanup and fan-out are bounded. Query indexes cover recipient/sequence, due state/time, leases and subscription ownership.
- Real SQL Server tests cover duplicate keys, transaction rollback, deadlock retry, commit ordering, stale lease completion and concurrent registration.
- Rollback initially disables producers/dispatch while retaining additive records. Do not drop delivery tables while workers or old clients can still reference them.

## N2 implemented schema and transport settings

These concrete names supersede the proposed N2 names above. Migrations are additive: `20261017144014_AddPersonalCenterAndBrowserPush`, then `20261017150426_AddBrowserPushAttemptAudit`. Applied only to fresh guarded test databases; no operational migration.

| Table | Implemented keys and semantics |
|---|---|
| `PersonalNotifications` | Guid Id; unique SchoolId/UserId/SourceKey; recipient CreatedAtUtc/Id history index; category, audience, permission, source type/id, optional legacy NotificationId/MessageId/StudentId/date, safe title/preview, independent ReadAtUtc, updated/expiry and PushDisposition. SourceKey 240/UserId 450 keeps SQL unique key below the nonclustered byte limit. |
| `AttendanceCompletions` | Durable explicit nonempty full-sheet signal: school, optional classroom, date, roster count, submitted UTC. Projection key deduplicates school/classroom/date or school/date staff; corrected saves may add a signal but never reset the existing entry or create a second device fan-out. |
| `BrowserPushSubscriptions` | Guid Id/installation, current school/user, unique SHA256 endpoint hash, Data Protection protected endpoint+p256dh+auth JSON, VAPID key association, binding version, created/confirmed/revoked. Installation is correlation, never an ownership credential. Maximum 20 active devices per user. |
| `BrowserPushTargets` | Entry/subscription/binding unique key, expiry/due, lease token/until, attempts/state, last HTTP status and provider acceptance timestamp; restrictive FKs preserve source/device audit. |
| `BrowserPushAttempts` | Unique target/lease, completed UTC, classified state and nullable HTTP status. Conditional completion rejects expired/stale leases before appending. No raw provider response, endpoint, key, body or exception text. |

The source save, canonical entry, N1 recipient journal and current-device snapshot commit together. Existing source transactions remain intact. New devices are not attached to old events. PushDisposition is Disabled, Expired, NoActiveSubscription or Scheduled; no active device never blocks the inbox.

N2 uses one claimed target per worker turn, a 30-second lease, 10-second HTTP deadline, at most eight claims, exponential delay with jitter capped at 900 seconds unless valid Retry-After is longer, always bounded by expiry. No library-level hidden retry. Normal urgency; maximum 24h TTL, capped by permit validity, approved gate window or next school-local Duty assignment midnight. Recheck pending workflow, latest Duty revision, submitted report, school/user/grants/relationships and binding before each send. Superseded/expired/cancelled work is not replayed. Configuration/signing failure is terminal for that target and retains audit; operator summaries/channel-key alerting remain N3.

ProviderAccepted records transport acceptance only; read state and business decisions remain unchanged. 404/410 revoke only the affected device/version and cancel its pending targets; 401/403 are configuration failures; 429/5xx and network/timeouts retry within limits; other provider failures are invalid payload. These mappings are exercised through intercepted real-library HTTP, not observed live providers. Interrupted unknown outcomes may repeat a generic stable-tag banner. Old-key subscriptions are fenced, not silently signed using a new identity; rotation requires explicit client re-enrollment and device acceptance testing.

Retention for the new center/device audit is not silently borrowed from N1's seven-day journal cleanup. It remains an operator/business retention decision for N3; no new source-history deletion job is added.
