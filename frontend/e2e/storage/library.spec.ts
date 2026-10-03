import { test, expect, Page } from '@playwright/test';

const file = { storedFileId: 31, folderId: 7, displayName: 'خطة المدرسة.pdf', size: 1000,
  mimeType: 'application/pdf', uploadedAt: '2026-10-03T10:00:00Z', state: 'Managed', isProtected: false, rowVersion: 'AAAAAAAAAAE=' };
async function mockSession(page: Page, role = 'SchoolManager', state = 'Connected') {
  const user = { userId: 'ui-test', username: 'ui-test', fullName: 'مستخدم الاختبار', activeSchoolId: 1,
    activeSchoolName: 'مدرسة الاختبار', preferredLanguage: 'ar', roles: [role], permissions: ['Storage.ViewSchool', 'Storage.ManageSchool', 'Storage.ViewOwn', 'Storage.ManageOwn'] };
  await page.addInitScript(user => {
    const payload = btoa(JSON.stringify({ sub: 'ui-test', exp: 2000000000 }));
    sessionStorage.setItem('alfalah_access_token', `e30.${payload}.mock`);
    sessionStorage.setItem('alfalah_user', JSON.stringify(user));
  }, user);
  const requests: { url: string; key?: string; body?: string }[] = [];
  await page.route('**/api/**', async route => {
    const req = route.request(); const url = new URL(req.url()); const path = url.pathname;
    requests.push({ url: req.url(), key: req.headers()['idempotency-key'], body: req.postData() || undefined });
    let data: unknown = {};
    if (path.endsWith('/auth/me')) data = user;
    else if (path.endsWith('/auth/schools')) data = [];
    else if (path.endsWith('/storage/context')) data = { schoolId: 1, schoolName: 'مدرسة الاختبار', academicYearId: 1,
      academicYearName: 'السنة الدراسية', canManage: true, isTeacher: role === 'Instructor', connectionState: state, rootFolderId: 7 };
    else if (path.endsWith('/storage/folders')) data = { items: [], total: 0, page: 1, pageSize: 25 };
    else if (path.endsWith('/storage/files/31/content')) return route.fulfill({ status: 200, contentType: 'application/pdf', body: '%PDF-1.7\n1 0 obj<</Type /Catalog>>endobj\n%%EOF' });
    else if (path.endsWith('/storage/files/31')) data = { file, versions: [{ versionId: 1, versionNumber: 1, size: 1000, mimeType: 'application/pdf', uploadedAt: file.uploadedAt, availability: 'Available' }] };
    else if (path.endsWith('/storage/files') || path.endsWith('/storage/me/files')) {
      if (req.method() === 'POST') data = { operationId: 1, storedFileId: 31, versionId: 1, status: 'Completed', displayName: file.displayName, size: 1000, mimeType: file.mimeType, uploadedAt: file.uploadedAt };
      else data = { items: [file], total: 5000, page: Number(url.searchParams.get('page') || 1), pageSize: 25 };
    }
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ isSuccess: true, data }) });
  });
  return requests;
}

test('manager and delegated role use RTL library, server pagination and authorized preview', async ({ page }) => {
  const requests = await mockSession(page, 'Secretary'); // Delegate access comes from live API, without a manager role.
  await page.goto('/school-manager/storage');
  await expect(page.locator('h1')).toHaveText('مكتبة المدرسة');
  await expect(page.locator('.storage-page')).toHaveAttribute('dir', 'rtl');
  await expect(page.locator('.file-name button')).toHaveText(file.displayName);
  await page.locator('.file-name button').click();
  await page.getByRole('button', { name: 'معاينة', exact: true }).click();
  await expect(page.locator('iframe')).toHaveAttribute('src', /^blob:/);
  expect(requests.some(r => r.url.includes('/storage/files/31/content?preview=true'))).toBeTruthy();
  await page.keyboard.press('Escape'); await page.keyboard.press('Escape');
  await page.locator('.p-paginator-next').click();
  await expect.poll(() => requests.some(r => r.url.includes('/storage/files?') && r.url.includes('page=2'))).toBeTruthy();
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1);
  expect(overflow).toBeFalsy();
  await page.screenshot({ path: `test-results/storage/library-${test.info().project.name}.png`, fullPage: true });
});

