# Transport Contracts

**Status:** N1/N2 routes and event contracts implemented locally. See [concrete N2 contracts](#n2-implemented-contracts) and [evidence/device gates](../verification/n2/README.md).

All paths below are relative to the application origin. Existing REST endpoints and `ApiResponse<T>` remain compatible. Version new fields/contracts explicitly; keep existing routes during client transition. Authenticate all school data, derive user/school from current server context and use existing permission checks. The caller cannot select a delivery recipient or hub group.

## Existing endpoints to retain

`/api/v1/conversations` already provides list/detail/messages/send/read operations. `/api/v1/notifications` already provides list/count/read/read-all and policy-controlled approval/suppression operations. Inspect the current controllers under `backend/AlFalah.Api/Controllers/StudentAffairs/` before implementation; keep their exact existing routes, DTOs and error meanings.

## N1 REST additions

| Method and proposed path | Response data / rules |
|---|---|
| `GET /api/v1/conversations/{id}/messages/window?limit=50&before={cursor}` | `items`, `nextBeforeCursor`, `hasOlder`. Latest window when `before` is absent; limit clamped to 1-100. Only current participants may fetch it. |
| `GET /api/v1/inbox/state` | Scoped `unreadMessages`, `unreadConversations`, `unreadNotifications`, `syncCursor`, `asOfUtc`, `realtimeAvailable`; authoritative database aggregates. An unavailable surface has a null count. |
| `GET /api/v1/inbox/changes?after={cursor}&limit=100` | Bounded `items`, `nextCursor`, `hasMore`, `resyncRequired`. Limit is clamped to 1-100. Each item contains `eventId`, recipient-local `sequence`, `occurredAtUtc`; no resource identifiers or bodies. |

The window endpoint is an additive query over the existing message service/repository, not a second messaging subsystem. Move the current Angular chat to it and retain legacy paging behavior for old callers during transition. Do not silently ignore existing `sortDirection`, `pageNumber` or `beforeMessageId` parameters on the legacy endpoint.

N1 resolves active user/school membership, active roles and current database grants on every new REST request. `Messaging.ViewOwn` and `Notification.ViewOwn` independently enable counts; null means unavailable. Guardian profile/link and current participation filters apply to new message queries. Notification counts retain the existing delivered/approved/unsuppressed and Duty guardian filters. No umbrella grant is introduced. Generic journal items only invalidate the authorized inbox; resource reads recheck visibility.

### Message window ordering

Expose an immutable viewer-specific `visibleAtUtc`: the sender's existing queue/create time for their own message, or the recipient's first permitted delivery/release time. Preserve original business timestamps separately. Sort by `(visibleAtUtc, messageId)`, select the latest bounded window descending, and return its items ascending for display. A delayed low-ID message is therefore discoverable when it becomes visible.

The opaque `before` cursor binds the exact tuple and user/school/conversation/query version; it is not a bare message ID. Concurrent inserts do not duplicate older pages; delayed new releases arrive through the change stream/latest resync. Database precision, equal-time ordering, sender-pending states and original history must be covered by regressions before replacing the UI query.

### Synchronization contract

- Data Protection protects scope, purpose, sequence and version; history cursors also bind the conversation and exact visible-time/ID tuple. There is no separate stream generation in N1: streams are never reset/deleted by cleanup. The opaque cursor carries the authoritative long sequence; Angular never calculates a watermark from the diagnostic numeric item sequence.
- `inbox/state` establishes a consistent watermark and counts. The client loads current views and then drains changes after that cursor. A realtime hint received during this process schedules another drain rather than starting competing refreshes.
- Consume pages until `hasMore=false`. Advance only to the last safely applied returned cursor, not to the newest hinted sequence. Dedupe by change/event identity and resource ID; stale responses from a prior login/school are discarded.
- The journal sequence design in [data-model.md](../data-model.md) must guarantee that an earlier commit cannot appear below an already consumed watermark. A raw identity high-water mark is forbidden.
- A stale cursor returns `resyncRequired=true`; perform a full current-state resync. A cursor for another user/school is rejected without disclosing that context's existence.
- Resource references are hints. Subsequent detail/history reads recheck current authorization. Access revocation can require a coarse resync/invalidation instead of returning now-forbidden detail IDs.

Read/read-all endpoints retain explicit user intent and append relevant change records atomically. Notification clicks only navigate; they do not call a mutating read API from the service worker.

## N1 realtime endpoint

Implemented hub: `/hubs/inbox`. Server-to-client event: `InboxChanged.v1`.

```json
{
  "contractVersion": 1,
  "eventId": "opaque-event-id",
  "kind": "inbox-invalidated",
  "occurredAtUtc": "2026-10-17T09:00:00Z"
}
```

This event contains no school/student names, message bodies, bearer tokens or arbitrary navigation URLs. It instructs the active context to drain the authorized REST change stream.

The hub is a thin authenticated adapter. The server assigns a school/user/session-scoped group after checking current membership; clients cannot join arbitrary groups or send business messages through the hub. REST remains the write interface. Do not broadcast by global user ID alone across schools.

Handle bearer extraction only on the exact hub route where browser transport requires it; redact access tokens from URL/request logs. Enforce origin policy separately from CORS where necessary. Configure token-expiry closure using an API supported by the selected runtime, and invalidate sessions/connections on logout, school changes and role/membership revocation. Groups alone do not enforce revocation. Test an already open connection, not just new connection rejection.

Client responsibilities: bounded initial-start retry, reconnect retry, token refresh through existing auth flow, current-context group establishment on every new connection, REST catch-up, coalescing duplicate hints and one bounded foreground fallback timer. App logout stops the connection/coordinator and clears context-specific in-memory state.

## N2 Web Push registration

### N2 personal center and target contract gate

Before implementing the [center](../notification-center.md), T012A must finalize bounded center list/history with category/unread filters, authoritative center count, owned read/read-all and authenticated target-resolution contracts in this file. Reuse compatible existing routes where possible; do not advertise unimplemented endpoints. Preserve legacy notification and message API meanings and distinguish source counts from the new canonical center-entry count.

Each row needs a stable recipient entry identity, category, safe authorized display data, timestamps, read state, current pending/resolved/unavailable task state where applicable and a server-resolved target locator. Target resolution must identify the exact message, complaint, approval, permit, Duty assignment or other source, or an explicitly scoped queue/date/filter with selection support. Use the same resolver for bell and device clicks. It never grants access or executes a business command. [Catalog CP-01-CP-04](../notification-catalog.md) remain gates for affected producer contracts.

| Method and proposed path | Purpose / constraints |
|---|---|
| `GET /api/v1/push/config` | Return enabled/support information and active VAPID public key/key ID. No private secrets. |
| `POST /api/v1/push/subscriptions` | Authenticated idempotent register/reconcile for the current installation and server-authorized user/school binding. |
| `GET /api/v1/push/subscriptions` | Current user's bounded device list; safe labels/status only, no endpoint/key material. |
| `DELETE /api/v1/push/subscriptions/{id}` | Revoke an owned subscription and pending targets. Idempotent, no cross-user oracle. |

Example registration body:

```json
{
  "installationId": "opaque-client-generated-installation-id",
  "vapidKeyId": "public-key-version",
  "subscription": {
    "endpoint": "https://verified-push-provider.example/opaque-value",
    "expirationTime": null,
    "keys": { "p256dh": "base64url-value", "auth": "base64url-value" }
  }
}
```

The example hostname is illustrative and must fail the production allowlist. Enforce body/field sizes, valid key encodings, HTTPS provider origin, no credentials/fragments and an allowed port. Subscription endpoints are untrusted outbound URLs: use a reviewed provider-origin allowlist, disable redirects and block private/loopback/link-local destinations, including DNS resolution/rebinding defenses. Never probe arbitrary URLs. Update provider origins through controlled configuration and verify actual Chrome/Edge/Firefox/Safari subscriptions in staging.

The response contains server subscription ID, binding version and status in the standard envelope. Ignore/reject client-supplied user/school/recipient fields. Origin/anti-forgery controls follow the application's actual authentication model; do not assume CORS alone prevents CSRF if cookies are introduced.

On account/school change or logout, revoke the server binding before discarding the authenticated session where possible, cancel queued old-version targets and reconcile/unsubscribe the local browser state. A failed/offline logout must not be claimed as confirmed server revocation: locally detach, keep generic payloads, and reauthorize/reconcile on next contact. Tests must document this residual delivery window.

## N2 payload and click behavior

Proposed Angular-worker-compatible payload shape:

```json
{
  "notification": {
    "title": "Al-Falah",
    "body": "You have a new school update.",
    "tag": "opaque-canonical-event-id",
    "data": {
      "contractVersion": 1,
      "eventId": "opaque-canonical-event-id",
      "targetKey": "opaque-authorized-target-reference",
      "onActionClick": {
        "default": {
          "operation": "navigateLastFocusedOrOpen",
          "url": "/notification-entry?target=opaque-authorized-target-reference"
        }
      }
    }
  }
}
```

English text is a schema example; implementation uses reviewed Arabic-primary generic strings. The selected Angular version must support the chosen click operation, including when no tab is open. `targetKey` is a locator, not an authorization token. Resolve it through an authenticated Application service and existing school scope; the example `/notification-entry` is a proposed frontend route, not an existing API.

Allow only known same-origin application routes. If a target belongs to another authorized school, require the existing explicit school-switch flow before loading its data; never silently widen active scope. If logged out, retain only a safe target across login. If deleted, expired or no longer authorized, show a neutral unavailable state. No automatic actions/signatures/approvals are permitted from a push click.

Payload excludes student names, behavior/medical information, lesson details, message excerpts and credentials. Keep payload size below the selected provider/adapter limit with encryption overhead. TTL/urgency are provider transport options derived from approved event policy, not trusted client fields.

## N3 operations and native contract boundary

Operations provide permission-controlled summaries and audit/replay through the existing administration conventions; finalize exact operation routes in N3 before implementation. Track queue age, expiry, invalid endpoints, retry/dead-letter counts, worker heartbeat and provider latency. Replay rechecks source visibility, binding and expiry, and cannot replay a business workflow.

Future native registration is separate from a browser subscription: model device token/platform/adapter through a later versioned contract. Reuse canonical event identity, current recipient policy, authenticated target resolution and explicit read APIs. Do not add native tokens to the browser subscription request or implement native SDKs in these phases.

## N2 implemented contracts

The following replaces the provisional center/registration/payload descriptions above. All endpoints are authenticated, active-school/current-user scoped and use `ApiResponse<T>`. Both center reads and outbound attempts recheck current database authorization. No client recipient/school is accepted.

| Method/path | Implemented request/result |
|---|---|
| GET `/api/v1/notification-center` | `before?`, `category?`, `unread=false`, `limit=30` clamped 1–50; `{items,nextCursor}`. Protected cursor binds user/school/category/unread and tuple time/Guid. Item: id/category/title/preview/createdAtUtc/readAtUtc/taskState/sourceType/sourceId. |
| GET `/api/v1/notification-center/state` | `{unreadCount,available}`; uncapped authorized **center-entry** count, independent from N1 source message/notice totals. |
| POST `/api/v1/notification-center/{id}/read`, `/read-all` | Explicit intent only; owned currently visible entries, bounded 250-row transactions for read-all. Mirrors linked legacy notification read, never a message receipt or workflow decision. Missing/ineligible single entry fails neutrally. |
| GET `/api/v1/notification-center/{id}/target` | `{available,route?,query?,title?,state?}` from allowlisted server resolver; exact selectors. Unavailable target returns `{available:false}` without disclosing its original school/user. No mutation. |
| GET `/api/v1/notification-center/{id}/detail` | Authorized explicit source DTO: title/preview/state/sourceType/sourceId/rows(label,value)/optional action. Read-only attendance sheets and selected workflow detail; no raw entities or case graph. |
| GET `/api/v1/push/config` | `{enabled,pwaUiEnabled,publicKey?,vapidKeyId?}`. Never private key/subject/endpoint. |
| POST `/api/v1/push/subscriptions` | `{installationId:Guid,vapidKeyId,subscription:{endpoint,keys:{p256dh,auth}}}`; max 8192-byte body; 10 requests/minute per authenticated user. Return safe PushDevice; reject foreign active endpoint/installation binding. |
| GET `/api/v1/push/subscriptions` | Up to 20 safe owned active records: `{id,installationId,bindingVersion,confirmedAtUtc}`; no endpoint/key. |
| DELETE `/api/v1/push/subscriptions/{id}` | Idempotent owned revocation, version fence and queued-target cancellation. |
| GET existing `/api/v1/conversations/{id}/messages/window` | Additive optional `at=messageId` loads the authorized selected message's window, including beyond the latest 50. Existing before/latest clients remain compatible. |

Exact selection is added to the original complaint, visit, Duty document, review, teacher/security, guardian and case workspaces; original commands, permissions and row-version checks remain authoritative. Plain source detail is the safe fallback when the user can view a notice but cannot execute the workflow. The entry route retains only a safe Guid locator through login. It never automatically switches schools or follows producer-supplied URLs.

Actual push payload: generic Arabic `title`/`body`, stable entry tag, `dir:rtl`, `lang:ar`, public icon, and `data:{contractVersion:1,targetKey:<entry Guid>,onActionClick:{default:{operation:"navigateLastFocusedOrOpen",url:"/notification-entry?target=<entry Guid>"}}}`. No extra eventId, private preview or auth token. Angular 17.3.12 implements the chosen click operation; actual closed-client phone behavior remains T020.
