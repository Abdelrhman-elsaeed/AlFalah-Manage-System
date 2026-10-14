// Actual running Angular UI, synthetic API fixtures only; no operational writes.
import assert from 'node:assert/strict';
import { mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { chromium } from '@playwright/test';
const origin = process.env.ALFALAH_DUTY_SETTINGS_ORIGIN || 'http://localhost:4200';
const evidence = resolve('../docs/specs/school-duty/verification/settings-screens');
mkdirSync(evidence, { recursive: true });
const browser = await chromium.launch({ headless: true });
const checks = [];
const teachers = [
  { profileId: 1, userId: 'a', name: 'عبد الرحمن محمد عبد الله عبد الرحمن الطويل · #1' },
  { profileId: 2, userId: 'b', name: 'أحمد محمد العتيبي · #2' },
  { profileId: 3, userId: 'c', name: 'أحمد محمد العتيبي · #3' }
];
async function setup(width, missing = false) {
  const page = await browser.newPage({ viewport: { width, height: 1000 }, reducedMotion: 'reduce' });
  const errors = [], requests = [];
  page.on('pageerror', e => errors.push(e.message));
  const user = { userId: 'synthetic-manager', fullName: 'مدير التحقق الاصطناعي', activeSchoolId: 1,
    roles: ['SchoolManager'], permissions: ['Duty.View','Duty.Manage','Timetable.View'], preferredLanguage: 'ar' };
  await page.addInitScript(user => {
    sessionStorage.setItem('alfalah_access_token', `e30.${btoa(JSON.stringify({ sub: user.userId, exp: 2000000000 }))}.fixture`);
    sessionStorage.setItem('alfalah_user', JSON.stringify(user)); localStorage.setItem('alfalah_language','ar');
  }, user);
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname; let data = {};
    if (path.endsWith('/auth/me')) data = user;
    else if (path.endsWith('/account/signature/requirement')) data = { required: false, hasSignature: true };
    else if (path.endsWith('/school-duty/context')) data = { canManage: true, school: 'مدرسة التحقق الاصطناعية',
      today: '2030-01-06', userId: user.userId, terms: [{ id: 1, yearId: 1, year: '2030', semester: 1, from: '2030-01-06', to: '2030-02-06' }],
      termId: 1, planRevision: 0, settings: null, teachers, suggestedWeekdays: missing ? [] : [0,2,4], days: [], history: [], fairness: [], pendingRetries: 0, closures: [], ownHistory: [] };
    else if (path.endsWith('/school-duty/plans/preview')) {
      requests.push(route.request().postDataJSON());
      return route.fulfill({ status: 409, json: { isSuccess: false, message: 'تغيّر التقويم؛ حدّث البيانات وأعد المعاينة.' } });
    }
    else if (path.endsWith('/auth/schools')) data = [];
    await route.fulfill({ json: { isSuccess: true, data, errors: [] } });
  });
  await page.goto(origin + '/school-duty'); await page.locator('.empty-state').waitFor();
  await page.getByRole('button', { name: 'إعداد المناوبات', exact: true }).click();
  await page.locator('.teacher-pool').waitFor();
  // PrimeNG's entry transform briefly scales the entire dialog, including fields.
  // Wait for its actual settled geometry; keep the exact 44px acceptance assertion.
  await page.waitForFunction(() => document.querySelector('.setup-content .p-calendar input')?.getBoundingClientRect().height === 44);
  return { page, errors, requests };
}
async function capture(page, name) {
  await page.screenshot({ path: resolve(evidence, name+'.png'), fullPage: true });
  const sizes = await page.evaluate(() => [document.documentElement,...document.querySelectorAll('.setup-content,.duty-dialog .p-dialog-content')].map(x => ({ scroll: x.scrollWidth, width: x.clientWidth })));
  for (const s of sizes) assert(s.scroll <= s.width + 1, name+': horizontal overflow');
}
async function calendars(page) {
  const sizes = await page.locator('.setup-content .p-calendar').evaluateAll(elements => elements.map(el => {
    const input = el.querySelector('input').getBoundingClientRect(), button = el.querySelector('button').getBoundingClientRect();
    return { inputHeight: input.height, buttonHeight: button.height, inputTop: input.top, buttonTop: button.top, width: input.width };
  }));
  for (const s of sizes) { assert.equal(s.inputHeight,44); assert.equal(s.buttonHeight,44); assert(Math.abs(s.inputTop-s.buttonTop)<1); assert(s.width>45); }
  return sizes;
}
try {
  for (const width of [390,768,1440]) {
    const { page, errors, requests } = await setup(width);
    assert.equal(await page.locator('.pool-row.selected').count(),3);
    assert.equal(await page.locator('.weekday-picker button').count(),0);
    assert.equal(await page.locator('.study-calendar span').count(),3);
    await page.locator('.setup-content').evaluate(x=>x.parentElement.scrollTop=0); await capture(page,`default-${width}`);
    const initialCalendarSizes = await calendars(page);
    const row = page.locator('.pool-row').first();
    await row.locator('button').first().focus(); await page.keyboard.press('Enter');
    assert.equal(await page.locator('.pool-row.selected').count(),2);
    await page.waitForFunction(()=>document.activeElement?.id==='duty-restore-a');
    await page.keyboard.press('Enter'); assert.equal(await page.locator('.pool-row.selected').count(),3);
    await page.waitForFunction(()=>document.activeElement?.id==='duty-exclude-a');
    await row.locator('button').nth(1).click(); await page.locator('#duty-exclude-from').waitFor();
    // Quick-date action moves keyboard focus to the date field; native picker remains usable.
    await page.keyboard.press('Escape');
    await page.locator('#duty-exclude-from').click();
    await page.locator('.p-datepicker:visible').getByText('6',{exact:true}).first().click();
    await page.locator('#duty-exclude-to').click();
    await page.locator('.p-datepicker:visible').getByText('8',{exact:true}).first().click();
    await page.locator('.exclusion-fields > label:last-child input').fill('فترة استبعاد اصطناعية محددة');
    await page.getByRole('button',{name:'إضافة استبعاد',exact:true}).click();
    assert.equal(await page.locator('.range-list > div').count(),1);
    await page.locator('.exclusions-section').scrollIntoViewIfNeeded(); await capture(page,`dated-${width}`);
    const datedCalendarSizes = await calendars(page);
    await page.locator('.exclusions-section .range-list button').click(); assert.equal(await page.locator('.range-list > div').count(),0);
    await page.locator('.exclusion-modes button').first().click();
    await page.locator('#duty-exclusion-teacher').click(); await page.getByRole('option',{name:teachers[1].name,exact:true}).click();
    await page.getByRole('button',{name:'إضافة استبعاد',exact:true}).click();
    await page.getByRole('button',{name:'إضافة استبعاد',exact:true}).click();
    assert.equal(await page.locator('.range-list > div').count(),1);
    await capture(page,`permanent-${width}`);
    await page.getByRole('button',{name:'معاينة التوزيع',exact:true}).click();
    await page.locator('.setup-content .feedback.error').waitFor();
    assert.deepEqual(requests[0].settings.teacherIds,['c','a']);
    assert.deepEqual(requests[0].settings.permanentlyExcludedTeacherIds,['b']);
    assert.deepEqual(requests[0].settings.weekdays,[0,2,4]);
    assert.equal(await page.locator('.range-list > div').count(),1);
    assert(await page.getByRole('button',{name:'معاينة التوزيع',exact:true}).isEnabled());
    await page.evaluate(()=>window.ng.getComponent(document.querySelector('app-school-duty')).translate.use('en'));
    await page.waitForFunction(()=>document.querySelector('.duty-workspace')?.getAttribute('dir')==='ltr');
    await page.locator('.setup-content').evaluate(x=>x.parentElement.scrollTop=0); await capture(page,`english-${width}`); await calendars(page);
    assert.deepEqual(errors,[]); checks.push({ width, allIncluded: true, permanentAndDated: true, duplicateSafe: true, errorRetainsDraft: true, timetableDays: true, rtlAndLtr: true, initialCalendarSizes, datedCalendarSizes });
    await page.close(); console.log(`Duty settings exercised ${width}px`);
  }
  const { page } = await setup(390,true);
  assert.equal(await page.locator('.study-calendar').count(),0);
  assert(await page.getByRole('button',{name:'معاينة التوزيع',exact:true}).isDisabled());
  assert.equal(await page.locator('a[href="/intelligent-timetable/timings"]').count(),1);
  await page.locator('#duty-from').scrollIntoViewIfNeeded(); await capture(page,'missing-calendar-390'); await page.close();
  checks.push({ missingStudyCalendar: true, blockedPreview: true, actionableTimetableLink: true });
  writeFileSync(resolve(evidence,'browser-results.json'),JSON.stringify({ verification:'Actual current Angular SPA; all API responses synthetic; no operational writes', checks, ownerReview:'Pending', physicalDevice:'Pending' },null,2));
  console.log('Duty settings browser checks passed.');
} finally { await browser.close(); }
