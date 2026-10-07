import { test, expect, Page } from '@playwright/test';
const revision={approvalRevision:2,status:'RetryScheduled',approvalSource:'Manual',approvedAtUtc:'2026-10-05T08:00:00Z',attempts:2,lastAttemptAtUtc:'2026-10-05T08:10:00Z',nextAttemptAtUtc:'2026-10-05T08:20:00Z',errorCode:'ArchiveUnavailable',isCurrent:false,canRetry:true,versions:[]};
const old={...revision,approvalRevision:1,status:'Completed',errorCode:null,completedAtUtc:'2026-10-04T10:00:00Z',canRetry:false,versions:[{versionId:8,versionNumber:1,uploadedAtUtc:'2026-10-04T10:00:00Z',availability:'Available',size:135000}]};
async function mock(page:Page,state='rows',operations={workerEnabled:false,externalWritesEnabled:false,ready:false}) {
  const user={userId:'ui-test',username:'ui-test',fullName:'مستخدم الاختبار',activeSchoolId:1,activeSchoolName:'مدرسة الاختبار',preferredLanguage:'ar',roles:['Secretary'],permissions:['Storage.ViewArchive','Visit.View']};
  await page.addInitScript(user=>{const token=btoa(JSON.stringify({sub:'ui-test',exp:2000000000}));sessionStorage.setItem('alfalah_access_token',`e30.${token}.mock`);sessionStorage.setItem('alfalah_user',JSON.stringify(user));},user);
  const calls:{url:string;body:any}[]=[];let retried=false;
  await page.route('**/api/**',async route=>{
    const request=route.request();const url=new URL(request.url());calls.push({url:request.url(),body:request.postDataJSON()});let data:any={};
    if(url.pathname.endsWith('/auth/me'))data=user;
    else if(url.pathname.endsWith('/auth/schools'))data=[];
    else if(url.pathname.endsWith('/storage/access'))data={canManage:true,canReviewEvidence:true};
    else if(url.pathname.endsWith('/storage/context'))data={schoolId:1,schoolName:'مدرسة',canManage:true,connectionState:'Connected'};
    else if(url.pathname.endsWith('/visits/operations-status'))data=operations;
    else if(url.pathname.endsWith('/visits/teachers'))data=[{userId:'teacher',name:'معلم الاختبار'}];
    else if(url.pathname.endsWith('/archive/retry')){retried=true;data={visitId:4,revisions:[]};}
    else if(url.pathname.endsWith('/content'))return route.fulfill({status:200,contentType:'application/pdf',body:'%PDF-1.7\n%%EOF'});
    else if(url.pathname.endsWith('/storage/visits')){
      if(state==='error')return route.fulfill({status:503,contentType:'application/json',body:JSON.stringify({isSuccess:false,message:'unavailable'})});
      data={items:state==='empty'?[]:[{visitId:4,instructorName:'معلم الاختبار',approvalRevision:2,isApproved:true,canManage:true,revisions:[{...revision,status:state==='missing'?'MissingFromDrive':retried?'Pending':'RetryScheduled'},old]}],total:state==='empty'?0:60,page:Number(url.searchParams.get('page')||1),pageSize:25};
    }
    return route.fulfill({status:200,contentType:'application/json',body:JSON.stringify({isSuccess:true,data})});
  });return calls;
}
test('delegated archive shows RTL pagination, filters, safe errors, retry and history',async({page})=>{
  const calls=await mock(page);await page.goto('/school-manager/storage/visits');await expect(page.locator('h1')).toHaveText('أرشيف الزيارات');await expect(page.locator('.archive-page')).toHaveAttribute('dir','rtl');await expect(page.locator('.visit-row')).toContainText('محاولة مجدولة');await page.locator('summary').click();await expect(page.getByRole('button',{name:'تنزيل النسخة المؤرشفة'})).toBeVisible();
  await page.getByRole('button',{name:'إعادة المحاولة',exact:true}).click();await expect(page.locator('.visit-row')).toContainText('بانتظار الأرشفة');expect(calls.find(c=>c.url.includes('/archive/retry'))?.body.approvalRevision).toBe(2);
  await page.locator('.p-paginator-next').click();await expect.poll(()=>calls.some(c=>c.url.includes('page=2'))).toBeTruthy();
  expect(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1)).toBeFalsy();await page.screenshot({path:`test-results/storage/s5-${test.info().project.name}.png`,fullPage:true});
});
test('missing report recreation needs a reason and preserves the approval revision',async({page})=>{
  const calls=await mock(page,'missing');await page.goto('/school-manager/storage/visits');await page.getByRole('button',{name:'استعادة التقرير المفقود',exact:true}).click();const dialog=page.getByRole('dialog');await expect(dialog.getByRole('button',{name:'استعادة التقرير المفقود'})).toBeDisabled();await dialog.locator('textarea').fill('استعادة النسخة الأصلية');await dialog.getByRole('button',{name:'استعادة التقرير المفقود'}).click();await expect.poll(()=>calls.some(c=>c.body?.recreateMissing===true && c.body.approvalRevision===2)).toBeTruthy();
});
test('revoked archive permission clears reports and teacher filters',async({page})=>{
  await mock(page);await page.goto('/school-manager/storage/visits');await expect(page.locator('.visit-row')).toBeVisible();await page.route('**/api/v1/storage/visits?**',route=>route.fulfill({status:403,contentType:'application/json',body:JSON.stringify({isSuccess:false,message:'revoked'})}));await page.getByRole('button',{name:'تحديث',exact:true}).click();await expect(page.locator('.visit-row')).toHaveCount(0);await expect(page.locator('[role=alert]')).toContainText('سُحبت صلاحية');await expect(page.locator('.filters')).toHaveCount(0);
});
for(const state of ['empty','error'])test(`archive ${state} state is explicit`,async({page})=>{await mock(page,state);await page.goto('/school-manager/storage/visits');await expect(page.locator(state==='empty'?'.empty':'[role=alert]')).toBeVisible();await expect(page.locator('.visit-row')).toHaveCount(0);});

test('archive deep link keeps filters on return and explains stopped worker on desktop and phone @mobile',async({page})=>{
  await mock(page);
  await page.goto('/school-manager/storage/visits?page=2&status=RetryScheduled');
  await expect(page.getByText('العامل مغلق')).toBeVisible();
  await expect(page.getByText('الكتابة الخارجية مغلقة')).toBeVisible();
  await page.locator('.visit-heading a').click();
  await expect(page).toHaveURL(/\/visits\?visitId=4/);
  await page.goBack();
  await expect(page).toHaveURL(/storage\/visits\?page=2&status=RetryScheduled/);
  await expect(page.locator('.visit-row')).toBeVisible();
  expect(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1)).toBeFalsy();
});
