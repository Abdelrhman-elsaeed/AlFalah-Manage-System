import { expect, test } from '@playwright/test';
import { openRoleSession } from '../support/role-session';

const landings = [
  ['secretary', '/student-affairs/attendance/sheet'],
  ['instructor', '/student-affairs/teacher'],
  ['officer', '/student-affairs/officer'],
  ['guardian', '/student-affairs/guardian'],
  ['security', '/student-affairs/security'],
  ['socialWorker', '/student-affairs/social-worker'],
  ['manager', '/school-manager/dashboard']
] as const;

test('Role sessions — seven isolated actors reach only their canonical landing', async ({ browser }) => {
  for (const [role, expectedPath] of landings) {
    const session = await openRoleSession(browser, role);
    await session.page.goto(expectedPath);
    await expect(session.page).toHaveURL(new RegExp(`${expectedPath.replaceAll('/', '\\/')}(?:$|\\?)`));
    await expect.poll(() => session.page.evaluate(() => getComputedStyle(document.body).direction)).toBe('rtl');
    await session.context.close();
  }
});

test('Role guards — Guardian cannot open the Officer workspace even by direct URL @mobile', async ({ browser }) => {
  const session = await openRoleSession(browser, 'guardian', { width: 390, height: 844 });
  await session.page.goto('/student-affairs/officer');
  await expect(session.page).toHaveURL(/\/unauthorized(?:$|\?)/);
  await session.context.close();
});

test('Scenario A mobile — Guardian → Officer critical surfaces remain RTL and usable @mobile', async ({ browser }) => {
  for (const [role, path] of [
    ['guardian', '/student-affairs/guardian/excuses'],
    ['officer', '/student-affairs/officer/excuses']
  ] as const) {
    const session = await openRoleSession(browser, role, { width: 390, height: 844 });
    await session.page.goto(path);
    await expect(session.page).toHaveURL(new RegExp(path.replaceAll('/', '\\/')));
    await expect(session.page.locator('main, section.page').first()).toBeVisible();
    await expect.poll(() => session.page.evaluate(() => getComputedStyle(document.body).direction)).toBe('rtl');
    await session.context.close();
  }
});
