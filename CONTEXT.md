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


## C33 / D-125 - Classroom catalogue foundation (2026-10-15)

The owner approved implementation after C32. C33 locally adds the existing-ID manager classroom catalogue and shared direct-confirmation teacher selector, including stage-local numeric sections, separate location, exact-only migration, current-year revisioned association sets and teacher scope ID joins. Legacy letters/unknown names stay retained for explicit review. Old Classes arrays are compatibility echoes, not a second source of new associations. Complete visit/timetable consumer conversion and production assignment UI/export remain later phases. See docs/specs/classroom-catalog/implementation.md.
