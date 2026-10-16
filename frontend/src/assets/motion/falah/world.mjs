import * as THREE from './assets/three/three.module.js';
import { RoundedBoxGeometry } from './assets/three/RoundedBoxGeometry.js';
export async function createWorld(root,window,document){
// Original, procedural educational diorama. No external models or student imagery.
// A single offscreen WebGL renderer paints only the active HyperFrames shots.

await document.fonts.ready;if(window.__afDisposed)return;
const renderer=new THREE.WebGLRenderer({antialias:true,alpha:true,preserveDrawingBuffer:true});
renderer.setSize(1160,680,false);renderer.setPixelRatio(1);
renderer.outputColorSpace=THREE.SRGBColorSpace;
renderer.toneMapping=THREE.ACESFilmicToneMapping;renderer.toneMappingExposure=1.05;
renderer.shadowMap.enabled=true;renderer.shadowMap.type=THREE.PCFSoftShadowMap;
const scene=new THREE.Scene();
const camera=new THREE.PerspectiveCamera(32,1160/680,.1,130);
scene.add(new THREE.HemisphereLight('#f5f9ed','#34694a',2.8));
const key=new THREE.DirectionalLight('#fff1d2',4.5);key.position.set(-9,19,11);key.castShadow=true;
key.shadow.mapSize.set(1024,1024);key.shadow.camera.left=-16;key.shadow.camera.right=16;key.shadow.camera.top=15;key.shadow.camera.bottom=-15;key.shadow.normalBias=.035;key.shadow.bias=-.0001;key.shadow.radius=4;scene.add(key);
const fill=new THREE.DirectionalLight('#d2f5df',2);fill.position.set(13,8,-10);scene.add(fill);
const mat={};
for(const [name,color] of Object.entries({ivory:'#eee9da',white:'#f8faf5',forest:'#0b3f29',green:'#1e8e4e',sage:'#a5c6ad',mint:'#d8eee1',gold:'#d3ac38',darkGold:'#977b29',wood:'#d1ad71',paper:'#f6f2e7',skin:'#b98461',navy:'#274d4b',glass:'#76a98c',floor:'#adcbb2',ink:'#344c3b'}))mat[name]=new THREE.MeshStandardMaterial({color,roughness:name==='gold'?.25:.65,metalness:name==='gold'?.65:.03});
const geometryCache=new Map();
function box(parent,x,y,z,w,h,d,material='ivory',radius=.05){
 const k=[w,h,d,radius].join('/');
 if(!geometryCache.has(k))geometryCache.set(k,new RoundedBoxGeometry(w,h,d,2,Math.min(radius,w/4,h/4,d/4)));
 const m=new THREE.Mesh(geometryCache.get(k),typeof material==='string'?mat[material]:material);m.position.set(x,y,z);m.castShadow=true;m.receiveShadow=true;parent.add(m);return m;
}
function cylinder(parent,x,y,z,r,h,material='gold',rTop=r){const m=new THREE.Mesh(new THREE.CylinderGeometry(rTop,r,h,20),mat[material]);m.position.set(x,y,z);m.castShadow=true;m.receiveShadow=true;parent.add(m);return m}
function sphere(parent,x,y,z,r,material='skin'){const m=new THREE.Mesh(new THREE.SphereGeometry(r,20,16),mat[material]);m.position.set(x,y,z);m.castShadow=true;parent.add(m);return m}
function label(parent,text,x,y,z,w=2.4,h=.5,bg='#0b3f29',color='#f7d667'){
 const cv=document.createElement('canvas');cv.width=768;cv.height=192;const ctx=cv.getContext('2d');ctx.fillStyle=bg;ctx.fillRect(0,0,768,192);ctx.direction='rtl';ctx.textAlign='center';ctx.textBaseline='middle';ctx.font='700 65px FalahMotion';ctx.fillStyle=color;ctx.fillText(text,384,105,730);
 const tx=new THREE.CanvasTexture(cv);tx.colorSpace=THREE.SRGBColorSpace;tx.anisotropy=4;
 const mesh=new THREE.Mesh(new THREE.PlaneGeometry(w,h),new THREE.MeshBasicMaterial({map:tx,side:THREE.DoubleSide}));mesh.position.set(x,y,z);parent.add(mesh);return mesh;
}
const campus=new THREE.Group();scene.add(campus);
const classroom=new THREE.Group();scene.add(classroom);
const guidance=new THREE.Group();scene.add(guidance);
const security=new THREE.Group();scene.add(security);
const storage=new THREE.Group();scene.add(storage);
const groups={school:campus,classroom,guidance,security,storage};
const actors=[];
function person(parent,x,z,role='student',scale=1,rotation=0){
 const g=new THREE.Group();g.position.set(x,0,z);g.scale.setScalar(scale);g.rotation.y=rotation;parent.add(g);
 const isStudent=role==='student',uniform=role==='guard'?'navy':role==='teacher'?'forest':role==='guide'?'sage':'white';
 // Feet, legs, articulated arms, rounded torso, and a sculpted head.
 box(g,-.15,.06,.03,.2,.12,.39,'forest');box(g,.15,.06,.03,.2,.12,.39,'forest');
 cylinder(g,-.14,.36,0,.09,.5,isStudent?'forest':'white');cylinder(g,.14,.36,0,.09,.5,isStudent?'forest':'white');
 box(g,0,.89,0,.54,.67,.34,uniform,.13);cylinder(g,0,1.28,0,.09,.18,'skin');sphere(g,0,1.54,0,.235);
 if(role==='guard'){box(g,0,1.73,.01,.5,.1,.43,'navy');box(g,.13,1.03,.18,.07,.11,.02,'gold');}
 else if(!isStudent){box(g,0,1.73,-.01,.47,.08,.43,'white');}
 else{const hair=sphere(g,0,1.64,-.025,.21,'ink');hair.scale.y=.63;box(g,0,.94,-.23,.34,.43,.17,'gold',.09)}
 const left=new THREE.Group();left.position.set(-.36,1.1,0);g.add(left);cylinder(left,0,-.2,0,.075,.43,uniform);sphere(left,0,-.46,0,.085);
 const right=new THREE.Group();right.position.set(.36,1.1,0);g.add(right);cylinder(right,0,-.2,0,.075,.43,uniform);sphere(right,0,-.46,0,.085);
 if(role!=='student'){const tablet=box(g,.25,.88,.29,.23,.31,.025,'forest',.025);tablet.rotation.x=-.25;box(g,.25,.89,.308,.17,.22,.008,'mint',.01)}
 actors.push({g,left,right,role,x,z});return g;
}
function floor(parent,w=11,d=7){box(parent,0,-.24,0,w+.8,.43,d+.8,'forest',.15);box(parent,0,.015,0,w,.09,d,'ivory',.06);box(parent,0,-.04,d/2,w,.05,.07,'gold',.015)}
function tree(parent,x,z,scale=1){const g=new THREE.Group();parent.add(g);g.position.set(x,0,z);g.scale.setScalar(scale);cylinder(g,0,.65,0,.13,1.3,'wood');sphere(g,0,1.65,0,.72,'green');sphere(g,.3,1.9,-.1,.52,'sage');cylinder(g,0,.13,0,.57,.26,'ivory');return g}
function bench(parent,x,z){box(parent,x,.48,z,1.7,.12,.55,'wood');box(parent,x,.9,z-.24,1.7,.65,.1,'wood');for(const dx of [-.6,.6])box(parent,x+dx,.23,z,.08,.43,.45,'forest')}
function desk(parent,x,z,student=false){box(parent,x,student?.83:1.0,z,student?1.1:2.2,.11,student?.67:1,'wood');for(const dx of (student?[-.4,.4]:[-.9,.9]))for(const dz of [-.24,.24])box(parent,x+dx,.43,z+dz,.055,.8,.055,'forest',.01)}
function chair(parent,x,z){box(parent,x,.49,z,.54,.1,.5,'sage');box(parent,x,.79,z-.24,.54,.57,.08,'sage');for(const dx of [-.2,.2])for(const dz of [-.16,.16])box(parent,x+dx,.24,z+dz,.04,.44,.04,'forest',.01)}

// School exterior: colonnade, raised sign, window rhythm, courtyard, gate and palms.
floor(campus,20,13);
box(campus,0,1.8,-2.3,15.8,3.5,5.2,'ivory',.15);
box(campus,0,3.75,-2.3,16.2,.28,5.6,'forest',.09);
box(campus,0,3.95,-2.3,16.4,.08,5.7,'gold',.025);
box(campus,0,2.4,.5,4,4.7,1.25,'white',.12);
box(campus,0,4.84,.52,4.3,.23,1.5,'forest');
label(campus,'متوسطة الفلاح الأهلية',0,3.9,1.15,3.8,.8);
box(campus,0,1.35,1.2,1.65,2.55,.1,'forest');box(campus,0,1.35,1.27,.025,2.4,.025,'gold');
for(const x of [-6.7,-4.65,-2.6,2.6,4.65,6.7]){box(campus,x,2.2,.335,1.25,1.7,.11,'forest');box(campus,x,2.22,.41,1.03,1.48,.055,'glass');box(campus,x,2.22,.46,.035,1.49,.02,'gold');box(campus,x,2.22,.46,1.04,.035,.02,'gold');}
for(const x of [-7.4,-5,-2.7,2.7,5,7.4]){cylinder(campus,x,1.4,1.12,.12,2.8,'white');box(campus,x,2.8,1.12,.43,.17,.43,'gold')}
box(campus,0,2.95,1.1,16.2,.23,1.5,'forest');
for(let i=0;i<7;i++)box(campus,0,.06,2.05+i*.52,1.5,.05,.32,i%2?'mint':'paper',.015);
box(campus,0,.065,4.2,16,.025,.06,'gold',.01);
tree(campus,-8.6,3.9,1.2);tree(campus,8.6,2.3,1.25);tree(campus,-8.6,-4.5,.95);tree(campus,8.6,-4.5,.95);
bench(campus,-5,3);bench(campus,5,3);
cylinder(campus,-3.2,2.6,2.6,.035,5.2,'gold');box(campus,-2.72,4.65,2.6,.88,.58,.02,'forest');
for(let i=0;i<5;i++)person(campus,-2.2+i*1.05,4.8+(i%2)*.4,'student',.66,Math.PI);
person(campus,2.9,2.2,'teacher',.85);person(campus,7,4.8,'guard',.85);

// Classroom cutaway: board, wall clock, teacher, six pupils, and one empty desk.
floor(classroom,11,7);
box(classroom,0,1.65,-3.2,11.3,3.3,.22,'ivory');box(classroom,-5.5,.65,0,.2,1.3,6.5,'ivory');
box(classroom,-.6,2.0,-3.04,5.7,1.8,.12,'forest');box(classroom,-.6,2.02,-2.96,5.45,1.57,.02,'green');
label(classroom,'الفصل الدراسي',-.6,2.1,-2.92,3.9,.72,'#1e8e4e','#f8faf8');
for(const x of [-4.5,4.5]){box(classroom,x,2.1,-3.02,1.1,1.5,.09,'forest');box(classroom,x,2.1,-2.95,.92,1.3,.06,'mint')}
const clock=new THREE.Group();clock.position.set(3.25,2.6,-2.95);classroom.add(clock);const disk=cylinder(clock,0,0,0,.38,.05,'gold');disk.rotation.x=Math.PI/2;const face=cylinder(clock,0,0,.04,.33,.02,'white');face.rotation.x=Math.PI/2;
const minute=box(clock,0,.13,.06,.018,.25,.02,'forest',.005);const hour=box(clock,.08,0,.06,.17,.025,.02,'forest',.005);
desk(classroom,-3.6,-1.9);person(classroom,-2.15,-1.8,'teacher',1.05);
for(let row=0;row<2;row++)for(let col=0;col<3;col++){const x=-2.8+col*2.7,z=.15+row*2.1;desk(classroom,x,z,true);chair(classroom,x,z+.75);box(classroom,x,.91,z,.5,.035,.38,'paper',.02);if(!(row===1&&col===2)){const p=person(classroom,x,z+.68,'student',.75,Math.PI);p.position.y=.15}}
const emptyRing=new THREE.Mesh(new THREE.TorusGeometry(.47,.028,10,48),mat.gold);emptyRing.rotation.x=-Math.PI/2;emptyRing.position.set(2.6,.1,2.9);classroom.add(emptyRing);

// Guidance office: a teacher handoff, student-facing conversation, guardian message.
floor(guidance,10,7);
box(guidance,0,1.6,-3.15,10.3,3.2,.2,'ivory');box(guidance,-5,.65,0,.2,1.3,6.5,'ivory');
label(guidance,'الموجّه الطلابي',0,2.55,-3.025,3.8,.72);
desk(guidance,-.2,-.3);chair(guidance,-.2,-1.35);chair(guidance,-.9,1);chair(guidance,1,1);
person(guidance,-.2,-1.4,'guide',1.02);person(guidance,-.9,1.1,'student',.82,Math.PI);person(guidance,3.1,.1,'teacher',1,Math.PI/3);
box(guidance,.15,1.11,-.3,.75,.04,.52,'paper');box(guidance,-.7,1.36,-.45,.59,.42,.06,'forest');box(guidance,-.7,1.37,-.407,.51,.33,.01,'mint');
tree(guidance,-4,1.8,.85);box(guidance,3.65,1.15,-2.75,1.55,2.3,.5,'forest');for(const y of [.35,.95,1.55]){box(guidance,3.65,y,-2.4,1.4,.05,.5,'gold');for(let j=0;j<4;j++)box(guidance,3.15+j*.3,y+.24,-2.47,.2,.42,.3,j%2?'paper':'sage')}
const handoff=box(guidance,2.8,1.24,.15,.42,.025,.3,'gold',.025);

// Security gate: physically checked pickup, then a recorded crossing.
floor(security,12,8);
box(security,-3,1.4,-1,2.8,2.8,2.5,'ivory');box(security,-3,2.91,-1,3.15,.2,2.8,'forest');box(security,-3,1.7,.29,1.8,.85,.07,'glass');label(security,'الحارس',-3,2.35,.32,1.8,.4);
for(const x of [-.8,4.5])box(security,x,1.5,-.2,.4,3,.5,'ivory');
box(security,1.85,3.1,-.2,5.8,.24,.7,'forest');label(security,'بوابة المدرسة',1.85,3.1,.18,3.5,.47);
const gate=new THREE.Group();gate.position.set(-.6,0,-.2);security.add(gate);for(let i=0;i<10;i++)box(gate,i*.49,1.35,0,.055,2.7,.055,'forest',.01);box(gate,2.2,.3,0,4.5,.055,.055,'gold',.01);box(gate,2.2,2.5,0,4.5,.055,.055,'gold',.01);
for(const x of [-5,5.5])tree(security,x,2.8,.8);
const guard=person(security,-1.6,1.4,'guard',1.05,-.5);
const leavingStudent=person(security,1.3,-1.8,'student',.84,0);
const guardian=person(security,2.8,3.2,'guide',1.02,Math.PI);
box(security,-1.4,1.03,1.1,.5,.6,.3,'forest');box(security,-1.4,1.39,1.15,.4,.24,.03,'mint');

// Storage: a physical archive resolves into organized digital files.
floor(storage,10,7);
box(storage,0,1.6,-3.1,10.3,3.2,.2,'ivory');
label(storage,'مكتبة المدرسة',0,2.55,-2.97,3.6,.7);
const shelves=[];
for(const x of [-3,0,3]){box(storage,x,1.15,-1.3,2.4,2.3,1.2,'forest');for(const y of [.28,1.15,2.08])box(storage,x,y,-.68,2.3,.08,1.13,'gold');for(let row=0;row<2;row++)for(let i=0;i<6;i++){const binder=box(storage,x-.9+i*.36,.68+row*.86,-.7,.25,.65,.8,(i+row)%3===0?'gold':(i+row)%3===1?'paper':'sage',.035);box(storage,x-.9+i*.36,.7+row*.86,-.286,.12,.19,.013,'forest',.01);shelves.push(binder)}}
const tray=new THREE.Group();storage.add(tray);tray.position.set(0,.2,2);box(tray,0,.08,0,4,.16,2.2,'forest');box(tray,0,.14,1.05,4,.15,.13,'gold');
const papers=[];for(let i=0;i<3;i++){const paper=new THREE.Group();paper.position.set(-.65+i*.65,.3+i*.1,0);tray.add(paper);box(paper,0,0,0,1.05,.035,1.5,'paper',.035);for(let n=0;n<4;n++)box(paper,-.08,.025,-.45+n*.22,.65,.009,.035,n===0?'gold':'sage',.003);papers.push(paper)}
person(storage,-4,1.5,'teacher',.96,.8);


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

}
