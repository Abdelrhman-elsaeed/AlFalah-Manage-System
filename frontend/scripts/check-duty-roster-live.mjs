// Actual local API and real authenticated browser downloads. No API interception or Drive writes.
import assert from 'node:assert/strict';
import {readFileSync,writeFileSync,mkdirSync} from 'node:fs';
import {resolve} from 'node:path';
import {createHash} from 'node:crypto';
import {chromium} from '@playwright/test';
const privateDir=resolve('../.audit/roster-source-20261015');
const session=JSON.parse(readFileSync(resolve(privateDir,'session-private.json'),'utf8').replace(/^\uFEFF/,''));
const expected=JSON.parse(readFileSync(resolve(privateDir,'actual-result.json'),'utf8'));
const evidence=resolve('../docs/specs/school-duty/verification/roster-source-screens');mkdirSync(evidence,{recursive:true});
const browser=await chromium.launch({headless:true}),results=[];
try{
 for(const width of [390,768,1440]){
  const page=await browser.newPage({viewport:{width,height:1000},reducedMotion:'reduce'}),errors=[],mutations=[];
  page.setDefaultTimeout(20000);page.on('pageerror',e=>errors.push(e.message));
  page.on('request',r=>{if(new URL(r.url()).pathname.startsWith('/api/')&&!['GET','OPTIONS'].includes(r.method()))mutations.push({method:r.method(),path:new URL(r.url()).pathname});});
  await page.addInitScript(session=>{sessionStorage.setItem('alfalah_access_token',session.token);sessionStorage.setItem('alfalah_user',JSON.stringify(session.user));},session);
  await page.goto('http://localhost:4200/school-duty');const panel=page.locator('.schedule-panel');await panel.waitFor();
  assert.equal(await page.locator('app-duty-roster-export').count(),1);
  const button=panel.locator('app-duty-roster-export button');await button.scrollIntoViewIfNeeded();
  assert((await button.boundingBox()).height>=44);
  const [download]=await Promise.all([page.waitForEvent('download'),button.click()]);
  const file=resolve(evidence,`official-roster-download-${width}.pdf`);await download.saveAs(file);
  const hash=createHash('sha256').update(readFileSync(file)).digest('hex').toUpperCase();assert.equal(hash,expected.hash);
  const mine=panel.locator('.schedule-filters button').first();await mine.click();
  const [again]=await Promise.all([page.waitForEvent('download'),button.click()]);const temp=resolve(privateDir,`filtered-download-${width}.pdf`);await again.saveAs(temp);assert.equal(createHash('sha256').update(readFileSync(temp)).digest('hex').toUpperCase(),expected.hash,'Official full-term roster does not silently become the current ten-day/teacher filter');
  await button.scrollIntoViewIfNeeded();await page.screenshot({path:resolve(evidence,`roster-button-${width}.png`),fullPage:true,animations:'disabled'});
  assert(await page.evaluate(()=>document.documentElement.scrollWidth<=document.documentElement.clientWidth+1));
  assert.deepEqual(errors,[]);assert.deepEqual(mutations,[]);
  results.push({width,realLocalApi:true,interceptedApi:false,nativeAuthenticatedDownload:true,sha256:hash,repeatAndFilterBytesEqual:true,buttonInsideSchedule:true,oneExportButton:true,minimum44px:true,noSigningOrArchiveRequests:true});await page.close();
 }
 writeFileSync(resolve(evidence,'browser-results.json'),JSON.stringify({at:new Date().toISOString(),results,physicalDevice:'Pending',ownerVisualReview:'Pending',driveWrites:0},null,2));console.log('Real authenticated official roster downloads passed at390/768/1440, including retained-byte equality and unchanged full-plan scope after filtering.');
}finally{await browser.close();}
