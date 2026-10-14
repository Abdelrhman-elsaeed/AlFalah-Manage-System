// Actual rendered Angular SPA. Every API response below is an isolated synthetic fixture.
import {createServer} from 'node:http';
import {readFileSync,existsSync,mkdirSync,writeFileSync} from 'node:fs';
import {resolve,extname,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
import assert from 'node:assert/strict';
import {chromium} from '@playwright/test';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'..'),dist=resolve(root,'dist/al-falah-app/browser'),evidence=process.env.ALFALAH_DUTY_BROWSER_EVIDENCE ? resolve(process.env.ALFALAH_DUTY_BROWSER_EVIDENCE) : resolve(root,'../docs/specs/school-duty/verification/d3-screens');
mkdirSync(evidence,{recursive:true});
const mime={'.html':'text/html','.js':'application/javascript','.mjs':'application/javascript','.css':'text/css','.json':'application/json','.woff2':'font/woff2','.png':'image/png','.svg':'image/svg+xml'};
const server=createServer((req,res)=>{let p=resolve(dist,decodeURIComponent(new URL(req.url,'http://localhost').pathname).replace(/^\/+/,'')||'index.html');if(!p.startsWith(dist)){res.writeHead(403);return res.end();}if(!existsSync(p))p=resolve(dist,'index.html');res.writeHead(200,{'Content-Type':mime[extname(p)]||'application/octet-stream'});res.end(readFileSync(p));});
await new Promise(done=>server.listen(0,'127.0.0.1',done));const origin=`http://127.0.0.1:${server.address().port}`;
const browser=await chromium.launch({headless:true});const errors=[],report=[];
const classes=[{id:1,label:'الثالث الابتدائي · أ',grade:3,count:28},{id:2,label:'الثالث الابتدائي · ب',grade:3,count:18},{id:3,label:'الرابع الابتدائي · أ',grade:4,count:30}];
const students=Array.from({length:76},(_,i)=>({studentId:101+i,enrollmentId:501+i,classroomId:i<28?1:i<46?2:3,classroom:i<28?classes[0].label:i<46?classes[1].label:classes[2].label,grade:i<46?3:4,studentNumber:String(200101+i),name:i<2?'عبد الرحمن محمد عبد الرحمن عبد الله العتيبي':`${['محمد عبدالله القحطاني','خالد أحمد الزهراني','علي حسن السبيعي','عبدالله صالح الغامدي'][i%4]} ${i+1}`}));
const header={school:'مدارس الفلاح الابتدائية',city:'مكة المكرمة',header:'وزارة التعليم · مدارس الفلاح',footer:null,logoUrl:null,manager:'محمد عبد الرحمن القحطاني',year:'١٤٤٨ هـ',semester:1,originalTeacher:'محمود عبد الرحمن العتيبي',actualTeacher:'علي محمد عبد الرحمن عبدالله القحطاني',isSubstitute:true};
function execution(){return{assignmentId:1,assignmentRevision:2,date:'2026-10-13',canExecute:true,canManage:false,header,dismissalAt:'13:30:00',timingException:null,signatureData:null,acknowledged:false,draft:null,lateStudents:[],documents:[],priorDrafts:[]};}
const pageData=(items,page=1,pageSize=24,total=items.length)=>({items,page,pageSize,totalCount:total,hasNext:page*pageSize<total,hasPrevious:page>1});
const noticeState=(state='Pending')=>({revision:1,state,recipients:1,dashboardDelivered:state==='Delivered'?1:0,messagesDelivered:state==='Delivered'?1:0,dashboardRead:0,messagesRead:0});
function fact(student=students[0],i=1,state='Pending'){return{id:i,revision:1,studentId:student.studentId,enrollmentId:student.enrollmentId,classroomId:student.classroomId,studentName:student.name,classroom:student.classroom,date:'2026-10-13',observedAt:'13:45:00',dismissalAt:'13:30:00',departureAt:null,action:'متابعة الطالب والتواصل لترتيب استلامه',notes:'',isRetracted:false,notices:[noticeState(state)]};}
async function setup(width,role='Instructor',mode='normal',lang='ar'){
 const page=await browser.newPage({viewport:{width,height:width===390?844:1000},reducedMotion:'reduce'});page.on('pageerror',e=>errors.push(e.message));
 const signature=await page.evaluate(()=>{const c=document.createElement('canvas');c.width=320;c.height=100;const x=c.getContext('2d');x.strokeStyle='#214737';x.lineWidth=2.8;x.beginPath();x.moveTo(32,70);x.bezierCurveTo(130,16,130,80,220,36);x.bezierCurveTo(270,4,190,90,285,50);x.stroke();x.beginPath();x.moveTo(100,80);x.lineTo(245,76);x.stroke();return c.toDataURL('image/png');});
 const current={userId:role==='Instructor'?'teacher':role==='Guardian'?'guardian':'manager',username:'synthetic',fullName:'حساب فحص الواجهة',activeSchoolId:1,activeSchoolName:header.school,preferredLanguage:lang,roles:[role],permissions:['Timetable.View','Duty.View','Messaging.ViewOwn','Messaging.Send','Guardian.ViewLinkedStudents','Guardian.ViewDashboard','StudentAffairsDashboard.Guardian','Notification.ViewOwn','Student.View','Dashboard.SchoolManager','StudentAffairsDashboard.SchoolOversight',...(role==='SchoolManager'?['Duty.Manage','Timetable.Manage']:[])]};
 const state={data:execution(),saves:[],late:[],signs:[],searches:[],read:[],mode,signature,noticeRead:false};state.data.signatureData=mode==='missing-signature'?null:signature;
 if(mode==='missing-signature')state.data.draft={id:1,version:1,assignmentRevision:2,authorUserId:'teacher',content:{noLateStudents:true,noViolations:true,violations:[],observations:'Synthetic draft retained through signature setup',lastDeparture:null},savedAt:'2026-10-13T12:00:00Z',submittedAt:null,correctionReason:null};
 if(role==='SchoolManager'){state.data.canExecute=false;state.data.canManage=true;state.data.acknowledged=true;state.data.lateStudents=[fact(students[0],1,'RetryScheduled'),fact(students[1],2,'NoEligibleGuardian')];}
 await page.addInitScript(({current,lang})=>{sessionStorage.setItem('alfalah_access_token',`e30.${btoa(JSON.stringify({sub:current.userId,exp:2000000000}))}.fixture`);sessionStorage.setItem('alfalah_user',JSON.stringify(current));localStorage.setItem('alfalah_language',lang);localStorage.setItem('language',lang);},{current,lang});
 const context={canManage:role==='SchoolManager',school:header.school,today:'2026-10-13',userId:current.userId,terms:[{id:1,yearId:1,year:header.year,semester:1,from:'2026-08-23',to:'2026-12-31'}],termId:1,planRevision:2,settings:null,teachers:[],suggestedWeekdays:[0,1,2,3,4],history:[],fairness:[],pendingRetries:0,closures:[],ownHistory:[],days:[{id:1,date:'2026-10-13',originalTeacherId:'original',originalTeacher:header.originalTeacher,teacherId:'teacher',teacher:header.actualTeacher,revision:2,protected:false,locked:false,exceptionCode:null,attendance:'Unknown',isMine:role==='Instructor',changeKind:'AutoExchange'}]};
 const systemThread={id:91,student:{id:101,studentNumber:'200101',displayName:students[0].name,classLabel:classes[0].label,isActive:true},subject:'النظام · تأخر الانصراف',threadType:'SystemNotice',status:'Closed',participants:[{userId:'guardian',displayName:'ولي الأمر',role:'Guardian'}],unreadCount:1,updatedAt:'2026-10-13T11:45:00Z',rowVersion:'fixture'};
 const message={id:301,conversationId:91,sender:{userId:'system',displayName:'النظام / System',roleSnapshot:'System'},body:`مدارس الفلاح — تم رصد تأخر انصراف الطالب ${students[0].name} بتاريخ 2026-10-13 الساعة 13:45، بعد وقت الانصراف 13:30. يرجى ترتيب استلام الطالب.`,createdAt:'2026-10-13T11:45:00Z',deliveryState:'Delivered',disposition:'SentImmediately',nextEligibleSendAt:null,receipts:[{displayName:'ولي الأمر',role:'Recipient',status:'Delivered',deliveredAt:'2026-10-13T11:45:00Z',readAt:null}]};
 const guardianDashboard={generatedAt:'2026-10-13T11:45:00Z',students:[],unreadNotifications:1,unreadThreads:1,actions:[{code:'UnreadThreads',count:1},{code:'UnreadNotifications',count:1}]};
 await page.route('**/api/**',async route=>{
  const req=route.request(),url=new URL(req.url()),path=url.pathname;let data={};const body=()=>req.postDataJSON();
  if(path.endsWith('/auth/me'))data=current;
  else if(path.endsWith('/account/signature/requirement'))data={required:false,hasSignature:true};
  else if(path.endsWith('/account/signature')){if(req.method()==='PUT'){state.data.signatureData=body().signatureDrawnData;data=true;}else data={signatureDrawnData:state.data.signatureData};}
  else if(path.endsWith('/auth/schools'))data=[];
  else if(path.endsWith('/school-duty/context'))data=context;
  else if(path.endsWith('/execution')){if(mode==='loading')await new Promise(r=>setTimeout(r,1500));if(mode==='error')return route.fulfill({status:503,json:{isSuccess:false,message:'تعذر تحميل المناوبة. حاول مجددًا.'}});data=state.data;}
  else if(path.endsWith('/school-duty/students')){
    state.searches.push(Object.fromEntries(url.searchParams));const search=url.searchParams.get('search')||'',classId=Number(url.searchParams.get('classroomId'))||null,grade=Number(url.searchParams.get('grade'))||null,p=Number(url.searchParams.get('page'))||1;
    if(search==='slow')await new Promise(r=>setTimeout(r,750));let rows=students.filter(x=>(!classId||x.classroomId===classId)&&(!grade||x.grade===grade)&&(!search||x.name.includes(search)||x.studentNumber.includes(search)));
    data={students:pageData(rows.slice((p-1)*24,p*24),p,24,rows.length),classes};
  }
  else if(path.endsWith('/acknowledge')){state.signs.push(body());state.data.acknowledged=true;state.data.documents.push({id:1,kind:'Letter',assignmentRevision:2,authorUserId:'teacher',signedAt:'2026-10-13T10:00:00Z',signatureHash:'fixture',previousDocumentId:null,correctionReason:null});data=state.data;}
  else if(path.endsWith('/report/draft')){const b=body();state.saves.push(b);if(state.mode==='conflict'){state.data.draft={id:1,version:9,assignmentRevision:2,authorUserId:'teacher',content:{...b.content,observations:'ملاحظة زميلة محفوظة في نسخة أحدث'},savedAt:'2026-10-13T12:05:00Z',submittedAt:null,correctionReason:null};state.mode='normal';return route.fulfill({status:409,json:{isSuccess:false,message:'توجد نسخة أحدث. احتُفظ بإدخالك؛ راجع النسختين قبل الحفظ.',errorCode:'DutyDraftConflict'}});}state.data.draft={id:1,version:(state.data.draft?.version??0)+1,assignmentRevision:2,authorUserId:'teacher',content:b.content,savedAt:new Date().toISOString(),submittedAt:null,correctionReason:null};data=state.data;}
  else if(path.endsWith('/report/submit')){state.signs.push(body());state.data.draft.submittedAt='2026-10-13T12:30:00Z';state.data.draft.version++;state.data.documents.push({id:Math.max(0,...state.data.documents.map(x=>x.id))+1,kind:'Report',assignmentRevision:2,authorUserId:'teacher',signedAt:'2026-10-13T12:30:00Z',signatureHash:'fixture',previousDocumentId:state.data.draft.correctionReason?state.data.documents.filter(x=>x.kind==='Report').at(-1)?.id??null:null,correctionReason:state.data.draft.correctionReason});data=state.data;}
  else if(path.match(/dismissal-lateness\/\d+\/correction$/)){const b=body(),f=state.data.lateStudents.find(x=>x.id===Number(path.split('/').at(-2)));Object.assign(f,{revision:f.revision+1,observedAt:b.observedAt,departureAt:b.departureAt,action:b.action,notes:b.notes,isRetracted:b.retract});f.notices.push({...noticeState(),revision:f.revision});data=f;}
  else if(path.endsWith('/report/correction')){state.data.draft.submittedAt=null;state.data.draft.version++;state.data.draft.correctionReason=body().reason;data=state.data;}
  else if(path.match(/dismissal-lateness\/\d+\/retry$/)){data=true;}
  else if(path.endsWith('/dismissal-lateness')&&req.method()==='POST'){const b=body();state.late.push(b);data=fact(students.find(x=>x.studentId===b.studentId),state.data.lateStudents.length+1);data.observedAt=b.observedAt;data.action=b.action;state.data.lateStudents.push(data);}
  else if(path.endsWith('/school-duty/dismissal-lateness'))data={uniqueStudents:27,incidents:32,items:pageData(students.slice(0,24).map((s,i)=>fact(s,i+1,i%3===0?'NoEligibleGuardian':'Delivered')),1,24,32)};
  else if(path.endsWith('/guardian/notices'))data=[{notificationId:11,factId:1,revision:1,studentName:students[0].name,date:'2026-10-13',content:message.body,messageRoute:'/student-affairs/messages?conversationId=91',isRead:state.noticeRead,at:'2026-10-13T11:45:00Z'}];
  else if(path.endsWith('/guardian/notices/11/read')){state.noticeRead=true;guardianDashboard.unreadNotifications=0;data=true;}
  else if(path.endsWith('/student-affairs/dashboard/guardian')||path.endsWith('/dashboards/guardian')||path.endsWith('/guardian/dashboard'))data=guardianDashboard;
  else if(path.endsWith('/conversations'))data=pageData([systemThread],1,100);
  else if(path.endsWith('/conversations/91'))data=systemThread;
  else if(path.endsWith('/conversations/91/messages'))data=pageData([message],1,50);
  else if(path.endsWith('/conversations/91/read')){state.read.push(body());systemThread.unreadCount=0;guardianDashboard.unreadThreads=0;data=true;}
  else if(path.endsWith('/dashboard/school-manager'))data={schoolId:1,schoolName:header.school,evaluationsPendingApprovalCount:0,openComplaintsCount:0,instructorsNeedingImprovementCount:0,instructorsCount:15,moderatorsCount:2,visitsThisMonthCount:0,complaintsCount:0,visitsByStatus:[],subjectPerformance:[],moderatorPerformance:[],instructorsNeedingImprovement:[],improvementPlans:{totalActive:0,totalCompleted:0,completionRate:0,averageProgress:0,byStatus:[],bySchool:[],byModerator:[],overdueCount:0},appliedFilters:{}};
  else if(path.endsWith('/dashboard/school-manager/operations'))data={generatedAt:'2026-10-13T12:00:00Z',date:'2026-10-13',timeZone:'Africa/Cairo',students:null,staff:{counts:{total:15,present:15,absent:0,excused:0,unrecorded:0,recorded:15,attendanceRate:100,recordingRate:100},absences:[],trend:[]},summons:null,schoolDay:null,activities:[],gatePasses:null,conversations:null,recentVisits:[],dismissalLatenessToday:27};
  else if(path.includes('analytics-profile'))data={studentId:101,studentNumber:'200101',fullName:students[0].name,identityNumber:'synthetic',nationalId:null,dateOfBirth:null,gender:'Male',isActive:true,profilePhotoStorageKey:null,classroomId:1,classroomName:classes[0].label,stage:'Primary',gradeLevel:3,section:'أ',rollNumber:1,enrollmentStatus:'Active',totalAbsences:0,totalDelays:0,totalExcuses:0,totalReferrals:0,totalBehaviors:0,totalRecognitions:0,totalGatePasses:0,totalDismissalLateness:1,monthlyTrends:[],recentEvents:[{id:'dismissal-1-r1',eventType:'DismissalLateness',title:'تأخر الانصراف',description:'الثالث الابتدائي · أ · متابعة الطالب وترتيب استلامه',occurredAt:'2026-10-13T11:45:00Z',severity:'warning',icon:'pi pi-clock',status:'r1',actorName:header.actualTeacher}],guardians:[]};
  else if(path.endsWith('/timetables/catalog'))data={academicYears:[{id:1,nameAr:header.year,isActive:true}],semesters:[{value:1,labelAr:'الفصل الأول'}],days:[],teachers:[],moderators:[],capabilities:{canManage:false,canDelegate:false},bellSchedule:null};
  else if(path.endsWith('/timetables/current'))data=null;
  else if(path.endsWith('/timetables'))data=[];
  else if(path.includes('/storage/access'))return route.fulfill({status:403,json:{isSuccess:false}});
  else if(path.endsWith('/notifications/unread-count'))data=state.noticeRead?0:1;
  else if(path.includes('notifications'))data=pageData([]);
  else if(path.includes('capabilities'))data={canAccess:false};
  await route.fulfill({json:{isSuccess:true,data,errors:[]}});
 });return{page,state};
}
async function capture(page,name){
 const target=name.startsWith('conflict-')?'.conflict-panel':(name.startsWith('review-')||name.startsWith('correction-resign'))?'.signature-review':(name.startsWith('submitted-')||name.startsWith('correction-submitted'))?'.submitted-banner':name.startsWith('draft-')?'.report-student':name.startsWith('letter-')&&!name.startsWith('letter-preview')?'.letter-surface':null;
 if(target&&await page.locator(target).count())await page.locator(target).first().scrollIntoViewIfNeeded();
 await page.screenshot({path:resolve(evidence,name+'.png'),fullPage:true,animations:'disabled'});
 const sizes=await page.evaluate(()=>[{scroll:document.documentElement.scrollWidth,width:innerWidth},...Array.from(document.querySelectorAll('.execution-workspace,.duty-picker .p-dialog-content,.execution-dialog .p-dialog-content,.dismissal-dialog .p-dialog-content')).filter(x=>x.clientWidth).map(x=>({scroll:x.scrollWidth,width:x.clientWidth}))]);for(const s of sizes)assert(s.scroll<=s.width+1,`${name}: overflow ${s.scroll}/${s.width}`);
}
// D3 document scenarios follow.
const pdfRoot=resolve(root,'../docs/specs/school-duty/verification/d3-pdf');
const hashes=[];
async function documents(width,role='Instructor',archive='Pending',lang='ar'){
 const x=await setup(width,role,'normal',lang);x.state.data.acknowledged=true;
 x.state.data.draft={id:1,version:2,assignmentRevision:2,authorUserId:'teacher',content:{noLateStudents:true,noViolations:true,violations:[],observations:'بيانات تحقق اصطناعية',lastDeparture:'14:27:00'},savedAt:'2026-10-13T12:00:00Z',submittedAt:'2026-10-13T12:01:00Z',correctionReason:null};
 x.state.data.documents=[{id:1,kind:'Letter',assignmentRevision:2,authorUserId:'teacher',signedAt:'2026-10-13T10:00:00Z',signatureHash:'synthetic',previousDocumentId:null,correctionReason:null},{id:2,kind:'Report',assignmentRevision:2,authorUserId:'teacher',signedAt:'2026-10-13T12:01:00Z',signatureHash:'synthetic',previousDocumentId:null,correctionReason:null}];
 const doc=(id,kind,previous=null)=>({documentId:id,kind,authorName:header.actualTeacher,dutyDate:"2026-10-13",assignmentRevision:2,signedAt:'2026-10-13T12:01:00Z',previousDocumentId:previous,correctionReason:previous?'تصحيح موثق من المدير مع احتفاظ النسخة الموقعة السابقة':null,artifactState:'Available',archiveState:archive,canRetry:role==='SchoolManager'&&archive==='NeedsAttention',attempts:archive==='NeedsAttention'?5:1,nextAttemptAt:null,verifiedAt:archive==='Archived'?'2026-10-13T12:05:00Z':null,errorCode:null});
 const state={documents:[doc(3,'Report',2),doc(2,'Report'),doc(1,'Letter')],denied:false,missing:false,retries:0,pdfReads:0};
 await x.page.route('**/api/v1/school-duty/**',async route=>{
  const req=route.request(),url=new URL(req.url()),path=url.pathname;
  if(path.endsWith('/documents'))return route.fulfill({json:{isSuccess:true,data:{readiness:{enabled:true,processingEnabled:true,connectionState:'Connected'},documents:state.documents}}});
  if(path.endsWith('/archive/retry')){assert(req.headers().authorization?.startsWith('Bearer '));state.retries++;state.documents[0].archiveState='Pending';state.documents[0].canRetry=false;return route.fulfill({status:202,json:{isSuccess:true,data:true}});}
  if(path.endsWith('/pdf')||path.endsWith('/roster.pdf')){
   state.pdfReads++;assert(req.headers().authorization?.startsWith('Bearer '),'PDF request must use authenticated HttpClient');
   if(state.denied||state.missing)return route.fulfill({status:state.denied?403:409,json:{isSuccess:false,message:state.denied?'الوثيقة غير متاحة لهذا الحساب':'النسخة المحفوظة غير متاحة حاليا. راجع المدير.'}});
   const name=path.endsWith('/roster.pdf')?'official-roster':path.includes('/1/')?'original-letter':path.includes('/2/')?'retained-report':'corrected-report';
   return route.fulfill({contentType:'application/pdf',headers:{'Content-Disposition':`attachment; filename="${name}.pdf"`,'Cache-Control':'private, no-store'},body:readFileSync(resolve(pdfRoot,name+'.pdf'))});
  }
  return route.fallback();
 });
 return {...x,docs:state};
}
async function downloadCheck(page,click,label,file){
 const [download]=await Promise.all([page.waitForEvent('download'),click()]);const destination=resolve(evidence,label+'.pdf');await download.saveAs(destination);
 const {createHash}=await import('node:crypto');const actual=createHash('sha256').update(readFileSync(destination)).digest('hex').toUpperCase();
 const expected=JSON.parse(readFileSync(resolve(pdfRoot,file+'.json'),'utf8')).Hash;assert.equal(actual,expected);hashes.push({label,hash:actual,expected,source:'Real generated PDF bytes; API delivery mocked in this harness'});
}
try{
 for(const width of [390,768,1440]){
  for(const role of ['Instructor','SchoolManager']){
   const x=await documents(width,role,role==='SchoolManager'?'Archived':'Pending');const page=x.page;
   await page.goto(origin+'/school-duty/documents?date=2026-10-13&execution=1');await page.locator('.document-card.available').first().waitFor();
   await page.locator('.documents').scrollIntoViewIfNeeded();await capture(page,`${role.toLowerCase()}-documents-${width}`);
   await page.locator('.document-card').first().getByRole('button',{name:'معاينة',exact:true}).click();await page.locator('.pdf-pages canvas').first().waitFor();await page.locator('.preview-message[role=status]').waitFor({state:'hidden'});await capture(page,`letter-preview-${role.toLowerCase()}-${width}`);
   await downloadCheck(page,()=>page.locator('.duty-pdf-dialog .dialog-download').click(),`letter-download-${role}-${width}`,'original-letter');
   await page.keyboard.press('Escape');await page.locator('.duty-pdf-dialog').waitFor({state:'hidden'});assert.equal(await page.evaluate(()=>document.activeElement?.textContent?.trim()),'معاينة');
   await page.locator('.document-card').last().getByRole('button',{name:'معاينة',exact:true}).click();await page.locator('.pdf-pages canvas').first().waitFor();await page.locator('.preview-message[role=status]').waitFor({state:'hidden'});await capture(page,`report-preview-${role.toLowerCase()}-${width}`);await page.keyboard.press('Escape');
   await page.locator('.history-action').first().click();await page.locator('.revision-history article').first().waitFor();await capture(page,`history-${role.toLowerCase()}-${width}`);
   await downloadCheck(page,()=>page.locator('.revision-actions').nth(1).getByRole('button').first().click(),`retained-download-${role}-${width}`,'retained-report');
   if(role==='SchoolManager')await downloadCheck(page,()=>page.locator('.revision-actions').first().getByRole('button').last().click(),`archived-download-${width}`,'corrected-report');
   await page.keyboard.press('Escape');await page.close();
  }
  report.push({width,checks:['teacher pending and manager archived cards','actual rasterized PDF preview','authenticated blob download SHA256','retained correction history','dialog Escape and focus return','RTL overflow']});
 }
 for(const archive of ['SetupUnavailable','Disconnected','Delivering','NeedsAttention','RetryScheduled','Unavailable','Preparing']){
  const x=await documents(390,'SchoolManager',archive);await x.page.goto(origin+'/school-duty/documents?date=2026-10-13&execution=1');await x.page.locator('.document-card.available').first().waitFor();await x.page.locator('.documents').scrollIntoViewIfNeeded();await capture(x.page,`archive-${archive.toLowerCase()}-390`);
  await downloadCheck(x.page,()=>x.page.locator('.document-card').last().getByRole('button',{name:'تنزيل',exact:false}).first().click(),`local-${archive}`,'corrected-report');
  if(archive==='NeedsAttention'){await x.page.locator('.retry-action').first().click();await x.page.getByText('أُدرجت محاولة الأرشفة. نسختك الموقّعة محفوظة بالفعل.',{exact:true}).waitFor();assert.equal(x.docs.retries,1);await capture(x.page,'retry-pending-390');}
  await x.page.close();
 }
 for(const mode of ['denied','missing']){const x=await documents(390);x.docs[mode]=true;await x.page.goto(origin+'/school-duty/documents?date=2026-10-13&execution=1');await x.page.locator('.document-card.available').first().waitFor();await x.page.locator('.document-card').first().getByRole('button',{name:'معاينة',exact:true}).click();await x.page.locator('.duty-error-dialog[open]').waitFor();await x.page.locator('.documents').scrollIntoViewIfNeeded();await capture(x.page,`pdf-${mode}-390`);assert.equal(await x.page.locator('.pdf-pages').count(),0);await x.page.close();}
 const roster=await documents(390,'SchoolManager');await roster.page.goto(origin+'/timetable?tab=duty');await roster.page.locator('app-duty-roster-export button').waitFor();await downloadCheck(roster.page,()=>roster.page.locator('app-duty-roster-export button').click(),'roster-download-390','official-roster');await capture(roster.page,'roster-export-390');await roster.page.close();
 const en=await documents(1440,'SchoolManager','Archived','en');await en.page.goto(origin+'/school-duty/documents?date=2026-10-13&execution=1');await en.page.locator('.document-card.available').first().waitFor();await en.page.locator('.documents').scrollIntoViewIfNeeded();await capture(en.page,'documents-en-1440');await en.page.close();
 assert.deepEqual(errors,[]);writeFileSync(resolve(evidence,'browser-results.json'),JSON.stringify({verification:'Actual rendered Angular SPA and generated synthetic PDF pages; mocked authenticated API presentation',report,hashes,pageErrors:errors,physicalDevice:'Pending; viewport simulation is not physical OS keyboard testing',ownerVisualReview:'Pending',liveProviderDelivery:'Not claimed'},null,2));console.log('D3 rendered document checks passed');
}finally{await browser.close();await new Promise(done=>server.close(done));}
