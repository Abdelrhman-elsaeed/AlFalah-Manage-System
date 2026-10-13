# Specification — School Dismissal Duty

**Status:** D1 implemented under explicit owner authorization; C1–C11 confirmed. D2/D3 remain planned and unimplemented.  
**Feature label:** `المناوبة`. **Administrative owner in v1:** School Manager.

## Purpose and boundaries

Assign one eligible teacher to whole-school dismissal supervision each operating day, distribute turns fairly, react to recorded teacher absence, capture the teacher's responsibility and daily observations, and retain signed official documents in school storage.

Duty is date-based dismissal supervision. Teaching lessons, lesson swaps, waiting periods and student morning attendance remain separate workflows. A duty swap changes duty dates only.

## Actors and visibility

| Actor | Access in the initial release |
|---|---|
| Actual active school manager | Configure rotation/exclusions, publish schedule, inspect all duty records, manually change assignments, resolve exceptions and retry archive operations in that school |
| Actual assigned teacher | See their upcoming duties, acknowledge their own responsibility letter, edit/submit their own daily report and read their own retained signed documents |
| Secretary | Continues to record staff attendance through existing authorization; receives no duty management solely from that role |
| Other staff / existing timetable or storage delegate | No duty management by inheritance; future feature assignment will explicitly grant it |

The duty roster can be visible to eligible school teachers using a schedule-only projection. Student observations, signatures and report content are available only to the actual report owner and authorized duty manager. Viewing the full timetable never grants access to reports. Backend ownership checks apply to every report, signature and PDF route.

## Functional requirements

### FR-01 — Workspace and context

Add `المناوبة` beside `الجدول الدراسي` inside the school timetable workspace. Keep `/timetable` usable for existing viewers and preserve personal views, editing, cloning, checkpoints, inline swaps and printing. Duty is school/year/term-scoped; opening a timetable clone does not create another duty schedule. A duty-only delegate must later be able to enter the duty tab without gaining timetable edit access.

### FR-02 — Rotation setup and publication

Duty settings contain an explicit teacher-pool selector. The manager chooses which active school teachers participate, removes excluded teachers without changing their lesson timetable, confirms the term/operating dates, sets an initial order and exclusions with effective dates/reasons, previews the distribution and publishes it. Published assignment changes notify affected teachers through existing in-app infrastructure if available, without adding a new channel dependency.

Confirmed fairness: each selected eligible teacher receives one turn in a cyclic ordered roster before the next cycle. Generation is deterministic for unchanged inputs. With unchanged eligibility and no exceptions, planned assignment counts differ by at most one. Display planned/completed/covered turns and pending compensation separately; absence does not count as completed duty. Teaching load does not weight distribution. Manual adjustments disclose their effect on fairness.

Regeneration affects explicitly selected future unlocked dates only, preserves original/actual identities, completed duty, signature snapshots and pending compensation, and previews its changes before publishing. Term renewal retains historical counts and outstanding obligations; it cannot silently cancel a pending exchange.

### FR-03 — Exclusions and closures

The manager can exclude a teacher from a date/range or the remainder of a term, and restore future eligibility. Exclusion from duty does not mean staff absence or exclusion from teaching. Holidays/closed days allocate no turn and do not advance the rotation. Exam days follow the explicit operating-day setting. Reject dates outside term/year bounds.

### FR-04 — Automatic absence exchange

Use existing school/user/date AttendanceRecord facts. Present is available; Absent/Excused is unavailable; no row is unknown. Saving or correcting attendance triggers durable reconciliation; page visits are not the only trigger.

For an unlocked duty day D with effective teacher A unavailable:

