import assert from 'node:assert/strict';
import {chromium,expect} from '@playwright/test';
import {readFileSync,writeFileSync,mkdirSync} from 'node:fs';
import {resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=fileURLToPath(new URL('../../',import.meta.url));
const state=JSON.parse(readFileSync(resolve(root,'.audit/standby-phase3/isolated-private.json'),'utf8'));
assert.match(state.connection,/(?:Database|Initial Catalog)=AlFalahRegisterTests_[a-f0-9]{32}(?:;|$)/i);
const output=resolve(root,'docs/specs/intelligent-timetable/verification/standby-save-navigation');mkdirSync(output,{recursive:true});
async function api(path,options={}){return fetch('http://localhost:5270/api/v1/'+path,{...options,headers:{Authorization:'Bearer '+state.sessions.manager.token,'Content-Type':'application/json',Origin:'http://localhost:4200',...options.headers}});}
async function data(path,options){const r=await api(path,options),b=await r.json();assert.equal(r.status,200,JSON.stringify(b));assert.equal(b.isSuccess,true);return b.data;}
const table=await data('timetables/current?academicYearId=1&semester=1&personalOnly=false');assert.ok(table?.id);
const endpoint=`timetables/${table.id}/standby`,teaching=JSON.stringify(table.entries.filter(e=>e.entryType===1||e.entryType==='Lesson'));
let wb=await data(endpoint);assert.equal(wb.ready,true);
// Only the guarded synthetic fixture is reset to the owner's empty-allocation scenario.
const request={revision:wb.revision,mode:'Manual',settings:{requiredPerPeriod:1,overrides:[],excludedTeacherIds:[]},placements:[]};
const proposal=await data(endpoint+'/preview',{method:'POST',body:JSON.stringify(request)});
await data(endpoint,{method:'PUT',body:JSON.stringify({...request,proposalId:proposal.proposalId})});
const browser=await chromium.launch({headless:true}),results=[];
try{
 for(const width of [390,768,1440,1920]){
  console.log('Standby save viewport',width);const context=await browser.newContext({viewport:{width,height:1080},reducedMotion:'reduce'});
  await context.addInitScript(s=>{sessionStorage.setItem('alfalah_access_token',s.token);sessionStorage.setItem('alfalah_user',JSON.stringify(s.user));},state.sessions.manager);
  let applies=0;const errors=[];
  await context.route('**/api/**',async route=>{const req=route.request(),url=new URL(req.url());if(req.method()==='PUT'&&url.pathname.endsWith('/standby'))applies++;
   const response=await api(url.pathname.replace('/api/v1/','')+url.search,{method:req.method(),body:req.postData()||undefined,headers:{Accept:req.headers().accept||'application/json'}});
   const headers=Object.fromEntries(response.headers);delete headers['content-length'];delete headers['content-encoding'];await route.fulfill({status:response.status,headers,body:Buffer.from(await response.arrayBuffer())});
  });
  const page=await context.newPage();page.setDefaultTimeout(40000);page.on('pageerror',e=>errors.push(e.message));
  await page.goto('http://localhost:4200/intelligent-timetable/standby',{waitUntil:'domcontentloaded',timeout:60000});await expect(page.locator('app-weekly-standby')).toBeVisible();await expect(page.locator('.hub-header h1')).toHaveText('حصص الانتظار');
  await expect(page.locator('.standby-workspace-tabs')).toHaveCount(0);await expect(page.locator('a[href="/intelligent-timetable/standby"]')).toHaveCount(1);
  await page.locator('.mode-picker button').nth(1).click();await expect(page.locator('.planning-status')).toBeVisible();await expect(page.locator('.standby .message.warning')).toHaveCount(0);
  const primary=page.locator('.save-actions .primary');await expect(primary).toBeEnabled();await expect(primary).toHaveText(/مراجعة وحفظ/);
  await page.locator('.cell:not(.selected) .cell-button:enabled').first().click();
  await primary.click();await expect(page.locator('.standby-save-review')).toBeVisible();assert.equal(applies,0,'Review must not save');
  await expect(page.locator('.review-actions .primary')).toBeEnabled();
  if(width===390){const widths=await page.locator('.review-actions').evaluate(e=>{const footer=e.parentElement,style=getComputedStyle(footer);return{container:e.clientWidth,button:e.querySelector('.primary').clientWidth,available:footer.clientWidth-parseFloat(style.paddingLeft)-parseFloat(style.paddingRight)};});assert.ok(widths.container>=widths.available*.95&&widths.button>=widths.container*.95,JSON.stringify(widths));}
  await expect(page.locator('.p-toast-message')).toHaveCount(0,{timeout:20000});await page.waitForTimeout(350);await page.screenshot({path:resolve(output,'review-'+width+'.png'),fullPage:true});
  await page.locator('.review-actions .continue-editing').click();await expect(page.locator('.standby-save-review')).toHaveCount(0);assert.equal(applies,0);
  if(width===1920){await page.locator('.mode-picker button').first().click();await primary.click();await expect(page.locator('.standby-save-review')).toBeVisible();assert.equal(applies,0);}
  else{await primary.click();await expect(page.locator('.standby-save-review')).toBeVisible();}
  if(width===1920){
   const current=await data(endpoint),advance={revision:current.revision,mode:'Manual',settings:current.settings,placements:current.placements};
   const concurrent=await data(endpoint+'/preview',{method:'POST',body:JSON.stringify(advance)});await data(endpoint,{method:'PUT',body:JSON.stringify({...advance,proposalId:concurrent.proposalId})});
   await page.locator('.review-actions .save-draft').click();await expect(page.locator('.standby .message.error')).toBeVisible();await expect(page.locator('.standby-save-review')).toHaveCount(0);await expect(primary).toBeEnabled();
   page.once('dialog',d=>d.accept());await page.locator('.overview-reload').click();await expect(page.locator('.standby .message.error')).toHaveCount(0);
   await primary.click();await expect(page.locator('.standby-save-review')).toBeVisible();
  }
  if(width>=1440){await page.locator('.review-actions .save-draft').click();await expect(page.locator('.standby .message.success')).toBeVisible();await expect(page.locator('.standby-save-review')).toHaveCount(0);assert.equal(applies,width===1920?2:1);wb=await data(endpoint);assert.ok(wb.placements.length>0);assert.equal(wb.isPublished,false);}
  else{await page.locator('.review-actions .continue-editing').click();}
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),true);
  await page.locator('.planner').scrollIntoViewIfNeeded();await page.screenshot({path:resolve(output,'workspace-'+width+'.png'),fullPage:true});
  const back=page.locator('.hub-actions a[href="/timetable"]');
  if(width<1440){page.once('dialog',d=>d.dismiss());await back.click();await expect(page).toHaveURL(/intelligent-timetable\/standby$/);page.once('dialog',d=>d.accept());}
  await back.click();await expect(page.locator('.schedule-grid')).toBeVisible();await expect(page.locator('app-weekly-standby')).toHaveCount(0);
  assert.equal(errors.length,0,JSON.stringify(errors));results.push({width,standaloneRoute:true,neutralUnallocatedGuidance:true,actionableReview:true,noWriteBeforeConfirmation:true,confirmedSave:width>=1440,mode:width===1920?'Fair':'Manual',dirtyNavigation:width<1440,staleSaveRecovery:width===1920,teachingRouteUnchanged:true,pageOverflow:false});await context.close();
 }
 const after=await data('timetables/'+table.id+'?personalOnly=false');assert.equal(JSON.stringify(after.entries.filter(e=>e.entryType===1||e.entryType==='Lesson')),teaching);
 writeFileSync(resolve(output,'browser-results.json'),JSON.stringify({at:new Date().toISOString(),isolated:true,providerWrites:false,teachingRowsUnchanged:true,results},null,2));console.log('PASS: standalone navigation, manual/fair reviewed saves, no premature apply and unchanged teaching.');
}finally{await browser.close();}
