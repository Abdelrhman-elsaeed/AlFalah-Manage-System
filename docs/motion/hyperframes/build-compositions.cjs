const fs=require('node:fs');
const path=require('node:path');
const root=__dirname;
const common=`
@font-face{font-family:Tajawal;src:url('assets/Tajawal-Regular.ttf');font-weight:400;font-display:block}
@font-face{font-family:Tajawal;src:url('assets/Tajawal-Medium.ttf');font-weight:500;font-display:block}
@font-face{font-family:Tajawal;src:url('assets/Tajawal-Bold.ttf');font-weight:700;font-display:block}
@font-face{font-family:Tajawal;src:url('assets/Tajawal-ExtraBold.ttf');font-weight:800;font-display:block}
*{box-sizing:border-box}html,body{margin:0;width:100%;height:100%;overflow:hidden}body{font-family:Tajawal,sans-serif;color:#f8faf8;background:#0b3f29}h1,h2,h3,p{margin:0}#root{position:relative;width:100%;height:100%;overflow:hidden}.clip{position:absolute;inset:0}.scene{position:absolute;inset:0;overflow:hidden;background:radial-gradient(ellipse at 28% 48%,#12563a 0%,#0b3f29 40%,#062c20 100%)}
.ambient{position:absolute;inset:-180px;background:radial-gradient(ellipse at 30% 34%,rgba(85,185,120,.16),transparent 47%),radial-gradient(ellipse at 76% 76%,rgba(224,179,27,.08),transparent 46%);pointer-events:none}
.orbit{position:absolute;width:1120px;height:1120px;border:1px solid rgba(247,214,103,.11);border-radius:50%;left:-280px;top:-150px}.orbit:before,.orbit:after{content:'';position:absolute;inset:85px;border:1px solid rgba(247,214,103,.075);border-radius:50%}.orbit:after{inset:170px}
.horizon{position:absolute;left:90px;right:90px;bottom:105px;height:1px;background:linear-gradient(90deg,transparent,rgba(247,214,103,.32),transparent)}
.brand{position:absolute;top:42px;right:64px;display:flex;direction:rtl;align-items:center;gap:15px;z-index:25;color:#f8faf8;font-size:19px;font-weight:700}.brand img{width:48px;height:48px;object-fit:contain;padding:6px;background:#fff;border-radius:50%}.brand small{display:block;font-weight:400;font-size:14px;color:#c1d2c7;margin-top:5px}
.footer{position:absolute;bottom:35px;left:64px;right:64px;display:flex;align-items:center;justify-content:space-between;direction:rtl;color:#b5ccbf;font-size:16px;z-index:30}.footer .line{width:36px;height:1px;background:#e0b31b;display:inline-block;vertical-align:middle;margin-left:12px}.demo{font-size:14px;color:#b5ccbf}
.caption{position:absolute;right:96px;top:150px;text-align:right;direction:rtl;z-index:12}.caption h2{font-size:54px;line-height:1.28;font-weight:800}.caption p{font-size:23px;line-height:1.65;color:#c1d2c7;margin-top:12px}.caption .gold{color:#f7d667}
.stage{position:absolute;inset:0;perspective:1900px}.world{position:absolute;inset:0;transform-style:preserve-3d;transform-origin:50% 50%;will-change:transform}
.metal{position:absolute;padding:3px;border-radius:19px;background:linear-gradient(140deg,#f7d667 0%,#7b722f 18%,#264f3a 42%,#e0b31b 78%,#6d6020 100%);box-shadow:0 65px 100px rgba(0,15,9,.4),0 20px 30px rgba(0,15,9,.35);transform-style:preserve-3d}
.screen{position:relative;width:100%;height:100%;border-radius:16px;overflow:hidden;background:#f5f4ef;color:#0b3f29}.screen img{display:block;width:100%;height:100%;object-fit:cover;object-position:top}.chrome{height:29px;background:#edf3ee;display:flex;align-items:center;justify-content:space-between;padding:0 16px;font-size:12px;color:#67756d;border-bottom:1px solid #d9e3da;direction:ltr}.dots{display:flex;gap:5px}.dots i{width:5px;height:5px;border-radius:50%;background:#abbeb0}.chrome span{direction:rtl}.screen-shot{width:100%;height:calc(100% - 29px);overflow:hidden}.screen-shot img{height:auto}
.sheen{position:absolute;inset:0;border-radius:16px;background:linear-gradient(113deg,transparent 25%,rgba(255,255,255,.22) 48%,transparent 68%);pointer-events:none;mix-blend-mode:screen}
.chip{display:inline-flex;align-items:center;gap:10px;padding:10px 17px;border:1px solid rgba(247,214,103,.35);background:rgba(247,214,103,.08);border-radius:30px;color:#f7d667;font-size:18px;line-height:1.2}.chip i{width:6px;height:6px;border-radius:50%;background:#f7d667}
.float-card{position:absolute;direction:rtl;background:#f8faf8;color:#0b3f29;border:1px solid #d8eee1;border-radius:14px;padding:24px 28px;box-shadow:0 20px 48px rgba(0,20,12,.25);transform-style:preserve-3d}.float-card h3{font-size:26px;font-weight:800;line-height:1.4}.float-card p{font-size:18px;color:#67756d;line-height:1.5;margin-top:8px}.icon-box{width:48px;height:48px;background:#edf7f1;border:1px solid #d8eee1;border-radius:12px;display:grid;place-items:center;margin-bottom:15px;color:#0f7132}.icon-box svg{width:25px;height:25px;fill:none;stroke:currentColor;stroke-width:1.6;stroke-linecap:round;stroke-linejoin:round}.status{display:inline-flex;align-items:center;gap:8px;font-size:15px;color:#0f7132;background:#edf7f1;border:1px solid #b1d6bd;padding:6px 12px;border-radius:30px}.status:before{content:'✓';font-weight:800}.gold-status{color:#704d00;background:#fbf4d7;border-color:#e4d49b}
.gold-rule{height:2px;width:78px;background:#e0b31b;transform-origin:right center}.beam{position:absolute;top:185px;left:-200px;width:200px;height:650px;opacity:0;background:linear-gradient(90deg,transparent,rgba(247,214,103,.1),transparent);transform:skewX(-25deg);pointer-events:none}
`;
const icon=(type)=>`<div class="icon-box"><svg viewBox="0 0 24 24">${({calendar:'<rect x="3" y="5" width="18" height="16" rx="3"/><path d="M7 3v4m10-4v4M3 11h18m-13 5h2m4 0h2"/>',document:'<path d="M7 3h7l5 5v13H5V3zM14 3v6h5M8 13h8m-8 4h6"/>',shield:'<path d="M12 3l8 4v5c0 5-8 9-8 9s-8-4-8-9V7zM8 12l3 3 5-6"/>',people:'<circle cx="9" cy="8" r="3"/><path d="M3 21v-3a6 6 0 0 1 12 0v3m1-16a3 3 0 0 1 0 6m2 4a5 5 0 0 1 3 5"/>',chart:'<path d="M4 3v17h17M8 15v-4m5 4V7m5 8V4"/>',check:'<path d="M5 12l4 4L20 5"/>'})[type]}</svg></div>`;
const brand=`<div class="brand"><img src="assets/logo.png" width="48" height="48" alt="شعار مدارس الفلاح"><div>متوسطة الفلاح الأهلية<small>مكة المكرمة</small></div></div>`;
const footer=(name)=>`<div class="footer"><span><i class="line"></i>${name}</span><span class="demo">عرض توضيحي • بيانات تجريبية</span></div>`;
const chrome=(title)=>`<div class="chrome"><div class="dots"><i></i><i></i><i></i></div><span>${title}</span><div style="width:20px"></div></div>`;
function composition(name,title,duration,css,body,animation,assets=[]){
 const dir=path.join(root,name); fs.mkdirSync(path.join(dir,'assets'),{recursive:true});
 for(const asset of ['Tajawal-Regular.ttf','Tajawal-Medium.ttf','Tajawal-Bold.ttf','Tajawal-ExtraBold.ttf','gsap.min.js','logo.png',...assets])fs.copyFileSync(path.join(root,'assets',asset),path.join(dir,'assets',asset));
 body=body.replaceAll('class="sheen"','class="sheen" data-layout-allow-overflow').replaceAll('class="opening"','class="opening" data-layout-allow-overflow').replaceAll('src="assets/dashboard.png"','data-layout-allow-overflow src="assets/dashboard.png"').replaceAll('src="assets/timetable.png"','data-layout-allow-overflow src="assets/timetable.png"');
 const fixes=`.chrome{color:#52645a}.closing,.end-message,.actual,.actual-label{opacity:0}.pulse-card.secondary{top:450px}.float-card p,.document p,.review p,.report p,.report small,.lesson small,.schedule-header small{color:#405449}.lesson.accent small{color:#704d00}.schedule{left:100px;top:320px;width:1000px;height:450px}.duty-card{left:1160px}.schedule-grid{grid-template-columns:130px repeat(5,1fr);grid-auto-rows:47px}.status{color:#0b3f29}.actual{height:450px}.actual .screen-shot img{margin-top:-220px}`;
 const html=`<!doctype html><html lang="ar-SA"><head><meta charset="UTF-8"><meta name="viewport" content="width=1600,height=900"><title>${title}</title><script src="assets/gsap.min.js"></script><style>${common}\n${css}\n${fixes}</style></head><body><div id="root" data-composition-id="main" data-width="1600" data-height="900" data-duration="${duration}" data-fps="30"><div id="scene-main" class="clip" data-start="0" data-duration="${duration}" data-track-index="0"><div class="scene"><div class="ambient" data-layout-allow-overflow></div><div class="orbit" data-layout-allow-overflow></div><div class="horizon"></div>${body}${brand}${footer(title)}</div></div></div><script>document.fonts.ready.then(()=>{const tl=gsap.timeline({paused:true,defaults:{ease:'power3.out'}});${animation}\nwindow.__timelines=window.__timelines||{};window.__timelines['main']=tl;});</script></body></html>`;
 fs.writeFileSync(path.join(dir,'index.html'),html);
 fs.writeFileSync(path.join(dir,'hyperframes.json'),JSON.stringify({name:`alfalah-${name}`,width:1600,height:900,fps:30},null,2));
}

