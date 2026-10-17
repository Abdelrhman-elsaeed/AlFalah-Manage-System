# Student Affairs Context

This context manages students' school-day attendance, conduct, recognition, guardian communication, and controlled entry/exit workflows within one school tenant. The vocabulary and business rules below are locked for the future implementation phase; biometric, Noor-export, and 14-quality-form integration details remain blocked pending client input.

## Language

**Morning Arrival Delay**:
A student's arrival at school after the configured morning cutoff. The core fact is source-neutral while biometric integration is blocked.
_Avoid_: Attendance, session delay

**Daily Student Attendance**:
The school's daily presence or absence fact recorded from the Secretary's classroom roster. The Secretary submits only absent students; every other actively enrolled roster student is derived as present.
_Avoid_: Staff attendance, biometric attendance

**Absent**:
A daily student-attendance absence with no accepted excuse. It remains an official absence and participates in the per-term 3/5/10 penalty count.
_Avoid_: Pending excuse, excused absence

**Absent Excused (`AbsentExcused`)**:
A daily absence whose guardian excuse has been accepted by the Student Affairs Officer. It remains an absence for official/future Noor reporting but is excluded from the penalty count.
_Avoid_: Present, deleted absence

**Penalty Absence Day**:
A distinct daily attendance row currently in `Absent` status for the student and term. `AbsentExcused` never contributes.
_Avoid_: Total absence, pending-excuse count

**Session Delay**:
A student's late entry to a particular lesson, recorded by the teacher responsible for that period.
_Avoid_: Morning delay, academic concern

**Academic Concern**:
A teacher-recorded concern about a student's academic progress that participates in the three-occurrence escalation rule.
_Avoid_: Session delay, behavior incident

**Behavior Incident**:
A recorded student conduct violation with a category and severity. Its per-term escalation metric is dynamically rebuilt from current active/countable incidents; correction, soft deletion, or a countability-changing severity downgrade can decrease it.
_Avoid_: Academic concern

**Student Recognition**:
A positive record of student excellence or distinction.
_Avoid_: Positive incident

**Classroom Entry Permit**:
Authorization issued by Student Affairs for a student to enter the current lesson after a justified delay.
_Avoid_: Gate pass, bathroom pass

**Gate Pass**:
A guardian-requested, Student-Affairs-approved authorization for a student to leave the school through Security.
_Avoid_: Classroom entry permit

**Pickup Identity Hint**:
Guardian-entered text naming/describing the expected pickup person. It is not a registered delegate identity; Security relies on visual/manual verification or a guardian-provided screenshot and records the verification method.
_Avoid_: Driver account, delegate registry

**Student Referral**:
An internal case handed to a Social Worker for assessment and action.
_Avoid_: Guardian summons

**Guardian Summons**:
A scheduled request for a guardian to attend school, followed through Pending, Attended, Under Observation, and Improved.
_Avoid_: Notification, referral

**Student Affairs Settings**:
The school-wide, versioned threshold policy edited by the Student Affairs Officer. Defaults are morning delay 10, behavior multiples of 10, academic concerns 3, classroom-entry permits 5, and absence levels 3/5/10.
_Avoid_: Hard-coded constants, UI-only configuration

**Pending Guardian Dispatch**:
An Officer decision queue item created for a behavior incident or academic concern. It is not yet a guardian notification; approval creates the guardian-recipient message, while suppression requires an audit reason.
_Avoid_: Immediate absence/delay notification

**Automation Impact Review**:
An Officer review flag on an unresolved automatic summons when recalculation shows its source threshold is no longer satisfied. It preserves history and requires an explicit retain/cancel/close decision.
_Avoid_: Automatic deletion, automatic improvement

**School Oversight Aggregate**:
Non-identifying School Manager statistics such as total present, absent, and excused per class. It excludes Social Worker case notes, counseling/session details, message bodies, guardian identifiers, and evidence.
_Avoid_: Confidential case view

## Academic placement and teaching

**School Grade (الصف الدراسي)**:
A level within one school stage, such as the first intermediate grade. Its ordinal starts within its stage rather than identifying a teaching section or an academic term.
_Avoid_: Classroom, semester

**Classroom Section (الفصل)**:
A numbered group within a school grade for a school and academic year, such as first intermediate, section2. It is distinct from the physical teaching room.
_Avoid_: Academic term, room, free-form class name

**Teacher Classroom Association (فصول المعلم)**:
The current-year classroom sections a teacher declares they teach. The owner permits teachers to select and confirm their own sections directly; this is separate from allocating subject periods.
_Avoid_: Teaching assignment, approval request

