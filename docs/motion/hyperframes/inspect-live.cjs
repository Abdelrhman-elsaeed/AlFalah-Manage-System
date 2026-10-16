const {chromium}=require('../../../frontend/node_modules/@playwright/test');
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});
 const page=await browser.newPage({viewport:{width:1600,height:1000}}),errors=[];page.on('pageerror',e=>{errors.push(e.message);console.error('PAGE',e.message)});page.on('console',m=>{if(m.type()==='error')console.error('CONSOLE',m.text())});
 page.on('response',r=>{if(r.status()>=400&&r.url().includes('/assets/motion/'))errors.push(`${r.status()} ${r.url()}`)});
 await page.route('**/api/**',r=>r.fulfill({status:200,contentType:'application/json',body:'{"isSuccess":true,"data":[],"errors":[]}'}));
 await page.goto('http://localhost:4200/',{waitUntil:'networkidle'});console.log('Homepage loaded');await page.locator('#system-story').scrollIntoViewIfNeeded();await page.waitForFunction(()=>document.getElementById('falah-section')?.falahStory);console.log('Native runtime mounted');
 const dir=path.join(__dirname,'reference/site-inspection');fs.mkdirSync(dir,{recursive:true});
 const result={errors,heights:[],loopCounts:[],cubeTextSafety:[],gatePaperSafety:[]};
 for(const width of [1914,1600,1280,1024,768,390]){
  await page.setViewportSize({width,height:1000});await page.evaluate(()=>new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r))));
  await page.evaluate(()=>document.getElementById('falah-section').falahStory.seek(7.4));
  const record=await page.evaluate(()=>({width:innerWidth,section:document.querySelector('#system-story .story-stage').getBoundingClientRect().height,vision:document.querySelector('#vision .vision').getBoundingClientRect().height,overflow:document.documentElement.scrollWidth>innerWidth}));result.heights.push(record);assert.ok(Math.abs(record.section-record.vision)<.02);assert.equal(record.overflow,false);
 }
 for(const width of [1600,390]){
  await page.setViewportSize({width,height:1000});
  for(const [id,time,faceIndex] of [['staff',5.4,0],['staff',7.4,1],['learning',18.4,0],['learning',20.8,1],['care',23.5,0],['care',25.5,1],['guardian',28.2,0],['guardian',29.9,1]]){
   await page.evaluate(t=>document.getElementById('falah-section').falahStory.seek(t),time);
   const safety=await page.evaluate(({id,faceIndex})=>{const face=document.querySelector(`#af-cube-${id} [data-face="${faceIndex}"]`),clip=face.closest('.af-cube-window').getBoundingClientRect(),node=[...face.childNodes].find(n=>n.nodeType===Node.TEXT_NODE&&n.textContent.trim()),range=document.createRange();range.selectNodeContents(node);const text=range.getBoundingClientRect();return{id,faceIndex,width:innerWidth,leftClearance:text.left-clip.left,rightClearance:clip.right-text.right}}, {id,faceIndex});result.cubeTextSafety.push(safety);assert.ok(safety.leftClearance>=3&&safety.rightClearance>=3,JSON.stringify(safety));
  }
  await page.evaluate(()=>document.getElementById('falah-section').falahStory.seek(34.8));
  const gate=await page.evaluate(()=>{const a=document.querySelector('.af-permit-paper').getBoundingClientRect(),b=document.querySelector('#af-canvas-gate').getBoundingClientRect();return{width:innerWidth,overlap:Math.max(0,Math.min(a.right,b.right)-Math.max(a.left,b.left))*Math.max(0,Math.min(a.bottom,b.bottom)-Math.max(a.top,b.top))}});result.gatePaperSafety.push(gate);assert.equal(gate.overlap,0);
  for(const time of [0,.85,1.45,2.8,7.4,26.7,27.05,27.4,29.9,34.8,46.999]){
   await page.evaluate(t=>document.getElementById('falah-section').falahStory.seek(t),time);await page.locator('#system-story').screenshot({path:path.join(dir,`${width}-at-${time}.png`)});
  }
 }
 for(let i=0;i<3;i++){
  await page.evaluate(()=>document.getElementById('falah-section').falahStory.seek(46.85,{resume:true}));await page.waitForFunction(()=>{const p=document.getElementById('falah-section').falahStory;return p.time<1&&p.playing});result.loopCounts.push(await page.evaluate(()=>document.getElementById('falah-section').falahStory.loopCount));
 }
 assert.ok(result.loopCounts[2]>=3);
 await page.evaluate(()=>{window.__priorStory=document.getElementById('falah-section').falahStory});
 await page.setViewportSize({width:1600,height:1000});
 await page.locator('app-landing-header .login').click();await page.waitForURL('**/auth/school-login');assert.equal(await page.evaluate(()=>window.__priorStory.disposed),true);result.disposedOnRouteExit=true;
 await page.goBack();await page.locator('#system-story').scrollIntoViewIfNeeded();await page.waitForFunction(()=>document.getElementById('falah-section')?.falahStory);result.remounted=await page.evaluate(()=>document.getElementById('falah-section').falahStory!==window.__priorStory);assert.equal(result.remounted,true);
 assert.deepEqual(errors,[]);fs.writeFileSync(path.join(dir,'verification.json'),JSON.stringify(result,null,2));console.log(JSON.stringify(result,null,2));await browser.close();
})().catch(e=>{console.error(e);process.exit(1)});
