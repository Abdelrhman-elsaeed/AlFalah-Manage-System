// Package the reviewed composition as a lifecycle-owned native Angular asset.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const ROOT=__dirname,REPO=path.resolve(ROOT,'../../..'),OUT=path.join(REPO,'frontend/src/assets/motion/falah');
const COMPONENT=path.join(REPO,'frontend/src/app/features/landing/components/system-story');
fs.mkdirSync(OUT,{recursive:true});fs.mkdirSync(COMPONENT,{recursive:true});
const authored=fs.readFileSync(path.join(ROOT,'alfalah-section/index.html'),'utf8');
const markup=authored.slice(authored.indexOf('<div id="falah-section"'),authored.indexOf('<script>window.__afScenes=')).replaceAll('font-family="Tajawal"','font-family="FalahMotion"');
if(!markup.startsWith('<div id="falah-section"')||!markup.endsWith('</div>'))throw new Error('Composition markup contract changed');
const scenes=require('./section-scenes.cjs');
let css=fs.readFileSync(path.join(ROOT,'section.css'),'utf8').replaceAll('font-family:Tajawal','font-family:FalahMotion');
let motion=fs.readFileSync(path.join(ROOT,'section-timeline.js'),'utf8').replaceAll('px Tajawal','px FalahMotion').replaceAll("'font-family':'Tajawal'","'font-family':'FalahMotion'");
const player=fs.readFileSync(path.join(ROOT,'section-player.js'),'utf8');
const digest=crypto.createHash('sha256').update(markup+css+motion+player+fs.readFileSync(path.join(ROOT,'section-world.mjs'),'utf8')+fs.readFileSync(path.join(ROOT,'school-world.mjs'),'utf8')+fs.readFileSync(__filename,'utf8')).digest('hex').slice(0,12);
fs.cpSync(path.join(ROOT,'alfalah-section/assets'),path.join(OUT,'assets'),{recursive:true});
// Use relative Three imports in production, with no page-global import map.
const rounded=path.join(OUT,'assets/three/RoundedBoxGeometry.js');fs.writeFileSync(rounded,fs.readFileSync(rounded,'utf8').replaceAll("from 'three'","from './three.module.js'"));
let geometry=fs.readFileSync(path.join(ROOT,'school-world.mjs'),'utf8').split('const shots=')[0];
geometry=geometry.replace(/^import .*?;\r?\n/gm,'').replace('await document.fonts.ready;','await document.fonts.ready;if(window.__afDisposed)return;').replace('key.shadow.mapSize.set(2048,2048)','key.shadow.mapSize.set(1024,1024)').replaceAll('px Tajawal','px FalahMotion');
const world=`import * as THREE from './assets/three/three.module.js';\nimport { RoundedBoxGeometry } from './assets/three/RoundedBoxGeometry.js';\nexport async function createWorld(root,window,document){\n${geometry}\n${fs.readFileSync(path.join(ROOT,'section-world.mjs'),'utf8')}\n}\n`;
fs.writeFileSync(path.join(OUT,'world.mjs'),world);fs.writeFileSync(path.join(OUT,'section.css'),css);
const runtime=`// Generated from the reviewed HyperFrames section. No preview-server dependency.
const revision=${JSON.stringify(digest)};
const markup=${JSON.stringify(markup)};
const scenes=${JSON.stringify(scenes)};
const base=new URL('./',import.meta.url);
let gsapReady,stylesReady;
function asset(name){return new URL(name,base).href}
function loadStyles(){return stylesReady||=(new Promise((resolve,reject)=>{const id='falah-story-styles-'+revision;const link=document.createElement('link');link.id=id;link.rel='stylesheet';link.href=asset('section.css?v='+revision);link.onload=resolve;link.onerror=reject;document.head.append(link)}))}
function loadGsap(){if(globalThis.gsap)return Promise.resolve(globalThis.gsap);return gsapReady||=(new Promise((resolve,reject)=>{const script=document.createElement('script');script.src=asset('assets/gsap.min.js');script.onload=()=>resolve(globalThis.gsap);script.onerror=reject;document.head.append(script)}))}
function startTimeline(window,document,gsap){${motion}}
function startPlayer(window,document){${player}}
export async function mountSection(container,{heightReference,wrapper,isCancelled=()=>false}={}){
 const [,gsap]=await Promise.all([loadStyles(),loadGsap()]);
 if(isCancelled())return{dispose(){}};
 await Promise.all([400,500,700,800].map(w=>document.fonts.load(w+' 28px FalahMotion')));
 if(isCancelled())return{dispose(){}};
 container.innerHTML=markup;
 const root=container.querySelector('#falah-section');
 root.querySelectorAll('img[src]').forEach(img=>img.src=asset(img.getAttribute('src')));
 const events=new EventTarget();
 const env={__afScenes:scenes,__afWrapper:wrapper||container,__afReference:heightReference,__afObservers:[],__timelines:{},addEventListener:events.addEventListener.bind(events),dispatchEvent:events.dispatchEvent.bind(events)};
 const scopedDocument=new Proxy(document,{get(target,key){if(key==='getElementById')return id=>root.id===id?root:root.querySelector('#'+CSS.escape(id));if(key==='querySelector')return selector=>root.querySelector(selector);if(key==='querySelectorAll')return selector=>root.querySelectorAll(selector);const value=Reflect.get(target,key,target);return typeof value==='function'?value.bind(target):value}});
 env.__afTimelinePromise=new Promise(resolve=>env.__resolveAfTimeline=resolve);
 env.__afLoadWorld=async()=>{const module=await import('./world.mjs?v='+revision);if(!env.__afDisposed)await module.createWorld(root,env,scopedDocument)};
 startTimeline(env,scopedDocument,gsap);startPlayer(env,scopedDocument);
 await env.__afTimelinePromise;await Promise.resolve();
 const controller={seek:(...args)=>env.__afPlayer.seek(...args),play:()=>env.__afPlayer.play(),pause:()=>env.__afPlayer.pause(),get time(){return env.__afPlayer.time},get loopCount(){return env.__afPlayer.loopCount},get playing(){return env.__afPlayer.playing},get disposed(){return!!env.__afDisposed},dispose(){if(env.__afDisposed)return;env.__afDisposed=true;env.__afPlayer?.dispose();env.__timelines.main?.kill();env.__afObservers.forEach(o=>o.disconnect());env.__afDisposeWorld?.();container.replaceChildren()}};
 Object.defineProperty(root,'falahStory',{value:controller});
 if(isCancelled())controller.dispose();
 return controller;
}
`;
fs.writeFileSync(path.join(OUT,'runtime.mjs'),runtime);
fs.writeFileSync(path.join(COMPONENT,'story-assets.ts'),`// Generated by docs/motion/hyperframes/build-site-section.cjs\nexport const FALAH_STORY_MODULE = 'assets/motion/falah/runtime.mjs?v=${digest}';\n`);
fs.writeFileSync(path.join(ROOT,'reference/site-section-manifest.json'),JSON.stringify({revision:digest,homepageIntegrated:true,native:true,loop:true,source:'HyperFrames section composition',assets:'frontend/src/assets/motion/falah',module:'runtime.mjs',heightReference:'#vision .vision'},null,2));
console.log('Packaged native homepage section:',digest);
