// Real authenticated local API/Drive reads. No intercepted application APIs or writes.
import assert from 'node:assert/strict';
import { chromium, expect } from '@playwright/test';
import { readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { createHash } from 'node:crypto';
const privateDir=resolve('../.audit/roster-source-20261015');
const session=JSON.parse(readFileSync(resolve(privateDir,'session-private.json'),'utf8').replace(/^\uFEFF/,''));
assert.equal(session.user.activeSchoolId,18);assert.equal(session.user.activeSchoolName,'Al-Falah E2E Test School');
const evidence=resolve('../docs/specs/school-file-storage/verification/download-access-live');mkdirSync(evidence,{recursive:true});
const api='http://localhost:5264/api/v1',origin='http://localhost:4200',transfer='application/vnd.alfalah.transfer';
const hash=bytes=>createHash('sha256').update(bytes).digest('hex').toUpperCase();
async function get(path,headers={}){const r=await fetch(api+path,{headers:{Authorization:'Bearer '+session.token,...headers}});assert.equal(r.status,200);return r;}
async function browse(handle){return (await (await get('/storage/drive'+(handle?'?handle='+encodeURIComponent(handle):''))).json()).data;}
const direct=await get('/school-duty/plans/roster.pdf?termId=1&revision=1');assert.match(direct.headers.get('content-disposition'),/attachment/);const rosterHash=hash(Buffer.from(await direct.arrayBuffer()));
const transported=await get('/school-duty/plans/roster/content?termId=1&revision=1',{Accept:transfer,Origin:origin});
assert.equal(transported.headers.get('content-type'),transfer);assert.equal(transported.headers.get('content-disposition'),null);assert.equal(transported.headers.get('x-alfalah-content-type'),'application/pdf');assert.match(transported.headers.get('access-control-expose-headers'),/X-AlFalah-File-Name/i);assert.equal(hash(Buffer.from(await transported.arrayBuffer())),rosterHash);
assert.equal((await fetch(api+'/school-duty/plans/roster/content?termId=1&revision=1',{headers:{Accept:transfer}})).status,401);
const home=await browse(),root=await browse(home.items[0].handle),library=root.items.find(x=>x.name==='مكتبة المدرسة');assert(library);
const libraryPage=await browse(library.handle);assert(libraryPage.writableFolderId>0);assert.equal(root.writableFolderId,null);
const legacy=(await (await get('/storage/drive/resolve?folderId='+libraryPage.writableFolderId)).json()).data;
assert.equal((await browse(legacy.handle)).writableFolderId,libraryPage.writableFolderId);
const queue=root.items.filter(x=>x.isFolder&&x.name!=='مكتبة المدرسة'&&x.name!=='ملفات المعلمين');let selected=null,reads=0;
while(queue.length&&reads++<24&&!selected){const f=queue.shift(),p=await browse(f.handle);const item=p.items.find(x=>!x.isFolder&&x.mimeType==='application/pdf');if(item)selected={parent:f.handle,item};else queue.push(...p.items.filter(x=>x.isFolder));}
assert(selected,'Expected an existing synthetic archived PDF; no synthetic uploads are performed here');
const storage=await get('/storage/drive/content?handle='+encodeURIComponent(selected.item.handle),{Accept:transfer});assert.equal(storage.headers.get('content-type'),transfer);assert.equal(storage.headers.get('content-disposition'),null);const storageHash=hash(Buffer.from(await storage.arrayBuffer()));
const browser=await chromium.launch({headless:true}),results=[];
try{for(const width of [390,768,1440]){
 const context=await browser.newContext({viewport:{width,height:1000},reducedMotion:'reduce'}),page=await context.newPage(),errors=[],mutations=[];
 page.setDefaultTimeout(30000);page.on('pageerror',e=>errors.push(e.message));page.on('request',r=>{if(r.url().includes('/api/')&&!['GET','OPTIONS'].includes(r.method()))mutations.push(r.method());});
 await page.addInitScript(s=>{sessionStorage.setItem('alfalah_access_token',s.token);sessionStorage.setItem('alfalah_user',JSON.stringify(s.user));},session);
 await page.goto(origin+'/instructor/my-files');await page.waitForURL('**/school-manager/storage');await page.locator('.drive-table').waitFor();
 assert.equal(await page.locator('a[href="/instructor/my-files"]').count(),0);
 await page.goto(origin+'/school-manager/storage?folder='+libraryPage.writableFolderId);await page.locator('app-storage-drive-browser .drive-table').waitFor();assert.equal(await page.locator('app-storage-page').count(),0);const create=page.getByRole('button',{name:'مجلد جديد',exact:true});await expect(create).toBeEnabled();await expect(page.getByRole('button',{name:'رفع ملف',exact:true})).toBeEnabled();await create.click();await expect(page.locator('#drive-create-title')).toBeVisible();await page.getByRole('button',{name:'إلغاء',exact:true}).click();await expect(create).toBeFocused();await page.screenshot({path:resolve(evidence,`library-tools-${width}.png`),fullPage:true});
 await page.goto(origin+'/school-manager/storage?drive='+encodeURIComponent(selected.parent));await page.locator('.drive-item').filter({hasText:selected.item.name}).click();await page.locator('app-duty-pdf-preview canvas').first().waitFor();await page.waitForFunction(()=>{const c=window.ng.getComponent(document.querySelector('app-duty-pdf-preview'));return c&&!c.loading()&&!c.error();});
 await expect(page.locator('.file-metadata')).toBeVisible();const [file]=await Promise.all([page.waitForEvent('download'),page.getByRole('button',{name:'تنزيل النسخة المعروضة',exact:true}).click()]);assert.equal(hash(readFileSync(await file.path())),storageHash);assert.equal(await page.locator('dialog[role="alertdialog"][open]').count(),0);await page.screenshot({path:resolve(evidence,`file-preview-${width}.png`),fullPage:true});
 await page.getByRole('button',{name:'إغلاق المعاينة',exact:true}).click();await page.goto(origin+'/school-duty');const button=page.locator('app-duty-roster-export button');await button.waitFor();const [roster]=await Promise.all([page.waitForEvent('download'),button.click()]);assert.equal(hash(readFileSync(await roster.path())),rosterHash);assert.equal(await page.locator('dialog[open]').count(),0);
 assert.deepEqual(errors,[]);assert.deepEqual(mutations,[]);assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1));results.push({width,actualLocalApi:true,interceptedApi:false,nativeStorageAndRosterDownloads:true,metadataPreview:true,schoolToolsRestored:true,singleSchoolBrowser:true,legacyFolderLinkUsesNativeBrowser:true,dialogFocusReturn:true,personalBookmarkRedirect:true,noPersonalAdminTab:true,noFalseError:true,rosterHash,storageHash});await context.close();
 }writeFileSync(resolve(evidence,'results.json'),JSON.stringify({at:new Date().toISOString(),results,providerWrites:0,migrations:0,actualIdmExtension:'Not installed in automated browser; owner desktop confirmation pending',physicalDevice:'Pending'},null,2));console.log('PASS: real storage/roster bytes, CORS metadata, 401, native downloads, library tools, previews and manager routing at390/768/1440; no writes.');
}finally{await browser.close();}
