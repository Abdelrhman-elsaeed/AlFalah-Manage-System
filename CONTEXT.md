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
