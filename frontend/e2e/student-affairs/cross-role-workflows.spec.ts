import { expect, test, type Browser } from '@playwright/test';
import { apiForRole, successful, type PageResult } from '../support/api-session';
import { openRoleSession, setE2EClock } from '../support/role-session';

interface StudentSummary { id: number; studentNumber: string; displayName: string }
interface GuardianStudent { student: StudentSummary }
interface Classroom { id: number; label: string }
interface AttendanceRow {
  attendanceId: number | null;
  student: StudentSummary;
  status: string;
  excuseStatus: string | null;
  rowVersion: string | null;
}
interface AttendanceSheet { rosterRevision: string; rows: AttendanceRow[] }
interface AttendanceRecord {
  id: number;
  date: string;
  status: string;
  excuseStatus: string | null;
  rowVersion: string;
}
interface Excuse { id: number; status: string; rowVersion: string }
interface ExcuseQueueItem { attendance: AttendanceRow; excuse: Excuse }
interface GuardianNotification { id: number; type: string; studentId: number }
interface TeacherContext {
  resolutionKind: string;
  currentPeriod: { timetableEntryId: number; effectiveInstructor: { displayName: string } } | null;
  roster: StudentSummary[];
}
interface BehaviorIncident { id: number; student: StudentSummary; dispatchDecision: string; rowVersion: string }
interface PendingDispatch { id: number; factType: string; factId: number; rowVersion: string }
interface GatePass { id: number; status: string; student: StudentSummary; rowVersion: string }
interface SecurityGatePass { id: number; status: string; student: StudentSummary; rowVersion: string; exitedAt?: string | null }
interface EntryPermit { id: number; status: string; student: StudentSummary; rowVersion: string; targetTeacher: { displayName: string } | null }
interface Referral {
  id: number;
  student: StudentSummary;
  status: string;
  assignedSocialWorker: { userId: string; displayName: string } | null;
  rowVersion: string;
  sourceSnapshot: {
    sourceType: string;
    sourceEntityId: number | null;
    countSnapshot: number | null;
    thresholdSnapshot: number | null;
  };
}
interface SocialWorkerOption { userId: string; displayName: string }
interface GuardianLink { guardian: { id: number; displayName: string }; isActive: boolean }
interface Summon {
  id: number;
  student: StudentSummary;
  status: string;
  rowVersion: string;
  requiresOfficerReview: boolean;
  referralId?: number | null;
  sourceCountSnapshot?: number | null;
  thresholdSnapshot?: number | null;
  guardianNotifiedAt?: string | null;
}
interface OfficeHourSlot {
  stableKey: string;
  dayOfWeek: string;
  periodSequence: number;
  isEligible: boolean;
  isSelected: boolean;
  isConflicted: boolean;
}
interface OfficeHours {
  instructorId: number;
  schoolTimetableId: number;
  timetableRevision: number;
  rowVersion: string;
  slots: OfficeHourSlot[];
}
interface TeacherOption { instructorProfileId: number; displayName: string }
interface Conversation { id: number; subject: string; rowVersion: string }
interface ConversationMessage {
  id: number;
  body: string;
  deliveryState: string;
  disposition: string;
  nextEligibleSendAt: string | null;
}
interface TimetableEntry {
  instructorProfileId: number;
  day: string;
  period: number;
  entryType: string;
  classLabel: string | null;
  subject: string | null;
  classroomId: number | null;
  subjectId: number | null;
  classSubjectRequirementId: number | null;
  roomId: number | null;
}
interface Timetable {
  id: number;
  academicYearId: number;
  semester: string;
  title: string;
  isPublished: boolean;
  revision: number;
  entries: TimetableEntry[];
}

const date = '2026-09-30';
const pageQuery = 'pageNumber=1&pageSize=100';

