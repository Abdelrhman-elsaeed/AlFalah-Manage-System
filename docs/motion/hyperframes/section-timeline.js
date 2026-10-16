document.fonts.ready.then(()=>{
 const root=document.getElementById('falah-section'),scenes=window.__afScenes;
 const tl=gsap.timeline({paused:true});
 const q=(id,s)=>`#af-picture-${id} ${s}`;
 const portals=[];let size={w:root.clientWidth,h:root.clientHeight};
 const paperReveal={p:0};let paperRect={left:1000,top:300,width:84,height:88};
 function paintPaper(){const u=1-paperReveal.p;document.getElementById('af-picture-archive').style.clipPath=`inset(${paperRect.top*u}px ${(size.w-paperRect.left-paperRect.width)*u}px ${(size.h-paperRect.top-paperRect.height)*u}px ${paperRect.left*u}px round ${6*u}px)`}
 function measurePaper(){const el=document.querySelector('.af-permit-paper');let n=el,x=0,y=0;while(n&&n!==root){x+=n.offsetLeft;y+=n.offsetTop;n=n.offsetParent}paperRect={left:x,top:y,width:el.offsetWidth,height:el.offsetHeight};paintPaper()}
 const ns='http://www.w3.org/2000/svg';
 function pivot(word){
  const c=document.createElement('canvas');c.width=1000;c.height=400;const x=c.getContext('2d',{willReadFrequently:true});x.font='800 220px Tajawal';x.direction='rtl';x.textAlign='center';x.fillText(word,500,270);const {data}=x.getImageData(0,0,1000,400);let best={score:-1,x:500,y:220};
  for(let yy=90;yy<270;yy+=3)for(let xx=250;xx<750;xx+=3){if(data[(yy*1000+xx)*4+3]<245)continue;let r=0;for(;r<26;r+=2){let solid=true;for(let k=0;k<10;k++){const a=k*Math.PI/5,px=Math.round(xx+Math.cos(a)*r),py=Math.round(yy+Math.sin(a)*r);if(data[(py*1000+px)*4+3]<245){solid=false;break}}if(!solid)break}const score=r-Math.abs(xx-500)*.005;if(score>best.score)best={score,x:xx,y:yy}}
  return{x:best.x-500,y:best.y-270};
 }
 function paintPortal(p){const f=Math.min(size.w*.22,size.h*.65)/220,tr=`translate(${size.w/2} ${size.h/2}) scale(${p.state.s*f}) translate(${-p.pivot.x} ${-p.pivot.y})`;p.mask.setAttribute('transform',tr);p.word.setAttribute('transform',tr)}
 function fitCubes(){
  const ctx=document.createElement('canvas').getContext('2d');ctx.direction='rtl';
  document.querySelectorAll('.af-cube-window').forEach(box=>{
   box.style.fontSize='';const style=getComputedStyle(box),base=parseFloat(style.fontSize);ctx.font=`800 ${base}px Tajawal`;
   const widest=Math.max(...[...box.querySelectorAll('.af-cube-face')].map(face=>ctx.measureText(face.textContent).width));
   const usable=box.clientWidth-54;box.style.fontSize=`${Math.min(base,base*usable/(widest+base*.22))}px`;
  });
 }
 function refreshMetrics(){size={w:root.clientWidth,h:root.clientHeight};portals.forEach(p=>{p.svg.setAttribute('viewBox',`0 0 ${size.w} ${size.h}`);paintPortal(p)});writingWidth=document.querySelector('.af-writing').clientWidth;paintQuill();fitCubes();measurePaper()}
 let writingWidth=document.querySelector('.af-writing').clientWidth;const pen={p:1,tilt:0};
 function paintQuill(){const y=Math.sin(pen.p*Math.PI*14)*3;document.querySelector('.af-quill').style.transform=`translate(${writingWidth*pen.p}px,${y}px) rotate(${pen.tilt+Math.sin(pen.p*Math.PI*8)*4}deg)`}
 function draw(selector,at,duration=.6){document.querySelectorAll(selector).forEach((p,i)=>{const len=p.getTotalLength();tl.fromTo(p,{strokeDasharray:len,strokeDashoffset:len},{strokeDashoffset:0,duration,ease:'power2.inOut'},at+i*.06)})}
 function marker(el,at){draw(`#${el.id} .af-marker-stroke`,at,.55);draw(`#${el.id} .af-marker-grain`,at+.16,.5);const word=el.querySelector('.af-marker-word');tl.fromTo(word,{clipPath:'inset(0% 0% 0% 100%)'},{clipPath:'inset(0% 0% 0% 0%)',duration:.55,ease:'power2.inOut'},at);tl.set(word,{clipPath:'none'},at+.56)}
 scenes.forEach((s,index)=>{
  const pic=`#af-picture-${s.id}`,t=s.start,enter=t+(index?.42:0);
  if(s.transition==='word'){
   const mask=document.getElementById(`af-mask-${s.id}`),piv=pivot(s.word);
   const svg=document.createElementNS(ns,'svg');svg.classList.add('af-word-caption');svg.setAttribute('data-layout-allow-overflow','');
   const word=document.createElementNS(ns,'text');for(const[k,v]of Object.entries({'font-family':'Tajawal','font-size':220,'font-weight':800,'text-anchor':'middle',direction:'rtl',fill:s.tone==='light'?'#f8faf8':'#0b3f29'}))word.setAttribute(k,v);word.textContent=s.word;svg.append(word);document.getElementById(`af-picture-${scenes[index-1].id}`).append(svg);
   const p={mask,word,svg,pivot:piv,state:{s:1}};portals.push(p);paintPortal(p);
   tl.to(q(scenes[index-1].id,'.af-layout'),{opacity:0,duration:.13},t-.25);
   tl.fromTo(svg,{opacity:0},{opacity:1,duration:.17},t-.17);
   tl.fromTo(p.state,{s:1},{s:100,duration:.49,ease:'expo.in',onUpdate:()=>paintPortal(p)},t);
   tl.set(pic,{clipPath:'inset(0% 0% 0% 0%)'},t+.49);
  }else if(s.transition==='care-handoff'){
   tl.fromTo(pic,{clipPath:'inset(35% 63% 27% 22% round 28px)'},{clipPath:'inset(0% 0% 0% 0% round 0px)',duration:.85,ease:'power3.inOut'},t);
  }else if(s.transition==='line'){
   tl.fromTo(pic,{clipPath:'inset(69.5% 100% 30% 0% round 30px)'},{clipPath:'inset(0% 0% 0% 0% round 0px)',duration:.48,ease:'power3.inOut'},t);
  }else if(s.transition==='iris'){
   tl.fromTo(pic,{clipPath:`circle(0% at ${s.id==='gate'?'82% 30%':'25% 55%'})`},{clipPath:`circle(135% at ${s.id==='gate'?'82% 30%':'25% 55%'})`,duration:.49,ease:'power3.inOut'},t);
  }else if(s.transition==='grid'){
   for(let i=0;i<5;i++)tl.fromTo(`#af-grid-${i}`,{attr:{height:0}},{attr:{height:1},duration:.35,ease:'power3.inOut'},t+i*.035);
   tl.set(pic,{clipPath:'inset(0% 0% 0% 0%)'},t+.49);
  }else if(s.transition==='document'){
   tl.fromTo(paperReveal,{p:0},{p:1,duration:.49,ease:'power3.inOut',onUpdate:paintPaper},t);
  }else if(s.transition==='shutter'){
   tl.fromTo(pic,{clipPath:'inset(0% 50% 0% 50%)'},{clipPath:'inset(0% 0% 0% 0%)',duration:.49,ease:'power3.inOut'},t);
  }
  const lines=document.querySelectorAll(q(s.id,'.af-title-line'));if(lines.length)tl.fromTo(lines,{y:28,opacity:0},{y:0,opacity:1,duration:.55,ease:'power3.out'},enter+.1);
  const copy=document.querySelector(q(s.id,'.af-support'));if(copy)tl.fromTo(copy,{clipPath:'inset(100% 0% 0% 0%)',y:7},{clipPath:'inset(0% 0% 0% 0%)',y:0,duration:.45,ease:'power2.out'},enter+.35);
  const lead=document.querySelector(q(s.id,'.af-lead'));if(lead)tl.fromTo(lead,{opacity:0,y:8},{opacity:.8,y:0,duration:.35},enter);
  const proof=document.querySelector(q(s.id,'.af-proof'));if(proof)tl.fromTo(proof,{opacity:0,y:10},{opacity:.8,y:0,duration:.4},enter+.7);
  const cube=document.getElementById(`af-cube-${s.id}`);
  if(cube){tl.fromTo(cube,{rotationX:-22,opacity:0},{rotationX:0,opacity:1,duration:.5,ease:'power3.out'},enter);tl.to(cube,{rotationX:90,duration:.65,ease:'power3.inOut'},t+2.35)}
  document.querySelectorAll(q(s.id,'.af-marker')).forEach((el,i)=>{
   el.id=`af-highlight-${s.id}-${i}`;
   const face=el.closest('.af-cube-face');marker(el,face&&+face.dataset.face? t+2.9:enter+(s.id==='intro'?1.95:s.id==='outro'?.8:.3));
  });
  if(index){const color=s.tone==='light'?'#0b3f29':'#d8eee1';tl.to('.af-top-brand,.af-chrome',{color,duration:.15},t+(s.transition==='care-handoff'?.8:.3))}
 });
 // The first scene is written as a joined Arabic phrase, rather than split glyphs.
 tl.fromTo('.af-school',{opacity:1,y:0},{opacity:1,y:0,duration:.45},.1);
 tl.fromTo('.af-writing-name',{clipPath:'inset(0% 0% 0% 100%)'},{clipPath:'inset(0% 0% 0% 0%)',duration:1.25,ease:'none'},.3);
 tl.fromTo('.af-quill',{opacity:0},{opacity:1,duration:.15},.12);
 tl.fromTo(pen,{p:1},{p:0,duration:1.25,ease:'none',onUpdate:paintQuill},.3);
 tl.to(pen,{p:-.15,tilt:-18,duration:.42,ease:'power2.out',onUpdate:paintQuill},1.55);
 tl.to('.af-quill',{opacity:0,duration:.2},1.8);
 tl.fromTo('.af-intro-promise',{opacity:0,y:10},{opacity:1,y:0,duration:.45},1.55);
 tl.fromTo('.af-coin',{rotationY:-24,rotationX:9},{rotationY:336,rotationX:9,duration:2.6,ease:'power2.inOut'},.1);
 tl.fromTo('.af-coin-shine',{xPercent:-140},{xPercent:140,duration:.8,ease:'power2.inOut'},2.6);
 tl.fromTo('.af-top-brand',{opacity:0},{opacity:1,duration:.25},4.6);
 tl.fromTo('.af-staff-person',{y:18,opacity:0,rotationY:-45},{y:0,opacity:1,rotationY:0,duration:.42,stagger:.035,ease:'power3.out'},4.55);
 tl.fromTo('.af-duty-ribbon',{clipPath:'inset(0% 0% 0% 100%)'},{clipPath:'inset(0% 0% 0% 0%)',duration:.5},5.4);
 draw('.af-fingerprint path',8.95,.75);tl.fromTo('.af-scan-line',{y:0},{y:66,duration:.8,repeat:3,yoyo:true,ease:'sine.inOut'},9.2);
 tl.fromTo('.af-arrival-record',{scale:.85,opacity:0,y:15},{scale:1,opacity:1,y:0,duration:.5,ease:'power3.out'},10.1);
 tl.fromTo('.af-assembly-roles>span',{opacity:0,y:12},{opacity:1,y:0,duration:.4,stagger:.12},13.5);
 tl.fromTo('.af-timetable',{rotationX:18,scale:.8,opacity:0},{rotationX:0,scale:1,opacity:1,duration:.55,ease:'power3.out'},17.35);
 tl.fromTo('.af-table-grid>span',{opacity:0,y:12},{opacity:1,y:0,duration:.3,stagger:.025},17.65);
 tl.to('.af-timetable',{scale:1.6,rotationX:30,opacity:0,duration:.7,ease:'power3.inOut'},19.2);
 draw('.af-care-link path',22.7,1.7);tl.fromTo('.af-care-roles>span',{opacity:0,scale:.8},{opacity:1,scale:1,duration:.45,stagger:.5,ease:'power3.out'},22.6);
 tl.fromTo('.af-guardian-phone',{rotationY:-12,rotationX:4,y:14,opacity:1},{rotationY:0,rotationX:0,y:0,opacity:1,duration:.8,ease:'power3.out'},26.6);
 tl.fromTo('.af-parent-update',{opacity:0,x:18},{opacity:1,x:0,duration:.45,stagger:.28},26.95);
 tl.fromTo('.af-permit-paper',{rotation:-18,scale:.4,opacity:0},{rotation:0,scale:1,opacity:1,duration:.55,ease:'power3.out'},31.6);
 tl.fromTo('.af-gate-flow>span',{opacity:0},{opacity:1,duration:.35,stagger:.8},31.4);
 tl.fromTo('.af-gate-confirm',{opacity:0,y:12},{opacity:1,y:0,duration:.45,ease:'power3.out'},35.2);
 tl.fromTo('.af-paper-back',{rotationX:35,rotationY:-30,rotation:-25,y:-35,opacity:0},{rotationX:0,rotationY:0,rotation:-9,y:0,opacity:1,duration:.8,ease:'power3.out'},36.4);
 tl.fromTo('.af-paper-mid',{rotationX:30,rotation:22,y:-25,opacity:0},{rotationX:0,rotation:5,y:0,opacity:1,duration:.8,ease:'power3.out'},36.55);
 tl.fromTo('.af-paper-front',{rotationX:30,rotationY:20,rotation:14,y:-20,opacity:0},{rotationX:0,rotationY:0,rotation:-2,y:0,opacity:1,duration:.8,ease:'power3.out'},36.7);
 tl.fromTo('.af-paper-front>span,.af-archive-lock',{opacity:0},{opacity:1,duration:.4,stagger:.25},38);
 tl.fromTo('.af-outro-lockup',{scale:.85,opacity:0},{scale:1,opacity:1,duration:.55,ease:'power3.out'},41.55);
 tl.fromTo('.af-outro-title',{y:25,opacity:0},{y:0,opacity:1,duration:.6,ease:'power3.out'},41.9);
 tl.fromTo('.af-benefits>span',{y:16,opacity:0},{y:0,opacity:1,duration:.5,stagger:.13,ease:'power3.out'},42.9);
 tl.fromTo('.af-outro-school',{opacity:0},{opacity:1,duration:.5},43.7);
 tl.to('.af-top-brand',{opacity:0,duration:.2},41.6);
 tl.to('.af-outro',{opacity:0,y:-12,duration:.3,ease:'power2.in'},45.9);
 tl.to({hold:0},{hold:1,duration:.1},46.9);
 tl.time(0,false);
 const bridge=document.getElementById('af-picture-intro').cloneNode(true);bridge.classList.add('af-loop-bridge');bridge.setAttribute('aria-hidden','true');bridge.querySelectorAll('[id]').forEach(el=>el.id=`af-loop-${el.id}`);bridge.id='af-loop-bridge';root.append(bridge);
 tl.fromTo(bridge,{opacity:0},{opacity:1,duration:.8,ease:'power2.inOut'},46.2);
 window.__timelines=window.__timelines||{};window.__timelines.main=tl;
 window.__afRefreshMetrics=refreshMetrics;const resizeObserver=new ResizeObserver(refreshMetrics);resizeObserver.observe(root);(window.__afObservers||=[]).push(resizeObserver);refreshMetrics();
 window.__afTimelineReady=true;window.__resolveAfTimeline?.();
});
