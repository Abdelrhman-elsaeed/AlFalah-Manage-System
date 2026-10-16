const {chromium}=require('../../../frontend/node_modules/@playwright/test');
const fs=require('node:fs');const path=require('node:path');
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});
 const page=await browser.newPage({viewport:{width:1600,height:900},deviceScaleFactor:1});const errors=[];
 page.on('pageerror',e=>{errors.push(e.message);console.error('PAGE',e.message)});
 page.on('console',m=>{if(m.type()==='error')console.error('CONSOLE',m.text())});
 await page.goto('http://127.0.0.1:8877/alfalah-story/index.html');
 await page.waitForFunction(()=>window.__timelines?.main&&window.__renderStoryAt);
 await page.evaluate(()=>window.__hf.buildReady.story);
 const dir=path.join(__dirname,'reference/story-inspection');fs.mkdirSync(dir,{recursive:true});
 const times=process.argv.slice(2).map(Number);if(!times.length)times.push(2,7.5,12.8,15.3,19.8,22.7,25.1,28.5,33.6,36.4,40.4,43,47.5,50.7,53.5,56.8,61.7);
 async function seek(t){await page.evaluate(time=>{window.__timelines.main.time(time,false);document.querySelectorAll('.clip').forEach(c=>c.style.visibility=time>=+c.dataset.start&&time<+c.dataset.start+(+c.dataset.duration)?'visible':'hidden');window.dispatchEvent(new CustomEvent('hf-seek',{detail:{time}}))},t)}
 for(const t of times){await seek(t);await page.screenshot({path:path.join(dir,`at-${t}.png`)});console.log('Captured',t)}
 const crypto=require('node:crypto'),reverseSeeks=[];
 for(const target of [5,12.8,23.55,38.1,52.25]){
  await seek(target);const a=await page.screenshot();await seek(61.7);await seek(target);const b=await page.screenshot();
  reverseSeeks.push({target,equal:a.equals(b),a:crypto.createHash('sha256').update(a).digest('hex'),b:crypto.createHash('sha256').update(b).digest('hex')});
 }
 const result={errors,seekDeterministic:reverseSeeks.every(r=>r.equal),reverseSeeks};
 fs.writeFileSync(path.join(dir,'inspection.json'),JSON.stringify(result,null,2));console.log(result);await browser.close();if(errors.length||!result.seekDeterministic)process.exitCode=1;
})().catch(e=>{console.error(e);process.exit(1)});