test.describe.serial('Student Affairs cross-role workflows', () => {
  test('Scenario A — Secretary → Guardian → Officer → Guardian preserves attendance, excuse idempotency, and privacy', async ({ browser }) => {
    await setE2EClock('2026-09-30T03:50:00.000Z');
    const secretary = await apiForRole('secretary');
    const guardian = await apiForRole('guardian');
    const officer = await apiForRole('officer');
    const crossOfficer = await apiForRole('crossOfficer');

    const linked = await successful<GuardianStudent[]>(await guardian.get('/api/v1/guardian/students'));
    expect(linked).toHaveLength(1);
    const student = linked[0].student;

    const classrooms = await successful<PageResult<Classroom>>(
      await secretary.get(`/api/v1/classrooms?${pageQuery}`)
    );
    const classroom = classrooms.items.find(item => item.label === 'E2E-1-A');
    expect(classroom).toBeDefined();

    const initial = await successful<AttendanceSheet>(
      await secretary.get(`/api/v1/student-attendance/sheet?date=${date}&classroomId=${classroom!.id}`)
    );
    const historyBefore = await successful<{ records: AttendanceRecord[]; absenceMetric: { eligibleTermCount: number } }>(
      await guardian.get(`/api/v1/student-attendance/students/${student.id}`)
    );
    expect(initial.rows).toHaveLength(2);
    const unlinked = initial.rows.find(row => row.student.studentNumber === 'E2E-STUDENT-UNLINKED');
    expect(unlinked).toBeDefined();

    const guardianUnlinked = await guardian.get(`/api/v1/guardian/students/${unlinked!.student.id}/summary`);
    expect([403, 404]).toContain(guardianUnlinked.status());
    const crossSchoolRead = await crossOfficer.get(`/api/v1/students/${student.id}`);
    expect([403, 404]).toContain(crossSchoolRead.status());

    const attendanceKey = 'w10-a-attendance-20260930';
    const submitBody = {
      date,
      classroomId: classroom!.id,
      absentStudentIds: [student.id],
      rosterRevision: initial.rosterRevision
    };
    const submitted = await successful<AttendanceSheet>(
      await secretary.put('/api/v1/student-attendance/sheet', {
        headers: { 'Idempotency-Key': attendanceKey },
        data: submitBody
      })
    );
    expect(submitted.rows.find(row => row.student.id === student.id)?.status).toBe('Absent');
    expect(submitted.rows.find(row => row.student.id === unlinked!.student.id)?.status).toBe('Present');

    const replayed = await successful<AttendanceSheet>(
      await secretary.put('/api/v1/student-attendance/sheet', {
        headers: { 'Idempotency-Key': attendanceKey },
        data: submitBody
      })
    );
    expect(replayed.rows.find(row => row.student.id === student.id)?.attendanceId)
      .toBe(submitted.rows.find(row => row.student.id === student.id)?.attendanceId);

    await expect.poll(async () => {
      const notifications = await successful<PageResult<GuardianNotification>>(
        await guardian.get(`/api/v1/guardian/students/${student.id}/notifications?${pageQuery}`)
      );
      return notifications.items.filter(item => item.type === 'GuardianImmediate').length;
    }, { timeout: 20_000 }).toBe(1);

    const attendance = submitted.rows.find(row => row.student.id === student.id)!;
    const pdf = Buffer.from('%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF');
    const excuseKey = 'w10-a-excuse-20260930';
    const upload = () => guardian.post(`/api/v1/student-attendance/${attendance.attendanceId}/excuses`, {
      headers: { 'Idempotency-Key': excuseKey },
      multipart: {
        excuseType: 'Medical',
        notes: 'W10 deterministic medical excuse',
        attachment: { name: 'w10-medical-excuse.pdf', mimeType: 'application/pdf', buffer: pdf }
      }
    });
    const submittedExcuse = await successful<Excuse>(await upload(), 202);
    const replayedExcuse = await successful<Excuse>(await upload(), 202);
    expect(replayedExcuse.id).toBe(submittedExcuse.id);

    const queue = await successful<PageResult<ExcuseQueueItem>>(
      await officer.get(`/api/v1/student-attendance/excuses/pending?${pageQuery}`)
    );
    const queued = queue.items.find(item => item.excuse.id === submittedExcuse.id);
    expect(queued).toBeDefined();
    const accepted = await successful<Excuse>(
      await officer.post(`/api/v1/student-attendance/excuses/${submittedExcuse.id}/accept`, {
        data: { reviewNote: 'W10 accepted', rowVersion: queued!.excuse.rowVersion }
      })
    );
    expect(accepted.status).toBe('Accepted');

    let history!: { records: AttendanceRecord[]; absenceMetric: { eligibleTermCount: number } };
    await expect.poll(async () => {
      history = await successful<{ records: AttendanceRecord[]; absenceMetric: { eligibleTermCount: number } }>(
        await guardian.get(`/api/v1/student-attendance/students/${student.id}`)
      );
      return history.absenceMetric.eligibleTermCount;
    }, { timeout: 20_000 }).toBe(historyBefore.absenceMetric.eligibleTermCount);
    const finalAttendance = history.records.find(row => row.id === attendance.attendanceId);
    expect(finalAttendance?.status).toBe('AbsentExcused');

    const afterCorrectionSheet = await successful<AttendanceSheet>(
      await secretary.get(`/api/v1/student-attendance/sheet?date=${date}&classroomId=${classroom!.id}`)
    );
    expect(afterCorrectionSheet.rows.find(row => row.student.id === student.id)?.status).toBe('AbsentExcused');

    await assertRolePageContains(browser, 'guardian', '/student-affairs/guardian/excuses', student.displayName);
    await Promise.all([secretary.dispose(), guardian.dispose(), officer.dispose(), crossOfficer.dispose()]);
  });

  test('Scenario B — Instructor → Officer → Guardian delivers only the approved behavior notification', async ({ browser }) => {
    await setE2EClock('2026-09-30T06:00:00.000Z');
    const instructor = await apiForRole('instructor');
    const substitute = await apiForRole('substituteInstructor');
    const officer = await apiForRole('officer');
    const guardian = await apiForRole('guardian');

    const linked = await successful<GuardianStudent[]>(await guardian.get('/api/v1/guardian/students'));
    const student = linked[0].student;
    const referralsBefore = await successful<PageResult<Referral>>(
      await officer.get(`/api/v1/referrals?studentId=${student.id}&${pageQuery}`)
    );
    const before = await successful<PageResult<GuardianNotification>>(
      await guardian.get(`/api/v1/guardian/students/${student.id}/notifications?${pageQuery}`)
    );
    const behaviorNotificationsBefore = before.items.filter(item => item.type === 'GuardianApprovalRequired').length;

    const context = await successful<TeacherContext>(
      await instructor.get('/api/v1/teacher/student-affairs/current-context')
    );
    expect(context.resolutionKind).toBe('ActiveLesson');
    expect(context.currentPeriod).not.toBeNull();
    expect(context.roster.map(item => item.id)).toContain(student.id);

    const substituteContext = await successful<TeacherContext>(
      await substitute.get('/api/v1/teacher/student-affairs/current-context')
    );
    expect(substituteContext.resolutionKind).not.toBe('ActiveLesson');

    const createBehavior = async (description: string) => successful<BehaviorIncident>(
      await instructor.post('/api/v1/behaviors', {
        data: {
          studentId: student.id,
          schoolTimetableEntryId: context.currentPeriod!.timetableEntryId,
          category: 'W10-CONDUCT',
          severity: 'Low',
          description,
          occurredAt: null,
          location: 'E2E classroom',
          immediateAction: 'Verbal reminder'
        }
      }),
      201
    );
    const approvedFact = await createBehavior('W10 behavior approved for guardian delivery');
    const suppressedFact = await createBehavior('W10 behavior suppressed from guardian delivery');
    for (let index = 3; index <= 10; index++)
      await createBehavior(`W10 threshold behavior ${index}`);

    const wrongRosterAttempt = await instructor.post('/api/v1/behaviors', {
      data: {
        studentId: 2147483000,
        schoolTimetableEntryId: context.currentPeriod!.timetableEntryId,
        category: 'W10-CONDUCT',
        severity: 'Low',
        description: 'Must be denied outside the current roster',
        occurredAt: null,
        location: null,
        immediateAction: null
      }
    });
    expect([403, 404]).toContain(wrongRosterAttempt.status());

    let pending: PendingDispatch[] = [];
    await expect.poll(async () => {
      const result = await successful<PageResult<PendingDispatch>>(
        await officer.get(`/api/v1/notifications/pending-dispatch?${pageQuery}`)
      );
      pending = result.items.filter(item =>
        item.factType === 'BehaviorIncident'
        && [approvedFact.id, suppressedFact.id].includes(item.factId));
      return pending.length;
    }, { timeout: 20_000 }).toBe(2);

    const approval = pending.find(item => item.factId === approvedFact.id)!;
    const suppression = pending.find(item => item.factId === suppressedFact.id)!;
    await successful<PendingDispatch>(
      await officer.post(`/api/v1/notifications/${approval.id}/approve`, {
        data: { rowVersion: approval.rowVersion }
      })
    );
    await successful<PendingDispatch>(
      await officer.post(`/api/v1/notifications/${suppression.id}/suppress`, {
        data: { reason: 'W10 privacy decision', rowVersion: suppression.rowVersion }
      })
    );

    const approvedIncident = await successful<BehaviorIncident>(
      await officer.get(`/api/v1/behaviors/${approvedFact.id}`)
    );
    const suppressedIncident = await successful<BehaviorIncident>(
      await officer.get(`/api/v1/behaviors/${suppressedFact.id}`)
    );
    expect(approvedIncident.dispatchDecision).toBe('Approved');
    expect(suppressedIncident.dispatchDecision).toBe('Suppressed');

    await expect.poll(async () => {
      const notifications = await successful<PageResult<GuardianNotification>>(
        await guardian.get(`/api/v1/guardian/students/${student.id}/notifications?${pageQuery}`)
      );
      return notifications.items.filter(item => item.type === 'GuardianApprovalRequired').length;
    }).toBe(behaviorNotificationsBefore + 1);

    await expect.poll(async () => {
      const referrals = await successful<PageResult<Referral>>(
        await officer.get(`/api/v1/referrals?studentId=${student.id}&${pageQuery}`)
      );
      return referrals.totalCount;
    }, { timeout: 25_000 }).toBe(referralsBefore.totalCount + 1);
    const behaviorThresholdReferrals = await successful<PageResult<Referral>>(
      await officer.get(`/api/v1/referrals?studentId=${student.id}&${pageQuery}`)
    );
    expect(behaviorThresholdReferrals.items.filter(item =>
      item.sourceSnapshot.sourceType === 'Behavior'
      && item.sourceSnapshot.countSnapshot === 10
      && item.sourceSnapshot.thresholdSnapshot === 10)).toHaveLength(1);

    await assertRolePageContains(browser, 'officer', '/student-affairs/notification-approvals');
    await assertRolePageContains(browser, 'guardian', '/student-affairs/guardian/notifications');
    await Promise.all([instructor.dispose(), substitute.dispose(), officer.dispose(), guardian.dispose()]);
  });

  test('Scenario C — Guardian → Officer → substitute Instructor → Security → Guardian executes one authorized exit', async ({ browser }) => {
    await setE2EClock('2026-09-30T03:50:00.000Z');
    const guardian = await apiForRole('guardian');
    const officer = await apiForRole('officer');
    const instructor = await apiForRole('instructor');
    const substitute = await apiForRole('substituteInstructor');
    const security = await apiForRole('security');

    const linked = await successful<GuardianStudent[]>(await guardian.get('/api/v1/guardian/students'));
    const student = linked[0].student;
    const payload = {
      studentId: student.id,
      desiredExitTime: '2026-09-30T04:15:00.000Z',
      reason: 'W10 medical appointment',
      pickupPersonName: 'E2E Parent',
      pickupRelationship: 'Father',
      pickupIdentityHint: 'last-four-001'
    };
    const created = await successful<GatePass>(
      await guardian.post('/api/v1/gate-passes', {
        headers: { 'Idempotency-Key': 'w10-c-gate-pass' },
        data: payload
      }),
      201
    );
    const replayed = await successful<GatePass>(
      await guardian.post('/api/v1/gate-passes', {
        headers: { 'Idempotency-Key': 'w10-c-gate-pass' },
        data: payload
      }),
      201
    );
    expect(replayed.id).toBe(created.id);
    await assertRolePageContains(browser, 'officer', '/student-affairs/gate-passes', student.displayName);

    const approved = await successful<GatePass>(
      await officer.post(`/api/v1/gate-passes/${created.id}/approve`, {
        data: {
          windowStartsAt: '2026-09-30T04:00:00.000Z',
          windowEndsAt: '2026-09-30T04:45:00.000Z',
          approvalNote: 'W10 approved window',
          rowVersion: created.rowVersion
        }
      })
    );
    expect(approved.status).toBe('Approved');

    await setE2EClock('2026-09-30T04:10:00.000Z');
    const originalTeacherAttempt = await instructor.post(
      `/api/v1/gate-passes/${created.id}/teacher-acknowledgement`,
      { data: { rowVersion: approved.rowVersion } }
    );
    expect([403, 404]).toContain(originalTeacherAttempt.status());

    await assertRolePageContains(browser, 'substituteInstructor', '/student-affairs/teacher', student.displayName);
    const teacherAcknowledged = await successful<GatePass>(
      await substitute.post(`/api/v1/gate-passes/${created.id}/teacher-acknowledgement`, {
        data: { rowVersion: approved.rowVersion }
      })
    );

    await assertRolePageContains(browser, 'security', '/student-affairs/security', student.displayName);
    const securityAcknowledged = await successful<SecurityGatePass>(
      await security.post(`/api/v1/gate-passes/${created.id}/security-acknowledgement`, {
        data: { rowVersion: teacherAcknowledged.rowVersion }
      })
    );
    expect(securityAcknowledged.status).toBe('SecurityAcknowledged');

    const staleExit = await security.post(`/api/v1/gate-passes/${created.id}/exit`, {
      data: {
        verificationMethod: 'Visual',
        verificationNote: 'W10 verified pickup identity',
        gateNote: 'Must reject stale row version',
        rowVersion: approved.rowVersion
      }
    });
    expect(staleExit.status()).toBe(409);

    await setE2EClock('2026-09-30T04:20:00.000Z');
    const exited = await successful<SecurityGatePass>(
      await security.post(`/api/v1/gate-passes/${created.id}/exit`, {
        data: {
          verificationMethod: 'Visual',
          verificationNote: 'W10 verified pickup identity',
          gateNote: 'W10 exit completed',
          rowVersion: securityAcknowledged.rowVersion
        }
      })
    );
    expect(exited.status).toBe('Exited');
    expect(exited.exitedAt).not.toBeNull();

    const mine = await successful<PageResult<GatePass>>(
      await guardian.get(`/api/v1/gate-passes/mine?${pageQuery}`)
    );
    expect(mine.items.find(item => item.id === created.id)?.status).toBe('Exited');
    await assertRolePageContains(browser, 'manager', '/student-affairs/gate-passes/audit', student.displayName);

    await Promise.all([
      guardian.dispose(), officer.dispose(), instructor.dispose(), substitute.dispose(), security.dispose()
    ]);
  });

  test('Scenario D — Officer → Instructor → Guardian acknowledges one current-lesson entry permit', async ({ browser }) => {
    await setE2EClock('2026-09-30T06:00:00.000Z');
    const officer = await apiForRole('officer');
    const instructor = await apiForRole('instructor');
    const substitute = await apiForRole('substituteInstructor');
    const guardian = await apiForRole('guardian');
    const linked = await successful<GuardianStudent[]>(await guardian.get('/api/v1/guardian/students'));
    const student = linked[0].student;

    const issued = await successful<EntryPermit>(
      await officer.post('/api/v1/classroom-entry-permits', {
        data: {
          studentId: student.id,
          reason: 'W10 late return to current lesson',
          validFrom: '2026-09-30T05:55:00.000Z',
          validUntil: '2026-09-30T06:20:00.000Z'
        }
      }),
      201
    );
    expect(issued.status).toBe('Issued');
    expect(issued.targetTeacher).not.toBeNull();

    const substituteAttempt = await substitute.post(
      `/api/v1/classroom-entry-permits/${issued.id}/acknowledge`,
      { data: { rowVersion: issued.rowVersion } }
    );
    expect([403, 404]).toContain(substituteAttempt.status());

    await assertRolePageContains(browser, 'instructor', '/student-affairs/teacher', student.displayName);
    const acknowledged = await successful<EntryPermit>(
      await instructor.post(`/api/v1/classroom-entry-permits/${issued.id}/acknowledge`, {
        data: { rowVersion: issued.rowVersion }
      })
    );
    expect(acknowledged.status).toBe('AcknowledgedByTeacher');

    const staleAcknowledge = await instructor.post(`/api/v1/classroom-entry-permits/${issued.id}/acknowledge`, {
      data: { rowVersion: issued.rowVersion }
    });
    expect([400, 409]).toContain(staleAcknowledge.status());

    const guardianPermits = await successful<PageResult<EntryPermit>>(
      await guardian.get(`/api/v1/classroom-entry-permits?studentId=${student.id}&${pageQuery}`)
    );
    expect(guardianPermits.items.find(item => item.id === issued.id)?.status).toBe('AcknowledgedByTeacher');
    await assertRolePageContains(browser, 'guardian', '/student-affairs/guardian/activity', student.displayName);
    await Promise.all([officer.dispose(), instructor.dispose(), substitute.dispose(), guardian.dispose()]);
  });

  test('Scenario E — Officer → assigned SocialWorker → Guardian completes the referral and summons lifecycle without leaking case scope', async ({ browser }) => {
    await setE2EClock('2026-09-30T06:00:00.000Z');
    const officer = await apiForRole('officer');
    const worker = await apiForRole('socialWorker');
    const otherWorker = await apiForRole('otherSocialWorker');
    const guardian = await apiForRole('guardian');
    const linked = await successful<GuardianStudent[]>(await guardian.get('/api/v1/guardian/students'));
    const student = linked[0].student;

    const openReferrals = await successful<PageResult<Referral>>(
      await officer.get(`/api/v1/referrals?status=Open&studentId=${student.id}&${pageQuery}`)
    );
    const referral = openReferrals.items.find(item => item.sourceSnapshot.sourceEntityId == null);
    expect(referral).toBeDefined();
    const options = await successful<SocialWorkerOption[]>(
      await officer.get('/api/v1/referrals/assignable-social-workers')
    );
    const primaryWorker = options.find(option => option.displayName.includes('E2E Social Worker'));
    expect(primaryWorker).toBeDefined();

    const assigned = await successful<Referral>(
      await officer.post(`/api/v1/referrals/${referral.id}/assign`, {
        data: {
          socialWorkerUserId: primaryWorker!.userId,
          reason: 'W10 ownership handoff',
          rowVersion: referral.rowVersion
        }
      })
    );
    expect(assigned.status).toBe('Assigned');

    const otherWorkerRead = await otherWorker.get(`/api/v1/referrals/${referral.id}`);
    expect([403, 404]).toContain(otherWorkerRead.status());
    await assertRolePageContains(browser, 'socialWorker', '/student-affairs/cases', student.displayName);

    const acceptedReferral = await successful<Referral>(
      await worker.post(`/api/v1/referrals/${referral.id}/accept`, {
        data: { rowVersion: assigned.rowVersion }
      })
    );
    expect(acceptedReferral.status).toBe('InProgress');

    const guardianLinks = await successful<GuardianLink[]>(
      await officer.get(`/api/v1/students/${student.id}/guardians`)
    );
    const guardianProfile = guardianLinks.find(link => link.isActive)?.guardian;
    expect(guardianProfile).toBeDefined();

    const summonPayload = {
      studentId: student.id,
      referralId: referral.id,
      reason: 'W10 assigned case guardian meeting',
      priority: 'High',
      guardianProfileId: guardianProfile!.id
    };
    const created = await successful<Summon>(
      await worker.post('/api/v1/summons', {
        headers: { 'Idempotency-Key': 'w10-e-summon' },
        data: summonPayload
      }),
      201
    );
    const replayed = await successful<Summon>(
      await worker.post('/api/v1/summons', {
        headers: { 'Idempotency-Key': 'w10-e-summon' },
        data: summonPayload
      }),
      201
    );
    expect(replayed.id).toBe(created.id);

    const scheduled = await successful<Summon>(
      await worker.post(`/api/v1/summons/${created.id}/schedule`, {
        data: {
          appointmentAt: '2026-10-01T07:00:00.000Z',
          location: 'E2E counseling office',
          instructions: 'Bring the guardian identity document',
          guardianProfileId: guardianProfile!.id,
          rowVersion: created.rowVersion
        }
      })
    );

    await expect.poll(async () => {
      const mine = await successful<PageResult<Summon>>(
        await guardian.get(`/api/v1/summons/mine?studentId=${student.id}&${pageQuery}`)
      );
      return mine.items.some(item => item.id === created.id);
    }, { timeout: 20_000 }).toBeTruthy();
    let notifiedSummon!: Summon;
    await expect.poll(async () => {
      notifiedSummon = await successful<Summon>(await worker.get(`/api/v1/summons/${created.id}`));
      return notifiedSummon.guardianNotifiedAt;
    }, { timeout: 20_000 }).not.toBeNull();
    await assertRolePageContains(browser, 'guardian', '/student-affairs/guardian/activity', student.displayName);

    await setE2EClock('2026-10-01T07:00:00.000Z');
    const attended = await successful<Summon>(
      await worker.post(`/api/v1/summons/${created.id}/attend`, {
        data: { attendanceNotes: 'W10 guardian attended', rowVersion: notifiedSummon.rowVersion }
      })
    );
    expect(attended.status).toBe('Attended');
    const observed = await successful<Summon>(
      await worker.post(`/api/v1/summons/${created.id}/start-observation`, {
        data: {
          goals: 'Sustained attendance improvement',
          startDate: '2026-10-01',
          reviewDate: '2026-10-08',
          endDate: '2026-10-15',
          responsibleStaffUserId: primaryWorker!.userId,
          measurableIndicators: ['No unexplained absence for seven days'],
          notes: 'W10 observation plan',
          rowVersion: attended.rowVersion
        }
      })
    );
    expect(observed.status).toBe('UnderObservation');
    const improved = await successful<Summon>(
      await worker.post(`/api/v1/summons/${created.id}/mark-improved`, {
        data: {
          outcomeEvidence: 'Seven consecutive attended study days',
          verificationDetails: 'Verified against the attendance register',
          rowVersion: observed.rowVersion
        }
      })
    );
    expect(improved.status).toBe('Improved');

    const stillOpen = await successful<Referral>(await worker.get(`/api/v1/referrals/${referral.id}`));
    expect(stillOpen.status).toBe('InProgress');
    const history = await successful<{ transitions: Array<{ toState: string }> }>(
      await worker.get(`/api/v1/summons/${created.id}/history`)
    );
    expect(history.transitions.map(item => item.toState)).toEqual(
      expect.arrayContaining(['Attended', 'UnderObservation', 'Improved'])
    );

    await Promise.all([officer.dispose(), worker.dispose(), otherWorker.dispose(), guardian.dispose()]);
  });

  test('Scenario F — threshold → accepted excuse → Officer review lowers the metric while preserving escalation history', async ({ browser }) => {
    await setE2EClock('2026-10-06T07:30:00.000Z');
    const secretary = await apiForRole('secretary');
    const guardian = await apiForRole('guardian');
    const officer = await apiForRole('officer');
    const linked = await successful<GuardianStudent[]>(await guardian.get('/api/v1/guardian/students'));
    const student = linked[0].student;
    const classrooms = await successful<PageResult<Classroom>>(
      await secretary.get(`/api/v1/classrooms?${pageQuery}`)
    );
    const classroom = classrooms.items.find(item => item.label === 'E2E-1-A')!;
    const beforeReferrals = await successful<PageResult<Referral>>(
      await officer.get(`/api/v1/referrals?studentId=${student.id}&${pageQuery}`)
    );
    const beforeSummons = await successful<PageResult<Summon>>(
      await officer.get(`/api/v1/summons?studentId=${student.id}&${pageQuery}`)
    );
    const existingSummonIds = new Set(beforeSummons.items.map(item => item.id));

    const thresholdDates = ['2026-10-01', '2026-10-02', '2026-10-03', '2026-10-04', '2026-10-05'];
    for (const absenceDate of thresholdDates) {
      const sheet = await successful<AttendanceSheet>(
        await secretary.get(`/api/v1/student-attendance/sheet?date=${absenceDate}&classroomId=${classroom.id}`)
      );
      const payload = {
        date: absenceDate,
        classroomId: classroom.id,
        absentStudentIds: [student.id],
        rosterRevision: sheet.rosterRevision
      };
      const key = `w10-f-attendance-${absenceDate}`;
      const submitted = await successful<AttendanceSheet>(
        await secretary.put('/api/v1/student-attendance/sheet', {
          headers: { 'Idempotency-Key': key }, data: payload
        })
      );
      const replayed = await successful<AttendanceSheet>(
        await secretary.put('/api/v1/student-attendance/sheet', {
          headers: { 'Idempotency-Key': key }, data: payload
        })
      );
      expect(replayed.rows.find(row => row.student.id === student.id)?.attendanceId)
        .toBe(submitted.rows.find(row => row.student.id === student.id)?.attendanceId);
    }

    let automatedSummon: Summon | undefined;
    await expect.poll(async () => {
      const summons = await successful<PageResult<Summon>>(
        await officer.get(`/api/v1/summons?studentId=${student.id}&${pageQuery}`)
      );
      automatedSummon = summons.items.find(item =>
        !existingSummonIds.has(item.id)
          && item.sourceCountSnapshot === 5
          && item.thresholdSnapshot === 5
          && item.referralId != null);
      return automatedSummon?.id ?? 0;
    }, { timeout: 25_000 }).toBeGreaterThan(0);

    const afterThresholdReferrals = await successful<PageResult<Referral>>(
      await officer.get(`/api/v1/referrals?studentId=${student.id}&${pageQuery}`)
    );
    expect(afterThresholdReferrals.totalCount).toBe(beforeReferrals.totalCount + 1);

    const historyBeforeCorrection = await successful<{
      records: AttendanceRecord[];
      absenceMetric: { eligibleTermCount: number };
    }>(await guardian.get(`/api/v1/student-attendance/students/${student.id}`));
    expect(historyBeforeCorrection.absenceMetric.eligibleTermCount).toBe(5);
    const factToCorrect = historyBeforeCorrection.records.find(record => record.date === thresholdDates[0])!;

    const pdf = Buffer.from('%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF');
    const correctionExcuse = await successful<Excuse>(
      await guardian.post(`/api/v1/student-attendance/${factToCorrect.id}/excuses`, {
        headers: { 'Idempotency-Key': 'w10-f-threshold-correction' },
        multipart: {
          excuseType: 'Official',
          notes: 'W10 approved source correction after threshold',
          attachment: { name: 'w10-threshold-correction.pdf', mimeType: 'application/pdf', buffer: pdf }
        }
      }),
      202
    );
    const corrected = await successful<Excuse>(
      await officer.post(`/api/v1/student-attendance/excuses/${correctionExcuse.id}/accept`, {
        data: { reviewNote: 'W10 canonical metric correction', rowVersion: correctionExcuse.rowVersion }
      })
    );
    expect(corrected.status).toBe('Accepted');

    let reviewCandidate: Summon | undefined;
    await expect.poll(async () => {
      const reviews = await successful<PageResult<Summon>>(
        await officer.get(`/api/v1/summons/automation-impact-reviews?studentId=${student.id}&${pageQuery}`)
      );
      reviewCandidate = reviews.items.find(item => item.id === automatedSummon!.id);
      return reviewCandidate?.requiresOfficerReview ?? false;
    }, { timeout: 25_000 }).toBeTruthy();

    const metricAfterCorrection = await successful<{
      records: AttendanceRecord[];
      absenceMetric: { eligibleTermCount: number };
    }>(await guardian.get(`/api/v1/student-attendance/students/${student.id}`));
    expect(metricAfterCorrection.absenceMetric.eligibleTermCount).toBe(4);
    expect(metricAfterCorrection.records.find(record => record.id === factToCorrect.id)?.status)
      .toBe('AbsentExcused');

    await assertRolePageContains(browser, 'officer', '/student-affairs/officer/automation-reviews', student.displayName);
    const reviewed = await successful<Summon>(
      await officer.post(`/api/v1/summons/${reviewCandidate!.id}/automation-impact-review`, {
        data: {
          decision: 'Retain',
          rationale: 'Historical escalation retained; future handling adjusted to the corrected metric.',
          rowVersion: reviewCandidate!.rowVersion
        }
      })
    );
    expect(reviewed.requiresOfficerReview).toBeFalsy();
    expect(reviewed.id).toBe(automatedSummon!.id);

    const staleReview = await officer.post(`/api/v1/summons/${reviewCandidate!.id}/automation-impact-review`, {
      data: {
        decision: 'Close',
        rationale: 'Must lose the concurrency race.',
        rowVersion: reviewCandidate!.rowVersion
      }
    });
    expect(staleReview.status()).toBe(409);

    const referralsAfterReview = await successful<PageResult<Referral>>(
      await officer.get(`/api/v1/referrals?studentId=${student.id}&${pageQuery}`)
    );
    expect(referralsAfterReview.totalCount).toBe(afterThresholdReferrals.totalCount);
    await Promise.all([secretary.dispose(), guardian.dispose(), officer.dispose()]);
  });

  test('Scenario G — Instructor → Guardian → SchoolManager republish flags office-hour conflicts and recalculates queued delivery', async ({ browser }) => {
    await setE2EClock('2026-10-03T09:00:00.000Z');
    const instructor = await apiForRole('instructor');
    const guardian = await apiForRole('guardian');
    const manager = await apiForRole('manager');
    const linked = await successful<GuardianStudent[]>(await guardian.get('/api/v1/guardian/students'));
    const student = linked[0].student;

    let eligible = await successful<OfficeHours>(
      await instructor.get('/api/v1/office-hours/me/eligible')
    );
    let freeSlots = eligible.slots.filter(slot => slot.isEligible);
    expect(freeSlots.length).toBeGreaterThan(1);
    const current = await successful<Timetable>(
      await manager.get(`/api/v1/timetables/${eligible.schoolTimetableId}`)
    );
    const conflictPlan = freeSlots.map(slot => ({
      slot,
      source: current.entries.find(entry =>
        entry.instructorProfileId === eligible.instructorId
        && !current.entries.some(other => other !== entry
          && other.day === slot.dayOfWeek
          && other.period === slot.periodSequence
          && ((entry.classroomId != null && other.classroomId === entry.classroomId)
            || (entry.roomId != null && other.roomId === entry.roomId))))
    })).find(plan => plan.source != null);
    expect(conflictPlan).toBeDefined();
    const target = conflictPlan!.slot;
    const sourceEntry = conflictPlan!.source!;
    const survivor = freeSlots.find(slot => slot.stableKey !== target.stableKey)!;
    const selectedSlots = [target, survivor];
    let configured: OfficeHours | undefined;
    for (let attempt = 0; attempt < 3 && !configured; attempt++) {
      const response = await instructor.put('/api/v1/office-hours/me', {
        data: {
          selectedSlotKeys: selectedSlots.map(slot => slot.stableKey),
          effectiveFrom: '2026-09-30',
          rowVersion: eligible.rowVersion
        }
      });
      if (response.status() === 409) {
        eligible = await successful<OfficeHours>(
          await instructor.get('/api/v1/office-hours/me/eligible')
        );
        freeSlots = eligible.slots.filter(slot => slot.isEligible);
        continue;
      }
      configured = await successful<OfficeHours>(response);
    }
    expect(configured).toBeDefined();
    expect(configured!.slots.filter(slot => slot.isSelected)).toHaveLength(selectedSlots.length);
    await assertRolePageContains(browser, 'instructor', '/student-affairs/office-hours');

    const teacherOptions = await successful<TeacherOption[]>(
      await guardian.get(`/api/v1/conversations/recipient-options/teachers?studentId=${student.id}`)
    );
    const teacher = teacherOptions.find(option => option.instructorProfileId === eligible.instructorId);
    expect(teacher).toBeDefined();
    const conversation = await successful<Conversation>(
      await guardian.post('/api/v1/conversations', {
        data: {
          studentId: student.id,
          threadType: 'GuardianTeacher',
          targetInstructorProfileId: teacher!.instructorProfileId,
          targetStaffRole: null,
          targetStaffUserId: null,
          subject: 'W10 queued office-hour subject',
          initialBody: 'W10 confidential queued message body',
          idempotencyKey: 'w10-g-conversation'
        }
      }),
      201
    );
    const messagesBefore = await successful<PageResult<ConversationMessage>>(
      await guardian.get(`/api/v1/conversations/${conversation.id}/messages?${pageQuery}`)
    );
    const queued = messagesBefore.items.find(message => message.body.includes('W10 confidential'))!;
    expect(queued.disposition).toBe('QueuedUntilOfficeHours');
    expect(queued.deliveryState).toBe('Pending');
    expect(queued.nextEligibleSendAt).not.toBeNull();
    await assertRolePageContains(browser, 'guardian', '/student-affairs/messages');

    const regenerated = await successful<Timetable>(
      await manager.post(`/api/v1/timetables/${current.id}/regenerate`, {
        data: { revision: current.revision }
      })
    );
    expect(regenerated.isPublished).toBeFalsy();

    const entries = current.entries.map(entry => entry === sourceEntry
      ? { ...entry, day: target.dayOfWeek, period: target.periodSequence }
      : entry);
    const draft = await successful<Timetable>(
      await manager.put(`/api/v1/timetables/${regenerated.id}`, {
        data: {
          title: `${regenerated.title} — W10 office-hours reconciliation`,
          revision: regenerated.revision,
          entries
        }
      })
    );

    const published = await successful<Timetable>(
      await manager.post(`/api/v1/timetables/${draft.id}/publish`, {
        data: { revision: draft.revision }
      })
    );
    expect(published.isPublished).toBeTruthy();

    let reconciled: OfficeHours | undefined;
    await expect.poll(async () => {
      reconciled = await successful<OfficeHours>(await instructor.get('/api/v1/office-hours/me'));
      const conflicted = reconciled.slots.some(slot => slot.isSelected && slot.isConflicted);
      const valid = reconciled.slots.some(slot => slot.isSelected && !slot.isConflicted);
      return conflicted && valid;
    }, { timeout: 25_000 }).toBeTruthy();

    let recalculated: ConversationMessage | undefined;
    await expect.poll(async () => {
      const messages = await successful<PageResult<ConversationMessage>>(
        await guardian.get(`/api/v1/conversations/${conversation.id}/messages?${pageQuery}`)
      );
      recalculated = messages.items.find(message => message.id === queued.id);
      return Boolean(recalculated?.nextEligibleSendAt
        && recalculated.nextEligibleSendAt !== queued.nextEligibleSendAt);
    }, { timeout: 25_000 }).toBeTruthy();
    expect(recalculated!.disposition).toBe('QueuedUntilOfficeHours');
    expect(recalculated!.deliveryState).toBe('Pending');
    expect(recalculated!.nextEligibleSendAt).not.toBeNull();

    const staleUpdate = await instructor.put('/api/v1/office-hours/me', {
      data: {
        selectedSlotKeys: selectedSlots.map(slot => slot.stableKey),
        effectiveFrom: '2026-09-30',
        rowVersion: configured!.rowVersion
      }
    });
    expect(staleUpdate.status()).toBe(409);

    const auditResponse = await manager.get(`/api/v1/conversations/audit?${pageQuery}`);
    const audit = await successful<PageResult<Record<string, unknown>>>(auditResponse);
    const serializedAudit = JSON.stringify(audit);
    expect(serializedAudit).not.toContain('W10 queued office-hour subject');
    expect(serializedAudit).not.toContain('W10 confidential queued message body');
    expect(serializedAudit).not.toMatch(/subject|body|evidence|attachment/i);
    await assertRolePageContains(browser, 'manager', '/student-affairs/messaging-audit');

    await Promise.all([instructor.dispose(), guardian.dispose(), manager.dispose()]);
  });
});

async function assertRolePageContains(
  browser: Browser,
  role: Parameters<typeof openRoleSession>[1],
  path: string,
  text?: string
): Promise<void> {
  const session = await openRoleSession(browser, role);
  await session.page.goto(path);
  await expect(session.page).toHaveURL(new RegExp(path.replaceAll('/', '\\/')));
  await expect.poll(() => session.page.evaluate(() => getComputedStyle(document.body).direction)).toBe('rtl');
  await expect(session.page.locator('main').first()).toBeVisible();
  if (text) await expect(session.page.getByText(text, { exact: false }).first()).toBeVisible();
  await session.context.close();
}