**Teaching Assignment (إسناد التدريس)**:
The allocation of a classroom's subject-period requirement to one or more teachers in a timetable setup. Single teaching, co-teaching and split quota describe how teachers share that requirement.
_Avoid_: Teacher classroom association, published lesson

**Weekly Teaching Load (نصاب الحصص)**:
The number of allocated weekly periods for a teacher, including specialization and other subjects. Co-teachers each carry the complete co-taught period load; a split allocation carries only its allocated share.
_Avoid_: Number of classrooms, number of subjects

**Assignment Statement (كشف إسنادات المواد)**:
A teacher-row statement grouping assigned sections under their grades and separating specialization periods from other-subject periods, with a total teacher load.
_Avoid_: Published timetable, duty roster

**Scheduled Standby Slot (خانة انتظار مجدولة)**:
A weekly period reserved for a teacher to be available for cover. It has no assigned classroom or subject until a dated coverage assignment is made.
_Avoid_: Free period, dismissal duty, completed cover

**Waiting-Period Coverage (تغطية حصة انتظار)**:
A dated assignment for a replacement teacher to cover a particular lesson while retaining its original teacher, classroom and subject.
_Avoid_: Structural lesson swap, permanent teaching assignment

**Waiting-Period Execution (تنفيذ حصة انتظار)**:
The covering teacher's record of what actually happened during an assigned waiting period. Assignment alone does not establish execution.
_Avoid_: Scheduled standby, automatic completion

**Eligible Waiting-Period Teacher (المعلم المؤهل للانتظار)**:
A teacher explicitly scheduled as standby for the required period who also satisfies availability, attendance and collision constraints. A free period alone does not qualify a teacher, in either manual or fair distribution.
_Avoid_: Any free teacher, unrestricted manual substitute

**Waiting-Period Distribution Mode (طريقة توزيع الانتظار)**:
The responsible administrator's choice at distribution time between selecting eligible standby teachers manually and reviewing a fair-distribution proposal. Absence generates coverage needs and suggestions; administrator approval makes an assignment effective.
_Avoid_: Automatic final assignment, fixed school-wide distribution mode

**Weekly Standby Allocation Mode (طريقة توزيع الانتظار الأسبوعي)**:
The manager's choice between manual placement and fair allocation of recurring standby periods after the teaching timetable is completed. This choice is independent from the mode used to assign dated absence coverage.
_Avoid_: Daily coverage mode, interleaved teaching generation

**Eligible Standby Capacity (السعة المتاحة للانتظار)**:
The actual study periods where a participating teacher is available and free from teaching or another blocking commitment. It is an upper bound on planned standby, not a requirement to fill every free period.
_Avoid_: Teaching quota remainder, fixed reserve quota, coverage demand

**Waiting-Period Register (سجل توزيع حصص الانتظار)**:
The dated school record grouping the absent teacher's lessons with their covering teachers, execution narrative, signatures and notes. Its official form is QF71-B-2-1; it is separate from the weekly timetable showing planned standby availability.
_Avoid_: Weekly standby grid, dismissal-duty report


## C33 / D-125 - Classroom catalogue foundation (2026-10-15)

The owner approved implementation after C32. C33 locally adds the existing-ID manager classroom catalogue and shared direct-confirmation teacher selector, including stage-local numeric sections, separate location, exact-only migration, current-year revisioned association sets and teacher scope ID joins. Legacy letters/unknown names stay retained for explicit review. Old Classes arrays are compatibility echoes, not a second source of new associations. Complete visit/timetable consumer conversion and production assignment UI/export remain later phases. See docs/specs/classroom-catalog/implementation.md.
## C38 / D-130 — Weekly standby phase 1 (2026-10-16)

Weekly standby is implemented in the existing timetable's separate manager tab after complete validated teaching. Manual/fair selection, visible editable demand (default1 per teaching period,0 otherwise), exclusions, natural capacity, explicit locks, reviewed proposals, undo and guarded draft save/publication are delivered. No arbitrary per-teacher quota is introduced. New additive settings/lock/source metadata preserves legacy manual intent, history/clone/restore/regeneration and teaching IDs on weekly apply. Present/future confirmed coverage guards the older full-entry replacement path.124 backend/SQL and5 Angular tests and actual responsive SQL/browser workflows pass; details and acceptance limits: docs/specs/intelligent-timetable/standby-periods/weekly-implementation.md. Daily coverage/teacher execution and official QF71-B-2-1 remain phases2/3; do not conflate the existing weekly timetable PDF with that register.

