import { test, expect, Page } from '@playwright/test';

const file = {storedFileId:31,folderId:7,displayName:'شاهد المدرسة.pdf',size:100,mimeType:'application/pdf',uploadedAt:'2026-10-04T08:00:00Z',state:'Managed',isProtected:true,rowVersion:'AAAAAAAAAAE='};
async function session(page:Page,own:boolean,canManage=true,canReviewEvidence=!own) {
  const user={userId:'actor',username:'actor',fullName:'مستخدم الاختبار',activeSchoolId:1,activeSchoolName:'مدرسة الاختبار',preferredLanguage:'ar',roles:[own?'Instructor':'Secretary'],permissions:['Storage.ViewSchool','Storage.ManageSchool','Storage.ViewOwn','Storage.ManageOwn']};
  await page.addInitScript(user=>{const payload=btoa(JSON.stringify({sub:'actor',exp:2000000000}));sessionStorage.setItem('alfalah_access_token',`e30.${payload}.mock`);sessionStorage.setItem('alfalah_user',JSON.stringify(user));},user);
  const catalog=[{id:2,academicYearId:1,code:'TASK',displayName:'خطة التدريس',originalTaskId:1,importance:'Normal',fulfillmentPolicy:'AnyApprovedLink',minimumApprovedLinks:1,rowVersion:'AAAAAAAAAAE='},
    {id:3,academicYearId:1,code:'STD-2.1',displayName:'بناء خبرات التعلم',domainCode:'2',standardCode:'2.1',importance:'Normal',fulfillmentPolicy:'AnyApprovedLink',minimumApprovedLinks:1,rowVersion:'AAAAAAAAAAE='}];
  let links=[{id:9,storedFileId:31,requirementId:2,academicYearId:1,teacherId:5,versionId:4,fileName:file.displayName,requirementName:'خطة التدريس',teacherName:'المعلم أ',status:own?'Rejected':'PendingReview',availability:'Available',rowVersion:'AAAAAAAAAAE=',decisions:own?[{id:1,versionId:4,decision:'Rejected',reviewerName:'المراجع',note:'أضف شرحًا واضحًا',reviewedAtUtc:file.uploadedAt}]:[]},
    {id:10,storedFileId:31,requirementId:3,academicYearId:1,teacherId:5,versionId:4,fileName:file.displayName,requirementName:'بناء خبرات التعلم',teacherName:'المعلم أ',status:'PendingReview',availability:'Available',rowVersion:'AAAAAAAAAAI=',decisions:[]}];
  let changes:any[]=[];let uploads=0;const keys:string[]=[];const bodies:any[]=[];const requests:URL[]=[];
  await page.route('**/api/**',async route=>{
    const r=route.request(),u=new URL(r.url()),p=u.pathname;let data:any={};requests.push(u);
    if(p.endsWith('/auth/me'))data=user;
    else if(p.endsWith('/auth/schools'))data=[];
    else if(p.endsWith('/storage/access'))data={canManage,canReviewEvidence};
    else if(p.endsWith('/storage/context'))data={schoolId:1,schoolName:'مدرسة الاختبار',academicYearId:1,academicYearName:'السنة الدراسية',canManage,canReviewEvidence,isTeacher:own,connectionState:'Connected',rootFolderId:7};
    else if(p.endsWith('/storage/academic-years'))data=[{id:1,nameAr:'السنة الدراسية'}];
    else if(p.endsWith('/storage/evidence-teachers'))data=[{id:5,displayName:'المعلم أ'}];
    else if(p.endsWith('/storage/folders'))data={items:[],total:0,page:1,pageSize:25};
    else if(p.endsWith('/storage/files') || p.endsWith('/storage/me/files'))data={items:[file],total:1,page:1,pageSize:25};
    else if(p.endsWith('/storage/files/31'))data={file,versions:[{versionId:4,versionNumber:1,size:100,mimeType:'application/pdf',uploadedAt:file.uploadedAt,availability:'Available'}]};
    else if(p.endsWith('/storage/files/31/content') || p.endsWith('/versions/4/content'))return route.fulfill({contentType:'application/pdf',body:'%PDF-1.7\nTest'});
    else if(p.endsWith('/storage/requirement-catalog'))data=catalog;
    else if(p.endsWith('/storage/requirements/2') && r.method()==='PATCH'){bodies.push(r.postDataJSON());data=catalog[0];}
    else if(p.endsWith('/storage/evidence-counts'))data={files:1,links:links.length,approvedLinks:links.filter(x=>x.status==='Approved').length,fulfilledRequirements:links.filter(x=>x.status==='Approved').length,requirements:2};
    else if(p.endsWith('/storage/review-queue'))data={items:links,total:links.length,page:1,pageSize:25};
    else if(p.endsWith('/storage/change-queue'))data={items:[],total:0,page:1,pageSize:25};
    else if(p.endsWith('/links') && r.method()==='GET')data=links;
    else if(p.endsWith('/files/31/links') && r.method()==='POST'){bodies.push(r.postDataJSON());links.push({...links[1],id:11,status:'Draft',rowVersion:'AAAAAAAAAAU='});data=links.at(-1);}
    else if(p.endsWith('/submit')){const id=Number(p.split('/').at(-2));links=links.map(x=>x.id===id?{...x,status:'Resubmitted',rowVersion:'AAAAAAAAAAM='}:x);data=links.find(x=>x.id===id);bodies.push(r.postDataJSON());}
    else if(p.endsWith('/review')){const id=Number(p.split('/').at(-2)),body=r.postDataJSON();links=links.map(x=>x.id===id?{...x,status:body.decision===3?'Approved':'Rejected',rowVersion:'AAAAAAAAAAQ=',decisions:[...x.decisions,{id:2,versionId:4,decision:body.decision===3?'Approved':'Rejected',reviewerName:'المراجع',note:body.note,reviewedAtUtc:file.uploadedAt}]}:x);data=links.find(x=>x.id===id);bodies.push(body);}
    else if(p.endsWith('/change-requests') && r.method()==='POST'){bodies.push(r.postDataJSON());changes=[{id:3,storedFileId:31,originalVersionId:4,kind:'Replace',status:'Pending',reason:r.postDataJSON().reason,requestedByUserId:'actor',rowVersion:'AAAAAAAAAAI=',decisions:[]}];data=changes[0];}
    else if(p.endsWith('/change-requests'))data=changes;
    else if(p.endsWith('/change-requests/3/version')){keys.push(r.headers()['idempotency-key']);uploads++;if(uploads===1)return route.fulfill({status:503,contentType:'application/json',body:JSON.stringify({isSuccess:false,message:'انقطع الاتصال بعد الرفع'})});changes[0].candidateVersionId=8;data={operationId:7,storedFileId:31,versionId:8,status:'Completed',displayName:'نسخة.pdf',size:8,mimeType:'application/pdf',uploadedAt:file.uploadedAt};}
    return route.fulfill({contentType:'application/json',body:JSON.stringify({isSuccess:true,data})});
  });
  return {keys,bodies,requests};
}

