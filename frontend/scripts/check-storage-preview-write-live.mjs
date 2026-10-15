// Explicitly bounded real writes: one new school-library verification folder and one synthetic PDF.
// Cleanup trashes only this run's folder after server impact review. Existing files are never targets.
import assert from 'node:assert/strict';
import { chromium, expect } from '@playwright/test';
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { resolve } from 'node:path';
import { randomUUID, createHash } from 'node:crypto';
const session=JSON.parse(readFileSync(resolve('../.audit/roster-source-20261015/session-private.json'),'utf8').replace(/^\uFEFF/,''));
assert.equal(session.user.activeSchoolId,18);assert.equal(session.user.activeSchoolName,'Al-Falah E2E Test School');
const api='http://localhost:5264/api/v1',origin='http://localhost:4200';
const evidence=resolve('../docs/specs/school-file-storage/verification/preview-layout-live'),privateDir=resolve('../.audit/storage-preview-20261015');mkdirSync(evidence,{recursive:true});
const name='C31_Verification_20261015_'+randomUUID().slice(0,8),pdf=readFileSync(resolve('../docs/specs/school-duty/verification/roster-source-pdf/source-eighteen-weeks.pdf'));
const receipt={folderName:name,folderId:null,created:false,uploaded:false,previewMatches:false,cleaned:false};
const save=()=>writeFileSync(resolve(privateDir,'write-receipt-private.json'),JSON.stringify(receipt,null,2));
async function request(path,body){const r=await fetch(api+path,{method:body?'POST':'GET',headers:{Authorization:'Bearer '+session.token,...(body?{'Content-Type':'application/json'}:{})},body:body?JSON.stringify(body):undefined});assert.equal(r.status,200);const result=await r.json();assert(result.isSuccess);return result.data;}
const contextInfo=await request('/storage/context');assert.equal(contextInfo.schoolId,18);assert(contextInfo.canManage&&contextInfo.rootFolderId>0);
const browser=await chromium.launch({headless:true}),context=await browser.newContext({viewport:{width:1440,height:1000},reducedMotion:'reduce'}),page=await context.newPage();page.setDefaultTimeout(60000);
await page.addInitScript(s=>{sessionStorage.setItem('alfalah_access_token',s.token);sessionStorage.setItem('alfalah_user',JSON.stringify(s.user));},session);
try{
 await page.goto(origin+'/school-manager/storage?folder='+contextInfo.rootFolderId);
 await page.locator('.write-actions button').nth(1).click();await page.locator('input[name=folderName]').fill(name);
 const created=page.waitForResponse(r=>r.request().method()==='POST'&&new URL(r.url()).pathname.endsWith('/storage/folders'));
 await page.locator('#drive-create-title').locator('..').locator('button[type=submit]').click();
 const folderResponse=await (await created).json();assert(folderResponse.isSuccess);receipt.folderId=folderResponse.data.id;receipt.created=true;save();
 await page.locator('.drive-item').filter({hasText:name}).click();await expect(page.locator('.write-actions button').first()).toBeEnabled();
 await page.waitForFunction(id=>window.ng.getComponent(document.querySelector('app-storage-drive-browser')).data()?.writableFolderId===id,receipt.folderId);
 const uploaded=page.waitForResponse(r=>r.request().method()==='POST'&&new URL(r.url()).pathname.endsWith('/storage/files'));
 await page.locator('input[type=file]').setInputFiles({name:'C31_upload_verification.pdf',mimeType:'application/pdf',buffer:pdf});
 const response=await uploaded;assert.equal(Number(new URL(response.url()).searchParams.get('parentFolderId')),receipt.folderId);const uploadResponse=await response.json();assert(uploadResponse.isSuccess);assert.equal(uploadResponse.data.status,'Completed');receipt.uploaded=true;save();
 await page.locator('.drive-item').filter({hasText:'C31_upload_verification.pdf'}).waitFor();await page.screenshot({path:resolve(evidence,'real-library-upload.png'),fullPage:true});
 await page.locator('.drive-item').filter({hasText:'C31_upload_verification.pdf'}).click();await page.locator('app-duty-pdf-preview canvas').first().waitFor();
 const [download]=await Promise.all([page.waitForEvent('download'),page.locator('.preview-dialog footer .primary').click()]);
 receipt.previewMatches=createHash('sha256').update(readFileSync(await download.path())).digest('hex')===createHash('sha256').update(pdf).digest('hex');assert(receipt.previewMatches);save();
 console.log('PASS: actual manager UI created a library folder, uploaded a synthetic PDF and read back identical bytes.');
}finally{
 try{if(receipt.created){
  const resolved=await request('/storage/drive/resolve?folderId='+receipt.folderId);
  const impact=await request('/storage/drive/impact',{handle:resolved.handle,action:'Delete'});
  assert.equal(impact.name,name);assert.equal(impact.signedDocuments,0);assert.equal(impact.linkedCount,0);assert.equal(impact.folders,1);assert(impact.files<=1);
  const op=await request('/storage/drive/mutations',{ticket:impact.ticket,requestKey:randomUUID()});assert.equal(op.status,'Completed');receipt.cleaned=true;save();
  const parent=await request('/storage/drive/resolve?folderId='+contextInfo.rootFolderId),listing=await request('/storage/drive?handle='+encodeURIComponent(parent.handle));assert(!listing.items.some(x=>x.name===name));
  console.log('PASS: only this run?s unlinked verification folder/file were trashed through the system and disappeared from the parent listing.');
 }}finally{await browser.close();writeFileSync(resolve(evidence,'write-results.json'),JSON.stringify({at:new Date().toISOString(),school:'Al-Falah E2E Test School',realProvider:true,created:receipt.created,uploaded:receipt.uploaded,previewMatches:receipt.previewMatches,cleaned:receipt.cleaned,originalFilesTargeted:0},null,2));}
}
