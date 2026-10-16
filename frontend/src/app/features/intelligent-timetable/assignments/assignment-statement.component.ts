import {CommonModule} from '@angular/common';
import {Component,DestroyRef,EventEmitter,HostListener,Input,OnChanges,Output,inject} from '@angular/core';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {FormsModule} from '@angular/forms';
import {DialogModule} from 'primeng/dialog';
import {finalize} from 'rxjs';
import {TeachingAssignmentService} from '../../../core/services/teaching-assignment.service';
import {TeachingCell,TeachingOverview} from '../../../core/models/teaching-assignment.models';
import {extractHttpErrorMessage,readHttpErrorBody} from '../../../core/http/http-error-message';
import {downloadBlob,fileNameFromResponse} from '../../../core/utils/browser-download';
import {DutyPdfPreviewComponent} from '../../timetables/duty/duty-pdf-preview.component';

export const statementCategories=[{value:'Specialization',label:'مواد التخصص'},{value:'LifeSkills',label:'مهارات حياتية'},
  {value:'CriticalThinking',label:'تفكير ناقد'},{value:'Activity',label:'النشاط'},{value:'Other',label:'مادة إضافية أخرى (الملحق)'}];
interface ClassificationRow {key:string;teacher:string;subject:string;category:string;original:string;}

@Component({selector:'app-assignment-statement',standalone:true,imports:[CommonModule,FormsModule,DialogModule,DutyPdfPreviewComponent],
  templateUrl:'./assignment-statement.component.html',styleUrl:'./assignment-statement.component.css'})