test('teacher sees independent badges, rejection reason, submits one link and retains previous decision',async({page})=>{
  const state=await session(page,true);await page.goto('/instructor/my-files');await page.locator('.file-name button').click();
  await page.getByRole('dialog',{name:'تفاصيل الملف'}).getByRole('tab',{name:'روابط الشواهد'}).click();
  const panel=page.locator('.p-dialog app-storage-evidence');await expect(panel.getByText('ملف واحد مرتبط بـ 2 متطلبات')).toBeVisible();
  const yearBox=await panel.locator('.filters>label').first().boundingBox();
  const requirementBox=await panel.locator('.filters>label').nth(1).boundingBox();
  expect(yearBox && requirementBox && (yearBox.x + yearBox.width <= requirementBox.x || requirementBox.x + requirementBox.width <= yearBox.x || yearBox.y + yearBox.height <= requirementBox.y)).toBeTruthy();
  const rejected=panel.locator('article.link').filter({hasText:'خطة التدريس'});await rejected.locator('summary').click();await expect(rejected.getByText('أضف شرحًا واضحًا').first()).toBeVisible();
  await rejected.getByRole('button',{name:'تقديم للمراجعة'}).click();await expect(panel.locator('.badge[data-status=Resubmitted]')).toBeVisible();
  await expect(panel.locator('.badge[data-status=PendingReview]')).toHaveCount(1);expect(state.bodies.at(-1)).toEqual({rowVersion:'AAAAAAAAAAE='});
  await panel.locator('article.link').first().scrollIntoViewIfNeeded();
  const body=page.locator('.storage-detail-dialog .p-dialog-content');
  const overflow=await body.evaluate(el=>({scrollWidth:el.scrollWidth,clientWidth:el.clientWidth,offenders:[...el.querySelectorAll('*')].filter(node=>node.getBoundingClientRect().left<el.getBoundingClientRect().left-2 || node.getBoundingClientRect().right>el.getBoundingClientRect().right+2).slice(0,8).map(node=>({tag:node.tagName,className:node.className}))}));
  expect(overflow.scrollWidth<=overflow.clientWidth+1,JSON.stringify(overflow)).toBeTruthy();
  await page.screenshot({path:`test-results/storage/evidence-teacher-${test.info().project.name}.png`,fullPage:true,animations:'disabled'});
});
test('delegate approves one link and rejects another with required reason and authorized preview @mobile',async({page})=>{
  const state=await session(page,false);await page.goto('/school-manager/storage?view=review');const queue=page.locator('.storage-page app-storage-evidence');
  await expect(queue.locator('article.link')).toHaveCount(2);
  await page.screenshot({path:`test-results/storage/evidence-queue-${test.info().project.name}.png`,fullPage:true,animations:'disabled'});
  const first=queue.locator('article.link').filter({hasText:'خطة التدريس'});const detail=queue.locator('.review-detail');
  await first.click();await detail.getByRole('button',{name:'اعتماد الرابط'}).click();await expect(queue.locator('.queue-items .badge[data-status=Approved]')).toHaveCount(1);await expect(queue.locator('.queue-items .badge[data-status=PendingReview]')).toHaveCount(1);
  const second=queue.locator('article.link').filter({hasText:'بناء خبرات التعلم'});await second.click();await expect(detail.getByRole('button',{name:'رفض مع السبب'})).toBeDisabled();
  await detail.locator('textarea').fill('الشرح غير كاف');await detail.getByRole('button',{name:'رفض مع السبب'}).click();await expect(queue.locator('.queue-items .badge[data-status=Rejected]')).toHaveCount(1);
  expect(state.bodies.map(x=>x.decision)).toEqual([3,4]);await first.click();await detail.getByRole('button',{name:'تفاصيل الملف ومعاينته'}).click();await page.getByRole('dialog',{name:'تفاصيل الملف'}).getByRole('tab',{name:'المعلومات الأساسية'}).click();await page.locator('.inspector-main .dialog-actions').getByRole('button',{name:'معاينة',exact:true}).click();await expect(page.locator('iframe')).toHaveAttribute('src',/^blob:/);
  await expect(page.locator('iframe')).toBeVisible();
  await page.screenshot({path:`test-results/storage/evidence-manager-${test.info().project.name}.png`,fullPage:true,animations:'disabled'});
});
test('reviewer without library management can decide links but cannot create one',async({page})=>{
  await session(page,false,false,true);
  await page.goto('/school-manager/storage/evidence');
  const queue=page.locator('.storage-page app-storage-evidence');
  await expect(queue.getByRole('button',{name:'اعتماد الرابط'})).toBeVisible();
  if (test.info().project.name === 'mobile') await expect(page.getByRole('navigation',{name:'أقسام مساحة الملفات'}).getByRole('link',{name:'الشواهد'})).toBeVisible();
  else await expect(page.locator('.shell-sidebar__category').filter({hasText:'مساحة الملفات'}).getByRole('link',{name:'الشواهد'})).toBeVisible();
  await expect(page.locator('.shell-sidebar__category').filter({hasText:'مساحة الملفات'}).getByRole('link',{name:'الإدارة'})).toHaveCount(0);
  await queue.getByRole('button',{name:'تفاصيل الملف ومعاينته'}).click();
  const filePanel=page.locator('.p-dialog app-storage-evidence');
  await expect(filePanel.getByRole('button',{name:'اعتماد الرابط'})).toHaveCount(2);
  await expect(filePanel.getByRole('button',{name:'ربط بمتطلب'})).toHaveCount(0);
});
test('approved file change uses a request and retries candidate upload with the same key',async({page})=>{
  const state=await session(page,true);await page.goto('/instructor/my-files');await page.locator('.file-name button').click();await page.getByRole('dialog',{name:'تفاصيل الملف'}).getByRole('tab',{name:'تعديل الملف'}).click();const panel=page.locator('#file-modify-panel app-storage-evidence');
  await panel.locator('textarea[name=reason]').fill('نسخة محدثة');await panel.getByRole('button',{name:'تقديم طلب التغيير'}).click();
  await expect(panel.getByText('ينتظر القرار',{exact:false}).first()).toBeVisible();expect(state.bodies[0].replaceBeforeReview).toBe(false);
  const candidate=panel.locator('article.link').filter({hasText:'نسخة محدثة'});await candidate.locator('input[type=file]').setInputFiles({name:'نسخة.pdf',mimeType:'application/pdf',buffer:Buffer.from('%PDF-1.7\nNew')});await candidate.getByRole('button',{name:'رفع / إعادة المحاولة'}).click();
  await expect(panel.locator('[role=alert]')).toContainText('تعذر إكمال الطلب. حاول مرة أخرى.');await candidate.getByRole('button',{name:'رفع / إعادة المحاولة'}).click();
  await expect(panel.locator('article.link').filter({hasText:'نسخة محدثة'}).getByRole('button',{name:'النسخة المرشحة',exact:true})).toBeVisible();expect(state.keys[1]).toBe(state.keys[0]);
});
test('revoked reviewer access clears queue and file preview data',async({page})=>{
  await session(page,false);await page.goto('/school-manager/storage?view=review');const queue=page.locator('.storage-page app-storage-evidence');await expect(queue.locator('article.link')).toHaveCount(2);
  await page.route('**/api/v1/storage/review-queue?**',route=>route.fulfill({status:403,contentType:'application/json',body:JSON.stringify({isSuccess:false,message:'تم سحب التفويض'})}));
  await queue.getByRole('button',{name:'تصفية النتائج'}).click();
  await queue.locator('.filters>label').filter({hasText:'الحالة'}).locator('.p-dropdown-trigger').click();
  await page.locator('.p-dropdown-item').filter({hasText:'معتمد'}).click();
  await expect(queue.locator('article.link')).toHaveCount(0);
  await expect(page.locator('.storage-page [role=alert]')).toBeVisible();
  await expect(page.locator('iframe')).toHaveCount(0);
});

