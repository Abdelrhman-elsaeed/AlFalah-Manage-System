const {chromium}=require('../../../frontend/node_modules/@playwright/test');
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'}),page=await browser.newPage({viewport:{width:1600,height:1000}}),errors=[];
 page.on('pageerror',e=>{errors.push(e.message);console.error(e.message)});
 await page.route('**/api/**',r=>r.fulfill({status:200,contentType:'application/json',body:'{"isSuccess":true,"data":[],"errors":[]}'}));
 await page.goto('http://localhost:4200/',{waitUntil:'networkidle'});await page.waitForSelector('.achievement-film');await page.locator('#achievements').scrollIntoViewIfNeeded();await page.waitForFunction(()=>document.querySelector('.achievement-film').achievementFilm);
 const dir=path.join(__dirname,'reference/homepage-refinement');fs.mkdirSync(dir,{recursive:true});
 for(const width of [1600,1024,768,390]){
  await page.setViewportSize({width,height:1000});
  for(const time of [4,13,22,31,40,44.999]){
   await page.evaluate(t=>document.querySelector('.achievement-film').achievementFilm.seek(t),time);
   await page.locator('app-achievement-film').screenshot({path:path.join(dir,`achievements-${width}-${time}.png`)});
  }
  await page.locator('#organization').scrollIntoViewIfNeeded();await page.locator('#organization').screenshot({path:path.join(dir,`team-${width}.png`)});
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);
 }
 await page.setViewportSize({width:1600,height:1000});await page.locator('#organization .reveal-button').click();await page.locator('#organization .department-toggle').first().click();await page.waitForTimeout(800);await page.locator('#organization').screenshot({path:path.join(dir,'team-expanded.png')});assert.equal(await page.locator('#organization .avatar img,#organization .member-avatar img').count(),31);
 await page.locator('#achievements').scrollIntoViewIfNeeded();await page.evaluate(()=>document.querySelector('.achievement-film').achievementFilm.seek(31));await page.locator('.scene-medals .achievement-evidence').click();assert.equal(await page.locator('#achievements dialog').evaluate(d=>d.open),true);await page.locator('#achievements .close-button').click();await page.waitForTimeout(200);
 await page.locator('.achievement-record').click();assert.equal(await page.locator('#achievements dialog').evaluate(d=>d.open),true);await page.locator('#achievements .close-button').click();
 fs.writeFileSync(path.join(dir,'initial-verification.json'),JSON.stringify({errors,staffCount:31,dialogs:true},null,2));assert.deepEqual(errors,[]);await browser.close();console.log('Verified four widths, five scenes, full staff and both dialogs');
})().catch(e=>{console.error(e);process.exit(1)});


