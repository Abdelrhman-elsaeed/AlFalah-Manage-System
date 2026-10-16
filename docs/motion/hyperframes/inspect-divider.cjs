const {chromium}=require('../../../frontend/node_modules/@playwright/test');
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'}),page=await browser.newPage({viewport:{width:1600,height:1000}}),errors=[];
 page.on('pageerror',e=>errors.push(e.message));
 await page.route('**/api/**',r=>r.fulfill({status:200,contentType:'application/json',body:'{"isSuccess":true,"data":[],"errors":[]}'}));
 await page.goto('http://localhost:4200/',{waitUntil:'networkidle'});await page.waitForSelector('#system-story-title');await page.waitForFunction(()=>!document.querySelector('.page-preloader'));
 const results=[];
 for(const width of [1914,1600,1280,1100,1024,900,800,769,768,390]){
  await page.setViewportSize({width,height:1000});await page.evaluate(()=>new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r))));
  const result=await page.evaluate(()=>{const rect=s=>document.querySelector(s).getBoundingClientRect(),intro=rect('.story-intro'),stage=rect('.story-stage'),nav=rect('#landing-navigation'),hero=rect('#home');return{width:innerWidth,introHeight:intro.height,separator:stage.top-hero.bottom,stageHeight:stage.height,visionHeight:rect('#vision .vision').height,navLeft:nav.left,navRight:nav.right,overflow:document.documentElement.scrollWidth>innerWidth,title:document.querySelector('#system-story-title').textContent.trim(),independent:document.getElementById('system-story').nextElementSibling.id==='vision'}});
  results.push(result);console.log(JSON.stringify(result));assert.equal(result.overflow,false);assert.ok(result.separator>120);assert.equal(result.independent,true);assert.equal(result.title,'نظام الفلاح');assert.ok(Math.abs(result.stageHeight-result.visionHeight)<.02);
 }
 await page.setViewportSize({width:1600,height:1000});await page.locator('app-landing-header a[href="#system-story"]').click();await page.waitForFunction(()=>Math.abs(document.getElementById('system-story').getBoundingClientRect().top-108)<8);await page.waitForFunction(()=>document.getElementById('falah-section')?.falahStory);await page.evaluate(()=>document.getElementById('falah-section').falahStory.seek(2.8));
 const dir=path.join(__dirname,'reference/site-inspection');await page.screenshot({path:path.join(dir,'section-divider-desktop.png')});
 assert.equal(await page.locator('app-landing-header a[href="#system-story"]').getAttribute('aria-current'),'location');
 await page.locator('app-landing-header a[href="#vision"]').click();await page.waitForFunction(()=>Math.abs(document.getElementById('vision').getBoundingClientRect().top-108)<8);assert.equal(await page.locator('app-landing-header a[href="#vision"]').getAttribute('aria-current'),'location');
 await page.setViewportSize({width:390,height:844});await page.locator('.menu-toggle').click();await page.locator('app-landing-header a[href="#system-story"]').click();await page.waitForFunction(()=>Math.abs(document.getElementById('system-story').getBoundingClientRect().top-96)<8);await page.evaluate(()=>document.getElementById('falah-section').falahStory.seek(2.8));await page.screenshot({path:path.join(dir,'section-divider-mobile.png')});
 assert.equal(await page.locator('.menu-toggle').getAttribute('aria-expanded'),'false');assert.deepEqual(errors,[]);
 fs.writeFileSync(path.join(dir,'section-divider-verification.json'),JSON.stringify({results,errors,navigation:true},null,2));console.log(JSON.stringify({results,errors,navigation:true},null,2));await browser.close();
})().catch(e=>{console.error(e);process.exit(1)});
