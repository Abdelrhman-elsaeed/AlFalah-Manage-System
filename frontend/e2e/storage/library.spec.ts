import { test, expect, Page } from '@playwright/test';

function samplePdf(): Buffer {
  const text = 'BT /F1 24 Tf 100 700 Td (PDF preview works) Tj ET';
  const secondText = 'BT /F1 24 Tf 100 700 Td (Page two works) Tj ET';
  const objects = [
    '<< /Type /Catalog /Pages 2 0 R >>',
    '<< /Type /Pages /Kids [3 0 R 6 0 R] /Count 2 >>',
    '<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>',
    `<< /Length ${Buffer.byteLength(text)} >>\nstream\n${text}\nendstream`,
    '<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>',
    '<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 7 0 R /Resources << /Font << /F1 5 0 R >> >> >>',
    `<< /Length ${Buffer.byteLength(secondText)} >>\nstream\n${secondText}\nendstream`
  ];
  let pdf = '%PDF-1.4\n';
  const offsets = [0];
  for (let i = 0; i < objects.length; i++) {
    offsets.push(Buffer.byteLength(pdf));
    pdf += `${i + 1} 0 obj\n${objects[i]}\nendobj\n`;
  }
  const xref = Buffer.byteLength(pdf);
  pdf += `xref\n0 ${objects.length + 1}\n0000000000 65535 f \n`;
  for (const offset of offsets.slice(1)) pdf += `${String(offset).padStart(10, '0')} 00000 n \n`;
  pdf += `trailer\n<< /Size ${objects.length + 1} /Root 1 0 R >>\nstartxref\n${xref}\n%%EOF\n`;
  return Buffer.from(pdf);
}

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
    else if (path.endsWith('/storage/access')) data = { canManage, canReviewEvidence };
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
    else if (path.endsWith('/storage/templates')) data = [{version:1,name:'القالب الحالي'}];
    else if (path.endsWith('/storage/evaluation-members') || path.endsWith('/storage/imports')) data = [];
    else if (path.endsWith('/storage/evidence-teachers') || path.endsWith('/storage/requirement-catalog') || path.endsWith('/links') || path.endsWith('/change-requests')) data = [];
    else if (path.endsWith('/storage/review-queue') || path.endsWith('/storage/change-queue')) data = {items:[],total:0,page:1,pageSize:25};
    else if (path.endsWith('/storage/evidence-counts')) data = {files:1,links:0,approvedLinks:0,fulfilledRequirements:0,requirements:11};
    else if (path.endsWith('/storage/delegations')) data = [];
    else if (path.endsWith('/storage/files/31/content')) return route.fulfill({ status: 200, contentType: 'application/pdf', body: samplePdf() });
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
  await expect(page.locator('.pdf-page-image')).toHaveAttribute('src', /^blob:/);
  await expect(page.locator('.pdf-page-image')).toBeVisible();
  await expect.poll(() => page.locator('.pdf-page-image').evaluate((image: HTMLImageElement) => image.complete && image.naturalWidth > 0)).toBe(true);
  await expect.poll(async () => {
    const image = await page.locator('.pdf-page-image').boundingBox();
    const media = await page.locator('.pdf-preview-media').boundingBox();
    return !!(image && media && image.height <= media.height - 20 && image.width <= media.width - 20);
  }).toBe(true);
  await page.getByRole('dialog', { name: 'معاينة' }).screenshot({ path: 'test-results/storage/pdf-preview-rendered.png', animations: 'disabled' });
  const firstPageUrl = await page.locator('.pdf-page-image').getAttribute('src');
  await page.getByRole('button', { name: 'التالي' }).click();
  await expect(page.getByText('صفحة 2 من 2')).toBeVisible();
  await expect.poll(() => page.locator('.pdf-page-image').getAttribute('src')).not.toBe(firstPageUrl);
  const previewDialog = page.getByRole('dialog', { name: 'معاينة' });
  const normalBounds = await previewDialog.boundingBox();
  await previewDialog.getByRole('button', { name: 'ملء شاشة المعاينة' }).click();
  await expect(previewDialog).toHaveClass(/storage-preview-expanded/);
  const expandedBounds = await previewDialog.boundingBox();
  const viewport = page.viewportSize()!;
  expect(expandedBounds && normalBounds && expandedBounds.width >= viewport.width - 2 && expandedBounds.height >= viewport.height - 2 && expandedBounds.height > normalBounds.height).toBeTruthy();
  await expect(previewDialog.locator('.pdf-page-image')).toBeVisible();
  const pageBounds = await previewDialog.locator('.pdf-page-image').boundingBox();
  const mediaBounds = await previewDialog.locator('.pdf-preview-media').boundingBox();
  expect(pageBounds && mediaBounds && pageBounds.height <= mediaBounds.height - 20 && pageBounds.width <= mediaBounds.width - 20).toBeTruthy();
  await expect(previewDialog.getByText('100%', { exact: true })).toBeVisible();
  await previewDialog.getByRole('button', { name: 'تكبير المعاينة' }).click();
  await expect(previewDialog.getByText('125%', { exact: true })).toBeVisible();
  const zoomedPageBounds = await previewDialog.locator('.pdf-page-image').boundingBox();
  expect(pageBounds && zoomedPageBounds && zoomedPageBounds.height > pageBounds.height).toBeTruthy();
  for (let step = 0; step < 3; step++) await previewDialog.getByRole('button', { name: 'تكبير المعاينة' }).click();
  await expect(previewDialog.getByText('200%', { exact: true })).toBeVisible();
  await expect.poll(() => previewDialog.locator('.pdf-preview-media').evaluate(element => element.scrollHeight > element.clientHeight)).toBe(true);
  await previewDialog.getByRole('button', { name: 'ملاءمة الصفحة' }).click();
  await expect(previewDialog.getByText('100%', { exact: true })).toBeVisible();
  await previewDialog.getByRole('button', { name: 'تصغير نافذة المعاينة' }).click();
  await expect(previewDialog).not.toHaveClass(/storage-preview-expanded/);
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