composition('pulse','نبض المدرسة',16,`
.opening{position:absolute;inset:0;z-index:14;display:flex;align-items:center;justify-content:center;gap:70px;direction:rtl}.opening-copy{width:650px;text-align:right}.opening h1{font-size:94px;line-height:1.3;font-weight:800;letter-spacing:-2px}.opening p{font-size:29px;color:#c1d2c7;margin-top:14px;line-height:1.7}.opening .gold-rule{margin-bottom:32px}.quill{width:580px;height:430px;object-fit:contain}.quill-wrap{position:relative;width:580px;height:430px}.quill-light{position:absolute;left:70px;top:95px;width:420px;height:220px;border-radius:50%;background:radial-gradient(ellipse,rgba(224,179,27,.16),transparent 70%)}
.product-frame{left:160px;top:250px;width:1280px;height:545px}.product-frame .screen-shot img{width:1280px}.product-frame .screen-shot{height:510px}.headline{right:100px;top:133px}.headline h2{font-size:48px}.headline p{font-size:22px;margin-top:8px}.headline .gold-rule{position:absolute;right:0;top:-18px;width:50px}
.pulse-card{width:290px;top:470px;left:99px;z-index:18}.pulse-card .status{margin-top:15px}.pulse-card.secondary{left:1138px;top:532px;width:324px}.pulse-card.secondary h3{font-size:25px}.pulse-card.secondary .tiny-row{display:flex;align-items:center;justify-content:space-between;margin-top:16px;padding-top:15px;border-top:1px solid #d8eee1;font-size:17px}.pulse-card.secondary .tiny-row b{font-size:28px;color:#0f7132}.intro-small{font-size:21px;color:#f7d667;margin-bottom:22px}
.feature-line{position:absolute;top:190px;left:103px;display:flex;gap:12px;direction:rtl;z-index:12}.feature-line span{font-size:17px;padding:10px 17px;border:1px solid rgba(216,238,225,.24);border-radius:24px;color:#d8eee1}
`, `<div class="opening"><div class="opening-copy"><div class="gold-rule"></div><div class="intro-small">منصة الفلاح التعليمية</div><h1>كل التفاصيل.<br>رؤية واحدة.</h1><p>مدرستك في نظرة واحدة،<br>وقراراتك في المكان الصحيح.</p></div><div class="quill-wrap"><div class="quill-light"></div><img class="quill" src="assets/school-pulse.svg" width="580" height="430" alt="ريشة الفلاح الذهبية"></div></div>
<div class="caption headline"><div class="gold-rule"></div><h2>نبض المدرسة</h2><p>قيادة المدرسة ومتابعة الأداء والإنجاز</p></div><div class="feature-line"><span>الطلاب</span><span>الكادر التعليمي</span><span>الجودة التعليمية</span></div>
<div class="stage"><div class="world"><div class="metal product-frame"><div class="screen">${chrome('لوحة مدير المدرسة')}<div class="screen-shot"><img src="assets/dashboard.png" width="1600" height="1000" alt="لوحة مدير المدرسة الفعلية ببيانات تجريبية"></div><div class="sheen"></div></div></div></div></div>
<div class="float-card pulse-card">${icon('shield')}<h3>قرارات تحتاج انتباهك</h3><p>متابعة المدرسة تبدأ من هنا</p><div class="status gold-status">زيارات بانتظار الاعتماد</div></div>
<div class="float-card pulse-card secondary">${icon('chart')}<h3>أدوات إدارة المدرسة</h3><p>من المتابعة إلى اتخاذ القرار</p><div class="tiny-row"><span>الزيارات الصفية</span><b>12</b></div><div class="tiny-row"><span>خطط التحسين</span><b>3</b></div></div>`, `
tl.addLabel('brand-open',0).addLabel('dashboard-reveal',2.4).addLabel('decision-focus',6.2).addLabel('reading-hold',9.8).addLabel('loop-return',13);
tl.fromTo('.quill-wrap',{y:0,rotation:0},{y:-12,rotation:-2,duration:1.3,ease:'sine.inOut',repeat:1,yoyo:true},0);
tl.fromTo('.opening',{opacity:1,scale:1},{opacity:0,scale:1.05,duration:.75,ease:'power2.inOut'},2.45);
tl.fromTo('.headline',{opacity:0,y:20},{opacity:1,y:0,duration:.8},2.8);
tl.fromTo('.feature-line',{opacity:0,y:15},{opacity:1,y:0,duration:.7},3.1);
tl.fromTo('.world',{rotationX:15,rotationY:-17,scale:.67,y:165},{rotationX:5,rotationY:-5,scale:.91,y:18,duration:2.15,ease:'power4.out'},2.5);
tl.fromTo('.product-frame',{opacity:0},{opacity:1,duration:.85},2.5);
tl.fromTo('.world',{rotationX:5,rotationY:-5,scale:.91,y:18},{rotationX:0,rotationY:0,scale:.87,y:30,duration:3.2,ease:'sine.inOut',immediateRender:false},5.3);
tl.fromTo('.pulse-card',{opacity:0,y:35,rotationY:12},{opacity:1,y:0,rotationY:0,duration:1,stagger:.22},6.3);
tl.fromTo('.sheen',{x:-1000,opacity:0},{x:1000,opacity:.5,duration:2.1,ease:'sine.inOut'},3.4);
tl.fromTo('.pulse-card',{opacity:1,y:0},{opacity:0,y:30,duration:.7,stagger:.08,immediateRender:false},11.8);
tl.to('.world',{rotationX:7,rotationY:9,scale:.8,y:80,duration:1.4,ease:'power2.inOut'},12.4);
tl.to('.product-frame',{opacity:0,duration:.7},13.2);
tl.to(['.headline','.feature-line'],{opacity:0,y:-12,duration:.6},12.8);
tl.fromTo('.opening',{opacity:0,scale:.97},{opacity:1,scale:1,duration:1.1,immediateRender:false},13.8);
tl.fromTo('.ambient',{x:0,y:0},{x:40,y:-20,duration:8,ease:'sine.inOut',repeat:1,yoyo:true},0);
`,['dashboard.png','school-pulse.svg']);

