// Appended to the shared, procedural school geometry by build-story.cjs.
// Every pose is derived from absolute composition time, including reverse seeks.
const terminal=new THREE.Group();scene.add(terminal);groups.terminal=terminal;
floor(terminal,5.5,4.1);
const reader=new THREE.Group();terminal.add(reader);reader.rotation.y=.18;
box(reader,0,1.35,0,2.45,2.65,.48,'forest',.18);
box(reader,0,1.65,.26,2.04,1.35,.07,'navy',.08);
box(reader,.48,.58,.28,.7,.58,.1,'gold',.07);
box(reader,.48,.58,.35,.47,.37,.035,'forest',.05);
for(let r=0;r<3;r++)for(let c=0;c<3;c++)box(reader,-.73+c*.24,.8-r*.21,.3,.13,.1,.045,'ivory',.025);
label(reader,'سجل الوصول',0,2.02,.315,1.65,.38,'#274d4b','#f7d667');
// The fingerprint is composited as drawn vector strokes over this surface.
const terminalRings=[];
for(let i=0;i<5;i++){
 const ring=new THREE.Mesh(new THREE.TorusGeometry(.12+i*.055,.009,6,48,Math.PI*1.7),mat.gold);
 ring.position.set(0,1.55,.34);reader.add(ring);ring.visible=false;terminalRings.push(ring);
}
// More pupils form three rows in the courtyard; they settle into the assembly.
const courtyard=actors.filter(a=>a.g.parent===campus&&a.role==='student');
for(let i=5;i<18;i++){const p=person(campus,0,0,'student',.66,Math.PI);courtyard.push(actors.find(a=>a.g===p))}
courtyard.forEach((a,i)=>{a.x=-3.4+(i%6)*1.15;a.z=3.2+Math.floor(i/6)*1.05;a.g.position.set(a.x,0,a.z)});
const initialActors=actors.map(a=>({a,y:a.g.position.y,ry:a.g.rotation.y}));
const shots=window.__storyScenes.filter(s=>s.view).map(s=>({...s,canvas:document.getElementById(`canvas-${s.id}`)}));
const clamp=x=>Math.max(0,Math.min(1,x));
const ease=x=>{const u=clamp(x);return u*u*(3-2*u)};
const mix=(a,b,t)=>a+(b-a)*ease(t);
const poses={
 terminal:{from:[4,3,9],to:[2.6,2.5,8],look:[0,1.25,0],offset:300},
 school:{from:[-17,8,24],to:[-12,5.3,23],look:[1.1,1.8,1],offset:360},
 classroom:{from:[1.6,3.8,16.8],to:[-5.3,6.3,17.8],look:[0,.75,-.3],offset:310},
 guidance:{from:[-8.5,6.7,15.7],to:[-3.7,4.8,14.8],look:[0,1.2,-.3],offset:310},
 security:{from:[9,3.8,16],to:[-4.7,4.8,15.5],look:[.2,1.1,0],offset:410},
 storage:{from:[10,8,13],to:[14,11,17],look:[0,.9,.5],offset:0}
};
function renderShot(shot,time){
 for(const [name,g]of Object.entries(groups))g.visible=name===shot.view;
 const local=time-shot.start,p=poses[shot.view],duration=shot.end-shot.start;
 let u=clamp(local/(duration-.8));
 camera.position.set(...p.from.map((v,i)=>mix(v,p.to[i],u)));
 let look=p.look.slice(),offset=p.offset;
 if(shot.view==='school'){
  const overhead=ease((local-3.3)/2.5);
  camera.position.lerp(new THREE.Vector3(.01,31,6),overhead);
  look=[mix(1.1,0,overhead),mix(1.8,0,overhead),mix(1,1.8,overhead)];
 }
 camera.setViewOffset(1600,900,offset,0,1600,900);camera.lookAt(...look);camera.updateProjectionMatrix();
 key.position.set(mix(-9,8,time/64),19,11);key.intensity=shot.view==='terminal'?5:4.2;
 for(const {a,y,ry}of initialActors){
  a.g.position.set(a.x,y,a.z);a.g.rotation.y=ry;
  a.left.rotation.z=a.role==='teacher'?-.42-Math.sin(local*.9)*.09:.04;
  a.right.rotation.x=a.role==='guide'?-.28-Math.sin(local*1.2)*.12:.03;
 }
 if(shot.view==='terminal'){
  reader.rotation.y=mix(.4,-.16,u);terminalRings.forEach((r,i)=>r.rotation.z=local*.15+i*.22);
 }
 if(shot.view==='school')for(let i=0;i<courtyard.length;i++){
  const a=courtyard[i],walk=1-ease((local-i*.035)/2.8);
  a.g.position.z=a.z+walk*(1.6+(i%3)*.22);a.left.rotation.x=walk*Math.sin(local*6+i)*.3;a.right.rotation.x=-a.left.rotation.x;
 }
 if(shot.view==='classroom'){
  emptyRing.scale.setScalar(1+.05*Math.sin(local*2));minute.rotation.z=-local*.11;
  const teacher=actors.find(a=>a.g.parent===classroom&&a.role==='teacher');
  teacher.right.rotation.z=-.7*ease((local-1)/1.4);teacher.g.rotation.y=mix(0,.6,u);
 }
 if(shot.view==='guidance'){
  const transfer=ease((local-1.2)/2.1);handoff.position.set(mix(2.8,.2,transfer),1.24+Math.sin(transfer*Math.PI)*.45,.15);handoff.rotation.y=transfer*.35;
 }
 if(shot.view==='security'){
  gate.rotation.y=-1.3*ease((local-3.9)/1.0);
  const walk=ease((local-4.7)/1.8);leavingStudent.position.z=mix(-1.8,2.3,walk);
  guardian.position.z=mix(3.2,3.7,walk);guard.rotation.y=mix(-.5,-1.1,local/4);
  const a=actors.find(a=>a.g===leavingStudent);a.left.rotation.x=Math.sin(local*7)*.3*Math.sin(walk*Math.PI);a.right.rotation.x=-a.left.rotation.x;
 }
 if(shot.view==='storage')papers.forEach((paper,i)=>{
  const drop=ease((local-.6-i*.3)/1.7);paper.position.set(-.65+i*.65,mix(2+i*.3,.3+i*.1,drop),0);paper.rotation.z=mix(-.3+i*.15,0,drop);
 });
 renderer.render(scene,camera);
 const ctx=shot.canvas.getContext('2d');ctx.clearRect(0,0,1600,900);ctx.drawImage(renderer.domElement,0,0);
}
function renderAt(time){for(const shot of shots)if(time>=shot.start&&time<shot.end)renderShot(shot,time)}
window.addEventListener('hf-seek',event=>renderAt(event.detail.time));
window.__renderStoryAt=renderAt;
renderAt(0);window.__resolveStory();