test('library search updates automatically while typing and clears without Enter', async ({ page }) => {
  const requests = await mockSession(page);
  await page.goto('/school-manager/storage');
  await expect(page.locator('.file-row')).toHaveCount(1);
  const search = page.locator('.search-group input[name="search"]');
  await search.fill('p');
  await search.fill('pl');
  await search.fill('plan');
  await expect.poll(() => requests.filter(request => new URL(request.url).pathname.endsWith('/storage/files'))
    .map(request => new URL(request.url).searchParams.get('search'))).toContain('plan');
  expect(requests.filter(request => new URL(request.url).pathname.endsWith('/storage/files'))
    .map(request => new URL(request.url).searchParams.get('search')).filter(Boolean)).toEqual(['plan']);
  await expect(page).toHaveURL(/search=plan/);
  await search.fill('');
  await expect.poll(() => requests.filter(request => new URL(request.url).pathname.endsWith('/storage/files'))
    .length).toBeGreaterThan(2);
  await expect(page).not.toHaveURL(/search=/);
});

test('file table stays stable while a filter request is pending', async ({ page }) => {
  await mockSession(page);
  await page.goto('/school-manager/storage');
  await expect(page.locator('.file-row')).toHaveCount(1);
  const before=await page.locator('.file-grid').boundingBox();
  let release!: () => void;
  const pending = new Promise<void>(resolve => { release = resolve; });
  await page.route('**/api/v1/storage/files?**', async route => {
    if (new URL(route.request().url()).searchParams.get('filter') === 'video') await pending;
    await route.fallback();
  });
  try {
    await page.getByRole('group', { name: 'تصفية الملفات' }).getByRole('button', { name: 'فيديو' }).click();
    await expect(page).toHaveURL(/filter=video/);
    await expect(page.locator('.file-row')).toHaveCount(1);
    await expect(page.locator('.file-grid-loading')).toHaveCount(0);
    const during=await page.locator('.file-grid').boundingBox();
    expect(before && during && Math.abs(before.y-during.y)<2 && Math.abs(before.height-during.height)<2).toBeTruthy();
  } finally { release(); }
  await expect(page.locator('.file-row')).toHaveCount(1);
});

