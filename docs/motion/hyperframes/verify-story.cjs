const {chromium}=require('../../../frontend/node_modules/@playwright/test');
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});
 const page=await browser.newPage({viewport:{width:1440,height:1000}});page.setDefaultTimeout(20000);
 const errors=[];page.on('pageerror',e=>errors.push(e.message));page.on('response',r=>{if(r.status()>=400&&!r.url().endsWith('favicon.ico'))errors.push(`${r.status()} ${r.url()}`)});
 const result={film:'alfalah-story',approval:'pending',desktop:{},mobile:{},errors};
 const response=await page.goto('http://127.0.0.1:8877/review.html',{waitUntil:'networkidle'});await page.evaluate(()=>document.fonts.ready);
 result.status=response.status();assert.equal(result.status,200);
 assert.equal(await page.locator('video').count(),1);assert.equal(await page.locator('video[autoplay]').count(),0);assert.equal(await page.locator('#pulse,#evidence').count(),0);
 result.desktop.overflow=await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth);assert.equal(result.desktop.overflow,false);
 assert.equal(await page.locator('#film').evaluate(v=>v.paused&&v.currentTime===0&&v.preload==='none'&&!v.loop),true);
 await page.screenshot({path:path.join(__dirname,'reference/story-review-desktop.png'),fullPage:true});
 await page.locator('#play-film').click();await page.waitForFunction(()=>document.querySelector('#film').currentTime>.1);
 result.desktop.metadata=await page.locator('#film').evaluate(v=>({duration:v.duration,width:v.videoWidth,height:v.videoHeight,muted:v.muted,playsInline:v.playsInline,source:v.currentSrc,playing:!v.paused}));assert.equal(result.desktop.metadata.width,1280);assert.equal(result.desktop.metadata.duration,64);
 result.chapters=[];
 for(const time of [10.55,37.6,4.5,51.8,30.5,58.8]){
  await page.locator(`[data-time="${time}"]`).click();await page.waitForFunction(t=>Math.abs(document.querySelector('#film').currentTime-t)<.9&&!document.querySelector('#film').seeking,time);await page.locator('#film').evaluate(v=>v.pause());result.chapters.push({target:time,actual:await page.locator('#film').evaluate(v=>v.currentTime)});
 }
 await page.locator('#film').evaluate(v=>{v.currentTime=15.3});await page.waitForFunction(()=>!document.querySelector('#film').seeking);await page.locator('#film').screenshot({path:path.join(__dirname,'reference/story-web-playback.png')});
 await page.locator('#film').evaluate(v=>{v.pause();v.querySelectorAll('source').forEach(s=>s.remove());v.src='renders/05-alfalah-school-day.mp4';v.load()});await page.waitForFunction(()=>document.querySelector('#film').readyState>=1);
 result.mp4Fallback=await page.locator('#film').evaluate(v=>({duration:v.duration,width:v.videoWidth,height:v.videoHeight}));assert.equal(result.mp4Fallback.duration,64);
 const range=await page.request.get('http://127.0.0.1:8877/renders/05-alfalah-school-day.mp4',{headers:{Range:'bytes=0-255'}});assert.equal(range.status(),206);assert.equal((await range.body()).length,256);result.byteRanges=true;
 await page.setViewportSize({width:390,height:844});await page.goto('http://127.0.0.1:8877/review.html',{waitUntil:'networkidle'});await page.evaluate(()=>document.fonts.ready);
 result.mobile.overflow=await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth);assert.equal(result.mobile.overflow,false);
 await page.screenshot({path:path.join(__dirname,'reference/story-review-mobile.png'),fullPage:true});await page.locator('#play-film').click();await page.waitForFunction(()=>document.querySelector('#film').currentTime>.1);
 result.mobile.metadata=await page.locator('#film').evaluate(v=>({duration:v.duration,width:v.videoWidth,height:v.videoHeight,source:v.currentSrc,playing:!v.paused}));assert.equal(result.mobile.metadata.width,640);await page.locator('#film').evaluate(v=>v.pause());
 await page.emulateMedia({reducedMotion:'reduce'});await page.reload({waitUntil:'networkidle'});result.reducedMotionPaused=await page.locator('#film').evaluate(v=>v.paused);assert.equal(result.reducedMotionPaused,true);
 assert.deepEqual(errors,[]);fs.writeFileSync(path.join(__dirname,'reference/story-browser-verification.json'),JSON.stringify(result,null,2));console.log(JSON.stringify(result,null,2));await browser.close();
})().catch(e=>{console.error(e);process.exit(1)});