test('teacher only uses own file endpoints and cancels queued upload before sending', async ({ page }) => {
  const requests = await mockSession(page, 'Instructor');
  await page.goto('/instructor/my-files'); await expect(page.locator('h1')).toHaveText('ملفاتي');
  await expect(page.getByRole('button', { name: 'مجلد جديد' })).toHaveCount(0);
  await page.locator('input[type=file]').setInputFiles({ name: 'شاهد.pdf', mimeType: 'application/pdf', buffer: Buffer.from('%PDF-1.7\nTest') });
  await page.getByRole('button', { name: 'إلغاء قبل الإرسال' }).click();
  expect(requests.filter(r => r.key).length).toBe(0);
  await page.locator('input[type=file]').setInputFiles({ name: 'شاهد.pdf', mimeType: 'application/pdf', buffer: Buffer.from('%PDF-1.7\nTest') });
  await page.getByRole('button', { name: 'رفع / إعادة المحاولة' }).click();
  await expect(page.getByRole('status').filter({ hasText: 'تم رفع الملف وحفظه.' })).toBeVisible();
  expect(requests.some(r => r.url.endsWith('/storage/me/files') && r.key)).toBeTruthy();
  await page.screenshot({ path: `test-results/storage/teacher-${test.info().project.name}.png`, fullPage: true });
});

test('no folder state renders without upload actions', async ({ page }) => {
  await mockSession(page, 'Instructor', 'FolderNotAssigned');
  await page.goto('/instructor/my-files');
  await expect(page.getByRole('heading', { name: 'لا يوجد مجلد ممنوح لك' })).toBeVisible();
  await expect(page.locator('input[type=file]')).toHaveCount(0);
});

test('Drive unavailable state renders without false connection or upload actions', async ({ page }) => {
  await mockSession(page, 'SchoolManager', 'Unavailable');
  await page.goto('/school-manager/storage');
  await expect(page.getByRole('heading', { name: 'الاتصال غير متاح' })).toBeVisible();
  await expect(page.locator('input[type=file]')).toHaveCount(0);
  await expect(page.locator('.file-name')).toHaveCount(0);
});

test('protected Office files offer download fallback and no direct mutations', async ({ page }) => {
  await mockSession(page);
  await page.route('**/api/v1/storage/files/31', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ isSuccess: true,
    data: { file: { ...file, displayName: 'سجل معتمد.docx', mimeType: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document', isProtected: true }, versions: [] } }) }));
  await page.goto('/school-manager/storage'); await page.locator('.file-name button').click();
  await expect(page.getByRole('button', { name: 'معاينة', exact: true })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'تنزيل', exact: true })).toBeVisible();
  await expect(page.locator('.details .notice')).toBeVisible();
  await expect(page.locator('.delete-confirm')).toHaveCount(0);
});

test('retries preserve upload key and revocation clears previously loaded file data', async ({ page }) => {
  const requests = await mockSession(page, 'SchoolManager');
  let attempts = 0; const keys: string[] = [];
  await page.route('**/api/v1/storage/files', async route => {
    if (route.request().method() !== 'POST') return route.fallback();
    keys.push(route.request().headers()['idempotency-key']); attempts++;
    await route.fulfill({ status: attempts === 1 ? 503 : 202, contentType: 'application/json', body: JSON.stringify(attempts === 1
      ? { isSuccess: false, message: 'انقطع الاتصال أثناء الحفظ' }
      : { isSuccess: true, data: { operationId: 2, status: 'NeedsAttention', displayName: 'شاهد.pdf', size: 8, mimeType: 'application/pdf' } }) });
  });
  await page.goto('/school-manager/storage'); await expect(page.locator('.file-name button')).toBeVisible();
  await page.locator('input[type=file]').setInputFiles({ name: 'شاهد.pdf', mimeType: 'application/pdf', buffer: Buffer.from('%PDF-1.7') });
  await page.getByRole('button', { name: 'رفع / إعادة المحاولة' }).click();
  await expect(page.locator('.storage-page [role=alert]')).toHaveText('انقطع الاتصال أثناء الحفظ');
  await page.getByRole('button', { name: 'رفع / إعادة المحاولة' }).click();
  await expect(page.getByRole('button', { name: 'التحقق من عملية الرفع' })).toBeVisible(); expect(keys[1]).toBe(keys[0]);
  await page.route('**/api/v1/storage/files?**', route => route.fulfill({ status: 403, contentType: 'application/json', body: JSON.stringify({ isSuccess: false, message: 'تم سحب التفويض' }) }));
  await page.getByRole('button', { name: 'بحث', exact: true }).click();
  await expect(page.locator('.storage-page [role=alert]')).toHaveText('تم سحب التفويض'); await expect(page.locator('.file-name')).toHaveCount(0);
});
