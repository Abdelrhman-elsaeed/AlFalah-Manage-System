// Browser smoke check against the built application bundle, using isolated API fixtures.
// No live account, database reset, or production writes are involved.
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
const types = { '.html': 'text/html', '.js': 'application/javascript', '.css': 'text/css', '.svg': 'image/svg+xml', '.png': 'image/png', '.woff2': 'font/woff2', '.json': 'application/json' };
const server = createServer((req, res) => {
  const relative = decodeURIComponent(new URL(req.url, 'http://localhost').pathname).replace(/^\/+/, '');
  let file = resolve(dist, relative || 'index.html');
  if (!file.startsWith(dist)) { res.writeHead(403); res.end(); return; }
  if (!existsSync(file)) file = resolve(dist, 'index.html');
  res.writeHead(200, { 'Content-Type': types[extname(file)] || 'application/octet-stream' });
  res.end(readFileSync(file));
});
await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
const origin = `http://127.0.0.1:${server.address().port}`;
const browser = await chromium.launch({ headless: true });
const page = await browser.newPage({ viewport: { width: 1440, height: 1000 }, reducedMotion: 'reduce' });
const errors = [];
page.on('pageerror', error => errors.push(error.message));
const user = { userId: 'manager-preview', username: 'manager-preview', fullName: 'مدير المدرسة',
  activeSchoolId: 1, activeSchoolName: 'مدارس الفلاح – الابتدائية بجدة', preferredLanguage: 'ar',
  roles: ['SchoolManager'], permissions: ['Dashboard.SchoolManager', 'Timetable.View', 'Visit.View', 'StudentAffairsDashboard.SchoolOversight', 'User.View', 'Instructor.View', 'Attendance.View'] };
await page.addInitScript(user => {
  sessionStorage.setItem('alfalah_access_token', `e30.${btoa(JSON.stringify({ sub: user.userId, exp: 2000000000 }))}.mock`);
  sessionStorage.setItem('alfalah_user', JSON.stringify(user));
}, user);
await page.clock.install({ time: new Date('2026-10-12T05:35:00Z') });
const counts = (total, present, absent, excused) => ({ total, present, absent, excused, recorded: present + absent + excused,
  unrecorded: total - present - absent - excused, attendanceRate: +(100 * present / (present + absent + excused)).toFixed(1),
  recordingRate: +(100 * (present + absent + excused) / total).toFixed(1) });
const plans = { totalActive: 8, totalCompleted: 12, totalCancelled: 0, totalFollowUps: 23, plansWithAtLeastOneFollowUp: 7, averageLatestProgressScore: 3.2 };
const dashboard = { schoolId: 1, schoolName: user.activeSchoolName, instructorsCount: 64, moderatorsCount: 4,
  visitsThisMonthCount: 18, instructorsNeedingImprovementCount: 2, evaluationsPendingApprovalCount: 3,
  complaintsCount: 5, openComplaintsCount: 2, visitsByStatus: [{ status: 1, statusLabelAr: 'مسودة', count: 2 }, { status: 3, statusLabelAr: 'بانتظار الاعتماد', count: 3 }, { status: 4, statusLabelAr: 'معتمدة', count: 13 }],
  subjectPerformance: [{ subject: 'الرياضيات', visitsCount: 8, approvedVisitsCount: 6, averageOverallScore: 3.2 }, { subject: 'اللغة العربية', visitsCount: 6, approvedVisitsCount: 4, averageOverallScore: 3.5 }],
  moderatorPerformance: [], instructorsNeedingImprovement: [], improvementPlans: plans, appliedFilters: {} };
