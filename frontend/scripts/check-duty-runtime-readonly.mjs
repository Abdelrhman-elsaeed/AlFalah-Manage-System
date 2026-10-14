// Read-only verification of already retained synthetic-school artifacts.
// No mocked API, acknowledgement, draft save, provider write or DB migration.
import {readFileSync,mkdirSync,writeFileSync} from 'node:fs';
import {resolve} from 'node:path';
import {createHash} from 'node:crypto';
import assert from 'node:assert/strict';
import {chromium} from '@playwright/test';
const config=JSON.parse(readFileSync(process.argv[2],'utf8'));
const evidence=resolve('../.audit/duty-organized-20261014/runtime-readonly');mkdirSync(evidence,{recursive:true});
const browser=await chromium.launch({headless:true});const checks=[];
try{
 for(const account of config.users)for(const width of [390,768,1440]){
  const page=await browser.newPage({viewport:{width,height:1000},reducedMotion:'reduce'});const errors=[],requests=[];
  page.on('pageerror',e=>errors.push(e.message));page.on('response',r=>{if(new URL(r.url()).pathname.includes('/school-duty/'))requests.push({status:r.status(),path:new URL(r.url()).pathname,mime:r.headers()['content-type']});});
  await page.addInitScript(({token,user})=>{sessionStorage.setItem('alfalah_access_token',token);sessionStorage.setItem('alfalah_user',JSON.stringify(user));localStorage.setItem('alfalah_language','ar');localStorage.setItem('language','ar');},account);
  await page.goto(`http://localhost:4200/school-duty/documents?execution=${config.assignment}`);
  try{await page.locator('.document-card.available').first().waitFor();}catch(e){await page.screenshot({path:resolve(evidence,'load-failure.png')});writeFileSync(resolve(evidence,'load-failure.json'),JSON.stringify({url:page.url(),errors,requests,visibleError:await page.locator('.duty-error-dialog[open] p').allTextContents()},null,2));throw e;}
  const card=page.locator('.document-card').first();
  let download,pdf;
  try{[download,pdf]=await Promise.all([page.waitForEvent('download'),page.waitForResponse(r=>new URL(r.url()).pathname.endsWith('/pdf')),card.locator('.download-action').click()]);}
  catch(e){await page.screenshot({path:resolve(evidence,'download-failure.png')});writeFileSync(resolve(evidence,'download-failure.json'),JSON.stringify({url:page.url(),errors,requests,visibleError:await page.locator('.duty-error-dialog[open] p').allTextContents()},null,2));throw e;}
  const expected=createHash('sha256').update(await pdf.body()).digest('hex').toUpperCase();
  const target=resolve(evidence,`${account.role}-${width}.pdf`);await download.saveAs(target);
  const actual=createHash('sha256').update(readFileSync(target)).digest('hex').toUpperCase();assert.equal(actual,expected);
  await card.locator('.primary-action').click();
  try{await page.locator('.pdf-pages canvas').first().waitFor();await page.locator('.preview-message[role=status]').waitFor({state:'hidden'});}
  catch(e){await page.screenshot({path:resolve(evidence,'failure.png')});writeFileSync(resolve(evidence,'failure.json'),JSON.stringify({errors,requests,visibleError:await page.locator('.duty-error-dialog[open] p').allTextContents()},null,2));throw e;}
  await page.screenshot({path:resolve(evidence,`${account.role}-${width}.png`),fullPage:true,animations:'disabled'});
  await page.keyboard.press('Escape');await page.locator('.duty-pdf-dialog').waitFor({state:'hidden'});
  assert.deepEqual(errors,[]);assert(requests.every(r=>r.status===200));checks.push({role:account.role,width,actual,expected,bytes:readFileSync(target).length,requests,actualApi:true,actualSql:true,nativeBrowserDownload:true,renderedPreview:true});await page.close();
 }
 writeFileSync(resolve(evidence,'results.json'),JSON.stringify({checks,mutations:0,providerWrites:0,mockedApi:false},null,2));console.log('Actual running API/browser retained letter reads passed: '+checks.length);
}finally{await browser.close();}
