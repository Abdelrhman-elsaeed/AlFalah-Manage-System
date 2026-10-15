import { TeachingCell, TeachingOverview } from '../../../core/models/teaching-assignment.models';
import { SubjectClassroom, SubjectRequirement } from '../../../core/models/subject.models';

export interface TeacherLoad { periods: number; subjects: number; classrooms: number; }
export interface LedgerEntry { requirement: SubjectRequirement; cell: TeachingCell; }
export const emptyLoad: TeacherLoad = {periods:0,subjects:0,classrooms:0};
const ordinals = ['','الأول','الثاني','الثالث','الرابع','الخامس','السادس'];
const stageNames: Record<number,string> = {1:'الابتدائي',2:'المتوسط',3:'الثانوي'};
export const gradeKey = (c: SubjectClassroom) => `${c.stage}:${c.gradeLevel}`;
export const gradeLabel = (stage:number, grade:number) => `${ordinals[grade] ?? `الصف ${grade}`} ${stageNames[stage] ?? ''}`.trim();
export const slotKey = (classroom:number, subject:number) => `${classroom}:${subject}`;

/** One linear projection per overview/draft replacement; render-time lookups are indexed. */
export function buildLedger(data: TeachingOverview, drafts: ReadonlyMap<number,TeachingCell>) {
  const requirements = new Map(data.requirements.map(r=>[r.id,r]));
  const slots = new Map(data.requirements.map(r=>[slotKey(r.classroomId,r.subjectId),r]));
  const cells = new Map<number,TeachingCell>(data.assignments.map(a=>[a.classSubjectRequirementId,a]));
  for(const [id,draft] of drafts)cells.set(id,draft);
  for(const r of data.requirements)if(!cells.has(r.id))cells.set(r.id,{classSubjectRequirementId:r.id,mode:'SingleTeacher',members:[]});
  const teachers = new Map(data.teachers.map(t=>[t.id,t]));
  const classrooms = new Map(data.classrooms.map(c=>[c.id,c]));
  const loads = new Map<number,TeacherLoad>(), entries = new Map<number,LedgerEntry[]>();
  const subjectSets = new Map<number,Set<number>>(),classSets = new Map<number,Set<number>>();
  for(const cell of cells.values())for(const member of cell.members) {
    const id=member.teacherTimetableProfileId,load=loads.get(id) ?? {...emptyLoad};
    load.periods+=member.allocatedPeriodCount || 0;loads.set(id,load);
    const r=requirements.get(cell.classSubjectRequirementId);
    if(!r)continue; // Orphan load still counts until explicitly removed.
    if(!entries.has(id))entries.set(id,[]);entries.get(id)!.push({requirement:r,cell});
    if(!subjectSets.has(id))subjectSets.set(id,new Set());subjectSets.get(id)!.add(r.subjectId);
    if(!classSets.has(id))classSets.set(id,new Set());classSets.get(id)!.add(r.classroomId);
  }
  for(const [id,load] of loads){load.subjects=subjectSets.get(id)?.size ?? 0;load.classrooms=classSets.get(id)?.size ?? 0;}
  const groups=new Map<string,{key:string;label:string;classrooms:SubjectClassroom[];total:number;assigned:number}>();
  for(const c of data.classrooms){const key=gradeKey(c);if(!groups.has(key))groups.set(key,{key,label:gradeLabel(c.stage,c.gradeLevel),classrooms:[],total:0,assigned:0});groups.get(key)!.classrooms.push(c);}
  let pending=0;
  for(const r of data.requirements){const assigned=!!cells.get(r.id)?.members.length;if(!assigned)pending++;
    const c=classrooms.get(r.classroomId),group=c ? groups.get(gradeKey(c)) : undefined;
    if(group){group.total++;if(assigned)group.assigned++;}}
  return {requirements,slots,cells,teachers,loads,entries,pending,groups:[...groups.values()],
    orphaned:data.assignments.filter(a=>!requirements.has(a.classSubjectRequirementId))};
}

export function sameCell(a:TeachingCell,b:TeachingCell):boolean {
  const value=(cell:TeachingCell)=>JSON.stringify({mode:cell.members.length ? cell.mode : 'SingleTeacher',members:[...cell.members]
    .sort((x,y)=>x.teacherTimetableProfileId-y.teacherTimetableProfileId).map(m=>[m.teacherTimetableProfileId,m.allocatedPeriodCount,m.allocatedPairedBlockCount])});
  return value(a)===value(b);
}
