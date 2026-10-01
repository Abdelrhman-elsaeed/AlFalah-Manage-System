import { expect, test } from '@playwright/test';
import { openRoleSession } from '../support/role-session';

test.describe('Student Affairs responsive and dialog regressions', () => {
  test('messages use a compact one-pane master/detail flow on a phone', async ({ browser }) => {
    const session = await openRoleSession(browser, 'officer', { width: 390, height: 844 });
    const page = session.page;
    try {
      await page.goto('/student-affairs/messages');
      await expect(page.getByRole('heading', { name: 'مركز الرسائل' })).toBeVisible();
      const shell = page.locator('.chat-shell');
      const inbox = page.locator('.inbox');
      const conversation = page.locator('.conversation');
      await expect(inbox).toBeVisible();
      await expect(conversation).toBeHidden();
      expect((await shell.boundingBox())!.height).toBeLessThanOrEqual(720);

      await page.locator('.thread-item').first().click();
      await expect(inbox).toBeHidden();
      await expect(conversation).toBeVisible();
      const back = page.getByRole('button', { name: 'العودة إلى صندوق المحادثات' });
      await expect(back).toBeVisible();
      await back.click();
      await expect(inbox).toBeVisible();
      await expect(conversation).toBeHidden();
    } finally {
      await session.context.close();
    }
  });

  test('PrimeNG dialog header controls keep visible icon geometry', async ({ browser }) => {
    const session = await openRoleSession(browser, 'officer');
    const page = session.page;
    try {
      await page.goto('/student-affairs/messages');
      await page.getByRole('button', { name: 'محادثة جديدة' }).click();
      const dialog = page.locator('.p-dialog').filter({ hasText: 'محادثة جديدة' });
      await expect(dialog).toBeVisible();
      const iconWrappers = dialog.locator('.p-dialog-header-icons .p-icon-wrapper');
      expect(await iconWrappers.count()).toBeGreaterThan(0);
      for (const wrapper of await iconWrappers.all()) {
        const box = await wrapper.boundingBox();
        expect(box?.width ?? 0).toBeGreaterThanOrEqual(12);
        expect(box?.height ?? 0).toBeGreaterThanOrEqual(12);
        await expect(wrapper.locator('svg')).toBeVisible();
      }
    } finally {
      await session.context.close();
    }
  });
});
