// Real authenticated local UI and Drive reads, no API interception and no mutation requests.
import assert from 'node:assert/strict';
import { chromium,expect } from '@playwright/test';
import { readFileSync,mkdirSync,writeFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { resolve } from 'node:path';
const scratch=resolve('../.audit/storage-drive-20261015');
const session=JSON.parse(readFileSync(resolve('../.audit/storage-duty-20261014/browser-private.json'),'utf8').replace(/^\uFEFF/,''));
assert.equal(session.user.activeSchoolId,18);assert.equal(session.user.activeSchoolName,'Al-Falah E2E Test School');
const generation=JSON.parse(readFileSync(resolve(scratch,'generation-output.json'),'utf8'));assert.equal(generation.realDrive,true);assert.equal(generation.uploads,5);
const expected=new Map(generation.hashes.map(x=>[x.name,x.hash]));const files=[];let reads=0;
async function api(handle){const r=await fetch('http://localhost:5264/api/v1/storage/drive'+(handle?'?handle='+encodeURIComponent(handle):''),{headers:{Authorization:'Bearer '+session.token}});assert.equal(r.status,200);const data=await r.json();assert(data.isSuccess);reads++;assert(!data.data.nextPageToken);return data.data;}
const home=await api(null);assert.equal(home.items.length,1);const root=await api(home.items[0].handle);assert.equal(root.items.length,4);assert(!root.items.some(x=>x.name==='D3_Test'));
async function walk(folder,branch,depth=0){assert(depth<12);const page=await api(folder.handle);for(const item of page.items){if(!item.isFolder&&expected.has(item.name))files.push({item,parent:folder.handle,branch});}
 if(branch==='مكتبة المدرسة')return;for(const item of page.items.filter(x=>x.isFolder))await walk(item,branch,depth+1);
}
for(const branch of root.items)await walk(branch,branch.name);assert.equal(files.length,5);
const evidence=resolve('../docs/specs/school-file-storage/verification/drive-browser-live-screens');mkdirSync(evidence,{recursive:true});
const browser=await chromium.launch({headless:true});const results=[];
try{for(const width of [390,768,1440]){
 const context=await browser.newContext({viewport:{width,height:1000},locale:'ar-SA'});const page=await context.newPage();const errors=[],mutations=[];
 page.on('pageerror',e=>errors.push(e.message));page.on('request',r=>{if(r.url().includes('/api/')&&!['GET','OPTIONS'].includes(r.method()))mutations.push(r.method());});
 await page.addInitScript(session=>{sessionStorage.setItem('alfalah_access_token',session.token);sessionStorage.setItem('alfalah_user',JSON.stringify(session.user));},session);
 await page.goto('http://localhost:4200/school-manager/storage');await page.locator('.drive-table tbody tr').waitFor();assert.equal(await page.locator('.drive-table tbody tr').count(),1);
 await page.locator('.drive-item').click();await expect(page.locator('.drive-table tbody tr')).toHaveCount(4);await page.screenshot({path:resolve(evidence,`real-school-root-${width}.png`),fullPage:true});
 const selected=width===1440?files:files.filter(x=>x.item.name.startsWith('خطاب المناوبة'));
 const downloads=[];for(const entry of selected){await page.goto('http://localhost:4200/school-manager/storage?drive='+encodeURIComponent(entry.parent));const row=page.locator('.drive-item').filter({hasText:entry.item.name});await row.waitFor();await row.click();try{await page.locator('app-duty-pdf-preview canvas').first().waitFor({timeout:30000});}catch(e){await page.screenshot({path:resolve(scratch,'preview-failure.png'),fullPage:true});writeFileSync(resolve(scratch,'preview-failure.json'),JSON.stringify({name:entry.item.name,mime:entry.item.mimeType,errors,dialogs:await page.locator('dialog[open]').allTextContents()},null,2));throw e;}await page.waitForFunction(()=>{const p=window.ng.getComponent(document.querySelector('app-duty-pdf-preview'));return p&&!p.loading()&&!p.error()&&p.pages()>0;});
  const downloadEvent=page.waitForEvent('download');await page.getByRole('button',{name:'تنزيل النسخة المعروضة',exact:true}).click();const download=await downloadEvent;const bytes=readFileSync(await download.path());const hash=createHash('sha256').update(bytes).digest('hex').toUpperCase();assert.equal(hash,expected.get(entry.item.name));downloads.push({name:entry.item.name,sha256:hash,bytes:bytes.length});
  await page.screenshot({path:resolve(evidence,`real-preview-${width}-${downloads.length}.png`),fullPage:true});await page.getByRole('button',{name:'إغلاق المعاينة',exact:true}).click();assert.equal(new URL(page.url()).pathname,'/school-manager/storage');
 }
 assert.deepEqual(errors,[]);assert.deepEqual(mutations,[]);results.push({width,realLocalApi:true,interception:false,realRootFolders:4,allNativeStorageNavigation:true,pdfCanvasRendered:true,nativeBrowserDownloads:downloads,mutations:0});await context.close();
 }writeFileSync(resolve(evidence,'browser-results.json'),JSON.stringify({at:new Date().toISOString(),boundedRootMetadataReads:reads,results,providerWrites:0,ownerVisualReview:'Pending',physicalDevice:'Pending'},null,2));console.log('Real native Drive folders, five workflow artifacts, rendered previews and matching native browser downloads verified at390/768/1440.');
}finally{await browser.close();}
