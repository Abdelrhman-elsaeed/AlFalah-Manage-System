// Browser interaction regression checks against mocked API contracts. Run with a local Angular dev server.
// PLAYWRIGHT_MODULE can point at an existing Playwright installation; no app dependency is required.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const assert = require('node:assert/strict');

(async () => {
  const browser = await chromium.launch({ headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
    const user = { userId: 'test', username: 'test', fullName: 'مدير الاختبار', preferredLanguage: 'ar', activeSchoolId: 1,
      roles: ['SchoolManager'], permissions: ['Timetable.View', 'Timetable.Manage'] };
    const token = `e30.${Buffer.from(JSON.stringify({ sub: 'test', exp: Math.floor(Date.now()/1000) + 3600 })).toString('base64url')}.test`;
    await page.addInitScript(({ user, token }) => {
      sessionStorage.setItem('alfalah_access_token', token); sessionStorage.setItem('alfalah_user', JSON.stringify(user));
    }, { user, token });
    let revision = 1, canOverride = true, posted = null, conflict = false;
    const history = [];
    const response = data => ({ isSuccess: true, data, message: '', errors: [] });
    const preview = [1, 2].map(period => ({ entryId: period, teacherName: 'أحمد', toTeacherName: 'محمد', classroom: 'أول أ',
      subject: 'الرياضيات', fromPeriod: period, toPeriod: period, fromTime: '08:00–08:45', toTime: '08:00–08:45', roomId: 1 }));
    const errors = []; page.on('pageerror', e => errors.push(e.message));
    await page.route('**/api/**', async route => {
      const url = new URL(route.request().url());
      let data = [];
      if (url.pathname.endsWith('/auth/me')) data = user;
      else if (url.pathname.endsWith('/substitutions')) data = [{ id: 1, title: 'جدول الاختبار', revision, isPublished: true }];
      else if (url.pathname.endsWith('/substitutions/1')) data = { timetableId: 1, title: 'جدول الاختبار', revision, isPublished: true,
        canManage: true, canOverride, lessons: [{ entryId: 1, teacherId: 1, teacherName: 'أحمد', classroom: 'أول أ', subject: 'الرياضيات',
          periods: [1,2], entryIds: [1,2], start: '08:00', end: '09:30', roomId: 1 }], history };
      else if (url.pathname.endsWith('/candidates')) data = { timetableId: 1, revision, date: url.searchParams.get('date'), sourceEntryId: 1,
        mode: url.searchParams.get('mode'), canOverride, expiresAt: new Date(Date.now() + 300000).toISOString(),
        candidates: ['Green','Yellow','Red'].map(color => ({ id: color, kind: 'Substitution', color,
          label: { Green: 'معلم متاح', Yellow: 'محمد', Red: 'معلم مشغول' }[color],
          errors: color === 'Red' ? ['تعارض حرج'] : [], warnings: color === 'Yellow' ? ['أربع حصص متتالية'] : [], preview })) };
      else if (url.pathname.endsWith('/execute')) {
        posted = route.request().postDataJSON();
        if (conflict) return route.fulfill({ status: 409, json: { isSuccess: false, message: 'Stale', errors: ['Stale'] } });
        revision++; data = { id: revision, kind: 'Substitution', beforeRevision: revision-1, afterRevision: revision, reason: posted.overrideReason,
          requestedBy: 'manager', approvedBy: 'manager', confirmedAt: new Date().toISOString() }; history.push(data);
      }
      await route.fulfill({ json: response(data) });
    });
    await page.goto(`${process.env.APP_URL || 'http://localhost:4200'}/intelligent-timetable/substitutions`);
    await page.getByRole('button', { name: 'عرض البدائل' }).click();
    await page.locator('.candidate.Red').waitFor();
    assert(await page.locator('.candidate.Red button').isDisabled());
    assert.match(await page.locator('.source').innerText(), /1، 2/);
    await page.locator('.candidate.Yellow button').click();
    const confirm = page.getByRole('button', { name: 'تأكيد التبديل', exact: true });
    assert(await confirm.isDisabled());
    assert.equal(await page.locator('.preview-grid article').count(), 2);
    await page.locator('textarea').fill('تغطية غياب المعلم');
    assert(await confirm.isEnabled()); await confirm.click();
    await page.locator('.notice').waitFor();
    assert.equal(posted.overrideReason, 'تغطية غياب المعلم'); assert.equal(posted.sourceEntryId, 1);
    assert.equal(posted.revision, 1); assert(posted.requestId);
    assert.match(await page.locator('.history').innerText(), /تغطية غياب المعلم/);
    // Stale confirmation refreshes candidates and requires a new review.
    await page.getByRole('button', { name: 'عرض البدائل' }).click(); await page.locator('.candidate.Green button').click();
    conflict = true; await confirm.click(); await page.locator('.candidate.Green').waitFor();
    await page.locator('.p-dialog').waitFor({ state: 'hidden' });
    assert.match(await page.locator('main .error').innerText(), /تغير الجدول/);
    // A manager without the override capability cannot open a yellow confirmation.
    await page.getByRole('button', { name: 'إلغاء الاختيار' }).click(); canOverride = false;
    await page.getByRole('button', { name: 'عرض البدائل' }).click();
    await page.locator('.candidate.Yellow').waitFor(); assert(await page.locator('.candidate.Yellow button').isDisabled());
    await page.setViewportSize({ width: 390, height: 844 });
    if (!(await page.locator('main.daily-dashboard').evaluate(el => el.scrollWidth <= el.clientWidth + 2)))
      console.log(await page.locator('main.daily-dashboard').evaluate(el => ({ width: el.clientWidth, scroll: el.scrollWidth,
        overflowing: [...el.querySelectorAll('*')].filter(x => x.getBoundingClientRect().width > el.clientWidth).map(x => ({ tag: x.tagName, cls: x.className, width: x.getBoundingClientRect().width })) })));
    assert(await page.locator('main.daily-dashboard').evaluate(el => el.scrollWidth <= el.clientWidth + 2));
    assert.deepEqual(errors, []);
    console.log('Passed: red disabled, paired preview, required yellow reason, confirmation payload/history, stale recovery, override permission, mobile layout.');
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