const trend = Array.from({ length: 12 }, (_, i) => ({ date: `2026-10-${String(i + 1).padStart(2, '0')}`, present: 92 + (i % 3), recorded: 100 }));
const operations = { generatedAt: '2026-10-12T05:35:00Z', date: '2026-10-12', timeZone: 'Asia/Riyadh',
  students: { counts: counts(720, 658, 38, 12), classrooms: Array.from({ length: 12 }, (_, i) => ({ classroomId: i + 1,
    label: `${['الأول', 'الثاني', 'الثالث', 'الرابع', 'الخامس', 'السادس'][Math.floor(i / 2)]} ${i % 2 ? 'ب' : 'أ'}`, total: 60, present: i < 10 ? 55 : 54, absent: i < 10 ? 3 : 4, excused: 1 })), trend },
  staff: { counts: counts(64, 55, 4, 3), absences: [
    { name: 'أ. فهد العتيبي', role: 'Instructor', status: 2 }, { name: 'أ. سلمان القحطاني', role: 'Instructor', status: 2 },
    { name: 'أ. ماجد الحربي', role: 'Instructor', status: 2 }, { name: 'أ. نايف الشمري', role: 'Instructor', status: 2 },
    { name: 'أ. عبدالله الدوسري', role: 'Instructor', status: 3 },
    { name: 'أ. عمر الجهني', role: 'Instructor', status: 3 }, { name: 'أ. ناصر السبيعي', role: 'Moderator', status: 3 }], trend },
  summons: { pending: 6, attended: 9, underObservation: 4, improved: 7, appointments: [
    { id: 1, studentName: 'عبدالرحمن محمد', scheduledAt: '2026-10-12T06:30:00Z', priority: 3 },
    { id: 2, studentName: 'ريان سعود', scheduledAt: '2026-10-12T07:45:00Z', priority: 2 },
    { id: 3, studentName: 'مؤيد خالد', scheduledAt: '2026-10-12T09:15:00Z', priority: 1 }] },
  schoolDay: { name: 'الجدول الأسبوعي المنشور', timeZone: 'Asia/Riyadh', slots: [
    { label: 'الحصة الأولى', kind: 'Lesson', startsAt: '07:00:00', endsAt: '07:45:00' },
    { label: 'الحصة الثانية', kind: 'Lesson', startsAt: '07:45:00', endsAt: '08:30:00' },
    { label: 'الحصة الثالثة', kind: 'Lesson', startsAt: '08:30:00', endsAt: '09:15:00' },
    { label: 'الفسحة', kind: 'Break', startsAt: '09:15:00', endsAt: '09:40:00' },
    { label: 'الحصة الرابعة', kind: 'Lesson', startsAt: '09:40:00', endsAt: '10:25:00' },
    { label: 'الحصة الخامسة', kind: 'Lesson', startsAt: '10:25:00', endsAt: '11:10:00' }] },
  activities: [
    { category: 'visits', title: 'تحديث زيارة أ. ماجد الحربي', at: '2026-10-12T05:30:00Z', route: '/visits' },
    { category: 'students', title: 'تحديث رصد حضور الطلاب', at: '2026-10-12T05:22:00Z', route: '/student-affairs/oversight' },
    { category: 'summons', title: 'تحديث سجل استدعاءات أولياء الأمور', at: '2026-10-12T04:58:00Z', route: '/student-affairs/oversight' },
    { category: 'staff', title: 'تحديث سجل حضور الموظفين', at: '2026-10-12T04:40:00Z', route: '/attendance' },
    { category: 'timetable', title: 'نشر الجدول المدرسي', at: '2026-10-12T04:15:00Z', route: '/timetable' }],
  recentVisits: [{ id: 1, teacher: 'أ. ماجد الحربي', evaluator: 'أ. ناصر السبيعي', subject: 'اللغة العربية', date: '2026-10-12T05:00:00Z', status: 3, score: null }], gatePasses: 7, conversations: 12 };
