import { CommonModule } from '@angular/common';
import { Component,DestroyRef,Input,OnChanges,OnInit,inject,signal } from '@angular/core';
import { timer } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { DutyDashboard,SchoolDutyService } from '../../../core/services/school-duty.service';
import { DutyErrorDialogComponent } from './duty-error-dialog.component';
import { DutyErrorDirective } from './duty-error.directive';
import { DutyFeedbackService } from './duty-feedback.service';
@Component({selector:'app-duty-dashboard-summary',standalone:true,imports:[CommonModule,RouterLink,TranslateModule,DutyErrorDirective,DutyErrorDialogComponent],providers:[DutyFeedbackService],
 template:`<app-duty-error-dialog/><span hidden [dutyError]="error()" (dutyErrorDismissed)="error.set('')"></span>
 <section class="duty-dashboard-summary" *ngIf="!denied()" [attr.aria-busy]="loading()"><header><div><span>{{'DUTY_ADMIN.DASHBOARD_EYEBROW'|translate}}</span><h2>{{'DUTY_ADMIN.DASHBOARD_TITLE'|translate}}</h2></div><a routerLink="/school-duty">{{'DUTY_ADMIN.MANAGE_DUTY'|translate}}<i class="pi pi-arrow-left" aria-hidden="true"></i></a></header>
 <p *ngIf="loading()&&!data()" role="status">{{'DUTY.LOADING'|translate}}</p><button *ngIf="!loading()&&!data()" (click)="load()">{{'DUTY.REFRESH'|translate}}</button>
 <div class="summary-grid" *ngIf="data() as d"><article class="today-summary"><span class="today-label"><i class="pi pi-sun" aria-hidden="true"></i>{{'DUTY_ADMIN.TODAY_TEACHER'|translate}}</span><time><bdi>{{d.today|date:'yyyy/MM/dd'}}</bdi></time><h3>{{d.current?.teacher||('DUTY_ADMIN.NO_TODAY_DUTY'|translate)}}</h3><p *ngIf="d.current?.needsReview">{{'DUTY_ADMIN.NEEDS_REVIEW'|translate}}</p></article><article class="recent-summary"><div class="recent-heading"><strong>{{'DUTY_ADMIN.RECENT_WEEK'|translate}}</strong><small><bdi>{{d.from|date:'MM/dd'}} — {{d.to|date:'MM/dd'}}</bdi></small></div><div class="recent-days"><a *ngFor="let day of d.recent" routerLink="/school-duty/execution" [queryParams]="{execution:day.assignmentId,date:day.date,review:true}"><time><bdi>{{day.date|date:'MM/dd'}}</bdi></time><strong>{{day.teacher}}</strong><i class="pi pi-angle-left" aria-hidden="true"></i></a><p *ngIf="!d.recent.length">{{'DUTY_ADMIN.NO_RECENT_DUTY'|translate}}</p></div></article></div></section>`,
 styleUrls:['./duty-dashboard-summary.component.css']})
export class DutyDashboardSummaryComponent implements OnInit,OnChanges {
 @Input() schoolId:number|null=null;
 private sequence=0;
 private readonly api=inject(SchoolDutyService);private readonly destroy=inject(DestroyRef);
 readonly data=signal<DutyDashboard|null>(null);readonly loading=signal(false);readonly denied=signal(false);readonly error=signal('');
 ngOnChanges(){this.sequence++;this.data.set(null);this.error.set('');this.denied.set(false);if(this.schoolId)this.load();}
 ngOnInit(){timer(60000,60000).pipe(takeUntilDestroyed(this.destroy)).subscribe(()=>{if(!this.loading()&&!this.denied()&&document.visibilityState==='visible')this.load();});}
 load(){const sequence=++this.sequence;this.loading.set(true);this.api.dashboard().pipe(takeUntilDestroyed(this.destroy)).subscribe({next:r=>{if(sequence!==this.sequence)return;this.loading.set(false);if(r.isSuccess&&r.data)this.data.set(r.data);else this.error.set(r.message||'');},error:e=>{if(sequence!==this.sequence)return;this.loading.set(false);this.data.set(null);if(e.status===403||e.status===401)this.denied.set(true);else this.error.set(e.error?.message||e.message);}});}
}
