import assert from 'node:assert/strict';
import {chromium,expect} from '@playwright/test';
import {readFileSync,writeFileSync,mkdirSync} from 'node:fs';
import {createHash} from 'node:crypto';
import {resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=fileURLToPath(new URL('../../',import.meta.url)),state=JSON.parse(readFileSync(resolve(root,'.audit/standby-phase3/isolated-private.json'),'utf8'));
assert.match(state.connection,/(?:Database|Initial Catalog)=AlFalahRegisterTests_[a-f0-9]{32}(?:;|$)/i);
const output=resolve(root,'docs/specs/intelligent-timetable/verification/standby-week');mkdirSync(output,{recursive:true});
const date=state.pastDate,hash=b=>createHash('sha256').update(b).digest('hex').toUpperCase();
async function api(path,{user='manager',...options}={}){return fetch('http://localhost:5270'+path,{...options,headers:{Authorization:'Bearer '+state.sessions[user].token,'Content-Type':'application/json',Origin:'http://localhost:4200',...options.headers}});}
async function data(path,options){const r=await api('/api/v1/timetable-coverage/register'+path,options);assert.equal(r.status,200);const b=await r.json();assert.equal(b.isSuccess,true);return b.data;}
const daily=await data('?date='+date),old=daily.history.items[0];assert.ok(old);const oldBytes=Buffer.from(await(await api('/api/v1/timetable-coverage/register/'+old.id+'/content')).arrayBuffer());assert.equal(hash(oldBytes),old.sha256);
const w=await data('?scope=Week&date='+date);assert.equal(w.scope,'Week');assert.equal(w.date,date);assert.equal(w.periodCount,daily.periodCount);assert.equal((await api('/api/v1/timetable-coverage/register?date='+date+'&scope=Month')).status,400);
assert.equal((await api('/api/v1/timetable-coverage/register?date='+date+'&scope=Week',{user:'teacher2'})).status,403);
assert.equal((await api('/api/v1/timetable-coverage/register',{method:'POST',body:JSON.stringify({date,scope:'Week',sourceRevision:'f'.repeat(64)})})).status,409);
const browser=await chromium.launch({headless:true}),results=[];let issuedWeekId=null;
try{
 for(const width of [390,768,1440,1920]){
  console.log('Weekly PDF viewport',width);const context=await browser.newContext({viewport:{width,height:1000},reducedMotion:'reduce',acceptDownloads:true});
  await context.addInitScript(s=>{sessionStorage.setItem('alfalah_access_token',s.token);sessionStorage.setItem('alfalah_user',JSON.stringify(s.user));},state.sessions.manager);
  let issues=0;const failures=[],errors=[];
  await context.route('**/api/**',async route=>{const req=route.request(),url=new URL(req.url());if(req.method()==='POST'&&url.pathname.endsWith('/timetable-coverage/register'))issues++;
   const r=await api(url.pathname+url.search,{method:req.method(),body:req.postData()||undefined,headers:{Accept:req.headers().accept||'application/json'}});if(r.status>=400)failures.push({path:url.pathname,status:r.status});const headers=Object.fromEntries(r.headers);delete headers['content-length'];delete headers['content-encoding'];await route.fulfill({status:r.status,headers,body:Buffer.from(await r.arrayBuffer())});
  });
  const page=await context.newPage();page.setDefaultTimeout(45000);page.on('pageerror',e=>errors.push(e.message));
  await page.goto('http://localhost:4200/intelligent-timetable/standby?tab=daily&register=true',{waitUntil:'domcontentloaded'});await expect(page.locator('.register-intro input')).toBeEnabled();
  const input=page.locator('.register-intro input');await input.fill(date);await input.dispatchEvent('change');await expect(page.locator('.revision-row').first()).toBeVisible();
  await page.locator('.register-scope button').nth(1).click();await expect(page.locator('.register-range')).toContainText(date);await expect(page.locator('.register-scope button').nth(1)).toBeEnabled();assert.equal(issues,0);
  await page.locator('.issue-button').click();await expect(page.locator('.pdf-pages canvas').first()).toBeVisible();await expect(page.locator('.register-message.error')).toHaveCount(0);
  const info=await page.locator('app-coverage-register').evaluate(e=>window.ng.getComponent(e).selected);assert.equal(info.scope,'Week');assert.equal(info.date,date);assert.notEqual(info.id,old.id);
  if(issuedWeekId===null)issuedWeekId=info.id;else assert.equal(info.id,issuedWeekId,'Repeated issue must replay the same retained weekly artifact');
  await expect(page.locator('.p-toast-message')).toHaveCount(0,{timeout:20000});await expect(page.locator('.preview-message')).toHaveCount(0);await page.waitForTimeout(350);assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1),false);
  const bounds=await page.locator('.register-intro').evaluate(e=>({scroll:e.scrollWidth,width:e.clientWidth}));assert.ok(bounds.scroll<=bounds.width+1,JSON.stringify(bounds));
  await page.screenshot({path:resolve(output,'week-'+width+'.png'),fullPage:true});
  const downloaded=page.waitForEvent('download');await page.locator('.register-footer .primary').click();const file=await downloaded;const bytes=readFileSync(await file.path());assert.equal(hash(bytes),info.sha256);if(width===1440)writeFileSync(resolve(output,'signed-week.pdf'),bytes);
  await page.locator('.register-scope button').first().click();await expect(page.locator('.register-range')).toHaveCount(0);await expect(page.locator('.pdf-pages canvas')).toHaveCount(0);await expect(page.locator('.revision-row').first()).toBeVisible();
  const dayInfo=await page.locator('app-coverage-register').evaluate(e=>window.ng.getComponent(e).context.history.items[0]);assert.equal(dayInfo.id,old.id);assert.equal(dayInfo.scope,'Day');
  assert.deepEqual(errors,[]);assert.ok(failures.every(f=>f.path.startsWith('/api/v1/storage/')&&f.status===403),JSON.stringify(failures));
  results.push({width,dayAndWeekChoices:true,explicitSevenDayRange:true,weeklyIssueAndPreview:true,separateHistory:true,exactDownloadHash:true,clearsObsoletePreview:true,pageOverflow:false,unrelatedStorageDenials:failures});await context.close();
 }
 const afterDay=await data('?date='+date),afterWeek=await data('?date='+date+'&scope=Week');assert.equal(afterDay.history.totalCount,daily.history.totalCount);assert.ok([w.history.totalCount,w.history.totalCount+1].includes(afterWeek.history.totalCount));assert.equal(afterWeek.history.items.filter(i=>i.id===issuedWeekId).length,1);
 assert.equal(hash(Buffer.from(await(await api('/api/v1/timetable-coverage/register/'+old.id+'/content')).arrayBuffer())),old.sha256);
 const info=afterWeek.history.items[0];assert.equal((await api('/api/v1/timetable-coverage/register/'+info.id+'/content',{user:'teacher2'})).status,403);
 writeFileSync(resolve(output,'browser-results.json'),JSON.stringify({at:new Date().toISOString(),isolated:true,providerWrites:false,oldDailyBytesUnchanged:true,weeklyReplayUnique:true,invalidScope400:true,staleSource409:true,teacher403:true,results},null,2));console.log('PASS: weekly retained PDF, exact preview/download, day compatibility and authorization.');
}finally{await browser.close();}
