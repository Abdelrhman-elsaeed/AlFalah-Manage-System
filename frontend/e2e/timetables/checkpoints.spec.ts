import { test, expect, Page } from '@playwright/test';
import { resolve } from 'node:path';

const screenshots = resolve('../docs/investigations/smartble/evidence/d07');
const actor = { userId: 'ui-manager', username: 'ui-manager', fullName: 'مدير مدرسة الاختبار',
  activeSchoolId: 1, activeSchoolName: 'مدرسة تجريبية', preferredLanguage: 'ar',
  roles: ['SchoolManager'], permissions: ['Timetable.View', 'Timetable.Manage'] };

async function fixture(page: Page, failCheckpoint = false) {
  const capabilities = { canManage: true, canDelegate: false, canViewVersions: true };
  const periods = [1, 2, 3, 4].map(sequence => ({ sequence, displayLabel: `الحصة ${sequence}`,
    startLocalTime: `${7 + sequence}:00:00`, endLocalTime: `${7 + sequence}:45:00` }));
  const bellSchedule = { id: 1, revisionId: 1, revision: 1, name: 'توقيت تجريبي',
    schoolTimeZoneId: 'Africa/Cairo', defaultPeriods: periods,
    days: [2, 3, 4, 5, 6].map(day => ({ day, isStudyDay: true, usesDefaultSchedule: true, periods: [], usesDefaultBreaks: true, breaks: [] })),
    defaultBreaks: [] };
  const source = { id: 1, schoolId: 1, academicYearId: 1, academicYearName: '2026/2027', semester: 1,
    semesterLabelAr: 'الفصل الأول', title: 'الجدول الأساسي التجريبي', revision: 2, isPublished: false,
    isClone: false, updatedAt: '2026-10-13T10:00:00Z', publishedAt: null, timetableSetupProfileId: 1,
    capabilities, bellSchedule, timingsRequireRevalidation: false,
    entries: [{ id: 10, instructorProfileId: 1, day: 2, period: 1, entryType: 1, classLabel: 'الأول أ', subject: 'رياضيات' }],
    teacherSummaries: [{ instructorProfileId: 1, lessonCount: 1, standbyCount: 0 }] };
  const tables = [source];
  const versions: Record<number, any[]> = { 1: [{ id: 2, versionNumber: 2, title: source.title,
    changeKind: 2, changeKindLabelAr: 'حفظ', createdAt: source.updatedAt, createdByFullName: actor.fullName, restoredFromVersionNumber: null,
    changeSummary: 'تم حفظ تعديل يدوي', changeDetails: [{ code: 'changed', label: 'حصص عُدلت', count: 1 }] }] };
  const calls: { path: string; method: string; body: any }[] = [];
  await page.addInitScript(user => {
    sessionStorage.setItem('alfalah_access_token', `e30.${btoa(JSON.stringify({ sub: user.userId, exp: 2000000000 }))}.mock`);
    sessionStorage.setItem('alfalah_user', JSON.stringify(user));
  }, actor);
  await page.route('**/api/**', async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    const body = request.postData() ? request.postDataJSON() : null;
    calls.push({ path, method: request.method(), body });
    let data: any = {};
    if (path.endsWith('/auth/me')) data = actor;
    else if (path.endsWith('/notifications')) data = { items: [], total: 0 };
    else if (path.endsWith('/catalog')) data = { schoolId: 1, schoolName: 'مدرسة تجريبية', capabilities, moderators: [], bellSchedule,
      academicYears: [{ id: 1, code: '2026', nameAr: '2026/2027', isActive: true }],
      semesters: [{ value: 1, labelAr: 'الفصل الأول' }], days: ['السبت', 'الأحد', 'الاثنين', 'الثلاثاء', 'الأربعاء', 'الخميس'].map((labelAr, i) => ({ value: i + 1, labelAr })),
      teachers: [{ instructorProfileId: 1, userId: 'teacher-test', fullName: 'معلم تجريبي', classes: ['الأول أ'], isCurrentUser: false }] };
    else if (path.endsWith('/current')) data = source;
    else if (path === '/api/v1/timetables') data = tables.map(t => ({ id: t.id, title: t.title, isClone: t.isClone, isPublished: t.isPublished }));
    else if (path.endsWith('/checkpoints')) {
      if (failCheckpoint) return route.fulfill({ status: 409, contentType: 'application/json', body: JSON.stringify({ isSuccess: false, message: 'هذه النسخة قديمة. حدّث الصفحة.' }) });
      versions[1].unshift({ ...versions[1][0], id: 3, versionNumber: 3, changeKind: 10, changeKindLabelAr: 'نقطة استعادة', checkpointName: body.name, checkpointNote: body.note,
        changeSummary: 'نقطة استعادة محفوظة', changeDetails: [{ code: 'checkpoint', label: 'لقطة كاملة محفوظة', count: 1 }] });
      data = source;
    } else if (path.endsWith('/clone')) {
      const clone = { ...source, id: 2, title: body.title, revision: 1, isClone: true, entries: source.entries.map(e => ({ ...e, id: 20 })) };
      tables.push(clone);
      versions[2] = [{ ...versions[1][0], id: 4, versionNumber: 1, changeKind: 11, changeKindLabelAr: 'نسخ مستقل', checkpointName: null, checkpointNote: null,
        sourceTimetableId: 1, sourceVersionNumber: body.versionNumber ?? 3, changeSummary: 'تم إنشاء نسخة مستقلة',
        changeDetails: [{ code: 'clone', label: 'مصدر النسخة', count: 1, before: 'الإصدار 3', after: body.title }] }];
      data = clone;
    } else if (path.endsWith('/versions')) data = versions[Number(path.split('/')[4])] ?? [];
    else if (path.endsWith('/restore')) { source.revision++; data = source; }
    else if (/\/timetables\/\d+$/.test(path)) data = tables.find(t => t.id === Number(path.split('/').pop()));
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ isSuccess: true, data }) });
  });
  await page.goto('/timetable');
  await expect(page.getByRole('button', { name: 'تسمية نقطة استعادة', exact: true })).toBeVisible();
  return calls;
}

