import assert from 'node:assert/strict';
import {chromium,expect} from '@playwright/test';
import {readFileSync,writeFileSync,mkdirSync} from 'node:fs';
import {createHash} from 'node:crypto';
import {fileURLToPath} from 'node:url';
import {resolve} from 'node:path';
const root=fileURLToPath(new URL('../../',import.meta.url));
const privateState=JSON.parse(readFileSync(resolve(root,'.audit/assignment-statement/isolated-private.json'),'utf8'));
if(!/(?:Database|Initial Catalog)=AlFalahStatementTests_[a-f0-9]{32}(?:;|$)/i.test(privateState.connection))throw new Error('An isolated statement SQL test connection is required.');
const session=JSON.parse(readFileSync(resolve(root,'.audit/classroom-consumers/manager-session-private.json'),'utf8'));
const output=resolve(root,'docs/specs/classroom-catalog/verification/statement');mkdirSync(output,{recursive:true});
const endpoint='/api/v1/intelligent-timetable/settings/profiles/1/assignments';
async function api(path,options={}){return fetch('http://localhost:5266'+path,{...options,headers:{Authorization:'Bearer '+privateState.managerToken,...options.headers}});}
const hash=bytes=>createHash('sha256').update(bytes).digest('hex');
let overview=await (await api(endpoint)).json();assert.equal(overview.isSuccess,true);
const first=await api(endpoint+`/statement/content?revision=${overview.data.revision}&scope=all`,{headers:{Accept:'application/vnd.alfalah.transfer'}});assert.equal(first.status,200);
assert.equal(first.headers.get('content-disposition'),null);assert.equal(first.headers.get('content-type'),'application/vnd.alfalah.transfer');assert.equal(first.headers.get('x-alfalah-content-type'),'application/pdf');
const bytes=Buffer.from(await first.arrayBuffer());assert.equal(bytes.subarray(0,4).toString(),'%PDF');
const again=await api(endpoint+`/statement/content?revision=${overview.data.revision}&scope=all`);assert.equal(again.status,200);assert.ok(again.headers.get('content-disposition'));assert.equal(hash(Buffer.from(await again.arrayBuffer())),hash(bytes));
assert.equal((await api(endpoint+'/statement/content?revision=1&scope=all')).status,409);
assert.equal((await api(endpoint+`/statement/content?revision=${overview.data.revision}&scope=search`)).status,400);
assert.equal((await api('/api/v1/intelligent-timetable/settings/profiles/999999/assignments/statement/content?revision=1&scope=all')).status,404);
assert.equal((await api(endpoint+`/statement/content?revision=${overview.data.revision}&scope=all`,{headers:{Authorization:'Bearer '+privateState.teacherToken}})).status,403);
writeFileSync(resolve(output,'sql-http.pdf'),bytes);
const localSettings=await (await fetch('http://localhost:5264/api/v1/intelligent-timetable/settings',{headers:{Authorization:'Bearer '+session.token}})).json();assert.equal(localSettings.isSuccess,true);
const browser=await chromium.launch({headless:true}),results=[];
try{
  for(const width of [390,768,1440]){
    const context=await browser.newContext({viewport:{width,height:1000},acceptDownloads:true,reducedMotion:'reduce'});
    await context.addInitScript(s=>{sessionStorage.setItem('alfalah_access_token',s.token);sessionStorage.setItem('alfalah_user',JSON.stringify(s.user));},session);
    const page=await context.newPage(),errors=[],writes=[];page.on('pageerror',error=>errors.push(error.message));
    // Real manager reads from the existing school; no operational assignment writes.
    await page.goto('http://localhost:4200/intelligent-timetable/assignments');await expect(page.locator('.assignment-cell').first()).toBeVisible();
    await page.getByRole('button',{name:'بيان الإسنادات PDF',exact:true}).click();await expect(page.locator('.statement-context')).toBeVisible();
    assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),true);await page.screenshot({path:resolve(output,'live-options-'+width+'.png')});
    await page.locator('.statement-footer').getByRole('button',{name:'إغلاق',exact:true}).click();await expect(page.locator('#assignment-statement-trigger')).toBeFocused();
    // The authenticated shell now hosts the isolated SQL API workspace. Only this API prefix is proxied.
    await context.route('**/api/v1/intelligent-timetable/settings**',async route=>{
      const request=route.request(),url=new URL(request.url());
      if(url.pathname==='/api/v1/intelligent-timetable/settings'){
        const profile={...localSettings.data.selectedProfile,id:1,name:'Synthetic SQL statement'};
        return route.fulfill({contentType:'application/json',body:JSON.stringify({...localSettings,data:{...localSettings.data,selectedAcademicYearId:1,selectedProfile:profile,profiles:[profile]}})});
      }
      if(['PUT','POST','DELETE','PATCH'].includes(request.method()))writes.push(request.method());
      const response=await api(url.pathname+url.search,{method:request.method(),body:request.postData()||undefined,headers:{Origin:'http://localhost:4200','Content-Type':request.headers()['content-type']||'application/json',Accept:request.headers()['accept']||'application/json'}});
      const headers={};for(const name of ['content-type','content-disposition','x-alfalah-content-type','x-alfalah-file-name','x-assignment-revision','x-assignment-scope','etag','cache-control','access-control-allow-origin','access-control-allow-credentials','access-control-expose-headers']){const value=response.headers.get(name);if(value)headers[name]=value;}
      return route.fulfill({status:response.status,headers,body:Buffer.from(await response.arrayBuffer())});
    });
    await page.goto('http://localhost:4200/intelligent-timetable/assignments?profileId=1');await expect(page.locator('.assignment-cell')).toHaveCount(1);
    await page.getByRole('button',{name:'بيان الإسنادات PDF',exact:true}).click();await expect(page.locator('.statement-context')).toBeVisible();
    await page.locator('.classifications summary').click();await expect(page.locator('.classifications tbody tr')).toHaveCount(1);
    const select=page.locator('.classifications tbody select'),previous=await select.inputValue(),category=previous==='Other'?'Specialization':'Other';await select.selectOption(category);
    page.once('dialog',dialog=>dialog.dismiss());await page.locator('#statement-scope').selectOption('stage:2');await expect(page.locator('#statement-scope')).toHaveValue('all');
    await expect(page.getByRole('button',{name:'تجهيز ومعاينة PDF',exact:true})).toBeDisabled();await page.getByRole('button',{name:'حفظ التصنيفات',exact:true}).click();
    await expect(page.getByRole('button',{name:'تجهيز ومعاينة PDF',exact:true})).toBeEnabled();
    if(width===390){
      const latest=await (await api(endpoint)).json();const saved=await api(endpoint,{method:'PUT',headers:{'Content-Type':'application/json'},body:JSON.stringify({revision:latest.data.revision,changes:latest.data.assignments})});assert.equal(saved.status,200);
      await page.getByRole('button',{name:'تجهيز ومعاينة PDF',exact:true}).click();await expect(page.locator('.statement-error')).toContainText('تغيّرت الإسنادات');
      await page.getByRole('button',{name:'تحميل النسخة الأحدث',exact:true}).click();await expect(page.locator('.statement-context')).toHaveCount(0);
      await expect(page.locator('.assignment-cell')).toHaveCount(1);await page.getByRole('button',{name:'بيان الإسنادات PDF',exact:true}).click();
    }
    await page.getByRole('button',{name:'تجهيز ومعاينة PDF',exact:true}).click();await expect(page.locator('.statement-preview canvas').first()).toBeVisible({timeout:60000});
    await expect(page.getByRole('button',{name:'تنزيل النسخة المعروضة',exact:true})).toBeEnabled();
    assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),true);await page.screenshot({path:resolve(output,'preview-'+width+'.png')});
    const downloadPromise=page.waitForEvent('download');await page.getByRole('button',{name:'تنزيل النسخة المعروضة',exact:true}).click();const download=await downloadPromise;
    const chunks=[];for await(const chunk of await download.createReadStream())chunks.push(chunk);const downloaded=Buffer.concat(chunks);
    overview=await (await api(endpoint)).json();const current=await api(endpoint+`/statement/content?revision=${overview.data.revision}&scope=all`);assert.equal(current.status,200);assert.equal(hash(downloaded),hash(Buffer.from(await current.arrayBuffer())));
    writeFileSync(resolve(output,'sql-http.pdf'),downloaded);
    assert.deepEqual(errors,[]);results.push({width,realSchoolOptionsReadOnly:true,isolatedSqlClassificationSave:true,scopeDiscardRestoresControl:true,revisionConflictRecovery:width===390,actualPdfCanvas:true,downloadMatchesRetainedBytes:true,noPageOverflow:true,noPageErrors:true,isolatedWriteRequests:writes.length});await context.close();
  }
  writeFileSync(resolve(output,'live-results.json'),JSON.stringify({at:new Date().toISOString(),sqlHttp:{generated:true,retainedBytes:true,browserTransfer:true,stale409:true,invalidScope400:true,missing404:true,teacher403:true},results},null,2));
  console.log('PASS: isolated SQL HTTP export/retention/auth/transfer; real-school read-only options and classification/conflict/PDF/native downloads at390/768/1440.');
}finally{await browser.close();}