export class AssignmentStatementComponent implements OnChanges {
  @Input({required:true}) data!:TeachingOverview;
  @Input({required:true}) setupId!:number;
  @Output() closed=new EventEmitter<void>();
  @Output() saved=new EventEmitter<TeachingOverview>();
  @Output() refresh=new EventEmitter<void>();
  private readonly api=inject(TeachingAssignmentService);private readonly destroy=inject(DestroyRef);
  readonly categories=statementCategories;
  scope='all';rows:ClassificationRow[]=[];search='';page=1;busy=false;error='';conflict=false;denied=false;
  private savingCategories=false;
  blob:Blob|null=null;fileName='بيان إسنادات التدريس.pdf';
  get stages(){return [...new Set(this.data.classrooms.map(c=>c.stage))].filter(s=>s>=1&&s<=3).sort();}
  stageLabel(stage:number){return ({1:'المرحلة الابتدائية',2:'المرحلة المتوسطة',3:'المرحلة الثانوية'} as Record<number,string>)[stage];}
  get filtered(){const q=this.search.trim();return this.rows.filter(r=>!q||r.teacher.includes(q)||r.subject.includes(q));}
  get pages(){return Math.max(1,Math.ceil(this.filtered.length/20));}
  get pageRows(){return this.filtered.slice((this.page-1)*20,this.page*20);}
  get missing(){return this.rows.filter(r=>!r.category).length;}
  hasUnsavedWork(){return this.savingCategories||this.rows.some(r=>r.category!==r.original);}
  ngOnChanges(){const page=this.page;this.resetRows();this.page=Math.min(page,this.pages);}
  private resetRows(){
    const requirements=new Map(this.data.requirements.map(r=>[r.id,r])),classrooms=new Map(this.data.classrooms.map(c=>[c.id,c]));
    const teachers=new Map(this.data.teachers.map(t=>[t.id,t.name])),subjects=new Map(this.data.subjects.map(s=>[s.id,s.name]));
    const rows=new Map<string,ClassificationRow>();
    for(const cell of this.data.assignments){const r=requirements.get(cell.classSubjectRequirementId);if(!r||!this.inScope(classrooms.get(r.classroomId)?.stage))continue;
      for(const m of cell.members){const key=`${m.teacherTimetableProfileId}:${r.subjectId}`,category=m.statementCategory||'';
        const old=rows.get(key);if(old){if(old.category!==category)old.category=old.original='';}
        else rows.set(key,{key,teacher:teachers.get(m.teacherTimetableProfileId)||'معلم غير متاح',subject:subjects.get(r.subjectId)||'مادة غير متاحة',category,original:category});}}
    this.rows=[...rows.values()].sort((a,b)=>a.teacher.localeCompare(b.teacher,'ar')||a.subject.localeCompare(b.subject,'ar'));this.page=1;this.blob=null;
  }
  private inScope(stage?:number){return this.scope==='all'||stage===Number(this.scope.slice(6));}
  changeScope(value:string,select?:HTMLSelectElement){
    if(this.busy || this.hasUnsavedWork()&&!window.confirm('تجاهل تعديلات تصنيف البيان غير المحفوظة؟')){if(select)select.value=this.scope;return;}
    this.scope=value;this.resetRows();this.error='';
  }
  changed(){this.blob=null;}
  close(){if(this.savingCategories)return;if(!this.hasUnsavedWork()||window.confirm('تجاهل تعديلات تصنيف البيان غير المحفوظة؟'))this.closed.emit();}
  @HostListener('document:keydown.escape') escape(){this.close();}
  reload(){if(this.busy)return;if(!this.hasUnsavedWork()||window.confirm('تجاهل تصنيفاتك وتحميل الإسنادات الأحدث؟'))this.refresh.emit();}
  saveCategories(){
    if(this.busy||!this.hasUnsavedWork())return;
    const chosen=new Map(this.rows.filter(r=>r.category && r.category!==r.original).map(r=>[r.key,r.category]));
    const requirements=new Map(this.data.requirements.map(r=>[r.id,r])),classrooms=new Map(this.data.classrooms.map(c=>[c.id,c]));
    const changes:TeachingCell[]=[];
    for(const cell of this.data.assignments){const r=requirements.get(cell.classSubjectRequirementId);if(!r||!this.inScope(classrooms.get(r.classroomId)?.stage))continue;
      if(cell.members.some(m=>chosen.has(`${m.teacherTimetableProfileId}:${r.subjectId}`)))changes.push({...cell,members:cell.members.map(m=>({...m,statementCategory:chosen.get(`${m.teacherTimetableProfileId}:${r.subjectId}`)||m.statementCategory}))});}
    if(!changes.length)return;this.busy=this.savingCategories=true;this.error='';this.conflict=false;
    this.api.save(this.setupId,this.data.revision,changes).pipe(takeUntilDestroyed(this.destroy),finalize(()=>{this.busy=this.savingCategories=false;})).subscribe({next:r=>{
      if(r.isSuccess&&r.data){const page=this.page;this.data=r.data;this.resetRows();this.page=Math.min(page,this.pages);this.saved.emit(r.data);}else this.error=r.message||'تعذر حفظ تصنيفات البيان.';
    },error:e=>void this.fail(e)});
  }
  prepare(){
    if(this.busy||this.missing||this.hasUnsavedWork()||!this.rows.length)return;this.busy=true;this.error='';this.conflict=false;
    this.api.statement(this.setupId,this.data.revision,this.scope).pipe(takeUntilDestroyed(this.destroy),finalize(()=>this.busy=false)).subscribe({next:r=>{
      if(!r.body||r.body.type!=='application/pdf'){this.error='لم تصل نسخة PDF صالحة. أعد المحاولة.';return;}
      this.blob=r.body;this.fileName=fileNameFromResponse(r,this.fileName);
    },error:e=>void this.fail(e)});
  }
  download(){if(this.blob)downloadBlob(this.blob,this.fileName);}
  private async fail(error:unknown){await readHttpErrorBody(error);this.conflict=(error as {status?:number})?.status===409;this.denied=(error as {status?:number})?.status===403;
    this.blob=null;this.error=this.conflict?'تغيّرت الإسنادات. احتفظنا بتصنيفاتك؛ راجعها ثم حمّل النسخة الأحدث.':extractHttpErrorMessage(error)||'تعذر تجهيز البيان. أعد المحاولة.';}
}
