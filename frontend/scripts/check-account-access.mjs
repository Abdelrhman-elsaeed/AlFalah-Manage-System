// Isolated browser fixtures: no real credentials or database writes.
import { createServer } from 'node:http';
import { readFileSync, existsSync, mkdirSync } from 'node:fs';
import { resolve, extname, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import assert from 'node:assert/strict';
import { chromium } from '@playwright/test';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const dist = resolve(root, 'dist/al-falah-app/browser');
const results = resolve(root, 'test-results');
mkdirSync(results, { recursive: true });
const mime = { '.html': 'text/html', '.js': 'application/javascript', '.css': 'text/css', '.svg': 'image/svg+xml', '.png': 'image/png', '.woff2': 'font/woff2', '.json': 'application/json' };
const server = createServer((req, res) => {
  let file = resolve(dist, decodeURIComponent(new URL(req.url, 'http://localhost').pathname).replace(/^\/+/, '') || 'index.html');
  if (!file.startsWith(dist + '/') && !file.startsWith(dist + '\\')) { res.writeHead(403); res.end(); return; }
  if (!existsSync(file)) file = resolve(dist, 'index.html');
  res.writeHead(200, { 'Content-Type': mime[extname(file)] || 'application/octet-stream' });
  res.end(readFileSync(file));
});
await new Promise(done => server.listen(0, '127.0.0.1', done));
const origin = `http://127.0.0.1:${server.address().port}`;
const browser = await chromium.launch({ headless: true });
const errors = [];
const report = [];
const user = role => ({ userId: `${role}-preview`, username: 'preview', fullName: 'حساب تجريبي',
  activeSchoolId: 1, activeSchoolName: 'مدارس الفلاح', preferredLanguage: 'ar', roles: [role],
  permissions: ['User.View', 'User.Create', 'User.Edit', 'Instructor.View', 'Instructor.Edit', 'Dashboard.SchoolManager'] });
const staff = { userId: 'staff-preview', username: 'preview.staff', fullName: 'موظف تجريبي', firstName: 'موظف', lastName: 'تجريبي',
  isActive: true, roles: ['Moderator', 'SocialWorker'], preferredLanguage: 'ar', classes: [],
  schools: [{ schoolId: 2, schoolName: 'مدرسة أخرى', role: 'Moderator' }, { schoolId: 1, schoolName: 'مدارس الفلاح', role: 'SocialWorker' }] };
const paged = items => ({ items, totalCount: items.length, page: 1, pageSize: 20, totalPages: 1, hasPrevious: false, hasNext: false });

async function setup(role, signed = false) {
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
  page.on('pageerror', error => errors.push(error.message));
  const current = user(role);
  const state = { signed, image: null, reveals: 0, legacy: false, saves: 0, failSave: false, roleQueries: [], creates: [] };
  await page.addInitScript(current => {
    sessionStorage.setItem('alfalah_access_token', `e30.${btoa(JSON.stringify({ sub: current.userId, exp: 2000000000 }))}.mock`);
    sessionStorage.setItem('alfalah_user', JSON.stringify(current));
  }, current);
  await page.route('**/api/**', async route => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname;
    let data = {};
    if (path.endsWith('/auth/me')) data = current;
    else if (path.endsWith('/auth/schools')) data = [];
    else if (path.endsWith('/account/signature/requirement')) data = { required: role !== 'SchoolManager' && !state.signed, hasSignature: state.signed };
    else if (path.endsWith('/account/signature')) {
      if (request.method() === 'PUT') {
        state.saves++;
        if (state.failSave) return route.fulfill({ status: 503, json: { isSuccess: false } });
        state.image = request.postDataJSON().signatureDrawnData;
        assert.match(state.image, /^data:image\/png;base64,/);
        state.signed = true; data = true;
      } else data = { signatureDrawnData: state.image };
    } else if (path.endsWith('/password/reveal')) {
      assert.equal(request.method(), 'POST');
      state.reveals++; data = { available: !state.legacy, password: state.legacy ? null : 'Fixture@Secret2026' };
    } else if (path.endsWith('/users/staff-preview')) data = staff;
    else if (path.endsWith('/users')) {
      if (request.method() === 'POST') { state.creates.push(request.postDataJSON()); data = staff; }
      else { state.roleQueries.push(url.searchParams.get('role')); data = paged([staff]); }
    } else if (path.endsWith('/schools')) data = paged([{ id: 1, name: 'مدارس الفلاح', city: 'جدة', isActive: true }]);
    else if (path.endsWith('/teachers')) data = paged([{ userId: staff.userId, username: staff.username, fullName: staff.fullName,
      employeeNumber: 'DEMO-1', subject: 'الرياضيات', stage: 1, schoolName: 'مدارس الفلاح', visitsCount: 0 }]);
    else if (path.endsWith('/account/teaching')) data = { subject: 'الرياضيات', classes: [] };
    else if (path.endsWith('/storage/access')) return route.fulfill({ status: 403, json: { isSuccess: false } });
    else if (path.includes('capabilities')) data = { canAccess: false };
    await route.fulfill({ json: { isSuccess: true, data } });
  });
  return { page, state };
}

