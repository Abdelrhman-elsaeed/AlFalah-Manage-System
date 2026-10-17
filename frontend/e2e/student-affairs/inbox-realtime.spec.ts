import { expect, test } from '@playwright/test';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { createHmac, randomUUID } from 'node:crypto';
import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { apiForRole, successful } from '../support/api-session';
import { openRoleSession, setE2EClock } from '../support/role-session';

const apiUrl = process.env['E2E_API_URL'] ?? 'http://127.0.0.1:5264';
const artifact = async (role: string) => JSON.parse(await readFile(resolve('test-results', '.auth', `${role}.json`), 'utf8'));

test.describe('Inbox N1 realtime', () => {
  test('two live sessions update messages and read receipts while preserving an unsent Arabic draft @mobile', async ({ browser }, info) => {
    test.setTimeout(120_000);
    await setE2EClock('2026-09-30T08:15:00.000Z');
    const guardian = await apiForRole('guardian');
    const officer = await apiForRole('officer');
    const students = await successful<Array<{ student: { id: number } }>>(await guardian.get('/api/v1/guardian/students'));
    const options = await successful<Array<{ role: string; userId: string; threadType: string }>>(
      await guardian.get(`/api/v1/conversations/recipient-options/staff?studentId=${students[0].student.id}`));
    const target = options.find(x => x.threadType === 'GuardianStudentAffairs')!;
    const conversation = await successful<{ id: number }>(await guardian.post('/api/v1/conversations', { data: {
      studentId: students[0].student.id, threadType: target.threadType, targetStaffRole: target.role,
      targetStaffUserId: target.userId, subject: `اختبار التحديث المباشر ${randomUUID()}`,
      initialBody: 'بداية اختبار الإشعارات', idempotencyKey: randomUUID()
    } }));
    const sender = await openRoleSession(browser, 'officer');
    const receiver = await openRoleSession(browser, 'guardian', info.project.name.includes('mobile') ? { width: 390, height: 844 } : undefined);
    try {
      let blocked = 0;
      await receiver.context.route('**/hubs/inbox/negotiate?**', async route => {
        if (blocked++ < 1) await route.abort(); else await route.fallback();
      });
      await Promise.all([sender.page.goto(`/student-affairs/messages?conversationId=${conversation.id}`),
        receiver.page.goto(`/student-affairs/messages?conversationId=${conversation.id}`)]);
      await expect(receiver.page.getByRole('heading', { name: 'مركز الرسائل' })).toBeVisible();
      await expect(sender.page.getByRole('heading', { name: 'مركز الرسائل' })).toBeVisible();
      await expect.poll(() => blocked, { timeout: 20_000 }).toBeGreaterThanOrEqual(2);
      await expect(receiver.page.locator('.messages-page > [role="status"]')).toBeHidden({ timeout: 20_000 });
      await expect(sender.page.locator('.messages-page > [role="status"]')).toBeHidden({ timeout: 20_000 });
      const draft = receiver.page.locator('.composer textarea');
      await draft.fill('مسودة عربية محفوظة أثناء وصول رسائل جديدة');
      const latencies: number[] = [];
      for (let index = 0; index < 10; index++) {
        const body = `رسالة لحظية ${index} ${randomUUID()}`;
        const start = Date.now();
        await successful(await officer.post(`/api/v1/conversations/${conversation.id}/messages`, { data: {
          body, replyToMessageId: null, idempotencyKey: randomUUID()
        } }));
        await expect(receiver.page.getByText(body, { exact: true })).toBeVisible({ timeout: 5000 });
        await expect(receiver.page.getByText(body, { exact: true })).toBeInViewport();
        latencies.push(Date.now() - start);
        await expect(draft).toHaveValue('مسودة عربية محفوظة أثناء وصول رسائل جديدة');
      }
      await expect(sender.page.locator('article.message').last().locator('.receipt-status')).toContainText('تمت القراءة');
      const sorted = [...latencies].sort((a, b) => a - b);
      const p95 = sorted[Math.ceil(sorted.length * .95) - 1];
      expect(p95).toBeLessThanOrEqual(5000);
      await info.attach('latency-profile', { body: JSON.stringify({ profile: 'LocalDB, two sessions, ten sequential messages, healthy local network', latencies, p95 }), contentType: 'application/json' });
      expect(await receiver.page.locator('main.messages-page').getAttribute('dir')).toBe('rtl');
      expect(await receiver.page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1)).toBeTruthy();
      await receiver.page.screenshot({ path: info.outputPath('inbox-rtl.png'), fullPage: true });
      // A user reading back must see an unread cue rather than a false read receipt.
      await receiver.page.locator('.timeline').evaluate(element => { element.scrollTop = 0; element.dispatchEvent(new Event('scroll')); });
      const historyArrival = `رسالة أثناء قراءة السجل ${randomUUID()}`;
      await successful(await officer.post(`/api/v1/conversations/${conversation.id}/messages`, { data: {
        body: historyArrival, replyToMessageId: null, idempotencyKey: randomUUID()
      } }));
      await expect(receiver.page.getByRole('button', { name: 'عرض الرسائل الجديدة' })).toBeVisible();
      await expect(receiver.page.getByText(historyArrival, { exact: true })).toHaveCount(0);
      await receiver.page.getByRole('button', { name: 'عرض الرسائل الجديدة' }).click();
      await expect(receiver.page.getByText(historyArrival, { exact: true })).toBeInViewport();
      // Stop the actual tab connection, miss a commit, then focus: REST recovers the missed change.
      await receiver.page.evaluate(async () => {
        const ng = (window as any).ng;
        const shell = ng.getComponent(document.querySelector('app-shell'));
        await shell.inboxSync.connection.stop();
      });
      const missed = `رسالة أثناء إعادة الاتصال ${randomUUID()}`;
      await successful(await officer.post(`/api/v1/conversations/${conversation.id}/messages`, { data: {
        body: missed, replyToMessageId: null, idempotencyKey: randomUUID()
      } }));
      await receiver.page.evaluate(() => window.dispatchEvent(new Event('focus')));
      await expect(receiver.page.getByText(missed, { exact: true })).toBeVisible();
      await expect(draft).toHaveValue('مسودة عربية محفوظة أثناء وصول رسائل جديدة');
    } finally {
      await sender.context.close(); await receiver.context.close(); await guardian.dispose(); await officer.dispose();
    }
  });

  test('hub closes on token expiry, rejects foreign origins and never accepts query bearer on REST', async () => {
    const auth = await artifact('officer');
    const secret = process.env['Jwt__Secret'];
    expect(secret, 'test-only signing secret must be supplied by isolated runner').toBeTruthy();
    const [header, raw] = auth.accessToken.split('.');
    const claims = JSON.parse(Buffer.from(raw, 'base64url').toString('utf8'));
    claims.exp = Math.floor(Date.now() / 1000) + 4;
    const payload = Buffer.from(JSON.stringify(claims)).toString('base64url');
    const signature = createHmac('sha256', secret!).update(`${header}.${payload}`).digest('base64url');
    const connection = new HubConnectionBuilder().withUrl(`${apiUrl}/hubs/inbox`, {
      accessTokenFactory: () => `${header}.${payload}.${signature}`
    }).configureLogging(LogLevel.None).build();
    let closed = false;
    connection.onclose(() => { closed = true; });
    try { await connection.start(); await expect.poll(() => closed, { timeout: 9000 }).toBe(true); }
    finally { await connection.stop(); }
    const evil = new HubConnectionBuilder().withUrl(`${apiUrl}/hubs/inbox`, {
      accessTokenFactory: () => auth.accessToken, headers: { Origin: 'https://foreign.invalid' }
    }).configureLogging(LogLevel.None).build();
    await expect(evil.start()).rejects.toThrow();
    const response = await fetch(`${apiUrl}/api/v1/inbox/state?access_token=${encodeURIComponent(auth.accessToken)}`);
    expect(response.status).toBe(401);
  });

  test('an already connected account is disconnected when the manager deactivates it', async () => {
    const manager = await apiForRole('manager');
    const managerAuth = await artifact('manager');
    const password = process.env['ALFALAH_E2E_PASSWORD']!;
    const username = `n1-${randomUUID()}`;
    const user = await successful<{ userId: string; username: string }>(await manager.post('/api/v1/users', { data: {
      username, password, firstName: 'اختبار', lastName: 'الإشعارات', fullName: 'اختبار الإشعارات',
      role: 'Instructor', employeeNumber: String(Date.now()), stage: 'Primary', subject: 'اختبار',
      schoolId: managerAuth.user.activeSchoolId
    } }));
    const login = await fetch(`${apiUrl}/api/v1/auth/school-login`, { method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ schoolId: managerAuth.user.activeSchoolId, username: user.username, password }) });
    const envelope = await login.json();
    expect(envelope.isSuccess).toBe(true);
    const connection = new HubConnectionBuilder().withUrl(`${apiUrl}/hubs/inbox`, {
      accessTokenFactory: () => envelope.data.accessToken
    }).configureLogging(LogLevel.None).build();
    let closed = false; connection.onclose(() => { closed = true; });
    try {
      await connection.start();
      expect((await manager.post(`/api/v1/users/${user.userId}/deactivate`, { data: {} })).ok()).toBeTruthy();
      await expect.poll(() => closed, { timeout: 10_000 }).toBe(true);
    } finally { await connection.stop(); await manager.dispose(); }
  });
});
