// Actual running Angular UI. Synthetic intercepted APIs; never touches operational data.
import assert from 'node:assert/strict';
import { readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { createHash } from 'node:crypto';
import { chromium } from '@playwright/test';
const origin = process.env.ALFALAH_DUTY_WORKSPACE_ORIGIN || 'http://localhost:4200';
const evidence = resolve('../docs/specs/school-duty/verification/organized-screens');
mkdirSync(evidence,{recursive:true});
const pdfRoot = resolve('../docs/specs/school-duty/verification/d3-pdf');
const browser = await chromium.launch({headless:true}); const results=[];
const teachers=[{profileId:1,userId:'teacher',name:'عبد الرحمن محمد عبد الله عبد الرحمن الطويل · #1'},{profileId:2,userId:'other',name:'أحمد محمد العتيبي · #2'},{profileId:3,userId:'third',name:'أحمد محمد العتيبي · #3'}];
const day={id:1,date:'2026-10-13',originalTeacherId:'other',originalTeacher:teachers[1].name,teacherId:'teacher',teacher:teachers[0].name,revision:2,protected:false,locked:false,exceptionCode:null,attendance:'Unknown',isMine:true,changeKind:'AutoExchange'};
const header={school:'مدرسة التحقق الاصطناعية',city:'التحقق',header:'وزارة التعليم · مدرسة التحقق الاصطناعية',footer:null,logoUrl:null,manager:'مدير التحقق الاصطناعي',year:'2026–2027',semester:1,originalTeacher:teachers[1].name,actualTeacher:teachers[0].name,isSubstitute:true};
async function setup(role,width,lang='ar'){
 const page=await browser.newPage({viewport:{width,height:1000},reducedMotion:'reduce'});const errors=[],calls=[];
 page.on('pageerror',error=>errors.push(error.message));
 const manager=role==='SchoolManager';const user={userId:manager?'manager':'teacher',fullName:'حساب التحقق الاصطناعي',username:'synthetic',activeSchoolId:1,activeSchoolName:header.school,roles:[role],permissions:['Duty.View','Timetable.View',...(manager?['Duty.Manage','Storage.ManageSchool','Storage.ViewSchool']:[])],preferredLanguage:lang};
 const signature=await page.evaluate(()=>{const c=document.createElement('canvas');c.width=300;c.height=100;const x=c.getContext('2d');x.strokeStyle='#214737';x.lineWidth=2;x.beginPath();x.moveTo(20,70);x.bezierCurveTo(100,10,150,90,270,30);x.stroke();return c.toDataURL();});
 const state={previewFail:false,pickerFail:false,downloadFail:false,saves:[],signs:[],previews:[],publications:[],pdfReads:0,
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
  else if(path.endsWith('/school-duty/context'))data={canManage:manager,school:header.school,today:state.schoolToday||day.date,userId:user.userId,terms:[{id:1,yearId:1,year:header.year,semester:1,from:'2026-10-01',to:'2026-12-31'}],termId:1,planRevision:state.revision,settings:state.settings,teachers:manager?teachers:[],suggestedWeekdays:[0,1,2,3,4],days:(state.scheduleDays||[day]).map(x=>({...x,isMine:!manager&&x.teacherId===user.userId})),history:manager?[{id:1,kind:'Publish',date:day.date,otherDate:null,before:'',after:'',otherBefore:null,otherAfter:null,reason:'Publication',actor:'manager',at:'2026-10-13T08:00:00Z',relatedChangeId:null,unresolved:false,beforeTeacherId:'',afterTeacherId:''}]:[],fairness:manager?teachers.map(x=>({userId:x.userId,name:x.name,planned:3,effective:3,covered:0,completed:0,pendingCompensation:0})):[],pendingRetries:0,closures:[],ownHistory:manager?[]:[{assignmentId:1,date:day.date,authorUserId:'teacher',assignmentRevision:2,acknowledged:true,submitted:false,savedAt:null,submittedAt:null}]};
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
  }else if(path.endsWith('/documents'))data={readiness:{enabled:false,processingEnabled:false,connectionState:'Disconnected'},documents:[doc]};
  else if(path.endsWith('/pdf')){
   state.pdfReads++;if(state.downloadFail)return route.fulfill({status:403,json:{isSuccess:false,message:'لا تملك صلاحية قراءة هذا المستند. / You cannot read this document.'}});
   return route.fulfill({contentType:'application/pdf',headers:{'Content-Disposition':'attachment; filename="synthetic-duty-letter.pdf"','Cache-Control':'private, no-store'},body:readFileSync(resolve(pdfRoot,'original-letter.pdf'))});
  }
  await route.fulfill({json:{isSuccess:true,data,errors:[]}});
 });return{page,state,errors,calls};
}
async function screenshot(page,name){await page.screenshot({path:resolve(evidence,name+'.png'),fullPage:true,animations:'disabled'});const widths=await page.evaluate(()=>[document.documentElement,...document.querySelectorAll('.duty-workspace,.settings-page,.execution-workspace,.duty-section-navigation')].filter(x=>x.clientWidth>0).map(x=>({scroll:x.scrollWidth,width:x.clientWidth})));for(const x of widths)assert(x.scroll<=x.width+1,name+': overflow');}
async function errorCheck(page,name){
 const dialog=page.locator('.duty-error-dialog[open]');await dialog.waitFor();const box=await dialog.boundingBox();const viewport=page.viewportSize();
 assert(Math.abs(box.x+box.width/2-viewport.width/2)<2);assert(Math.abs(box.y+box.height/2-viewport.height/2)<2);
 assert(await dialog.getByRole('button').evaluate(x=>x===document.activeElement));
 assert(!((await dialog.locator('p').innerText()).includes(' / ')),'Error must use the current language');
 await page.keyboard.press('Escape');assert(await dialog.isVisible());await screenshot(page,name);
 await page.keyboard.press('Tab');assert(await dialog.getByRole('button').evaluate(x=>x===document.activeElement));
 await dialog.getByRole('button').click();await dialog.waitFor({state:'hidden'});
}
async function sidebar(page,route){const button=page.getByRole('button',{name:'المناوبة',exact:true});if(await page.locator('.shell-sidebar.is-collapsed').count())await button.click();const link=page.locator('#sidebar-category-duty a').filter({has:page.locator(`:scope[href^="${route}"]`)});await page.locator(`#sidebar-category-duty a[href^="${route}"]`).first().click();}
try{
 for(const width of [390,768,1440]){
  const x=await setup('SchoolManager',width),{page,state}=x;await page.goto(origin+'/school-duty');await page.locator('.today-card').waitFor();
  assert.equal(await page.locator('#sidebar-category-duty a').count(),6);assert.equal(await page.locator('.fairness-panel').count(),0);assert.equal(await page.locator('.documents').count(),0);await screenshot(page,`manager-schedule-${width}`);
  await sidebar(page,'/school-duty/settings');await page.locator('.settings-teachers').waitFor();assert.equal(await page.locator('.pool-row.selected').count(),3);assert.equal(await page.locator('.settings-calendar').count(),0);
  await page.locator('.pool-row').last().locator('button').first().click();await screenshot(page,`manager-teachers-${width}`);
  await page.locator('#duty-stage-teachers').focus();await page.keyboard.press('ArrowLeft');assert(await page.locator('#duty-stage-calendar').evaluate(x=>x===document.activeElement));await page.keyboard.press('Enter');await page.locator('.settings-calendar').waitFor();
  assert.equal(await page.locator('.teacher-pool').count(),0);await screenshot(page,`manager-calendar-${width}`);
  const geometry=await page.locator('.settings-calendar .p-calendar').evaluateAll(xs=>xs.map(x=>({input:x.querySelector('input').getBoundingClientRect().height,button:x.querySelector('button').getBoundingClientRect().height})));for(const g of geometry){assert.equal(g.input,44);assert.equal(g.button,44);}
  state.previewFail=true;await page.getByRole('button',{name:'معاينة التوزيع',exact:true}).click();await errorCheck(page,`manager-error-${width}`);assert.equal(await page.locator('.settings-calendar').count(),1);
  state.previewFail=false;await page.getByRole('button',{name:'معاينة التوزيع',exact:true}).click();await page.locator('.settings-preview').waitFor();assert.equal(await page.locator('.teacher-pool').count(),0);await screenshot(page,`manager-preview-${width}`);
  await sidebar(page,'/school-duty/fairness');await page.locator('.fairness-panel').waitFor();assert.equal(await page.locator('.schedule-panel').count(),0);await screenshot(page,`manager-fairness-${width}`);
  await sidebar(page,'/school-duty/history');await page.locator('.history-timeline').waitFor();assert.equal(await page.locator('.fairness-panel').count(),0);await screenshot(page,`manager-history-${width}`);
  await sidebar(page,'/school-duty/settings');await page.locator('.settings-preview').waitFor();assert.equal(state.previews.length,2,'Returning to a section preserves its reviewed draft without republishing');
  await page.getByRole('button',{name:'تأكيد ونشر المناوبات',exact:true}).click();await page.waitForURL(url=>url.pathname==='/school-duty');assert.equal(state.publications.length,1);assert.deepEqual(state.publications[0].settings.permanentlyExcludedTeacherIds,['third']);
  await sidebar(page,'/school-duty/documents');await page.locator('.document-card').first().waitFor();assert.equal(await page.locator('.report-section,.signature-review,.letter-surface').count(),0);await screenshot(page,`manager-documents-${width}`);
  assert.deepEqual(x.errors,[]);results.push({role:'SchoolManager',width,orderedSidebar:true,focusedPages:true,retainedSetup:true,centeredError:true,calendarGeometry:geometry});await page.close();
 }
 for(const width of [390,768,1440]){
  const x=await setup('Instructor',width),{page,state}=x;await page.goto(origin+'/timetable?tab=duty&date='+day.date+'&execution=1');await page.locator('.letter-surface').waitFor();assert.equal(new URL(page.url()).pathname,'/school-duty/execution');
  assert.equal(await page.locator('#sidebar-category-duty a').count(),3);assert.equal(await page.locator('.documents').count(),0);await screenshot(page,`teacher-letter-${width}`);
  assert.equal(await page.locator('.assignment-selector').count(),0,'Edit page uses school today without a date picker');
  await page.getByRole('button',{name:'اطلعت وأتعهد',exact:true}).click();await page.locator('#add-late').waitFor();await page.getByRole('button',{name:'مراجعة التقرير',exact:true}).last().click();await errorCheck(page,`teacher-required-error-${width}`);assert.equal(state.signs.length,1,'Error/read navigation never signs a report');
  const categoryBoxes=await page.locator('.student-category-grid>.report-section').evaluateAll(xs=>xs.map(x=>({width:x.getBoundingClientRect().width,y:x.getBoundingClientRect().y})));assert.equal(categoryBoxes.length,2);if(width===1440){assert.equal(categoryBoxes[0].width,categoryBoxes[1].width);assert.equal(categoryBoxes[0].y,categoryBoxes[1].y);}else assert(categoryBoxes[1].y>categoryBoxes[0].y);await screenshot(page,`teacher-report-${width}`);
  await page.locator('.empty-confirm input').first().check();await page.locator('.empty-confirm input').last().check();await page.locator('.section-body textarea').last().fill('ملاحظة اصطناعية محفوظة عند التنقل');await page.locator('.departure-card input').fill('14:15');await page.waitForFunction(()=>window.ng.getComponent(document.querySelector('app-duty-execution'))?.dirty()===false);
  await sidebar(page,'/school-duty/documents');await page.locator('.documents').waitFor();assert.equal(await page.locator('.execution-workspace').count(),0);await screenshot(page,`teacher-documents-${width}`);
  const [download]=await Promise.all([page.waitForEvent('download'),page.locator('.document-card').first().locator('.download-action').click()]);const file=resolve(evidence,`letter-native-${width}.pdf`);await download.saveAs(file);assert.equal(createHash('sha256').update(readFileSync(file)).digest('hex'),createHash('sha256').update(readFileSync(resolve(pdfRoot,'original-letter.pdf'))).digest('hex'));
  state.downloadFail=true;await page.locator('.document-card').first().getByRole('button',{name:'معاينة',exact:true}).click();await errorCheck(page,`teacher-denied-read-${width}`);assert.equal(await page.locator('.pdf-pages').count(),0);state.downloadFail=false;
  await sidebar(page,'/school-duty/execution');await page.locator('.report-section').first().waitFor();assert.equal(await page.locator('.section-body textarea').last().inputValue(),'ملاحظة اصطناعية محفوظة عند التنقل');
  state.pickerFail=true;await page.locator('#add-late').click();await errorCheck(page,`teacher-picker-error-${width}`);assert(await page.getByRole('button',{name:'اختيار الطالب',exact:true}).isDisabled());state.pickerFail=false;await page.locator('.picker-state button').click();await page.locator('.student-result').waitFor();await page.keyboard.press('Escape');await page.locator('.duty-picker').waitFor({state:'hidden'});
  await page.getByRole('button',{name:'مراجعة التقرير',exact:true}).last().click();await page.locator('.review-page').waitFor();assert.equal(await page.locator('.report-section').count(),0);assert.equal(await page.locator('.documents').count(),0);await screenshot(page,`teacher-review-${width}`);
  assert.equal(await page.locator('.execution-documents-link').count(),0,'Review has no additional documents section');
  await page.getByRole('button',{name:'توقيع وإرسال التقرير',exact:true}).click();await page.locator('.submitted-banner').waitFor();assert.equal(state.signs.length,2);assert.equal(await page.locator('.report-section').count(),0);await screenshot(page,`teacher-submitted-${width}`);
  assert.deepEqual(x.errors,[]);results.push({role:'Instructor',width,legacyContext:true,teacherOnlyPages:true,draftPreserved:true,explicitErrorOK:true,nativeDownloadHash:true,deniedRead:true,pickerRecovery:true,focusedReview:true,noExtraSignatures:true});await page.close();
 }
 for(const width of [390,768,1440]){
  const x=await setup('Instructor',width),{page,state}=x;
  state.schoolToday='2026-10-14';state.scheduleDays=Array.from({length:32},(_,i)=>{const date=new Date(Date.UTC(2026,9,13+i));const teacher=teachers[i%2];return{...day,id:i+1,date:date.toISOString().slice(0,10),teacherId:teacher.userId,teacher:teacher.name,originalTeacherId:teacher.userId,originalTeacher:teacher.name};}).filter(x=>new Date(x.date+'T12:00:00Z').getUTCDay()<=4);
  await page.goto(origin+'/school-duty');await page.locator('.duty-day-card').first().waitFor();assert.equal(await page.locator('.duty-day-card').count(),10);assert.match(await page.locator('.today-duty-status').innerText(),/ليست لديك مناوبة اليوم/);assert.equal(await page.locator('.today-top time').getAttribute('datetime'),'2026-10-15');
  const first=await page.locator('.card-date bdi').first().innerText();await screenshot(page,`teacher-schedule-${width}`);
  await page.locator('.duty-pagination button').last().click();await page.waitForFunction(first=>document.querySelector('.card-date bdi')?.textContent!==first,first);assert.notEqual(await page.locator('.card-date bdi').first().innerText(),first);assert.equal(await page.locator('.duty-day-card').count(),10);
  await page.locator('.schedule-filters button').first().click();assert.equal(await page.locator('.duty-pagination b').innerText(),'1');assert((await page.locator('.card-teacher').allTextContents()).every(x=>x===teachers[0].name));
  await page.locator('#duty-teacher-filter').click();await page.getByRole('option',{name:teachers[1].name,exact:true}).click();assert((await page.locator('.card-teacher').allTextContents()).every(x=>x===teachers[1].name));
  await page.locator('.duty-day-card').first().click();await page.locator('.day-detail').waitFor();assert.equal(await page.locator('.letter-ack,.detail-actions').count(),0,'Roster-only teacher cannot take another teacher duty');await page.keyboard.press('Escape');
  const priorExecutionReads=x.calls.filter(p=>p.endsWith('/execution')).length;
  await sidebar(page,'/school-duty/execution');await page.locator('.execution-today-empty').waitFor();assert.equal(await page.locator('.assignment-selector,.letter-ack').count(),0);assert.equal(x.calls.filter(p=>p.endsWith('/execution')).length,priorExecutionReads,'No own duty today must not open a future assignment');await screenshot(page,`teacher-no-duty-today-${width}`);
  await page.goto(origin+'/school-duty/documents?execution=1');await page.locator('.assignment-selector').waitFor();await page.getByRole('button',{name:'عرض تفاصيل المناوبة',exact:true}).click();await page.locator('.archive-inspection-note').waitFor();await page.locator('.letter-surface').waitFor();assert.equal(await page.locator('.letter-ack button').count(),0,'Historical inspection cannot apply a signature');await screenshot(page,`teacher-archive-inspection-${width}`);
  await sidebar(page,'/school-duty/documents');await page.getByRole('button',{name:'استكمال التقرير',exact:true}).click();await page.locator('.letter-ack button').waitFor();assert.equal(await page.locator('.assignment-selector').count(),0,'Explicit overdue completion preserves the workflow without an edit date picker');
  assert.deepEqual(x.errors,[]);results.push({role:'Instructor',width,tenDayCards:true,pagination:true,schoolTodayAlert:true,nearestOwnDate:true,ownAllAndTeacherFilters:true,otherTeacherRosterOnly:true,automaticTodayWithoutPicker:true,noFutureFallback:true,readOnlyHistoricalInspection:true,explicitOverdueResume:true,archiveSelectionRetained:true});await page.close();
 }
 for(const width of [390,768,1440]){
  const x=await setup('SchoolManager',width),{page,state}=x;await page.goto(origin+'/school-manager/storage/admin');await page.locator('.duty-archive-settings').waitFor();
  const panel=page.locator('.duty-archive-settings');await page.waitForFunction(()=>window.ng.getComponent(document.querySelector('app-duty-archive-settings'))?.busy()===false);
  assert(await panel.locator('.archive-primary').isDisabled());await screenshot(page,`manager-archive-disconnected-${width}`);
  state.archive.connectionState='Connected';await panel.getByRole('button',{name:'إعادة فحص الاتصال',exact:true}).click();await page.waitForFunction(()=>window.ng.getComponent(document.querySelector('app-duty-archive-settings'))?.ready()?.connectionState==='Connected');
  await panel.locator('.archive-primary').click();await panel.locator('.enabled-state').waitFor();assert.deepEqual(state.archiveWrites,[{enabled:true}]);await screenshot(page,`manager-archive-enabled-${width}`);
  await panel.getByRole('button',{name:'إيقاف الرفع التلقائي',exact:true}).click();await panel.locator('.archive-primary').waitFor();assert.deepEqual(state.archiveWrites,[{enabled:true},{enabled:false}]);assert.deepEqual(x.errors,[]);
  results.push({role:'SchoolManager',width,archiveReadiness:true,disconnectedActivationDenied:true,explicitEnableAndPause:true,syntheticApiOnly:true});await page.close();
 }
 const en=await setup('Instructor',1440,'en');await en.page.goto(origin+'/school-duty/execution?execution=1');await en.page.locator('.letter-surface').waitFor();await en.page.locator('.letter-ack .primary-action').click();await en.page.locator('.report-section').first().waitFor();await en.page.locator('.execution-actionbar .primary-action').click();await errorCheck(en.page,'teacher-error-en-1440');assert.deepEqual(en.errors,[]);await en.page.close();
 const denied=await setup('Instructor',390);await denied.page.goto(origin+'/school-duty/settings');await denied.page.waitForURL('**/unauthorized');assert(!denied.calls.some(x=>x.endsWith('/plans/preview')));await denied.page.close();
 writeFileSync(resolve(evidence,'browser-results.json'),JSON.stringify({verification:'Actual current Angular server; synthetic API fixtures only; native downloads contain actual generated synthetic PDF bytes',results,ownerVisualReview:'Pending',physicalDevice:'Pending',externalProvider:'Not used'},null,2));console.log('Ordered duty pages and centered error checks passed.');
}finally{await browser.close();}
