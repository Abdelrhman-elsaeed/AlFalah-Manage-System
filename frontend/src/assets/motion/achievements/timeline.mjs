// HyperFrames choreography: one finite, paused, deterministic 45-second timeline.
let gsapReady;
async function loadGsap(){
 if(globalThis.gsap)return globalThis.gsap;
 gsapReady??=new Promise((resolve,reject)=>{const script=document.createElement('script');script.src=new URL('../falah/assets/gsap.min.js',import.meta.url).href;script.onload=()=>resolve(globalThis.gsap);script.onerror=reject;document.head.append(script)});
 return gsapReady;
}
export async function createTimeline(root,engine){
 const gsap=engine||await loadGsap();await document.fonts.ready;
 const tl=gsap.timeline({paused:true});
 const scenes=[...root.querySelectorAll('.achievement-scene')];
 for(const [i,scene] of scenes.entries()){
  const start=i*9,find=s=>scene.querySelectorAll(s);
  // Each chapter gets a long readable hold. Reveals share the quill's diagonal.
  tl.fromTo(scene,{opacity:i?0:1,clipPath:i?(i%2?'inset(0% 0% 100% 0% round 80px)':'inset(0% 100% 0% 0% round 80px)'):'inset(0% 0% 0% 0% round 0px)'},{opacity:1,clipPath:'inset(0% 0% 0% 0% round 0px)',duration:i?1.4:1,ease:'power2.inOut'},i?start-.8:start);
  tl.fromTo(find('.achievement-number'),{y:i?22:0,opacity:i?0:1},{y:0,opacity:1,duration:1.35,ease:'power3.out'},start+.15);
  tl.fromTo(find('.achievement-stat>span'),{y:i?12:0,opacity:i?0:1},{y:0,opacity:1,duration:1.2,ease:'power2.out'},start+.45);
  tl.fromTo(find('.achievement-title'),{clipPath:i?'inset(0% 0% 100% 0%)':'inset(0% 0% 0% 0%)',y:i?14:0},{clipPath:'inset(0% 0% 0% 0%)',y:0,duration:1.35,ease:'power2.out'},start+.65);
  tl.fromTo(find('.achievement-description,.achievement-evidence'),{opacity:i?0:1,y:i?10:0},{opacity:1,y:0,duration:1,stagger:.15,ease:'power2.out'},start+1.3);
  for(const path of find('.achievement-signature path')){const length=path.getTotalLength();tl.fromTo(path,{strokeDasharray:length,strokeDashoffset:length},{strokeDashoffset:0,duration:1.8,ease:'power2.inOut'},start+1.1)}
  tl.fromTo(root.querySelector(`[data-progress="${i}"]`),{scaleX:0},{scaleX:1,duration:9,ease:'none'},start);
  if(i>0)tl.to(scenes[i-1],{opacity:0,duration:.7,ease:'power1.inOut'},start-.7);
 }
 const q=s=>root.querySelectorAll(s);
 tl.fromTo(q('.keepsake-back'),{rotation:7,y:14},{rotation:3,y:0,duration:2,ease:'power2.out'},0);
 tl.fromTo(q('.keepsake-photo'),{rotation:-9,rotationY:14,y:32,opacity:1},{rotation:-3,rotationY:-4,y:0,opacity:1,duration:2,ease:'power2.out'},.25);
 tl.to(q('.keepsake-photo'),{rotation:-1.5,rotationY:3,y:-7,duration:8,ease:'sine.inOut'},2.25);
 tl.fromTo(q('.keepsake-seal'),{scale:.85,rotation:-25,opacity:1},{scale:1,rotation:-8,opacity:1,duration:1.5,ease:'power2.out'},1.3);
 tl.fromTo(q('.participation-network'),{rotationX:24,rotationZ:-10,scale:.83},{rotationX:5,rotationZ:2,scale:1,duration:2.8,ease:'power2.out'},9.3);
 tl.fromTo(q('.network-spoke'),{strokeDasharray:160,strokeDashoffset:160},{strokeDashoffset:0,duration:1.5,stagger:.025,ease:'power2.inOut'},9.5);
 tl.fromTo(q('.network-dot'),{opacity:0,scale:.2,svgOrigin:'250 210'},{opacity:1,scale:1,duration:.8,stagger:.04,ease:'power2.out'},10.2);
 tl.fromTo(q('.network-label'),{opacity:0,y:8},{opacity:1,y:0,duration:.7,stagger:.25},10.8);
 tl.fromTo(q('.sheet-back'),{rotation:-15,rotationX:18,y:30,opacity:0},{rotation:-9,rotationX:0,y:0,opacity:1,duration:1.6,ease:'power2.out'},18.3);
 tl.fromTo(q('.sheet-front'),{rotation:12,rotationY:-25,y:40,opacity:0},{rotation:5,rotationY:0,y:0,opacity:1,duration:1.8,ease:'power2.out'},18.6);
 for(const path of q('.project-line')){const length=path.getTotalLength();tl.fromTo(path,{strokeDasharray:length,strokeDashoffset:length},{strokeDashoffset:0,duration:2.1,ease:'power2.inOut'},20)}
 tl.fromTo(q('.idea-emblem'),{opacity:0,y:30,rotation:-18},{opacity:1,y:0,rotation:5,duration:1.5,ease:'power2.out'},20.3);
 tl.to(q('.idea-emblem'),{y:-8,rotation:2,duration:3.5,ease:'sine.inOut'},21.8);
 for(let i=0;i<3;i++){
  tl.fromTo(q(`.medal-${i}`),{y:-45,rotation:i===1?0:-8+i*16,opacity:0},{y:i===1?0:-12,rotation:i===1?0:-8+i*16,opacity:1,duration:1.5,ease:'power2.out'},27.3+i*.18);
  tl.fromTo(q(`.medal-${i} .medal-glint`),{xPercent:-100},{xPercent:100,duration:1.6,ease:'sine.inOut'},29+i*.35);
 }
 tl.to(q('.medal-1'),{rotation:-2,duration:3,ease:'sine.inOut'},30);
 tl.fromTo(q('.podium'),{rotationY:-26,rotationX:14},{rotationY:-16,rotationX:9,duration:2.8,ease:'sine.inOut'},36.3);
 tl.fromTo(q('.podium-step'),{y:45,opacity:0},{y:0,opacity:1,duration:1.3,stagger:.22,ease:'power2.out'},36.45);
 tl.fromTo(q('.podium-cup'),{scale:.7,y:20,opacity:0},{scale:1,y:0,opacity:1,duration:1.3,ease:'power2.out'},38.3);
 // A copy of the first pose makes the last-to-first handoff seek-safe.
 tl.time(.001,false);
 const bridge=scenes[0].cloneNode(true);bridge.classList.remove('current');bridge.classList.add('achievement-loop-bridge');bridge.setAttribute('aria-hidden','true');bridge.setAttribute('inert','');bridge.querySelectorAll('[id]').forEach(e=>e.removeAttribute('id'));bridge.querySelector('.achievement-evidence')?.remove();root.querySelector('.achievement-stage').append(bridge);
 tl.fromTo(bridge,{opacity:0,clipPath:'inset(0% 0% 100% 0% round 80px)'},{opacity:1,clipPath:'inset(0% 0% 0% 0% round 0px)',duration:.8,ease:'power2.inOut'},44.2);
 tl.time(0,false);
 const kill=tl.kill.bind(tl);tl.kill=()=>{bridge.remove();return kill()};
 return {timeline:tl};
}
