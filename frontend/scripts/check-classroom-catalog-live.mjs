// Read-only real application verification. Private test-school sessions are
// generated locally; never print or publish their tokens.
import assert from 'node:assert/strict';
import { chromium, expect } from '@playwright/test';
import { readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
const evidence=resolve('../docs/specs/classroom-catalog/verification/implementation');
mkdirSync(evidence,{recursive:true});
const browser=await chromium.launch({headless:true}),results=[];
try {
  for(const role of ['manager','teacher']) for(const width of [390,768,1440]) {
    const session=JSON.parse(readFileSync(resolve(`../.audit/classroom-implementation/${role}-session-private.json`),'utf8'));
    const context=await browser.newContext({viewport:{width,height:1000}});
    await context.addInitScript(s=>{sessionStorage.setItem('alfalah_access_token',s.token);sessionStorage.setItem('alfalah_user',JSON.stringify(s.user));},session);
    const page=await context.newPage(),errors=[],writes=[];
    page.on('pageerror',e=>errors.push(e.message));
    page.on('request',r=>{if(r.url().includes('/api/')&&['POST','PUT','PATCH','DELETE'].includes(r.method()))writes.push(r.method()+' '+new URL(r.url()).pathname);});
    await page.goto('http://localhost:4200'+(role==='manager'?'/student-affairs/classrooms':'/account/settings'));
    if(role==='manager') {
      await expect(page.locator('.catalog-table')).toBeVisible({timeout:60000});
      await expect(page.getByRole('button',{name:'إضافة شعب',exact:true})).toBeEnabled();
      await page.screenshot({path:resolve(evidence,`manager-${width}.png`),fullPage:true});
      await page.getByRole('button',{name:'إضافة شعب',exact:true}).click();
      await page.locator('[name=location]').fill('موقع تجريبي دون حفظ');
      await page.locator('[name=firstSection]').fill('5');await page.locator('[name=lastSection]').fill('3');
      await page.locator('.catalog-form button[type=submit]').click();await expect(page.locator('.catalog-form [role=alert]')).toBeVisible();
      await page.screenshot({path:resolve(evidence,`manager-dialog-${width}.png`)});
      await page.getByRole('button',{name:'إلغاء',exact:true}).click();
    } else {
      const selector=page.locator('app-classroom-selector');await expect(selector.locator('.p-multiselect')).toBeEnabled({timeout:60000});
      await selector.locator('.p-multiselect').click();
      await expect(page.locator('.p-multiselect-panel .p-multiselect-item').first()).toBeVisible();
      await expect(page.locator('.p-multiselect-panel')).toHaveCSS('opacity','1');
      await expect(page.locator('.p-multiselect-panel').locator('..')).toHaveCSS('opacity','1');
      await page.screenshot({path:resolve(evidence,`teacher-selector-${width}.png`)});
      await page.keyboard.press('Escape');
      await expect(page.locator('.p-multiselect-panel')).toBeHidden();
      await page.screenshot({path:resolve(evidence,`teacher-${width}.png`),fullPage:true});
    }
    assert.deepEqual(errors,[]);assert.deepEqual(writes,[]);
    assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),true);
    results.push({role,width,realApi:true,noBusinessWrites:true,noPageErrors:true,noPageOverflow:true});await context.close();
  }
  writeFileSync(resolve(evidence,'browser-live.json'),JSON.stringify({at:new Date().toISOString(),results},null,2));
  console.log('PASS: actual manager catalogue and teacher selector at 390/768/1440; no business writes.');
} finally { await browser.close(); }
