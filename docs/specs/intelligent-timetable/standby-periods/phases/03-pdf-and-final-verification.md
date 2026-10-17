# Phase 3: official PDF and final verification

**State:** Delivered locally on 2026-10-17 (C40 / D-132). [Implementation](../register-implementation.md) and [verification/manual handoff](../../verification/standby-phase3/README.md). Owner appearance/physical-print acceptance and official logo assets remain pending.
**Depends on:** Phase 2 retained assignment/execution records and resolved signature/issue policy.
**Discoverability follow-up:** C43 / D-135 exposes the existing register from the weekly standby header via a guarded daily-ledger deep link. No parallel PDF implementation or automatic issuance. [Verification](../../verification/standby-publish/README.md).
**Current entry:** C44 / D-136 opens that same register in the daily section of **حصص الانتظار**; old deep links redirect. [Unified-page verification](../../verification/standby-hub/README.md).
**Weekly export follow-up:** C45 / D-137 extends that register with explicit Day/Week selection, Sunday–Saturday range, retained seven-day source, continuous pagination and independent scoped history. Additive local schema 76 preserves all original table hashes. 37 backend checks (one opt-in allocator SQL suite skipped), 29 Angular checks, Development build and actual SQL/browser/PDF checks pass. [Evidence](../../verification/standby-week/README.md).
**Skills:** dotnet-layered-api-architect and frontend-design; tdd for immutable artifacts/integration behavior; diagnosing-bugs only when a failure is reproduced.

## Deliver

A source-faithful QF71-B-2-1 register with polished preview/download, plus verification of the complete standby feature and a manual-test handoff.

## Work

1. Reopen original `3.jpeg`, verify its fingerprint and reproduce the exact portrait header, instructions, three groups/eight printed rows, columns, borders, signature/footer positions and actual school metadata.
2. Reuse the Arabic PDF/fonts infrastructure. Build readable continuation pages for extra teachers/periods and long content; retain blank unrecorded fields and never manufacture signatures.
3. Issue revision-bound frozen artifacts; retain previous versions after correction. Weekly timetable printing remains a separate output.
4. Integrate the existing polished preview/native download flow. Previewed and downloaded bytes must match; available local documents work without Drive.
5. Use existing storage only if archive policy/destination/activation is agreed. No implicit duty/visit archive activation, parallel file browser or unapproved provider test.
6. Verify actual PDF pages against the source at A4/100%, and actual preview/download interactions at 390/768/1440/1920px.
7. Exercise the full isolated journey: teaching completion -> weekly manual/fair standby -> review/publication -> absence -> daily manual/fair proposal -> approval -> teacher notification/dashboard -> execution -> PDF.
8. Run meaningful cross-feature regressions for scope/roles, concurrency, retries, regeneration/restore/swaps, effective lesson ownership, office-hour/student integration and retained corrections.
9. If archival verification is authorized/in scope, verify delivery/retry/recovery in the designated synthetic target.
10. Record commands, results, captures, sample PDFs, remaining limits and the manual-test checklist; update the spec kit.

## Acceptance and tests

- PDF cases include one/three/four absent teachers, more than eight periods, long names/narrative/notes, missing signatures, pre-execution output and corrected signed records.
- Independently inspect actual Arabic shaping, text bounds, page breaks, repeated headers and signature ownership; green logic tests do not establish visual fidelity.
- Real SQL/HTTP/browser evidence proves the integrated journey and prevents duplicate booking or lost history.
- Do not weaken budgets/assertions to hide unrelated baseline failures. Reproduce/classify any remaining failure with its practical impact.
- Owner visual/physical-print acceptance is reported separately and remains pending until performed.

## Handoff and boundary

Provide local frontend/backend URLs and tested roles without secrets, how to choose both weekly/daily modes, approve work, complete teacher execution and preview/download the register. Start services within existing user authorization. No production rollout, operational reset/root deletion or real-user test traffic is implied.

## Actual delivery

Manual-test clarification on 2026-10-17: approved replacement names print before acknowledgement/signature. The local empty PDF was traced to zero dated coverage needs despite ten weekly standby slots. [Read-only findings and handoff](../../verification/standby-names/README.md); no implementation or phase status change.

Whole-day register issuance and ten-item retained history are integrated in the existing manager ledger. Exact preview/download bytes, independent receipt/execution evidence, draft exclusion, stale/retry behavior and correction retention are verified.342 focused backend/SQL checks across the regression and fresh-SQL runs,16 Angular checks, six independently rendered PDF cases and authenticated responsive HTTP/browser workflows pass. Additive local schema75 preserves existing values across45 related tables. Existing production CSS budget blockers, the unrelated SQLite fixture issue and Arabic glyph text-extraction limitations are recorded in the evidence. No Drive writer is enabled: archive policy for this register remains unagreed. Stop at this phase boundary; no extra implementation phase is introduced.
