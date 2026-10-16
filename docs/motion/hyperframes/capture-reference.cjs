// Capture the unchanged application using synthetic, browser-intercepted data.
// Every API request is fulfilled locally; no production/student data is read.
const { chromium } = require('../../../frontend/node_modules/@playwright/test');
const path = require('node:path');
const fs = require('node:fs');
const out = __dirname;
const actor = { userId:'motion-demo', username:'motion-demo', fullName:'حساب العرض التوضيحي',
  activeSchoolId:1, activeSchoolName:'متوسطة الفلاح الأهلية', preferredLanguage:'ar',
  roles:['SchoolManager'], permissions:['Dashboard.View','Timetable.View','Timetable.Manage','SchoolDuty.View','SchoolDuty.Manage','StudentAffairs.View','Visit.View','Instructor.View','Storage.View'] };
const counts = {total:240,present:228,absent:8,excused:4,recorded:240,unrecorded:0,attendanceRate:95,recordingRate:100};
const dashboard = {schoolId:1,schoolName:actor.activeSchoolName,instructorsCount:24,moderatorsCount:3,visitsThisMonthCount:12,
  instructorsNeedingImprovementCount:0,evaluationsPendingApprovalCount:2,complaintsCount:0,openComplaintsCount:0,
  visitsByStatus:[{status:4,statusLabelAr:'معتمدة',count:10},{status:3,statusLabelAr:'بانتظار الاعتماد',count:2}],
  subjectPerformance:[],moderatorPerformance:[],instructorsNeedingImprovement:[],
  improvementPlans:{totalActive:3,totalCompleted:6,totalCancelled:0,totalFollowUps:18,plansWithAtLeastOneFollowUp:3,averageLatestProgressScore:80},appliedFilters:{}};
const operations = {generatedAt:'2026-10-15T07:00:00Z',date:'2026-10-15',timeZone:'Asia/Riyadh',
  students:{counts,classrooms:['الأول أ','الأول ب','الثاني أ','الثاني ب','الثالث أ','الثالث ب'].map((label,i)=>({classroomId:i+1,label,total:40,present:38,absent:i<2?2:1,excused:i<2?0:1})),trend:[]},
  staff:{counts:{...counts,total:24,present:24,absent:0,excused:0,recorded:24,attendanceRate:100},absences:[],trend:[]},
  summons:{pending:0,attended:2,underObservation:1,improved:3,appointments:[]},
  schoolDay:{name:'اليوم الدراسي',timeZone:'Asia/Riyadh',slots:[]},activities:[],recentVisits:[],gatePasses:0,conversations:0};
const names=['معلم الرياضيات','معلم العلوم','معلم اللغة العربية','معلم اللغة الإنجليزية','معلم الدراسات الإسلامية','معلم الدراسات الاجتماعية'];
const subjects=['رياضيات','علوم','لغة عربية','لغة إنجليزية','دراسات إسلامية','اجتماعيات'];
const periods=Array.from({length:6},(_,i)=>({sequence:i+1,displayLabel:`الحصة ${i+1}`,startLocalTime:`${String(7+Math.floor(i*0.75)).padStart(2,'0')}:${i%2?'45':'00'}:00`,endLocalTime:`${String(8+Math.floor(i*0.75)).padStart(2,'0')}:30:00`}));
const capabilities={canManage:true,canDelegate:false,canViewVersions:true};
const bellSchedule={id:1,revisionId:1,revision:1,name:'التوقيت المدرسي',schoolTimeZoneId:'Asia/Riyadh',defaultPeriods:periods,defaultBreaks:[],days:[2,3,4,5,6].map(day=>({day,isStudyDay:true,usesDefaultSchedule:true,periods:[],usesDefaultBreaks:true,breaks:[]}))};
const teachers=names.map((fullName,i)=>({instructorProfileId:i+1,userId:`demo-${i}`,fullName,classes:['الأول أ','الثاني أ','الثالث أ'],isCurrentUser:false}));
const timetable={id:1,schoolId:1,academicYearId:1,academicYearName:'2026/2027',semester:1,semesterLabelAr:'الفصل الأول',title:'الجدول المدرسي',revision:1,isPublished:true,isClone:false,updatedAt:'2026-10-15T07:00:00Z',publishedAt:'2026-10-15T07:00:00Z',timetableSetupProfileId:1,capabilities,bellSchedule,timingsRequireRevalidation:false,
 entries:teachers.flatMap((t,i)=>[2,3,4,5,6].flatMap(day=>periods.filter((_,p)=>(p+i+day)%3!==0).map((p,j)=>({id:i*100+day*10+j,instructorProfileId:t.instructorProfileId,day,period:p.sequence,entryType:1,classLabel:['الأول أ','الثاني أ','الثالث أ'][(i+j)%3],subject:subjects[i]})))),
 teacherSummaries:teachers.map(t=>({instructorProfileId:t.instructorProfileId,lessonCount:20,standbyCount:0}))};
