import assert from 'node:assert/strict';
import { chromium,expect } from '@playwright/test';
import {readFileSync,mkdirSync,writeFileSync} from 'node:fs';
import {resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
import {assignmentFixture,recompute} from './fixtures/assignment-ledger.mjs';
const root=fileURLToPath(new URL('../../',import.meta.url));
const evidence=resolve(root,'docs/specs/classroom-catalog/verification/assignments');mkdirSync(evidence,{recursive:true});
const sessions=Object.fromEntries(['manager','teacher'].map(role=>[role,JSON.parse(readFileSync(resolve(root,'.audit/classroom-consumers/'+role+'-session-private.json'),'utf8'))]));
const endpoint='http://localhost:5264/api/v1/intelligent-timetable/settings';
const response=await fetch(endpoint,{headers:{Authorization:'Bearer '+sessions.manager.token}});assert.equal(response.status,200);
const settings=await response.json(),profile=settings.data.selectedProfile.id;
const browser=await chromium.launch({headless:true}),results=[];
const wrap=data=>({isSuccess:true,data,message:'',errors:[]});
async function contextFor(role,width){const context=await browser.newContext({viewport:{width,height:1000},reducedMotion:'reduce'});await context.addInitScript(s=>{sessionStorage.setItem('alfalah_access_token',s.token);sessionStorage.setItem('alfalah_user',JSON.stringify(s.user));},sessions[role]);return context;}
try {
  for(const width of [390,768,1440]) {
    const context=await contextFor('manager',width),page=await context.newPage(),errors=[],writes=[];
    page.on('pageerror',e=>errors.push(e.message));page.on('request',r=>{if(r.url().includes('/api/')&&['PUT','POST','PATCH','DELETE'].includes(r.method()))writes.push(r.method());});
    await page.goto('http://localhost:4200/intelligent-timetable/assignments?profileId='+profile);
    await expect(page.locator('.assignment-cell').first()).toBeVisible({timeout:60000});
    await page.screenshot({path:resolve(evidence,'live-ledger-'+width+'.png'),fullPage:true});
    await page.locator('.assignment-cell').first().focus();await page.keyboard.press('Enter');
    await expect(page.locator('.teacher-results')).toHaveAttribute('aria-busy','false',{timeout:60000});
    await expect(page.locator('.p-dialog')).toHaveCSS('opacity','1');
    await page.screenshot({path:resolve(evidence,'live-picker-'+width+'.png')});
    await page.keyboard.press('Tab');assert.equal(await page.evaluate(()=>!!document.activeElement.closest('.p-dialog')),true);
    await expect(page.locator('.assignment-cell').first()).toHaveCSS('transition-duration','0s');
    await page.keyboard.press('Escape');await expect(page.locator('.p-dialog')).toBeHidden();await expect(page.locator('.assignment-cell').first()).toBeFocused();
    assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),true);assert.deepEqual(errors,[]);assert.deepEqual(writes,[]);
    results.push({kind:'real API',role:'manager',width,noBusinessWrites:true,noPageErrors:true,noPageOverflow:true,keyboardOpenCloseAndFocusTrap:true,focusReturned:true,reducedMotion:true});await context.close();
  }
  const teacherContext=await contextFor('teacher',1440),teacherPage=await teacherContext.newPage();
  await teacherPage.goto('http://localhost:4200/intelligent-timetable/assignments?profileId='+profile);
  await expect(teacherPage).not.toHaveURL(/intelligent-timetable\/assignments/);
  const denied=await fetch(endpoint+'/profiles/'+profile+'/assignments',{headers:{Authorization:'Bearer '+sessions.teacher.token}});assert.equal(denied.status,403);
  results.push({kind:'real API',role:'teacher',routeDenied:true,apiStatus:403});await teacherContext.close();

  for(const width of [390,768,1440]) {
    const context=await contextFor('manager',width),page=await context.newPage(),errors=[],requests=[];
    let data=assignmentFixture(),conflict=false;page.on('pageerror',e=>errors.push(e.message));
    await context.route('**/api/v1/intelligent-timetable/settings**',async route=>{
      const request=route.request(),url=new URL(request.url()),path=url.pathname;
      const send=(body,status=200)=>route.fulfill({status,contentType:'application/json',body:JSON.stringify(body)});
      if(path.endsWith('/assignments/teachers')) {
        const p=url.searchParams;requests.push(Object.fromEntries(p));if(p.get('search')==='network-failure')return send({message:'تعذر تحميل المعلمين'},503);
        let rows=data.teachers.filter(t=>(!p.get('search')||t.name.includes(p.get('search'))||t.specialization.includes(p.get('search'))) &&
          (!p.get('specialization')||t.specialization.includes(p.get('specialization'))) && (p.get('isActive')===''||p.get('isActive')===null||t.isActive===(p.get('isActive')==='true')) &&
          (!p.has('isVisiting')||t.isVisiting===(p.get('isVisiting')==='true')) && (p.get('hasCapacity')!=='true'||t.remainingPeriods>0));
        const sort=p.get('sort');rows.sort((a,b)=>sort==='load'?a.allocatedPeriods-b.allocatedPeriods:sort==='remaining'?a.remainingPeriods-b.remainingPeriods:a.id-b.id);if(p.get('descending')==='true')rows.reverse();
        const first=(Number(p.get('page'))-1)*Number(p.get('pageSize'));return send(wrap({items:rows.slice(first,first+Number(p.get('pageSize'))),totalCount:rows.length}));
      }
      if(path.endsWith('/assignments')) {
        if(request.method()==='PUT') {
          const body=request.postDataJSON();if(conflict){conflict=false;data.revision++;return send({message:'Stale setup revision',errors:['Stale setup revision']},409);}
          assert.equal(body.revision,data.revision);
          for(const change of body.changes){data.assignments=data.assignments.filter(a=>a.classSubjectRequirementId!==change.classSubjectRequirementId);if(change.members.length)data.assignments.push({...change,id:change.classSubjectRequirementId,members:change.members.map(m=>({...m,name:data.teachers.find(t=>t.id===m.teacherTimetableProfileId).name}))});}
          data.revision++;recompute(data);
        }return send(wrap(data));
      }
      if(path.endsWith('/settings'))return send(settings);return route.continue();
    });
    await page.goto('http://localhost:4200/intelligent-timetable/assignments?profileId='+profile);
    await expect(page.locator('.classroom-row')).toHaveCount(10);await page.screenshot({path:resolve(evidence,'ledger-'+width+'.png'),fullPage:true});
    await page.locator('[data-requirement-id="101"]').click();await expect(page.locator('.teacher-results')).toHaveAttribute('aria-busy','false');
    await page.locator('[data-teacher-id="1"] input').check();await page.getByRole('button',{name:'نتائج المعلمين التالية',exact:true}).click();
    await expect(page.locator('[data-teacher-id="11"]')).toBeVisible();await page.locator('[data-teacher-id="11"] input').check();
    await expect(page.locator('.selection-pane [role="alert"]')).toContainText('اختر التدريس المشترك');
    await page.getByLabel('تقسيم الحصص',{exact:true}).check();await page.locator('#periods-1').fill('3');await page.locator('#pairs-1').fill('1');await page.locator('#periods-11').fill('2');await page.locator('#pairs-11').fill('0');
    await expect(page.getByRole('button',{name:'تأكيد الخلية',exact:true})).toBeEnabled();await page.locator('.selection-pane').scrollIntoViewIfNeeded();
    await page.screenshot({path:resolve(evidence,'split-'+width+'.png')});await page.getByRole('button',{name:'تأكيد الخلية',exact:true}).click();
    await expect(page.locator('.draft-bar')).toBeVisible();await expect(page.locator('.changed-cell')).toHaveCount(1);
    conflict=true;await page.getByRole('button',{name:'حفظ التغييرات',exact:true}).click();await expect(page.locator('main>[role="alert"]')).toContainText('احتفظنا بمسودتك');await expect(page.locator('.draft-bar')).toBeVisible();
    page.once('dialog',dialog=>dialog.accept());await page.getByRole('button',{name:'تحميل النسخة الأحدث',exact:true}).click();await expect(page.locator('.draft-bar')).toHaveCount(0);
    await page.locator('[data-requirement-id="101"]').click();await expect(page.locator('.teacher-results')).toHaveAttribute('aria-busy','false');
    await page.locator('[data-teacher-id="1"] input').check();await page.locator('#picker-search').fill('network-failure');await page.getByRole('button',{name:'بحث',exact:true}).click();await expect(page.locator('.teacher-browser [role="alert"]')).toBeVisible();await expect(page.locator('#selection-heading')).toContainText('1 مختار');
    await page.locator('#picker-search').fill('');await page.getByRole('button',{name:'بحث',exact:true}).click();await expect(page.locator('.teacher-results')).toHaveAttribute('aria-busy','false');
    await page.getByRole('button',{name:'تأكيد الخلية',exact:true}).click();await page.getByRole('button',{name:'حفظ التغييرات',exact:true}).click();await expect(page.locator('.draft-bar')).toHaveCount(0);await expect(page.locator('main>.success')).toContainText('تم حفظ');
    await page.getByRole('button',{name:'المعلمون',exact:true}).click();await expect(page.locator('.teacher-summary')).toHaveCount(10);await page.screenshot({path:resolve(evidence,'teachers-'+width+'.png'),fullPage:true});
    await page.reload();await expect(page.locator('[data-requirement-id="101"] .assigned-name')).toHaveCount(1);
    assert.equal(requests.some(p=>p.page==='2'),true);assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),true);assert.deepEqual(errors,[]);
    results.push({kind:'synthetic API fixture',width,sections:24,teachers:110,pagingRetainsSelection:true,splitPairs:true,conflictRetainsDraft:true,failedSearchRetainsSelection:true,atomicSaveAndReload:true,noPageErrors:true,noPageOverflow:true});await context.close();
  }
  // Loading/error/empty and large-school rendering use synthetic responses only.
  const stateContext=await contextFor('manager',1440),statePage=await stateContext.newPage();
  let scenario='loading',release;
  const waiting=new Promise(resolve=>release=resolve),large=assignmentFixture(180,500,12);
  large.teachers[0].name='خالد محمد عبدالله السالم، معلم الرياضيات والتفكير الناقد وإثراء المهارات المدرسية';
  const stateErrors=[];statePage.on('pageerror',error=>stateErrors.push(error.message));
  await stateContext.route('**/api/v1/intelligent-timetable/settings**',async route=>{
    const path=new URL(route.request().url()).pathname;
    const send=(data,status=200)=>route.fulfill({status,contentType:'application/json',body:JSON.stringify(data)});
    if(path.endsWith('/assignments/teachers'))return send(wrap({items:large.teachers.slice(0,10),totalCount:500}));
    if(path.endsWith('/assignments')) {
      if(scenario==='loading')await waiting;
      if(scenario==='error')return send({message:'تعذر تحميل الإسنادات التجريبية'},503);
      return send(wrap(scenario==='large'?large:{...assignmentFixture(),requirements:[],assignments:[]}));
    }
    if(path.endsWith('/settings'))return send(settings);return route.continue();
  });
  await statePage.goto('http://localhost:4200/intelligent-timetable/assignments?profileId='+profile);
  await expect(statePage.locator('.loading-state')).toBeVisible();scenario='error';release();
  await expect(statePage.locator('main>[role="alert"]')).toBeVisible();scenario='empty';await statePage.getByRole('button',{name:'إعادة المحاولة',exact:true}).click();
  await expect(statePage.getByRole('link',{name:'إعداد مواد الفصول',exact:true})).toBeVisible();
  scenario='large';const started=performance.now();await statePage.reload();await expect(statePage.locator('.classroom-row')).toHaveCount(10);const renderMs=performance.now()-started;
  assert.ok(await statePage.locator('.assignment-cell').count()<=120);await statePage.locator('[data-requirement-id="101"]').focus();await statePage.keyboard.press('Enter');
  await expect(statePage.locator('.teacher-choice')).toHaveCount(10);await statePage.locator('[data-teacher-id="1"] input').check();
  await expect(statePage.locator('.selected-teacher')).toContainText(large.teachers[0].name);await statePage.getByRole('button',{name:'تأكيد الخلية',exact:true}).click();
  await expect(statePage.locator('.draft-bar')).toBeVisible();
  let asked=false;statePage.once('dialog',async dialog=>{asked=true;await dialog.dismiss();});await statePage.locator('.page-heading').getByRole('link',{name:'إعدادات الجدول',exact:true}).click();
  await expect(statePage).toHaveURL(/intelligent-timetable\/assignments/);assert.equal(asked,true);
  await statePage.getByRole('button',{name:'المعلمون',exact:true}).click();await expect(statePage.locator('.teacher-summary')).toHaveCount(10);
  assert.equal(await statePage.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),true);assert.deepEqual(stateErrors,[]);
  results.push({kind:'synthetic API fixture',states:['loading','error','retry','empty'],largeSchool:{classrooms:180,subjects:12,teachers:500,requirements:large.requirements.length,renderMs,visibleClassrooms:10,visibleTeacherResults:10,visibleSummaryTeachers:10},longNamePreserved:true,navigationDraftGuard:true,noPageErrors:true,noPageOverflow:true});await stateContext.close();
  writeFileSync(resolve(evidence,'browser-results.json'),JSON.stringify({at:new Date().toISOString(),results},null,2));console.log('PASS: real roles and fixture editing/paging/split/conflict/retry/save/reload at 390/768/1440, large-school bounded rendering and navigation guard.');
} finally{await browser.close();}
