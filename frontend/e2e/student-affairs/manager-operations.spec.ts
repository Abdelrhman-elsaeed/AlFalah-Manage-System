import { expect, test, type Browser } from '@playwright/test';
import { apiForRole, successful, type PageResult } from '../support/api-session';
import { openRoleSession } from '../support/role-session';

interface TeacherOption { instructorProfileId: number; }

test.describe('School manager Student Affairs operations', () => {
  test('manager APIs and all four operational surfaces stay usable', async ({ browser }) => {
    const manager = await apiForRole('manager');
    try {
      await successful(await manager.get('/api/v1/student-affairs/dashboard/school-oversight'));
      const teachers = await successful<TeacherOption[]>(await manager.get('/api/v1/office-hours/teachers'));
      expect(teachers.length).toBeGreaterThan(0);
      await successful(await manager.get(`/api/v1/office-hours/teachers/${teachers[0].instructorProfileId}`));
      await successful<PageResult<unknown>>(await manager.get('/api/v1/gate-passes/manager-audit?pageNumber=1&pageSize=12'));
      await successful<PageResult<unknown>>(await manager.get('/api/v1/conversations/audit?pageNumber=1&pageSize=20'));

      await expectManagerPage(browser, '/student-affairs/oversight', 'الإشراف المدرسي');
      await expectManagerPage(browser, '/student-affairs/office-hours/manage', 'إدارة الساعات المكتبية');
      await expectManagerPage(browser, '/student-affairs/gate-passes/audit', 'تدقيق استئذانات الخروج');
      await expectManagerPage(browser, '/student-affairs/messaging-audit', 'تدقيق المراسلات');
      await expectManagerPage(browser, '/school-manager/dashboard', 'مركز عمليات شؤون الطلاب');
      if (process.env['E2E_VISUAL_REVIEW'] === '1') {
        await expectManagerPage(browser, '/student-affairs/oversight', 'الإشراف المدرسي', { width: 390, height: 844 }, 'mobile');
        await expectManagerPage(browser, '/school-manager/dashboard', 'مركز عمليات شؤون الطلاب', { width: 390, height: 844 }, 'mobile');
      }
    } finally {
      await manager.dispose();
    }
  });
});

async function expectManagerPage(
  browser: Browser,
  path: string,
  heading: string,
  viewport?: { width: number; height: number },
  suffix = 'desktop'
): Promise<void> {
  const session = await openRoleSession(browser, 'manager', viewport);
  try {
    await session.page.goto(path);
    await expect(session.page).toHaveURL(new RegExp(path.replaceAll('/', '\\/')));
    await expect(session.page.getByText(heading, { exact: false }).first()).toBeVisible();
    await expect(session.page.locator('[role="alert"]')).toHaveCount(0);
    if (process.env['E2E_VISUAL_REVIEW'] === '1') {
      if (path === '/school-manager/dashboard') {
        await session.page.locator('.student-affairs-command').scrollIntoViewIfNeeded();
        await expect(session.page.locator('.dashboard-loading')).toHaveCount(0);
      }
      const name = path.split('/').filter(Boolean).join('-');
      await session.page.screenshot({ path: `test-results/manager-${name}-${suffix}.png`, fullPage: true });
    }
  } finally {
    await session.context.close();
  }
}
