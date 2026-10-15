// Standalone in-memory design review. No application API or operational writes.
import assert from 'node:assert/strict';
import { chromium, expect } from '@playwright/test';
import { mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
const evidence=resolve('../docs/specs/classroom-catalog/verification');mkdirSync(evidence,{recursive:true});
const url='http://127.0.0.1:4780/index.html';
const browser=await chromium.launch({headless:true}),results=[];
try{for(const width of [390,768,1440]){
 const context=await browser.newContext({viewport:{width,height:1000},reducedMotion:'reduce'}),page=await context.newPage(),errors=[],api=[];
 page.on('pageerror',e=>errors.push(e.message));page.on('request',r=>{if(r.url().includes('/api/'))api.push(r.method());});
 await page.goto(url);await page.locator('.cell').first().waitFor();await page.evaluate(()=>document.fonts.ready);
 await page.screenshot({path:resolve(evidence,`assignments-${width}.png`),fullPage:true});
 await page.locator('[data-edit="1-1"]').click();await page.locator('[data-member="1"]').check();await page.locator('[data-member="2"]').check();
 await expect(page.locator('[data-action=confirm-assignment]')).toBeDisabled();
 await page.locator('#assignment-mode').selectOption('SplitQuota');await page.locator('[data-count="1"]').fill('3');await page.locator('[data-pairs="1"]').fill('1');await page.locator('[data-count="2"]').fill('2');await page.locator('[data-pairs="2"]').fill('0');
 await expect(page.locator('[data-action=confirm-assignment]')).toBeEnabled();
 await page.screenshot({path:resolve(evidence,`picker-${width}.png`)});
 await page.locator('[data-action=confirm-assignment]').click();await expect(page.locator('.draft-bar')).toBeVisible();
 await page.locator('[data-action=export]').click();await expect(page.locator('#export-dialog')).not.toBeVisible();
 await page.locator('[data-edit="2-1"]').click();await page.locator('[data-member="1"]').check();await page.locator('#assignment-mode').selectOption('CoTeaching');await expect(page.locator('[data-action=confirm-assignment]')).toBeEnabled();await page.locator('[data-action=confirm-assignment]').click();
 await page.locator('[data-edit="1-2"]').click();await page.locator('[data-member="7"]').uncheck();await page.locator('[data-member="1"]').check();await page.locator('[data-action=confirm-assignment]').click();
 await page.locator('[data-action=save]').click();await page.locator('[data-action=export]').click();await expect(page.locator('.paper-addendum')).toBeVisible();await expect(page.locator('.paper-table')).toContainText('(\u0663\u062d)');await expect(page.locator('.paper-table')).toContainText('\u0645\u0634\u062a\u0631\u0643');await expect(page.locator('.paper-table tbody tr')).toHaveCount(24);
 await page.screenshot({path:resolve(evidence,`export-${width}.png`)});
 if(width===1440)await page.pdf({path:resolve(evidence,'assignment-statement-sample.pdf'),format:'A4',landscape:true,printBackground:true,preferCSSPageSize:true});
 await page.keyboard.press('Escape');await expect(page.locator('[data-action=export]')).toBeFocused();
 await page.locator('[data-action=teachers]').click();await expect(page.locator('.teacher-table tbody tr')).toHaveCount(8);await page.locator('[data-page="2"]').click();await page.screenshot({path:resolve(evidence,`teacher-load-${width}.png`),fullPage:true});
 await page.locator('[data-view=catalog]').click();await page.locator('[data-action=new-classes]').click();await page.locator('[name=from]').fill('1');await page.locator('[name=to]').fill('2');await page.locator('#class-form button[type=submit]').click();await expect(page.locator('#class-error')).toBeVisible();
 await page.locator('[name=from]').fill('5');await page.locator('[name=to]').fill('6');await page.locator('#class-form button[type=submit]').click();await expect(page.locator('.class-row')).toHaveCount(14);await page.screenshot({path:resolve(evidence,`catalog-${width}.png`),fullPage:true});
 await page.locator('[data-view=teacher]').click();await expect(page.locator('[data-class]')).toHaveCount(14);await expect(page.locator('.class-selector')).not.toHaveAttribute('open');await page.locator('.class-selector summary').click();await page.locator('[data-class="13"]').check();await page.locator('[data-class="14"]').check();await page.locator('[data-action=save-classes]').click();await expect(page.locator('#toast')).toContainText('دون انتظار موافقة');await page.screenshot({path:resolve(evidence,`teacher-selection-${width}.png`),fullPage:true});
 assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'Document overflow');assert.deepEqual(api,[]);assert.deepEqual(errors,[]);
 results.push({width,syntheticInMemoryOnly:true,splitQuotaAndWholePair:true,explicitMultiTeacherMode:true,coTeaching:true,nonSpecializationAppendix:true,splitAndCoPrintAnnotations:true,unsavedExportBlocked:true,sourceStructuredStatement24Rows:true,teacherPagination:true,batchClassCreation:true,duplicatesRejectedAtomically:true,newClassesInTeacherSelection:true,groupedMultiSelectDropdown:true,selfConfirmationNoApproval:true,noApiRequests:true,escapeFocus:true,noDocumentOverflow:true});await context.close();
}writeFileSync(resolve(evidence,'browser-results.json'),JSON.stringify({at:new Date().toISOString(),results,physicalDevice:'Pending',productionImplementation:false},null,2));console.log('PASS: isolated classroom/assignment design workflows at390/768/1440; no API requests.');}finally{await browser.close();}
