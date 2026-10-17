import assert from 'node:assert/strict';
import { chromium, expect } from '@playwright/test';
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
const root=fileURLToPath(new URL('../../',import.meta.url));
const state=JSON.parse(readFileSync(resolve(root,'.audit/standby-phase2/isolated-private.json'),'utf8'));
assert.match(state.connection,/(?:Database|Initial Catalog)=AlFalahCoverageTests_[a-f0-9]{32}(?:;|$)/i);
const output=resolve(root,'docs/specs/intelligent-timetable/verification/standby-phase2');mkdirSync(output,{recursive:true});
async function api(path,{user='manager',...opts}={}){return fetch('http://localhost:5268/api/v1/'+path,{...opts,headers:{Authorization:'Bearer '+state.sessions[user].token,'Content-Type':'application/json',...opts.headers}});}
async function data(path,opts){const r=await api(path,opts),body=await r.json();assert.equal(r.status,200,JSON.stringify(body));assert.equal(body.isSuccess,true,JSON.stringify(body));return body.data;}
const prefix='timetable-coverage/';
let b=await data(prefix+'reconcile?date='+state.date,{method:'POST'});assert.equal(b.needs.length,2);
assert.equal((await api(prefix+'board?date='+state.date,{user:'teacher2'})).status,403);
assert.equal((await api(prefix+'tasks/'+b.needs[0].id,{user:'teacher1'})).status,403);
const browser=await chromium.launch({headless:true}),results=[];
async function capture(page,name){await expect(page.locator('.p-toast-message')).toHaveCount(0,{timeout:15000});await page.waitForTimeout(350);await page.screenshot({path:resolve(output,name),fullPage:true});}
async function open(user,width,url){
 const context=await browser.newContext({viewport:{width,height:1080},reducedMotion:'reduce'});
 await context.addInitScript(s=>{sessionStorage.setItem('alfalah_access_token',s.token);sessionStorage.setItem('alfalah_user',JSON.stringify(s.user));},state.sessions[user]);
 await context.route('**/api/**',async route=>{const req=route.request(),url=new URL(req.url());const response=await api(url.pathname.replace('/api/v1/','')+url.search,{user,method:req.method(),body:req.postData()||undefined});return route.fulfill({status:response.status,headers:{'content-type':response.headers.get('content-type')||'application/json'},body:Buffer.from(await response.arrayBuffer())});});
 const page=await context.newPage();page.on('pageerror',e=>results.push({runtimeError:e.message}));
 await page.goto('http://localhost:4200'+url,{waitUntil:'domcontentloaded',timeout:60000});return {context,page};
}
try{
 for(const width of [390,768,1440,1920]){
  console.log('Manager viewport',width);const {context,page}=await open('manager',width,'/intelligent-timetable/substitutions');
  await expect(page.locator('.coverage-ledger')).toBeVisible({timeout:60000});
  await page.locator('.coverage-ledger input[type=date]').fill(state.date);await page.locator('.coverage-ledger input[type=date]').dispatchEvent('change');
  await expect(page.locator('.ledger-table tbody tr')).toHaveCount(2);await expect(page.locator('.coverage-ledger .message.error')).toHaveCount(0);
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),true);
  await capture(page,'manager-'+width+'.png');
  if(width===1440){
   await page.locator('.row-actions button').first().click();await expect(page.locator('.option-list article')).toBeVisible();
   await capture(page,'candidates-1440.png');
   await page.locator('.option-list button:enabled').first().click();
   await page.locator('.allocation-bar .primary').click();await expect(page.locator('.review-list article')).toHaveCount(2);
   await capture(page,'review-1440.png');
   const reason=page.locator('.coverage-dialog textarea');if(await reason.count())await reason.fill('Approved synthetic live verification');
   await page.locator('.coverage-dialog footer .primary').click();await expect(page.locator('.coverage-ledger .message.success')).toBeVisible();
   await expect(page.locator('.coverage-ledger .message.error')).toHaveCount(0);
  }
  results.push({width,manager:true,pageOverflow:false});await context.close();
 }
 b=await data(prefix+'board?date='+state.date);assert.equal(b.needs.every(n=>n.substitutionId!=null),true);
 const future=b.needs[0],history=(await data(prefix+'my-tasks',{user:'teacher2'})).items;
 const unfinished=history.find(n=>n.date===state.pastDate&&!n.submittedAt);assert.ok(unfinished);
 for(const width of [390,768,1440,1920]){
  const {context,page}=await open('teacher2',width,'/standby-tasks?needId='+future.id);
  await expect(page.locator('.task-detail')).toBeVisible({timeout:60000});await expect(page.locator('.coverage-tasks .message.error')).toHaveCount(0);
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),true);
  await capture(page,'teacher-'+width+'.png');
  if(width===1440){
   await page.locator('.receipt .primary').click();await expect(page.locator('.sign-review .signature-box img')).toBeVisible();
   await page.locator('.sign-review footer .primary').click();await expect(page.locator('.receipt')).toHaveCount(0);
   await expect(page.locator('.report-actions .primary')).toBeDisabled();
   await page.goto('http://localhost:4200/standby-tasks?needId='+unfinished.id);await expect(page.locator('.receipt')).toBeVisible();
   await page.locator('.receipt .primary').click();await page.locator('.sign-review footer .primary').click();await expect(page.locator('.receipt')).toHaveCount(0);
   await page.locator('.report-field textarea').first().fill('نفذنا مراجعة تفاعلية وتمارين قصيرة مع الطلاب. هذه بيانات اختبار معزولة.');
   await page.locator('.report-actions .secondary').click();await expect(page.locator('.coverage-tasks .message.success')).toBeVisible();
   await page.reload();await expect(page.locator('.report-field textarea').first()).toHaveValue(/مراجعة تفاعلية/);
   await page.locator('.report-actions .primary').click();await expect(page.locator('.review-narrative')).toContainText('مراجعة تفاعلية');
   await capture(page,'execution-review-1440.png');
   await page.locator('.sign-review footer .primary').click();await expect(page.locator('.task-detail .message.success')).toBeVisible();
   await page.locator('.history summary').click();await expect(page.locator('.history img')).toHaveCount(2);
   await capture(page,'executed-1440.png');
   await page.goto('http://localhost:4200/instructor/dashboard');await expect(page.locator('.coverage-dashboard')).toBeVisible({timeout:30000});
   await expect(page.locator('.coverage-dashboard .task-row').first()).toBeVisible();
  }
  results.push({width,teacher:true,pageOverflow:false});await context.close();
 }
 assert.equal(results.some(r=>r.runtimeError),false,JSON.stringify(results.filter(r=>r.runtimeError)));
 writeFileSync(resolve(output,'browser-results.json'),JSON.stringify({at:new Date().toISOString(),isolated:true,providerWrites:false,checks:['manager/teacher 390,768,1440,1920','manual choice + fair preview + approval','wrong owner HTTP 403','teacher board HTTP 403','future receipt + early execution disabled','persisted historical draft reload + signed execution','retained separate signatures','dashboard direct tasks'],results},null,2));
 console.log('Daily coverage live checks passed');
}finally{await browser.close();}
