import { test, expect, Page } from '@playwright/test';

const file = { storedFileId: 31, folderId: 7, displayName: 'خطة المدرسة.pdf', size: 1000,
  mimeType: 'application/pdf', uploadedAt: '2026-10-03T10:00:00Z', state: 'Managed', isProtected: false, rowVersion: 'AAAAAAAAAAE=' };
async function mockSession(page: Page, role = 'SchoolManager', state = 'Connected', canManage = true, archiveAllowed = true, canReviewEvidence = role !== 'Instructor' && canManage) {
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
      academicYearName: 'السنة الدراسية', canManage, canReviewEvidence, isTeacher: role === 'Instructor', connectionState: state, rootFolderId: 7 };
    else if (path.endsWith('/visits/operations-status')) return route.fulfill({ status: archiveAllowed ? 200 : 403,
      contentType: 'application/json', body: JSON.stringify(archiveAllowed ? {isSuccess:true,data:{workerEnabled:false,externalWritesEnabled:false,ready:false}} : {isSuccess:false,message:'غير مخوّل'}) });
    else if (path.endsWith('/storage/folders')) data = { items: [], total: 0, page: 1, pageSize: 25 };
    else if (/\/storage\/folders\/\d+\/path$/.test(path)) {
      const id = Number(path.split('/').at(-2));
      data = id === 7 ? [{id:7,displayName:'مكتبة المدرسة',kind:'SchoolLibrary',rowVersion:''}]
        : [{id:7,displayName:'مكتبة المدرسة',kind:'SchoolLibrary',rowVersion:''},{id,parentFolderId:7,displayName:id === 33 ? 'المجلد الأخير' : 'كوكو',kind:'SchoolLibrary',rowVersion:''}];
    }
    else if (path.endsWith('/storage/academic-years')) data = [{id:1,nameAr:'السنة الدراسية'}];
    else if (path.endsWith('/storage/evidence-teachers') || path.endsWith('/storage/requirement-catalog') || path.endsWith('/links') || path.endsWith('/change-requests')) data = [];
    else if (path.endsWith('/storage/review-queue') || path.endsWith('/storage/change-queue')) data = {items:[],total:0,page:1,pageSize:25};
    else if (path.endsWith('/storage/evidence-counts')) data = {files:1,links:0,approvedLinks:0,fulfilledRequirements:0,requirements:11};
    else if (path.endsWith('/storage/delegations')) data = [];
    else if (path.endsWith('/storage/files/31/content')) return route.fulfill({ status: 200, contentType: 'application/pdf', body: '%PDF-1.7\n1 0 obj<</Type /Catalog>>endobj\n%%EOF' });
    else if (path.endsWith('/storage/files/31')) data = { file: { ...file, folderId: Number(new URL(page.url()).searchParams.get('folder')) || 7 }, versions: [{ versionId: 1, versionNumber: 1, size: 1000, mimeType: 'application/pdf', uploadedAt: file.uploadedAt, availability: 'Available' }] };
    else if (path.endsWith('/storage/files') || path.endsWith('/storage/me/files')) {
      if (req.method() === 'POST') data = { operationId: 1, storedFileId: 31, versionId: 1, status: 'Completed', displayName: file.displayName, size: 1000, mimeType: file.mimeType, uploadedAt: file.uploadedAt };
      else data = { items: [{ ...file, folderId: Number(url.searchParams.get('folderId')) || 7 }], total: 5000, page: Number(url.searchParams.get('page') || 1), pageSize: 25 };
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
  await expect(page.getByRole('dialog', { name:'معاينة' })).toBeHidden();
  await expect(page.getByRole('dialog', { name:'تفاصيل الملف' })).toBeHidden();
  await page.locator('.p-paginator-next').first().click();
  await expect.poll(() => requests.some(r => r.url.includes('/storage/files?') && r.url.includes('page=2'))).toBeTruthy();
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1);
  expect(overflow).toBeFalsy();
  await page.screenshot({ path: `test-results/storage/library-${test.info().project.name}.png`, fullPage: true });
});

test('library entry requests only the visible folder and file pages', async ({ page }) => {
  const requests = await mockSession(page);
  await page.goto('/school-manager/storage');
  await expect(page.locator('.file-name button')).toBeVisible();
  const paths = requests.map(request => new URL(request.url).pathname);
  expect(paths.filter(path => path.endsWith('/storage/folders'))).toHaveLength(1);
  expect(paths.filter(path => path.endsWith('/storage/files'))).toHaveLength(1);
  expect(paths.some(path => /\/storage\/folders\/\d+\/path$/.test(path))).toBeFalsy();
});

