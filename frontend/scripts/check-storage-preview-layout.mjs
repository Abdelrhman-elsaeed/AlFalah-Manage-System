// Actual Angular presentation with synthetic intercepted APIs, including extension
// takeover policy replay. No real SQL/provider mutations or installed IDM claim.
import assert from 'node:assert/strict';
import { chromium, expect } from '@playwright/test';
import { readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
const origin='http://localhost:4200',type='application/vnd.alfalah.transfer';
const pdf=readFileSync(resolve('../docs/specs/school-duty/verification/roster-source-pdf/source-eighteen-weeks.pdf'));
const evidence=resolve('../docs/specs/school-file-storage/verification/preview-layout-screens');mkdirSync(evidence,{recursive:true});
const browser=await chromium.launch({headless:true}),results=[];
async function fixture(width,delegate=false){
 const user={userId:'synthetic-file-manager',username:'synthetic-file-manager',fullName:delegate?'مشرف التخزين الاصطناعي':'مدير مدرسة التحقق الاصطناعية',activeSchoolId:18,activeSchoolName:'مدرسة التحقق الاصطناعية',roles:[delegate?'Instructor':'SchoolManager'],preferredLanguage:'ar',permissions:['Storage.ViewSchool','Storage.ManageSchool','Storage.ViewOwn','Storage.ManageOwn','Storage.ReviewEvidence']};
 const context=await browser.newContext({viewport:{width,height:1000},reducedMotion:'reduce'}),page=await context.newPage(),errors=[],keys=[];
 const state={outage:false,uploaded:false,created:false,uploadCalls:0,binary:0};page.on('pageerror',e=>errors.push(e.message));page.setDefaultTimeout(20000);
 await page.addInitScript(user=>{sessionStorage.setItem('alfalah_access_token',`e30.${btoa(JSON.stringify({sub:user.userId,exp:2000000000}))}.synthetic`);sessionStorage.setItem('alfalah_user',JSON.stringify(user));},user);
 const file={storedFileId:9,folderId:17,displayName:'محضر متابعة المدرسة — نسخة اصطناعية.pdf',size:pdf.length,mimeType:'application/pdf',uploadedAt:'2026-10-15T08:00:00Z',state:'Managed',isProtected:false,rowVersion:'v1'};
 const folder={id:17,parentFolderId:7,displayName:'الملفات المدرسية',kind:'SchoolLibrary',rowVersion:'v1'};
 await page.route('**/api/**',async route=>{
  const req=route.request(),url=new URL(req.url()),path=url.pathname;let data={};
  const ok=data=>route.fulfill({json:{isSuccess:true,data,message:'',errors:[]}});
  if(path.endsWith('/auth/me'))return ok(user);
  if(path.endsWith('/auth/schools'))return ok([]);
  if(path.endsWith('/account/signature/requirement'))return ok({required:false,hasSignature:true});
  if(path.endsWith('/storage/access'))return ok({canManage:true,canReviewEvidence:true});
  if(path.endsWith('/storage/context'))return ok({schoolId:18,schoolName:user.activeSchoolName,canManage:true,canReviewEvidence:true,isTeacher:false,connectionState:'Connected',rootFolderId:7,academicYearId:1});
  if(path.endsWith('/drive/resolve'))return ok({handle:'library',openFile:url.searchParams.get('fileId')?{handle:'file',name:file.displayName,isFolder:false,size:pdf.length,mimeType:'application/pdf',canModify:true,storedFileId:9,folderId:17}:null});
  if(path.endsWith('/storage/drive')){const h=url.searchParams.get('handle');return ok({name:h==='archive'?'أرشيف الزيارات':h==='library'?'مكتبة المدرسة':'ملفات المدرسة',handle:h,breadcrumbs:h?[{handle:'root',name:'ملفات المدرسة'},{handle:h,name:'مكتبة المدرسة'}]:[],items:h==='library'?[{handle:'file',name:file.displayName,isFolder:false,size:pdf.length,mimeType:'application/pdf',canModify:!delegate,storedFileId:9,folderId:17,modifiedAt:'2026-10-15T08:00:00Z'},...(state.created?[{handle:'new-folder',name:'ملفات اللجان',isFolder:true,mimeType:'application/vnd.google-apps.folder',canModify:true}]:[])]:[{handle:'library',name:'مكتبة المدرسة',isFolder:true,mimeType:'application/vnd.google-apps.folder',canModify:false}],canManage:!delegate,writableFolderId:h==='library'?17:null,nextPageToken:null});}
  if(path.endsWith('/drive/operations'))return ok([]);
  if(path.endsWith('/content')){
   state.binary++;if(state.outage||req.headers().accept!==type)return route.abort('failed');
   return route.fulfill({body:pdf,headers:{'Content-Type':type,'X-AlFalah-Content-Type':'application/pdf','X-AlFalah-File-Name':encodeURIComponent(file.displayName),'Access-Control-Expose-Headers':'X-AlFalah-Content-Type,X-AlFalah-File-Name'}});
  }
  if(path.endsWith('/storage/files')&&req.method()==='POST'){
   state.uploadCalls++;keys.push(req.headers()['idempotency-key']);assert.equal(url.searchParams.get('parentFolderId'),'17');assert(req.postDataBuffer().equals(pdf));if(state.uploadCalls===1)return route.abort('failed');state.uploaded=true;return ok({operationId:3,status:'Completed',storedFileId:10});
  }
  if(path.endsWith('/storage/folders')&&req.method()==='POST'){assert.equal(req.postDataJSON().parentFolderId,17);assert.equal(req.postDataJSON().displayName,'ملفات اللجان');state.created=true;return ok({id:19,parentFolderId:17,displayName:'ملفات اللجان',kind:'SchoolLibrary',rowVersion:'v1'});}
  if(path.endsWith('/storage/map'))return ok({schoolName:user.activeSchoolName,year:'2026–2027',checkedAt:'2026-10-15T08:00:00Z',registeredFiles:1,spaces:[{key:'library',name:'مكتبة المدرسة',folderId:7,state:'Available',canOpen:true,browseHandle:'library'},{key:'duty',name:'المناوبات',state:'Available',canOpen:true,browseHandle:'archive'}],nodes:[{id:17,parentId:7,name:'1 - الإدارة المدرسية',files:1,state:'Available'},{id:18,parentId:17,name:'1.1 - التخطيط المدرسي',files:0,state:'Available'}]});
  if(path.endsWith('/folders/17/path'))return ok([{id:7,displayName:'مكتبة المدرسة',kind:'SchoolLibrary',rowVersion:'v1'},folder]);
  if(path.endsWith('/folders/18/path'))return ok([{id:7,displayName:'مكتبة المدرسة',kind:'SchoolLibrary',rowVersion:'v1'},folder,{...folder,id:18,parentFolderId:17}]);
  if(path.endsWith('/storage/folders'))return ok({items:[],total:0,page:1,pageSize:25});
  if(path.endsWith('/storage/files/9'))return ok({file,versions:[{versionId:1,versionNumber:1,size:pdf.length,mimeType:'application/pdf',uploadedAt:file.uploadedAt,availability:'Available'}]});
  if(['/links','/change-requests','/academic-years','/evidence-teachers','/requirement-catalog'].some(x=>path.endsWith(x)))return ok([]);
  if(path.endsWith('/storage/files'))return ok({items:[file],total:1,page:1,pageSize:25});
  if(path.endsWith('/folders/17/drive-items'))return ok({items:[],nextPageToken:null});
  if(path.includes('/evidence/')||path.includes('review-queue'))return ok({items:[],total:0});
  if(path.endsWith('/visits/operations-status'))return route.fulfill({status:403,json:{isSuccess:false,message:'Denied'}});
  return ok(data);
 });return {context,page,state,errors,keys};
}
try { for(const width of [390,768,1440]) {
 const x=await fixture(width),{page}=x;
 await page.goto(origin+'/school-manager/storage?drive=archive');
 await page.locator('.drive-write-hint button').waitFor();
 assert.equal(await page.locator('.write-actions').count(),0,'Protected archives must not advertise disabled upload/create');
 await page.screenshot({path:resolve(evidence,`archive-${width}.png`)});
 await page.locator('.drive-write-hint button').click();await page.waitForURL('**folder=7');
 await expect(page.locator('.write-actions button').first()).toBeEnabled();
 await expect(page.locator('.write-actions button').nth(1)).toBeEnabled();
 await page.locator('.write-actions button').nth(1).click();await page.locator('#drive-create-title').waitFor();await page.keyboard.press('Escape');
 await expect(page.locator('.write-actions button').nth(1)).toBeFocused();
 await page.locator('.drive-item').first().click();
 await page.locator('app-duty-pdf-preview canvas').first().waitFor();await page.waitForTimeout(700);
 const measure=()=>page.evaluate(()=>{
 const host=document.querySelector('app-duty-pdf-preview'), stage=host.querySelector('.pdf-pages'), canvas=host.querySelector('canvas'), parent=host.parentElement, dialog=document.querySelector('.preview-dialog');
 return {dialog:dialog.clientWidth,parent:parent.clientWidth,host:host.clientWidth,stage:stage.clientWidth,canvas:canvas.getBoundingClientRect().width,viewportHeight:stage.clientHeight,dialogHeight:dialog.clientHeight,dialogScrollHeight:dialog.scrollHeight};
 });
 const fitted=await measure();console.log(JSON.stringify({width,...fitted}));
 assert(fitted.canvas>fitted.parent*.85,'PDF must fill preview width');assert(fitted.viewportHeight>450,'Document must have useful vertical space');assert(fitted.dialogScrollHeight<=fitted.dialogHeight+1,'Dialog itself must not scroll');
 await page.screenshot({path:resolve(evidence,`preview-${width}.png`)});
 await page.locator('app-duty-pdf-preview button').filter({has:page.locator('.pi-plus')}).click();await page.waitForTimeout(700);assert((await measure()).canvas>fitted.canvas*1.2,'Zoom must enlarge the page');
 await page.locator('.fit-width').click();await page.waitForTimeout(700);assert(Math.abs((await measure()).canvas-fitted.canvas)<3,'Fit width must reset zoom');
 await page.locator('.preview-sections button').nth(1).click();await page.locator('app-storage-file-inspector .version-row').waitFor();await page.waitForTimeout(300);await page.locator('.preview-sections button').first().click();await page.waitForTimeout(700);assert((await measure()).canvas>fitted.parent*.85,'Returning from hidden inspector must retain full width');
 await page.locator('.preview-window-actions button').first().click();await page.waitForTimeout(700);const expanded=await measure();assert(expanded.dialog>=width-2,'Expanded viewer must occupy viewport');assert(expanded.canvas>expanded.parent*.85);await page.screenshot({path:resolve(evidence,`expanded-${width}.png`)});await page.locator('.preview-window-actions button').first().click();await page.waitForTimeout(700);
 await page.keyboard.press('Escape');await expect(page.locator('.preview-dialog')).not.toBeVisible();await expect(page.locator('.drive-item').first()).toBeFocused();assert.deepEqual(x.errors,[]);await page.mouse.move(width/2,990);await page.waitForTimeout(350);assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1));
 results.push({width,fitted,expanded,archiveWriteControlsAbsent:true,openLibraryEnabledTools:true,fitWidth:true,zoom:true,inspectorReturn:true,escapeFocus:true,syntheticInterceptedApi:true});await x.context.close();
}writeFileSync(resolve(evidence,'results.json'),JSON.stringify({at:new Date().toISOString(),results,providerWrites:0},null,2));console.log('PASS: preview sizing/zoom/expand/inspector return and archive-to-library tools at 390/768/1440'); } finally {await browser.close();}
