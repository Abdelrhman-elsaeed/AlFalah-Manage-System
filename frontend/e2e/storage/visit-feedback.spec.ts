import { expect, test } from '@playwright/test';

test('visit evaluator can add multiple strengths and improvements to a standard', async ({ page }) => {
  const user = { userId: 'manager', username: 'manager', fullName: 'مدير المدرسة', activeSchoolId: 1,
    activeSchoolName: 'مدرسة الاختبار', preferredLanguage: 'ar', roles: ['SchoolManager'], permissions: ['Visit.Create'] };
  await page.addInitScript(value => {
    const payload = btoa(JSON.stringify({ sub: 'manager', exp: 2000000000 }));
    sessionStorage.setItem('alfalah_access_token', `e30.${payload}.mock`);
    sessionStorage.setItem('alfalah_user', JSON.stringify(value));
  }, user);
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    let data: unknown = {};
    if (path.endsWith('/auth/me')) data = user;
    else if (path.endsWith('/api/v2/visits/availability')) data = { isEnabled: true };
    else if (path.endsWith('/api/v2/visits/feedback-bank')) data = [
      { id: 1, kind: 1, text: 'تميز في الشرح' }, { id: 2, kind: 1, text: 'تميز في مشاركة الطلاب' },
      { id: 3, kind: 2, text: 'تحسين التقويم' }, { id: 4, kind: 2, text: 'تنويع الأنشطة' }
    ];
    else if (path.endsWith('/api/v2/visits/observation-card')) data = {
      rubricVersionId: 1, rubricVersionNumber: 2, scoreLabels: [], domains: [{
        id: 1, code: 'D1', nameAr: 'بيئة التعلم', sortOrder: 1, standards: [{
          id: 21, code: 'D1-S1', textAr: 'يوفر المعلم بيئة تعلم آمنة.', sortOrder: 1,
          score: 3, evidenceNote: '', indicators: [{ id: 31, code: 'I1', textAr: 'تفاعل المتعلمين مع النشاط', sortOrder: 1, isObserved: false }]
        }]
      }]
    };
    else if (path.endsWith('/teachers')) data = { items: [{ userId: 'teacher', fullName: 'المعلم التجريبي', isActive: true }] };
    else if (path.endsWith('/teaching')) data = { subject: 'رياضيات', classes: ['الثاني أ'] };
    else if (path.endsWith('/api/v2/visits') && route.request().method() === 'POST') data = { id: 42 };
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ isSuccess: true, data }) });
  });

  await page.goto('/visits');
  await expect(page.locator('#v2-teacher')).toBeVisible();
  await page.locator('#v2-teacher').selectOption('teacher');
  await page.locator('#v2-subject').fill('رياضيات');
  await page.locator('#v2-class').fill('الثاني أ');
  await page.locator('#v2-lesson').fill('درس تجريبي');
  await page.locator('.next-button').click();
  await page.locator('.evidence-toggle').first().click();
  const strength = page.locator('.feedback-field.strength select').first();
  const improvement = page.locator('.feedback-field.improvement select').first();
  await expect(strength.locator('option')).toHaveCount(3);
  await strength.selectOption({ label: 'تميز في الشرح' });
  await strength.selectOption({ label: 'تميز في مشاركة الطلاب' });
  await improvement.selectOption({ label: 'تحسين التقويم' });
  await improvement.selectOption({ label: 'تنويع الأنشطة' });
  await expect(page.locator('.feedback-field.strength .selected-feedback')).toHaveCount(2);
  await expect(page.locator('.feedback-field.improvement .selected-feedback')).toHaveCount(2);

  const update = page.waitForRequest(request => request.url().endsWith('/api/v2/visits/42') && request.method() === 'PUT');
  await page.locator('.action-group button').first().click();
  const payload = (await update).postDataJSON();
  expect(payload.scores[0]).toMatchObject({ rubricStandardId: 21,
    strengthNotes: ['تميز في الشرح', 'تميز في مشاركة الطلاب'],
    improvementNotes: ['تحسين التقويم', 'تنويع الأنشطة'] });
});

test('bank manager can add, edit, and delete a school phrase', async ({ page }) => {
  const user = { userId: 'manager', username: 'manager', fullName: 'مدير المدرسة', activeSchoolId: 1,
    activeSchoolName: 'مدرسة الاختبار', preferredLanguage: 'ar', roles: ['SchoolManager'], permissions: ['Visit.Edit'] };
  const bank: { id: number; kind: 1 | 2; text: string }[] = [];
  await page.addInitScript(value => {
    const payload = btoa(JSON.stringify({ sub: 'manager', exp: 2000000000 }));
    sessionStorage.setItem('alfalah_access_token', `e30.${payload}.mock`);
    sessionStorage.setItem('alfalah_user', JSON.stringify(value));
  }, user);
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    const method = route.request().method();
    let data: unknown = {};
    if (path.endsWith('/auth/me')) data = user;
    else if (path.endsWith('/api/v2/visits/availability')) data = { isEnabled: true };
    else if (path.endsWith('/api/v2/visits/observation-card')) data = { domains: [], scoreLabels: [] };
    else if (path.endsWith('/api/v2/visits/feedback-bank')) {
      if (method === 'POST') { data = { id: 10, ...route.request().postDataJSON() }; bank.push(data as typeof bank[number]); }
      else data = [...bank];
    } else if (path.endsWith('/api/v2/visits/feedback-bank/10')) {
      if (method === 'PUT') { data = { id: 10, ...route.request().postDataJSON() }; bank[0] = data as typeof bank[number]; }
      if (method === 'DELETE') bank.splice(0, 1);
    }
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ isSuccess: true, data }) });
  });
  page.on('dialog', dialog => dialog.accept());
  await page.goto('/visits');
  await page.getByRole('button', { name: 'بنك عبارات الزيارة' }).click();
  await page.locator('input[name="bankText"]').fill('عبارة جديدة');
  await page.getByRole('button', { name: 'إضافة للبنك' }).click();
  await expect(page.locator('.bank-row')).toContainText('عبارة جديدة');
  await page.locator('.bank-row [aria-label="تعديل العبارة"]').click();
  await page.locator('.bank-row textarea').fill('عبارة معدلة');
  await page.locator('.bank-row').getByRole('button', { name: 'حفظ' }).click();
  await expect(page.locator('.bank-row')).toContainText('عبارة معدلة');
  await page.locator('.bank-row [aria-label="حذف العبارة"]').click();
  await expect(page.locator('.bank-row')).toHaveCount(0);
});
