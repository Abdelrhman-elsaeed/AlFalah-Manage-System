import assert from 'node:assert/strict';
import {chromium,expect} from '@playwright/test';
import {readFileSync,writeFileSync,mkdirSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
import {resolve} from 'node:path';
const root=fileURLToPath(new URL('../../',import.meta.url));
const state=JSON.parse(readFileSync(resolve(root,'.audit/standby-phase3/isolated-private.json'),'utf8'));
assert.match(state.connection,/(?:Database|Initial Catalog)=AlFalahRegisterTests_[a-f0-9]{32}(?:;|$)/i);
const output=resolve(root,'docs/specs/intelligent-timetable/verification/standby-undo');mkdirSync(output,{recursive:true});
async function api(path,options={}){return fetch('http://localhost:5270/api/v1/'+path,{...options,headers:{Authorization:'Bearer '+state.sessions.manager.token,'Content-Type':'application/json',Origin:'http://localhost:4200',...options.headers}});}
async function data(path){const r=await api(path);assert.equal(r.status,200);const b=await r.json();assert.equal(b.isSuccess,true);return b.data;}
const table=await data('timetables/current?academicYearId=1&semester=1&personalOnly=false'),endpoint=`timetables/${table.id}/standby`;
const baseline=await data(endpoint);assert.ok(baseline.ready&&baseline.placements.length>=2,'Run against the prepared synthetic standby fixture');
const stored=JSON.stringify({revision:baseline.revision,settings:baseline.settings,placements:baseline.placements});
const browser=await chromium.launch({headless:true}),results=[];
try{
 for(const width of [390,768,1440,1920]){
  console.log('Undo viewport',width);const context=await browser.newContext({viewport:{width,height:1080},reducedMotion:'reduce'});
  await context.addInitScript(s=>{sessionStorage.setItem('alfalah_access_token',s.token);sessionStorage.setItem('alfalah_user',JSON.stringify(s.user));},state.sessions.manager);
  let writes=0;await context.route('**/api/**',async route=>{const req=route.request(),url=new URL(req.url());if(['PUT','PATCH','DELETE'].includes(req.method())){writes++;throw Error('Undo must not write to the API');}
   const r=await api(url.pathname.replace('/api/v1/','')+url.search,{method:req.method(),body:req.postData()||undefined,headers:{Accept:req.headers().accept||'application/json'}});const headers=Object.fromEntries(r.headers);delete headers['content-length'];delete headers['content-encoding'];await route.fulfill({status:r.status,headers,body:Buffer.from(await r.arrayBuffer())});
  });
  const page=await context.newPage();page.setDefaultTimeout(45000);const errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.goto('http://localhost:4200/intelligent-timetable/standby',{waitUntil:'domcontentloaded'});await expect(page.locator('.standby')).toBeVisible();
  const snapshot=()=>page.locator('app-weekly-standby').evaluate(e=>{const p=window.ng.getComponent(e);return JSON.stringify({settings:p.view.settings,placements:p.view.placements});});
  const original=await snapshot();await expect(page.locator('.planner-tools .undo-action')).toBeDisabled();
  await page.locator('.mode-picker button').nth(1).click();await page.locator('.cell.selected .cell-button:enabled').first().click();
  const manual=await snapshot(),count=await page.locator('.cell.selected').count();assert.notEqual(manual,original);
  await page.locator('.mode-picker button').first().click();await page.locator('.save-actions .primary').click();await expect(page.locator('.standby-save-review')).toBeVisible();await expect(page.locator('.review-actions .undo-review')).toBeEnabled();
  assert.ok(await page.locator('.cell.selected').count()>count);await expect(page.locator('.review-undo-hint')).toBeVisible();
  await expect(page.locator('.p-toast-message')).toHaveCount(0,{timeout:20000});await page.waitForTimeout(350);await page.screenshot({path:resolve(output,'review-'+width+'.png'),fullPage:true});
  await page.locator('.review-actions .undo-review').click();await expect(page.locator('.standby-save-review')).toHaveCount(0);assert.equal(await snapshot(),manual);assert.equal(writes,0);
  await expect(page.locator('.save-actions .undo-action')).toBeEnabled();await page.locator('.save-actions .undo-action').click();assert.equal(await snapshot(),original);await expect(page.locator('.planner-tools .undo-action')).toBeDisabled();
  assert.equal(await page.evaluate(()=>{const e=new Event('beforeunload',{cancelable:true});window.dispatchEvent(e);return e.defaultPrevented;}),false);
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1),false);assert.deepEqual(errors,[]);
  results.push({width,fairProposalChanged:true,reviewUndoRestoredExactManualState:true,secondUndoRestoredLoadedState:true,settingsAndLocksRestored:true,noApplyRequests:true,cleanUnload:true,pageOverflow:false});await context.close();
 }
 const after=await data(endpoint);assert.equal(JSON.stringify({revision:after.revision,settings:after.settings,placements:after.placements}),stored);
 writeFileSync(resolve(output,'browser-results.json'),JSON.stringify({at:new Date().toISOString(),isolated:true,storedRevisionUnchanged:true,providerWrites:false,results},null,2));console.log('PASS: visible fair-review undo, stepwise exact restoration and unchanged stored data.');
}finally{await browser.close();}
