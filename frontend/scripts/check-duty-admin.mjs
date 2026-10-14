// Actual running Angular UI. Synthetic intercepted APIs; never touches operational data.
import assert from 'node:assert/strict';
import { readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { createHash } from 'node:crypto';
import { chromium } from '@playwright/test';
const origin = process.env.ALFALAH_DUTY_WORKSPACE_ORIGIN || 'http://localhost:4200';
const evidence = resolve(process.env.ALFALAH_DUTY_ADMIN_EVIDENCE||'../docs/specs/school-duty/verification/admin-screens');
mkdirSync(evidence,{recursive:true});
const pdfRoot = resolve('../docs/specs/school-duty/verification/d3-pdf');
const browser = await chromium.launch({headless:true}); const results=[];
const teachers=[{profileId:1,userId:'teacher',name:'عبد الرحمن محمد عبد الله عبد الرحمن الطويل · #1'},{profileId:2,userId:'other',name:'أحمد محمد العتيبي · #2'},{profileId:3,userId:'third',name:'أحمد محمد العتيبي · #3'}];
teachers.push(...Array.from({length:102},(_,i)=>({profileId:i+4,userId:'teacher-'+(i+4),name:'معلم التحقق الاصطناعي عبد الرحمن محمد عبد الله · '+(i+4)})));
const day={id:1,date:'2026-10-13',originalTeacherId:'other',originalTeacher:teachers[1].name,teacherId:'teacher',teacher:teachers[0].name,revision:2,protected:false,locked:false,exceptionCode:null,attendance:'Unknown',isMine:true,changeKind:'AutoExchange'};
const header={school:'مدرسة التحقق الاصطناعية',city:'التحقق',header:'وزارة التعليم · مدرسة التحقق الاصطناعية',footer:null,logoUrl:null,manager:'مدير التحقق الاصطناعي',year:'2026–2027',semester:1,originalTeacher:teachers[1].name,actualTeacher:teachers[0].name,isSubstitute:true};
async function setup(role,width,lang='ar'){
 const page=await browser.newPage({viewport:{width,height:1000},reducedMotion:'reduce'});const errors=[],calls=[];
 page.on('pageerror',error=>errors.push(error.message));
 const manager=role!=='Instructor';const user={userId:manager?'manager':'teacher',fullName:'حساب التحقق الاصطناعي',username:'synthetic',activeSchoolId:1,activeSchoolName:header.school,roles:[role],permissions:['Duty.View','Timetable.View','StudentAffairsDashboard.Officer',...(manager?['Duty.Manage','Storage.ManageSchool','Storage.ViewSchool']:[])],preferredLanguage:lang};
 const signature=await page.evaluate(()=>{const c=document.createElement('canvas');c.width=300;c.height=100;const x=c.getContext('2d');x.strokeStyle='#214737';x.lineWidth=2;x.beginPath();x.moveTo(20,70);x.bezierCurveTo(100,10,150,90,270,30);x.stroke();return c.toDataURL();});
 const state={previewFail:false,pickerFail:false,downloadFail:false,denyIndex:false,indexEmpty:false,retries:[],saves:[],signs:[],previews:[],publications:[],pdfReads:0,
  execution:{assignmentId:1,assignmentRevision:2,date:day.date,canExecute:!manager,canManage:manager,header,dismissalAt:'13:30:00',timingException:null,signatureData:signature,acknowledged:manager,draft:null,lateStudents:[],documents:[],priorDrafts:[]},
  settings:null,revision:2,archive:{enabled:false,processingEnabled:true,connectionState:'Disconnected'},archiveWrites:[]};
 const doc={documentId:1,kind:'Letter',assignmentRevision:2,signedAt:'2026-10-13T10:00:00Z',previousDocumentId:null,correctionReason:null,artifactState:'Ready',archiveState:'Disconnected',canRetry:false,attempts:0,nextAttemptAt:null,verifiedAt:null,errorCode:null,authorName:teachers[0].name,dutyDate:day.date,isCurrent:true};
 await page.addInitScript(({user,lang})=>{sessionStorage.setItem('alfalah_access_token',`e30.${btoa(JSON.stringify({sub:user.userId,exp:2000000000}))}.fixture`);sessionStorage.setItem('alfalah_user',JSON.stringify(user));localStorage.setItem('alfalah_language',lang);localStorage.setItem('language',lang);},{user,lang});
 await page.route('**/api/**',async route=>{
  const request=route.request(),path=new URL(request.url()).pathname;calls.push(path);let data={};const body=()=>request.postDataJSON();
  if(path.endsWith('/auth/me'))data=user;
  else if(path.endsWith('/account/signature/requirement'))data={required:false,hasSignature:true};
  else if(path.endsWith('/auth/schools'))data=[];
  else if(path.endsWith('/storage/access'))data={canManage:true,canReviewEvidence:true};
  else if(path.endsWith('/storage/context'))data={schoolId:1,schoolName:header.school,academicYearName:header.year,canManage:true,isTeacher:false,connectionState:'Disconnected'};
  else if(path.endsWith('/storage/setup'))data={folders:[],teachers:[],archiveEnabled:false,archiveCapabilityReady:false};
  else if(path.endsWith('/archive/enablement')){if(request.method()==='PUT'){state.archiveWrites.push(body());state.archive.enabled=body().enabled;}data=state.archive;}
  else if(path.endsWith('/school-duty/context'))data={canManage:manager,canDelegate:role==='SchoolManager',school:header.school,today:state.schoolToday||day.date,userId:user.userId,terms:[{id:1,yearId:1,year:header.year,semester:1,from:'2026-10-01',to:'2026-12-31'}],termId:1,planRevision:state.revision,settings:state.settings,teachers:manager?teachers:[],suggestedWeekdays:[0,1,2,3,4],days:(state.scheduleDays||[day]).map(x=>({...x,isMine:!manager&&x.teacherId===user.userId})),history:manager?[{id:1,kind:'Publish',date:day.date,otherDate:null,before:'',after:'',otherBefore:null,otherAfter:null,reason:'Publication',actor:'manager',at:'2026-10-13T08:00:00Z',relatedChangeId:null,unresolved:false,beforeTeacherId:'',afterTeacherId:''}]:[],fairness:manager?teachers.map(x=>({userId:x.userId,name:x.name,planned:3,effective:3,covered:0,completed:0,pendingCompensation:0})):[],pendingRetries:0,closures:[],ownHistory:manager?[]:[{assignmentId:1,date:day.date,authorUserId:'teacher',assignmentRevision:2,acknowledged:true,submitted:false,savedAt:null,submittedAt:null}]};
  else if(path.endsWith('/plans/preview')){
   state.previews.push(body());if(state.previewFail)return route.fulfill({status:409,json:{isSuccess:false,message:'تغيّر التوزيع؛ حدّث البيانات وأعد المعاينة. / The schedule changed; refresh and preview again.'}});
   data={digest:'synthetic-digest',days:[{date:day.date,teacherId:'teacher',teacher:teachers[0].name,before:null,preserved:false,exceptionCode:null}],fairness:teachers.map(x=>({userId:x.userId,name:x.name,planned:1,effective:1,covered:0,completed:0,pendingCompensation:0})),warnings:[],changedDates:1};
  }else if(path.endsWith('/plans/publish')){state.publications.push(body());state.settings=body().settings;data={planId:1,revision:++state.revision};}
  else if(path.endsWith('/execution'))data=state.execution;
  else if(path.endsWith('/acknowledge')){state.signs.push(body());state.execution.acknowledged=true;state.execution.documents=[{id:1,kind:'Letter',assignmentRevision:2,authorUserId:'teacher',signedAt:doc.signedAt,signatureHash:'synthetic',previousDocumentId:null,correctionReason:null}];data=state.execution;}
  else if(path.endsWith('/report/draft')){const b=body();state.saves.push(b);state.execution.draft={id:1,version:(state.execution.draft?.version??0)+1,assignmentRevision:2,authorUserId:'teacher',content:b.content,savedAt:'2026-10-13T12:00:00Z',submittedAt:null,correctionReason:null};data=state.execution;}
  else if(path.endsWith('/report/submit')){state.signs.push(body());state.execution.draft.submittedAt='2026-10-13T12:01:00Z';data=state.execution;}
  else if(path.endsWith('/students')){
   if(state.pickerFail)return route.fulfill({status:503,json:{isSuccess:false,message:'تعذر تحميل الطلاب؛ أعد المحاولة. / Student loading failed; try again.'}});
   data={classes:[{id:1,label:'الثالث · أ',grade:3,count:1}],students:{items:[{studentId:1,enrollmentId:1,classroomId:1,classroom:'الثالث · أ',grade:3,studentNumber:'SYN001',name:'طالب التحقق الاصطناعي'}],page:1,pageSize:24,totalCount:1,hasNext:false,hasPrevious:false}};
  }else if(path.endsWith('/school-duty/dashboard'))data={school:header.school,today:day.date,from:'2026-10-06',to:'2026-10-12',current:{assignmentId:1,date:day.date,teacher:teachers[0].name,needsReview:false},recent:Array.from({length:5},(_,i)=>({assignmentId:i+2,date:'2026-10-'+String(12-i).padStart(2,'0'),teacher:teachers[i].name,needsReview:false}))};
  else if(path.endsWith('/school-duty/archive')){
   if(state.denyIndex)return route.fulfill({status:403,json:{isSuccess:false,message:'لا تملك صلاحية عرض الأرشيف. / Archive access denied.'}});
   const query=new URL(request.url()).searchParams,pageNumber=Number(query.get('page')||1);
   let rows=Array.from({length:103},(_,i)=>({assignmentId:i+1,teacherId:teachers[i].userId,teacher:teachers[i].name,date:'2026-10-13',revision:i===4?1:2,isCurrent:i!==4,revisionCount:i===4?3:2,letter:i===2?null:{...doc,documentId:i*2+1,archiveState:i===0?'Archived':i===1?'Pending':i===3?'RetryScheduled':i===4?'SetupUnavailable':'Delivering',canRetry:i===3,artifactState:i===5?'Unavailable':'Available'},report:i===2?null:{...doc,kind:'Report',documentId:i*2+2,archiveState:'Pending',artifactState:'Available'}}));
   if(query.get('teacherId'))rows=rows.filter(x=>x.teacherId===query.get('teacherId'));
   if(query.get('from'))rows=rows.filter(x=>x.date>=query.get('from'));if(query.get('to'))rows=rows.filter(x=>x.date<=query.get('to'));if(state.indexEmpty)rows=[];
   data={page:pageNumber,pageSize:10,total:rows.length,readiness:state.archive,rows:rows.slice((pageNumber-1)*10,pageNumber*10)};
  }else if(path.endsWith('/retry')){state.retries.push(path);data=null;}

  else if(path.endsWith('/documents'))data={readiness:{enabled:false,processingEnabled:false,connectionState:'Disconnected'},documents:[doc]};
  else if(path.endsWith('/pdf')){
   state.pdfReads++;if(state.downloadFail)return route.fulfill({status:403,json:{isSuccess:false,message:'لا تملك صلاحية قراءة هذا المستند. / You cannot read this document.'}});
   return route.fulfill({contentType:'application/pdf',headers:{'Content-Disposition':'attachment; filename="synthetic-duty-letter.pdf"','Cache-Control':'private, no-store'},body:readFileSync(resolve(pdfRoot,'original-letter.pdf'))});
  }
  if(path.includes('/dashboard')&&!path.endsWith('/school-duty/dashboard'))return route.fulfill({status:503,json:{isSuccess:false,message:'Synthetic unrelated dashboard unavailable'}});
  await route.fulfill({json:{isSuccess:true,data,errors:[]}});
 });return{page,state,errors,calls};
}
async function dashboardPosition(page){assert(await page.evaluate(()=>{const main=document.querySelector('.shell-content'),outlet=main?.querySelector(':scope>router-outlet'),summary=main?.querySelector(':scope>app-duty-dashboard-summary');return !!summary&&main.lastElementChild===summary&&outlet.nextElementSibling!==summary&&!!(outlet.nextElementSibling.compareDocumentPosition(summary)&Node.DOCUMENT_POSITION_FOLLOWING);}), 'Duty summary must follow the entire routed dashboard');}
async function screenshot(page,name){if(name.startsWith('admin-archive-')&&!name.includes('empty'))await page.locator('.archive-table tbody tr').first().scrollIntoViewIfNeeded();if(name.startsWith('admin-teachers-'))await page.locator('.teacher-table').scrollIntoViewIfNeeded();await page.screenshot({path:resolve(evidence,name+'.png'),fullPage:true,animations:'disabled'});const widths=await page.evaluate(()=>[document.documentElement,...document.querySelectorAll('.duty-workspace,.settings-page,.execution-workspace,.duty-section-navigation')].filter(x=>x.clientWidth>0).map(x=>({scroll:x.scrollWidth,width:x.clientWidth})));for(const x of widths)assert(x.scroll<=x.width+1,name+': overflow');}
async function errorCheck(page,name){
 const dialog=page.locator('.duty-error-dialog[open]');await dialog.waitFor();const box=await dialog.boundingBox();const viewport=page.viewportSize();
 assert(Math.abs(box.x+box.width/2-viewport.width/2)<2);assert(Math.abs(box.y+box.height/2-viewport.height/2)<2);
 assert(await dialog.getByRole('button').evaluate(x=>x===document.activeElement));
 assert(!((await dialog.locator('p').innerText()).includes(' / ')),'Error must use the current language');
 await page.keyboard.press('Escape');assert(await dialog.isVisible());await screenshot(page,name);
 await page.keyboard.press('Tab');assert(await dialog.getByRole('button').evaluate(x=>x===document.activeElement));
 await dialog.getByRole('button').click();await dialog.waitFor({state:'hidden'});
}
async function sidebar(page,route){const button=page.getByRole('button',{name:'المناوبة',exact:true});if(await page.locator('.shell-sidebar.is-collapsed').count()||!await page.locator('#sidebar-category-duty a').count())await button.click();const link=page.locator('#sidebar-category-duty a').filter({has:page.locator(`:scope[href^="${route}"]`)});await page.locator(`#sidebar-category-duty a[href^="${route}"]`).first().click();}try{
 for(const width of [390,768,1440]){
  const x=await setup('SchoolManager',width),{page,state}=x;
  if(process.env.ALFALAH_DUTY_DASHBOARD_ONLY==='true'){await page.goto(origin+'/school-manager/dashboard');await page.locator('.today-summary h3').waitFor();await dashboardPosition(page);await screenshot(page,`manager-header-${width}`);await page.locator('app-duty-dashboard-summary').scrollIntoViewIfNeeded();await screenshot(page,`manager-footer-${width}`);assert.deepEqual(x.errors,[]);results.push({role:'SchoolManager',width,dutySummaryAfterEntireDashboard:true});await page.close();continue;}
  await page.goto(origin+'/school-duty/settings');await page.locator('.teacher-table tbody tr').first().waitFor();
  const reloadDialogs=[];page.on('dialog',async d=>{reloadDialogs.push(d.type());await d.dismiss();});await page.reload();await page.locator('.teacher-table tbody tr').first().waitFor();assert.deepEqual(reloadDialogs,[],'Untouched settings reload must not warn');
  assert.equal(await page.locator('.teacher-table tbody tr').count(),10);assert.equal(await page.locator('.rotation-order>div').count(),10);
  assert(await page.locator('.settings-shell>.settings-steps').count());assert.equal(await page.locator('.settings-page .settings-steps').count(),0);
  await screenshot(page,`admin-teachers-${width}`);
  const pageNav=page.locator('.setup-section>.admin-pagination').first();await pageNav.locator('button').last().click();assert.match(await pageNav.innerText(),/2 \/ 11/);
  await page.locator('.search-box input').fill('105');assert.equal(await page.locator('.teacher-table tbody tr').count(),1);await page.locator('.teacher-table tbody tr button').first().click();await page.locator('.search-box input').fill('');assert.equal(await page.locator('.setup-section h4 span').first().innerText(),'104');
  assert(await page.evaluate(()=>{const event=new Event('beforeunload',{cancelable:true});window.dispatchEvent(event);return event.defaultPrevented;}),'Changed settings retain unload protection');
  await page.locator('#duty-stage-teachers').focus();await page.keyboard.press('ArrowLeft');await page.keyboard.press('Enter');await page.locator('.settings-calendar').waitFor();
  const timing=page.locator('.dismissal-settings');await timing.locator('button').last().click();await timing.locator('input').fill('13:45');await screenshot(page,`admin-calendar-${width}`);
  await page.getByRole('button',{name:'معاينة التوزيع',exact:true}).click();await page.locator('.settings-preview').waitFor();assert.equal(state.previews[0].settings.dismissalAt,'13:45:00');await screenshot(page,`admin-preview-${width}`);
  await page.getByRole('button',{name:'تأكيد ونشر المناوبات',exact:true}).click();await page.waitForURL(url=>url.pathname==='/school-duty');assert.equal(state.publications[0].settings.dismissalAt,'13:45:00');assert.equal(state.publications[0].settings.teacherIds.length,104);await screenshot(page,`admin-schedule-${width}`);
  await sidebar(page,'/school-duty/fairness');await page.locator('.fairness-row').first().waitFor();assert.equal(await page.locator('.fairness-row').count(),10);await page.locator('.fairness-pagination button').last().click();assert.match(await page.locator('.fairness-pagination').innerText(),/2 \/ 11/);await screenshot(page,`admin-fairness-${width}`);
  await sidebar(page,'/school-duty/history');await page.locator('.history-timeline article').first().waitFor();await screenshot(page,`admin-history-${width}`);
  await sidebar(page,'/school-duty/execution');await page.locator('.execution-workspace .execution-hero').waitFor();await screenshot(page,`admin-followup-${width}`);
  await sidebar(page,'/school-duty/documents');await page.locator('.archive-table tbody tr').first().waitFor();assert.equal(await page.locator('.archive-table tbody tr').count(),10);assert.equal(await page.locator('.document-card').count(),0);
  const headings=await page.locator('.archive-table thead th').allTextContents();assert.match(headings[1],/خطاب/);assert.match(headings[2],/تقرير/);await screenshot(page,`admin-archive-${width}`);
  const row=page.locator('.archive-table tbody tr').first();const [download]=await Promise.all([page.waitForEvent('download'),row.locator('.letter-column .cell-actions button').nth(1).click()]);
  const output=resolve(evidence,`admin-native-${width}.pdf`);await download.saveAs(output);const hash=createHash('sha256').update(readFileSync(output)).digest('hex');assert.equal(hash,createHash('sha256').update(readFileSync(resolve(pdfRoot,'original-letter.pdf'))).digest('hex'));
  await row.locator('.letter-column .cell-actions button').first().click();await page.locator('.pdf-pages canvas').first().waitFor();await screenshot(page,`admin-preview-pdf-${width}`);await page.locator('.p-dialog-footer .dialog-action').click();await page.locator('.pdf-pages').waitFor({state:'hidden'});
  await row.locator('.row-history button').click();await page.locator('.revision-history article').waitFor();await screenshot(page,`admin-document-history-${width}`);await page.locator('.p-dialog-footer .dialog-action').click();
  await page.locator('.archive-table tbody tr').nth(3).locator('.letter-column .cell-actions button').last().click();await page.waitForFunction(()=>window.ng.getComponent(document.querySelector('app-duty-admin-archive'))?.busy===false);assert.equal(state.retries.length,1);
  await page.locator('.archive-pagination button').last().click();await page.waitForFunction(()=>document.querySelector('.archive-pagination')?.textContent.includes('2 / 11'));
  await page.locator('#archive-teacher').click();await page.getByRole('option',{name:teachers[0].name,exact:true}).click();await page.waitForFunction(()=>document.querySelectorAll('.archive-table tbody tr').length===1);
  state.downloadFail=true;await page.locator('.archive-table .letter-column .cell-actions button').first().click();await errorCheck(page,`admin-denied-pdf-${width}`);state.downloadFail=false;
  state.indexEmpty=true;await page.locator('.archive-toolbar button').click();await page.locator('.archive-empty').waitFor();await screenshot(page,`admin-archive-empty-${width}`);
  state.indexEmpty=false;state.denyIndex=true;await page.locator('.archive-toolbar button').click();await errorCheck(page,`admin-revoked-${width}`);assert.equal(await page.locator('.archive-table tbody tr').count(),0);
  await page.goto(origin+'/school-manager/dashboard');await page.locator('.today-summary h3').waitFor();assert.equal(await page.locator('.recent-days a').count(),5);await dashboardPosition(page);await screenshot(page,`manager-general-dashboard-${width}`);
  assert.deepEqual(x.errors,[]);results.push({role:'SchoolManager',width,teachers:105,teacherPageSize:10,archiveRows:103,archivePageSize:10,externalStickySteps:true,noContinuousGreenBar:true,untouchedNativeReloadWithoutWarning:true,changedSettingsProtected:true,customTime:'13:45',retainedOrder:true,letterRightReportLeft:true,realBrowserDownloadSha256:hash,pdfPreview:true,revisionHistory:true,retry:true,filters:true,empty:true,deniedReads:true,revokedAccess:true,generalDashboard:true});await page.close();
 }
 const officer=await setup('StudentAffairsOfficer',1440);await officer.page.goto(origin+'/student-affairs/officer');await officer.page.locator('.today-summary h3').waitFor();await dashboardPosition(officer.page);await officer.page.locator('app-duty-dashboard-summary').scrollIntoViewIfNeeded();await screenshot(officer.page,'authorized-officer-general-dashboard-1440');await sidebar(officer.page,'/school-duty/settings');assert.equal(await officer.page.locator('#sidebar-category-duty a').count(),6);await officer.page.locator('.teacher-table').waitFor();assert.deepEqual(officer.errors,[]);results.push({role:'StudentAffairsOfficer',actualDutyManage:true,generalDashboard:true,adminSettings:true});await officer.page.close();
 const ordinary=await setup('Instructor',390);await ordinary.page.goto(origin+'/instructor/dashboard');await ordinary.page.waitForTimeout(500);assert.equal(await ordinary.page.locator('app-duty-dashboard-summary').count(),0);assert(!ordinary.calls.some(x=>x.endsWith('/school-duty/dashboard')));results.push({role:'Instructor',ordinaryTeacherUnchanged:true,noAdminSummaryRequest:true});await ordinary.page.close();
 writeFileSync(resolve(evidence,'browser-results.json'),JSON.stringify({verification:process.env.ALFALAH_DUTY_DASHBOARD_ONLY==='true'?'Actual Angular dashboard rendering and DOM placement at390/768/1440; synthetic API fixtures, no PDF reads/provider writes.':'Actual Angular UI and browser keyboard/native PDF download; synthetic intercepted APIs. Isolated real SQL is verified separately. No provider writes.',results,ownerVisualReview:'Pending',physicalDevice:'Pending'},null,2));console.log(process.env.ALFALAH_DUTY_DASHBOARD_ONLY==='true'?'Dashboard footer position passed: 3 widths, manager/officer and ordinary teacher isolation.':'Administrator responsive workflow passed: 3 widths, 105 teachers, 103 archive rows, 2 capability roles and ordinary teacher isolation.');
}finally{await browser.close();}