test('teacher links the existing asset without another upload and sees Draft',async({page})=>{
  const state=await session(page,true);await page.goto('/instructor/my-files');await page.locator('.file-name button').click();await page.getByRole('dialog',{name:'تفاصيل الملف'}).getByRole('tab',{name:'روابط الشواهد'}).click();const panel=page.locator('#file-evidence-panel app-storage-evidence');
  await panel.locator('.filters>label').filter({hasText:'المتطلب'}).locator('.p-dropdown-trigger').click();
  await page.locator('.p-dropdown-item').filter({hasText:'بناء خبرات التعلم'}).click();
  await panel.getByRole('button',{name:'ربط الملف',exact:true}).click();await expect(panel.locator('.badge[data-status=Draft]')).toHaveCount(1);
  expect(state.bodies.at(-1)).toEqual({requirementId:3,academicYearId:1});expect(state.keys).toEqual([]);
  expect(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1)).toBeFalsy();
});

test('manager configures a catalog requirement with policy and rowversion',async({page})=>{
  const state=await session(page,false);await page.goto('/school-manager/storage?view=review');const queue=page.locator('.storage-page app-storage-evidence');
  await queue.getByRole('button',{name:'تصفية النتائج'}).click();
  await queue.locator('.filters>label').filter({hasText:'المتطلب'}).locator('.p-dropdown-trigger').click();
  await page.locator('.p-dropdown-item').filter({hasText:'خطة التدريس'}).click();
  await queue.getByRole('button',{name:'إعداد المتطلب'}).click();const form=queue.locator('form').filter({hasText:'سياسة الاستيفاء'});
  await form.locator('label').filter({hasText:'سياسة الاستيفاء'}).locator('.p-dropdown-trigger').click();
  await page.locator('.p-dropdown-item').filter({hasText:'الحد الأدنى للروابط المعتمدة'}).click();await form.locator('input[type=number]').fill('2');
  await form.getByRole('button',{name:'حفظ',exact:true}).click();await expect(form).toHaveCount(0);
  expect(state.bodies.at(-1)).toMatchObject({fulfillmentPolicy:2,minimumApprovedLinks:2,rowVersion:'AAAAAAAAAAE='});
  expect(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1)).toBeFalsy();
});

