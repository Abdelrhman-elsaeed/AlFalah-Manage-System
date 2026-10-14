// Actual running Angular UI. Synthetic intercepted APIs; never touches operational data.
import assert from 'node:assert/strict';
import { readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { createHash } from 'node:crypto';
import { chromium } from '@playwright/test';
const origin = process.env.ALFALAH_DUTY_WORKSPACE_ORIGIN || 'http://localhost:4200';
const evidence = resolve(process.env.ALFALAH_DUTY_ADMIN_EVIDENCE||'../docs/specs/school-duty/verification/delegation-screens');
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
 const state={accounts:[{userId:'manager',name:'مدير التحقق الاصطناعي',roles:['SchoolManager'],eligible:true,inherited:true,enabled:false,version:null},{userId:'moderator',name:'مشرف التحقق الاصطناعي أحمد محمد عبد الرحمن',roles:['Moderator'],eligible:true,inherited:false,enabled:false,version:null},{userId:'guardian',name:'ولي أمر التحقق الاصطناعي',roles:['Guardian'],eligible:true,inherited:false,enabled:false,version:null}],grants:[],grantFail:false,previewFail:false,pickerFail:false,downloadFail:false,denyIndex:false,indexEmpty:false,retries:[],saves:[],signs:[],previews:[],publications:[],pdfReads:0,
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
  else if(path.endsWith('/administrators'))data=state.accounts;
  else if(path.includes('/administrators/')){const id=decodeURIComponent(path.split('/').at(-1)),change=body();if(state.grantFail)return route.fulfill({status:409,json:{isSuccess:false,message:'تغيّرت الصلاحية. حدّث القائمة. / Access changed. Refresh.'}});state.grants.push({id,...change});state.accounts=state.accounts.map(x=>x.userId===id?{...x,enabled:change.enabled,version:'2026-10-14T12:00:00Z'}:x);data=state.accounts;}
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
async function sidebar(page,route){const button=page.getByRole('button',{name:'المناوبة',exact:true});if(await page.locator('.shell-sidebar.is-collapsed').count()||!await page.locator('#sidebar-category-duty a').count())await button.click();const link=page.locator('#sidebar-category-duty a').filter({has:page.locator(`:scope[href^="${route}"]`)});await page.locator(`#sidebar-category-duty a[href^="${route}"]`).first().click();}
try{
 for(const width of [390,768,1440,1920]){
  const x=await setup('SchoolManager',width),{page,state}=x;await page.goto(origin+'/school-duty/permissions');await page.locator('.duty-permissions table tbody tr').waitFor();assert.equal(await page.locator('#sidebar-category-duty a').count(),7);
  await page.locator('#duty-admin-account').click();await page.getByRole('option',{name:'مشرف التحقق الاصطناعي أحمد محمد عبد الرحمن',exact:true}).click();await screenshot(page,`permissions-selected-${width}`);
  await page.locator('.grant-form button').click();await page.waitForFunction(()=>document.querySelectorAll('.duty-permissions tbody tr').length===2);assert.equal(state.grants.length,1);assert.equal(state.grants[0].id,'moderator');assert.equal(state.grants[0].enabled,true);await screenshot(page,`permissions-granted-${width}`);
  await page.locator('.duty-permissions .revoke').click();await page.locator('.p-dialog').waitFor();await screenshot(page,`permissions-revoke-confirm-${width}`);await page.locator('.p-dialog-footer button').first().click();assert.equal(state.grants.length,1);
  await page.locator('.duty-permissions .revoke').click();await page.locator('.p-dialog-footer .revoke').click();await page.waitForFunction(()=>document.querySelectorAll('.duty-permissions tbody tr').length===1);assert.equal(state.grants.length,2);assert.equal(state.grants[1].enabled,false);
  state.grantFail=true;await page.locator('#duty-admin-account').click();await page.getByRole('option',{name:'ولي أمر التحقق الاصطناعي',exact:true}).click();await page.locator('.grant-form button').click();await errorCheck(page,`permissions-conflict-${width}`);assert.match(await page.locator('#duty-admin-account').innerText(),/ولي أمر/);assert.equal(state.grants.length,2);
  await page.goto(origin+'/school-manager/dashboard');await page.locator('.today-summary h3').waitFor();await dashboardPosition(page);
  const geometry=await page.evaluate(()=>{const top=document.querySelector('.dashboard-page').getBoundingClientRect(),bottom=document.querySelector('app-duty-dashboard-summary').getBoundingClientRect();return{dashboard:{width:top.width,x:top.x},duty:{width:bottom.width,x:bottom.x}};});assert(Math.abs(geometry.dashboard.width-geometry.duty.width)<1);assert(Math.abs(geometry.dashboard.x-geometry.duty.x)<1);await page.locator('app-duty-dashboard-summary').scrollIntoViewIfNeeded();await screenshot(page,`dashboard-width-${width}`);
  assert.deepEqual(x.errors,[]);results.push({role:'SchoolManager',width,groupedAllRoles:true,grant:true,revokeConfirmation:true,cancelWithoutMutation:true,revoke:true,conflictRetainsSelection:true,matchingDashboardWidth:geometry});await page.close();
 }
 const delegate=await setup('Moderator',1440);await delegate.page.goto(origin+'/school-duty');await delegate.page.locator('.today-card').waitFor();assert.equal(await delegate.page.locator('#sidebar-category-duty a').count(),6);await delegate.page.goto(origin+'/school-duty/permissions');await delegate.page.waitForURL('**/unauthorized');assert(!delegate.calls.some(x=>x.endsWith('/administrators')));results.push({role:'Moderator',sixDutyAdministrationPages:true,cannotDelegate:true});await delegate.page.close();
 writeFileSync(resolve(evidence,'browser-results.json'),JSON.stringify({verification:'Actual Angular and browser interaction; synthetic APIs only, no operational grants/provider writes',results,ownerVisualReview:'Pending',physicalDevice:'Pending'},null,2));console.log('Duty delegation and matching dashboard widths passed at390/768/1440/1920.');
}finally{await browser.close();}
