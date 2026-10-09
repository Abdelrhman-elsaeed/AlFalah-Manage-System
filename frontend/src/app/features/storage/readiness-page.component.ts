import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { CalendarModule } from 'primeng/calendar';
import { DialogModule } from 'primeng/dialog';
import { PaginatorModule } from 'primeng/paginator';
import { Subject, Subscription, combineLatest, debounceTime, distinctUntilChanged, forkJoin, takeUntil, Observable } from 'rxjs';
import { ClearableSelectComponent } from '../../shared/components/clearable-select/clearable-select.component';
import { ReadinessApiService, ReadinessBootstrap, ReadinessViewSnapshot } from './readiness-api.service';
import { StorageApiService } from './storage-api.service';
import { StorageEvidenceApiService } from './evidence-api.service';
import { StorageWorkspaceNavComponent } from './storage-workspace-nav.component';
import { EvaluationFilter, EvaluationTemplate, ReadinessSummary, EvaluationRequirement, ManualEvaluation, EvaluationMember, EvaluationVersion, DigitalIndexFile } from './readiness.models';
import { StorageContext, StoragePage } from './storage.models';

@Component({selector:'app-storage-readiness',standalone:true,imports:[CommonModule,FormsModule,RouterLink,TranslateModule,ButtonModule,CalendarModule,DialogModule,PaginatorModule,ClearableSelectComponent,StorageWorkspaceNavComponent],templateUrl:'./readiness-page.component.html',styleUrls:['./readiness-page.component.css']})
export class ReadinessPageComponent implements OnInit,OnDestroy {
  private readonly api=inject(ReadinessApiService);private readonly storage=inject(StorageApiService);private readonly evidence=inject(StorageEvidenceApiService);
  private readonly route=inject(ActivatedRoute);private readonly router=inject(Router);private readonly translate=inject(TranslateService);
  private readonly destroyed=new Subject<void>();private readonly searchChanges=new Subject<string>();private load?:Subscription;
  private bootstrapContext?:StorageContext;
  private lastRouteFilter?:EvaluationFilter;
  private rowPage?:StoragePage<EvaluationRequirement>;
  private criticalPage?:StoragePage<EvaluationRequirement>;
  private filePage?:StoragePage<DigitalIndexFile>;
  readonly mode=this.route.snapshot.data['mode'] as string || 'readiness';
  readonly navigation=['readiness','tracker','gaps','reports','manual'];
  get returnToUrl() { return this.router.url; }
  filter:EvaluationFilter={academicYearId:0,templateVersion:1,criticalOnly:false,hideCompleted:false,gapsOnly:this.mode==='gaps',trackerOnly:this.mode==='tracker',page:1,pageSize:25};
  template?:EvaluationTemplate;summary?:ReadinessSummary;rows:EvaluationRequirement[]=[];critical:EvaluationRequirement[]=[];files:DigitalIndexFile[]=[];
  years:{id:number;nameAr:string}[]=[];members:EvaluationMember[]=[];versions:EvaluationVersion[]=[];manuals:ManualEvaluation[]=[];
  total=0;busy=false;saving=false;error='';notice='';canManage=false;disabled=false;initialized=false;grouping='table';cards=false;showAdvancedFilters=false;
  criticalLoading=false;manualsLoading=false;
  editing?:EvaluationRequirement;editOpen=false;responsible?:string;responsibleRole='';dueDate?:Date;importance=1;mandatory=true;policy=1;minimum=1;followUp='NotStarted';note='';reason='';
  manualScope='school';judgment='';manualValue?:number;manualReason='';historyOpen=false;history:{actor:string;reason:string;date:string;judgment?:string;revision?:number}[]=[];
  readonly statusOptions=['Fulfilled','Unfulfilled','NoFile','Unavailable','AwaitingReview','Rejected','InsufficientApprovedLinks'].map(value=>({label:'S4.'+value,value}));
  readonly importanceOptions=[{label:'S4.Normal',value:1},{label:'S4.Important',value:2},{label:'S4.Critical',value:3}];
  readonly followUpOptions=['NotStarted','InProgress','ReadyForReview'].map(value=>({label:'S4.'+value,value}));
  get domainOptions() {return (this.template?.domains || []).map(s=>({label:s.name,value:s.code}));}
  get standardOptions() {return (this.template?.standards || []).filter(s=>!this.filter.domainCode || s.code.startsWith(this.filter.domainCode+'.')).map(s=>({label:s.code+' '+s.name,value:s.code}));}
  get selectedDomainName() {return this.summary?.domains.find(d=>d.code===this.filter.domainCode)?.name;}
  trackByCode(_:number,item:{code:string}) {return item.code;}
  trackByRequirement(_:number,item:EvaluationRequirement) {return item.id;}
  trackByStandardGroup(_:number,item:{code:string}) {return item.code;}
  trackByNameGroup(_:number,item:{name:string}) {return item.name;}
  toggleRequirement(detail:HTMLDetailsElement) {detail.open=!detail.open;}
  get scopeOptions() {return [{label:this.translate.instant('S4.SCHOOL'),value:'school'},...(this.template?.domains || []).map(s=>({label:s.name,value:s.code})),...(this.template?.standards || []).map(s=>({label:s.code+' '+s.name,value:s.code}))];}
  get groups() {
    const groups=new Map<string,EvaluationRequirement[]>();for(const row of this.rows) {const key=this.grouping==='person'?(row.responsibleName || row.responsibleRole || this.translate.instant('S4.UNASSIGNED')):this.template?.domains.find(d=>d.code===row.domainCode)?.name || this.translate.instant('S4.UNCLASSIFIED');groups.set(key,[...(groups.get(key)||[]),row]);}return Array.from(groups,([name,items])=>({name,items}));
  }
  get tableGroups() {
    const groups=new Map<string,EvaluationRequirement[]>();
    for(const row of this.rows) {
      const code=row.standardCode || 'other';
      if(!groups.has(code)) groups.set(code,[]);
      groups.get(code)!.push(row);
    }
    return Array.from(groups,([code,items])=>({code,name:this.template?.standards.find(s=>s.code===code)?.name || '',items}));
  }
  ngOnInit() {
    this.busy=true;
    this.searchChanges.pipe(debounceTime(250),distinctUntilChanged(),takeUntil(this.destroyed)).subscribe(()=>this.filtersChanged());
    const saved=this.api.peekLastView(this.mode,Number(this.route.snapshot.queryParamMap.get('academicYearId')) || undefined);
    if(saved) this.showSnapshot(saved);
    const cachedBootstrap=this.api.peekBootstrap();
    if(cachedBootstrap) this.applyBootstrap(cachedBootstrap);
    this.storage.contextInfo(false).pipe(takeUntil(this.destroyed)).subscribe({next:context=>{
      this.bootstrapContext=context;this.canManage=context.canManage;
      if(!this.initialized) this.startRoute(context.academicYearId || this.years[0]?.id || 0);
      this.rememberBootstrap();
    },error:e=>{this.busy=false;this.fail(e);}});
    forkJoin({years:this.evidence.years(),members:this.api.members(),versions:this.api.versions()}).pipe(takeUntil(this.destroyed)).subscribe({next:metadata=>{
      this.years=metadata.years;this.members=metadata.members;this.versions=metadata.versions;
      this.rememberBootstrap();
    },error:e=>this.fail(e)});
  }
  private applyBootstrap(bootstrap:ReadinessBootstrap) {
    this.bootstrapContext=bootstrap.context;this.canManage=bootstrap.context.canManage;
    this.years=bootstrap.years;this.members=bootstrap.members;this.versions=bootstrap.versions;
    if(!this.initialized) this.startRoute(bootstrap.context.academicYearId || bootstrap.years[0]?.id || 0);
  }
  private rememberBootstrap() {
    if(this.bootstrapContext && this.years.length && this.versions.length)
      this.api.rememberBootstrap({context:this.bootstrapContext,years:this.years,members:this.members,versions:this.versions});
  }
  private startRoute(year:number) {
    this.filter.academicYearId=year;this.initialized=true;
    combineLatest([this.route.paramMap,this.route.queryParamMap]).pipe(takeUntil(this.destroyed)).subscribe(([params,q])=>{
      const previousFilter=this.lastRouteFilter;
      const previousKey=previousFilter && this.routeFilterKey(previousFilter);
      this.filter={academicYearId:Number(q.get('academicYearId') || this.filter.academicYearId),templateVersion:Number(q.get('templateVersion') || 1),
        domainCode:q.get('domainCode') || undefined,standardCode:params.get('code') || q.get('standardCode') || undefined,responsibleUserId:q.get('responsibleUserId') || undefined,
        importance:q.get('importance')?Number(q.get('importance')):undefined,status:q.get('status') || undefined,search:q.get('search') || undefined,
        criticalOnly:q.get('criticalOnly')==='true',hideCompleted:q.get('hideCompleted')==='true',gapsOnly:this.mode==='gaps',trackerOnly:this.mode==='tracker',page:Number(q.get('page') || 1),pageSize:25};
      this.grouping=q.get('view') || 'table';this.cards=q.get('cards')==='true';
      const nextKey=this.routeFilterKey(this.filter);this.lastRouteFilter={...this.filter};
      if(previousKey===nextKey && (this.rowPage || this.filePage || this.mode==='manual')) return;
      this.reload(false,!previousFilter || previousFilter.academicYearId!==this.filter.academicYearId || previousFilter.templateVersion!==this.filter.templateVersion);
    });
  }
  private routeFilterKey(f:EvaluationFilter) {return JSON.stringify([f.academicYearId,f.templateVersion,f.domainCode,f.standardCode,f.responsibleUserId,f.importance,f.status,f.search,f.criticalOnly,f.hideCompleted,f.gapsOnly,f.trackerOnly,f.page,f.pageSize]);}
  private summaryFilter():EvaluationFilter {
    return {academicYearId:this.filter.academicYearId,templateVersion:this.filter.templateVersion,criticalOnly:false,hideCompleted:false,trackerOnly:false,gapsOnly:false,page:1,pageSize:25};
  }
  searchChanged() {this.searchChanges.next(this.filter.search || '');}
  reload(preserveError=false,forceSummary=true) {
    if(!this.filter.academicYearId) {this.busy=false;return;}
    this.load?.unsubscribe();this.load=new Subscription();this.busy=true;if(!preserveError)this.error='';
    const summaryFilter=this.summaryFilter();
    const cachedSummary=this.api.peekSummary(summaryFilter);
    const saved=this.api.peekView(this.mode,this.filter);
    if(saved) this.showSnapshot(saved);
    else {
      const shared=this.api.peekSharedView(this.filter);
      if(shared) {this.template=shared.template;this.summary=shared.summary;}
      if(cachedSummary) this.summary=cachedSummary;
      else if(this.summary && (this.summary.academicYearId!==this.filter.academicYearId || this.summary.templateVersion!==this.filter.templateVersion))
        {this.rows=[];this.files=[];this.summary=undefined;this.manuals=[];}
      this.rowPage=undefined;this.criticalPage=undefined;this.filePage=undefined;
    }
    if(cachedSummary) this.summary=cachedSummary;
    const needsRows=this.mode!=='manual' && this.mode!=='digital-index';
    const needsFiles=this.mode==='digital-index';
    const needsManuals=this.mode==='manual';
    const refreshSummary=forceSummary || !cachedSummary;
    const summaryBlocking=!cachedSummary;
    let pending=1+Number(summaryBlocking)+Number(needsRows)+Number(needsFiles)+Number(needsManuals);
    let failed=false;
    let blocked=false;
    const complete=()=>{if(--pending===0){this.busy=false;if(!failed){this.disabled=false;this.rememberView();}}};
    const fail=(e:any)=>{failed=true;blocked=e?.status===401 || e?.status===403;this.fail(e);if(blocked)this.busy=false;else complete();};
    this.load.add(this.api.template(this.filter.templateVersion).subscribe({next:value=>{this.template=value;complete();},error:fail}));
    if(blocked)return;
    if(refreshSummary) {
      this.load.add(this.api.readiness(summaryFilter).subscribe({next:value=>{this.summary=value;this.api.rememberSummary(summaryFilter,value);if(summaryBlocking)complete();else this.rememberView();},error:e=>{if(summaryBlocking)fail(e);else this.fail(e);}}));
      if(blocked)return;
    }
    if(needsRows) this.load.add(this.api.requirements(this.filter,this.mode==='gaps').subscribe({next:value=>{
      this.rowPage=value;this.rows=value.items;this.total=value.total;complete();
    },error:fail}));
    if(needsFiles) this.load.add(this.api.index(this.filter).subscribe({next:value=>{
      this.filePage=value;this.files=value.items;this.total=value.total;complete();
    },error:fail}));
    if(needsManuals) {this.manualsLoading=true;this.load.add(this.api.manuals(this.filter.academicYearId,this.filter.templateVersion).subscribe({next:value=>{this.manuals=value;this.manualsLoading=false;complete();},error:e=>{this.manualsLoading=false;fail(e);}}));}
    this.criticalLoading=this.mode==='readiness' || this.mode==='reports' || this.mode==='standard';
    if(this.criticalLoading)
      this.load.add(this.api.requirements({...this.filter,status:undefined,hideCompleted:false,criticalOnly:true,page:1},true).subscribe({next:value=>{
        this.criticalPage=value;this.critical=value.items;this.criticalLoading=false;this.rememberView();
      },error:e=>{this.criticalLoading=false;this.fail(e);}}));
    if(this.mode==='readiness' || this.mode==='reports' || this.mode==='standard') {this.manualsLoading=true;this.load.add(this.api.manuals(this.filter.academicYearId,this.filter.templateVersion).subscribe({next:value=>{this.manuals=value;this.manualsLoading=false;this.rememberView();},error:e=>{this.manualsLoading=false;this.fail(e);}}));}
  }
  private rememberView() {
    if(!this.template || !this.summary || (this.mode!=='manual' && this.mode!=='digital-index' && !this.rowPage)) return;
    const emptyRows:StoragePage<EvaluationRequirement>={items:[],total:0,page:1,pageSize:25};
    const emptyFiles:StoragePage<DigitalIndexFile>={items:[],total:0,page:1,pageSize:25};
    this.api.rememberView(this.mode,{filter:{...this.filter},template:this.template,summary:this.summary,
      rows:this.rowPage || emptyRows,critical:this.criticalPage || emptyRows,manuals:this.manuals,files:this.filePage || emptyFiles});
  }
  private showSnapshot(snapshot:ReadinessViewSnapshot) {
    this.template=snapshot.template;this.summary=snapshot.summary;this.rows=snapshot.rows.items;this.critical=snapshot.critical.items;
    this.files=snapshot.files.items;this.total=this.mode==='digital-index'?snapshot.files.total:snapshot.rows.total;this.manuals=snapshot.manuals;
    this.rowPage=snapshot.rows;this.criticalPage=snapshot.critical;this.filePage=snapshot.files;
  }
  filtersChanged(reset=true) {if(!this.initialized)return;if(reset)this.filter.page=1;const code=this.route.snapshot.paramMap?.get('code');const path=code && code!==this.filter.standardCode?[this.filter.standardCode?'/school-manager/storage/standards/'+this.filter.standardCode:'/school-manager/storage/readiness']:[];this.router.navigate(path,{relativeTo:this.route,queryParams:{...this.filter,search:this.filter.search || null,view:this.grouping,cards:this.cards},replaceUrl:true});}
  domainChanged() {this.filter.standardCode=undefined;this.filtersChanged();}
  selectDomain(code:string) {this.filter.domainCode=this.filter.domainCode===code?undefined:code;this.filter.standardCode=undefined;this.filtersChanged();}
  resetDisplay() {const {academicYearId,templateVersion}=this.filter;this.filter={academicYearId,templateVersion,page:1,pageSize:25,criticalOnly:false,hideCompleted:false,trackerOnly:this.mode==='tracker',gapsOnly:this.mode==='gaps'};this.grouping='table';this.cards=false;this.filtersChanged();}
  navigate(mode:string) {return ['/school-manager/storage/'+mode];}
  navigationParams(mode:string) {return {...this.filter,gapsOnly:mode==='gaps',trackerOnly:mode==='tracker',page:1};}
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
  private fail(e:any) {this.error=e?.error?.message || e?.message || this.translate.instant('S4.ERROR');if(e.status===404)this.disabled=true;if(e.status===403 || e.status===401){this.api.forgetViews();this.load?.unsubscribe();this.busy=false;this.criticalLoading=false;this.manualsLoading=false;this.bootstrapContext=undefined;this.rows=[];this.files=[];this.critical=[];this.summary=undefined;this.template=undefined;this.manuals=[];this.members=[];this.canManage=false;this.editOpen=false;this.historyOpen=false;this.history=[];}}
  ngOnDestroy() {this.destroyed.next();this.destroyed.complete();this.load?.unsubscribe();}
}
