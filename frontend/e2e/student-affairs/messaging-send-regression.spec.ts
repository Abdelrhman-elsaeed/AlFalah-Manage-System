import { expect, test } from '@playwright/test';
import { apiForRole, successful } from '../support/api-session';
import { openRoleSession, setE2EClock } from '../support/role-session';

interface GuardianStudent {
  student: { id: number; displayName: string };
}

interface StaffOption {
  userId: string;
  displayName: string;
  role: string;
  threadType: string;
}

interface GuardianOption {
  guardianProfileId: number;
  displayName: string;
  relationship: string;
  isPrimary: boolean;
}

interface Conversation {
  id: number;
  subject: string;
  threadType: string;
  participants: Array<{ userId: string; role: string }>;
}

interface ConversationPage {
  items: Conversation[];
}

test.describe('Messaging send regression', () => {
  test('officer reply stays in the chat, reaches the API, and clears the draft', async ({ browser }) => {
    await setE2EClock('2026-09-30T08:15:00.000Z');
    const guardian = await apiForRole('guardian');
    const linkedStudents = await successful<GuardianStudent[]>(
      await guardian.get('/api/v1/guardian/students')
    );
    const student = linkedStudents[0]?.student;
    expect(student).toBeDefined();

    const staff = await successful<StaffOption[]>(
      await guardian.get(`/api/v1/conversations/recipient-options/staff?studentId=${student!.id}`)
    );
    const officer = staff.find(option => option.threadType === 'GuardianStudentAffairs');
    expect(officer).toBeDefined();

    await successful(
      await guardian.post('/api/v1/conversations', {
        data: {
          studentId: student!.id,
          threadType: 'GuardianStudentAffairs',
          targetInstructorProfileId: null,
          targetStaffRole: officer!.role,
          targetStaffUserId: officer!.userId,
          subject: 'متابعة انتظام الطالب',
          initialBody: 'أرغب في متابعة انتظام ابني مع شؤون الطلاب.',
          idempotencyKey: 'messaging-send-regression-thread'
        }
      }),
      201
    );

    const session = await openRoleSession(browser, 'officer');
    const page = session.page;
    let guardianSession: Awaited<ReturnType<typeof openRoleSession>> | null = null;
    let loadEvents = 0;
    page.on('load', () => loadEvents += 1);

    try {
      await page.goto('/student-affairs/officer');
      const unreadSidebarBadge = page.locator('.shell-sidebar__badge--messages').first();
      await expect(unreadSidebarBadge).toBeVisible();
      expect(Number(await unreadSidebarBadge.textContent())).toBeGreaterThan(0);

      await page.goto('/student-affairs/messages');
      await expect(page.getByRole('heading', { name: 'مركز الرسائل' })).toBeVisible();
      await expect(page.getByText('متابعة انتظام الطالب', { exact: true }).first()).toBeVisible();
      const baselineLoads = loadEvents;

      const draft = page.locator('textarea').last();
      const reply = 'تمت مراجعة الحالة وسنتابع انتظام الطالب يوميًا.';
      await draft.fill(reply);

      const responsePromise = page.waitForResponse(response =>
        response.request().method() === 'POST'
        && /\/api\/v1\/conversations\/\d+\/messages(?:\?|$)/.test(response.url())
      );
      await page.getByRole('button', { name: /^إرسال$/ }).click();
      const response = await responsePromise;

      expect(response.ok(), `send returned HTTP ${response.status()}`).toBeTruthy();
      await expect(page).toHaveURL(/\/student-affairs\/messages$/);
      expect(loadEvents, 'the composer triggered a full document reload').toBe(baselineLoads);
      await expect(page.getByText(reply, { exact: true })).toBeVisible();
      await expect(draft).toHaveValue('');

      guardianSession = await openRoleSession(browser, 'guardian');
      const readPromise = guardianSession.page.waitForResponse(response =>
        response.request().method() === 'POST'
        && /\/api\/v1\/conversations\/\d+\/read(?:\?|$)/.test(response.url())
      );
      await guardianSession.page.goto('/student-affairs/messages');
      await expect(guardianSession.page.getByText(reply, { exact: true })).toBeVisible();
      await readPromise;

      await page.reload();
      const sentMessage = page.locator('article.message').filter({ hasText: reply });
      await expect(sentMessage.locator('.receipt-status')).toContainText('تمت القراءة');
      await expect(sentMessage.locator('.receipt-status time')).toBeVisible();
    } finally {
      await guardianSession?.context.close();
      await session.context.close();
      await guardian.dispose();
    }
  });

  test('officer can start a direct conversation only with an active linked guardian', async () => {
    await setE2EClock('2026-09-30T08:20:00.000Z');
    const guardian = await apiForRole('guardian');
    const officer = await apiForRole('officer');
    const crossSchoolOfficer = await apiForRole('crossOfficer');

    try {
      const linkedStudents = await successful<GuardianStudent[]>(
        await guardian.get('/api/v1/guardian/students')
      );
      const student = linkedStudents[0]?.student;
      expect(student).toBeDefined();

      const options = await successful<GuardianOption[]>(
        await officer.get(`/api/v1/conversations/recipient-options/guardians?studentId=${student!.id}`)
      );
      expect(options).toHaveLength(1);

      const isolatedOptions = await successful<GuardianOption[]>(
        await crossSchoolOfficer.get(`/api/v1/conversations/recipient-options/guardians?studentId=${student!.id}`)
      );
      expect(isolatedOptions).toEqual([]);

      const subject = 'Officer direct guardian follow-up';
      const conversation = await successful<Conversation>(
        await officer.post('/api/v1/conversations', {
          data: {
            studentId: student!.id,
            threadType: 'GuardianStudentAffairs',
            targetInstructorProfileId: null,
            targetStaffRole: null,
            targetStaffUserId: null,
            targetGuardianProfileId: options[0].guardianProfileId,
            subject,
            initialBody: 'Student Affairs would like to follow up directly with the guardian.',
            idempotencyKey: 'officer-direct-guardian-e2e'
          }
        }),
        201
      );

      expect(conversation.threadType).toBe('GuardianStudentAffairs');
      expect(conversation.participants.map(participant => participant.role).sort())
        .toEqual(['Guardian', 'StudentAffairsOfficer']);

      const guardianInbox = await successful<ConversationPage>(
        await guardian.get('/api/v1/conversations?pageNumber=1&pageSize=100')
      );
      expect(guardianInbox.items.some(item => item.id === conversation.id && item.subject === subject)).toBeTruthy();
    } finally {
      await guardian.dispose();
      await officer.dispose();
      await crossSchoolOfficer.dispose();
    }
  });
});
