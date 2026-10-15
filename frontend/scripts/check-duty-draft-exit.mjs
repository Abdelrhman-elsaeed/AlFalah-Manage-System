// Actual Angular/browser behavior; intercepted synthetic APIs only. Never signs a real user's report.
import assert from 'node:assert/strict';
import { mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { chromium } from '@playwright/test';
const evidence=resolve('../docs/specs/school-duty/verification/draft-exit-screens');mkdirSync(evidence,{recursive:true});
const browser=await chromium.launch({headless:true});const results=[];
const envelope=data=>({isSuccess:true,data,errors:[]});
try{
 for(const [width,lang] of [[390,'ar'],[768,'ar'],[1440,'ar'],[1440,'en']]){
  const page=await browser.newPage({viewport:{width,height:1000},reducedMotion:'reduce'}),errors=[];page.setDefaultTimeout(15000);page.on('pageerror',e=>errors.push(e.message));
  page.on('dialog',async dialog=>{errors.push('Unexpected native dialog: '+dialog.type());await dialog.dismiss();});
  const user={userId:'teacher',fullName:'حساب اختبار المسودة',username:'draft-synthetic',activeSchoolId:1,activeSchoolName:'مدرسة اختبار اصطناعية',roles:['Instructor'],permissions:['Duty.View','Timetable.View'],preferredLanguage:lang};
  await page.addInitScript(({user,lang})=>{sessionStorage.setItem('alfalah_access_token',`e30.${btoa(JSON.stringify({sub:user.userId,exp:2000000000}))}.fixture`);sessionStorage.setItem('alfalah_user',JSON.stringify(user));localStorage.setItem('alfalah_language',lang);localStorage.setItem('language',lang);},{user,lang});
  const header={school:user.activeSchoolName,city:'اختبار',header:null,footer:null,logoUrl:null,manager:'مدير اصطناعي',year:'2026–2027',semester:1,originalTeacher:user.fullName,actualTeacher:user.fullName,isSubstitute:false};
  const content={noLateStudents:true,noViolations:true,violations:[],observations:'مسودة اختبار محفوظة',lastDeparture:'14:15:00'};
  const state={saves:0,signs:0,failSave:false,execution:{assignmentId:1,assignmentRevision:1,date:'2026-10-15',canExecute:true,canManage:false,header,dismissalAt:'13:30:00',timingException:null,signatureData:'data:image/png;base64,iVBORw0KGgo=',acknowledged:true,draft:{id:1,version:1,assignmentRevision:1,authorUserId:'teacher',content,submittedAt:null,savedAt:'2026-10-15T10:00:00Z'},lateStudents:[],documents:[],priorDrafts:[]}};
  await page.route('**/api/**',async route=>{
   const req=route.request(),path=new URL(req.url()).pathname;let data={};
   if(path.endsWith('/auth/me'))data=user;
   else if(path.endsWith('/account/signature/requirement'))data={required:false,hasSignature:true};
   else if(path.endsWith('/auth/schools'))data=[];
   else if(path.endsWith('/school-duty/context'))data={canManage:false,canDelegate:false,school:header.school,today:state.execution.date,userId:user.userId,terms:[{id:1,yearId:1,year:header.year,semester:1,from:'2026-10-01',to:'2026-12-31'}],termId:1,planRevision:1,settings:null,teachers:[],suggestedWeekdays:[0,1,2,3,4],days:[{id:1,date:state.execution.date,teacherId:'teacher',teacher:user.fullName,originalTeacherId:'teacher',originalTeacher:user.fullName,revision:1,locked:true,protected:false,isMine:true,attendance:'Unknown'}],history:[],fairness:[],pendingRetries:0,closures:[],ownHistory:[]};
   else if(path.endsWith('/execution'))data=state.execution;
   else if(path.endsWith('/report/draft')){
    if(state.failSave)return route.fulfill({status:503,json:{isSuccess:false,message:'تعذر حفظ المسودة. / Draft could not be saved.'}});
    state.saves++;state.execution.draft={...state.execution.draft,version:state.execution.draft.version+1,content:req.postDataJSON().content};data=state.execution;
   }else if(path.endsWith('/report/submit')){state.signs++;state.execution.draft.submittedAt='2026-10-15T11:00:00Z';data=state.execution;}
   else if(path.endsWith('/documents'))data={readiness:{enabled:true,processingEnabled:true,connectionState:'Connected'},documents:[]};
   await route.fulfill({json:envelope(data)});
  });
  const origin='http://localhost:4200',execution=origin+'/school-duty/execution',notice=page.locator('.draft-exit-dialog[open]');
  const open=async()=>{
   await page.goto(execution);await page.locator('.report-section').first().waitFor();
   if(lang==='en')await page.evaluate(async()=>{const component=window.ng.getComponent(document.querySelector('app-duty-execution'));await new Promise((resolve,reject)=>component.translate.use('en').subscribe({next:resolve,error:reject}));window.ng.applyChanges(component);});
  };
  const navigate=async()=>{
   const link=page.locator('#sidebar-category-duty a[href^="/school-duty/documents"]').first();
   for(let attempt=0;attempt<2&&!await link.isVisible();attempt++){await page.locator('button[aria-controls="sidebar-category-duty"]').click();await page.waitForTimeout(150);}
   await link.click();
  };
  const edit=async text=>{await page.locator('.section-body textarea').last().fill(text);await page.waitForFunction(()=>window.ng.getComponent(document.querySelector('app-duty-execution'))?.dirty()===false);};
  await open();await navigate();await page.waitForURL(url=>url.pathname.endsWith('/documents'));assert.equal(await notice.count(),0,'Untouched existing draft never prompts');
  await open();await edit('عمل اصطناعي قبل المغادرة');assert.equal(await notice.count(),0,'Autosave itself never opens notice');
  await navigate();await notice.waitFor();assert(new URL(page.url()).pathname.endsWith('/execution'));assert.equal(state.signs,0);
  const box=await notice.boundingBox();assert(Math.abs(box.x+box.width/2-width/2)<2);assert(Math.abs(box.y+box.height/2-500)<2);assert.equal(await notice.locator('li').count(),3);
  assert.equal(await notice.getAttribute('dir'),lang==='en'?'ltr':'rtl');assert((await notice.locator('h2').innerText()).includes(lang==='en'?'has not been submitted':'لم يُسلَّم'));
  assert(await notice.locator('.continue-report').evaluate(x=>x===document.activeElement));
  await page.keyboard.press('Shift+Tab');assert(await notice.locator('.leave-draft').evaluate(x=>x===document.activeElement));await page.keyboard.press('Tab');assert(await notice.locator('.continue-report').evaluate(x=>x===document.activeElement));
  await page.screenshot({path:resolve(evidence,`draft-exit-${lang}-${width}.png`),fullPage:true});
  await notice.locator('.continue-report').click();await notice.waitFor({state:'hidden'});assert(new URL(page.url()).pathname.endsWith('/execution'));assert.equal(await page.locator('.section-body textarea').last().inputValue(),'عمل اصطناعي قبل المغادرة');
  await navigate();await notice.waitFor();await notice.locator('.leave-draft').click();await page.waitForURL(url=>url.pathname.endsWith('/documents'));assert.equal(state.signs,0);assert.equal(state.execution.draft.submittedAt,null);
  await open();await page.locator('.execution-actionbar .primary-action').last().click();await page.locator('.review-page').waitFor();assert.equal(await notice.count(),0);
  await page.locator('.review-page .primary-action').click();await page.locator('.submitted-banner').waitFor();assert.equal(state.signs,1);
  await navigate();await page.waitForURL(url=>url.pathname.endsWith('/documents'));assert.equal(await notice.count(),0,'Submitted report never prompts');
  // Full workspace departure also waits for the same custom dialog, without window.confirm.
  state.execution.draft.submittedAt=null;await open();await edit('خروج كامل دون فقد المسودة');
  await page.evaluate(()=>{void window.ng.getComponent(document.querySelector('app-duty-execution')).router.navigate(['/account/signature']);});await notice.waitFor();await notice.locator('.leave-draft').click();await page.waitForURL(url=>url.pathname==='/account/signature');
  assert.equal(state.signs,1,'Departure never signs automatically');
  // Failed save blocks departure and retains input; no false saved-as-draft claim.
  await open();state.failSave=true;await page.locator('.section-body textarea').last().fill('مسودة تبقى عند فشل الحفظ');await page.locator('.execution-nav button').click();
  const error=page.locator('.duty-error-dialog[open]');await error.waitFor();assert.equal(await notice.count(),0);assert(new URL(page.url()).pathname.endsWith('/execution'));assert.equal(await page.locator('.section-body textarea').last().inputValue(),'مسودة تبقى عند فشل الحفظ');
  await error.locator('button').click();state.failSave=false;
  assert.deepEqual(errors,[]);
  results.push({width,language:lang,actualAngular:true,interceptedSyntheticApi:true,untouchedDraftNoNotice:true,autosaveNoRandomNotice:true,centeredThreeSteps:true,keyboardFocusCycle:true,continuePreservesInput:true,okLeavesDraft:true,noImplicitSigning:true,submittedNoNotice:true,wholeWorkspaceGuard:true,failedSaveRetainsInput:true});await page.close();
 }
 writeFileSync(resolve(evidence,'browser-results.json'),JSON.stringify({at:new Date().toISOString(),results,physicalDevice:'Pending',ownerVisualReview:'Pending',providerWrites:0},null,2));console.log('Draft departure browser checks passed at390/768/1440 and English desktop. Synthetic APIs; zero provider writes.');
}finally{await browser.close();}
