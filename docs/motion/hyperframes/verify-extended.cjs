// Verify masks and Three.js in the actual HyperFrames preview, and web playback.
const {chromium}=require('../../../frontend/node_modules/@playwright/test');
const fs=require('node:fs');
const path=require('node:path');
(async()=>{
 const browser=await chromium.launch({headless:true});
 const page=await browser.newPage({viewport:{width:1440,height:1000}});
 page.setDefaultTimeout(15000);
 const errors=[];
 page.on('pageerror',e=>errors.push(e.message));
 page.on('response',r=>{if(r.status()>=400)errors.push(`${r.status()} ${r.url()}`)});
 const result={film:'harmony-extended',approval:'pending',desktop:{},mobile:{},errors};
 const response=await page.goto('http://127.0.0.1:8877/review.html',{waitUntil:'networkidle'});
 await page.evaluate(()=>document.fonts.ready);
 result.status=response.status();
 result.filmCount=await page.locator('video').count();
 result.autoplayCount=await page.locator('video[autoplay]').count();
 result.removedStudies=await page.locator('#pulse,#evidence').count()===0;
 result.desktop.overflow=await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth);
 await page.screenshot({path:path.join(__dirname,'reference/extended-review-desktop.png'),fullPage:true});
 await page.locator('#play-film').click();
 await page.waitForFunction(()=>document.querySelector('#film').currentTime>.1);
 console.log('Desktop playback verified');
 result.desktop.metadata=await page.locator('#film').evaluate(v=>({duration:v.duration,width:v.videoWidth,height:v.videoHeight,muted:v.muted,playsInline:v.playsInline,source:v.currentSrc,playing:!v.paused}));
 result.chapters=[];
 for(const time of [5.2,12.3,20.7,29,37.4]){
  await page.locator(`[data-time="${time}"]`).click();
  await page.waitForFunction(t=>Math.abs(document.querySelector('#film').currentTime-t)<1,time);
  await page.waitForFunction(()=>!document.querySelector('#film').seeking);
  await page.locator('#film').evaluate(v=>v.pause());
  result.chapters.push({target:time,actual:await page.locator('#film').evaluate(v=>v.currentTime)});
 }
 await page.locator('#film').evaluate(v=>{v.currentTime=34});
 await page.waitForFunction(()=>!document.querySelector('#film').seeking);
 await page.locator('#film').screenshot({path:path.join(__dirname,'reference/extended-web-playback.png')});
 // Check the fallback MP4, as well as the default desktop WebM.
 console.log('Chapter seeking verified');
 await page.locator('#film').evaluate(v=>{
  v.pause();v.querySelectorAll('source').forEach(source=>source.remove());v.src='renders/04-day-in-harmony-extended.mp4';v.load();
 });
 await page.waitForFunction(()=>document.querySelector('#film').readyState>=1);
 result.mp4Fallback=await page.locator('#film').evaluate(v=>({duration:v.duration,width:v.videoWidth,height:v.videoHeight}));
 await page.setViewportSize({width:390,height:844});
 await page.goto('http://127.0.0.1:8877/review.html',{waitUntil:'networkidle'});
 result.mobile.overflow=await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth);
 await page.screenshot({path:path.join(__dirname,'reference/extended-review-mobile.png'),fullPage:true});
 await page.locator('#play-film').click();
 await page.waitForFunction(()=>document.querySelector('#film').currentTime>.1);
 result.mobile.metadata=await page.locator('#film').evaluate(v=>({duration:v.duration,width:v.videoWidth,height:v.videoHeight,source:v.currentSrc,playing:!v.paused}));
 await page.locator('#film').evaluate(v=>v.pause());
 await page.emulateMedia({reducedMotion:'reduce'});
 await page.reload({waitUntil:'networkidle'});
 result.reducedMotion={autoplayCount:await page.locator('video[autoplay]').count(),paused:await page.locator('#film').evaluate(v=>v.paused)};
 result.passed=result.status===200&&result.filmCount===1&&result.autoplayCount===0&&result.removedStudies&&!result.desktop.overflow&&!result.mobile.overflow&&result.desktop.metadata.duration===50&&result.mobile.metadata.width===640&&result.reducedMotion.paused&&errors.length===0;
 fs.writeFileSync(path.join(__dirname,'reference/extended-browser-check.json'),JSON.stringify(result,null,2));
 console.log(JSON.stringify(result,null,2));
 await browser.close();
 if(!result.passed)process.exitCode=1;
})().catch(error=>{console.error(error);process.exit(1)});
