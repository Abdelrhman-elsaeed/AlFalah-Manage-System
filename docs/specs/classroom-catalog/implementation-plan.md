# Implementation handoff and acceptance gates

**C36 status:** C1/C2 foundation, C3 consumers, C4 production assignment UX and C5 saved-revision assignment PDF are delivered locally. [C33 foundation](implementation.md), [C34 consumers](consumers.md), [C35 workspace](assignments.md), [C36 statement](statement.md). C6 pilot/owner/physical/printer acceptance remains pending. Production CSS budget blockers remain disclosed; no budget was relaxed. This table retains the original acceptance gates.

| Phase | Work | Exit evidence |
|---|---|---|
| **C0 — Discovery/design** | Inspect source image and consumers, capture owner decisions, publish proposed model/UX/export and standalone prototype | Impact map, reproducible source inventory, screenshots390/768/1440, interactive workflows, sample PDF; delivered2026-10-15. |
| **C1 — Read-only mapping and additive identity** | Inspect operational data under authorized scope; finalize stage metadata, duplicate/letter mappings, bridge revisions and specialization category; additive migration tested in isolated SQL first | Counts for exact/ambiguous/orphan/duplicate cases, preserved IDs/history, rollback/read compatibility tests and school/teacher authorization baseline. No blind operational conversion. |
| **C2 — Shared catalogue + teacher association** | Scoped/paged options; derived names; atomic bulk sections with location handling; current-year ID multi-select in account, manager profile and user teaching writes; self confirms directly | All paths use one policy; rejection tests for foreign school/year/deleted IDs, stale revision, partial updates, duplicate sections, selected values off-page and >100 classrooms. |
| **C3 — Consumers and authorization cutover** | ID joins for teacher class scope; active V2 drafts (V1 writes remain retired410), manual/legacy timetable resolution, enrollment/import review, labels/filter/bookmarks and dashboards | Current lesson, attendance, permits, student rosters and all migrated string-identity consumers verified. Historical visit/duty PDFs and timetable snapshots hash-identical. No unreviewed access widening or loss. |
| **C4 — Production assignment UX** | Port approved visual design into existing Angular module; preserve current server picker/search/paging/filter modes, save transaction and guards | Component tests plus actual role-based browser tests, empty/loading/error/denied/conflict, large-school performance, phone/tablet/desktop, keyboard/screen-reader/reduced-motion and unsaved state checks. |
| **C5 — Assignment PDF** | Separate saved-revision projection/renderer, explicit scope, source header/grouping, categorization, appendices and continuation | Real API/SQL/export checks; readable24-row A4 landscape and large/long-name cases; arithmetic co/split/pair validation; retained revision and C30 download/IDM policy; no new Drive obligation. |
| **C6 — Pilot/review** | Per-school mapping/cutover telemetry and rollback rehearsal | Owner approves appearance/source parity and physical devices/printer; authorized pilot verified; documented operational release decision. |

## Consumer completion checklist

- [x] Existing Classroom entity and IDs retained; no parallel classroom system.
- [x] Manager catalogue reachable in correct navigation, with current permission checks.
- [x] Stage-local numeric grade/section identity and duplicate constraints; physical room/location separate.
- [x] School/year boundaries and historical selected-ID hydration across all selectors.
- [x] Account teaching editor and manager teacher profile stop accepting free-text classes.
- [x] User/teacher creation and synchronization paths preserve existing associations unless explicitly changed.
- [x] Teacher self-confirmation is immediate; no pending approval requirement added.
- [x] TeacherContext list and access membership use ID joins with existing role/enrollment conditions.
- [x] CurrentLessonEntry and gate legacy resolution are classified and tested.
- [x] Student enrollment, transfers, import, daily attendance and session delay remain term-correct.
- [x] Conduct, academic concerns, recognition, referrals, summons, guardian, security, messaging and dashboards covered.
- [x] Intelligent timetable settings, teacher profiles, subjects and requirements use the same catalogue.
- [x] Assignment single/co/split, whole pairs, draft-aware load, teacher paging and atomic revision saves retained.
- [x] Manual/generated import/save/publish/review/repair/substitution/swap/restore conflict semantics retained.
- [x] Active VisitsV2 uses structured draft selection; V1 writes remain retired410. Prior reports/signed/archive versions remain unchanged.
- [x] Duty late-departure class filters and student selections validated; retained signed files unchanged.
- [x] Existing timetable PDF/Excel and retained snapshots, search, ID bookmarks and reference projections preserved.
- [x] Production grouped assignment ledger, bounded teacher picker/summary, no-op drafts, keyboard/focus and concurrent-save handling (C4); physical assistive-technology and release acceptance remain C6.
- [x] New saved-revision assignment PDF, explicit classification/scope, retained bytes and source-structured rendering (C5); final official assets/physical source parity remain C6.
- [x] Mapping/backfill idempotency, collision handling, auth comparison, constraints and safe rollback proven.
- [x] Source scan rerun; every remaining active text comparison categorized before cutover.

## Test matrix for future implementation

| Scenario | Required outcome |
|---|---|
| Same visible1/1 across two schools/years/stages | Correct context IDs; no cross-context access or import match. |
| Arabic/ASCII digits, whitespace, section2 vs10 | Canonical identity/duplicate handling and numeric sorting; no duplicate new rows. |
| Legacy أ/ب or unrecognized label | Review mapping, retain IDs; no silent conversion or extra invented class. |
| Concurrent bulk create / teacher set update | One atomic result or conflict; no overwritten selection or half-batch. |
| Teacher selects multiple classes, including option on another page | Complete ID set saved directly; selection retained across search/paging. |
| Self-select vs scheduled lesson disagreement | Association follows owner policy; current lesson still uses published timetable. No timetable mutation. |
| Rename/deactivate/remove classroom | Correct live options; history retained; old IDs, signed bytes and previous scope behavior accounted for. |
| New academic year | New-year section rows and explicit teacher/enrollment carry-forward choices; old references unchanged. |
| Two+ teachers, quota5 split3+2, pair count1 | Whole pair owned by one member, sum valid, correct load and print annotation. Invalid6-total/half-pair rejected. |
| Co-teaching | Each teacher gets full quota; two names and one logical occurrence; no duplicate-class conflict. |
| Non-specialization subject not in the three reference headings | Appendix includes allocation and arithmetic remains reconcilable. |
| Large school / slow catalogue / lost save response | Complete paging, bounded server queries, stable selection/draft, clear retry/conflict without duplicate writes. |
| Export draft/stale revision/subset/long names/multiple pages | Explicit saved revision/scope; no silent data change; repeated headers, whole rows and readable text. |

Existing backend suites to retain include TeachingAssignmentPhase6Tests,SubjectPhase5Tests,TeacherAvailabilityPhase4Tests,CurrentLessonResolverTests,TimetableGenerationTests,TimetableReviewPhase7Tests,TimetableSubstitutionPhase8Tests and student-affairs classroom/student/permit workflows. Add account/teacher/V1/V2 selector, migration and PDF tests at their actual interfaces. A design-only prototype is not evidence that any production API/security/migration tests passed.