## C39 / D-131 — Daily standby coverage, phase 2 (2026-10-16)

Phase 2 adds durable attendance-driven needs, independent manual/fair preview and atomic administrator approval through the existing substitution/version/outbox ledger. Every covered period requires scheduled standby; free-only teachers cannot be assigned through old or new endpoints. Fairness balances distinct actual signed execution periods plus pending/proposed term commitments; manual choices are reserved first. Existing timetable manager/delegated-editor authority is reused. Full-day Absent/Excused facts are the input; do not infer partial-day attendance.

The owner explicitly confirms separate signed receipt and signed execution. `/standby-tasks`, teacher navigation/dashboard and existing internal notifications lead to revision-safe drafts, signed receipt, execution after actual period end, inability reporting and administrator correction/release. Signature bytes and assignment data are frozen per action. Reassignment preserves the old draft/receipt and denies the old teacher further edits; signed execution permits retained correction rather than reassignment. Corrected absence/publication/lesson timing requires review. Drafts and notification delivery are never execution.

333 focused backend/SQL and 11 Angular checks pass; real SQL fault injection proves whole-batch rollback, replay and retained signatures. Actual authenticated browser workflows cover 390/768/1440/1920. See docs/specs/intelligent-timetable/verification/standby-phase2/README.md for evidence and limits. The broader unrelated SQLite messaging fixture failure and unchanged VisitsV2/storage production CSS budgets are recorded. Phase 3 official register/PDF/print acceptance is unstarted. No provider writes or deployment.

**Coverage Need (احتياج تغطية)**: A school/date original lesson member or block prepared from recorded absence. It does not change ownership until approved in the existing substitution ledger.

**Receipt Acknowledgement (إقرار الاستلام)**: The actual assigned teacher's independently signed acknowledgement of the assignment; it is not an execution report.

**Signed Execution (تنفيذ موقّع)**: The actual teacher's submitted narrative/notes and signature snapshot after the lesson ends. Corrections append another retained version and count that need once in fairness.

## C40 / D-132 — Official standby register, phase 3 (2026-10-17)

All three standby phases are delivered locally. `CoverageRegisterController -> CoverageRegisterService -> ICoverageRegisterRepository` issues whole-school dated QF71-B-2-1 through the existing manager ledger. The separate dialog reuses fit-width PDF.js and exact blob download with paged retained history. It never exports only the current filtered/page slice. Management/delegation and school scope apply before source/history/content reads; ordinary teachers cannot read the whole-school register.

The source's main signature column is signed receipt. Actual independent signed execution proof is in the appendix. Project only current assignment/actual-owner evidence; exclude unsent drafts. Open correction keeps the last signed execution with an incomplete status. Three groups of eight printed rows preserve the source; extra teachers/periods and long text continue. Use approved platform/available school letterhead assets, leave unconfigured deputy name/signature blank; photographed logos/names/signatures are not assets.

Append-only `CoverageRegisterArtifact` freezes source JSON/fingerprint, renderer version, human school/date revision and exact PDF/hash. Issue replay returns retained bytes after later changes; uncached stale sources conflict. Migration `20261017072905_AddCoverageRegisterArtifacts` adds the table/indexes; local schema75 preserves original values across45 checked tables after fresh SQL verification. The existing transaction boundary now maps nested EF-wrapped SQL1205 to the established recoverable conflict.

342 focused backend/SQL checks across regression and fresh integrated SQL runs,16 Angular checks, six independently parsed/rasterized PDF cases and actual HTTP/browser390/768/1440/1920 checks pass. Existing VisitsV2/storage production CSS budget failures and unrelated SQLite fixture failure remain recorded. A timing-bounded repair check failed under concurrent build load and passed unchanged when rerun without that load; do not hide the retained failed log. PDF.js extraction reveals contextual Amiri glyph NUL mappings despite correct visual shaping; full Arabic copy/search/assistive access is not claimed. Owner appearance/printer/official branding acceptance remains pending; no unagreed Drive writer, provider file writes, commit or production rollout. Existing local background workers may refresh tokens/read metadata during normal startup. See docs/specs/intelligent-timetable/verification/standby-phase3/README.md. Stop at this phase boundary.

**Register Issue (نسخة سجل الانتظار)**: An immutable school/date source snapshot and exact PDF retained independently of later draft, signature, publication or staff changes. Viewing/downloading history is read-only; issue is explicit and replay-safe.
