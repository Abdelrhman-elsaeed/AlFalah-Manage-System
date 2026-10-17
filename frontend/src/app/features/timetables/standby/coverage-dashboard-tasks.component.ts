import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { CoverageNeed } from '../../../core/models/daily-coverage.models';
import { DailyCoverageService } from '../../../core/services/daily-coverage.service';

@Component({selector:'app-coverage-dashboard-tasks',standalone:true,imports:[CommonModule,RouterLink,TranslateModule],
template:`<section class="coverage-dashboard" *ngIf="items.length || error"><header><h2><i class="pi pi-bell"></i>{{ 'COVERAGE.MY_TASKS' | translate }}</h2><a routerLink="/standby-tasks">{{ 'COVERAGE.ALL_TASKS' | translate }}</a></header><p role="alert" *ngIf="error">{{ 'COVERAGE.TASKS_FAILED' | translate }} <button type="button" (click)="load()">{{ 'COVERAGE.REFRESH' | translate }}</button></p><a *ngFor="let n of items" class="task-row" routerLink="/standby-tasks" [queryParams]="{needId:n.id}"><strong>{{ n.classroom }} · {{ n.subject }}</strong><span>{{ n.date }} · {{ 'COVERAGE.PERIODS' | translate }} {{ n.periods.join('، ') }}</span><span class="state">{{ ('COVERAGE.STATE_'+n.state) | translate }}</span><i class="pi pi-angle-left"></i></a></section>`,
styles:[':host{display:block;min-width:0}.coverage-dashboard{background:white;border:1px solid #dce6dd;border-radius:14px;padding:20px;margin:24px 0}.coverage-dashboard header{display:flex;justify-content:space-between;align-items:center;gap:15px;margin-bottom:12px}.coverage-dashboard h2{font-size:17px;margin:0;color:#14583e;display:flex;gap:9px;align-items:center}.coverage-dashboard a{color:#14583e;text-decoration:none;font-size:12px}.task-row{display:flex;align-items:center;gap:16px;padding:14px 0;border-top:1px solid #e4ebe2}.task-row>span:nth-child(2){flex:1;color:#728177;font-size:11px}.state{background:#edf7f0;border-radius:6px;padding:5px 8px;font-size:10px}.coverage-dashboard button{font:inherit;border:0;background:none;color:#14583e;cursor:pointer}.coverage-dashboard p{font-size:12px;color:#9c3e2e}a:focus-visible,button:focus-visible{outline:3px solid #bc962f;outline-offset:3px}@media(max-width:600px){.coverage-dashboard{padding:14px}.task-row{flex-wrap:wrap;gap:10px}.task-row>span:nth-child(2){flex-basis:65%}}']})
export class CoverageDashboardTasksComponent {
  private readonly api=inject(DailyCoverageService);private readonly destroy=inject(DestroyRef);
  items:CoverageNeed[]=[];error=false;
  constructor(){this.load();}
  load(){this.error=false;this.api.tasks(1,5).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:r=>{if(r.isSuccess&&r.data)this.items=[...r.data.items].filter(n=>!n.submittedAt);else this.error=true;},error:()=>this.error=true});}
}
