// Browser-only playback host. The authored composition remains paused/seekable.
(async()=>{
 await window.__afTimelinePromise;
 const root=document.getElementById('falah-section'),wrapper=window.__afWrapper||document.getElementById('af-live-section'),reference=window.__afReference||document.querySelector('#af-vision-reference .vision');
 const tl=window.__timelines.main,scenes=window.__afScenes,duration=47;
 const mq=matchMedia('(prefers-reduced-motion: reduce)');
 let time=0,playing=false,userPaused=false,visible=false,frame=0,lastStamp=null,worldPromise=null,lastRendered=-1,loopCount=0;
 const cleanups=[];function on(target,type,handler){target.addEventListener(type,handler);cleanups.push(()=>target.removeEventListener(type,handler))}
 const playButton=document.getElementById('af-play'),label=document.querySelector('.af-chapter-name');
 const pauseIcon=playButton.innerHTML,playIcon='<svg class="af-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M8 4l12 8-12 8z"/></svg>';
 function syncHeight(){const h=reference.getBoundingClientRect().height;wrapper.style.setProperty('--af-section-height',`${h}px`);const proof=document.getElementById('af-height-proof');if(proof)proof.textContent=`ارتفاع السكشن: ${h.toFixed(2)}px · ارتفاع رؤيتنا: ${h.toFixed(2)}px`;window.__afRefreshMetrics?.();if(!playing)paint()}
 const referenceObserver=new ResizeObserver(syncHeight);referenceObserver.observe(reference);cleanups.push(()=>referenceObserver.disconnect());syncHeight();
 async function ensureWorld(){
  if(!worldPromise)worldPromise=(window.__afLoadWorld?window.__afLoadWorld():import('./alfalah-section/section-world.mjs')).catch(()=>{root.classList.add('af-vector-mode');window.__afWorldReady=false});
  await worldPromise;
 }
 function paint(){
  window.__afCurrentTime=time;tl.time(time,false);
  for(const s of scenes){const active=time>=s.start&&time<s.end,e=document.getElementById(`af-shot-${s.id}`);e.style.visibility=active?'visible':'hidden';e.setAttribute('aria-hidden',String(!active))}
  const current=[...scenes].reverse().find(s=>time>=s.start)||scenes[0];label.textContent=current.chapter;
  for(const b of document.querySelectorAll('.af-segments button'))b.setAttribute('aria-current',String(b.dataset.scene===current.id));
  window.__afRenderWorld?.(time);
 }
 function setButton(){playButton.innerHTML=playing?pauseIcon:playIcon;playButton.setAttribute('aria-label',playing?'إيقاف الحركة':'تشغيل الحركة');playButton.setAttribute('aria-pressed',String(playing))}
 function pause(manual=false){playing=false;lastStamp=null;cancelAnimationFrame(frame);if(manual)userPaused=true;setButton()}
 function tick(stamp){
  if(!playing||window.__afDisposed)return;if(lastStamp!==null)time+=Math.min((stamp-lastStamp)/1000,.12);lastStamp=stamp;
  if(time>=duration){time%=duration;loopCount++;lastRendered=-1}
  // 30fps is enough for the section and bounds WebGL work on high-refresh screens.
  if(time-lastRendered>=1/30-.0005||time>=duration-.002){paint();lastRendered=time}
  frame=requestAnimationFrame(tick);
 }
 async function play(manual=false){
  if(manual)userPaused=false;if(time>=duration-.01)time=0;
  if(playing)return;playing=true;setButton();paint();
  ensureWorld().then(()=>{if(!playing)paint()});
  lastStamp=null;lastRendered=-1;frame=requestAnimationFrame(tick);
 }
 async function seek(t,{resume=false}={}){pause(true);time=Math.max(0,Math.min(duration-.001,t));lastRendered=-1;if(!mq.matches)await ensureWorld();paint();if(resume)await play(true)}
 playButton.addEventListener('click',()=>playing?pause(true):play(true));
 document.getElementById('af-replay').addEventListener('click',()=>{time=0;lastRendered=-1;userPaused=false;paint();if(!playing)play(true)});
 for(const b of document.querySelectorAll('.af-segments button'))b.addEventListener('click',()=>seek(+b.dataset.time+.8,{resume:true}));
 const intersectionObserver=new IntersectionObserver(entries=>{visible=entries[0].isIntersecting;if(!visible)pause();else if(!userPaused&&!mq.matches&&!document.hidden)play()}, {threshold:.15});intersectionObserver.observe(wrapper);cleanups.push(()=>intersectionObserver.disconnect());
 on(document,'visibilitychange',()=>{if(document.hidden)pause();else if(visible&&!userPaused&&!mq.matches)play()});
 on(mq,'change',()=>{pause();userPaused=false;if(mq.matches){time=45.5;paint()}else if(visible)play()});
 if(mq.matches){time=45.5;paint();setButton()}else{paint();setButton()}
 window.__afPlayer={seek,play:()=>play(true),pause:()=>pause(true),dispose:()=>{pause();cleanups.forEach(fn=>fn())},get loopCount(){return loopCount},get time(){return time},get playing(){return playing},get reducedMotion(){return mq.matches},get visible(){return visible},get worldLoaded(){return!!window.__afWorldReady}};
})();
