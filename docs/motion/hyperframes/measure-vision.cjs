const {chromium}=require('../../../frontend/node_modules/@playwright/test');
const fs=require('node:fs'),path=require('node:path');
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});
 const page=await browser.newPage({viewport:{width:1914,height:1000}});
 await page.route('**/api/**',r=>r.fulfill({status:200,contentType:'application/json',body:JSON.stringify({isSuccess:true,data:[],message:'',errors:[]})}));
 await page.goto('http://localhost:4200/',{waitUntil:'networkidle'});await page.evaluate(()=>document.fonts.ready);
 await page.locator('[data-reveal]').evaluateAll(es=>es.forEach(e=>e.classList.add('is-revealed')));
 const records=[];
 for(const width of [1914,1600,1440,1280,1024,768,390]){
  await page.setViewportSize({width,height:1000});
  records.push(await page.locator('app-vision-strip').evaluate(e=>{const r=e.getBoundingClientRect(),inner=e.querySelector('.vision-inner'),cs=getComputedStyle(inner);return{width:innerWidth,height:r.height,innerWidth:inner.clientWidth,padding:cs.paddingBlock,font:getComputedStyle(e).fontFamily,lineHeight:getComputedStyle(e).lineHeight,fontSize:getComputedStyle(e).fontSize}}));
 }
 await page.setViewportSize({width:1600,height:1000});
 const snapshot=await page.locator('app-vision-strip .vision').evaluate(e=>({html:e.outerHTML,vars:Object.fromEntries(['--brand-950','--brand-100','--gold-700','--bg-surface','--text-muted','--font-app'].map(k=>[k,getComputedStyle(e).getPropertyValue(k).trim()])),height:e.getBoundingClientRect().height}));
 fs.writeFileSync(path.join(__dirname,'reference/section-vision-measurements.json'),JSON.stringify({source:'Unchanged localhost:4200 homepage; synthetic intercepted APIs',records,snapshot},null,2));
 console.log(JSON.stringify(records,null,2));await browser.close();
})().catch(e=>{console.error(e);process.exit(1)});
