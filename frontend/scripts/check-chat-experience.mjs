// Exercises the real Angular app and SignalR client with synthetic API/Hub responses.
// No messages are written to the operational database.
import assert from 'node:assert/strict';
import { mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { chromium, expect } from '@playwright/test';

const origin = process.env.ALFALAH_CHAT_ORIGIN || 'http://localhost:4200';
const evidence = resolve('../docs/verification/chat-experience');
mkdirSync(evidence, { recursive: true });
const browser = await chromium.launch({ headless: true });
const results = [];
try {
  for (const width of [1440, 768, 390, 320]) {
    const height = width > 860 ? 1000 : 844;
    const page = await browser.newPage({ viewport: { width, height }, reducedMotion: 'reduce' });
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    const user = { userId: 'chat-officer', fullName: 'أحمد عبد الرحمن', username: 'chat-fixture', activeSchoolId: 1,
      activeSchoolName: 'مدارس الفلاح', roles: ['StudentAffairsOfficer'], preferredLanguage: 'ar',
      permissions: ['Messaging.ViewOwn', 'Messaging.Send', 'Messaging.CloseThread', 'Messaging.StartOfficerGuardian'] };
    const thread = { id: 9, student: { id: 5, displayName: 'عبد الله محمد العتيبي', studentNumber: 'S-105',
      classroomId: 2, classLabel: 'الأول المتوسط / ٢', isActive: true, photoUrl: null },
      subject: 'متابعة المستوى الدراسي', threadType: 'GuardianStudentAffairs', status: 'Open', rowVersion: 'v1', unreadCount: 0,
      participants: [{ userId: user.userId, displayName: user.fullName, role: 'StudentAffairsOfficer' },
        { userId: 'chat-parent', displayName: 'محمد العتيبي', role: 'Guardian' }], updatedAt: new Date().toISOString(), referralId: null };
    const message = (id, body, mine = false) => ({ id, conversationId: 9, body, replyToMessageId: null,
      createdAt: new Date(Date.now() - (10 - id) * 60000).toISOString(), deliveryState: 'Delivered', disposition: 'SentImmediately',
      nextEligibleSendAt: null, sender: { userId: mine ? user.userId : 'chat-parent', displayName: mine ? user.fullName : 'محمد العتيبي',
        roleSnapshot: mine ? 'StudentAffairsOfficer' : 'Guardian' }, receipts: [] });
    const messages = [message(1, 'السلام عليكم، أود الاطمئنان على مستوى عبد الله خلال هذا الأسبوع.'),
      message(2, 'وعليكم السلام ورحمة الله، أهلًا بك. لاحظنا تحسنًا جميلًا في المشاركة والالتزام بالواجبات.', true),
      message(3, 'الحمد لله، شكرًا لاهتمامكم ومتابعتكم. هل هناك نقاط نركز عليها في البيت؟'),
      message(4, 'نقترح تخصيص عشر دقائق يوميًا للقراءة. ويسعدنا متابعة تقدمه معكم هنا.', true)];
    let socket, delayList = false, listStarted = 0;
    await page.addInitScript(user => {
      sessionStorage.setItem('alfalah_access_token', `e30.${btoa(JSON.stringify({sub:user.userId,exp:2000000000}))}.fixture`);
      sessionStorage.setItem('alfalah_user', JSON.stringify(user));
    }, user);
    await page.route('**/hubs/inbox/negotiate*', route => route.fulfill({ json: {
      negotiateVersion: 1, connectionId: 'fixture', connectionToken: 'fixture',
      availableTransports: [{ transport: 'WebSockets', transferFormats: ['Text', 'Binary'] }]
    } }));
    await page.routeWebSocket('**/hubs/inbox**', ws => {
      socket = ws;
      ws.onMessage(raw => { if (String(raw).includes('"protocol"')) ws.send('{}\x1e'); });
    });
    await page.route('**/api/**', async route => {
      const url = new URL(route.request().url()), path = url.pathname, method = route.request().method();
      let data = {};
      if (path.endsWith('/auth/me')) data = user;
      else if (path.endsWith('/account/signature/requirement')) data = { required: false, hasSignature: true };
      else if (path.endsWith('/inbox/state')) data = { unreadMessages: 0, unreadConversations: 0, unreadNotifications: 0,
        syncCursor: 'fixture', realtimeAvailable: true, asOfUtc: new Date().toISOString() };
      else if (path.endsWith('/inbox/changes')) data = { items: [], nextCursor: 'fixture', hasMore: false, resyncRequired: false };
      else if (path.endsWith('/conversations')) {
        listStarted++;
        if (delayList) await new Promise(resolve => setTimeout(resolve, 650));
        data = { items: [thread], totalCount: 1, page: 1, pageSize: 100, hasNext: false, hasPrevious: false, totalPages: 1 };
      } else if (path.endsWith('/conversations/9')) data = thread;
      else if (path.endsWith('/messages/window')) data = { items: messages, hasOlder: false, nextBeforeCursor: null };
      else if (path.endsWith('/conversations/9/messages') && method === 'POST') {
        const sent = message(messages.length + 1, route.request().postDataJSON().body, true);
        messages.push(sent); thread.rowVersion = 'v2';
        data = { message: sent, conversationRowVersion: 'v2', disposition: 'SentImmediately', nextEligibleSendAt: null };
      } else if (path.endsWith('/conversations/9/close')) { thread.status = 'Closed'; thread.rowVersion = 'v3'; data = thread; }
      else if (path.endsWith('/read')) data = true;
      else if (path.endsWith('/notification-center/state')) data = { available: true, unreadCount: 0 };
      else if (path.endsWith('/push/config')) data = { enabled: false, pwaUiEnabled: false };
      else if (path.endsWith('/auth/schools')) data = [];
      else if (path.endsWith('/capabilities')) data = { canAccess: false };
      await route.fulfill({ json: { isSuccess: true, data, errors: [], message: '' } });
    });
    await page.goto(origin + '/student-affairs/messages');
    await page.locator('.thread-item').first().waitFor();
    await expect(page.locator('.connection-state')).toHaveText('متصل مباشرة');
    if (width <= 860) {
      const menu = page.getByRole('button', { name: 'فتح القائمة الرئيسية' });
      const before = await page.locator('.chat-shell').boundingBox();
      await menu.click();
      const drawer = page.getByRole('dialog', { name: 'القائمة الرئيسية' });
      await expect(drawer).toBeVisible();
      assert.deepEqual(await page.locator('.chat-shell').boundingBox(), before, 'drawer must not displace the chat');
      await page.keyboard.press('Shift+Tab');
      assert(await drawer.evaluate(el => el.contains(document.activeElement)), 'drawer traps keyboard focus');
      await page.screenshot({ path: resolve(evidence, `navigation-${width}.png`) });
      await page.keyboard.press('Escape');
      await expect(menu).toBeFocused();
      await expect(drawer).not.toBeVisible();
      await page.screenshot({ path: resolve(evidence, `inbox-${width}.png`) });
      await page.locator('.thread-item').first().click();
    }
    await expect(page.locator('article.message')).toHaveCount(4);
    if (width <= 860) {
      await expect(page.locator('.messages-page > .page-header')).not.toBeVisible();
      await expect(page.locator('.conversation-context')).not.toBeVisible();
      const headerBox = await page.locator('.conversation-header').boundingBox();
      assert(headerBox.height <= 64, 'mobile conversation uses one compact header');
      const details = page.getByRole('button', { name: 'تفاصيل المحادثة', exact: true });
      await details.click();
      await expect(page.locator('.details-participants')).toBeVisible();
      await expect(page.locator('.conversation-context')).toBeVisible();
      await expect(page.getByRole('button', { name: 'إغلاق المحادثة', exact: true })).toBeVisible();
      await page.screenshot({ path: resolve(evidence, `conversation-details-${width}.png`) });
      await details.click();
      await expect(page.locator('.conversation-context')).not.toBeVisible();
    }
    const composer = page.getByRole('textbox', { name: 'نص الرسالة' });
    await composer.fill('شكرًا لتواصلكم، سنستمر في المتابعة معًا.');
    await page.getByRole('button', { name: 'إرسال الرسالة', exact: true }).click();
    await expect(page.locator('article.message')).toHaveCount(5);
    await expect(composer).toHaveValue('');
    await composer.fill('مسودة لم تكتمل');
    await page.evaluate(() => { window.chatRow = document.querySelector('.thread-item'); window.chatRowRemoved = false;
      new MutationObserver(() => { if (!window.chatRow.isConnected) window.chatRowRemoved = true; })
        .observe(document.querySelector('.inbox'), { subtree: true, childList: true }); });
    delayList = true;
    const previousLists = listStarted;
    messages.push(message(6, 'وصلت رسالتكم الآن، جزاكم الله خيرًا على هذا التواصل.'));
    socket.send(JSON.stringify({ type: 1, target: 'InboxChanged.v1', arguments: [{}] }) + '\x1e');
    await expect(page.locator('article.message')).toHaveCount(6);
    await expect.poll(() => listStarted).toBeGreaterThan(previousLists);
    await page.waitForTimeout(800);
    assert.equal(await page.evaluate(() => window.chatRowRemoved), false, 'background update preserves the existing row');
    await expect(composer).toHaveValue('مسودة لم تكتمل');
    const inputBox = await composer.boundingBox();
    assert(inputBox.x >= 0 && inputBox.x + inputBox.width <= width + 1, 'composer fits viewport width');
    assert(inputBox.y + inputBox.height <= height, 'composer stays inside viewport height');
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1), false);
    await page.screenshot({ path: resolve(evidence, `conversation-${width}.png`) });
    await page.getByRole('button', { name: /ردود سريعة/ }).click();
    await page.locator('.reply-tools button').first().click();
    await expect(composer).toHaveValue(/مسودة لم تكتمل\nتمت مراجعة الحالة/);
    await expect(page.locator('.reply-tools')).not.toBeVisible();
    if (width <= 860) {
      await page.setViewportSize({ width, height: 580 });
      await expect.poll(async () => { const small = await composer.boundingBox(); return small.y + small.height; },
        { message: 'composer remains reachable with a shorter viewport' }).toBeLessThanOrEqual(580);
      await page.setViewportSize({ width, height });
      await page.getByRole('button', { name: 'العودة إلى صندوق المحادثات' }).click();
      await expect(page.locator('.thread-item')).toBeVisible();
      await page.getByRole('button', { name: /الأشخاص/ }).click();
      await page.locator('.person-item').click();
      await expect(page.locator('.contact-thread-card')).toBeVisible();
      await page.locator('.contact-thread-card').click();
      await expect(composer).toBeVisible();
    }
    if (width <= 860) await page.getByRole('button', { name: 'تفاصيل المحادثة', exact: true }).click();
    await page.getByRole('button', { name: 'إغلاق المحادثة', exact: true }).click();
    await page.getByRole('textbox', { name: /سبب الإغلاق/ }).fill('اكتملت المتابعة مع ولي الأمر');
    await page.getByRole('button', { name: /تأكيد الإغلاق/ }).click();
    await expect(page.locator('.closed-note')).toBeVisible();
    await expect(page.getByRole('button', { name: 'إرسال الرسالة', exact: true })).toBeDisabled();
    await expect(page.locator('article.message')).toHaveCount(6);
    assert.deepEqual(errors, [], 'no browser runtime errors');
    results.push({ width, passed: true, checks: ['SignalR-arrival-without-click','send-immediate','no-list-remount',
      'draft-preserved','composer-fits','quick-replies','close-keeps-history', ...(width <= 860 ? ['compact-header','expandable-details','overlay-navigation','focus-trap','escape','short-viewport','people-history'] : [])] });
    await page.close();
  }
} finally { await browser.close(); }
writeFileSync(resolve(evidence, 'results.json'), JSON.stringify({ mode: 'synthetic-api-and-websocket', results }, null, 2));
console.log(JSON.stringify(results, null, 2));
