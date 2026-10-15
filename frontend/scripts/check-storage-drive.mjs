// Current Angular UI, synthetic intercepted APIs. No Google writes or operational SQL.
import assert from 'node:assert/strict';
import { chromium } from '@playwright/test';
import { mkdirSync,readFileSync,writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
const evidence=resolve('../docs/specs/school-file-storage/verification/drive-browser-screens');mkdirSync(evidence,{recursive:true});
const pdf=readFileSync(resolve('../docs/specs/school-file-storage/verification/storage-live-screens/actual-authenticated-letter-390.pdf'));
const browser=await chromium.launch({headless:true});const results=[];
try{for(const width of [390,768,1440]){
 const context=await browser.newContext({viewport:{width,height:1000},locale:'ar-SA'});const page=await context.newPage();const errors=[];page.on('pageerror',e=>errors.push(e.message));
 const user={userId:'synthetic-storage-manager',username:'synthetic-storage-manager',fullName:'مدير اختبار مساحة الملفات',activeSchoolId:18,activeSchoolName:'مدرسة التحقق الاصطناعية',roles:['SchoolManager'],preferredLanguage:'ar',permissions:['Storage.ViewSchool','Storage.ManageSchool','Storage.ViewArchive','Duty.View','Duty.Manage']};
 await page.addInitScript(user=>{sessionStorage.setItem('alfalah_access_token',`e30.${btoa(JSON.stringify({sub:user.userId,exp:2000000000}))}.synthetic`);sessionStorage.setItem('alfalah_user',JSON.stringify(user));localStorage.setItem('alfalah_language','ar');},user);
 const state={live:true,name:'تقرير اصطناعي طويل الاسم لإثبات ارتباط الملف بالشواهد.pdf',operations:[],plans:new Map(),mutations:0,content:0,denied:false,linked:true,lost:false};
 const folder=(handle,name)=>({handle,name,isFolder:true,size:null,mimeType:'application/vnd.google-apps.folder',modifiedAt:'2026-10-15T08:00:00Z',canModify:handle!=='h-root',protection:null});
 const file=()=>({handle:'h-file',name:state.name,isFolder:false,size:pdf.length,mimeType:'application/pdf',modifiedAt:'2026-10-15T08:00:00Z',canModify:true,protection:null});
 function listing(handle){
  if(!handle)return{name:'ملفات المدرسة',handle:null,breadcrumbs:[],items:[folder('h-root','ملفات المدرسة')],nextPageToken:null,canManage:true};
  const root={handle:'h-root',name:'ملفات المدرسة'};const names={'h-library':'مكتبة المدرسة','h-duty':'المناوبات','h-teachers':'ملفات المعلمين','h-visits':'أرشيف الزيارات','h-extra':'مجلد إضافي','h-dest':'وجهة النقل'};
  return{name:handle==='h-root'?'ملفات المدرسة':names[handle],handle,breadcrumbs:handle==='h-root'?[root]:[root,{handle,name:names[handle]}],items:handle==='h-root'?[folder('h-library',names['h-library']),folder('h-teachers',names['h-teachers']),folder('h-duty',names['h-duty']),folder('h-visits',names['h-visits']),folder('h-extra',names['h-extra'])]:handle==='h-library'?[...(state.live?[file()]:[]),folder('h-dest','وجهة النقل')]:handle==='h-duty'?[file()]:[],nextPageToken:null,canManage:true};
 }
 await page.route('**/api/**',async route=>{const req=route.request(),url=new URL(req.url()),path=url.pathname;let data={};
  if(path.endsWith('/auth/me'))data=user;else if(path.endsWith('/auth/schools'))data=[];
  else if(path.endsWith('/account/signature/requirement'))data={required:false,hasSignature:true};
  else if(path.endsWith('/storage/access'))data={canManage:true,canReviewEvidence:true};
  else if(path.endsWith('/storage/context'))data={schoolId:18,schoolName:user.activeSchoolName,canManage:true,connectionState:'Connected',rootFolderId:7};
  else if(path.endsWith('/storage/drive')){if(state.denied)return route.fulfill({status:403,json:{isSuccess:false,message:'Synthetic denied'}});data=listing(url.searchParams.get('handle'));}
  else if(path.endsWith('/drive/content')){state.content++;return route.fulfill({body:pdf,contentType:'application/pdf',headers:{'Content-Disposition':'attachment; filename=synthetic.pdf'}});}
  else if(path.endsWith('/drive/operations'))data=state.operations.filter(x=>x.status!=='Completed');
  else if(path.endsWith('/drive/deleted'))data=!state.live?[{id:1,status:'Completed',action:'Delete',name:state.name}]:[];
  else if(path.endsWith('/drive/impact')){const body=req.postDataJSON(),ticket='confirmed-'+state.plans.size;state.plans.set(ticket,body);data={ticket,name:state.name,action:body.action,files:1,folders:0,linkedTo:state.linked?['المعيار ١.١ — شاهد معتمد مرتبط بالملف']:[],linkedCount:state.linked?1:0,signedDocuments:0,warning:state.linked?'سيُزال الملف من الشواهد النشطة المرتبطة به، مع بقاء سجل المراجعات.':'سينتقل الملف إلى مهملات Google Drive.'};}
  else if(path.endsWith('/drive/mutations')){state.mutations++;const body=state.plans.get(req.postDataJSON().ticket);assert(body);if(body.action==='Delete'){state.live=false;state.linked=false;}if(body.action==='Rename')state.name=body.name;data={id:1,status:state.lost?'NeedsAttention':'Completed',action:body.action,name:state.name};state.operations=[data];}
  else if(path.endsWith('/reconcile')){state.operations=[];data={id:1,status:'Completed',action:'Delete',name:state.name};}
  else if(path.endsWith('/restore')){state.live=true;data={id:2,status:'Completed',action:'Restore',name:state.name};}
  return route.fulfill({json:{isSuccess:true,data}});
 });
 await page.goto('http://localhost:4200/school-manager/storage');await page.locator('.drive-item').waitFor();assert.equal(await page.locator('.space-card').count(),0);assert.equal(await page.locator('.drive-table tbody tr').count(),1);
 await page.locator('.drive-item').click();await page.waitForURL('**drive=h-root');assert.equal(await page.locator('.drive-table tbody tr').count(),5);
 await page.screenshot({path:resolve(evidence,`school-folders-${width}.png`),fullPage:true});
 for(const name of ['ملفات المعلمين','المناوبات','أرشيف الزيارات']){await page.locator('.drive-item').filter({hasText:name}).click();assert.equal(new URL(page.url()).pathname,'/school-manager/storage');await page.locator('.drive-path button').filter({hasText:'ملفات المدرسة'}).click();await page.waitForURL('**drive=h-root');}
 await page.locator('.drive-item').filter({hasText:'مكتبة المدرسة'}).click();await page.waitForURL('**drive=h-library');await page.locator('.drive-item').filter({hasText:state.name}).waitFor();
 const download=page.waitForEvent('download');await page.getByRole('button',{name:'تنزيل · '+state.name,exact:true}).click();assert((await download).suggestedFilename().endsWith('.pdf'));
 await page.locator('.drive-item').filter({hasText:state.name}).click();await page.locator('app-duty-pdf-preview canvas').first().waitFor();await page.waitForFunction(()=>!window.ng.getComponent(document.querySelector('app-duty-pdf-preview')).loading());await page.getByRole('button',{name:'إغلاق المعاينة',exact:true}).click();
 const deleteButton=page.getByRole('button',{name:'حذف · '+state.name,exact:true});await deleteButton.focus();await page.keyboard.press('Enter');await page.getByText('المعيار ١.١ — شاهد معتمد مرتبط بالملف',{exact:true}).waitFor();const dialogBox=await page.locator('dialog[aria-labelledby="drive-action-title"]').boundingBox();assert(dialogBox);assert(Math.abs(dialogBox.y+dialogBox.height/2-500)<8);await page.screenshot({path:resolve(evidence,`linked-delete-${width}.png`),fullPage:true});
 await page.getByRole('button',{name:'إلغاء',exact:true}).click();assert.equal(state.mutations,0);assert(await deleteButton.evaluate(e=>e===document.activeElement));
 await deleteButton.click();await page.getByText('المعيار ١.١ — شاهد معتمد مرتبط بالملف',{exact:true}).waitFor();state.lost=true;await page.getByRole('button',{name:'نعم، حذف من Drive',exact:true}).click();await page.getByRole('button',{name:'التحقق من النتيجة',exact:true}).waitFor();assert.equal(state.mutations,1);
 await page.getByRole('button',{name:'التحقق من النتيجة',exact:true}).click();await page.waitForFunction(()=>window.ng.getComponent(document.querySelector('app-storage-drive-browser')).operations().length===0);assert.equal(state.mutations,1);
 await page.getByRole('button',{name:'المحذوفات',exact:true}).click();await page.getByRole('button',{name:'استعادة',exact:true}).waitFor();await page.getByRole('button',{name:'استعادة',exact:true}).click();await page.getByText('لا توجد عناصر محذوفة عبر النظام.',{exact:true}).waitFor();await page.locator('dialog[aria-labelledby="drive-deleted-title"]').getByRole('button',{name:'إغلاق',exact:true}).click();assert.equal(state.linked,false);
 await page.getByRole('button',{name:'تعديل الاسم · '+state.name,exact:true}).click();await page.locator('.rename-field input').fill('ملف بعد تعديل الاسم.pdf');state.lost=false;await page.getByRole('button',{name:'حفظ',exact:true}).click();await page.locator('.drive-item').filter({hasText:'ملف بعد تعديل الاسم.pdf'}).waitFor();
 assert(await page.locator('.drive-browser').evaluate(e=>e.scrollWidth<=e.clientWidth+1));await page.screenshot({path:resolve(evidence,`native-files-${width}.png`),fullPage:true});
 state.denied=true;await page.getByRole('button',{name:'تحديث الملفات',exact:true}).click();await page.locator('.drive-empty[role="alert"]').waitFor();assert.equal(await page.locator('.drive-table').count(),0);assert.deepEqual(errors,[]);
 results.push({width,virtualSchoolFolder:true,actualNativeFolderNavigation:true,noFeatureRedirect:true,linkedDeleteConfirmation:true,cancelFocusReturn:true,unknownResultReconciledWithoutSecondDelete:true,restoreLeavesEvidenceWithdrawn:true,rename:true,authenticatedBlobDownload:true,pdfCanvasRendered:true,noOverflow:true,deniedClearsData:true,api:'Synthetic intercepted fixtures only'});await context.close();
 }writeFileSync(resolve(evidence,'browser-results.json'),JSON.stringify({at:new Date().toISOString(),results,driveWrites:0,physicalDevice:'Pending',ownerVisualReview:'Pending'},null,2));console.log('Native folder browsing, linked deletion, recovery, download and rendered preview passed at390/768/1440.');
}finally{await browser.close();}
