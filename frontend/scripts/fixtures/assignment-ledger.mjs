// Synthetic UI-only data. Never sent to the operational API.
export function assignmentFixture(classCount=24,teacherCount=110,subjectCount=8) {
  const names=['خالد السالم','أحمد الحربي','عبدالله القحطاني','محمد الغامدي','حسن الزهراني','ناصر العتيبي','سالم الشهري','عمر الدوسري','صالح العمري','وليد المطيري'];
  const titles=['الرياضيات','اللغة العربية','اللغة الإنجليزية','العلوم','المهارات الحياتية','التفكير الناقد','النشاط','التربية البدنية'];
  const colors=['#28794d','#397b98','#796298','#a07732','#427b71','#777c45','#a06b5b','#566c83'];
  const teachers=Array.from({length:teacherCount},(_,i)=>({id:i+1,instructorProfileId:500+i,name:names[i%10]+' '+String(i+1).padStart(3,'0'),specialization:titles[i%subjectCount] ?? 'معلم',isActive:i!==teacherCount-1,isVisiting:i%9===0,maximumWeeklyPeriods:24,allocatedPeriods:0,remainingPeriods:24,subjectCount:0,classroomCount:0,warnings:[]}));
  const subjects=Array.from({length:subjectCount},(_,i)=>({id:i+1,name:titles[i%8]+(i>=8?' '+(i+1):''),color:colors[i%8],revision:1}));
  const ordinals=['الأول','الثاني','الثالث'];
  const groupSize=Math.ceil(classCount/3);
  const classrooms=Array.from({length:classCount},(_,i)=>({id:i+1,name:ordinals[Math.floor(i/groupSize)%3]+' المتوسط، شعبة '+(i%groupSize+1),stage:2,gradeLevel:Math.floor(i/groupSize)%3+1}));
  const requirements=classrooms.flatMap(c=>subjects.filter(s=>s.id!==8 || c.id%2===0).map(s=>({id:c.id*100+s.id,subjectId:s.id,classroomId:c.id,classroomName:c.name,revision:1,totalWeeklyPeriods:s.id<=2?5:s.id<=4?4:2,
    rules:{individualPeriodCount:s.id<=2?3:s.id<=4?4:2,pairedBlockCount:s.id<=2?1:0,timePreference:'None',earliestPeriodSequence:null,latestPreferredPeriodSequence:null,allowedDays:[],fixedSlots:[],roomIds:[],preferredRoomId:null}})));
  const assignments=requirements.filter((r,i)=>i%6!==0).map((r,i)=>{const t=teachers[(i*7)%(teacherCount-1)];return {id:r.id,classSubjectRequirementId:r.id,mode:'SingleTeacher',members:[{teacherTimetableProfileId:t.id,instructorProfileId:t.instructorProfileId,name:t.name,allocatedPeriodCount:r.totalWeeklyPeriods,allocatedPairedBlockCount:r.rules.pairedBlockCount}]};});
  return recompute({revision:41,subjects,classrooms,requirements,assignments,teachers,warnings:[]});
}
export function recompute(data) {
  for(const t of data.teachers) {
    const assigned=data.assignments.filter(a=>a.members.some(m=>m.teacherTimetableProfileId===t.id));
    const r=assigned.map(a=>data.requirements.find(r=>r.id===a.classSubjectRequirementId)).filter(Boolean);
    t.allocatedPeriods=assigned.reduce((n,a)=>n+a.members.find(m=>m.teacherTimetableProfileId===t.id).allocatedPeriodCount,0);
    t.remainingPeriods=t.maximumWeeklyPeriods-t.allocatedPeriods;t.subjectCount=new Set(r.map(r=>r.subjectId)).size;t.classroomCount=new Set(r.map(r=>r.classroomId)).size;
  }return data;
}