1. Search future effective duty assignments in date order within the term for the next eligible teacher B, excluding A, excluded/inactive users and users known unavailable on D. Validate published timetable membership/availability as applicable; lesson-period availability alone does not establish dismissal availability.
2. Preserve A as the original planned teacher on D and record B as the actual effective teacher, with absence reason and AttendanceRecord identity/version evidence.
3. Exchange A into B's selected future date E; retain E's original teacher and the compensation relationship. Persist both assignment changes and one exchange audit atomically.
4. If A is still absent on E, repeat against the current effective future schedule. Skip unusable candidates; do not always restart from the original printed roster.
5. Show both dates and the before/after teachers. Do not modify SchoolTimetableEntry or staff attendance.

Repeated handling of the same effective absence creates no duplicate exchange, obligation or notification. A changed attendance fact can prompt a new decision. If no candidate/future date exists, create an explicit manager exception. Never assign the same teacher twice on one day or invent completion credit.

Example: Sunday A=Mahmoud, Monday B=Ali, Tuesday C=Hassan. Mahmoud absent Sunday → Ali Sunday, Mahmoud Monday. Mahmoud absent Monday → Hassan Monday, Mahmoud Tuesday. Original roster, each exchange and actual execution remain reconstructable.

For attendance corrected to Present before acknowledgement/activity, automatically undo only the exact still-unmodified exchange pair if both dates remain unlocked. Otherwise surface manager review. A signed or completed day is never silently reassigned.

### FR-05 — Manual management

The duty manager can select a substitute, change a future date, exchange two future assignments, override an automatic choice or resolve unresolved compensation. The review shows both dates, availability/exclusion warnings, affected acknowledgement/report states and fairness impact. A reason is mandatory and the choice is protected from automatic overwrite.

A same-day emergency replacement after acknowledgement retains the original letter as historical, creates a new assignment revision and requires the replacement teacher's own acknowledgement. An existing draft is retained for inspection, not copied to another teacher as their authored report. After report submission, use a correction workflow rather than rewriting the completed assignment. An override cannot fake attendance, another person's signature or a completed report.

### FR-06 — Responsibility letter

Show school identity, academic year/term, day/date, actual teacher and manager identity, with the source obligation to supervise until the last student leaves. `اطلعت وأتعهد` is an explicit state-changing action. Merely fetching/previewing the letter does not sign it.

Use the authenticated teacher's saved handwritten signature. Freeze teacher display name, signature bytes, acknowledged-at timestamp, template version and assignment revision. A later account signature change cannot change the retained letter. Missing signature is an actionable setup state; draft/report progress remains available. Managers cannot acknowledge on behalf of teachers.

### FR-07 — Mobile daily report

The assigned teacher creates/updates a draft for that duty day and assignment revision. Include:

- Original planned teacher, actual teacher, `بدلاً عنه الأستاذ` when applicable, date/day, year/term, signatures and relevant notes.
- `أسماء الطلاب المتأخرين`: student, class, local time, action taken and optional notes.
- `أسماء الطلاب المخالفين`: student, class, local time, action taken and optional notes.
- General observations and required last-student departure time.

Use scoped school student search with active term enrollment and class auto-fill. Duty grants a minimal lookup, not student-profile, guardian or disciplinary access. Proposed fallback for an unavailable roster entry: an explicitly marked manual name/class line with an explanation; it never creates a Student record. Allow the same student in both categories; exact duplicate lines warn for confirmation.

No observations is a valid report when the teacher explicitly confirms both categories have none. Do not treat zero lines as missing work automatically. Validate required fields and school-local times; reject departure before dismissal or before a recorded observation unless the duty manager records an explicit timing exception. Support overflow pages; do not enforce the paper's seven-line limit.

### FR-08 — Submission and correction

`توقيع وإرسال التقرير` validates current assignment, acknowledgement, signature and required last-departure time. Freeze all report fields and signature at submission. Commit submitted revision, audit and two distinct document archive intents as needed; Google failures do not roll back a submitted report.

Submission is idempotent. Further edits require an authorized correction with reason, preserved prior version and teacher re-signature. Review by management is visible independently and does not fabricate manager signatures or delay initial archive by default. Show overdue submission using real timestamps.