test('evidence entry does not request unrelated library folders or files', async ({ page }) => {
  const requests = await mockSession(page);
  await page.goto('/school-manager/storage/evidence');
  await expect(page.locator('.review-panel')).toBeVisible();
  const paths = requests.map(request => new URL(request.url).pathname);
  expect(paths.some(path => path.endsWith('/storage/folders') || path.endsWith('/storage/files'))).toBeFalsy();
});

test('workspace overview uses live counts and links to separate areas', async ({ page }) => {
  await mockSession(page);
  await page.goto('/school-manager/storage/overview');
  await expect(page.getByRole('heading', { name:/صباح الخير/ })).toBeVisible();
  await expect(page.getByText('0 من 11')).toBeVisible();
  await expect(page.getByRole('link', { name:/مكتبة المدرسة/ }).first()).toHaveAttribute('href', '/school-manager/storage');
  await page.screenshot({path:`test-results/storage/overview-${test.info().project.name}.png`,fullPage:true,animations:'disabled'});
});

test('manager administration uses live status and routes to teacher folders and import without mock counts', async ({ page }) => {
  const requests = await mockSession(page);
  await page.goto('/school-manager/storage/admin');
  await expect(page.getByRole('heading', { name: 'إعدادات المساحة' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Google Drive' })).toBeVisible();
  await expect(page.getByText('الأرشفة تنتظر بوابة التشغيل.')).toBeVisible();
  await page.screenshot({path:`test-results/storage/admin-${test.info().project.name}.png`,fullPage:true,animations:'disabled'});
  await page.getByRole('link', { name: 'مجلدات المعلمين' }).click();
  await expect(page.getByRole('link', { name: 'فتح قائمة المعلمين' })).toHaveAttribute('href', '/teachers');
  await page.getByRole('link', { name: 'التفويض', exact: true }).click();
  await expect(page.getByText('لا توجد تفويضات مسجلة.')).toBeVisible();
  expect(requests.some(request => new URL(request.url).pathname.endsWith('/storage/delegations'))).toBeTruthy();
  await page.getByRole('link', { name: 'الاستيراد التاريخي' }).click();
  await expect(page).toHaveURL(/tab=imports/);
});

test('administration direct URL rejects a user without management permission', async ({ page }) => {
  await mockSession(page, 'Secretary', 'Connected', false);
  await page.goto('/school-manager/storage/admin?tab=delegations');
  await expect(page).toHaveURL(/\/unauthorized/);
});

test('delegate can view administration but cannot see manager-only delegation actions', async ({ page }) => {
  await mockSession(page, 'Secretary');
  await page.route('**/api/v1/storage/delegations', route => route.fulfill({status:403,contentType:'application/json',body:JSON.stringify({isSuccess:false,message:'Forbidden'})}));
  await page.goto('/school-manager/storage/admin?tab=delegations');
  await expect(page.getByRole('alert')).toContainText('مدير المدرسة');
  await expect(page.getByRole('heading', {name:'منح تفويض'})).toHaveCount(0);
  await expect(page.getByRole('button', {name:'سحب التفويض'})).toHaveCount(0);
});

test('library manager without review permission cannot open either evidence route', async ({ page }) => {
  await mockSession(page, 'SchoolManager', 'Connected', true, true, false);
  await page.goto('/school-manager/storage/overview');
  await expect(page.locator('.shell-sidebar__category').filter({hasText:'مساحة الملفات'}).getByRole('link', {name:'الشواهد'})).toHaveCount(0);
  await expect(page.getByRole('link', {name:/مراجعة الشواهد/})).toHaveCount(0);
  await page.goto('/school-manager/storage/evidence');
  await expect(page).toHaveURL(/\/unauthorized/);
  await page.goto('/school-manager/storage?view=review');
  await expect(page).toHaveURL(/\/unauthorized/);
  await page.goto('/school-manager/storage?file=31&requirement=42&academicYearId=1');
  await expect(page.getByRole('dialog',{name:'تفاصيل الملف'})).toContainText(file.displayName);
  await expect(page.locator('.review-panel')).toHaveCount(0);
  await page.keyboard.press('Escape');
  await expect(page).not.toHaveURL(/requirement=42/);
  await expect(page.locator('.review-panel')).toHaveCount(0);
});

test('library filter is kept in the URL and applied by the server', async ({ page }) => {
  const requests = await mockSession(page);
  await page.goto('/school-manager/storage');
  await page.getByRole('group', { name: 'تصفية الملفات' }).getByRole('button', { name: 'PDF' }).click();
  await expect(page).toHaveURL(/filter=pdf/);
  await expect.poll(() => requests.some(request => request.url.includes('/storage/files?') && request.url.includes('filter=pdf'))).toBeTruthy();
  await page.reload();
  await expect(page.getByRole('group', { name: 'تصفية الملفات' }).getByRole('button', { name: 'PDF' })).toHaveAttribute('aria-pressed', 'true');
});

test('search scope switch updates the server query and browser history', async ({ page }) => {
  const requests = await mockSession(page);
  await page.goto('/school-manager/storage');
  await page.locator('.search-scope').click();
  await expect(page).toHaveURL(/global=true/);
  await expect.poll(() => requests.some(request => request.url.includes('/storage/files?') && request.url.includes('global=true'))).toBeTruthy();
  await page.goBack();
  await expect(page.locator('.search-scope input')).not.toBeChecked();
});

test('historical-year file URL never requests or displays a current-year file', async ({ page }) => {
  const requests = await mockSession(page);
  await page.goto('/school-manager/storage?academicYearId=2&file=31');
  await expect(page.getByRole('heading', { name: 'هذه السنة للقراءة فقط' })).toBeVisible();
  expect(requests.some(request => /\/storage\/files\/31(?:\?|$)/.test(request.url))).toBeFalsy();
  await expect(page.getByRole('dialog', { name: 'تفاصيل الملف' })).toHaveCount(0);
});

test('double clicking one folder keeps a single breadcrumb for that folder', async ({ page }) => {
  await mockSession(page);
  await page.route('**/api/v1/storage/folders?**', async route => {
    const parent = new URL(route.request().url()).searchParams.get('parentFolderId');
    if (parent === '8') await new Promise(resolve => setTimeout(resolve, 300));
    const items = parent === '7' ? [{ id: 8, parentFolderId: 7, displayName: 'كوكو', kind: 'SchoolLibrary', rowVersion: 'AAAAAAAAAAE=' }] : [];
    await route.fulfill({ contentType: 'application/json', body: JSON.stringify({ isSuccess: true, data: { items, total: items.length, page: 1, pageSize: 25 } }) });
  });
  await page.goto('/school-manager/storage');
  await page.locator('.folder-tile').getByText('كوكو').dblclick();
  await expect(page.locator('.breadcrumbs button:not(.folder-back)')).toHaveCount(2);
  await expect(page.locator('.breadcrumbs button').last()).toHaveText('كوكو');
  await page.goBack();
  await expect(page.locator('.breadcrumbs button:not(.folder-back)')).toHaveCount(1);
});

test('folder deep link restores its authorized ancestor path and workspace navigation', async ({ page }) => {
  const requests = await mockSession(page);
  await page.goto('/school-manager/storage?folder=8&page=2&academicYearId=1');
  await expect(page.locator('.breadcrumbs button:not(.folder-back)')).toHaveCount(2);
  await expect(page.locator('.breadcrumbs button').last()).toHaveText('كوكو');
  expect(requests.some(r => r.url.includes('/storage/folders/8/path?own=false'))).toBeTruthy();
  expect(requests.some(r => r.url.includes('/storage/files?') && r.url.includes('folderId=8') && r.url.includes('page=2'))).toBeTruthy();
  const sidebar = page.locator('.shell-sidebar__category').filter({ hasText: 'مساحة الملفات' });
  await expect(sidebar).toHaveCount(1);
  await expect(sidebar.locator('a').filter({ hasText: 'الزيارات' })).toHaveCount(1);
  await expect(sidebar.locator('a').filter({ hasText: 'الإدارة' })).toHaveCount(1);
  if (test.info().project.name === 'mobile') {
    const mobile = page.getByRole('navigation', { name: 'أقسام مساحة الملفات' });
    await mobile.getByRole('button', { name: 'المزيد' }).click();
    await expect(mobile.getByRole('link', { name: 'الزيارات' })).toBeVisible();
    await expect(mobile.getByRole('link', { name: 'الإدارة' })).toBeVisible();
    await mobile.getByRole('link', { name: 'الشواهد' }).click();
  } else await sidebar.getByRole('link', { name: 'الشواهد' }).click();
  await expect(page).toHaveURL(/\/storage\/evidence/);
  await expect(page.getByRole('heading', { name: 'بانتظار قراري' }).first()).toBeVisible();
});

test('copied file link reopens the same folder, year, search and page @mobile', async ({ page }) => {
  await mockSession(page);
  await page.addInitScript(() => Object.defineProperty(navigator, 'clipboard', { configurable: true,
    value: { writeText: (value: string) => { (window as any).__copiedStorageLink = value; return Promise.resolve(); } } }));
  await page.goto('/school-manager/storage?folder=8&page=2&academicYearId=1&search=%D8%AE%D8%B7%D8%A9');
  await expect(page.locator('.file-name button')).toBeVisible();
  await page.getByRole('button', {name:'نسخ رابط داخلي'}).click();
  const copied = await page.evaluate(() => (window as any).__copiedStorageLink as string);
  const link = new URL(copied);
  expect(link.searchParams.get('file')).toBe('31');
  expect(link.searchParams.get('folder')).toBe('8');
  expect(link.searchParams.get('page')).toBe('2');
  expect(link.searchParams.get('academicYearId')).toBe('1');
  expect(link.searchParams.get('search')).toBe('خطة');
  await page.goto(copied);
  await expect(page.locator('.breadcrumbs button').last()).toHaveText('كوكو');
  await expect(page.getByRole('dialog',{name:'تفاصيل الملف'})).toContainText(file.displayName);
});

test('folder navigation, file details and browser history preserve the server query', async ({ page }) => {
  const requests = await mockSession(page);
  await page.route('**/api/v1/storage/folders?**', async route => {
    const parent = new URL(route.request().url()).searchParams.get('parentFolderId');
    const items = parent === '7' ? [{ id: 8, parentFolderId: 7, displayName: 'كوكو', kind: 'SchoolLibrary', rowVersion: 'AAAAAAAAAAE=' }] : [];
    await route.fulfill({ contentType: 'application/json', body: JSON.stringify({ isSuccess: true, data: { items, total: items.length, page: 1, pageSize: 25 } }) });
  });
  await page.addInitScript(() => Object.defineProperty(navigator, 'clipboard', { configurable: true,
    value: { writeText: (value: string) => { (window as any).__copiedStorageLink = value; return Promise.resolve(); } } }));
  await page.goto('/school-manager/storage?academicYearId=1&search=%D8%AE%D8%B7%D8%A9&sort=date&page=2');
  await expect(page.getByRole('button', { name: 'كوكو', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'كوكو', exact: true })).toHaveCount(1);
  await page.getByRole('button', { name: 'كوكو', exact: true }).click();
  await expect(page).toHaveURL(/folder=8/);
  await expect(page).toHaveURL(/academicYearId=1/);
  await expect(page).toHaveURL(/search=/);
  await expect(page).toHaveURL(/sort=date/);
  await expect(page).toHaveURL(/page=1/);
  await page.locator('.file-name button').click();
  await expect(page).toHaveURL(/file=31/);
  await expect(page.getByRole('dialog', {name:'تفاصيل الملف'})).toContainText(file.displayName);
  await page.getByRole('dialog', {name:'تفاصيل الملف'}).getByRole('button', {name:'نسخ رابط داخلي'}).click();
  const copied = new URL(await page.evaluate(() => (window as any).__copiedStorageLink as string));
  expect(copied.searchParams.get('folder')).toBe('8');
  expect(copied.searchParams.get('file')).toBe('31');
  expect(copied.searchParams.get('academicYearId')).toBe('1');
  expect(copied.searchParams.get('search')).toBe('خطة');
  await page.goBack();
  await expect(page.getByRole('dialog', {name:'تفاصيل الملف'})).toHaveCount(0);
  await expect(page.locator('.breadcrumbs button').last()).toHaveText('كوكو');
  await page.goBack();
  await expect(page.locator('.breadcrumbs button')).toHaveCount(1);
  await expect(page).toHaveURL(/page=2/);
  expect(requests.some(r => r.url.includes('/storage/files?') && r.url.includes('folderId=7') && r.url.includes('page=2') && r.url.includes('sort=date'))).toBeTruthy();
  expect(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1)).toBeFalsy();
});

test('folder list loads the next server page without duplicating folders', async ({ page }) => {
  await mockSession(page);
  const folderPages: number[] = [];
  await page.route('**/api/v1/storage/folders?**', async route => {
    const url = new URL(route.request().url());
    const parent = url.searchParams.get('parentFolderId');
    const pageNumber = Number(url.searchParams.get('page'));
    if (parent === '7') folderPages.push(pageNumber);
    const items = parent === '7' ? (pageNumber === 1
      ? Array.from({ length: 25 }, (_, index) => ({ id: index + 8, parentFolderId: 7, displayName: `مجلد ${index + 1}`, kind: 'SchoolLibrary', rowVersion: '' }))
      : [{ id: 33, parentFolderId: 7, displayName: 'المجلد الأخير', kind: 'SchoolLibrary', rowVersion: '' }]) : [];
    await route.fulfill({ contentType: 'application/json', body: JSON.stringify({ isSuccess: true, data: { items, total: parent === '7' ? 26 : 0, page: pageNumber, pageSize: 25 } }) });
  });
  await page.goto('/school-manager/storage');
  await expect(page.locator('.folder-tile')).toHaveCount(25);
  await page.getByRole('button', {name:'عرض المزيد من المجلدات'}).click();
  await expect(page.locator('.folder-tile')).toHaveCount(26);
  await expect(page.getByRole('button', {name:'عرض المزيد من المجلدات'})).toHaveCount(0);
  await page.getByRole('button', {name:'المجلد الأخير', exact:true}).click();
  await expect(page).toHaveURL(/folder=33/);
  await expect(page.locator('.breadcrumbs button').last()).toHaveText('المجلد الأخير');
  expect(folderPages).toContain(2);
});

test('delegated reader sees shared navigation without administration or archive links, and archive route is denied @mobile', async ({ page }) => {
  await mockSession(page, 'Secretary', 'Connected', false, false);
  await page.goto('/school-manager/storage');
  const nav = page.locator('.shell-sidebar__category').filter({ hasText: 'مساحة الملفات' });
  await expect(nav).toHaveCount(1);
  await expect(nav.getByRole('link', {name:'الإدارة'})).toHaveCount(0);
  await expect(nav.getByRole('link', {name:'الزيارات'})).toHaveCount(0);
  await page.goto('/school-manager/storage/visits');
  await expect(page).toHaveURL(/\/unauthorized/);
  await page.goto('/visits?visitId=4');
  await expect(page).toHaveURL(/\/unauthorized/);
});

test('teacher keeps existing visit workspace access without archive management permission', async ({ page }) => {
  await mockSession(page, 'Instructor', 'Connected', false, false);
  await page.goto('/visits');
  await expect(page).toHaveURL(/\/visits$/);
});

test('teacher only uses own file endpoints and cancels queued upload before sending', async ({ page }) => {
  const requests = await mockSession(page, 'Instructor');
  await page.goto('/instructor/my-files'); await expect(page.locator('h1')).toHaveText('ملفاتي');
  await expect(page.locator('.shell-sidebar__category').filter({ hasText: 'مساحة الملفات' }).locator('a').filter({ hasText: 'ملفاتي' })).toHaveCount(1);
  await expect(page.locator('.shell-sidebar__category').filter({ hasText: 'مساحة الملفات' }).locator('a').filter({ hasText: 'الإدارة' })).toHaveCount(0);
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

test('teacher selects a real requirement, uploads to own files, and gets a draft link', async ({ page }) => {
  const requests = await mockSession(page, 'Instructor');
  let linkedBody = '';
  await page.route('**/api/v1/storage/requirement-catalog?**', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ isSuccess:true, data:[{id:42,academicYearId:1,code:'1.1.1',displayName:'خطة المدرسة',importance:'High',fulfillmentPolicy:'Evidence',minimumApprovedLinks:1,rowVersion:'AAAAAAAAAAE='}] }) }));
  await page.route('**/api/v1/storage/files/31/links', route => { linkedBody = route.request().postData() || ''; return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ isSuccess:true, data:{id:55,storedFileId:31,requirementId:42,academicYearId:1,versionId:1,fileName:'شاهد.pdf',requirementName:'خطة المدرسة',teacherName:'مستخدم الاختبار',status:'Draft',availability:'Available',rowVersion:'AAAAAAAAAAE=',decisions:[]} }) }); });
  await page.goto('/instructor/my-files');
  const requirement = page.getByRole('group', { name:'المتطلبات' }).getByRole('button', { name:/خطة المدرسة/ });
  await expect(requirement).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1)).toBeFalsy();
  await page.screenshot({ path:`test-results/storage/teacher-flow-${test.info().project.name}.png`, fullPage:true, animations:'disabled' });
  await requirement.click();
  await expect(page).toHaveURL(/requirement=42/);
  await page.locator('input[type=file]').setInputFiles({ name:'شاهد.pdf', mimeType:'application/pdf', buffer:Buffer.from('%PDF-1.7\nTest') });
  await page.getByRole('button', { name:'رفع / إعادة المحاولة' }).click();
  await expect(page.getByRole('button', { name:'فتح الرابط وإرساله للمراجعة' })).toBeVisible();
  expect(requests.some(request => request.url.endsWith('/storage/me/files') && request.key)).toBeTruthy();
  expect(linkedBody).toContain('"requirementId":42');
  expect(requests.some(request => request.url.includes('/submit'))).toBeFalsy();
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
  await page.route('**/api/v1/storage/files/31?**', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ isSuccess: true,
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
  await expect(page.getByRole('dialog', { name: 'تفاصيل الملف' })).toHaveCount(0);
});