test('image preview can expand to the viewport and return to its normal size', async ({ page }) => {
  await mockSession(page);
  const image = { ...file, displayName: 'صورة.png', mimeType: 'image/png' };
  const imageData = await page.evaluate(() => {
    const canvas = document.createElement('canvas'); canvas.width = 1200; canvas.height = 1600;
    const context = canvas.getContext('2d')!; context.fillStyle = '#177345'; context.fillRect(0, 0, 1200, 1600);
    return canvas.toDataURL('image/png').split(',')[1];
  });
  await page.route('**/api/v1/storage/files?**', route => route.fulfill({ status: 200, contentType: 'application/json',
    body: JSON.stringify({ isSuccess: true, data: { items: [image], total: 1, page: 1, pageSize: 25 } }) }));
  await page.route('**/api/v1/storage/files/31/content?**', route => route.fulfill({ status: 200, contentType: 'image/png',
    body: Buffer.from(imageData, 'base64') }));
  await page.goto('/school-manager/storage');
  await page.locator('.file-row').filter({ hasText: image.displayName }).getByRole('button', { name: `معاينة ${image.displayName}` }).click();
  const preview = page.getByRole('dialog', { name: 'معاينة' });
  await expect(preview.locator('img.image-preview')).toBeVisible();
  await preview.getByRole('button', { name: 'ملء شاشة المعاينة' }).click();
  await expect(preview).toHaveClass(/storage-preview-expanded/);
  await expect(preview.locator('.preview-stage')).toBeVisible();
  const stageBounds = await preview.locator('.preview-stage').boundingBox();
  expect(stageBounds && stageBounds.height > 600).toBeTruthy();
  const imageBounds = await preview.locator('img.image-preview').boundingBox();
  const mediaBounds = await preview.locator('.preview-media').boundingBox();
  expect(imageBounds && mediaBounds && imageBounds.height <= mediaBounds.height - 20 && imageBounds.width <= mediaBounds.width - 20).toBeTruthy();
  await preview.getByRole('button', { name: 'تكبير المعاينة' }).click();
  const zoomedImageBounds = await preview.locator('img.image-preview').boundingBox();
  expect(imageBounds && zoomedImageBounds && zoomedImageBounds.height > imageBounds.height).toBeTruthy();
  await preview.getByRole('button', { name: 'تصغير المعاينة' }).click();
  await expect(preview.getByText('100%', { exact: true })).toBeVisible();
  await preview.getByRole('button', { name: 'تصغير نافذة المعاينة' }).click();
  await expect(preview).not.toHaveClass(/storage-preview-expanded/);
});

test('file row opens preview directly and keeps a visible loading state until media is ready', async ({ page }) => {
  const requests = await mockSession(page);
  let releaseContent!: () => void;
  const pendingContent = new Promise<void>(resolve => { releaseContent = resolve; });
  await page.route('**/api/v1/storage/files/31/content?**', async route => {
    await pendingContent;
    await route.fallback();
  });
  try {
    await page.goto('/school-manager/storage');
    const row = page.locator('.file-row').filter({ hasText: file.displayName }).first();
    const view = row.getByRole('button', { name: `معاينة ${file.displayName}` });
    await expect(view).toBeVisible();
    await expect(row.locator('.file-actions button')).toHaveCount(3);
    await view.click();
    const preview = page.getByRole('dialog', { name: 'معاينة' });
    await expect(preview.locator('.preview-loading')).toBeVisible();
    await expect(preview.locator('.preview-orbit')).toBeVisible();
    await preview.screenshot({ path: 'test-results/storage/preview-loading-desktop.png', animations: 'disabled' });
    expect(requests.some(request => new URL(request.url).pathname.endsWith('/storage/files/31'))).toBeFalsy();
  } finally { releaseContent(); }
  const preview = page.getByRole('dialog', { name: 'معاينة' });
  await expect(preview.locator('.pdf-page-image')).toHaveAttribute('src', /^blob:/);
  await expect(preview.locator('.preview-loading')).toBeHidden();
});

test('folder move offers a browsable destination and prevents selecting the source', async ({ page }) => {
  const requests = await mockSession(page);
  await page.route('**/api/v1/storage/folders?**', async route => {
    const parent = Number(new URL(route.request().url()).searchParams.get('parentFolderId'));
    const items = parent === 7 ? [
      { id: 9, parentFolderId: 7, displayName: 'كوكو', kind: 'SchoolLibrary', rowVersion: 'v1' },
      { id: 33, parentFolderId: 7, displayName: 'المجلد الأخير', kind: 'SchoolLibrary', rowVersion: 'v2' }
    ] : parent === 33 ? [{ id: 44, parentFolderId: 33, displayName: 'مجلد الفريق', kind: 'SchoolLibrary', rowVersion: 'v3' }] : [];
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ isSuccess: true, data: { items, total: items.length, page: 1, pageSize: 25 } }) });
  });
  await page.goto('/school-manager/storage?folder=9');
  await expect(page.locator('h1')).toHaveText('كوكو');
  await page.getByRole('button', { name: 'نقل المجلد' }).click();
  const dialog = page.getByRole('dialog', { name: 'نقل المجلد' });
  await expect(dialog.locator('.move-source')).toContainText('كوكو');
  const sourceChoice = dialog.locator('.move-choice').filter({ hasText: 'المجلد الجاري نقله' });
  await expect(sourceChoice.getByRole('button', { name: 'اختيار' })).toBeDisabled();
  await dialog.getByRole('button', { name: 'فتح المجلد الأخير' }).click();
  await expect(dialog.locator('.move-choice').filter({ hasText: 'مجلد الفريق' })).toBeVisible();
  await dialog.locator('.move-choice').filter({ hasText: 'مجلد الفريق' }).getByRole('button', { name: 'اختيار' }).click();
  await dialog.screenshot({ path: 'test-results/storage/move-folder-desktop.png', animations: 'disabled' });
  await dialog.getByRole('button', { name: 'نقل إلى مجلد الفريق' }).click();
  await expect.poll(() => requests.find(request => request.url.includes('/storage/folders/9/parent'))?.body).toContain('"parentFolderId":44');
  await expect(dialog).toBeHidden();
});