test('checkpoint and historical clone expose actor, source and reusable table selection', async ({ page }) => {
  const calls = await fixture(page);
  await page.getByRole('button', { name: 'تسمية نقطة استعادة', exact: true }).click();
  const checkpoint = page.getByRole('dialog', { name: 'إنشاء نقطة استعادة' });
  await checkpoint.getByLabel('اسم النقطة').fill('قبل تعديل الإسنادات');
  await checkpoint.getByLabel('ملاحظة اختيارية').fill('نقطة محفوظة قبل تجربة توزيع جديد للحصص');
  await checkpoint.screenshot({ path: resolve(screenshots, 'checkpoint-dialog.png') });
  await checkpoint.getByRole('button', { name: 'حفظ النقطة' }).click();
  const history = page.getByRole('dialog', { name: 'سجل تغييرات الجدول' });
  await expect(history).toContainText('قبل تعديل الإسنادات');
  await expect(history).toContainText(actor.fullName);
  await history.screenshot({ path: resolve(screenshots, 'checkpoint-history.png') });
  expect(calls.find(c => c.path.endsWith('/checkpoints'))?.body).toMatchObject({ revision: 2, name: 'قبل تعديل الإسنادات' });
  await history.locator('article').first().getByRole('button', { name: 'نسخ من هنا' }).click();
  const clone = page.getByRole('dialog', { name: 'نسخ الجدول إلى مسودة مستقلة' });
  await clone.getByLabel('عنوان النسخة').fill('تجربة توزيع الحصص — نسخة مستقلة');
  await clone.screenshot({ path: resolve(screenshots, 'clone-dialog.png') });
  await clone.getByRole('button', { name: 'إنشاء النسخة' }).click();
  await expect(page.locator('.status-block')).toContainText('نسخة تجريبية');
  await expect(page.getByRole('button', { name: 'نشر للمعلمين' })).toHaveCount(0);
  expect(calls.find(c => c.path.endsWith('/clone'))?.body).toMatchObject({ revision: 2, versionNumber: 3 });
  await page.getByRole('button', { name: 'سجل التغييرات', exact: true }).click();
  await expect(history).toContainText('مصدر النسخة: جدول 1 · الإصدار 3');
  await history.screenshot({ path: resolve(screenshots, 'clone-provenance.png') });
  await page.keyboard.press('Escape');
  await page.locator('.timetable-picker select').selectOption('1');
  await expect(page.locator('.status-block')).not.toContainText('نسخة تجريبية');
  await page.reload();
  await expect(page.locator('.timetable-picker select')).toBeVisible();
  await page.locator('.timetable-picker select').selectOption('2');
  await expect(page.locator('.status-block')).toContainText('نسخة تجريبية');
});

test('checkpoint errors keep the form and entered note available for retry', async ({ page }) => {
  await fixture(page, true);
  await page.getByRole('button', { name: 'تسمية نقطة استعادة', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'إنشاء نقطة استعادة' });
  await expect(dialog.getByRole('button', { name: 'حفظ النقطة' })).toBeDisabled();
  await dialog.getByLabel('اسم النقطة').fill('قرار تجريبي');
  await dialog.getByRole('button', { name: 'حفظ النقطة' }).click();
  await expect(page.getByText('تعذر إنشاء نقطة الاستعادة', { exact: true })).toBeVisible();
  await expect(dialog.getByLabel('اسم النقطة')).toHaveValue('قرار تجريبي');
});
