import { test, expect, Page } from '@playwright/test';

const folder = (itemId: string, name: string, canSelect = true) => ({ itemId, name, isFolder: true, size: null, mimeType: 'application/vnd.google-apps.folder', canSelect });
const file = { itemId: 'document', name: 'تقرير داخل الحساب.pdf', isFolder: false, size: 200, mimeType: 'application/pdf', canSelect: false };
const root = { currentFolderId: 'account-root', currentFolderName: 'ملفاتي', isAccountRoot: true, canSelectCurrent: false,
  breadcrumbs: [{ itemId: 'account-root', name: 'ملفاتي' }], items: [folder('school-folder', 'ملفات المدرسة التجريبية'), folder('read-only', 'مجلد للقراءة فقط', false), file], nextPageToken: 'next-1' };

async function setup(page: Page, connected = true) {
  const user = { userId: 'drive-manager', username: 'ui-test', fullName: 'مدير الاختبار', activeSchoolId: 18,
    activeSchoolName: 'Al-Falah E2E Test School', preferredLanguage: 'ar', roles: ['SchoolManager'], permissions: ['Settings.Manage'] };
  await page.addInitScript(user => {
    const payload = btoa(JSON.stringify({ sub: user.userId, exp: 2000000000 }));
    sessionStorage.setItem('alfalah_access_token', `e30.${payload}.mock`); sessionStorage.setItem('alfalah_user', JSON.stringify(user));
  }, user);
  let settings = { schoolId: 18, isConfigured: true, isEnabled: false, credentialType: 'OAuthRefreshToken', schoolGoogleEmail: 'test@example.invalid',
    oAuthClientId: 'fixture.apps.googleusercontent.com', rootFolderId: '', rootFolderDisplayName: '', hasStoredCredential: connected, hasStoredOAuthClientSecret: true };
  const writes: Record<string, unknown>[] = []; const browses: URL[] = [];
  await page.route('**/api/**', async route => {
    const request = route.request(); const url = new URL(request.url()); let data: unknown = {};
    if (url.pathname.endsWith('/auth/me')) data = user;
    else if (url.pathname.endsWith('/auth/schools')) data = [];
    else if (url.pathname.endsWith('/school-google-drive/folders')) {
      browses.push(url);
      const parent = url.searchParams.get('parentItemId');
      if (url.searchParams.get('pageToken')) data = { ...root, items: [folder('next-folder', 'مجلد الصفحة التالية')], nextPageToken: null };
      else if (url.searchParams.get('search')) data = { ...root, items: [folder('found-folder', 'نتيجة البحث')], nextPageToken: null };
      else if (parent === 'school-folder') data = { currentFolderId: parent, currentFolderName: 'ملفات المدرسة التجريبية', isAccountRoot: false, canSelectCurrent: true,
        breadcrumbs: [...root.breadcrumbs, { itemId: parent, name: 'ملفات المدرسة التجريبية' }], items: [file], nextPageToken: null };
      else data = root;
    } else if (url.pathname.endsWith('/school-google-drive')) {
      if (request.method() === 'PUT') {
        const body = request.postDataJSON(); writes.push(body);
        settings = { ...settings, ...body, hasStoredCredential: body.oAuthClientSecret ? false : settings.hasStoredCredential, hasStoredOAuthClientSecret: true };
      }
      data = settings;
    }
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ isSuccess: true, data }) });
  });
  await page.goto('/school-manager/evidence-settings');
  await expect(page.getByRole('heading', { name: 'إعدادات ملفات الإنجاز' })).toBeVisible();
  return { writes, browses };
}

test('OAuth JSON imports and saves a disabled connection without a manual folder ID', async ({ page }) => {
  const { writes } = await setup(page, false);
  await page.getByLabel('ملف OAuth JSON').setInputFiles({ name: 'oauth-web.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify({ web: {
    client_id: 'new-fixture.apps.googleusercontent.com', client_secret: 'synthetic-secret-for-ui-test', redirect_uris: ['http://localhost:5264/api/v1/school-google-drive/callback'] } })) });
  await page.getByRole('button', { name: 'حفظ بيانات الاتصال', exact: true }).click();
  await expect(page.locator('p.status')).toContainText('بيانات الاتصال محفوظة');
  expect(writes).toHaveLength(1); expect(writes[0]['isEnabled']).toBe(false); expect(writes[0]['rootFolderId']).toBe('');
  await expect(page.locator('input[formControlName=oAuthClientSecret]')).toHaveValue('');
  await expect(page.getByRole('button', { name: 'ربط حساب Google', exact: true })).toBeEnabled();
  await expect(page.getByRole('button', { name: 'اختيار مجلد من الحساب' })).toBeDisabled();
});