test('file inspector opens from the validated row before slow details and defers evidence requests', async ({ page }) => {
  const requests = await mockSession(page);
  await page.goto('/school-manager/storage');
  await expect(page.locator('.file-name button')).toHaveText(file.displayName);
  let releaseDetails!: () => void;
  const pendingDetails = new Promise<void>(resolve => { releaseDetails = resolve; });
  await page.route('**/api/v1/storage/files/31?**', async route => { await pendingDetails; await route.fallback(); });
  try {
    const started = Date.now();
    await page.locator('.file-name button').click();
    const inspector = page.getByRole('dialog', { name: 'تفاصيل الملف' });
    await expect(inspector.locator('.inspector-current-file')).toContainText(file.displayName);
    await expect(inspector.locator('.inspector-preview')).toBeVisible();
    await expect(inspector.getByRole('tab')).toHaveCount(3);
    const bounds = await inspector.boundingBox();
    expect(bounds && bounds.width > bounds.height && bounds.x > 0 && bounds.y > 0).toBeTruthy();
    expect(await inspector.locator('.inspector-preview').evaluate(element => getComputedStyle(element).backgroundColor)).toBe('rgb(238, 234, 223)');
    expect(Date.now() - started).toBeLessThan(600);
    await expect(inspector.locator('app-storage-evidence')).toHaveCount(0);
    expect(requests.some(request => new URL(request.url).pathname.endsWith('/links'))).toBeFalsy();
    expect(requests.some(request => new URL(request.url).pathname.endsWith('/change-requests'))).toBeFalsy();
    await page.screenshot({ path: 'test-results/storage/file-inspector-desktop.png', animations: 'disabled' });
  } finally {
    releaseDetails();
  }
  const inspector = page.getByRole('dialog', { name: 'تفاصيل الملف' });
  await expect(inspector.locator('.inspector-version')).toHaveCount(1);
  await inspector.getByRole('tab', { name: 'روابط الشواهد' }).click();
  await expect(inspector.locator('#file-evidence-panel app-storage-evidence')).toBeVisible();
  await expect(inspector.locator('#file-evidence-panel')).toContainText('كل رابط له قرار مستقل');
  expect(requests.some(request => new URL(request.url).pathname.endsWith('/links'))).toBeTruthy();
  expect(requests.some(request => new URL(request.url).pathname.endsWith('/change-requests'))).toBeFalsy();
  const linkReads = requests.filter(request => new URL(request.url).pathname.endsWith('/links')).length;
  await inspector.getByRole('tab', { name: 'تعديل الملف' }).click();
  await expect(inspector.locator('#file-modify-panel app-storage-evidence')).toBeVisible();
  await expect(inspector.locator('#file-modify-panel')).toContainText('تبقى النسخة والقرارات السابقة محفوظة');
  expect(requests.some(request => new URL(request.url).pathname.endsWith('/change-requests'))).toBeTruthy();
  expect(requests.filter(request => new URL(request.url).pathname.endsWith('/links'))).toHaveLength(linkReads);
  await page.screenshot({ path: 'test-results/storage/file-modify-desktop.png', animations: 'disabled' });
  await inspector.getByRole('tab', { name: 'المعلومات الأساسية' }).click();
  await expect(inspector.locator('.inspector-current-file')).toContainText(file.displayName);
});

