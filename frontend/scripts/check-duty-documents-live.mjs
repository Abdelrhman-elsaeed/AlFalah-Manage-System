// Actual document/execution/schedule APIs use isolated HTTP/JWT/SQL. Other app-shell APIs are synthetic fixtures.
import {createServer} from 'node:http';
import {readFileSync,existsSync,mkdirSync,writeFileSync} from 'node:fs';
import {resolve,extname,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
import assert from 'node:assert/strict';
import {chromium} from '@playwright/test';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'..'),dist=resolve(root,'dist/al-falah-app/browser'),evidence=resolve(root,'../docs/specs/school-duty/verification/d3-live-screens');
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
const config=JSON.parse(readFileSync(process.argv[2],'utf8'));const {createHash}=await import('node:crypto');const checks=[];
try {
 for(const user of config.users) for(const width of [390,768,1440]) {
  const active={...config,...user};const suffix=user.role==='SchoolManager'?'manager-':'';
  const {page}=await setup(width,user.role);
  await page.addInitScript(config=>{sessionStorage.setItem('alfalah_access_token',config.token);const user=JSON.parse(sessionStorage.getItem('alfalah_user'));user.userId=config.userId;user.activeSchoolId=config.school;sessionStorage.setItem('alfalah_user',JSON.stringify(user));},active);
  let authenticated=0;
  await page.route('**/api/v1/school-duty/**',async route=>{
   const req=route.request(),url=new URL(req.url());assert(req.headers().authorization==='Bearer '+active.token,'Actual authenticated bearer header required');authenticated++;
   const response=await route.fetch({url:config.origin+url.pathname+url.search,headers:{...req.headers(),authorization:'Bearer '+active.token}});
   await route.fulfill({response});
  });
  await page.goto(origin+`/timetable?tab=duty&date=${config.date}&execution=${config.assignment}`);await page.locator('.document-card.available').first().waitFor();
  await page.locator('.documents').scrollIntoViewIfNeeded();await capture(page,`live-${suffix}documents-${width}`);await page.locator('.documents').screenshot({path:resolve(evidence,`live-${suffix}document-cards-${width}.png`)});
  await page.locator('.document-card').last().locator('.primary-action').click();await page.locator('.pdf-pages canvas').first().waitFor();await page.locator('.preview-message[role=status]').waitFor({state:'hidden'});
  await page.locator('.duty-pdf-dialog').screenshot({path:resolve(evidence,`live-${suffix}report-preview-${width}.png`)});
  const [download]=await Promise.all([page.waitForEvent('download'),page.locator('.dialog-download').click()]);const target=resolve(evidence,`live-${suffix}authenticated-report-${width}.pdf`);await download.saveAs(target);
  const hash=createHash('sha256').update(readFileSync(target)).digest('hex').toUpperCase();assert.equal(hash,config.expectedHash);assert.match(download.suggestedFilename(),/\.pdf$/i);
  await page.keyboard.press('Escape');await page.locator('.duty-pdf-dialog').waitFor({state:'hidden'});
  await page.locator('.document-card').first().locator('.primary-action').click();await page.locator('.pdf-pages canvas').first().waitFor();await page.locator('.preview-message[role=status]').waitFor({state:'hidden'});
  await page.locator('.duty-pdf-dialog').screenshot({path:resolve(evidence,`live-${suffix}letter-preview-${width}.png`)});
  const [letterDownload]=await Promise.all([page.waitForEvent('download'),page.locator('.dialog-download').click()]);const letterPath=resolve(evidence,`live-${suffix}authenticated-letter-${width}.pdf`);await letterDownload.saveAs(letterPath);
  const letterHash=createHash('sha256').update(readFileSync(letterPath)).digest('hex').toUpperCase();assert.equal(letterHash,config.expectedLetterHash);await page.keyboard.press('Escape');await page.locator('.duty-pdf-dialog').waitFor({state:'hidden'});
  await page.goto(origin+'/timetable?tab=duty');await page.locator('app-duty-roster-export button').waitFor();
  const [rosterDownload]=await Promise.all([page.waitForEvent('download'),page.locator('app-duty-roster-export button').click()]);const rosterPath=resolve(evidence,`live-${suffix}authenticated-roster-${width}.pdf`);await rosterDownload.saveAs(rosterPath);
  const rosterHash=createHash('sha256').update(readFileSync(rosterPath)).digest('hex').toUpperCase();assert.equal(rosterHash,config.expectedRosterHash);
  checks.push({role:user.role,width,authenticatedRequests:authenticated,hash,expected:config.expectedHash,letterHash,expectedLetterHash:config.expectedLetterHash,rosterHash,expectedRosterHash:config.expectedRosterHash,checks:['Actual HttpClient bearer requests to running isolated API','Real SQL signed letter/report artifacts','PDF.js rendered previews','Native browser letter/report/roster downloads and retained SHA256','External TestVerified status does not claim Drive archived']});await page.close();
 }
 assert.deepEqual(errors,[]);writeFileSync(resolve(evidence,'browser-results.json'),JSON.stringify({verification:'Live isolated local HTTP/JWT/SQL document, execution and schedule APIs; unrelated app shell API fixtures; no external provider delivery',checks,physicalKeyboard:'Pending physical device; headless browser keyboard tested',ownerVisualAcceptance:'Pending',errors},null,2));console.log('D3 live authenticated browser documents passed');
}finally{await browser.close();await new Promise(done=>server.close(done));}