try {
  const manager = await setup('SchoolManager');
  const { page, state } = manager;
  for (const route of ['student-affairs-officers', 'social-workers', 'security-guards', 'secretaries', 'guardians', 'school-managers', 'moderators', '']) {
    await page.goto(origin + '/users/' + route);
    await page.locator('app-user-password-cell').first().waitFor();
    assert.equal(await page.locator('app-user-password-cell .secret').first().innerText(), '••••••••');
    assert.equal(state.reveals, 0, 'Password disclosure must be requested only by clicking');
    assert.equal(await page.locator('th').filter({ hasText: 'كلمة المرور' }).count(), 1);
  }
  assert(state.roleQueries.includes('SocialWorker'));
  assert(state.roleQueries.includes('StudentAffairsOfficer'));
  assert(state.roleQueries.includes('SecurityGuard'));
  for (const route of ['student-affairs-officers', 'social-workers', 'security-guards', 'guardians'])
    assert.equal(await page.locator(`.shell-sidebar a[href="/users/${route}"]`).count(), 1, `Missing people link: ${route}`);
  await page.goto(origin + '/users/social-workers');
  const cell = page.locator('app-user-password-cell').first();
  await cell.locator('button').click();
  await page.waitForFunction(() => document.querySelector('app-user-password-cell .secret')?.textContent.includes('Fixture@Secret2026'));
  assert.equal(state.reveals, 1);
  await cell.locator('button').click();
  assert.equal(await cell.locator('.secret').innerText(), '••••••••');
  state.legacy = true;
  await cell.locator('button').click();
  await cell.locator('.unavailable').waitFor();
  await page.screenshot({ path: resolve(results, 'account-people-legacy.png'), fullPage: true });
  state.legacy = false;
  await page.goto(origin + '/teachers');
  await page.locator('app-user-password-cell').first().waitFor();
  assert.equal(await page.locator('th').filter({ hasText: 'كلمة المرور' }).count(), 1);
  report.push('Role tables, role filters, people links, masked passwords, explicit reveal/hide, legacy message');

  await page.goto(origin + '/users/staff-preview/edit?role=SocialWorker');
  await page.locator('#user-role-edit').waitFor();
  assert.match(await page.locator('app-clearable-select[formcontrolname="role"]').innerText(), /موجه|اجتماعي/);
  assert.equal(await page.locator('#teacher-full-name').count(), 0, 'Active school role must override unrelated global role order');
  for (const role of ['StudentAffairsOfficer', 'SocialWorker', 'SecurityGuard', 'Secretary', 'Guardian']) {
    await page.goto(origin + '/users/new?role=' + role);
    await page.locator('#user-first').waitFor();
    await page.locator('#user-username').fill('fixture.' + role.toLowerCase());
    await page.locator('#user-password').fill('Fixture@Secret2026');
    await page.locator('#user-first').fill('موظف');
    await page.locator('#user-last').fill('تجريبي');
    await page.waitForFunction(() => document.querySelector('app-clearable-select[formcontrolname="schoolId"]')?.textContent.includes('مدارس الفلاح'));
    await page.locator('button[type="submit"]').click();
    await page.waitForURL(origin + '/users');
    const created = state.creates.at(-1);
    assert.equal(created.role, role); assert.equal(created.schoolId, 1);
  }
  report.push('Staff creation preselects roles and active school; editing preserves school role');

  await page.goto(origin + '/account/settings');
  await page.locator('#current-password').waitFor();
  assert.equal(await page.locator('canvas').count(), 0);
  assert.equal(await page.locator('.shell-sidebar a[href="/account/signature"]').count(), 1);
  assert.equal(await page.locator('.shell-sidebar a[href="/account/settings"]').count(), 1);
  await page.screenshot({ path: resolve(results, 'account-settings.png'), fullPage: true });
  await page.goto(origin + '/account/signature');
  await page.locator('canvas').waitFor();
  assert.equal(await page.locator('#current-password').count(), 0);
  await page.screenshot({ path: resolve(results, 'account-signature.png'), fullPage: true });
  await page.close();
  report.push('Signature and account settings are separate; manager signature is optional');

  const instructor = await setup('Instructor');
  await instructor.page.goto(origin + '/account/settings');
  await instructor.page.waitForURL('**/auth/signature?returnUrl=*');
  await instructor.page.locator('canvas').waitFor();
  assert.equal(await instructor.page.locator('.shell-sidebar').count(), 0, 'Onboarding must appear before dashboard shell');
  const save = instructor.page.locator('app-signature-editor button').last();
  await save.waitFor();
  await instructor.page.waitForFunction(() => !document.querySelector('app-signature-editor button:last-child').disabled);
  await save.click();
  await instructor.page.locator('app-signature-editor [role="alert"]').waitFor();
  assert.equal(instructor.state.saves, 0, 'Empty signature cannot be submitted');
  const canvas = await instructor.page.locator('canvas').boundingBox();
  await instructor.page.mouse.move(canvas.x + 25, canvas.y + 80);
  await instructor.page.mouse.down();
  await instructor.page.mouse.move(canvas.x + 65, canvas.y + 40, { steps: 8 });
  await instructor.page.mouse.move(canvas.x + 135, canvas.y + 115, { steps: 8 });
  await instructor.page.mouse.up();
  instructor.state.failSave = true;
  await save.click();
  await instructor.page.locator('app-signature-editor [role="alert"]').waitFor();
  assert(instructor.page.url().includes('/auth/signature'));
  instructor.state.failSave = false;
  await save.click();
  await instructor.page.waitForURL(origin + '/account/settings');
  assert.equal(instructor.state.saves, 2);
  await instructor.page.locator('#current-password').waitFor();
  await instructor.page.reload();
  await instructor.page.locator('#current-password').waitFor();
  assert.equal(await instructor.page.locator('.onboarding').count(), 0, 'Saved signature must survive reload');
  assert.equal(await instructor.page.locator('.shell-sidebar a[href="/account/signature"]').count(), 1);
  await instructor.page.goto(origin + '/account/signature');
  await instructor.page.locator('canvas').waitFor();
  assert.equal(await instructor.page.locator('#current-password').count(), 0);
  await instructor.page.close();
  report.push('First signature blocks entry; blank/rejected saves stay blocked; successful save resumes destination once');

  const mobile = await setup('SocialWorker');
  await mobile.page.setViewportSize({ width: 390, height: 844 });
  await mobile.page.goto(origin + '/account/settings');
  await mobile.page.waitForURL('**/auth/signature?returnUrl=*');
  await mobile.page.locator('canvas').waitFor();
  assert.equal(await mobile.page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false);
  await mobile.page.screenshot({ path: resolve(results, 'account-onboarding-mobile.png'), fullPage: true });
  await mobile.page.close();
  const restricted = await setup('Moderator', true);
  await restricted.page.goto(origin + '/users/moderators');
  await restricted.page.locator('.p-datatable tbody tr').first().waitFor();
  assert.equal(await restricted.page.locator('app-user-password-cell').count(), 0, 'Staff must never see managed password column');
  await restricted.page.close();
  assert.deepEqual(errors, [], 'Browser runtime errors');
  console.log(JSON.stringify({ passed: true, checks: report, screenshots: results }, null, 2));
} finally {
  await browser.close();
  await new Promise(done => server.close(done));
}
