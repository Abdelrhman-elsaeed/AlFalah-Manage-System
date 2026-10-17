import assert from 'node:assert/strict';
import { chromium, expect } from '@playwright/test';
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { createHash, randomUUID } from 'node:crypto';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
const root=fileURLToPath(new URL('../../',import.meta.url));
const state=JSON.parse(readFileSync(resolve(root,'.audit/standby-phase3/isolated-private.json'),'utf8'));
assert.match(state.connection,/(?:Database|Initial Catalog)=AlFalahRegisterTests_[a-f0-9]{32}(?:;|$)/i);
const output=resolve(root,'docs/specs/intelligent-timetable/verification/standby-phase3');mkdirSync(output,{recursive:true});
const date=state.pastDate,prefix='timetable-coverage/',register=prefix+'register',results=[];
const hash=b=>createHash('sha256').update(b).digest('hex').toUpperCase();
async function api(path,{user='manager',...opts}={}){return fetch('http://localhost:5270/api/v1/'+path,{...opts,headers:{Authorization:'Bearer '+state.sessions[user].token,'Content-Type':'application/json',Origin:'http://localhost:4200',...opts.headers}});}
async function data(path,opts){const r=await api(path,opts),body=await r.json();assert.equal(r.status,200,JSON.stringify(body));assert.equal(body.isSuccess,true,JSON.stringify(body));return body.data;}
const post=(body,user='manager')=>({method:'POST',body:JSON.stringify(body),user});
const context=()=>data(register+'?date='+date);
const issue=async()=>{const c=await context();return data(register,post({date,sourceRevision:c.sourceRevision}));};
let c=await context();assert.ok(c.isComplete);const old=(await data(register+'?date='+date+'&page='+Math.ceil((c.history.totalCount-1)/10))).history.items.find(i=>i.revision===2);assert.ok(old);
const oldBytes=Buffer.from(await(await api(register+'/'+old.id+'/content')).arrayBuffer());assert.equal(hash(oldBytes),old.sha256);
assert.equal((await api(register+'?date='+date,{user:'teacher2'})).status,403);
assert.equal((await api(register+'/'+old.id+'/content',{user:'teacher2'})).status,403);
assert.ok(state.sessions.foreign,'Prepare both isolated schools before running the live workflow');assert.equal((await api(register+'/'+old.id+'/content',{user:'foreign'})).status,404);
const board=await data(prefix+'board?date='+date);const need=board.needs.find(n=>n.submittedAt);assert.ok(need);
let detail=await data(prefix+'needs/'+need.id+'/reopen',post({requestId:randomUUID(),revision:need.revision,substitutionId:need.substitutionId,reason:'Synthetic retained register correction'}));
assert.equal((await issue()).isComplete,false);
async function draft(text){detail=await data(prefix+'tasks/'+need.id+'/draft',{method:'PUT',user:'teacher2',body:JSON.stringify({revision:detail.need.revision,substitutionId:need.substitutionId,narrative:text,notes:'Synthetic isolated register verification'})});}
await draft('مسودة تصحيح غير مُرسلة');const stale=(await context()).sourceRevision;
await draft('مسودة أحدث غير مُرسلة');assert.equal((await api(register,post({date,sourceRevision:stale}))).status,409);
while((await context()).history.totalCount<12){await draft('تصحيح تجريبي محفوظ '+randomUUID());await issue();}
await draft('نُفذت مراجعة وتدريبات تفاعلية مع الطلاب — اختبار معزول.');
detail=await data(prefix+'tasks/'+need.id+'/submit',post({requestId:randomUUID(),revision:detail.need.revision,substitutionId:need.substitutionId},'teacher2'));
const latest=await issue();assert.equal(latest.isComplete,true);
assert.equal(hash(Buffer.from(await(await api(register+'/'+old.id+'/content')).arrayBuffer())),old.sha256);
const transfer=await api(register+'/'+latest.id+'/content',{headers:{Accept:'application/vnd.alfalah.transfer'}});
assert.equal(transfer.headers.get('content-type'),'application/vnd.alfalah.transfer');assert.equal(transfer.headers.get('x-alfalah-content-type'),'application/pdf');assert.equal(hash(Buffer.from(await transfer.arrayBuffer())),latest.sha256);
const browser=await chromium.launch({headless:true});
async function open(width){
 const ctx=await browser.newContext({viewport:{width,height:1000},reducedMotion:'reduce',acceptDownloads:true});
 await ctx.addInitScript(s=>{sessionStorage.setItem('alfalah_access_token',s.token);sessionStorage.setItem('alfalah_user',JSON.stringify(s.user));},state.sessions.manager);
 await ctx.route('**/api/**',async route=>{const req=route.request(),url=new URL(req.url());const response=await api(url.pathname.replace('/api/v1/','')+url.search,{method:req.method(),body:req.postData()||undefined,headers:{Accept:req.headers().accept||'application/json'}});const headers=Object.fromEntries(response.headers);delete headers['content-length'];delete headers['content-encoding'];return route.fulfill({status:response.status,headers,body:Buffer.from(await response.arrayBuffer())});});
 const page=await ctx.newPage();page.setDefaultTimeout(45000);page.on('pageerror',e=>results.push({runtimeError:e.message}));await page.goto('http://localhost:4200/intelligent-timetable/substitutions',{waitUntil:'domcontentloaded',timeout:60000});return{ctx,page};
}
try {
 for(const width of [390,768,1440,1920]) {
  console.log('Register viewport',width);const{ctx,page}=await open(width);await expect(page.locator('.coverage-ledger')).toBeVisible({timeout:60000});
  await page.locator('.coverage-ledger>.toolbar input[type=date]').fill(date);await page.locator('.coverage-ledger>.toolbar input[type=date]').dispatchEvent('change');await expect(page.locator('.ledger-table tbody tr')).toHaveCount(2);
  await page.locator('.register-trigger').click();await expect(page.locator('.revision-row')).toHaveCount(10);await expect(page.locator('.register-context .complete')).toBeVisible();
  // Viewing history is read-only. The older complete issue remains byte-identical after correction.
  await page.locator('.register-history footer button').last().click();await expect(page.locator('.register-history footer>span')).toHaveText('2');
  const oldButton=page.locator('.revision-row').filter({has:page.locator('strong',{hasText:'النسخة 2'})});await expect(oldButton).toHaveCount(1);await oldButton.click();
  await expect(page.locator('.pdf-pages canvas').first()).toBeVisible({timeout:45000});await expect(page.locator('.preview-message')).toHaveCount(0,{timeout:45000});
  console.log('Older PDF rendered',width);
  const download=page.waitForEvent('download',{timeout:20000});await page.locator('.register-footer .primary').click();const file=await download;console.log('Download started',width);assert.equal(hash(readFileSync(await file.path())),old.sha256);console.log('Download verified',width);
  // Issuing while on page two must restore page one with the actual latest ten entries.
  await page.locator('.issue-button').click();await expect(page.locator('.register-history footer>span')).toHaveText('1');await expect(page.locator('.revision-row').first()).toHaveClass(/active/);
  await expect(page.locator('.pdf-pages canvas').first()).toBeVisible({timeout:45000});await expect(page.locator('.preview-message')).toHaveCount(0,{timeout:45000});await expect(page.locator('.register-message.error')).toHaveCount(0);
  const dimensions=await page.evaluate(()=>{const dialog=document.querySelector('.p-dialog'),workspace=document.querySelector('.register-workspace'),stage=document.querySelector('.pdf-pages'),canvas=stage.querySelector('canvas');return{pageOverflow:document.documentElement.scrollWidth>innerWidth+1,dialog:dialog.getBoundingClientRect().width,workspace:workspace.scrollWidth,workspaceWidth:workspace.clientWidth,canvas:canvas.getBoundingClientRect().width,stage:stage.clientWidth};});
  assert.equal(dimensions.pageOverflow,false);assert.ok(dimensions.workspace<=dimensions.workspaceWidth+1,JSON.stringify(dimensions));assert.ok(dimensions.canvas>=dimensions.stage*.85&&dimensions.canvas<=dimensions.stage,JSON.stringify(dimensions));
  await expect(page.locator('.p-toast-message')).toHaveCount(0,{timeout:20000});await page.waitForTimeout(350);await page.screenshot({path:resolve(output,'register-'+width+'.png'),fullPage:true});
  const finalDownload=page.waitForEvent('download',{timeout:20000});await page.locator('.register-footer .primary').click();const current=await finalDownload;assert.equal(hash(readFileSync(await current.path())),latest.sha256);
  results.push({width,preview:true,fitWidth:true,retainedDownloadHash:true,pagination:true,issuedFromSecondPage:true,...dimensions});await ctx.close();
 }
 assert.equal(results.some(r=>r.runtimeError),false,JSON.stringify(results.filter(r=>r.runtimeError)));
 writeFileSync(resolve(output,'browser-results.json'),JSON.stringify({at:new Date().toISOString(),isolated:true,providerWrites:false,checks:['manager register, teacher 403','foreign school 404','actual signed correction and new revision','uncached stale source 409','old retained bytes unchanged','13+ issue history pagination','transfer MIME restored','preview/download SHA256 equality','390/768/1440/1920 layout'],results},null,2));console.log('PASS: live register HTTP and browser checks.');
} finally {await browser.close();}