test('a requirement link opens the review workspace with its filter selected',async({page})=>{
  await session(page,false);
  await page.goto('/school-manager/storage?requirement=2&academicYearId=1');
  await expect(page.locator('.review-panel article.link')).toHaveCount(2);
  if (test.info().project.name === 'mobile') await expect(page.getByRole('navigation',{name:'أقسام مساحة الملفات'}).getByRole('link',{name:'الشواهد'})).toHaveAttribute('aria-current','page');
  else await expect(page.locator('.shell-sidebar__category').filter({hasText:'مساحة الملفات'}).getByRole('link',{name:'الشواهد'})).toHaveAttribute('aria-current','page');
  await expect(page.locator('.review-panel article.link')).toHaveCount(2);
});

test('review selection and decided history survive URL reload without loading library pages',async({page})=>{
  const state=await session(page,false);
  await page.goto('/school-manager/storage/evidence');
  await expect(page.locator('.queue-items article.link')).toHaveCount(2);
  await page.locator('.queue-items article.link').last().click();
  await expect(page).toHaveURL(/evidenceLink=\d+/);
  await page.reload();
  await expect(page.locator('.queue-items article.link.selected-link')).toHaveCount(1);
  await page.getByRole('tab',{name:'سجل القرارات'}).click();
  await expect(page).toHaveURL(/evidenceTab=history/);
  await expect(page.getByRole('tab',{name:'بانتظار القرار',exact:true})).toBeVisible();
  expect(state.requests.some(x=>x.pathname.endsWith('/storage/review-queue') && x.searchParams.get('decided')==='true')).toBeTruthy();
  expect(state.requests.some(x=>x.pathname.endsWith('/storage/folders') || x.pathname.endsWith('/storage/files'))).toBeFalsy();
});

