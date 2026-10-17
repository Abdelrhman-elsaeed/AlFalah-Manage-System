import assert from 'node:assert/strict';
import {chromium,expect} from '@playwright/test';
import {readFileSync,writeFileSync,mkdirSync} from 'node:fs';
import {createHash} from 'node:crypto';
import {resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=fileURLToPath(new URL('../../',import.meta.url));
const state=JSON.parse(readFileSync(resolve(root,'.audit/standby-phase3/isolated-private.json'),'utf8'));
assert.match(state.connection,/(?:Database|Initial Catalog)=AlFalahRegisterTests_[a-f0-9]{32}(?:;|$)/i);
const output=resolve(root,'docs/specs/intelligent-timetable/verification/standby-publish');mkdirSync(output,{recursive:true});
async function api(path,{user='manager',...options}={}){return fetch('http://localhost:5270'+(path.startsWith('/api/')?path:'/api/v1/'+path),{...options,headers:{Authorization:'Bearer '+state.sessions[user].token,'Content-Type':'application/json',Origin:'http://localhost:4200',...options.headers}});}
async function data(path,options){const r=await api(path,options);assert.equal(r.status,200);const b=await r.json();assert.equal(b.isSuccess,true);return b.data;}
const current='timetables/current?academicYearId=1&semester=1&personalOnly=false';
const table=await data(current),endpoint=`timetables/${table.id}/standby`;
const teaching=table.entries.filter(e=>e.entryType==='Lesson'||e.entryType===1);
const hash=b=>createHash('sha256').update(b).digest('hex').toUpperCase();
const historyBefore=await data('timetable-coverage/register?date='+state.pastDate);assert.ok(historyBefore.history.totalCount>0);
const browser=await chromium.launch({headless:true}),results=[];
try{
 for(const width of [390,768,1440,1920]){
  console.log('Publication viewport',width);const context=await browser.newContext({viewport:{width,height:1080},reducedMotion:'reduce',acceptDownloads:true});
  await context.addInitScript(s=>{sessionStorage.setItem('alfalah_access_token',s.token);sessionStorage.setItem('alfalah_user',JSON.stringify(s.user));},state.sessions.manager);
  let applies=0,publishes=0,issued=0,failPublish=width===768;const order=[],errors=[],backgroundResponses=[];
  await context.route('**/api/**',async route=>{const req=route.request(),url=new URL(req.url());
   if(req.method()==='PUT'&&url.pathname.endsWith('/standby')){applies++;order.push('apply');}
   if(req.method()==='POST'&&url.pathname.endsWith('/publish')){publishes++;order.push('publish');if(failPublish){failPublish=false;return route.fulfill({status:503,contentType:'application/json',body:JSON.stringify({isSuccess:false,message:'Synthetic publication outage',errors:[]})});}}
   if(req.method()==='POST'&&url.pathname.endsWith('/timetable-coverage/register'))issued++;
   const r=await api(url.pathname+url.search,{method:req.method(),body:req.postData()||undefined,headers:{Accept:req.headers().accept||'application/json'}});if(r.status>=400)backgroundResponses.push({path:url.pathname,status:r.status});const headers=Object.fromEntries(r.headers);delete headers['content-length'];delete headers['content-encoding'];await route.fulfill({status:r.status,headers,body:Buffer.from(await r.arrayBuffer())});
  });
  const page=await context.newPage();page.setDefaultTimeout(45000);page.on('pageerror',e=>errors.push(e.message));
  await page.goto('http://localhost:4200/intelligent-timetable/standby',{waitUntil:'domcontentloaded'});await expect(page.locator('.standby')).toBeVisible();
  await expect(page.locator('.register-action')).toBeVisible();
  await page.locator('.save-actions .primary').click();await expect(page.locator('.standby-save-review')).toBeVisible();
  assert.equal(applies,0);assert.equal(publishes,0);await expect(page.locator('.save-and-publish')).toBeEnabled();await expect(page.locator('.save-draft')).toBeEnabled();
  await expect(page.locator('.p-toast-message')).toHaveCount(0,{timeout:20000});await page.waitForTimeout(350);
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1),false);
  await page.screenshot({path:resolve(output,'review-'+width+'.png'),fullPage:true});
  if(width===390){
   await page.locator('.save-draft').click();await expect(page.locator('.standby .message.success')).toBeVisible();assert.equal(publishes,0);assert.equal((await data(endpoint)).isPublished,false);
   await expect(page.locator('.publish-saved')).toBeEnabled();await page.locator('.publish-saved').click();
  }else{
   await page.locator('.save-and-publish').click();
   if(width===768){await expect(page.locator('.standby .message.error')).toBeVisible();assert.equal((await data(endpoint)).isPublished,false);assert.equal(applies,1);await page.locator('.publish-saved').click();}
  }
  await expect(page.locator('.standby .message.success')).toBeVisible();await expect(page.locator('.publish-saved')).toHaveCount(0);assert.equal(applies,1);
  assert.deepEqual(order,width===768?['apply','publish','publish']:['apply','publish']);
  assert.equal((await data(endpoint)).isPublished,true);const teacher=await data(current,{user:'teacher2'});assert.ok(teacher.isPublished);assert.ok(teacher.entries.some(e=>e.entryType==='Standby'||e.entryType===2));
  assert.deepEqual((await data(current)).entries.filter(e=>e.entryType==='Lesson'||e.entryType===1),teaching);
  await expect(page.locator('.p-toast-message')).toHaveCount(0,{timeout:20000});await page.locator('.hub-header').scrollIntoViewIfNeeded();await page.waitForTimeout(350);await page.screenshot({path:resolve(output,'published-'+width+'.png'),fullPage:true});
  await page.locator('.register-action').click();await expect(page.locator('.register-workspace')).toBeVisible();
  const registerDate=page.locator('.register-intro input[type=date]');await expect(registerDate).toBeEnabled();await registerDate.fill(state.pastDate);await registerDate.dispatchEvent('change');
  await expect(page.locator('.revision-row').first()).toBeVisible();await page.locator('.revision-row').first().click();await expect(page.locator('.pdf-pages canvas').first()).toBeVisible();
  await expect(page.locator('.register-message.error')).toHaveCount(0);await expect(page.locator('.preview-message')).toHaveCount(0);await page.waitForTimeout(350);await page.screenshot({path:resolve(output,'register-'+width+'.png'),fullPage:true});
  const displayed=await page.locator('app-coverage-register').evaluate(e=>window.ng.getComponent(e).selected.sha256);
  const download=page.waitForEvent('download');await page.locator('.register-footer .primary').click();const file=await download;assert.equal(hash(readFileSync(await file.path())),displayed.toUpperCase());assert.equal(issued,0);
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1),false);assert.deepEqual(errors,[]);
  assert.ok(backgroundResponses.every(r=>r.path.startsWith('/api/v1/storage/')&&r.status===403),JSON.stringify(backgroundResponses));
  results.push({width,explicitDraftOption:width===390,combinedSavePublish:width!==390,publicationRecoveryWithoutSecondApply:width===768,noWritesBeforeConfirmation:true,teacherSeesPublishedStandby:true,teachingRowsUnchanged:true,pdfShortcutOpensExistingRegister:true,retainedPreviewDownloadHash:true,noRegisterIssueOnView:true,pageOverflow:false,unrelatedFixtureStorageDenials:backgroundResponses});await context.close();
 }
 assert.equal((await data('timetable-coverage/register?date='+state.pastDate)).history.totalCount,historyBefore.history.totalCount);
 writeFileSync(resolve(output,'browser-results.json'),JSON.stringify({at:new Date().toISOString(),isolated:true,providerWrites:false,results},null,2));console.log('PASS: draft/publication, teacher visibility, failed publication recovery and PDF shortcut/download.');
}finally{await browser.close();}
