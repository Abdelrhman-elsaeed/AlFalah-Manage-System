// Actual Angular presentation with synthetic intercepted APIs, including extension
// takeover policy replay. No real SQL/provider mutations or installed IDM claim.
import assert from 'node:assert/strict';
import { chromium, expect } from '@playwright/test';
import { readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
const origin='http://localhost:4200',type='application/vnd.alfalah.transfer';
const pdf=readFileSync(resolve('../docs/specs/school-duty/verification/roster-source-pdf/source-eighteen-weeks.pdf'));
const evidence=resolve(process.env.ALFALAH_STORAGE_BROWSER_EVIDENCE||'../docs/specs/school-file-storage/verification/download-access-screens');mkdirSync(evidence,{recursive:true});
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
try{for(const width of [390,768,1440]){
 const x=await fixture(width),{page,state}=x;
 await page.goto(origin+'/school-manager/storage?drive=library');await page.locator('.drive-table tbody tr').first().waitFor();assert.equal(await page.locator('a[href="/instructor/my-files"]').count(),0);
 await page.getByRole('button',{name:'مجلد جديد',exact:true}).click();await page.locator('input[name="folderName"]').fill('ملفات اللجان');await page.getByRole('button',{name:'إنشاء المجلد',exact:true}).click();await page.locator('.drive-item').filter({hasText:'ملفات اللجان'}).waitFor();assert(state.created);
 await page.locator('input[type=file]').setInputFiles({name:'شاهد اصطناعي.pdf',mimeType:'application/pdf',buffer:pdf});await page.locator('#drive-error-title').waitFor();await page.getByRole('button',{name:'موافق',exact:true}).click();await page.getByRole('button',{name:'التحقق وإعادة المحاولة',exact:true}).click();await page.getByText('تم رفع الملف وحفظه في سجل المدرسة.').waitFor();assert.equal(x.keys[0],x.keys[1]);assert(state.uploaded);
 await page.locator('.drive-item').filter({hasText:'محضر متابعة المدرسة'}).click();await page.locator('app-duty-pdf-preview canvas').first().waitFor();await expect(page.locator('.file-metadata')).toBeVisible();assert.equal(await page.locator('dialog[role=alertdialog][open]').count(),0);await page.screenshot({path:resolve(evidence,`metadata-preview-${width}.png`),fullPage:true});
 await page.getByRole('button',{name:'تفاصيل الملف والشواهد والإصدارات',exact:true}).click();await page.locator('app-storage-file-inspector .version-row').waitFor();assert.equal(await page.locator('app-storage-page').count(),0);await page.screenshot({path:resolve(evidence,`existing-ledger-details-${width}.png`),fullPage:true});
 await page.getByRole('button',{name:'روابط الشواهد',exact:true}).click();await page.locator('app-storage-evidence').first().waitFor();await page.getByRole('button',{name:'تعديل الملف',exact:true}).click();await page.locator('.rename-form').waitFor();await page.getByRole('button',{name:'إغلاق',exact:true}).click();
 await page.goto(origin+'/school-manager/storage?drive=library');state.outage=true;await page.locator('.drive-item').filter({hasText:'محضر متابعة المدرسة'}).click();await expect(page.locator('dialog[role=alertdialog][open]')).toBeVisible();await page.screenshot({path:resolve(evidence,`real-network-error-${width}.png`),fullPage:true});state.outage=false;
 await page.goto(origin+'/school-manager/storage?display=map');await page.locator('.domain-grid').waitFor();assert.equal(await page.getByText('عرض الملفات',{exact:true}).count(),0);await page.screenshot({path:resolve(evidence,`clickable-map-${width}.png`),fullPage:true});
 await page.locator('.root-spaces article').first().click({position:{x:30,y:45}});await page.waitForURL('**drive=library');
 await page.goto(origin+'/school-manager/storage?display=map');await page.locator('.domain>header').first().waitFor();await page.locator('.domain-actions button').first().click();await expect(page.locator('.standard-rail').first()).toBeHidden();assert(new URL(page.url()).searchParams.get('display')==='map');await page.locator('.domain-actions button').first().click();if(width===768){await page.locator('.standard-node a').first().focus();await page.keyboard.press('Enter');}else await page.locator('.standard-node').first().click({position:{x:30,y:30}});await page.waitForURL('**folder=18');await page.locator('app-storage-drive-browser .drive-table').waitFor();assert.equal(await page.locator('app-storage-page').count(),0);await page.screenshot({path:resolve(evidence,`unified-folder-${width}.png`),fullPage:true});
 await page.goto(origin+'/school-manager/storage?display=map');await page.locator('.domain>header').first().click({position:{x:30,y:30}});await page.waitForURL('**folder=17');await page.locator('app-storage-drive-browser .drive-table').waitFor();assert.equal(await page.locator('app-storage-page').count(),0);await page.goto(origin+'/school-manager/storage?file=9&folder=17');await page.locator('app-duty-pdf-preview canvas').first().waitFor();assert.equal(await page.locator('app-storage-page').count(),0);assert.deepEqual(x.errors,[]);assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1));results.push({width,syntheticInterceptedApi:true,extensionTakeoverPolicyReplay:true,metadataAndExistingLedger:true,singleSchoolBrowser:true,legacyFolderAndFileLinks:true,uploadLostResponseSameIdentity:true,existingFolderCreation:true,trueNetworkFailureVisible:true,wholeCardsClickable:true,collapseIndependent:true,adminOwnTabHidden:true});await x.context.close();
 }
 const delegate=await fixture(1440,true);await delegate.page.goto(origin+'/school-manager/storage?drive=library');await expect(delegate.page.getByRole('button',{name:'رفع ملف',exact:true})).toBeEnabled();assert.equal(await delegate.page.locator('a[href="/instructor/my-files"]').count(),0);await delegate.page.screenshot({path:resolve(evidence,'instructor-storage-delegate.png'),fullPage:true});await delegate.context.close();
 writeFileSync(resolve(evidence,'results.json'),JSON.stringify({at:new Date().toISOString(),results,instructorDelegate:true,realProviderWrites:0,physicalDevice:'Pending',actualIdmExtension:'Not installed; takeover policy replay only'},null,2));console.log('PASS: synthetic Angular workflows and takeover-policy replay at390/768/1440, existing ledger, create/upload retry, full cards and Instructor storage delegate.');
}finally{await browser.close();}