test('review filters stay compact until opened and a filtered deep link restores them',async({page})=>{
  await session(page,false);
  await page.goto('/school-manager/storage/evidence');
  const queue=page.locator('.review-panel app-storage-evidence');
  await expect(queue.locator('.filters')).toHaveCount(0);
  await queue.getByRole('button',{name:'تصفية النتائج'}).click();
  await expect(queue.locator('.filters')).toBeVisible();
  await queue.locator('.filters>label').filter({hasText:'الحالة'}).locator('.p-dropdown-trigger').click();
  await page.locator('.p-dropdown-item').filter({hasText:'معتمد'}).click();
  await expect(page).toHaveURL(/evidenceStatus=3/);
  await page.reload();
  await expect(queue.locator('.filters')).toBeVisible();
});

test('a failed change queue keeps review decisions available and recovers independently',async({page})=>{
  await session(page,false);
  let failOnce=true;
  await page.route('**/api/v1/storage/change-queue?**',route=>{
    if(!failOnce)return route.fallback();
    failOnce=false;
    return route.fulfill({status:500,contentType:'application/json',body:JSON.stringify({isSuccess:false,message:'تعذر تحميل طلبات التغيير'})});
  });
  await page.goto('/school-manager/storage?view=review');
  const panel=page.locator('.review-panel');
  await expect(panel.locator('article.link')).toHaveCount(2);
  await expect(panel.getByRole('alert')).toContainText('تعذر تحميل طلبات التغيير');
  await expect(panel.getByRole('button',{name:'تهيئة الكتالوج'})).toHaveCount(0);
  await panel.getByRole('button',{name:'تصفية النتائج'}).click();
  await expect(panel.locator('.filters')).toBeVisible();
  await panel.getByRole('button',{name:'إعادة محاولة طلبات التغيير'}).click();
  await expect(panel.locator('.filters')).toBeVisible();
  await expect(panel.getByRole('alert')).toHaveCount(0);
});

test('a failed review queue is shown as an error instead of an empty catalog',async({page})=>{
  await session(page,false);
  await page.route('**/api/v1/storage/review-queue?**',route=>route.fulfill({status:500,contentType:'application/json',body:JSON.stringify({isSuccess:false,message:'Internal SQL detail'})}));
  await page.goto('/school-manager/storage?view=review');
  const panel=page.locator('.review-panel');
  await expect(panel.getByRole('alert')).toContainText('تعذر تحميل الشواهد');
  await expect(panel.getByText('Internal SQL detail')).toHaveCount(0);
  await expect(panel.locator('.empty')).toHaveCount(0);
  await expect(panel.getByRole('button',{name:'تهيئة الكتالوج'})).toHaveCount(0);
});

test('a failed evidence count leaves review decisions available',async({page})=>{
  await session(page,false);
  await page.route('**/api/v1/storage/evidence-counts?**',route=>route.fulfill({status:500,contentType:'application/json',body:JSON.stringify({isSuccess:false,message:'Internal SQL detail'})}));
  await page.goto('/school-manager/storage?view=review');
  const panel=page.locator('.review-panel');
  await expect(panel.locator('article.link')).toHaveCount(2);
  await expect(panel.getByRole('alert')).toContainText('تعذر تحميل عدادات الشواهد');
  await expect(panel.locator('.counts')).toHaveCount(0);
});