test('file rename action uses a full width button and enables only after a name change', async ({ page }) => {
  await mockSession(page);
  await page.goto('/school-manager/storage');
  await page.locator('.file-name button').click();
  const inspector = page.getByRole('dialog', {name:'تفاصيل الملف'});
  await inspector.getByRole('tab', {name:'تعديل الملف'}).click();
  const save = inspector.getByRole('button', {name:'حفظ الاسم'});
  await expect(save).toBeDisabled();
  await inspector.locator('.inspector-manage input:not([type=checkbox])').fill('خطة المدرسة المحدثة.pdf');
  await expect(save).toBeEnabled();
  const [inputBox,buttonBox] = await Promise.all([inspector.locator('.inspector-manage input:not([type=checkbox])').boundingBox(),save.boundingBox()]);
  expect(inputBox && buttonBox && buttonBox.width >= inputBox.width - 2).toBeTruthy();
  await expect.poll(() => save.evaluate(element => getComputedStyle(element).backgroundColor)).toBe('rgb(12, 103, 58)');
});

test('file dialog keeps its tabs usable without horizontal overflow @mobile', async ({ page }) => {
  await mockSession(page);
  await page.goto('/school-manager/storage');
  await page.locator('.file-name button').click();
  const dialog = page.getByRole('dialog', { name: 'تفاصيل الملف' });
  await expect(dialog.getByRole('tab')).toHaveCount(3);
  const box = await dialog.boundingBox();
  const viewport = page.viewportSize()!;
  expect(box && box.x >= 0 && box.x + box.width <= viewport.width + 1).toBeTruthy();
  await dialog.getByRole('tab', { name: 'تعديل الملف' }).click();
  await expect(dialog.getByRole('tabpanel', { name: 'تعديل الملف' })).toBeVisible();
  expect(await dialog.locator('.p-dialog-content').evaluate(element => element.scrollWidth <= element.clientWidth + 1)).toBeTruthy();
  await page.screenshot({ path: `test-results/storage/file-dialog-tabs-${test.info().project.name}.png`, animations: 'disabled' });
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

test('visits row and its icon are visible while archive status is still pending', async ({ page }) => {
  await mockSession(page);
  let release!: () => void;
  const pending = new Promise<void>(resolve => { release = resolve; });
  await page.route('**/visits/operations-status', async route => { await pending; await route.fallback(); });
  try {
    await page.goto('/school-manager/storage/overview');
    const visits = page.locator('.overview .actions a').filter({hasText:'تقارير الزيارات'});
    await expect(visits).toBeVisible();
    await expect(visits.locator('.action-icon .pi-clipboard')).toBeVisible();
    expect(await visits.locator('.action-icon .pi-clipboard').evaluate(element => getComputedStyle(element,'::before').content)).not.toBe('none');
  } finally { release(); }
});

test('overview readiness keeps its last numbers while fresh counts arrive', async ({ page }) => {
  await mockSession(page);
  await page.goto('/school-manager/storage/overview');
  await expect(page.locator('.overview .ring')).toHaveText('0%');
  await page.locator('.shell-sidebar__category a[href="/school-manager/storage"]').click();
  let release!: () => void;
  const pending = new Promise<void>(resolve => { release = resolve; });
  await page.route('**/storage/evidence-counts?**', async route => { await pending; await route.fallback(); });
  try {
    await page.locator('.shell-sidebar__category a[href="/school-manager/storage/overview"]').click();
    await expect(page.locator('.overview .ring')).toHaveText('0%');
    await expect(page.locator('.overview .ready .skeleton')).toHaveCount(0);
  } finally { release(); }
});

test('library follows the supplied root cards and nested breadcrumb hierarchy', async ({ page, isMobile }) => {
  const requests = await mockSession(page);
  const folders = ['الشؤون التعليمية', 'ملفات المعلمين', 'تقارير الزيارات', 'الشؤون الإدارية', 'كوكو']
    .map((displayName, index) => ({ id: index + 8, parentFolderId: 7, displayName, kind: 'SchoolLibrary', rowVersion: '' }));
  await page.route('**/api/v1/storage/folders?**', route => route.fulfill({ contentType: 'application/json',
    body: JSON.stringify({ isSuccess: true, data: { items: new URL(route.request().url()).searchParams.get('parentFolderId') === '7' ? folders : [], total: 5, page: 1, pageSize: 25 } }) }));
  await page.route('**/api/v1/storage/files?**', route => route.fulfill({ contentType: 'application/json',
    body: JSON.stringify({ isSuccess: true, data: { items: [], total: 0, page: 1, pageSize: 25 } }) }));
  await page.goto('/school-manager/storage');
  await expect(page.locator('.folder-entry')).toHaveCount(5);
  await expect(page.getByRole('heading', { name: 'مكتبة المدرسة', exact: true })).toBeVisible();
  await page.screenshot({ path: `test-results/storage/library-figma-root-${test.info().project.name}.png`, fullPage: true, animations: 'disabled' });
  if (!isMobile) {
    const first = await page.locator('.folder-entry').first().boundingBox();
    const fourth = await page.locator('.folder-entry').nth(3).boundingBox();
    const fifth = await page.locator('.folder-entry').nth(4).boundingBox();
    expect(first?.y).toBe(fourth?.y);
    expect(fifth!.y).toBeGreaterThan(first!.y);
  }
  await page.locator('.folder-tile').filter({ hasText: 'كوكو' }).click();
  await expect(page.getByRole('heading', { name: 'كوكو', exact: true })).toBeVisible();
  await expect(page.locator('.breadcrumbs button').last()).toHaveText('كوكو');
  expect(requests.some(request => new URL(request.url).pathname.endsWith('/storage/folders/12/path'))).toBeFalsy();
  await page.screenshot({ path: `test-results/storage/library-figma-nested-${test.info().project.name}.png`, fullPage: true, animations: 'disabled' });
});

test('manager upload dialog sends raw file bytes to the selected folder', async ({ page }) => {
  await mockSession(page);
  const bytes = Buffer.from('%PDF-1.7\nVerified upload');
  let sent: { type?: string; folder?: string | null; name?: string | null; length?: string | null; bytes?: Buffer } = {};
  await page.route('**/api/v1/storage/files?**', route => {
    const request = route.request();
    if (request.method() !== 'POST') return route.fallback();
    const url = new URL(request.url());
    sent = { type: request.headers()['content-type'], folder: url.searchParams.get('parentFolderId'),
      name: url.searchParams.get('fileName'), length: url.searchParams.get('length'), bytes: request.postDataBuffer() ?? undefined };
    return route.fulfill({ contentType: 'application/json', body: JSON.stringify({ isSuccess: true,
      data: { operationId: 12, storedFileId: 31, versionId: 1, status: 'Completed', displayName: 'شاهد.pdf', size: bytes.length,
        mimeType: 'application/pdf', uploadedAt: '2026-10-03T10:00:00Z' } }) });
  });
  await page.goto('/school-manager/storage');
  await page.getByRole('button', { name: 'رفع ملف' }).click();
  const dialog = page.getByRole('dialog', { name: 'رفع ملف' });
  await expect(dialog.getByText('الوجهة')).toBeVisible();
  await dialog.locator('input[type=file]').setInputFiles({ name: 'شاهد.pdf', mimeType: 'application/pdf', buffer: bytes });
  await expect(dialog.getByText('شاهد.pdf')).toBeVisible();
  await page.screenshot({ path: `test-results/storage/upload-dialog-${test.info().project.name}.png`, animations: 'disabled' });
  await dialog.getByRole('button', { name: 'رفع الملف' }).click();
  await expect(dialog).toHaveCount(0);
  expect(sent.type).toBe('application/octet-stream');
  expect(sent.folder).toBe('7');
  expect(sent.name).toBe('شاهد.pdf');
  expect(sent.length).toBe(String(bytes.length));
  expect(sent.bytes).toEqual(bytes);
});

test('storage keeps the platform shell typography and controls', async ({ page, isMobile }) => {
  await mockSession(page);
  await page.goto('/school-manager/storage/overview');
  await expect(page.locator('.overview')).toBeVisible();
  const fonts = await page.evaluate(() => ({
    app: getComputedStyle(document.body).fontFamily,
    shell: getComputedStyle(document.querySelector('.shell')!).fontFamily,
    page: getComputedStyle(document.querySelector('.overview')!).fontFamily
  }));
  expect(fonts.shell).toBe(fonts.app);
  expect(fonts.page).toBe(fonts.app);
  await expect(page.locator('.shell-topbar__right-group')).toBeVisible();
  if (isMobile) await expect(page.locator('.storage-mobile-nav')).toBeVisible();
  else await expect(page.locator('.shell-sidebar__controls')).toBeVisible();
});

test('moving between storage tabs does not wait for a repeated context request', async ({ page, isMobile }) => {
  const requests = await mockSession(page);
  await page.goto('/school-manager/storage/overview');
  await expect(page.locator('.overview .columns')).toBeVisible();
  await page.waitForLoadState('networkidle');
  const contextCalls = requests.filter(request => new URL(request.url).pathname.endsWith('/storage/context')).length;
  const accessCalls = requests.filter(request => new URL(request.url).pathname.endsWith('/storage/access')).length;
  await page.route('**/api/v1/storage/context?**', async route => {
    await new Promise(resolve => setTimeout(resolve, 1200));
    await route.fallback();
  });
  await page.route('**/api/v1/storage/access?**', async route => {
    await new Promise(resolve => setTimeout(resolve, 1200));
    await route.fallback();
  });
  const started = Date.now();
  await page.locator(isMobile ? '.storage-mobile-nav a[href="/school-manager/storage"]'
    : '.shell-sidebar__category a[href="/school-manager/storage"]').click();
  await expect(page).toHaveURL(/\/school-manager\/storage$/);
  expect(Date.now() - started).toBeLessThan(600);
  expect(requests.filter(request => new URL(request.url).pathname.endsWith('/storage/context'))).toHaveLength(contextCalls);
  expect(requests.filter(request => new URL(request.url).pathname.endsWith('/storage/access'))).toHaveLength(accessCalls);
});

test('storage sidebar is present while fresh context is still pending after refresh', async ({ page }) => {
  await mockSession(page);
  await page.goto('/school-manager/storage/overview');
  await expect(page.locator('.shell-sidebar__category').filter({ hasText: 'مساحة الملفات' })).toBeVisible();
  let releaseContext!: () => void;
  const pendingContext = new Promise<void>(resolve => { releaseContext = resolve; });
  await page.route('**/api/v1/storage/context?**', async route => { await pendingContext; await route.fallback(); });
  try {
    await page.reload();
    await expect(page.locator('.shell-sidebar__category').filter({ hasText: 'مساحة الملفات' })).toBeVisible();
    await expect(page.locator('.overview .columns')).toHaveCount(0);
  } finally {
    releaseContext();
  }
  await expect(page.locator('.overview .columns')).toBeVisible();
});

test('returning to the library shows its last rows while the server refreshes', async ({ page }) => {
  await mockSession(page);
  await page.goto('/school-manager/storage');
  await expect(page.locator('.file-name button')).toHaveText(file.displayName);
  await page.locator('.shell-sidebar__category a[href="/school-manager/storage/overview"]').click();
  await expect(page.locator('.overview .columns')).toBeVisible();
  let releaseFiles!: () => void;
  const pendingFiles = new Promise<void>(resolve => { releaseFiles = resolve; });
  await page.route('**/api/v1/storage/files?**', async route => { await pendingFiles; await route.fallback(); });
  try {
    const started = Date.now();
    await page.locator('.shell-sidebar__category a[href="/school-manager/storage"]').click();
    await expect(page).toHaveURL(/\/school-manager\/storage$/);
    await expect(page.locator('.file-name button')).toHaveText(file.displayName);
    expect(Date.now() - started).toBeLessThan(600);
    await expect(page.locator('.file-grid-loading')).toHaveCount(0);
  } finally {
    releaseFiles();
  }
});

test('first library load keeps a stable table surface without skeleton rows', async ({ page, isMobile }) => {
  await mockSession(page);
  await page.goto('/school-manager/storage/overview');
  await expect(page.locator('.overview .columns')).toBeVisible();
  let releaseFiles!: () => void;
  const heldFiles = new Promise<void>(resolve => { releaseFiles = resolve; });
  await page.route('**/api/v1/storage/files?**', async route => {
    await heldFiles;
    await route.fallback();
  });
  await page.locator(isMobile ? '.storage-mobile-nav a[href="/school-manager/storage"]'
    : '.shell-sidebar__category a[href="/school-manager/storage"]').click();
  try {
    await expect(page.locator('.file-pending')).toBeVisible();
    await expect(page.locator('.file-grid-loading')).toHaveCount(0);
    await expect(page.getByRole('progressbar')).toHaveCount(0);
  } finally {
    releaseFiles();
  }
  await expect(page.locator('.file-name button')).toBeVisible();
  await expect(page.locator('.file-pending')).toHaveCount(0);
});

test('manager administration uses live status and routes to teacher folders and import without mock counts', async ({ page }) => {
  const requests = await mockSession(page);
  await page.goto('/school-manager/storage/admin');
  await expect(page.getByRole('heading', { name: 'إدارة مساحة الملفات' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Drive والجاهزية' })).toBeVisible();
  await expect(page.getByText('الأرشفة تحتاج متابعة')).toBeVisible();
  await page.screenshot({path:`test-results/storage/admin-${test.info().project.name}.png`,fullPage:true,animations:'disabled'});
  await page.getByRole('link', { name: 'مجلدات المعلمين' }).click();
  await expect(page.getByRole('link', { name: 'فتح قائمة المعلمين' })).toHaveAttribute('href', '/teachers');
  await page.getByRole('link', { name: 'التفويض', exact: true }).click();
  await expect(page.getByText('لا توجد تفويضات مسجلة')).toBeVisible();
  expect(requests.some(request => new URL(request.url).pathname.endsWith('/storage/delegations'))).toBeTruthy();
  await page.getByRole('link', { name: 'الاستيراد التاريخي' }).click();
  await expect(page).toHaveURL(/tab=imports/);
  await expect(page.getByRole('heading', { name: 'اختر ملف المصدر' })).toBeVisible();
  await page.screenshot({path:`test-results/storage/admin-import-${test.info().project.name}.png`,fullPage:true,animations:'disabled'});
  await page.locator('input[name="sourceVersion"]').fill('نسخة المراجعة');
  await page.getByRole('link', { name: 'التفويض', exact: true }).click();
  await expect(page.getByText('لا توجد تفويضات مسجلة')).toBeVisible();
  await page.getByRole('link', { name: 'الاستيراد التاريخي' }).click();
  await expect(page.locator('input[name="sourceVersion"]')).toHaveValue('نسخة المراجعة');
  expect(requests.filter(request => new URL(request.url).pathname.endsWith('/storage/delegations'))).toHaveLength(1);
  expect(requests.filter(request => new URL(request.url).pathname.endsWith('/storage/imports'))).toHaveLength(1);
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
  await expect(page.locator('.breadcrumbs')).toHaveCount(0);
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
  await expect(page.locator('.breadcrumbs')).toHaveCount(0);
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
  expect(requests.some(r => new URL(r.url).pathname.endsWith('/storage/me/files') && r.key)).toBeTruthy();
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
  expect(requests.some(request => new URL(request.url).pathname.endsWith('/storage/me/files') && request.key)).toBeTruthy();
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
  await expect(page.locator('.inspector-info-grid .inspector-tip')).toBeVisible();
  await expect(page.locator('.delete-confirm')).toHaveCount(0);
});

test('retries preserve upload key and revocation clears previously loaded file data', async ({ page }) => {
  const requests = await mockSession(page, 'SchoolManager');
  let attempts = 0; const keys: string[] = [];
  await page.route('**/api/v1/storage/files?**', async route => {
    if (route.request().method() !== 'POST') return route.fallback();
    keys.push(route.request().headers()['idempotency-key']); attempts++;
    await route.fulfill({ status: attempts === 1 ? 503 : 202, contentType: 'application/json', body: JSON.stringify(attempts === 1
      ? { isSuccess: false, message: 'انقطع الاتصال أثناء الحفظ' }
      : { isSuccess: true, data: { operationId: 2, status: 'NeedsAttention', displayName: 'شاهد.pdf', size: 8, mimeType: 'application/pdf' } }) });
  });
  await page.goto('/school-manager/storage'); await expect(page.locator('.file-name button')).toBeVisible();
  await page.getByRole('button', { name: 'رفع ملف' }).click();
  await page.getByRole('dialog', { name: 'رفع ملف' }).locator('input[type=file]').setInputFiles({ name: 'شاهد.pdf', mimeType: 'application/pdf', buffer: Buffer.from('%PDF-1.7') });
  await page.getByRole('dialog', { name: 'رفع ملف' }).getByRole('button', { name: 'رفع الملف' }).click();
  await expect(page.getByRole('dialog', { name: 'رفع ملف' }).getByRole('alert')).toHaveText('تعذر حفظ الملف على الخادم. أعد المحاولة بنفس الملف.');
  await page.getByRole('dialog', { name: 'رفع ملف' }).getByRole('button', { name: 'إعادة المحاولة' }).click();
  await expect(page.getByRole('button', { name: 'التحقق من عملية الرفع' })).toBeVisible(); expect(keys[1]).toBe(keys[0]);
  await page.getByRole('dialog', { name: 'رفع ملف' }).getByRole('button', { name: 'إغلاق' }).click();
  await page.route('**/api/v1/storage/files?**', route => route.fulfill({ status: 403, contentType: 'application/json', body: JSON.stringify({ isSuccess: false, message: 'تم سحب التفويض' }) }));
  await page.getByRole('button', { name: 'بحث', exact: true }).click();
  await expect(page.locator('.storage-page [role=alert]')).toHaveText('تم سحب التفويض'); await expect(page.locator('.file-name')).toHaveCount(0);
  await expect(page.getByRole('dialog', { name: 'تفاصيل الملف' })).toHaveCount(0);
});
