// Actual Angular/Chromium UI with synthetic intercepted API responses. No live provider/device claim.
import assert from 'node:assert/strict';
import { mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { chromium, expect } from '@playwright/test';
const origin = process.env.ALFALAH_N2_ORIGIN || 'http://127.0.0.1:4316';
const evidence = resolve('../docs/specs/notifications-pwa/verification/n2');
mkdirSync(evidence, { recursive: true });
const browser = await chromium.launch({ headless: true });
const checks = [];
const now = Date.now();
const fixtures = [
  ['messages', 'رسالة جديدة من ولي الأمر', 'السلام عليكم، أود متابعة مستوى ابني الدراسي ومعرفة ما يمكننا العمل عليه معًا خلال هذا الأسبوع.', 'info'],
  ['duty', 'تم توقيع تقرير المناوبة وإرساله', 'التقرير الموقّع متاح للمراجعة ضمن مستندات المناوبة.', 'resolved'],
  ['approvals', 'تقييم زيارة بانتظار الاعتماد', 'تم إرسال تقييم الزيارة للمراجعة والاعتماد. يمكنك الاطلاع على التقرير قبل اتخاذ القرار.', 'pending'],
  ['attendance', 'اكتمل رصد حضور الفصل', 'اكتمل رصد الحضور ليوم 2026-10-18. يمكنك الاطلاع على كشف الفصل.', 'info'],
  ['complaints', 'رد جديد على شكواك', 'تمت مراجعة طلبك وإضافة الرد. افتح الشكوى للاطلاع على التفاصيل.', 'resolved']
].map((item, i) => ({ id: `10000000-0000-4000-8000-${String(i + 1).padStart(12, '0')}`, category: item[0], title: item[1], preview: item[2],
  taskState: item[3], createdAtUtc: new Date(now - (i * 17 + 3) * 60000).toISOString(), readAtUtc: i === 4 ? new Date(now).toISOString() : null,
  sourceType: i === 0 ? 'Message' : 'Notification', sourceId: i + 1 }));
for (const width of [1440, 390, 320]) {
  const page = await browser.newPage({ viewport: { width, height: width < 600 ? 844 : 1000 }, reducedMotion: 'reduce' });
  const errors = [], mutations = [], listQueries = [];
  let entries = structuredClone(fixtures);
  const user = { userId: 'synthetic-n2', fullName: 'أحمد عبد الله', username: 'synthetic', activeSchoolId: 1,
    activeSchoolName: 'مدارس الفلاح', roles: ['SchoolManager'], permissions: ['Notification.ViewOwn', 'Messaging.ViewOwn'], preferredLanguage: 'ar' };
  page.on('pageerror', error => errors.push(error.message));
  await page.addInitScript(user => {
    sessionStorage.setItem('alfalah_access_token', `e30.${btoa(JSON.stringify({sub: user.userId, exp: 2000000000}))}.fixture`);
    sessionStorage.setItem('alfalah_user', JSON.stringify(user));
  }, user);
  await page.route('**/api/**', async route => {
    const url = new URL(route.request().url()), path = url.pathname;
    const method = route.request().method(); let data = {};
    if (method !== 'GET') mutations.push(path);
    if (path.endsWith('/auth/me')) data = user;
    else if (path.endsWith('/account/signature/requirement')) data = { required: false, hasSignature: true };
    else if (path.endsWith('/inbox/state')) data = { unreadMessages: 121, unreadConversations: 121, unreadNotifications: 4, syncCursor: 'fixture', realtimeAvailable: false, asOfUtc: new Date().toISOString() };
    else if (path.endsWith('/inbox/changes')) data = { items: [], nextCursor: 'fixture', hasMore: false, resyncRequired: false };
    else if (path.endsWith('/notification-center/state')) data = { available: true, unreadCount: entries.filter(x => !x.readAtUtc).length };
    else if (path.endsWith('/notification-center') && method === 'GET') {
      listQueries.push(url.searchParams.toString());
      data = { items: entries.filter(x => (!url.searchParams.get('category') || x.category === url.searchParams.get('category')) && (url.searchParams.get('unread') !== 'true' || !x.readAtUtc)), nextCursor: null };
    } else if (path.endsWith('/read-all')) { entries.forEach(x => x.readAtUtc = new Date().toISOString()); data = true; }
    else if (/notification-center\/[^/]+\/read$/.test(path)) { const id = path.split('/').at(-2); entries.find(x => x.id === id).readAtUtc = new Date().toISOString(); data = true; }
    else if (path.endsWith('/target')) data = { available: true, route: '/notifications/detail/' + fixtures[0].id, query: {} };
    else if (path.endsWith('/detail')) data = { title: fixtures[0].title, preview: fixtures[0].preview, state: 'pending', sourceId: 1, sourceType: 'Notification', rows: [{label: 'السجل', value: 'عنصر التحقق المحدد'}] };
    else if (path.endsWith('/push/config')) data = { enabled: false, pwaUiEnabled: true };
    else if (path.endsWith('/push/subscriptions')) data = [];
    else if (path.endsWith('/school-duty/dashboard-summary')) data = {canManage: false};
    else if (path.endsWith('/auth/schools')) data = [];
    await route.fulfill({ json: { isSuccess: true, data, errors: [] } });
  });
  await page.goto(origin + '/notifications');
  await page.locator('.notification-center.history .notification-entry').first().waitFor();
  const bell = page.locator('button.bell');
  await bell.click();
  const panel = page.getByRole('dialog', { name: 'مركز الإشعارات' });
  await panel.waitFor();
  await panel.locator('.notification-entry').first().waitFor();
  assert.equal(mutations.length, 0, 'opening/history must not read or decide');
  assert.match(await bell.getAttribute('aria-label'), /4/);
  await expect(panel.locator('.task-state.pending')).toBeVisible();
  const box = await panel.boundingBox();
  if (!(box.y >= 0 && box.y + box.height <= (width < 600 ? 844 : 1000) + 1)) console.log(JSON.stringify({width,box,ancestors:await panel.evaluate(el => {const result=[]; for(let p=el.parentElement;p;p=p.parentElement){const s=getComputedStyle(p);result.push({tag:p.tagName,cls:p.className,transform:s.transform,filter:s.filter,backdrop:s.backdropFilter,contain:s.contain});}return result;})}));
  assert(box.x >= 0 && box.x + box.width <= width + 1, 'panel fits viewport');
  assert(box.y >= 0 && box.y + box.height <= (width < 600 ? 844 : 1000) + 1, 'panel fits viewport height');
  await page.screenshot({ path: resolve(evidence, `center-${width}.png`), fullPage: false });
  await panel.getByRole('button', {name:'غير المقروءة',exact:true}).click();
  await expect(panel.locator('.notification-entry')).toHaveCount(4);
  await panel.getByRole('combobox').selectOption('approvals');
  await expect(panel.locator('.notification-entry')).toHaveCount(1);
  await panel.getByRole('button', {name:/تحديد كمقروء/}).click();
  await page.waitForTimeout(250);
  assert.equal(entries.find(x=>x.category==='approvals').taskState, 'pending', 'read never decides a task');
  await panel.getByRole('button', {name:'الكل',exact:true}).click();
  await page.waitForTimeout(150);
  await expect(panel.locator('.task-state.pending')).toBeVisible();
  await page.keyboard.press('Escape');
  assert(await bell.evaluate(el => document.activeElement === el), 'Escape restores focus');
  await bell.click(); await panel.waitFor();
  await page.keyboard.press('Shift+Tab');
  assert(await panel.evaluate(el => el.contains(document.activeElement)), 'keyboard focus trapped');
  await panel.getByRole('combobox').selectOption('');
  await page.waitForTimeout(150);
  const beforeTarget = mutations.length;
  await panel.getByRole('button', {name: /^رسالة جديدة من ولي الأمر/}).click();
  await page.locator('.notification-detail').waitFor();
  assert.equal(mutations.length, beforeTarget, 'target navigation never reads/approves');
  assert(await page.getByText('عنصر التحقق المحدد').isVisible());
  assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth + 1), false, 'no horizontal overflow');
  assert.deepEqual(errors, []);
  checks.push({ width, passed: true, checks: ['open-does-not-read', 'authoritative-independent-count', 'unread-and-category-filter', 'read-preserves-task', 'escape-and-focus-trap', 'exact-source-navigation', 'no-overflow'], listQueries });
  await page.close();
}
await browser.close();
writeFileSync(resolve(evidence, 'browser-results.json'), JSON.stringify({ date: '2026-10-17', environment: 'Desktop Chromium; responsive widths; intercepted synthetic HTTP; no Web Push delivery', checks }, null, 2));
console.log(JSON.stringify({ passed: checks.length, widths: checks.map(x=>x.width), evidence }, null, 2));
