const {chromium}=require('../../../frontend/node_modules/@playwright/test');
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});
 const page=await browser.newPage({viewport:{width:1600,height:1000},deviceScaleFactor:1});const errors=[];
 page.on('pageerror',e=>{errors.push(e.message);console.error(e.message)});page.on('response',r=>{if(r.status()>=400&&!r.url().endsWith('favicon.ico'))errors.push(`${r.status()} ${r.url()}`)});
 const dir=path.join(__dirname,'reference/section-inspection');fs.mkdirSync(dir,{recursive:true});
 await page.goto('http://127.0.0.1:8877/section-review.html',{waitUntil:'networkidle'});await page.waitForFunction(()=>window.__afPlayer);
 const results={errors,heights:[],reverseSeeks:[],rasterTolerance:{maxChannelDelta:2,maxChangedFraction:.0001}};
 for(const width of [1914,1600,1440,1280,1024,768,390]){
  await page.setViewportSize({width,height:1000});await page.evaluate(()=>new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve))));
  await page.evaluate(()=>window.__afPlayer.seek(45));
  const r=await page.evaluate(()=>({width:innerWidth,section:document.getElementById('af-live-section').getBoundingClientRect().height,vision:document.querySelector('#af-vision-reference .vision').getBoundingClientRect().height,overflow:document.documentElement.scrollWidth>innerWidth}));results.heights.push(r);assert.ok(Math.abs(r.section-r.vision)<.02);assert.equal(r.overflow,false);
 }
 const times=[.7,1.9,3,5.4,7.4,10.4,14.7,18.4,20.8,23.5,25.5,28.2,29.9,32.6,35.4,37.4,40,44.5];
 for(const width of [1600,390]){
  await page.setViewportSize({width,height:1000});
  for(const t of times){await page.evaluate(t=>window.__afPlayer.seek(t),t);await page.locator('#af-live-section').screenshot({path:path.join(dir,`${width}-at-${t}.png`)});console.log('Captured',width,t)}
  await page.screenshot({path:path.join(dir,`${width}-page.png`),fullPage:true});
 }
 await page.setViewportSize({width:1600,height:1000});
 for(const target of [1.1,4.25,6.8,17.15,24.1,29,36.15]){
  await page.evaluate(t=>window.__afPlayer.seek(t),target);const a=await page.locator('#af-live-section').screenshot();await page.evaluate(()=>window.__afPlayer.seek(45));await page.evaluate(t=>window.__afPlayer.seek(t),target);const b=await page.locator('#af-live-section').screenshot();
  const record={target,exact:a.equals(b),stable:a.equals(b)};
  if(!record.exact){
   const pa=path.join(dir,`mismatch-${target}-a.png`),pb=path.join(dir,`mismatch-${target}-b.png`);fs.writeFileSync(pa,a);fs.writeFileSync(pb,b);
   const py="from PIL import Image,ImageChops; import sys,json; a=Image.open(sys.argv[1]).convert('RGB'); b=Image.open(sys.argv[2]).convert('RGB'); d=ImageChops.difference(a,b); pixels=list(d.getdata()); print(json.dumps({'maxChannelDelta':max(max(p) for p in pixels),'changedFraction':sum(max(p)>0 for p in pixels)/len(pixels)}))";
   Object.assign(record,JSON.parse(require('node:child_process').execFileSync('python',['-c',py,pa,pb],{encoding:'utf8'})));record.stable=record.maxChannelDelta<=2&&record.changedFraction<=.0001;
  }
  results.reverseSeeks.push(record);
 }
 results.videoElements=await page.locator('video').count();assert.equal(results.videoElements,0);
 fs.writeFileSync(path.join(dir,'inspection.json'),JSON.stringify(results,null,2));console.log(JSON.stringify(results,null,2));await browser.close();assert.deepEqual(errors,[]);assert.ok(results.reverseSeeks.every(r=>r.stable));
})().catch(e=>{console.error(e);process.exit(1)});