const days=['الأحد','الاثنين','الثلاثاء','الأربعاء','الخميس'];
const lessons=['رياضيات','علوم','لغة عربية','لغة إنجليزية','دراسات إسلامية'];
const grid=`<div class="schedule-grid"><div class="cell corner">اليوم / الحصة</div>${Array.from({length:5},(_,i)=>`<div class="cell col-head">الحصة ${i+1}</div>`).join('')}${days.map((day,d)=>`<div class="cell day-head">${day}</div>${Array.from({length:5},(_,i)=>`<div class="cell lesson ${((i+d)%4===0)?'accent':''}"><span>${lessons[(i+d)%5]}</span><small>${['الأول أ','الثاني أ','الثالث أ'][(i+d)%3]}</small></div>`).join('')}`).join('')}</div>`;
composition('harmony','يوم دراسي بتناغم',16,`
.caption{top:140px;right:95px}.caption h2{font-size:62px}.caption p{max-width:600px}.title-rule{position:absolute;right:96px;top:113px}.schedule{position:absolute;left:180px;top:330px;width:1120px;height:420px;padding:3px}.schedule .screen{padding:26px}.schedule-header{display:flex;direction:rtl;align-items:center;justify-content:space-between;margin-bottom:24px}.schedule-header h3{font-size:28px}.schedule-header small{font-size:17px;color:#67756d;display:block;margin-top:6px}.schedule-grid{display:grid;grid-template-columns:155px repeat(5,1fr);grid-auto-rows:49px;direction:rtl;border:1px solid #cbd2c9;border-radius:9px;overflow:hidden}.cell{padding:8px;border-left:1px solid #d8e3d9;border-bottom:1px solid #d8e3d9;display:flex;justify-content:center;align-items:center;font-size:17px}.col-head,.corner{background:#0b3f29;color:#fff;font-weight:700}.day-head{background:#edf7f1;font-weight:700}.lesson{background:#fff;flex-direction:column;gap:3px}.lesson small{font-size:12px;color:#67756d}.lesson.accent{background:#fbf4d7;color:#704d00}.lesson.accent small{color:#946800}.schedule-header .status{font-size:16px}
.duty-card{left:1090px;top:400px;width:330px;z-index:16}.duty-card .status{margin-top:17px}.duty-card h3{font-size:28px}.duty-days{display:flex;gap:8px;margin-top:20px}.duty-days b{display:grid;place-items:center;width:44px;height:44px;background:#edf7f1;border:1px solid #d8eee1;border-radius:8px;font-size:20px}.duty-days b:nth-child(3){background:#0b3f29;color:#f7d667;border-color:#e0b31b}.calendar-orbit{position:absolute;left:85px;top:152px;font-size:140px;font-weight:800;color:rgba(247,214,103,.1);line-height:1}.agenda-line{position:absolute;left:105px;top:210px;display:flex;gap:10px;direction:rtl}.agenda-line .chip{font-size:17px}.actual{position:absolute;left:180px;top:300px;width:1240px;height:500px}.actual .screen-shot img{width:1240px}.actual-label{position:absolute;top:765px;left:200px;right:200px;text-align:center;font-size:17px;color:#c1d2c7}.closing{position:absolute;inset:0;z-index:20;display:grid;place-content:center;text-align:center;direction:rtl;background:radial-gradient(ellipse at 50% 70%,#12563a,#0b3f29 65%)}.closing h2{font-size:78px;font-weight:800;line-height:1.4}.closing p{font-size:29px;color:#c1d2c7;margin-top:20px}.closing-line{width:84px;height:2px;background:#e0b31b;margin:30px auto 0}
`, `<div class="gold-rule title-rule"></div><div class="caption"><h2>يوم دراسي بتناغم.</h2><p>جدول واضح. مناوبة منظمة. فريق على اطلاع.</p></div><div class="agenda-line"><span class="chip"><i></i>الجدول المدرسي</span><span class="chip">المناوبة المدرسية</span></div>
<div class="stage"><div class="world"><div class="metal schedule"><div class="screen"><div class="schedule-header"><div><h3>الجدول المدرسي</h3><small>الفصل الدراسي الأول</small></div><div class="status">منشور للمعلمين</div></div>${grid}</div></div></div></div>
<div class="float-card duty-card">${icon('people')}<h3>المناوبة المدرسية</h3><p>التوزيع والمتابعة وسجل التنفيذ</p><div class="duty-days"><b>ح</b><b>ن</b><b>ث</b><b>ر</b><b>خ</b></div><div class="status gold-status">متابعة اليوم الدراسي</div></div>
<div class="metal actual"><div class="screen">${chrome('الجدول المدرسي • من داخل المنصة')}<div class="screen-shot"><img src="assets/timetable.png" width="1600" height="1000" alt="شاشة الجدول المدرسي الفعلية ببيانات تجريبية"></div><div class="sheen"></div></div></div><p class="actual-label">شاشة فعلية من المنصة • الجدول والمناوبة مساحتان مستقلتان</p>
<div class="closing"><h2>وضوح في كل حصة.<br>تنظيم في كل يوم.</h2><p>من التخطيط إلى متابعة التنفيذ</p><div class="closing-line"></div></div>`, `
tl.addLabel('spatial-schedule',0).addLabel('duty-focus',3).addLabel('real-product',6.5).addLabel('close',11.5).addLabel('loop-return',14);
tl.fromTo('.world',{rotationX:22,rotationY:-13,scale:.91,y:35},{rotationX:8,rotationY:-7,scale:.94,y:0,duration:3,ease:'sine.inOut'},0);
tl.fromTo('.lesson',{opacity:.25,scale:.96},{opacity:1,scale:1,duration:.6,stagger:.015},.15);
tl.fromTo('.duty-card',{opacity:0,x:60,rotationY:14},{opacity:1,x:0,rotationY:0,duration:1.3},2.7);
tl.fromTo('.world',{rotationX:8,rotationY:-7,scale:.94,y:0},{rotationX:4,rotationY:3,scale:.86,y:10,duration:2.6,ease:'sine.inOut',immediateRender:false},3.2);
tl.to('.duty-card',{opacity:0,x:40,duration:.7},5.7);
tl.to('.schedule',{opacity:0,duration:.35},5.85);
tl.fromTo('.actual',{opacity:0,y:40,rotationX:8,rotationY:-3,scale:.94},{opacity:1,y:0,rotationX:0,rotationY:0,scale:1,duration:1.5},6.2);
tl.fromTo('.actual-label',{opacity:0},{opacity:1,duration:.8},7.1);
tl.fromTo('.sheen',{x:-1200,opacity:0},{x:1000,opacity:.5,duration:2.3,ease:'sine.inOut'},7);
tl.to(['.actual','.actual-label'],{opacity:0,y:-15,duration:.8},11.25);
tl.to(['.caption','.agenda-line','.title-rule'],{opacity:0,duration:.35},11.25);
tl.fromTo('.closing',{opacity:0},{opacity:1,duration:.9},11.6);
tl.fromTo('.closing h2',{y:25,opacity:0},{y:0,opacity:1,duration:.9},11.7);
tl.to('.closing',{opacity:0,duration:.8},14.4);
tl.to(['.caption','.agenda-line','.title-rule'],{opacity:1,duration:.6},15.1);
tl.fromTo('.schedule',{opacity:0},{opacity:1,duration:.8,immediateRender:false},14.4);
tl.fromTo('.world',{rotationX:4,rotationY:3,scale:.86,y:10},{rotationX:22,rotationY:-13,scale:.91,y:35,duration:1.6,ease:'sine.inOut',immediateRender:false},14.4);
tl.fromTo('.lesson',{opacity:1,scale:1},{opacity:.25,scale:.96,duration:.6,stagger:.015,immediateRender:false},15);
`,['timetable.png']);