let failOperations = false;
let operationsRequests = 0;
await page.route('**/api/**', async route => {
  const path = new URL(route.request().url()).pathname;
  let data = {};
  if (path.endsWith('/auth/me')) data = user;
  else if (path.endsWith('/auth/schools')) data = [];
  else if (path.endsWith('/dashboard/school-manager/operations')) {
    operationsRequests++;
    if (failOperations) return route.fulfill({ status: 503, json: { isSuccess: false, message: 'Unavailable' } });
    data = operations;
  } else if (path.endsWith('/dashboard/school-manager')) data = dashboard;
  else if (path.endsWith('/teachers')) data = { items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0, hasPrevious: false, hasNext: false };
  else if (path.endsWith('/storage/access')) return route.fulfill({ status: 403, json: { isSuccess: false } });
  else if (path.includes('capabilities')) data = { canAccess: false };
  await route.fulfill({ json: { isSuccess: true, data } });
});
try {
  await page.goto(origin + '/school-manager/dashboard');
  await page.locator('.manager-tools .manager-tool').first().waitFor();
  await page.locator('.appointment-list>div').first().waitFor();
  await page.evaluate(() => document.fonts.ready);
  const geometry = await page.evaluate(() => {
    const rect = selector => { const r = document.querySelector(selector).getBoundingClientRect(); return { x: r.x, y: r.y, width: r.width, height: r.height }; };
    return { hero: rect('.pulse-hero'), attendance: [...document.querySelectorAll('.attendance-row>.overview-panel')].map(el => {
      const r = el.getBoundingClientRect(); return { y: r.y, width: r.width, height: r.height }; }),
      columns: [...document.querySelectorAll('.school-pulse-row>.overview-panel')].map(el => {
        const r = el.getBoundingClientRect(); return { y: r.y, width: r.width, height: r.height }; }),
      overflow: document.documentElement.scrollWidth > innerWidth,
      artwork: document.querySelector('.pulse-art img').naturalWidth, heroHeadingColor: getComputedStyle(document.querySelector('.pulse-copy h2')).color };
  });
  assert(geometry.hero.height < 310, 'Desktop hero is too tall');
  assert.equal(geometry.heroHeadingColor, 'rgb(255, 255, 255)', 'Hero title must remain readable');
  assert.equal(geometry.attendance[0].y, geometry.attendance[1].y);
  assert(Math.abs(geometry.attendance[0].width - geometry.attendance[1].width) < 1);
  assert.equal(geometry.attendance[0].height, geometry.attendance[1].height);
  assert(geometry.columns.every(r => Math.abs(r.y - geometry.columns[0].y) < 1));
  assert(geometry.columns.every(r => Math.abs(r.width - geometry.columns[0].width) < 1));
  assert(geometry.columns.every(r => Math.abs(r.height - geometry.columns[0].height) < 1));
  assert(!geometry.overflow, 'Desktop has horizontal overflow');
  assert(geometry.artwork > 0, 'Original Figma artwork is missing');
  assert.equal(await page.locator('.manager-tool').count(), 10);
  assert.equal(await page.locator('.sm-analytics-grid, .manager-analytics-card, .manager-ranking-card').count(), 0, 'Removed manager analytics must stay absent');
  assert.equal(operationsRequests, 1, 'Overview must use one operations request');
  assert.match(await page.locator('.current-period').innerText(), /الحصة الثالثة/);
  await page.screenshot({ path: resolve(results, 'manager-dashboard-desktop.png') });
  await page.locator('.attendance-row').screenshot({ path: resolve(results, 'manager-dashboard-attendance.png') });
  await page.locator('.school-pulse-row').screenshot({ path: resolve(results, 'manager-dashboard-operations.png') });
  await page.locator('.more-tools').click();
  assert.equal(await page.locator('.operation-tools button').count(), 4);
  failOperations = true;
  await page.locator('.snapshot-note button').click();
  await page.locator('.overview-status').waitFor();
  assert.match(await page.locator('.overview-status').innerText(), /تعذر تحديث/);
  assert.equal(await page.locator('.appointment-list>div').count(), 3, 'Refresh failures must retain loaded data');
  failOperations = false;
  await page.locator('.overview-status button').click();
  await page.locator('.overview-status').waitFor({ state: 'detached' });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.reload();
  await page.locator('.appointment-list>div').first().waitFor();
  await page.evaluate(() => document.fonts.ready);
  assert(await page.locator('.shell-sidebar').evaluate(el => el.classList.contains('is-collapsed')), 'Mobile navigation should start collapsed');
  assert(await page.locator('.school-overview').evaluate(el => el.scrollWidth <= el.clientWidth + 1), 'Overview content has mobile overflow');
  await page.screenshot({ path: resolve(results, 'manager-dashboard-mobile.png'), fullPage: true });
  assert(await page.locator('.shell-content').evaluate(el => el.scrollWidth <= el.clientWidth + 1), 'Shell content has mobile overflow');
  assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false, 'Mobile has horizontal overflow');
  await page.screenshot({ path: resolve(results, 'manager-dashboard-mobile.png'), fullPage: true });
  await page.locator('.manager-tool').filter({ hasText: 'الكادر التعليمي' }).click();
  await page.waitForURL('**/teachers');
  assert.deepEqual(errors, [], 'Browser runtime errors');
  console.log(JSON.stringify({ passed: true, geometry, operationsRequests, screenshots: results }, null, 2));
} finally {
  await browser.close();
  await new Promise(resolve => server.close(resolve));
}
