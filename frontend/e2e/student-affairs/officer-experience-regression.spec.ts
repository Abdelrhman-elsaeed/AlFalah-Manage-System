import { expect, test } from '@playwright/test';
import { openRoleSession } from '../support/role-session';

test.describe('Student Affairs officer experience regression', () => {
  test('operational tabs settle and entry permits offer classroom-first student selection', async ({ browser }) => {
    const session = await openRoleSession(browser, 'officer');
    const page = session.page;

    try {
      await page.goto('/student-affairs/officer/excuses');
      await expect(page.getByRole('heading', { name: /مراجعة أعذار الغياب/ })).toBeVisible();
      await expect(page.getByRole('status')).toHaveCount(0, { timeout: 8_000 });
      await expect(page.getByText(/جارٍ جمع الأعذار المعلقة/)).toHaveCount(0);

      await page.goto('/student-affairs/officer/entry-permits');
      await expect(page.getByRole('heading', { name: /تصاريح دخول الفصل/ })).toBeVisible();
      await expect(page.getByRole('status')).toHaveCount(0, { timeout: 8_000 });
      await expect(page.getByLabel('الفصل')).toBeVisible();
      await expect(page.getByLabel('الطالب')).toBeVisible();

      await page.goto('/student-affairs/officer/referrals');
      await expect(page.getByRole('heading', { name: /إنشاء وإسناد الإحالات/ })).toBeVisible();
      await expect(page.getByRole('status')).toHaveCount(0, { timeout: 8_000 });
      await expect(page.getByRole('button', { name: /إنشاء الحالة/ })).toBeVisible();

      await page.goto('/student-affairs/officer/operations');
      await expect(page.getByRole('heading', { name: /السجلات التشغيلية/ })).toBeVisible();
      await expect(page.locator('.record-type-card')).toHaveCount(5);
      await expect(page.getByText('مركز المتابعة')).toBeVisible();

      await page.goto('/student-affairs/officer/guide');
      await expect(page.getByRole('heading', { name: /كل أدوات شؤون الطلاب/ })).toBeVisible();
      await expect(page.locator('.feature-card')).toHaveCount(11);
      await expect(page.getByText('دليل العمل اليومي')).toBeVisible();
    } finally {
      await session.context.close();
    }
  });
});
