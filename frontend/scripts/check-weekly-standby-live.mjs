import assert from 'node:assert/strict';
import { chromium, expect } from '@playwright/test';
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
const root=fileURLToPath(new URL('../../',import.meta.url));
const privateState=JSON.parse(readFileSync(resolve(root,'.audit/standby-discovery/isolated-private.json'),'utf8'));
assert.match(privateState.connection,/(?:Database|Initial Catalog)=AlFalahStandbyTests_[a-f0-9]{32}(?:;|$)/i);
const session=JSON.parse(readFileSync(resolve(root,'.audit/standby-discovery/manager-session-private.json'),'utf8'));
const output=resolve(root,'docs/specs/intelligent-timetable/verification/standby-phase1');mkdirSync(output,{recursive:true});
const endpoint=`/api/v1/timetables/${privateState.timetableId}/standby`;
async function api(path,options={}) {return fetch('http://localhost:5267'+path,{...options,headers:{Authorization:'Bearer '+privateState.managerToken,'Content-Type':'application/json',...options.headers}});}
console.log('Checking isolated API');
let workbench=await (await api(endpoint)).json();assert.equal(workbench.isSuccess,true);assert.equal(workbench.data.ready,true);
assert.equal((await api(endpoint,{headers:{Authorization:'Bearer '+privateState.teacherToken}})).status,403);
assert.equal((await api('/api/v1/timetables/999999/standby')).status,404);
assert.equal((await api(endpoint+'/preview',{method:'POST',body:JSON.stringify({revision:0,mode:'Fair',settings:workbench.data.settings,placements:workbench.data.placements})})).status,409);
const browser=await chromium.launch({headless:true}),results=[];
try {
 for(const width of [390,768,1440,1920]) {
  console.log('Viewport',width);
  const context=await browser.newContext({viewport:{width,height:1080},reducedMotion:'reduce'});
  await context.addInitScript(s=>{sessionStorage.setItem('alfalah_access_token',s.token);sessionStorage.setItem('alfalah_user',JSON.stringify(s.user));},session);
  const page=await context.newPage(),errors=[],writes=[];page.on('pageerror',e=>errors.push(e.message));
  await context.route('**/api/v1/timetables**',async route=>{
   const request=route.request(),url=new URL(request.url());
   if(['POST','PUT','DELETE','PATCH'].includes(request.method()))writes.push({method:request.method(),path:url.pathname});
   const response=await api(url.pathname+url.search,{method:request.method(),body:request.postData()||undefined});
   return route.fulfill({status:response.status,headers:{'content-type':response.headers.get('content-type')||'application/json'},body:Buffer.from(await response.arrayBuffer())});
  });
  console.log('Loading authenticated shell');
  await page.goto('http://localhost:4200/timetable',{waitUntil:'domcontentloaded',timeout:60000});
  console.log('Shell loaded');
  await expect(page.locator('.standby-workspace-tabs')).toBeVisible({timeout:60000});
  await page.locator('.standby-workspace-tabs button').nth(1).click();
  await expect(page.locator('.standby table tbody tr')).toHaveCount(18);
  await expect(page.locator('.standby .message.error')).toHaveCount(0);
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),true);
  assert.equal(await page.locator('.grid-scroll').evaluate(e=>e.scrollHeight>e.clientHeight),true);
  await page.screenshot({path:resolve(output,'workspace-'+width+'.png'),fullPage:true});
  await page.locator('.planner').scrollIntoViewIfNeeded();await page.screenshot({path:resolve(output,'grid-'+width+'.png'),fullPage:true});
  await page.locator('.teacher-heading .inspect-trigger').first().click();await expect(page.locator('.inspector')).toBeFocused();
  await page.locator('.inspector .icon-button').click();await expect(page.locator('.teacher-heading .inspect-trigger').first()).toBeFocused();
  if(width===1440) {
   await page.locator('.mode-picker button').nth(1).click();
   const row=page.locator('.standby tbody tr').filter({hasText:'Standby Teacher 2'});
   if(await row.locator('.cell').first().getAttribute('class').then(s=>!s.includes('selected'))) await row.locator('.cell-button').first().click();
   const demand=page.locator('.standby .default-demand input');await demand.fill(String(Number(await demand.inputValue())%17+1));
   await expect(row.locator('.cell.selected').first()).toBeVisible();
   const count=await page.locator('.cell.selected').count();
   page.once('dialog',dialog=>dialog.dismiss());await page.locator('.standby-workspace-tabs button').first().click();
   await expect(page.locator('.standby')).toBeVisible();
   await page.locator('.standby .save-actions .secondary').click();
   await expect(page.locator('.standby .save-actions .primary')).toBeEnabled();
   await page.locator('.standby .save-actions .primary').click();
   await expect(page.locator('.standby .message.success')).toContainText('تم حفظ');
   const persisted=(await (await api(endpoint)).json()).data;assert.equal(persisted.placements.length,count);assert.equal(persisted.isPublished,false);
   await page.locator('.standby-workspace-tabs button').first().click();
   await expect(page.locator('.schedule-grid')).toBeVisible();
   await page.locator('.standby-workspace-tabs button').nth(1).click();
   await expect(page.locator('.standby tbody tr')).toHaveCount(18);
   await page.locator('.standby .demand-input input').nth(2).fill('1');
   await page.locator('.standby .save-actions .secondary').click();await expect(page.locator('.standby .save-actions .primary')).toBeEnabled();
   // Advance the isolated server behind the reviewed client proposal; stale save must retain UI edits.
   const latest=(await (await api(endpoint)).json()).data;
   const body={revision:latest.revision,mode:'Manual',settings:latest.settings,placements:latest.placements};
   const preview=(await (await api(endpoint+'/preview',{method:'POST',body:JSON.stringify(body)})).json()).data;
   assert.equal((await api(endpoint,{method:'PUT',body:JSON.stringify({...body,proposalId:preview.proposalId})})).status,200);
   await page.locator('.standby .save-actions .primary').click();await expect(page.locator('.standby .message.error')).toBeVisible();
   await expect(page.locator('.standby .demand-input input').nth(2)).toHaveValue('1');
   page.once('dialog',dialog=>dialog.accept());await page.locator('.standby .intro .icon-button').click();
   await expect(page.locator('.standby .message.error')).toHaveCount(0);
   await page.locator('.mode-picker button').first().click();await page.locator('.standby .save-actions .secondary').click();
   await expect(page.locator('.standby .save-actions .primary')).toBeEnabled();
   await page.screenshot({path:resolve(output,'review-'+width+'.png'),fullPage:true});
   await page.locator('.standby .save-actions .primary').click();await expect(page.locator('.standby .message.success')).toContainText('تم حفظ');
   await page.locator('.standby-workspace-tabs button').first().click();
   await page.getByRole('button',{name:'نشر للمعلمين',exact:true}).click();
   await expect(page.locator('.status-block')).toContainText('منشور');
   const personal=await (await api('/api/v1/timetables/current?academicYearId=1&semester=1&personalOnly=true',{headers:{Authorization:'Bearer '+privateState.teacherToken}})).json();
   assert.equal(personal.isSuccess,true);assert.ok(personal.data.entries.some(e=>e.entryType===2||e.entryType==='Standby'));
   const pdf=await api(`/api/v1/timetables/${privateState.timetableId}/pdf?colorMode=monochrome&personalOnly=false`);assert.equal(pdf.status,200);const bytes=Buffer.from(await pdf.arrayBuffer());assert.equal(bytes.subarray(0,4).toString(),'%PDF');writeFileSync(resolve(output,'weekly-timetable.pdf'),bytes);
  }
  assert.deepEqual(errors,[]);results.push({width,noPageOverflow:true,boundedScrollableGrid:true,noPageErrors:true,isolatedApi:true,writes:writes.length,manualFairPersistenceConflictAndPublication:width===1440});
  await context.close();
 }
 const local=await browser.newContext({viewport:{width:1440,height:1080},reducedMotion:'reduce'});
 await local.addInitScript(s=>{sessionStorage.setItem('alfalah_access_token',s.token);sessionStorage.setItem('alfalah_user',JSON.stringify(s.user));},session);
 const localPage=await local.newPage();await localPage.goto('http://localhost:4200/timetable');await localPage.locator('.standby-workspace-tabs button').nth(1).click();
 await expect(localPage.locator('.standby tbody tr')).toHaveCount(2);await expect(localPage.locator('.standby .message.error')).toHaveCount(0);await localPage.screenshot({path:resolve(output,'local-workspace.png'),fullPage:true});await local.close();
 writeFileSync(resolve(output,'browser-results.json'),JSON.stringify({at:new Date().toISOString(),results,localWorkspaceReadOnly:true,operationalWrites:false,providerWrites:false},null,2));
 console.log(JSON.stringify(results));
}finally{await browser.close();}
