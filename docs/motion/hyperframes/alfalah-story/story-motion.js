// One paused, seek-safe timeline. Runtime controls scene visibility and playback.
document.fonts.ready.then(()=>{
 const tl=gsap.timeline({paused:true});
 const scenes=window.__storyScenes;
 const q=(id,sel)=>`#picture-${id} ${sel}`;
 const ns='http://www.w3.org/2000/svg';
 // Find the centre of a solid piece of Arabic letter ink. Zooming around that
 // point makes the incoming scene fill the frame through the actual word.
 function inkPivot(word){
  const cv=document.createElement('canvas');cv.width=1200;cv.height=450;
  const ctx=cv.getContext('2d',{willReadFrequently:true});ctx.font='800 250px Tajawal';ctx.textAlign='center';ctx.direction='rtl';ctx.fillText(word,600,300);
  const {data}=ctx.getImageData(0,0,1200,450);let best={score:-1,x:600,y:220};
  for(let y=120;y<300;y+=3)for(let x=320;x<880;x+=3){
   if(data[(y*1200+x)*4+3]<245)continue;
   let r=0;for(;r<30;r+=2){let solid=true;for(let k=0;k<12;k++){const a=k*Math.PI/6,xx=Math.round(x+Math.cos(a)*r),yy=Math.round(y+Math.sin(a)*r);if(data[(yy*1200+xx)*4+3]<245){solid=false;break}}if(!solid)break}
   const score=r-Math.abs(x-600)*.006;if(score>best.score)best={score,x,y};
  }
  return {x:best.x-600,y:best.y-300};
 }
 scenes.forEach((s,index)=>{
  const pic=`#picture-${s.id}`,t=s.start;
  if(s.transition==='word'){
   const pivot=inkPivot(s.word),mask=document.getElementById(`word-mask-${s.id}`);
   const svg=document.createElementNS(ns,'svg');svg.setAttribute('viewBox','0 0 1600 900');svg.classList.add('portal-caption');svg.style.cssText='position:absolute;inset:0;width:100%;height:100%;opacity:0;pointer-events:none;overflow:hidden';
   svg.setAttribute('data-layout-allow-overflow','');
   const text=document.createElementNS(ns,'text');for(const[k,v]of Object.entries({'text-anchor':'middle',direction:'rtl','font-family':'Tajawal','font-weight':'800','font-size':'250',fill:s.tone==='light'?'#f8faf8':'#0b3f29'}))text.setAttribute(k,v);text.textContent=s.word;svg.append(text);
   document.getElementById(`picture-${scenes[index-1].id}`).append(svg);
   const state={scale:1};
   const update=()=>{const tr=`translate(800 450) scale(${state.scale}) translate(${-pivot.x} ${-pivot.y})`;mask.setAttribute('transform',tr);text.setAttribute('transform',tr)};
   update();
   tl.to(q(scenes[index-1].id,'.editorial'),{opacity:0,duration:.2},t-.58);
   tl.fromTo(svg,{opacity:0},{opacity:1,duration:.22,ease:'power2.out'},t-.35);
   tl.fromTo(state,{scale:1},{scale:95,duration:.95,ease:'expo.in',onUpdate:update},t);
   tl.set(pic,{clipPath:'inset(0% 0% 0% 0%)'},t+.95);
  }else if(s.transition==='iris'){
   const origin=`${s.origin[0]/16}% ${s.origin[1]/9}%`;
   tl.fromTo(pic,{clipPath:`circle(0% at ${origin})`},{clipPath:`circle(145% at ${origin})`,duration:.92,ease:'power3.inOut'},t);
  }else if(s.transition==='grid'){
   for(let i=0;i<5;i++)tl.fromTo(`#grid-reveal-${i}`,{attr:{height:0}},{attr:{height:900},duration:.65,ease:'power3.inOut'},t+i*.065);
   tl.set(pic,{clipPath:'inset(0% 0% 0% 0%)'},t+.94);
  }else if(s.transition==='line'){
   tl.fromTo(pic,{clipPath:'inset(64% 100% 35.8% 0% round 30px)'},{clipPath:'inset(0% 0% 0% 0% round 0px)',duration:.94,ease:'power3.inOut'},t);
  }else if(s.transition==='document'){
   tl.fromTo(pic,{clipPath:'inset(58.33% 10.94% 23.89% 76.88% round 8px)'},{clipPath:'inset(0% 0% 0% 0% round 0px)',duration:.94,ease:'power3.inOut'},t);
  }else if(s.transition==='shutter'){
   tl.fromTo(pic,{clipPath:'inset(0% 50% 0% 50%)'},{clipPath:'inset(0% 0% 0% 0%)',duration:.95,ease:'power3.inOut'},t);
  }
  const content=t+(index?.8:.35);
  if(document.querySelector(q(s.id,'.line>span')))tl.fromTo(q(s.id,'.line>span'),{yPercent:112},{yPercent:0,duration:.8,stagger:.13,ease:'power4.out'},content);
  const support=document.querySelector(q(s.id,'.support'));
  if(support)tl.fromTo(support,{clipPath:'inset(100% 0% 0% 0%)',y:13},{clipPath:'inset(0% 0% 0% 0%)',y:0,duration:.65,ease:'power3.out'},content+.4);
  if(s.time)tl.fromTo(q(s.id,'.scene-time'),{y:-20,opacity:0},{y:0,opacity:1,duration:.5},content);
  if(index){tl.to('.brand-mark',{color:s.tone==='light'?'#0b3f29':'#f7d667',duration:.18},t+.6);tl.to('.demo-footer',{color:s.tone==='light'?'#52645a':'#c3d3c7',duration:.18},t+.6)}
 });
 tl.fromTo('.opening-lockup',{y:35,opacity:0},{y:0,opacity:1,duration:.9,ease:'power3.out'},.15);
 tl.fromTo('.identity-baseline',{clipPath:'inset(0% 100% 0% 0%)'},{clipPath:'inset(0% 0% 0% 0%)',duration:1,ease:'power3.inOut'},1.1);
 tl.fromTo('.brand-ribbon>span',{y:22,opacity:0},{y:0,opacity:1,duration:.55,stagger:.12},1.6);
 tl.fromTo('.brand-ribbon>i',{scaleX:0},{scaleX:1,duration:.6,stagger:.1},1.8);
 tl.fromTo('.identity-orbit',{scale:.75,rotation:-12},{scale:1.15,rotation:12,duration:5.45,ease:'none'},0);
 tl.fromTo('.brand-mark',{opacity:0},{opacity:1,duration:.4},5.4);
 tl.fromTo('.staff-person',{scale:.3,y:25,opacity:0},{scale:1,y:0,opacity:1,stagger:{each:.033,from:'end'},duration:.5,ease:'back.out(1.2)'},5.4);
 tl.fromTo('.staff-person.missing',{scale:1},{scale:1.13,duration:.6,repeat:1,yoyo:true},8.2);
 tl.fromTo('.staff-legend,.staff-source,.staff-rail',{opacity:0,y:15},{opacity:1,y:0,duration:.55,stagger:.1},6.4);
 tl.to('.staff-viz',{scale:1.07,transformOrigin:'50% 50%',duration:3.6,ease:'sine.inOut'},6.6);
 tl.fromTo('.scan-surface',{opacity:0,scale:.82},{opacity:1,scale:1,duration:.65},11.65);
 document.querySelectorAll('.finger-line').forEach((p,i)=>{const len=p.getTotalLength();tl.fromTo(p,{strokeDasharray:len,strokeDashoffset:len},{strokeDashoffset:0,duration:1.5,ease:'power2.inOut'},11.8+i*.06)});
 tl.fromTo('.scan-beam',{y:0},{y:215,duration:1.05,repeat:3,yoyo:true,ease:'sine.inOut'},12.2);
 tl.fromTo('.arrival-flow>div',{y:24,opacity:0},{y:0,opacity:1,duration:.6,stagger:.5},13.1);
 tl.fromTo('.flow-line',{scaleX:0},{scaleX:1,duration:.9,ease:'power3.inOut'},13.4);
 tl.fromTo('.truth-note',{opacity:0},{opacity:1,duration:.5},14.3);
 tl.fromTo('.assembly-ground-label',{opacity:0,y:22},{opacity:1,y:0,duration:.6},18.6);
 tl.to('.assembly-type',{y:-70,opacity:0,duration:.6,ease:'power2.in'},21.8);
 tl.fromTo('.assembly-grid-guide',{opacity:0,scale:.8},{opacity:.4,scale:1,duration:.7},22.1);
 tl.fromTo('.clock-wrap',{rotation:-25,scale:.75,opacity:0},{rotation:0,scale:1,opacity:1,duration:1,ease:'power3.out'},24.0);
 tl.fromTo('.clock-wrap .minute-hand',{rotation:-90},{rotation:180,duration:2.0,ease:'power2.inOut'},24.4);
 tl.fromTo('.clock-wrap .hour-hand',{rotation:0},{rotation:55,duration:2.0,ease:'power2.inOut'},24.4);
 tl.to('.clock-wrap',{scale:1.8,opacity:0,duration:.7,ease:'power3.in'},26.05);
 tl.to('.schedule-title',{y:-135,scale:.38,transformOrigin:'right top',duration:.75,ease:'power3.inOut'},26.1);
 tl.fromTo('.schedule-board',{opacity:1,clipPath:'inset(0% 50% 100% 50% round 20px)'},{opacity:1,clipPath:'inset(0% 0% 0% 0% round 0px)',duration:.85,ease:'power3.inOut'},26.2);
 tl.fromTo('.lesson-tile',{y:12,opacity:0},{y:0,opacity:1,duration:.4,stagger:.025},26.65);
 tl.fromTo('.student-attendance',{clipPath:'inset(0% 100% 0% 0%)'},{clipPath:'inset(0% 0% 0% 0%)',duration:.8,ease:'power3.inOut'},32.5);
 tl.fromTo('.attendance-dots>i',{scale:0},{scale:1,duration:.3,stagger:.06,ease:'back.out(1.6)'},33.0);
 tl.fromTo('.observation-tag',{scale:.6,opacity:0},{scale:1,opacity:1,duration:.6,ease:'back.out(1.1)'},34.2);
 tl.to('.observation-tag',{x:125,y:55,scale:1.15,duration:1.6,ease:'power2.inOut'},36.0);
 const connector=document.querySelector('.care-connector'),len=connector.getTotalLength();
 tl.fromTo(connector,{strokeDasharray:len,strokeDashoffset:len},{strokeDashoffset:0,duration:2.1,ease:'power2.inOut'},39.0);
 tl.fromTo('.care-node',{scale:.7,opacity:0,y:15},{scale:1,opacity:1,y:0,duration:.6,stagger:.75,ease:'power3.out'},38.6);
 tl.fromTo('.care-document',{opacity:0,y:40,rotation:-15},{opacity:1,y:0,rotation:0,duration:.6},39.2);
 tl.to('.care-document',{x:390,y:10,rotation:6,duration:1.4,ease:'power3.inOut'},40);
 tl.to('.care-document',{x:1000,y:0,scale:.7,duration:1.4,ease:'power3.inOut'},42);
 tl.fromTo('.departure-clock',{opacity:0,rotation:-45},{opacity:1,rotation:0,duration:.7},44.9);
 tl.fromTo('.departure-clock .minute-hand',{rotation:30},{rotation:270,duration:1.7,ease:'power2.inOut'},45.1);
 tl.fromTo('.permit-stage',{opacity:0,y:16},{opacity:1,y:0,duration:.45,stagger:1.05},45.8);
 tl.fromTo('.exit-line',{scaleX:0},{scaleX:1,duration:.7,stagger:1.05},46.1);
 tl.fromTo('.permit-paper',{opacity:0,rotation:12,scale:.7},{opacity:1,rotation:0,scale:1,duration:.7,ease:'power3.out'},46.9);
 tl.fromTo('.exit-record',{opacity:0,y:20},{opacity:1,y:0,duration:.6,ease:'power3.out'},50.2);
 // Pull back from the document into the archive, then expose the digital index.
 tl.fromTo('.archive-file-list',{opacity:0,x:-30},{opacity:1,x:0,duration:.7,ease:'power3.out'},54.7);
 tl.fromTo('.archive-file-list>div',{clipPath:'inset(0% 100% 0% 0%)'},{clipPath:'inset(0% 0% 0% 0%)',duration:.6,stagger:.22},54.8);
 tl.fromTo('.archive-tools>span',{opacity:0,y:20},{opacity:1,y:0,duration:.5,stagger:.15},55.3);
 tl.fromTo('.archive-permission',{opacity:0},{opacity:1,duration:.5},56.1);
 tl.fromTo('.signature-lockup>img',{scale:.6,rotation:-20,opacity:0},{scale:1,rotation:0,opacity:1,duration:.85,ease:'power3.out'},59.3);
 tl.fromTo('.signature-name',{y:65,clipPath:'inset(100% 0% 0% 0%)'},{y:0,clipPath:'inset(0% 0% 0% 0%)',duration:.85,ease:'power4.out'},59.6);
 tl.fromTo('.signature-lockup>p',{opacity:0,y:15},{opacity:1,y:0,duration:.7},60.3);
 tl.fromTo('.signature-roles>span',{opacity:0,y:20},{opacity:1,y:0,duration:.6,stagger:.16},60.8);
 tl.fromTo('.signature-school',{opacity:0},{opacity:1,duration:.5},61.5);
 tl.fromTo('.signature-rays',{rotation:-10,scale:.85},{rotation:8,scale:1.1,duration:5.2,ease:'none'},58.8);
 tl.to('.brand-mark',{opacity:0,duration:.3},59.5);
 tl.fromTo('.story-progress>i',{scaleX:0},{scaleX:1,duration:64,ease:'none'},0);
 window.__timelines=window.__timelines||{};window.__timelines.main=tl;
});
