import { CommonModule } from '@angular/common';
import { Component, Input, OnChanges, OnDestroy, inject } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { Subscription } from 'rxjs';
import { downloadBlob } from '../../core/utils/browser-download';
import { VisitArchive, VisitArchiveApiService } from './visit-archive-api.service';
@Component({selector:'app-visit-archive-badge',standalone:true,imports:[CommonModule,TranslateModule],template:`<div class="archive-badge" role="status"><span>{{ 'S5.TITLE' | translate }}: {{ (loading?'S5.LOADING':error?'S5.ERROR':archive?.revisions?.length?'S5.'+archive!.revisions[0].status:'S5.NO_RECORD') | translate }}</span><button *ngIf="archive?.revisions?.[0]?.isCurrent" type="button" (click)="download()" [disabled]="loading">{{ 'S5.DOWNLOAD' | translate }}</button></div>`,styles:[`.archive-badge{display:flex;flex-wrap:wrap;gap:10px;align-items:center;padding:10px;color:#0b5426;font-size:.9rem}.archive-badge button{border:1px solid #0f7132;border-radius:8px;background:#eaf5ee;color:#0b5426;padding:8px;cursor:pointer}`]})
export class VisitArchiveBadgeComponent implements OnChanges,OnDestroy {
  @Input() visitId=0;@Input() status=0;private readonly api=inject(VisitArchiveApiService);private request?:Subscription;archive?:VisitArchive;loading=false;error=false;
  ngOnChanges(){this.request?.unsubscribe();this.archive=undefined;this.error=false;if(!this.visitId)return;this.loading=true;this.request=this.api.get(this.visitId).subscribe({next:r=>{this.archive=r;this.loading=false;},error:e=>{this.loading=false;this.error=e.status!==404;this.archive=undefined;}});}
  download(){const r=this.archive?.revisions[0];if(!r?.isCurrent)return;this.loading=true;this.request=this.api.content(this.visitId,r.approvalRevision).subscribe({next:blob=>{downloadBlob(blob,`تقرير-زيارة-${this.visitId}-اعتماد-${r.approvalRevision}.pdf`);this.loading=false;},error:()=>{this.archive=undefined;this.loading=false;this.error=true;}});}
  ngOnDestroy(){this.request?.unsubscribe();}
}
