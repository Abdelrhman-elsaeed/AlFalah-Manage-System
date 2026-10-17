import assert from 'node:assert/strict';
import {chromium,expect} from '@playwright/test';
import {readFileSync,writeFileSync,mkdirSync} from 'node:fs';
import {createHash} from 'node:crypto';
import {resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=fileURLToPath(new URL('../../',import.meta.url));
const state=JSON.parse(readFileSync(resolve(root,'.audit/standby-phase3/isolated-private.json'),'utf8'));
assert.match(state.connection,/(?:Database|Initial Catalog)=AlFalahRegisterTests_[a-f0-9]{32}(?:;|$)/i);
const output=resolve(root,'docs/specs/intelligent-timetable/verification/standby-hub');mkdirSync(output,{recursive:true});
async function api(path,options={}){return fetch('http://localhost:5270'+path,{...options,headers:{Authorization:'Bearer '+state.sessions.manager.token,'Content-Type':'application/json',Origin:'http://localhost:4200',...options.headers}});}
async function data(path){const r=await api('/api/v1/'+path);assert.equal(r.status,200);return(await r.json()).data;}
const table=await data('timetables/current?academicYearId=1&semester=1&personalOnly=false');
const baseline=JSON.stringify(await data(`timetables/${table.id}/standby`));
const browser=await chromium.launch({headless:true}),results=[];
try{
 for(const width of [390,768,1440,1920]){
  console.log('Standby hub viewport',width);const context=await browser.newContext({viewport:{width,height:1080},reducedMotion:'reduce',acceptDownloads:true});
  await context.addInitScript(s=>{sessionStorage.setItem('alfalah_access_token',s.token);sessionStorage.setItem('alfalah_user',JSON.stringify(s.user));},state.sessions.manager);
  const writes=[],failures=[],errors=[];
  await context.route('**/api/**',async route=>{const req=route.request(),url=new URL(req.url());if(!['GET','OPTIONS'].includes(req.method())&&!url.pathname.endsWith('/standby/preview')){writes.push({path:url.pathname,method:req.method()});throw Error('Hub verification must not mutate stored data');}
   const r=await api(url.pathname+url.search,{method:req.method(),body:req.postData()||undefined,headers:{Accept:req.headers().accept||'application/json'}});if(r.status>=400)failures.push({path:url.pathname,status:r.status});const headers=Object.fromEntries(r.headers);delete headers['content-length'];delete headers['content-encoding'];await route.fulfill({status:r.status,headers,body:Buffer.from(await r.arrayBuffer())});
  });
  const page=await context.newPage();page.setDefaultTimeout(45000);page.on('pageerror',e=>errors.push(e.message));
  await page.goto('http://localhost:4200/intelligent-timetable/standby',{waitUntil:'domcontentloaded'});await expect(page.locator('.standby')).toBeVisible();
  await expect(page.locator('h1:visible')).toHaveCount(1);await expect(page.locator('.standby .intro')).toHaveCount(0);await expect(page.locator('.standby .message.error')).toHaveCount(0);
  await expect(page.locator('#weekly-tab')).toHaveAttribute('aria-pressed','true');
  const overflow=()=>page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1);
  await expect(page.locator('.p-toast-message')).toHaveCount(0,{timeout:20000});await page.waitForTimeout(350);assert.equal(await overflow(),false);
  const clipped=await page.locator('.standby-embedded .filters-card select').evaluateAll(items=>items.some(e=>{const b=e.getBoundingClientRect(),p=e.closest('.filters-card').getBoundingClientRect();return b.left<p.left||b.right>p.right;}));assert.equal(clipped,false,'Academic selectors must remain inside their context surface');
  await page.screenshot({path:resolve(output,'weekly-'+width+'.png'),fullPage:true});
  const readState=()=>page.locator('app-weekly-standby').evaluate(e=>{const c=window.ng.getComponent(e);return JSON.stringify({settings:c.view.settings,placements:c.view.placements});});
  const original=await readState();await page.locator('.mode-picker button').nth(1).click();await page.locator('.cell-button:enabled').first().click();const edited=await readState();assert.notEqual(edited,original);
  await page.locator('#daily-tab').click();await expect(page.locator('#daily-panel')).toBeVisible();await expect(page.locator('.ledger-summary')).toBeVisible();await expect(page.locator('#weekly-panel')).toBeHidden();
  await expect(page.locator('h1:visible')).toHaveCount(1);await expect(page.locator('.ledger-header')).toHaveCount(0);
  assert.equal(await overflow(),false);await page.screenshot({path:resolve(output,'daily-empty-'+width+'.png'),fullPage:true});
  const date=page.locator('.coverage-ledger>.toolbar input[type=date]');await date.fill(state.pastDate);await date.dispatchEvent('change');await expect(page.locator('.ledger-table tbody tr')).toHaveCount(2);
  await page.screenshot({path:resolve(output,'daily-'+width+'.png'),fullPage:true});
  await page.locator('#weekly-tab').click();await expect(page.locator('#weekly-panel')).toBeVisible();assert.equal(await readState(),edited);await expect(page.locator('.planner-tools .undo-action')).toBeEnabled();
  await page.locator('.hub-actions a[href="/timetable"]').scrollIntoViewIfNeeded();page.once('dialog',d=>d.dismiss());await page.locator('.hub-actions a[href="/timetable"]').click();await expect(page).toHaveURL(/standby(?:\?.*)?$/);
  await page.locator('.planner-tools .undo-action').click();assert.equal(await readState(),original);
  await page.locator('.register-action').click();await expect(page.locator('.register-workspace')).toBeVisible();await expect(page.locator('#daily-tab')).toHaveAttribute('aria-pressed','true');
  await expect(page.locator('.revision-row').first()).toBeVisible();await page.locator('.revision-row').first().click();await expect(page.locator('.pdf-pages canvas').first()).toBeVisible();
  const selected=await page.locator('app-coverage-register').evaluate(e=>window.ng.getComponent(e).selected.sha256);const download=page.waitForEvent('download');await page.locator('.register-footer .primary').click();const file=await download;assert.equal(createHash('sha256').update(readFileSync(await file.path())).digest('hex').toUpperCase(),selected.toUpperCase());
  await page.locator('.register-footer button:not(.primary)').click();await expect(page.locator('.register-workspace')).toHaveCount(0);
  await page.reload();await expect(page.locator('#daily-tab')).toHaveAttribute('aria-pressed','true');await expect(page.locator('.ledger-summary')).toBeVisible();
  await page.goto('http://localhost:4200/intelligent-timetable/substitutions?register=true',{waitUntil:'domcontentloaded'});await expect(page).toHaveURL(/standby\?.*tab=daily/);await expect(page.locator('.register-workspace')).toBeVisible();
  await expect(page.locator('.register-intro input')).toBeEnabled();await page.locator('.register-footer button:not(.primary)').click();
  await page.goto('http://localhost:4200/intelligent-timetable/substitutions',{waitUntil:'domcontentloaded'});await expect(page.locator('app-daily-substitutions')).toBeVisible();await expect(page.locator('app-daily-coverage-ledger')).toHaveCount(0);await expect(page.locator('.coverage-tabs')).toHaveCount(0);
  assert.equal(await overflow(),false);assert.deepEqual(errors,[]);assert.deepEqual(writes,[]);assert.ok(failures.every(f=>f.path.startsWith('/api/v1/storage/')&&f.status===403),JSON.stringify(failures));
  results.push({width,singlePageHeading:true,weeklyAndDailyInOneHub:true,localWeeklyStateAndUndoRetained:true,dirtyLeaveCancelled:true,dailyDateRetainedAcrossTabs:true,emptyAndSignedDailyRows:true,pdfPreviewDownloadHash:true,dailyRefreshRoute:true,legacyPdfRedirect:true,swapsSeparate:true,noMutations:true,pageOverflow:false,unrelatedStorageDenials:failures});await context.close();
 }
 assert.equal(JSON.stringify(await data(`timetables/${table.id}/standby`)),baseline);
 writeFileSync(resolve(output,'browser-results.json'),JSON.stringify({at:new Date().toISOString(),isolated:true,providerWrites:false,storedWorkbenchUnchanged:true,results},null,2));console.log('PASS: unified standby hub, preserved work, PDF compatibility and separate swaps.');
}finally{await browser.close();}
