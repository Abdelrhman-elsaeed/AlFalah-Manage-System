# N2 - Installable PWA and Opt-in Web Push

**Status:** Not started.  
**Story:** US3. **Tasks:** T012-T021 in [tasks.md](../tasks.md).  
**Dependency:** Accepted N1 exit and compatible dependency decision.

## Outcome

Supported users can install the website where appropriate, opt into browser notifications and reach the current authorized message/notice from a device notification. Permission denial or unsupported devices retain a usable inbox.

## Included work

- Version-matched Angular PWA, stable manifest identity and one root service worker.
- Public app-shell allowlist and honest offline state; no private API/file caching.
- Contextual Arabic/RTL installation and permission UX, including iOS Home Screen guidance.
- Standard Web Push/VAPID adapter, protected subscriptions and independent per-device delivery targets.
- Ownership, endpoint validation, revocation/rotation, TTL, retries and channel switches.
- Generic notification payload, authenticated deep link, safe app updates and draft protection.

This phase does not require Firebase browser SDKs, Apple Developer Program enrollment, native builds or offline message submission. Preserve business office hours/approval and do not push old seeded/history records.

## Required checks

- Real HTTPS staging iPhone/iPad Home Screen and Android Chrome delivery with app backgrounded/no open tab; record OS/browser and settings.
- User-gesture permission; denied/unsupported/install-required states; no automatic or repeated prompts.
- Multiple devices, reinstall, stale subscription, revoked endpoint, key change and no-active-device outcome.
- Correct provider response mapping: acceptance, invalid subscription, signing/configuration failure, throttling, timeout and invalid payload.
- Account/school changes and owned-device deletion; verify queued target cancellation and document the offline/in-flight revocation window.
- Endpoint SSRF/ownership/rate-limit negative tests; inspect logs, bundles and caches for secrets/private data.
- Closed-client click, expired login, wrong active school, unauthorized/deleted target and unsent draft during an update.
- Arabic/RTL review at mobile/desktop widths, keyboard access and clear enable/disable/status controls.

## Exit evidence

- [ ] US3 and N2 portions of SC-004/SC-005/SC-006 pass with device evidence.
- [ ] Per-event expiry, target support matrix, active-school binding and generic content defaults are reviewed and recorded.
- [ ] Q01/Q04/Q05/Q07/Q08 N2 inputs are resolved for staging; production-only gaps remain explicitly gated.
- [ ] No competing root worker; cache policy and notification click work in the selected Angular version.
- [ ] Channel-disable and service-worker removal procedures are concrete and ready for the N3 stale-client rehearsal.
- [ ] Source/API/schema/security documentation and central records match actual implementation.

Stop and report after the phase exit. A desktop DevTools push simulation alone does not satisfy the actual-phone requirement.