test('RTL account browser opens folders, shows files, pages and selects the current folder', async ({ page }) => {
  const { writes, browses } = await setup(page);
  await page.getByRole('button', { name: 'اختيار مجلد من الحساب' }).click();
  const dialog = page.getByRole('dialog'); await expect(dialog.locator('.drive-picker')).toHaveAttribute('dir', 'rtl');
  await expect(dialog.getByText('تقرير داخل الحساب.pdf')).toBeVisible();
  await expect(dialog.getByRole('button', { name: 'اختيار هذا المجلد' })).toBeDisabled();
  await expect(dialog.getByRole('button', { name: 'اختيار مجلد للقراءة فقط', exact: true })).toBeDisabled();
  await dialog.getByRole('button', { name: 'عرض المزيد' }).click();
  await expect(dialog.getByRole('button', { name: 'مجلد الصفحة التالية', exact: true })).toBeVisible();
  expect(browses.at(-1)?.searchParams.get('pageToken')).toBe('next-1');
  await dialog.getByRole('button', { name: 'ملفات المدرسة التجريبية', exact: true }).click();
  await expect(dialog.getByRole('button', { name: 'اختيار هذا المجلد' })).toBeEnabled();
  await page.screenshot({ path: `test-results/storage/drive-picker-${test.info().project.name}.png`, fullPage: true });
  await dialog.getByRole('button', { name: 'اختيار هذا المجلد' }).click();
  await expect(dialog).not.toBeVisible(); await expect(page.locator('.selected-folder')).toContainText('ملفات المدرسة التجريبية');
  expect(writes).toHaveLength(0);
  await page.getByRole('button', { name: 'حفظ إعدادات المجلد' }).click();
  await expect(page.locator('p.status')).toContainText('ملفات المدرسة مفعّلة');
  expect(writes[0]['rootFolderId']).toBe('school-folder'); expect(writes[0]['rootFolderDisplayName']).toBe('ملفات المدرسة التجريبية'); expect(writes[0]['isEnabled']).toBe(true);
});

test('search stays within the current folder and breadcrumbs return to the account root', async ({ page }) => {
  const { browses } = await setup(page); await page.getByRole('button', { name: 'اختيار مجلد من الحساب' }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByRole('button', { name: 'ملفات المدرسة التجريبية', exact: true }).click();
  await dialog.getByLabel('بحث داخل المجلد').fill('نتيجة'); await dialog.getByRole('button', { name: 'بحث', exact: true }).click();
  await expect(dialog.getByRole('button', { name: 'نتيجة البحث', exact: true })).toBeVisible();
  expect(browses.at(-1)?.searchParams.get('parentItemId')).toBe('school-folder'); expect(browses.at(-1)?.searchParams.get('search')).toBe('نتيجة');
  await dialog.locator('.picker-breadcrumb').getByRole('button', { name: 'ملفاتي' }).click();
  await expect(dialog.getByRole('button', { name: 'اختيار هذا المجلد' })).toBeDisabled();
});

test('revoked authorization clears previously visible account contents and supports safe retry', async ({ page }) => {
  await setup(page); await page.getByRole('button', { name: 'اختيار مجلد من الحساب' }).click(); const dialog = page.getByRole('dialog');
  await expect(dialog.getByText('تقرير داخل الحساب.pdf')).toBeVisible();
  await page.route('**/school-google-drive/folders?**', route => route.fulfill({ status: 403, contentType: 'application/json', body: JSON.stringify({ isSuccess: false, message: 'تم سحب صلاحية المدير' }) }));
  await dialog.getByRole('button', { name: 'ملفات المدرسة التجريبية', exact: true }).click();
  await expect(dialog.getByRole('alert')).toContainText('تم سحب صلاحية المدير'); await expect(dialog.locator('.picker-items li')).toHaveCount(0);
});

test('a rejected folder save surfaces backend validation and does not report activation', async ({ page }) => {
  await setup(page); await page.getByRole('button', { name: 'اختيار مجلد من الحساب' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'اختيار ملفات المدرسة التجريبية', exact: true }).click();
  await page.route('**/api/v1/school-google-drive', async route => {
    if (route.request().method() !== 'PUT') return route.fallback();
    return route.fulfill({ status: 400, contentType: 'application/json', body: JSON.stringify({ isSuccess: false, message: 'المجلد يتداخل مع مجلد مدرسة أخرى.' }) });
  });
  await page.getByRole('button', { name: 'حفظ إعدادات المجلد' }).click();
  await expect(page.locator('section.page > [role=alert]')).toHaveText('المجلد يتداخل مع مجلد مدرسة أخرى.');
  await expect(page.locator('p.status')).not.toContainText('ملفات المدرسة مفعّلة');
});

test('invalid client JSON is rejected and changing credentials closes the picker until saved', async ({ page }) => {
  await setup(page);
  await page.getByLabel('ملف OAuth JSON').setInputFiles({ name: 'installed.json', mimeType: 'application/json', buffer: Buffer.from('{"installed":{"client_id":"fixture"}}') });
  await expect(page.getByRole('alert')).toContainText('Web application');
  await page.locator('input[formControlName=oAuthClientSecret]').fill('replacement-fixture-secret');
  await expect(page.getByRole('button', { name: 'اختيار مجلد من الحساب' })).toBeDisabled();
  await expect(page.getByRole('button', { name: 'إعادة ربط حساب Google' })).toBeDisabled();
});
