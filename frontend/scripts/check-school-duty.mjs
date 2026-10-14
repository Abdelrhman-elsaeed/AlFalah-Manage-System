// Rendered SPA verification with isolated API fixtures. No operational API or credentials.
import { createServer } from 'node:http';
import { readFileSync, existsSync, mkdirSync, writeFileSync } from 'node:fs';
import { resolve, extname, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import assert from 'node:assert/strict';
import { chromium } from '@playwright/test';
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const dist = resolve(root, 'dist/al-falah-app/browser');
const evidence = (process.env.ALFALAH_DUTY_BROWSER_EVIDENCE ? resolve(process.env.ALFALAH_DUTY_BROWSER_EVIDENCE) : resolve(root, '../docs/specs/school-duty/verification/screens'));
mkdirSync(evidence,{recursive:true});
const mime={'.html':'text/html','.js':'application/javascript','.css':'text/css','.json':'application/json','.woff2':'font/woff2','.png':'image/png','.svg':'image/svg+xml'};
const server=createServer((req,res)=>{let path=resolve(dist,decodeURIComponent(new URL(req.url,'http://localhost').pathname).replace(/^\/+/, '')||'index.html');if(!path.startsWith(dist)){res.writeHead(403);return res.end();}if(!existsSync(path))path=resolve(dist,'index.html');res.writeHead(200,{'Content-Type':mime[extname(path)]||'application/octet-stream'});res.end(readFileSync(path));});
await new Promise(done=>server.listen(0,'127.0.0.1',done));
const origin=`http://127.0.0.1:${server.address().port}`;
const browser=await chromium.launch({headless:true});const errors=[];const report=[];
const teachers=[{profileId:1,userId:'a',name:'محمود عبد الرحمن العتيبي'},{profileId:2,userId:'b',name:'علي محمد القحطاني'},{profileId:3,userId:'c',name:'حسن عبدالله الزهراني'},{profileId:4,userId:'d',name:'عبد الرحمن صالح عبد الرحمن الطويل'},{profileId:5,userId:'e',name:'خالد أحمد السبيعي'}];
const settings={termId:1,from:'2026-10-11',to:'2026-11-05',weekdays:[0,1,2,3,4],teacherIds:teachers.map(x=>x.userId),exclusions:[],closures:[],regenerateFrom:'2026-10-14',regenerateTo:'2026-11-05',acceptAttendanceCalendarMismatch:false};
function fixture(manager=true){
 const days=[];let i=0;for(let date=new Date('2026-10-11T12:00:00');date<=new Date('2026-11-05T12:00:00');date.setDate(date.getDate()+1)){if(date.getDay()>4)continue;const t=teachers[i%5];days.push({id:++i,date:date.toISOString().slice(0,10),originalTeacherId:t.userId,originalTeacher:t.name,teacherId:t.userId,teacher:t.name,revision:1,protected:false,locked:false,exceptionCode:null,attendance:manager?'Unknown':null,isMine:t.userId==='a'});}
 const today=days.find(x=>x.date==='2026-10-13');today.teacherId='b';today.teacher=teachers[1].name;today.originalTeacherId='a';today.originalTeacher=teachers[0].name;today.isMine=false;today.changeKind='AutoExchange';
 days.find(x=>x.date==='2026-10-14').teacherId='a';days.find(x=>x.date==='2026-10-14').teacher=teachers[0].name;days.find(x=>x.date==='2026-10-14').changeKind='AutoExchange';
 if(manager)days.find(x=>x.date==='2026-10-15').exceptionCode='NoEligibleSubstitute';
 const history=manager?[{id:2,kind:'AutoExchange',date:'2026-10-13',otherDate:'2026-10-14',before:teachers[0].name,after:teachers[1].name,otherBefore:teachers[1].name,otherAfter:teachers[0].name,reason:'Recorded Absent staff attendance',actor:'system',at:'2026-10-13T08:15:00Z',relatedChangeId:null,unresolved:false,beforeTeacherId:'a',afterTeacherId:'b'},
 {id:3,kind:'UnresolvedCompensation',date:'2026-10-08',otherDate:null,before:teachers[3].name,after:teachers[4].name,reason:'Recorded Excused staff attendance',actor:'system',at:'2026-10-08T08:00:00Z',relatedChangeId:null,unresolved:true,beforeTeacherId:'d',afterTeacherId:'e'}]:[];
 return {canManage:manager,school:'مدارس الفلاح · المرحلة الابتدائية',today:'2026-10-13',userId:manager?'manager':'a',terms:[{id:1,yearId:1,year:'1448 هـ / 2026–2027',semester:1,from:'2026-10-11',to:'2026-11-05'}],termId:1,planRevision:3,settings:manager?structuredClone(settings):null,teachers:manager?teachers:[],suggestedWeekdays:[0,1,2,3,4],days,history,fairness:manager?teachers.map((t,i)=>({userId:t.userId,name:t.name,planned:4,effective:i===3?3:i===4?5:4,covered:i===1?1:0,completed:0,pendingCompensation:i===0||i===3?1:0})):[],pendingRetries:0,closures:[]};
}
async function setup(width,manager=true,mode='normal',lang='ar'){
 const page=await browser.newPage({viewport:{width,height:1000},reducedMotion:'reduce'});page.on('pageerror',e=>{errors.push(e.message);console.error('Page error:',e.message);});
 page.on('dialog',async dialog=>{errors.push('Unexpected browser dialog: '+dialog.message());await dialog.dismiss();});
 const current={userId:manager?'manager':'a',username:'fixture',fullName:'حساب فحص الواجهة',activeSchoolId:1,activeSchoolName:'مدارس الفلاح',preferredLanguage:lang,roles:[manager?'SchoolManager':'Instructor'],permissions:['Timetable.View',...(manager?['Timetable.Manage','Duty.Manage','Duty.View']:['Duty.View'])]};
 const state={data:fixture(manager),mode,publish:[],changes:[],previews:[],delay:false};if(mode==='empty')state.data.days=[];
 await page.addInitScript(({current,lang})=>{sessionStorage.setItem('alfalah_access_token',`e30.${btoa(JSON.stringify({sub:current.userId,exp:2000000000}))}.fixture`);sessionStorage.setItem('alfalah_user',JSON.stringify(current));localStorage.setItem('alfalah_language',lang);localStorage.setItem('language',lang);}, {current,lang});
 await page.route('**/api/**',async route=>{
  const req=route.request(),url=new URL(req.url()),path=url.pathname;let data={};
  if(path.endsWith('/auth/me'))data=current;
  else if(path.endsWith('/account/signature/requirement'))data={required:false,hasSignature:true};
  else if(path.endsWith('/auth/schools'))data=[];
  else if(path.endsWith('/school-duty/context')){if(state.mode==='error')return route.fulfill({status:503,json:{isSuccess:false,message:'تعذر الاتصال بالخادم. أعد المحاولة.'}});if(state.mode==='loading')await new Promise(done=>setTimeout(done,1800));data=state.data;}
  else if(path.endsWith('/school-duty/plans/preview')){const body=req.postDataJSON();state.previews.push(body);data={digest:'fixture-digest',changedDates:16,warnings:[],days:state.data.days.map(x=>({date:x.date,teacherId:x.teacherId,teacher:x.teacher,before:x.originalTeacher,preserved:x.date<'2026-10-14',exceptionCode:null})),fairness:state.data.fairness};}
  else if(path.endsWith('/school-duty/plans/publish')){state.publish.push(req.postDataJSON());state.data.settings=state.publish.at(-1).settings;state.data.planRevision++;data={planId:1,revision:state.data.planRevision};}
  else if(path.endsWith('/school-duty/assignments/change-preview')){const body=req.postDataJSON();state.previews.push(body);const day=state.data.days.find(x=>x.id===body.assignmentId),other=state.data.days.find(x=>x.id===body.otherAssignmentId);data={digest:'manual-fixture',changedDates:other?2:1,warnings:body.teacherId==='d'?['ManualEligibilityException']:[],days:[{date:day.date,teacherId:body.teacherId,teacher:teachers.find(x=>x.userId===body.teacherId).name,before:day.teacher,preserved:false,exceptionCode:null},...(other?[{date:other.date,teacherId:day.teacherId,teacher:day.teacher,before:other.teacher,preserved:false,exceptionCode:null}]:[])],fairness:state.data.fairness};}
  else if(path.endsWith('/school-duty/assignments/change')){const body=req.postDataJSON();state.changes.push(body);assert(body.reason.trim());const day=state.data.days.find(x=>x.id===body.assignmentId);day.teacherId=body.teacherId;day.teacher=teachers.find(x=>x.userId===body.teacherId).name;day.protected=true;state.data.planRevision++;data={};}
  else if(path.endsWith('/timetables/catalog'))data={academicYears:[{id:1,nameAr:'1448',isActive:true}],semesters:[{value:1,labelAr:'الفصل الأول'},{value:2,labelAr:'الفصل الثاني'}],days:[{value:2,labelAr:'الأحد'},{value:3,labelAr:'الاثنين'}],teachers:teachers.map(x=>({instructorProfileId:x.profileId,userId:x.userId,fullName:x.name,classes:['1/أ'],isCurrentUser:x.userId==='a',subject:'اللغة العربية'})),moderators:[],capabilities:{canManage:manager,canDelegate:manager},bellSchedule:null};
  else if(path.endsWith('/timetables/current'))data=null;
  else if(path.endsWith('/timetables'))data=[];
  else if(path.endsWith('/storage/access'))return route.fulfill({status:403,json:{isSuccess:false}});
  else if(path.includes('/timetable-settings/'))data={profiles:[],bellSchedule:null};
  else if(path.includes('capabilities'))data={canAccess:false};
  else if(path.includes('notifications'))data={items:[],totalCount:0,unreadCount:0};
  await route.fulfill({json:{isSuccess:true,data,errors:[]}});
 });
 return {page,state};
}
async function capture(page,name){await page.screenshot({path:resolve(evidence,`${name}.png`),fullPage:true,animations:'disabled'});const sizes=await page.evaluate(()=>[{scroll:document.documentElement.scrollWidth,width:innerWidth},...Array.from(document.querySelectorAll('.duty-workspace,.duty-dialog .p-dialog-content')).filter(x=>x.clientWidth).map(x=>({scroll:x.scrollWidth,width:x.clientWidth}))]);for(const size of sizes)assert(size.scroll<=size.width+1,`${name}: overflow ${size.scroll}/${size.width}`);}
async function pickDate(page,id,day){
 await page.locator('.p-datepicker:visible').waitFor({state:'hidden'});
 await page.locator(`#${id}`).click();await page.locator('.p-datepicker:visible').getByText(String(day),{exact:true}).first().click();
 await page.waitForFunction(({id,day})=>document.getElementById(id)?.value?.endsWith('-'+String(day).padStart(2,'0')),{id,day});
 await page.locator('.p-datepicker:visible').waitFor({state:'hidden'});
}
try{
 for(const width of [390,768,1440]){
  const {page,state}=await setup(width);await page.goto(origin+'/timetable?tab=duty');await page.locator('.today-card').waitFor();await capture(page,`manager-${width}`);
  await page.locator('.schedule-panel').scrollIntoViewIfNeeded();await capture(page,`week-schedule-${width}`);await page.locator('.duty-heading').scrollIntoViewIfNeeded();
  assert((await page.locator('.duty-day-card').count())<=10);assert.equal(await page.locator('.schedule-filters button').count(),2);await capture(page,`term-${width}`);
  await page.getByRole('button',{name:'حالات تحتاج المراجعة',exact:false}).count();
  await page.locator('.overview-summary button').nth(1).click();await page.waitForFunction(()=>document.querySelectorAll('.duty-workspace .day-row').length===1);await page.locator('.duty-workspace .day-row').click();await page.locator('.day-detail').waitFor();await capture(page,`exception-${width}`);await page.keyboard.press('Escape');await page.locator('.filter-chip').click();
  await page.locator('.today-card .hero-action').click();await page.getByRole('button',{name:'تعديل المناوبة',exact:true}).click();
  await page.locator('#duty-replacement').click();await page.getByRole('option',{name:'خالد أحمد السبيعي',exact:true}).click();
  await page.locator('#duty-exchange-date').click();await page.getByRole('option').first().click();await page.locator('textarea').fill('تبادل طارئ موثّق لتغطية المناوبة');
  await page.getByRole('button',{name:'مراجعة التعديل',exact:true}).click();await page.locator('.exchange-review').waitFor();assert.equal(await page.locator('.exchange-review article').count(),2);await capture(page,`exchange-${width}`);
  await page.getByRole('button',{name:'تأكيد التعديل',exact:true}).click();await page.locator('.manual-modes').waitFor({state:'hidden'});assert.equal(state.changes.length,1);
  await page.getByRole('button',{name:'إعدادات المناوبة',exact:true}).click();await page.locator('.teacher-pool').waitFor();
  await page.locator('.rotation-order button').nth(3).click();await page.locator('.pool-row').last().locator('.pool-actions button').first().click();assert.equal(await page.locator('.pool-row.selected').count(),4);
  await page.locator('.shell-content').evaluate(x=>x.scrollTop=0);await page.locator('.teacher-pool').evaluate(x=>x.scrollTop=0);await page.locator('.rotation-order').evaluate(x=>x.scrollTop=0);await capture(page,`setup-${width}`);

  await page.locator('.exclusion-modes button').nth(1).click();await page.locator('#duty-exclusion-teacher').click();await page.getByRole('option',{name:teachers[3].name,exact:true}).click();await pickDate(page,'duty-exclude-from',14);await pickDate(page,'duty-exclude-to',16);await page.locator('.exclusion-fields > label:last-child input').fill('استبعاد مؤقت من المناوبة');await page.getByRole('button',{name:'إضافة استبعاد',exact:true}).click();
  await page.locator('.exclusions-section .range-list').scrollIntoViewIfNeeded();await capture(page,`exclusions-${width}`);await page.locator('.exclusions-section .range-list > div').last().locator('button').click();assert.equal(await page.locator('.exclusions-section .range-list > div').count(),1);
  await page.locator('.exclusion-fields > label:last-child input').fill('استبعاد مؤقت من المناوبة');await page.getByRole('button',{name:'إضافة استبعاد',exact:true}).click();
  await page.locator('#duty-stage-calendar').click();await page.locator('.settings-calendar').waitFor();
  await pickDate(page,'duty-close-from',14);await pickDate(page,'duty-close-to',15);await page.locator('.setup-section .reason-field input').fill('إجازة مدرسية');await page.getByRole('button',{name:'إضافة فترة إغلاق',exact:true}).click();
  await page.getByRole('button',{name:'معاينة التوزيع',exact:true}).click();await page.locator('.preview-list').waitFor();await capture(page,`preview-${width}`);
  await page.getByRole('button',{name:'تأكيد ونشر المناوبات',exact:true}).click();await page.locator('.settings-page').waitFor({state:'hidden'});assert.equal(state.publish.length,1);assert.deepEqual(state.publish[0].settings.teacherIds,['a','c','b','d']);assert.equal(state.publish[0].settings.closures.length,1);assert.equal(state.publish[0].settings.exclusions.length,1);assert(state.publish[0].requestId);
  await page.waitForURL(url=>url.pathname==='/school-duty');await page.locator('.schedule-panel').waitFor();if(await page.locator('.shell-sidebar.is-collapsed').count())await page.getByRole('button',{name:'المناوبة',exact:true}).click();await page.locator('#sidebar-category-duty a[href^="/school-duty/fairness"]').click();await page.locator('.obligations').waitFor();await capture(page,`history-${width}`);
  await page.getByRole('button',{name:'تحديد تاريخ التعويض',exact:true}).click();await page.locator('#duty-resolve-date').click();await page.getByRole('option').last().click();await page.locator('textarea').fill('نقل التعويض إلى تاريخ محدد مع حفظ الالتزام');await page.getByRole('button',{name:'مراجعة التعديل',exact:true}).click();await page.locator('.exchange-review').waitFor();assert.equal(state.previews.at(-1).mode,'ResolveCompensation');await capture(page,`compensation-${width}`);await page.keyboard.press('Escape');
  // Independent sidebar keyboard entry; the lesson timetable is no longer mounted inside duty.
  if(await page.locator('.shell-sidebar.is-collapsed').count())await page.getByRole('button',{name:'المناوبة',exact:true}).click();const dutyLink=page.locator('#sidebar-category-duty a[href="/school-duty"]');await dutyLink.focus();await page.keyboard.press('Enter');assert.equal(new URL(page.url()).pathname,'/school-duty');assert(await dutyLink.evaluate(x=>x.classList.contains('is-active')));
  assert.equal(await page.locator('.timetable-page').count(),0);report.push({width,checks:['today','term','exception filter','manual two-date review/confirm','teacher selection/order','preview/publish','history/debt','sidebar keyboard entry','no document overflow','reduced motion']});await page.close();console.log(`Rendered and exercised ${width}px`);
 }
 for(const mode of ['empty','error','loading']){const {page}=await setup(390,true,mode);await page.goto(origin+'/timetable?tab=duty');if(mode==='loading')await page.locator('.hero-skeleton').waitFor();else await page.locator(mode==='empty'?'.empty-state':'.duty-error-dialog[open]').last().waitFor();await capture(page,`${mode}-390`);await page.close();}
 const teacher=await setup(390,false);await teacher.page.goto(origin+'/timetable?tab=duty');await teacher.page.locator('.today-card').waitFor();assert.equal(await teacher.page.getByRole('button',{name:'إعدادات المناوبة',exact:true}).count(),0);assert.equal(await teacher.page.locator('.fairness-panel').count(),0);await capture(teacher.page,'teacher-390');await teacher.page.close();
 const english=await setup(1440);await english.page.goto(origin+'/timetable?tab=duty');await english.page.locator('.today-card').waitFor();
 await english.page.evaluate(()=>{const component=window.ng.getComponent(document.querySelector('app-school-duty'));component.translate.use('en');});
 await english.page.waitForFunction(()=>document.querySelector('.duty-workspace')?.getAttribute('dir')==='ltr');await english.page.getByRole('button',{name:'Duty settings',exact:true}).waitFor();await capture(english.page,'manager-en-1440');await english.page.close();
 const warning=await setup(390);await warning.page.goto(origin+'/timetable?tab=duty');await warning.page.locator('.today-card .hero-action').click();await warning.page.getByRole('button',{name:'تعديل المناوبة',exact:true}).click();await warning.page.getByRole('button',{name:'استبدال مباشر',exact:true}).click();await warning.page.locator('#duty-replacement').click();await warning.page.getByRole('option',{name:teachers[3].name,exact:true}).click();await warning.page.locator('textarea').fill('استثناء موثّق بسبب ظرف طارئ');await warning.page.getByRole('button',{name:'مراجعة التعديل',exact:true}).click();await warning.page.locator('.exchange-review').waitFor();assert(await warning.page.getByRole('button',{name:'تأكيد التعديل',exact:true}).isDisabled());await capture(warning.page,'manual-warning-390');await warning.page.locator('.exception-checkbox input').check();await warning.page.getByRole('button',{name:'تأكيد التعديل',exact:true}).click();await warning.page.locator('.exchange-review').waitFor({state:'hidden'});await warning.page.close();
 const inactive=await setup(390);inactive.state.data.teachers=structuredClone(inactive.state.data.teachers);inactive.state.data.teachers[0].isEligible=false;await inactive.page.goto(origin+'/timetable?tab=duty');await inactive.page.locator('.today-card').waitFor();await inactive.page.getByRole('button',{name:'إعدادات المناوبة',exact:true}).click();const inactiveRow=inactive.page.locator('.pool-row').filter({hasText:teachers[0].name});await capture(inactive.page,'inactive-selection-390');assert.equal(await inactiveRow.locator('.pool-actions').count(),0);assert(!(await inactiveRow.evaluate(x=>x.classList.contains('selected'))));await inactive.page.locator('#duty-stage-calendar').click();await inactive.page.getByRole('button',{name:'معاينة التوزيع',exact:true}).click();await inactive.page.locator('.preview-list').waitFor();assert(!inactive.state.previews.at(-1).settings.teacherIds.includes('a'));await inactive.page.keyboard.press('Escape');await inactive.page.getByRole('button',{name:'إلغاء',exact:true}).click();await inactive.page.close();
 assert.deepEqual(errors,[]);writeFileSync(resolve(evidence,'browser-results.json'),JSON.stringify({verification:'Mock API fixtures / actual rendered Angular SPA',report,pageErrors:errors,states:['empty','error','loading','teacher','English LTR','manual eligibility warning','closures','exclusions and restoration','inactive selected teacher removal','nested preview Escape retains settings','explicit compensation date review'],clientVisualAcceptance:'Pending owner review'},null,2));
 console.log('Duty rendered checks passed. Evidence: '+evidence);
}finally{await browser.close();await new Promise(done=>server.close(done));}
