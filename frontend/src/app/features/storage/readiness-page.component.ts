import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { CalendarModule } from 'primeng/calendar';
import { DialogModule } from 'primeng/dialog';
import { PaginatorModule } from 'primeng/paginator';
import { ProgressBarModule } from 'primeng/progressbar';
import { Subject, Subscription, combineLatest, forkJoin, takeUntil, Observable } from 'rxjs';
import { ClearableSelectComponent } from '../../shared/components/clearable-select/clearable-select.component';
import { ReadinessApiService } from './readiness-api.service';
import { StorageApiService } from './storage-api.service';
import { StorageEvidenceApiService } from './evidence-api.service';
import { StorageWorkspaceNavComponent } from './storage-workspace-nav.component';
import { EvaluationFilter, EvaluationTemplate, ReadinessSummary, EvaluationRequirement, ManualEvaluation, EvaluationMember, EvaluationVersion, DigitalIndexFile } from './readiness.models';

@Component({selector:'app-storage-readiness',standalone:true,imports:[CommonModule,FormsModule,RouterLink,TranslateModule,ButtonModule,CalendarModule,DialogModule,PaginatorModule,ProgressBarModule,ClearableSelectComponent,StorageWorkspaceNavComponent],templateUrl:'./readiness-page.component.html',styleUrls:['./readiness-page.component.css']})
export class ReadinessPageComponent implements OnInit,OnDestroy {
  private readonly api=inject(ReadinessApiService);private readonly storage=inject(StorageApiService);private readonly evidence=inject(StorageEvidenceApiService);
  private readonly route=inject(ActivatedRoute);private readonly router=inject(Router);private readonly translate=inject(TranslateService);
  private readonly destroyed=new Subject<void>();private load?:Subscription;
  readonly mode=this.route.snapshot.data['mode'] as string || 'readiness';
  readonly navigation=['readiness','tracker','gaps','reports'];
  get returnToUrl() { return this.router.url; }
  filter:EvaluationFilter={academicYearId:0,templateVersion:1,criticalOnly:false,hideCompleted:false,gapsOnly:this.mode==='gaps',trackerOnly:this.mode==='tracker',page:1,pageSize:25};
  template?:EvaluationTemplate;summary?:ReadinessSummary;rows:EvaluationRequirement[]=[];critical:EvaluationRequirement[]=[];files:DigitalIndexFile[]=[];
  years:{id:number;nameAr:string}[]=[];members:EvaluationMember[]=[];versions:EvaluationVersion[]=[];manuals:ManualEvaluation[]=[];
  total=0;busy=false;saving=false;error='';notice='';canManage=false;disabled=false;initialized=false;grouping='table';cards=false;standardSearch='';
  editing?:EvaluationRequirement;editOpen=false;responsible?:string;responsibleRole='';dueDate?:Date;importance=1;mandatory=true;policy=1;minimum=1;followUp='NotStarted';note='';reason='';
  manualScope='school';judgment='';manualValue?:number;manualReason='';historyOpen=false;history:{actor:string;reason:string;date:string;judgment?:string;revision?:number}[]=[];
  readonly statusOptions=['Fulfilled','Unfulfilled','NoFile','Unavailable','AwaitingReview','Rejected','InsufficientApprovedLinks'].map(value=>({label:'S4.'+value,value}));
  readonly importanceOptions=[{label:'S4.Normal',value:1},{label:'S4.Important',value:2},{label:'S4.Critical',value:3}];
  readonly followUpOptions=['NotStarted','InProgress','ReadyForReview'].map(value=>({label:'S4.'+value,value}));
  get domainOptions() {return (this.template?.domains || []).map(s=>({label:s.name,value:s.code}));}
  get standardOptions() {return (this.template?.standards || []).filter(s=>!this.filter.domainCode || s.code.startsWith(this.filter.domainCode+'.')).map(s=>({label:s.code+' '+s.name,value:s.code}));}
  get visibleStandards() {return (this.summary?.standards || []).filter(s=>(s.code+' '+s.name).toLocaleLowerCase().includes(this.standardSearch.trim().toLocaleLowerCase()));}
  get scopeOptions() {return [{label:this.translate.instant('S4.SCHOOL'),value:'school'},...(this.template?.domains || []).map(s=>({label:s.name,value:s.code})),...(this.template?.standards || []).map(s=>({label:s.code+' '+s.name,value:s.code}))];}
  get groups() {
    const groups=new Map<string,EvaluationRequirement[]>();for(const row of this.rows) {const key=this.grouping==='person'?(row.responsibleName || row.responsibleRole || this.translate.instant('S4.UNASSIGNED')):this.template?.domains.find(d=>d.code===row.domainCode)?.name || this.translate.instant('S4.UNCLASSIFIED');groups.set(key,[...(groups.get(key)||[]),row]);}return Array.from(groups,([name,items])=>({name,items}));
  }
  ngOnInit() {
    this.busy=true;
    forkJoin({context:this.storage.contextInfo(false),years:this.evidence.years(),members:this.api.members(),versions:this.api.versions()}).pipe(takeUntil(this.destroyed)).subscribe({next:r=>{
      this.canManage=r.context.canManage;this.years=r.years;this.members=r.members;this.versions=r.versions;
      this.filter.academicYearId=r.context.academicYearId || this.years[0]?.id || 0;
      this.initialized=true;
      combineLatest([this.route.paramMap,this.route.queryParamMap]).pipe(takeUntil(this.destroyed)).subscribe(([params,q])=>{
        this.filter={academicYearId:Number(q.get('academicYearId') || this.filter.academicYearId),templateVersion:Number(q.get('templateVersion') || 1),
          domainCode:q.get('domainCode') || undefined,standardCode:params.get('code') || q.get('standardCode') || undefined,responsibleUserId:q.get('responsibleUserId') || undefined,
          importance:q.get('importance')?Number(q.get('importance')):undefined,status:q.get('status') || undefined,search:q.get('search') || undefined,
          criticalOnly:q.get('criticalOnly')==='true',hideCompleted:q.get('hideCompleted')==='true',gapsOnly:this.mode==='gaps',trackerOnly:this.mode==='tracker' || q.get('trackerOnly')==='true',page:Number(q.get('page') || 1),pageSize:25};
        this.grouping=q.get('view') || 'table';this.cards=q.get('cards')==='true';this.reload();
      });
    },error:e=>{this.busy=false;this.fail(e);}});
  }
  reload(preserveError=false) {
    if(!this.filter.academicYearId) {this.busy=false;return;}
    this.load?.unsubscribe();this.busy=true;if(!preserveError)this.error='';this.rows=[];this.files=[];this.summary=undefined;this.manuals=[];
    this.load=forkJoin({template:this.api.template(this.filter.templateVersion),summary:this.api.readiness(this.filter),
      rows:this.api.requirements(this.filter,this.mode==='gaps'),critical:this.api.requirements({...this.filter,status:undefined,hideCompleted:false,criticalOnly:true,page:1},true),
      manuals:this.api.manuals(this.filter.academicYearId,this.filter.templateVersion),files:this.api.index(this.filter)}).pipe(takeUntil(this.destroyed)).subscribe({next:r=>{
        this.busy=false;this.template=r.template;this.summary=r.summary;this.rows=r.rows.items;this.critical=r.critical.items;this.files=r.files.items;this.total=this.mode==='digital-index'?r.files.total:r.rows.total;
        this.manuals=r.manuals;this.disabled=false;
      },error:e=>{this.busy=false;this.fail(e);}});
  }
  filtersChanged(reset=true) {if(!this.initialized)return;if(reset)this.filter.page=1;const code=this.route.snapshot.paramMap?.get('code');const path=code && code!==this.filter.standardCode?[this.filter.standardCode?'/school-manager/storage/standards/'+this.filter.standardCode:'/school-manager/storage/readiness']:[];this.router.navigate(path,{relativeTo:this.route,queryParams:{...this.filter,search:this.filter.search || null,view:this.grouping,cards:this.cards},replaceUrl:true});}
  domainChanged() {this.filter.standardCode=undefined;this.filtersChanged();}
  resetDisplay() {const {academicYearId,templateVersion}=this.filter;this.filter={academicYearId,templateVersion,page:1,pageSize:25,criticalOnly:false,hideCompleted:false,trackerOnly:this.mode==='tracker',gapsOnly:this.mode==='gaps'};this.grouping='table';this.cards=false;this.filtersChanged();}
  navigate(mode:string) {return ['/school-manager/storage/'+mode];}
  initialize() {this.perform(this.api.initialize(this.filter.academicYearId,this.filter.templateVersion));}
  edit(row:EvaluationRequirement) {this.editing=row;this.responsible=row.responsibleUserId;this.responsibleRole=row.responsibleRole || '';this.dueDate=row.dueDate?new Date(row.dueDate+'T12:00:00'):undefined;
    this.importance=row.importance==='Critical'?3:row.importance==='Important'?2:1;this.mandatory=row.isMandatory;this.policy=row.policy==='MinimumApprovedLinks'?2:1;this.minimum=row.requiredLinks;this.followUp=row.followUpStatus;this.note=row.followUpNote || '';this.reason='';this.editOpen=true;}
  saveFollowUp() {if(!this.editing || !this.reason.trim())return;const date=this.dueDate?`${this.dueDate.getFullYear()}-${String(this.dueDate.getMonth()+1).padStart(2,'0')}-${String(this.dueDate.getDate()).padStart(2,'0')}`:null;
    this.perform(this.api.configure(this.editing.id,{responsibleUserId:this.responsible || null,responsibleRole:this.responsibleRole || null,importance:this.importance,isMandatory:this.mandatory,policy:this.policy,minimumApprovedLinks:this.minimum,dueDate:date,followUpStatus:this.followUp,note:this.note || null,reason:this.reason,rowVersion:this.editing.rowVersion}),()=>this.editOpen=false);}
  selectManualScope() {const e=this.manuals.find(x=>x.scopeCode===this.manualScope);this.judgment=e?.judgment || '';this.manualValue=e?.value;this.manualReason='';}
  saveManual() {if((!this.judgment.trim() && this.manualValue==null) || !this.manualReason.trim())return;const e=this.manuals.find(x=>x.scopeCode===this.manualScope);
    this.perform(this.api.saveManual({academicYearId:this.filter.academicYearId,templateVersion:this.filter.templateVersion,scopeCode:this.manualScope,judgment:this.judgment,value:this.manualValue ?? null,reason:this.manualReason,rowVersion:e?.rowVersion ?? null}));}
  showManualHistory(e:ManualEvaluation) {this.history=[];this.historyOpen=true;this.api.manualHistory(e.id).pipe(takeUntil(this.destroyed)).subscribe({next:rows=>this.history=rows.map(r=>{const s=JSON.parse(r.snapshotJson);return {actor:s.evaluatorName || s.EvaluatorName,reason:s.reason || s.Reason,date:r.createdAtUtc,judgment:s.judgment || s.Judgment,revision:r.revision};}),error:x=>this.fail(x)});}
  showFollowUpHistory(e:EvaluationRequirement) {this.history=[];this.historyOpen=true;this.api.followUpHistory(e.id).pipe(takeUntil(this.destroyed)).subscribe({next:rows=>this.history=rows.map(r=>({actor:r.actorName,reason:r.reason,date:r.createdAtUtc})),error:x=>this.fail(x)});}
  export(format:string) {this.saving=true;this.api.export(format,this.filter).pipe(takeUntil(this.destroyed)).subscribe({next:blob=>{this.saving=false;const url=URL.createObjectURL(blob);const a=document.createElement('a');a.href=url;a.download=`school-readiness-${this.filter.academicYearId}-v${this.filter.templateVersion}.${format==='excel'?'xlsx':format}`;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);},error:e=>{this.saving=false;this.fail(e);}});}
  private perform(action:Observable<unknown>,success?:()=>void) {if(this.saving)return;this.saving=true;this.error='';action.pipe(takeUntil(this.destroyed)).subscribe({next:()=>{this.saving=false;success?.();this.notice=this.translate.instant('S4.SAVED');this.reload();},error:e=>{this.saving=false;this.fail(e);if(e.status===409){this.editOpen=false;this.reload(true);}}});}
  private fail(e:any) {this.error=e?.error?.message || e?.message || this.translate.instant('S4.ERROR');if(e.status===404)this.disabled=true;if(e.status===403 || e.status===401){this.load?.unsubscribe();this.rows=[];this.files=[];this.critical=[];this.summary=undefined;this.template=undefined;this.manuals=[];this.members=[];this.canManage=false;this.editOpen=false;this.historyOpen=false;this.history=[];}}
  ngOnDestroy() {this.destroyed.next();this.destroyed.complete();this.load?.unsubscribe();}
}
