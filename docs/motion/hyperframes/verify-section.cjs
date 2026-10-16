const {chromium}=require('../../../frontend/node_modules/@playwright/test');
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const executablePath='C:/Program Files/Google/Chrome/Application/chrome.exe';
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath});
 const context=await browser.newContext({viewport:{width:1280,height:720}}),page=await context.newPage(),errors=[];
 page.on('pageerror',e=>errors.push(e.message));const result={format:'Native responsive section',scope:'isolated composition behavior',homepageIntegrated:true,errors};
 const response=await page.goto('http://127.0.0.1:8877/review.html',{waitUntil:'networkidle'});assert.equal(response.status(),200);await page.waitForFunction(()=>window.__afPlayer);
 await page.waitForFunction(()=>window.__afPlayer.playing&&window.__afPlayer.time>.1);
 result.startsWhenVisible=true;assert.equal(await page.locator('video,iframe').count(),0);
 result.aspectRatio=await page.locator('#af-live-section').evaluate(e=>getComputedStyle(e).aspectRatio);assert.equal(result.aspectRatio,'auto');
 await page.locator('#af-play').click();const stopped=await page.evaluate(()=>window.__afPlayer.time);await page.waitForTimeout(180);assert.equal(await page.evaluate(()=>window.__afPlayer.time),stopped);result.manualPause=true;
 await page.locator('#af-play').click();await page.waitForFunction(t=>window.__afPlayer.time>t+.1,stopped);
 await page.locator('#af-review-details').scrollIntoViewIfNeeded();await page.waitForFunction(()=>!window.__afPlayer.visible&&!window.__afPlayer.playing);result.pausesOutsideViewport=true;
 await page.locator('#af-live-section').scrollIntoViewIfNeeded();await page.waitForFunction(()=>window.__afPlayer.visible&&window.__afPlayer.playing);result.resumesOnReturn=true;
 await page.locator('[data-scene="guardian"]').click();await page.waitForFunction(()=>window.__afPlayer.time>=27.3&&window.__afPlayer.time<28.5);await page.locator('#af-play').click();result.sceneNavigation=true;
 await page.evaluate(()=>window.__afPlayer.seek(6.7));result.cubeTransform=await page.locator('#af-cube-staff').evaluate(e=>getComputedStyle(e).transform);assert.ok(result.cubeTransform.startsWith('matrix3d('));
 await page.evaluate(()=>window.__afPlayer.seek(1.1));result.quillMoves=await page.locator('#af-picture-intro .af-quill').evaluate(e=>e.style.transform.includes('translate('));assert.equal(result.quillMoves,true);
 await page.setViewportSize({width:390,height:844});await page.evaluate(()=>window.__afPlayer.seek(29.9));
 result.mobile=await page.evaluate(()=>{const section=document.getElementById('af-live-section'),vision=document.querySelector('#af-vision-reference .vision'),cube=document.querySelector('#af-cube-guardian .af-cube-face[data-face="1"]'),phone=document.querySelector('.af-guardian-phone'),support=document.querySelector('#af-picture-guardian .af-support');return{sectionHeight:section.getBoundingClientRect().height,visionHeight:vision.getBoundingClientRect().height,overflow:document.documentElement.scrollWidth>innerWidth,cubeFont:getComputedStyle(cube).fontSize,phoneTop:phone.getBoundingClientRect().top,supportBottom:support.getBoundingClientRect().bottom}});
 assert.equal(result.mobile.sectionHeight,result.mobile.visionHeight);assert.equal(result.mobile.overflow,false);assert.ok(result.mobile.phoneTop>result.mobile.supportBottom+4);
 await page.locator('#af-live-section').screenshot({path:path.join(__dirname,'reference/section-inspection/mobile-final.png')});
 // Reduced motion starts with the complete benefit summary and no Three.js download.
 const reduced=await browser.newContext({viewport:{width:1440,height:900},reducedMotion:'reduce'}),rp=await reduced.newPage(),threeRequests=[];
 rp.on('request',r=>{if(r.url().includes('/three/')||r.url().includes('section-world.mjs'))threeRequests.push(r.url())});
 await rp.goto('http://127.0.0.1:8877/section-review.html',{waitUntil:'networkidle'});await rp.waitForFunction(()=>window.__afPlayer);
 result.reducedMotion=await rp.evaluate(()=>({time:window.__afPlayer.time,playing:window.__afPlayer.playing,worldLoaded:window.__afPlayer.worldLoaded}));assert.equal(result.reducedMotion.time,45.5);assert.equal(result.reducedMotion.playing,false);assert.equal(result.reducedMotion.worldLoaded,false);assert.deepEqual(threeRequests,[]);
 await rp.locator('#af-live-section').screenshot({path:path.join(__dirname,'reference/section-inspection/reduced-motion.png')});await reduced.close();
 assert.deepEqual(errors,[]);await browser.close();
 // A browser with WebGL unavailable still has a readable vector narrative.
 const fallbackBrowser=await chromium.launch({headless:true,executablePath,args:['--disable-webgl']});
 const fp=await fallbackBrowser.newPage({viewport:{width:1280,height:720}});await fp.goto('http://127.0.0.1:8877/section-review.html',{waitUntil:'networkidle'});await fp.waitForFunction(()=>window.__afPlayer);await fp.evaluate(()=>window.__afPlayer.seek(14.7));
 result.vectorFallback=await fp.locator('#falah-section').evaluate(e=>e.classList.contains('af-vector-mode')&&!e.classList.contains('af-has-world'));assert.equal(result.vectorFallback,true);
 await fallbackBrowser.close();fs.writeFileSync(path.join(__dirname,'reference/section-browser-verification.json'),JSON.stringify(result,null,2));console.log(JSON.stringify(result,null,2));
})().catch(e=>{console.error(e);process.exit(1)});
