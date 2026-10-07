import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { CalendarModule } from 'primeng/calendar';
import { PaginatorModule } from 'primeng/paginator';
import { DialogModule } from 'primeng/dialog';
import { Subject, Subscription, takeUntil } from 'rxjs';
import { ClearableSelectComponent } from '../../shared/components/clearable-select/clearable-select.component';
import { downloadBlob } from '../../core/utils/browser-download';
import { ArchiveFilter, ArchiveOperations, ArchiveRevision, VisitArchive, VisitArchiveApiService } from './visit-archive-api.service';
import { StorageWorkspaceNavComponent } from './storage-workspace-nav.component';

@Component({selector:'app-visit-archive-page',standalone:true,imports:[CommonModule,FormsModule,RouterLink,TranslateModule,ButtonModule,CalendarModule,PaginatorModule,DialogModule,ClearableSelectComponent,StorageWorkspaceNavComponent],templateUrl:'./visit-archive-page.component.html',styleUrls:['./visit-archive-page.component.css']})
export class VisitArchivePageComponent implements OnInit,OnDestroy {
  private readonly api=inject(VisitArchiveApiService);private readonly route=inject(ActivatedRoute);private readonly router=inject(Router);private readonly translate=inject(TranslateService);
  private readonly destroyed=new Subject<void>();private load?:Subscription;
  filter:ArchiveFilter={page:1,pageSize:25};from?:Date;to?:Date;teachers:{userId:string;name:string}[]=[];
  rows:VisitArchive[]=[];total=0;busy=false;saving=false;error='';notice='';disabled=false;
  operations?:ArchiveOperations;operationsError='';
  reason='';recreateOpen=false;selected?:{visit:VisitArchive;revision:ArchiveRevision};
  readonly states=['Pending','Processing','Completed','RetryScheduled','NeedsAttention','MissingFromDrive'].map(value=>({label:'S5.'+value,value}));
  ngOnInit() {
    this.loadOperations();
    this.api.teachers().pipe(takeUntil(this.destroyed)).subscribe({next:r=>this.teachers=r,error:e=>this.fail(e)});
    this.route.queryParamMap.pipe(takeUntil(this.destroyed)).subscribe(q=>{
      this.filter={page:Number(q.get('page') || 1),pageSize:25,from:q.get('from') || undefined,to:q.get('to') || undefined,teacherId:q.get('teacherId') || undefined,status:q.get('status') || undefined};
      this.from=this.filter.from?new Date(this.filter.from):undefined;this.to=this.filter.to?new Date(this.filter.to):undefined;this.reload();
    });
  }
  loadOperations() {this.operations=undefined;this.operationsError='';this.api.operationsStatus().pipe(takeUntil(this.destroyed)).subscribe({
    next:status=>this.operations=status,
    error:e=>{if(e.status===401 || e.status===403)this.fail(e);else this.operationsError='تعذر قراءة حالة تشغيل الأرشيف. أعد المحاولة.';}
  });}
  reload() {this.load?.unsubscribe();this.busy=true;this.error='';this.rows=[];
    this.load=this.api.list(this.filter).pipe(takeUntil(this.destroyed)).subscribe({next:r=>{this.rows=r.items;this.total=r.total;this.busy=false;this.disabled=false;},error:e=>{this.busy=false;this.fail(e);}});
  }
  apply(page=1) {this.filter.page=page;this.filter.from=this.from?.toISOString();if(this.to){const end=new Date(this.to);end.setHours(23,59,59,999);this.filter.to=end.toISOString();}else this.filter.to=undefined;this.router.navigate([],{relativeTo:this.route,queryParams:this.filter,replaceUrl:true});}
  retry(visit:VisitArchive,revision:ArchiveRevision) {if(revision.status==='MissingFromDrive'){this.selected={visit,revision};this.reason='';this.recreateOpen=true;}else this.submitRetry(visit,revision);}
  recreate() {if(this.selected && this.reason.trim())this.submitRetry(this.selected.visit,this.selected.revision,true,this.reason.trim());}
  private submitRetry(visit:VisitArchive,revision:ArchiveRevision,recreate=false,reason?:string) {
    this.saving=true;this.api.retry(visit.visitId,revision.approvalRevision,recreate,reason).pipe(takeUntil(this.destroyed)).subscribe({next:()=>{this.saving=false;this.recreateOpen=false;this.selected=undefined;this.notice=this.translate.instant('S5.QUEUED');this.reload();},error:e=>{this.saving=false;this.fail(e);}});
  }
  download(visit:VisitArchive,revision:ArchiveRevision,versionId:number) {this.saving=true;this.api.content(visit.visitId,revision.approvalRevision,versionId).pipe(takeUntil(this.destroyed)).subscribe({next:blob=>{downloadBlob(blob,`تقرير-زيارة-${visit.visitId}-اعتماد-${revision.approvalRevision}.pdf`);this.saving=false;},error:e=>{this.saving=false;this.fail(e);}});}
  private fail(error:{status?:number}) {this.rows=[];this.total=0;this.notice='';this.selected=undefined;this.recreateOpen=false;
    if(error.status===401 || error.status===403){this.teachers=[];this.disabled=true;this.error=this.translate.instant('S5.REVOKED');}
    else if(error.status===404){this.disabled=true;this.error=this.translate.instant('S5.DISABLED');}
    else this.error=this.translate.instant(error.status===409?'S5.CONFLICT':'S5.ERROR');
  }
  ngOnDestroy(){this.destroyed.next();this.destroyed.complete();}
}