### FR-09 — Official output and archive

Provide an official term duty roster, responsibility letter and daily report. Letter acknowledgement queues its signed PDF; report submission queues its signed report PDF. The roster is an export; automatic retention of each roster publication is optional and outside the initial minimum.

Letter and report archive under the approved school root / `المناوبات` / academic year / term / actual teacher / duty date hierarchy, and retain original/substitute context inside the documents. Suggested filenames:

```text
خطاب المناوبة - {actual teacher} - {yyyy-MM-dd}.pdf
تقرير المناوبة - {actual teacher} - {yyyy-MM-dd}.pdf
```

Names are display values, not identity keys. Distinguish duplicate names and revisions through stable internal identities and version metadata. Prevent path characters from escaping the selected school boundary. Two PDFs are separate artifacts, not a single overwritten upload.

Archive status is independent: Pending, Processing, Completed, RetryScheduled or NeedsAttention, with actionable configuration/connection states. Retry uses the frozen snapshot and reserved provider identity and does not duplicate documents. Authorized teacher reads use the duty boundary even if the file resides outside the teacher's ordinary grant.

## Acceptance scenarios

| ID | Given / when | Required result |
|---|---|---|
| AC-01 | Ten eligible teachers and twenty operating days | Two planned turns each; repeat generation is identical |
| AC-02 | A teacher excluded or a date closed | No assignment to that teacher/date; closure consumes no turn |
| AC-03 | Mahmoud absent, Ali next with future date E | Ali covers today; Mahmoud takes E; original names and one audit retained |
| AC-04 | Mahmoud remains absent on E | Repeat against current effective queue without duplicate compensation |
| AC-05 | Next candidate absent/excluded/inactive | Skip with reason; no usable candidate produces manager exception |
| AC-06 | Secretary saves the same attendance twice, or worker retries | One effective exchange and one notification set |
| AC-07 | Attendance corrected after a signed letter | No silent reassignment; manager review with historical letter retained |
| AC-08 | Manager manually changes the pair | Reason, before/after dates and protection retained; automation respects choice |
| AC-09 | Teacher acknowledges with saved signature, later edits account signature | Original letter remains byte/snapshot faithful to acknowledgement |
| AC-10 | Original teacher absent and substitute submits | Original absence is shown; substitute's own signature appears; absent teacher not signed |
| AC-11 | Teacher uses phone, enters over seven student lines, no violations | Accessible cards, explicit no-violations state, all rows in paginated PDF |
| AC-12 | No stored signature | Draft saves; acknowledgement/submission gives setup guidance; no fake signature |
| AC-13 | Double submit or lost response | Same committed report revision and archive intent returned |
| AC-14 | Drive succeeds but persistence/response fails | Recover exact reserved upload identity; no second logical artifact |
| AC-15 | Drive disconnected or disabled | Report stays submitted, PDF can be generated from frozen data, archive visibly awaits configuration |
| AC-16 | Another teacher/school forges report, student or file ID | Backend denies access with no report/signature/provider metadata leak |
| AC-17 | Timetable clone/regeneration/lesson swap | Existing lesson workflows pass regression; no duplicate duty schedule |
| AC-18 | Concurrent manual change and automatic reconciliation | One valid revision wins; loser gets conflict/retries current state; no half-exchange |
| AC-19 | Compensation has no remaining date in term | Unresolved obligation is visible and retained through renewal |
| AC-20 | Signed report corrected | Prior report/bytes retained; correction reason and re-signature link the new revision |

## Completion criteria

The feature is complete after D1–D3 pass business/security/failure verification, official PDFs are visually checked against supplied forms, mobile/desktop RTL flows meet `ux.md`, and a configured school demonstrates the actual archive write/read/retry path. Record unavailable live validation explicitly; mocks alone do not establish live storage acceptance.
