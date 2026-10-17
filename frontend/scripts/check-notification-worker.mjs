// Real Angular worker/cache on localhost Chromium; synthetic API only, no Web Push provider or phone.
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { readFile, stat } from 'node:fs/promises';
import { writeFileSync, mkdirSync } from 'node:fs';
import { resolve, extname, sep } from 'node:path';
import { chromium, expect } from '@playwright/test';
const root = resolve('dist/n2-pwa-verification/browser');
const evidence = resolve('../docs/specs/notifications-pwa/verification/n2'); mkdirSync(evidence, {recursive:true});
const sentinel = 'SYNTHETIC_PRIVATE_N2_' + Date.now();
const misses = [];
let networkAvailable = true;
const server = createServer(async (req, res) => {
  if (!networkAvailable) { res.writeHead(503, {'Cache-Control':'no-store'}); res.end(); return; }
  const url = new URL(req.url, 'http://127.0.0.1');
  if (url.pathname.startsWith('/api/')) {
    res.writeHead(200, {'Content-Type':'application/json', 'Cache-Control':'private, no-store'});
    res.end(JSON.stringify(url.pathname === '/api/private-cache-probe' ? { sentinel } : {isSuccess:true,data:[],errors:[]})); return;
  }
  let file = resolve(root, '.' + decodeURIComponent(url.pathname));
  if (file !== root && !file.startsWith(root + sep)) { res.writeHead(404); res.end(); return; }
  try { if (!(await stat(file)).isFile()) file = resolve(root, 'index.html'); }
  catch { if (extname(file)) { misses.push(url.pathname); res.writeHead(404); res.end(); return; } file = resolve(root, 'index.html'); }
  try {
    const mime = {'.html':'text/html','.js':'text/javascript','.css':'text/css','.json':'application/json','.webmanifest':'application/manifest+json','.png':'image/png','.woff2':'font/woff2'}[extname(file)] || 'application/octet-stream';
    res.writeHead(200, {'Content-Type':mime,'Service-Worker-Allowed':'/','Cache-Control':'no-cache'}); res.end(await readFile(file));
  } catch { res.writeHead(404); res.end(); }
});
await new Promise(resolve => server.listen(4317, '127.0.0.1', resolve));
let browser;
try {
  browser = await chromium.launch({headless:true});
  const context = await browser.newContext(); const page = await context.newPage();
  await page.goto('http://127.0.0.1:4317/auth/school-login');
  // `active` can refer to an activating worker. Reload only after activation/claim
  // completes, otherwise a navigation can race clients.claim() and stay uncontrolled.
  await expect.poll(() => page.evaluate(async () => {
    const registrations = await navigator.serviceWorker.getRegistrations();
    return registrations.some(x=>x.active?.state === 'activated');
  }), {timeout:55000}).toBe(true);
  // Activation claims this existing page; do not introduce a reload during claim.
  try { await page.waitForFunction(()=>!!navigator.serviceWorker.controller); }
  catch(error) { console.log(JSON.stringify({misses,url:page.url(),registrations:await page.evaluate(async()=>(await navigator.serviceWorker.getRegistrations()).map(x=>({active:x.active?.state,waiting:x.waiting?.state,scope:x.scope})))})); throw error; }
  const registrations = await page.evaluate(async()=> (await navigator.serviceWorker.getRegistrations()).map(x=>({scope:x.scope,script:x.active?.scriptURL})));
  assert.equal(registrations.length,1); assert(registrations[0].script.endsWith('/ngsw-worker.js'));
  assert.equal(await page.evaluate(async()=> (await (await fetch('/api/private-cache-probe')).json()).sentinel),sentinel);
  // Angular schedules full version initialization after its idle interval. Trigger an
  // asset fetch after that interval and wait for the actual public-shell response.
  await page.waitForTimeout(6000);
  await page.evaluate(async()=>{await fetch('/index.html'); await fetch('/favicon.ico');});
  const workerManifest = JSON.parse(await readFile(resolve(root,'ngsw.json'),'utf8'));
  const expectedAssets = workerManifest.assetGroups.find(group=>group.name==='public-shell').urls;
  await expect.poll(() => page.evaluate(async expected=> {
    const paths=new Set();
    for(const name of await caches.keys()) for(const request of await (await caches.open(name)).keys()) paths.add(new URL(request.url).pathname);
    return expected.every(path=>paths.has(path));
  }, expectedAssets), {timeout:55000}).toBe(true);
  const cached = await page.evaluate(async()=> {
    const result=[]; for(const name of await caches.keys()) for(const request of await (await caches.open(name)).keys()) result.push(new URL(request.url).pathname); return [...new Set(result)];
  });
  if(!cached.includes('/index.html')) console.log(JSON.stringify({misses,cached,state:await page.evaluate(async()=>await(await fetch('/ngsw/state')).text())}));
  assert(cached.includes('/index.html'), 'public shell cached');
  if(!expectedAssets.every(path=>cached.includes(path))) console.log(JSON.stringify({expected:expectedAssets.length,cached:cached.length,missing:expectedAssets.filter(path=>!cached.includes(path)).slice(0,12),cacheNames:await page.evaluate(()=>caches.keys())}));
  assert(expectedAssets.every(path=>cached.includes(path)), 'complete public prefetch group cached before going offline');
  assert(!cached.some(x=>/^\/(api|hubs|uploads|files)\//.test(x)), 'private paths excluded');
  await page.goto('http://127.0.0.1:4317/auth/school-login?worker-check=navigation');
  await page.waitForFunction(()=>!!navigator.serviceWorker.controller);
  networkAvailable = false; await context.setOffline(true);
  const offline = await page.evaluate(async()=> {
    let privateAvailable = false; try { privateAvailable = (await fetch('/api/private-cache-probe')).ok; } catch {}
    return {privateAvailable, shellAvailable:(await fetch('/index.html')).ok};
  });
  assert.equal(offline.privateAvailable,false); assert.equal(offline.shellAvailable,true);
  const manifest = JSON.parse(await readFile(resolve(root,'manifest.webmanifest'),'utf8'));
  assert.equal(manifest.dir,'rtl'); assert.equal(manifest.display,'standalone');
  const png = await readFile(resolve(root,'assets/pwa/maskable-512.png'));
  assert.equal(png.readUInt32BE(16),512); assert.equal(png.readUInt32BE(20),512);
  writeFileSync(resolve(evidence,'worker-results.json'),JSON.stringify({date:'2026-10-17',browser:browser.version(),environment:'Real localhost Angular worker; synthetic API; desktop Chromium; no phone/provider delivery',registrations,cachedPathCount:cached.length,expectedPublicAssets:expectedAssets.length,completePublicPrefetch:true,controlledNavigation:true,privatePathCount:cached.filter(x=>x.startsWith('/api/')).length,offline,manifest:{id:manifest.id,dir:manifest.dir,display:manifest.display},passed:true},null,2));
  console.log(JSON.stringify({passed:true,cachedPathCount:cached.length,privatePathCount:0,offline}));
} finally { if(browser)await browser.close(); await new Promise(resolve=>server.close(resolve)); }
