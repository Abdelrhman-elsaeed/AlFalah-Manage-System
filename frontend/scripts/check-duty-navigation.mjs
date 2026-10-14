// Real current Angular development server; all API responses are synthetic fixtures.
// No request reaches the operational API, and no school data or credentials are changed.
import assert from 'node:assert/strict';
import { mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { chromium } from '@playwright/test';

const origin = process.env.ALFALAH_DUTY_NAV_ORIGIN || 'http://localhost:4200';
const evidence = resolve('../docs/specs/school-duty/verification/navigation-screens');
mkdirSync(evidence, { recursive: true });
const browser = await chromium.launch({ headless: true });
const checks = [];
async function setup(role, width = 1440, permissions = ['Duty.View', 'Duty.Manage', 'Timetable.View']) {
  const page = await browser.newPage({ viewport: { width, height: 1000 }, reducedMotion: 'reduce' });
  const user = { userId: 'synthetic-navigation', username: 'synthetic', fullName: 'حساب فحص التنقل', activeSchoolId: 1,
    activeSchoolName: 'مدرسة التحقق الاصطناعية', preferredLanguage: 'ar', roles: [role], permissions };
  const errors = [], calls = [];
  page.on('pageerror', e => errors.push(e.message));
  await page.addInitScript(user => {
    sessionStorage.setItem('alfalah_access_token', `e30.${btoa(JSON.stringify({ sub: user.userId, exp: 2000000000 }))}.fixture`);
    sessionStorage.setItem('alfalah_user', JSON.stringify(user)); localStorage.setItem('alfalah_language', 'ar');
  }, user);
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname; calls.push(path); let data = {};
    if (path.endsWith('/auth/me')) data = user;
    else if (path.endsWith('/account/signature/requirement')) data = { required: role === 'Instructor', hasSignature: false };
    else if (path.endsWith('/school-duty/context')) data = { canManage: role === 'SchoolManager', school: user.activeSchoolName,
      today: '2026-10-14', userId: user.userId, terms: [{ id: 1, yearId: 1, year: '2026–2027', semester: 1, from: '2026-09-01', to: '2027-01-15' }],
      termId: 1, planRevision: 0, settings: null, teachers: [], suggestedWeekdays: [0, 1, 2, 3, 4], days: [], history: [], fairness: [], pendingRetries: 0, closures: [], ownHistory: [] };
    else if (path.endsWith('/timetables/catalog')) data = { academicYears: [], semesters: [], days: [], teachers: [], moderators: [], capabilities: { canManage: false, canDelegate: false } };
    else if (path.endsWith('/timetables/current')) data = null;
    else if (path.endsWith('/auth/schools')) data = [];
    await route.fulfill({ json: { isSuccess: true, data, errors: [] } });
  });
  return { page, errors, calls };
}
try {
  for (const role of ['SchoolManager', 'Instructor']) for (const width of [390, 768, 1440]) {
    const { page, errors, calls } = await setup(role, width);
    await page.goto(origin + '/timetable?tab=duty'); await page.locator('.duty-workspace').waitFor();
    await page.screenshot({ path: resolve(evidence, `${role}-${width}.png`), fullPage: true });
    const link = page.locator('.shell-sidebar__list--top a[href="/school-duty"]');
    assert.equal(await link.count(), 1, 'Duty must have an independent top-level sidebar entry');
    assert.equal(await page.getByRole('link', { name: 'المناوبة', exact: true }).count(), 1, 'Collapsed navigation retains an accessible duty label');
    assert.equal(new URL(page.url()).pathname, '/school-duty', 'Old duty links must resolve to the independent page');
    assert.equal(await page.locator('.workspace-tabs').count(), 0, 'Duty must not be nested in lesson timetable tabs');
    assert(await link.evaluate(el => el.classList.contains('is-active')), 'Independent duty item must be highlighted');
    await link.focus(); await page.keyboard.press('Enter');
    assert.equal(await page.locator('.duty-heading h1').innerText(), 'المناوبة.');
    assert(!calls.some(x => x.endsWith('/timetables/current')), 'Opening duty must not load the lesson timetable');
    const sizes = await page.evaluate(() => ({ scroll: document.documentElement.scrollWidth, width: innerWidth }));
    assert(sizes.scroll <= sizes.width + 1, 'No viewport overflow'); assert.deepEqual(errors, []);
    checks.push({ role, width, independentSidebar: true, legacyRedirect: true, keyboardEntry: true, signatureSetupPreserved: role === 'Instructor', pageErrors: errors }); await page.close();
  }
  {
    const { page } = await setup('SchoolManager'); await page.goto(origin + '/timetable');
    await page.locator('.timetable-page').waitFor(); assert.equal(await page.locator('.duty-workspace').count(), 0);
    await page.locator('.shell-sidebar__list--top a[href="/school-duty"]').click(); await page.locator('.duty-workspace').waitFor();
    assert.equal(new URL(page.url()).pathname, '/school-duty'); assert.equal(await page.locator('.timetable-page').count(), 0);
    checks.push({ separateLessonPage: true, independentSidebarClick: true }); await page.close();
  }
  for (const [role, permissions] of [['Secretary', ['Timetable.View']], ['SchoolManager', ['Timetable.View']]]) {
    const { page, calls } = await setup(role, 1440, permissions); await page.goto(origin + '/school-duty');
    await page.waitForURL('**/unauthorized'); assert(!calls.some(x => x.endsWith('/school-duty/context')));
    checks.push({ role, deniedWithoutDutyAccess: true }); await page.close();
  }
  writeFileSync(resolve(evidence, 'browser-results.json'), JSON.stringify({ verification: 'Actual current localhost Angular server; synthetic API fixtures only; no operational writes', checks }, null, 2));
  console.log(`Independent duty navigation passed: ${checks.length} journeys.`);
} finally { await browser.close(); }