(async()=>{
 const browser=await chromium.launch({headless:true});
 const page=await browser.newPage({viewport:{width:1600,height:1000},deviceScaleFactor:1});
 const errors=[]; page.on('pageerror',e=>errors.push(e.message));
 await page.route('**/api/**',async route=>{
  const p=new URL(route.request().url()).pathname; let data=[];
  if(p.endsWith('/auth/me')) data=actor;
  else if(p.endsWith('/dashboard/school-manager/operations')) data=operations;
  else if(p.endsWith('/dashboard/school-manager')) data=dashboard;
  else if(p.endsWith('/notifications')) data={items:[],total:0};
  else if(p.endsWith('/catalog')) data={schoolId:1,schoolName:actor.activeSchoolName,capabilities,moderators:[],bellSchedule,academicYears:[{id:1,code:'2026',nameAr:'2026/2027',isActive:true}],semesters:[{value:1,labelAr:'الفصل الأول'}],days:['السبت','الأحد','الاثنين','الثلاثاء','الأربعاء','الخميس'].map((labelAr,i)=>({value:i+1,labelAr})),teachers};
  else if(p.endsWith('/current')) data=timetable;
  else if(p==='/api/v1/timetables') data=[{id:1,title:timetable.title,isPublished:true,isClone:false}];
  else if(p.endsWith('/timetables/1')) data=timetable;
  else if(p.includes('/feature')) data={};
  else if(p.endsWith('/signature/requirement')) data={required:false};
  await route.fulfill({status:200,contentType:'application/json',body:JSON.stringify({isSuccess:true,data,message:'',errors:[]})});
 });
 await page.goto('http://localhost:4200/',{waitUntil:'networkidle'});
 await page.evaluate(()=>document.fonts.ready); await page.waitForTimeout(1200);
 await page.locator('[data-reveal]').evaluateAll(elements=>elements.forEach(element=>element.classList.add('is-revealed')));
 await page.waitForTimeout(900);
 await page.screenshot({path:path.join(out,'reference/home-desktop.png'),fullPage:true});
 await page.setViewportSize({width:390,height:844});
 await page.screenshot({path:path.join(out,'reference/home-mobile.png'),fullPage:true});
 await page.setViewportSize({width:1600,height:1000});
 await page.addInitScript(user=>{
  sessionStorage.setItem('alfalah_access_token',`e30.${btoa(JSON.stringify({sub:user.userId,exp:2000000000}))}.mock`);
  sessionStorage.setItem('alfalah_user',JSON.stringify(user));
 },actor);
 for(const [name,url] of [['dashboard','/school-manager/dashboard'],['timetable','/timetable']]){
  await page.goto(`http://localhost:4200${url}`,{waitUntil:'networkidle'});
  await page.evaluate(()=>document.fonts.ready); await page.waitForTimeout(1200);
  await page.screenshot({path:path.join(out,`assets/${name}.png`)});
  console.log(name,page.url(),await page.locator('h1').allTextContents());
 }
 fs.writeFileSync(path.join(out,'reference/capture-report.json'),JSON.stringify({syntheticData:true,apiRequestsIntercepted:true,errors},null,2));
 await browser.close();
})();
