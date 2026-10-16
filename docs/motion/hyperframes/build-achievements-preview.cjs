// Capture the native, approved homepage content into a seekable HyperFrames review.
const fs=require('node:fs'),path=require('node:path');
const {chromium}=require('../../../frontend/node_modules/@playwright/test');
const REPO=path.resolve(__dirname,'../../..'),OUT=path.join(__dirname,'alfalah-achievements');
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe'});
 try{
  const page=await browser.newPage({viewport:{width:1600,height:1000},reducedMotion:'reduce'});
  await page.route('**/api/**',r=>r.fulfill({status:200,contentType:'application/json',body:'{"isSuccess":true,"data":[],"errors":[]}'}));
  await page.goto('http://localhost:4200/',{waitUntil:'networkidle'});
  await page.waitForSelector('.achievement-film');
  const capture=await page.locator('.achievement-film').evaluate(root=>{
   const clone=root.cloneNode(true);clone.querySelectorAll('.achievement-loop-bridge').forEach(e=>e.remove());
   clone.querySelectorAll('[style]').forEach(e=>e.removeAttribute('style'));
   clone.querySelectorAll('.achievement-visual').forEach(e=>e.setAttribute('data-layout-allow-overlap',''));
   const images=[...clone.querySelectorAll('img')].map(e=>e.getAttribute('src'));
   return{html:clone.outerHTML,images};
  });
  fs.mkdirSync(OUT,{recursive:true});
  for(const image of new Set(capture.images)){
   if(!image)continue;const relative=new URL(image,'http://localhost:4200/').pathname.slice(1),source=path.join(REPO,'frontend/src',relative),target=path.join(OUT,relative);
   fs.mkdirSync(path.dirname(target),{recursive:true});fs.copyFileSync(source,target);
  }
  const shared=path.join(REPO,'frontend/src/assets/motion/falah/assets');
  for(const font of ['Tajawal-Regular.ttf','Tajawal-Medium.ttf','Tajawal-Bold.ttf','Tajawal-ExtraBold.ttf','gsap.min.js'])fs.copyFileSync(path.join(shared,font),path.join(OUT,'assets',font));
  fs.copyFileSync(path.join(REPO,'frontend/src/assets/motion/achievements/timeline.mjs'),path.join(OUT,'timeline.mjs'));
  const css=fs.readFileSync(path.join(REPO,'frontend/src/app/features/landing/components/achievement-film/achievement-film.component.css'),'utf8').replace(':host{display:block}','');
  const fonts=[['Regular',400],['Medium',500],['Bold',700],['ExtraBold',800]].map(([name,weight])=>`@font-face{font-family:FalahMotion;src:url('assets/Tajawal-${name}.ttf');font-weight:${weight};font-display:block}`).join('\n');
  fs.writeFileSync(path.join(OUT,'index.html'),`<!doctype html><html lang="ar"><head><meta charset="utf-8"><title>منجزات الفلاح</title><style>${fonts}\nhtml,body{margin:0;width:100%;height:100%;font-family:FalahMotion,Arial,sans-serif}#alfalah-achievements{width:100%;height:100%}.achievement-film{font-family:FalahMotion,Arial,sans-serif}app-falah-quill{display:block;width:150px;height:28px;color:#946800}app-falah-quill svg{display:block;width:100%;height:100%;overflow:visible}.quill-trail{stroke:currentColor;stroke-width:1.6;stroke-linecap:round}.quill-edge{fill:#a57b1414;stroke:currentColor;stroke-width:2;stroke-linecap:round;stroke-linejoin:round}${css}</style><script src="assets/gsap.min.js"></script></head><body dir="rtl"><div id="alfalah-achievements" data-composition-id="alfalah-achievements" data-width="1200" data-height="548" data-duration="45"><div id="achievement-story-track" class="clip" data-start="0" data-duration="45" data-track-index="0">${capture.html}</div></div><script type="module">import{createTimeline}from'./timeline.mjs';const {timeline}=await createTimeline(document.querySelector('.achievement-film'),gsap);window.__timelines=window.__timelines||{};window.__timelines['alfalah-achievements']=timeline;</script></body></html>`);
  console.log('HyperFrames achievements composition captured from the actual homepage.');
 }finally{await browser.close()}
})().catch(e=>{console.error(e);process.exit(1)});




