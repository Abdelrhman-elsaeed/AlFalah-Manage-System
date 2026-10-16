// Appended to the approved procedural geometry; native short-section cameras.
const terminal=new THREE.Group();scene.add(terminal);groups.terminal=terminal;
floor(terminal,4.6,3.2);box(terminal,0,1.2,0,2.1,2.35,.45,'forest',.16);box(terminal,0,1.5,.25,1.75,1.25,.06,'navy',.07);
label(terminal,'سجل الوصول',0,1.91,.29,1.4,.3,'#274d4b','#f7d667');
for(let r=0;r<3;r++)for(let c=0;c<3;c++)box(terminal,-.62+c*.23,.73-r*.19,.28,.12,.09,.04,'ivory',.02);
box(terminal,.43,.48,.26,.65,.5,.1,'gold',.06);box(terminal,.43,.48,.33,.43,.33,.03,'forest',.04);
const courtyard=actors.filter(a=>a.g.parent===campus&&a.role==='student');
for(let i=5;i<15;i++){const g=person(campus,0,0,'student',.66,Math.PI);courtyard.push(actors.find(a=>a.g===g))}
courtyard.forEach((a,i)=>{a.x=-2.8+(i%5)*1.2;a.z=3+Math.floor(i/5)*1.1});
const initial=actors.map(a=>({a,y:a.g.position.y,ry:a.g.rotation.y}));
const shots=window.__afScenes.filter(s=>s.view).map(s=>({...s,canvas:document.getElementById(`af-canvas-${s.id}`)}));
const clamp=x=>Math.max(0,Math.min(1,x)),ease=x=>{let u=clamp(x);return u*u*(3-2*u)},mix=(a,b,t)=>a+(b-a)*ease(t);
const frames={
 terminal:{from:[4,3.1,8],to:[2.3,2.6,7.6],look:[0,1.1,0]},
 school:{from:[-18,8.5,25],to:[-4,27,15],look:[0,.7,0]},
 classroom:{from:[1.5,5.1,15],to:[-6.5,8.2,15.8],look:[0,.4,-.2]},
 guidance:{from:[-8,6.7,14.7],to:[-3.2,4.7,14.2],look:[0,.7,-.4]},
 security:{from:[8.8,4,17],to:[-5.2,5.3,16.4],look:[.1,.8,0]},
 storage:{from:[11,8,13.8],to:[14,10,16],look:[0,.8,0]}
};
let currentTime=0,activeSize='';
function renderShot(shot,time){
 const w=Math.max(2,Math.round(shot.canvas.clientWidth)),h=Math.max(2,Math.round(shot.canvas.clientHeight));
 const ratio=Math.min(devicePixelRatio||1,1.4),rw=Math.round(w*ratio),rh=Math.round(h*ratio),keySize=`${rw}x${rh}`;
 if(activeSize!==keySize){renderer.setSize(rw,rh,false);activeSize=keySize}
 if(shot.canvas.width!==rw||shot.canvas.height!==rh){shot.canvas.width=rw;shot.canvas.height=rh}
 camera.aspect=w/h;camera.clearViewOffset();camera.updateProjectionMatrix();
 for(const[name,g]of Object.entries(groups))g.visible=name===shot.view;
 const local=time-shot.start,u=clamp(local/(shot.end-shot.start-.5)),p=frames[shot.view];
 camera.position.set(...p.from.map((v,i)=>mix(v,p.to[i],u)));camera.lookAt(...p.look);
 key.position.set(mix(-9,8,time/47),19,11);
 for(const{a,y,ry}of initial){a.g.position.set(a.x,y,a.z);a.g.rotation.y=ry;a.left.rotation.z=a.role==='teacher'?-.4-Math.sin(local*.9)*.07:.04;a.right.rotation.x=a.role==='guide'?-.3-Math.sin(local)*.1:.04}
 if(shot.view==='terminal')terminal.rotation.y=mix(.12,-.13,u);
 if(shot.view==='school')courtyard.forEach((a,i)=>{const walk=1-ease((local-i*.03)/1.7);a.g.position.z=a.z+walk*1.4;a.left.rotation.x=walk*Math.sin(local*7+i)*.3;a.right.rotation.x=-a.left.rotation.x});
 if(shot.view==='classroom'){emptyRing.scale.setScalar(1+.05*Math.sin(local*2));minute.rotation.z=-local*.12}
 if(shot.view==='guidance'){const v=ease((local-.7)/1.6);handoff.position.set(mix(2.8,.2,v),1.24+Math.sin(v*Math.PI)*.4,.15);handoff.rotation.y=v*.3}
 if(shot.view==='security'){gate.rotation.y=-1.3*ease((local-2.6)/.8);const walk=ease((local-3.4)/1.2);leavingStudent.position.z=mix(-1.8,2.3,walk);guardian.position.z=mix(3.2,3.7,walk);guard.rotation.y=mix(-.5,-1.1,local/3)}
 if(shot.view==='storage')papers.forEach((p,i)=>{const v=ease((local-.3-i*.2)/1.2);p.position.set(-.65+i*.65,mix(1.8+i*.3,.3+i*.1,v),0);p.rotation.z=mix(-.2+i*.14,0,v)});
 renderer.render(scene,camera);const ctx=shot.canvas.getContext('2d');ctx.clearRect(0,0,rw,rh);ctx.drawImage(renderer.domElement,0,0);
}
function renderAt(time){currentTime=time;for(const shot of shots)if(time>=shot.start&&time<shot.end)renderShot(shot,time)}
window.__afRenderWorld=renderAt;window.addEventListener('hf-seek',e=>renderAt(e.detail.time));
const worldObserver=new ResizeObserver(()=>renderAt(currentTime));worldObserver.observe(document.getElementById('falah-section'));(window.__afObservers||=[]).push(worldObserver);
window.__afDisposeWorld=()=>{worldObserver.disconnect();const resources=new Set();scene.traverse(object=>{if(object.geometry)resources.add(object.geometry);for(const material of (Array.isArray(object.material)?object.material:[object.material]))if(material){resources.add(material);for(const value of Object.values(material))if(value?.isTexture)resources.add(value)}});resources.forEach(r=>r.dispose());renderer.dispose();renderer.forceContextLoss()};
document.getElementById('falah-section').classList.add('af-has-world');window.__afWorldReady=true;renderAt(window.__afCurrentTime||0);window.__resolveAfWorld?.();
