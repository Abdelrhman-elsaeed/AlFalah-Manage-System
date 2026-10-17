// Real browser layout check. No business writes or notification sends.
import assert from 'node:assert/strict';
import { chromium, expect } from '@playwright/test';
const origin = process.env.ALFALAH_SESSION_ORIGIN || 'http://localhost:4200';
const browser = await chromium.launch();
try {
  for (const width of [320, 390, 768, 1440]) {
    const page = await browser.newPage({ viewport: { width, height: 900 }, reducedMotion: 'reduce' });
    await page.goto(origin + '/#system-story');
    await page.locator('app-system-story').scrollIntoViewIfNeeded();
    await expect(page.locator('#falah-section .af-segments button')).toHaveCount(10);
    const layout = await page.locator('#falah-section .af-segments').evaluate(nav => {
      const buttons = [...nav.querySelectorAll('button')].map(button => button.getBoundingClientRect());
      const bounds = nav.getBoundingClientRect();
      return { rows: new Set(buttons.map(b => Math.round(b.top))).size,
        fits: buttons.every(b => b.left >= bounds.left - 1 && b.right <= bounds.right + 1) };
    });
    assert.equal(layout.rows, 1, `${width}px: story navigation must occupy one row`);
    assert.equal(layout.fits, true, `${width}px: every chapter must fit within the navigation`);
    console.log(`PASS ${width}px: one story navigation row; ten chapters fit`);
    await page.close();
  }
  const user = { userId: 'session-browser-fixture', username: 'session.fixture', fullName: 'Session Test',
    preferredLanguage: 'ar', activeSchoolId: 1, roles: ['SchoolManager'], permissions: [] };
  const accessToken = `e30.${Buffer.from(JSON.stringify({ sub: user.userId, exp: Math.floor(Date.now() / 1000) + 3600 })).toString('base64url')}.fixture`;
  async function mockApi(context) {
    await context.route('**/api/**', route => {
      const path = new URL(route.request().url()).pathname;
      const data = path.endsWith('/auth/me') ? user
        : path.endsWith('/signature/requirement') ? { required: false }
        : path.endsWith('/inbox/state') ? { unreadMessages: 0, unreadNotifications: 0, realtimeAvailable: false }
        : path.endsWith('/notification-center/state') ? { available: true, unreadCount: 0 }
        : path.endsWith('/push/config') ? { enabled: false, pwaUiEnabled: false }
        : {};
      return route.fulfill({ json: { isSuccess: true, errors: [], data } });
    });
  }
  const context = await browser.newContext({ reducedMotion: 'reduce' });
  await mockApi(context);
  const page = await context.newPage();
  await page.goto(origin);
  await page.evaluate(({ user, accessToken }) => {
    sessionStorage.setItem('alfalah_access_token', accessToken);
    sessionStorage.setItem('alfalah_refresh_token', 'fixture-refresh');
    sessionStorage.setItem('alfalah_user', JSON.stringify(user));
  }, { user, accessToken });
  await page.goto(origin + '/auth/school-login');
  await expect(page).toHaveURL(/\/school-manager\/dashboard$/);
  await page.goto(origin);
  await page.locator('app-landing-header .login').click();
  await expect(page).toHaveURL(/\/school-manager\/dashboard$/);
  assert.equal(await page.evaluate(() => sessionStorage.getItem('alfalah_refresh_token')), null);
  const saved = await context.storageState();
  await context.close();
  const reopened = await browser.newContext({ storageState: saved, reducedMotion: 'reduce' });
  await mockApi(reopened);
  const restored = await reopened.newPage();
  await restored.goto(origin + '/auth/school-login');
  await expect(restored).toHaveURL(/\/school-manager\/dashboard$/);
  const otherTab = await reopened.newPage();
  await otherTab.goto(origin);
  await otherTab.evaluate(() => {
    for (const key of ['alfalah_access_token', 'alfalah_refresh_token', 'alfalah_user']) localStorage.removeItem(key);
  });
  await expect(restored).toHaveURL(/\/auth\/school-login$/);
  await reopened.close();
  console.log('PASS real Angular routing: legacy migration, homepage entry, reopen with persistent storage, and logout across tabs');
} finally { await browser.close(); }
