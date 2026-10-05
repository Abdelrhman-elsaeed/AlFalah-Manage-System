import { test, expect, Page } from '@playwright/test';
async function session(page:Page,role='SchoolManager',disabled=false) {
  const user={userId:'ui',username:'ui',fullName:'اختبار',activeSchoolId:18,preferredLanguage:'ar',roles:[role],permissions:['Storage.ViewSchool','Storage.ManageSchool']};
  await page.addInitScript(user=>{sessionStorage.setItem('alfalah_access_token',`e30.${btoa(JSON.stringify({sub:'ui',exp:2000000000}))}.mock`);sessionStorage.setItem('alfalah_user',JSON.stringify(user));},user);
  let batch:any={id:7,academicYearId:1,templateVersion:1,sourceName:'source.json',sourceVersion:'1',sourceSHA256:'A'.repeat(64),status:'Preview',rows:1,matched:1,missing:0,conflicts:0,referenceOnly:1,importedFiles:0,digest:'digest-1',rowVersion:'v1'};
  const calls:string[]=[];
  await page.route('**/api/**',route=>{
    const path=new URL(route.request().url()).pathname;calls.push(path);let data:any={};let status=200;
    if(path.endsWith('/auth/me'))data=user;
    else if(path.endsWith('/auth/schools'))data=[];
    else if(path.endsWith('/storage/context'))data={schoolId:18,schoolName:'مدرسة التجربة',connectionState:disabled?'Disabled':'Connected',canManage:true,rootFolderId:7};
    else if(path.endsWith('/storage/academic-years'))data=[{id:1,nameAr:'سنة اختبار'}];
    else if(path.endsWith('/storage/templates'))data=[{version:1,name:'قالب S4'}];
    else if(path.endsWith('/storage/evaluation-members'))data=[{userId:'real-user',name:'مسؤول حقيقي'}];
    else if(path.endsWith('/storage/requirement-catalog'))data=[{id:8,displayName:'بند S4'}];
    else if(path.endsWith('/imports'))data=[];
    else if(path.endsWith('/preview'))data=batch;
    else if(path.endsWith('/rows'))data={items:[{id:9,source:{key:'source-1',name:'original.pdf',referencePath:'C:\\old\\original.pdf',sourceStatus:'completed'},classification:'Matched',status:'ReferenceOnly',reason:'مرجع بلا أصل',requirementId:8,suggestions:[],rowVersion:'r1'}],total:1};
    else if(path.endsWith('/review')){batch={...batch,reviewedDigest:'digest-1',rowVersion:'v2'};data=batch;}
    else if(path.endsWith('/commit')){batch={...batch,committedAtUtc:'2026-10-05T10:00:00Z',status:'Committed',rowVersion:'v3'};data=batch;}
    else if(path.endsWith('/bytes')){status=403;data=null;}
    else if(path.endsWith('/imports/7'))data=batch;
    if(disabled && path.includes('/imports'))status=404;
    return route.fulfill({status,contentType:'application/json',body:JSON.stringify({isSuccess:status===200,data,message:status===403?'تم سحب التفويض':'الاستيراد غير مفعّل'})});
  });return calls;
}
test('disabled workspace remains discoverable to the manager with Drive setup link',async({page})=>{
  await session(page,'SchoolManager',true);await page.goto('/school-manager/storage');
  await expect(page.locator('.empty a')).toHaveText('إعدادات Google Drive');
  await expect(page.locator('a[href="/school-manager/storage"]').first()).toBeAttached();
  await expect(page.locator('input[type=file]')).toHaveCount(0);
});
for(const role of ['SchoolManager','Secretary'])test(`${role} reviews and commits reference metadata with RTL and no fabricated file`,async({page})=>{
  const calls=await session(page,role);await page.goto('/school-manager/storage/imports');await expect(page.locator('section.import-page')).toHaveAttribute('dir','rtl');
  await page.locator('p-dropdown[name=year]').click();await page.getByRole('option',{name:'سنة اختبار'}).click();
  await page.locator('input[accept=".json,.csv"]').setInputFiles({name:'source.json',mimeType:'application/json',buffer:Buffer.from('[{"key":"source-1","name":"original.pdf"}]')});
  await page.getByRole('button',{name:'معاينة المصدر'}).click();await expect(page.getByRole('heading',{name:'original.pdf'})).toBeVisible();
  await expect(page.getByRole('button',{name:'اعتماد الاستيراد',exact:true})).toBeDisabled();
  await page.getByPlaceholder('وثّق قرارك').fill('راجعت المصدر والمطابقة والاستثناءات');await page.getByRole('checkbox').check();
  await page.getByRole('button',{name:'تسجيل المراجعة'}).click();await page.getByRole('checkbox').check();await page.getByRole('button',{name:'اعتماد الاستيراد',exact:true}).click();
  await expect(page.getByRole('status')).toContainText('تم اعتماد المراجع');await expect(page.locator('.summary')).toContainText('أصول مرفوعة 0');
  expect(calls.filter(x=>x.endsWith('/bytes'))).toHaveLength(0);expect(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1)).toBeFalsy();
  await page.screenshot({path:`test-results/storage/import-${role}-${test.info().project.name}.png`,fullPage:true});
});
test('revocation during byte import clears reference and member data',async({page})=>{
  await session(page,'Secretary');await page.goto('/school-manager/storage/imports?batch=7');await expect(page.getByRole('heading',{name:'original.pdf'})).toBeVisible();
  await page.getByPlaceholder('وثّق قرارك').fill('فحص');await page.getByRole('checkbox').check();await page.getByRole('button',{name:'تسجيل المراجعة'}).click();await page.getByRole('checkbox').check();await page.getByRole('button',{name:'اعتماد الاستيراد',exact:true}).click();
  await expect(page.locator('section.import-page')).toHaveAttribute('aria-busy','false');
  await expect(page.locator('.row input[type=file]')).toBeEnabled();
  await page.locator('.row input[type=file]').setInputFiles({name:'original.pdf',mimeType:'application/pdf',buffer:Buffer.from('%PDF-1.7 test')});
  await expect(page.getByRole('alert')).toHaveText('تم سحب التفويض');await expect(page.locator('.row')).toHaveCount(0);
});