composition('evidence','من الشواهد إلى الرؤية',16,`
.scene{background:radial-gradient(ellipse at 70% 36%,#1c6141 0%,#0b3f29 45%,#062c20 100%)}.caption{top:145px;right:94px}.caption h2{font-size:64px}.caption p{font-size:24px}.ev-rule{position:absolute;top:117px;right:96px}.flow{position:absolute;top:325px;left:90px;width:1420px;height:450px;perspective:1800px}.flow-world{position:absolute;inset:0;transform-style:preserve-3d}.document{position:absolute;left:1045px;top:10px;width:300px;height:344px;border-radius:13px;background:#f8faf8;border:1px solid #d8eee1;color:#0b3f29;padding:32px;box-shadow:0 25px 50px rgba(0,20,12,.22);direction:rtl;transform-style:preserve-3d}.document h3{font-size:27px;line-height:1.4;margin-top:16px}.document p{font-size:18px;color:#67756d;line-height:1.6;margin-top:11px}.doc-line{height:7px;border-radius:4px;background:#d8eee1;margin-top:17px}.doc-line.short{width:68%}.doc-line.gold{background:#eee1a6;width:85%}.file-type{display:flex;align-items:center;gap:12px;font-size:17px;font-weight:700}.file-type .icon-box{margin:0}.document .status{margin-top:22px}.document.back-a{left:1078px;top:32px;background:#c5dcca}.document.back-b{left:1108px;top:55px;background:#8fba9a}.review{position:absolute;left:580px;top:55px;width:350px;height:330px;direction:rtl;background:#f8faf8;padding:28px;border:1px solid #d8eee1;border-radius:15px;color:#0b3f29;box-shadow:0 30px 60px rgba(0,20,12,.28)}.review h3{font-size:27px}.review p{font-size:18px;color:#67756d;line-height:1.6;margin-top:9px}.review .status{margin-top:20px}.review-list{display:flex;flex-direction:column;gap:13px;margin-top:24px}.review-list span{display:flex;align-items:center;gap:9px;font-size:18px}.check-dot{width:21px;height:21px;border-radius:50%;border:1px solid #0f7132;background:#edf7f1;display:grid;place-items:center;color:#0f7132;font-size:15px}.review-list .muted-dot{border-color:#c99508;background:#fbf4d7;color:#704d00}.report{position:absolute;left:50px;top:30px;width:370px;height:350px;border-radius:16px;border:1px solid #d8eee1;background:#f8faf8;padding:29px;color:#0b3f29;direction:rtl;box-shadow:0 35px 70px rgba(0,20,12,.3)}.report h3{font-size:27px}.report p{font-size:17px;color:#67756d;margin-top:9px}.chart-rows{margin-top:28px;display:flex;flex-direction:column;gap:16px}.chart-row{display:grid;grid-template-columns:104px 1fr 34px;align-items:center;gap:10px;font-size:16px}.track{height:9px;border-radius:6px;background:#e2e5df;overflow:hidden}.fill{width:100%;height:100%;border-radius:6px;background:#168044;transform-origin:right center}.chart-row:last-child .fill{background:#c99508}.chart-row b{font-size:17px;direction:ltr}.report small{display:block;font-size:15px;color:#67756d;margin-top:24px}.connect{position:absolute;top:176px;width:110px;height:2px;background:linear-gradient(90deg,#e0b31b,rgba(247,214,103,.15));transform-origin:right center}.connect.a{left:933px}.connect.b{left:440px;width:138px}.connect:before{content:'';position:absolute;left:0;top:-4px;width:10px;height:10px;border-left:2px solid #e0b31b;border-bottom:2px solid #e0b31b;transform:rotate(45deg)}.flow-labels{position:absolute;left:180px;right:180px;bottom:125px;display:flex;direction:rtl;justify-content:space-between;color:#d8eee1;font-size:21px}.flow-labels span{width:280px;text-align:center}.detail-note{position:absolute;bottom:83px;left:0;right:0;text-align:center;color:#b5ccbf;font-size:17px}.end-message{position:absolute;inset:0;z-index:15;background:radial-gradient(ellipse at 35% 55%,#12563a,#0b3f29 65%);display:grid;place-content:center;text-align:center;direction:rtl}.end-message h2{font-size:78px;line-height:1.5;font-weight:800}.end-message p{font-size:28px;color:#c1d2c7;margin-top:16px}.end-message .gold-rule{margin:30px auto;width:88px}
`, `<div class="gold-rule ev-rule"></div><div class="caption"><h2>كل شاهد في مكانه.</h2><p>من ملف موثق إلى رؤية أوضح للجاهزية.</p></div><div class="flow"><div class="flow-world">
<div class="document back-b"></div><div class="document back-a"></div><div class="document front"><div class="file-type">${icon('document')}<span>ملف الشاهد</span></div><h3>خطة التحسين المدرسي</h3><p>مساحة ملفات المدرسة</p><div class="doc-line"></div><div class="doc-line short"></div><div class="doc-line gold"></div><div class="status gold-status">مرتبط بمتطلب</div></div>
<div class="connect a"></div><div class="review">${icon('shield')}<h3>مراجعة الشواهد</h3><p>رفع الملف خطوة.<br>اعتماد الشاهد قرار مستقل.</p><div class="review-list"><span><i class="check-dot">✓</i>ربط بالمتطلب</span><span><i class="check-dot">✓</i>مراجعة الشاهد</span><span class="approval"><i class="check-dot">✓</i>اعتماد بعد المراجعة</span></div></div><div class="connect b"></div>
<div class="report"><h3>الجاهزية</h3><p>متابعة المتطلبات والشواهد المعتمدة</p><div class="chart-rows"><div class="chart-row"><span>الإدارة المدرسية</span><div class="track"><div class="fill f1"></div></div><b>8/9</b></div><div class="chart-row"><span>التعليم والتعلم</span><div class="track"><div class="fill f2"></div></div><b>7/9</b></div><div class="chart-row"><span>نواتج التعلم</span><div class="track"><div class="fill f3"></div></div><b>6/9</b></div><div class="chart-row"><span>البيئة المدرسية</span><div class="track"><div class="fill f4"></div></div><b>5/9</b></div></div><small>قيم توضيحية • ليست نتيجة تقييم المدرسة</small></div></div></div>
<div class="flow-labels"><span>تنظيم الملفات</span><span>مراجعة واعتماد</span><span>متابعة الجاهزية</span></div><div class="detail-note">تمثيل بصري لمسار قائم في المنصة • الرفع وحده لا يعني اعتماد الشاهد</div><div class="end-message"><h2>أثر موثق.<br>رؤية أوضح.</h2><p>الشواهد والمتابعة والتقارير في مساحة واحدة</p><div class="gold-rule"></div></div>`, `
tl.addLabel('files',0).addLabel('review',2.5).addLabel('readiness',5).addLabel('readable-hold',7.8).addLabel('close',11.4).addLabel('loop-return',14);
tl.fromTo('.flow-world',{rotationY:-7,rotationX:8,y:10},{rotationY:0,rotationX:0,y:0,duration:4,ease:'sine.inOut'},0);
tl.fromTo('.back-a',{x:0,rotation:0},{x:10,rotation:3,duration:1.4,ease:'sine.inOut'},.2);
tl.fromTo('.back-b',{x:0,rotation:0},{x:20,rotation:6,duration:1.4,ease:'sine.inOut'},.3);
tl.set('.document.front',{z:45},0);
tl.set('.review',{z:65},0);
tl.set('.report',{z:90},0);
tl.fromTo('.review',{opacity:0,y:20},{opacity:1,y:0,duration:1},2.1);
tl.fromTo('.connect.a',{scaleX:0},{scaleX:1,duration:.8},1.6);
tl.fromTo('.review-list span',{opacity:0,x:15},{opacity:1,x:0,duration:.55,stagger:.55},2.7);
tl.fromTo('.connect.b',{scaleX:0},{scaleX:1,duration:.8},4.8);
tl.fromTo('.report',{opacity:0,y:20},{opacity:1,y:0,duration:1},5.2);
tl.to('.document.front',{z:0,duration:1.2,ease:'sine.inOut'},5.1);
tl.to('.review',{z:0,duration:1.2,ease:'sine.inOut'},5.2);
tl.to('.report',{z:0,duration:1.2,ease:'sine.inOut'},5.4);
tl.fromTo('.f1',{scaleX:0},{scaleX:8/9,duration:1.2},5.5);
tl.fromTo('.f2',{scaleX:0},{scaleX:7/9,duration:1.2},5.65);
tl.fromTo('.f3',{scaleX:0},{scaleX:6/9,duration:1.2},5.8);
tl.fromTo('.f4',{scaleX:0},{scaleX:5/9,duration:1.2},5.95);
tl.to('.flow-world',{rotationY:0,rotationX:0,y:-5,duration:2.8,ease:'sine.inOut'},8);
tl.to(['.flow','.flow-labels','.detail-note','.caption','.ev-rule'],{opacity:0,duration:.5},10.8);
tl.fromTo('.end-message',{opacity:0},{opacity:1,duration:1},11.3);
tl.fromTo('.end-message h2',{opacity:0,y:25},{opacity:1,y:0,duration:1},11.4);
tl.to('.end-message',{opacity:0,duration:.9},14.25);
tl.to('.flow-world',{rotationY:-7,rotationX:8,y:10,duration:1.6,ease:'sine.inOut'},14.4);
tl.to('.document.front',{z:45,duration:1.6,ease:'sine.inOut'},14.4);
tl.to('.review',{z:65,duration:1.6,ease:'sine.inOut'},14.4);
tl.to('.report',{z:90,duration:1.6,ease:'sine.inOut'},14.4);
tl.to(['.review','.report'],{opacity:0,y:20,duration:1.3},14.7);
tl.to('.review-list span',{opacity:0,x:15,duration:.8},15.2);
tl.to('.connect',{scaleX:0,duration:1.2},14.8);
tl.to('.fill',{scaleX:0,duration:1.2},14.8);
tl.to(['.back-a','.back-b'],{x:0,rotation:0,duration:1.3},14.7);
tl.to(['.flow','.flow-labels','.detail-note','.caption','.ev-rule'],{opacity:1,duration:.6},15.4);
`);
console.log('Built pulse, harmony, evidence: 1600×900 / 30 fps / 16 seconds each.');
