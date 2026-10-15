// Real API/browser checks with private test-school sessions. No business writes.
import assert from 'node:assert/strict';
import { chromium, expect } from '@playwright/test';
import { readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
const evidence=resolve('../docs/specs/classroom-catalog/verification/consumers');
mkdirSync(evidence,{recursive:true});
const session=JSON.parse(readFileSync(resolve('../.audit/classroom-consumers/manager-session-private.json'),'utf8'));
const browser=await chromium.launch({headless:true}),results=[];
try {
  for(const width of [390,768,1440]) for(const feature of ['visits','timetable']) {
    const context=await browser.newContext({viewport:{width,height:1000}});
    await context.addInitScript(s=>{sessionStorage.setItem('alfalah_access_token',s.token);sessionStorage.setItem('alfalah_user',JSON.stringify(s.user));},session);
    const page=await context.newPage(),errors=[],writes=[],catalogRequests=[];
    page.on('pageerror',error=>errors.push(error.message));
    page.on('dialog',dialog=>dialog.accept());
    page.on('request',request=>{
      if(request.url().includes('/api/')&&['POST','PUT','PATCH','DELETE'].includes(request.method())) writes.push(request.method()+' '+new URL(request.url()).pathname);
      if(request.url().includes('/classroom-catalog'))catalogRequests.push(new URL(request.url()).searchParams);
    });
    await page.goto('http://localhost:4200/'+(feature==='visits'?'visits-v2':'timetable'));
    if(feature==='timetable') {
      await expect(page.locator('.schedule-cell.editable').first()).toBeVisible({timeout:60000});
      if(width===390)await page.getByRole('button',{name:'تكبير الجدول',exact:true}).click();
      await page.locator('.schedule-cell.editable').first().scrollIntoViewIfNeeded();
      // The timetable has sticky teacher/total columns; use an exposed cell.
      const exposed=await page.locator('.schedule-cell.editable').evaluateAll(cells=>cells.findIndex(cell=>{
        const r=cell.getBoundingClientRect(),x=(Math.max(0,r.left)+Math.min(innerWidth,r.right))/2,y=(Math.max(0,r.top)+Math.min(innerHeight,r.bottom))/2;
        return r.right>0&&r.left<innerWidth&&r.bottom>0&&r.top<innerHeight&&cell.contains(document.elementFromPoint(x,y));
      }));
      assert.ok(exposed>=0,'an editable cell is exposed beside the sticky columns');
      await page.locator('.schedule-cell.editable').nth(exposed).click();
      const editor=page.locator('.cell-editor');await expect(editor).toBeVisible();
      await editor.locator('select').selectOption({label:'حصة'});
    }
    const picker=page.locator('app-classroom-picker');
    await expect(picker.locator('.p-dropdown')).toBeVisible({timeout:60000});
    await expect(picker.locator('.p-disabled')).toHaveCount(0,{timeout:60000});
    await picker.locator('.p-dropdown').click();
    await expect(page.locator('.p-dropdown-panel .p-dropdown-item').first()).toBeVisible();
    await expect(page.locator('.p-dropdown-panel')).toHaveCSS('opacity','1');
    await expect(page.locator('.p-dropdown-panel').locator('..')).toHaveCSS('opacity','1');
    await page.screenshot({path:resolve(evidence,`${feature}-${width}.png`)});
    await page.keyboard.press('Escape');
    assert.equal(catalogRequests.some(p=>feature==='visits'?!!p.get('onDate'):!!p.get('academicYearId')),true);
    assert.deepEqual(errors,[]);assert.deepEqual(writes,[]);
    const noOverflow=await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1);
    assert.equal(noOverflow,true);
    results.push({feature,width,realApi:true,contextQueryVerified:true,noBusinessWrites:true,noPageErrors:true,noPageOverflow:noOverflow});
    await context.close();
  }
  writeFileSync(resolve(evidence,'browser-live.json'),JSON.stringify({at:new Date().toISOString(),results},null,2));
  console.log('PASS: live contextual visit/timetable pickers at 390/768/1440, no business writes.');
} finally {await browser.close();}
