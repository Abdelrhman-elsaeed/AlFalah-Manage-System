import { CommonModule } from '@angular/common';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { DialogModule } from 'primeng/dialog';
import { CalendarModule } from 'primeng/calendar';
import { interval } from 'rxjs';
import { DutyExecutionService } from '../../../core/services/duty-execution.service';
import { SchoolDutyService } from '../../../core/services/school-duty.service';
import { DutyClass, DutyDismissalStatistics } from '../../../core/models/duty-execution.models';
import { DutyTerm } from '../../../core/models/school-duty.models';
import { ClearableSelectComponent } from '../../../shared/components/clearable-select/clearable-select.component';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';
import { AuthService } from '../../../core/services/auth.service';
@Component({ selector:'app-dismissal-statistics',standalone:true,imports:[CommonModule,FormsModule,RouterLink,TranslateModule,DialogModule,CalendarModule,ClearableSelectComponent],
 templateUrl:'./dismissal-statistics.component.html',styleUrl:'./dismissal-statistics.component.css' })
export class DismissalStatisticsComponent implements OnInit {
  private readonly api=inject(DutyExecutionService);private readonly duty=inject(SchoolDutyService);readonly translate=inject(TranslateService);
  private readonly destroy=inject(DestroyRef);
  private readonly auth=inject(AuthService);
  get canOpenRecord(){return this.auth.hasPermission('Student.View')&&(this.auth.hasRole('StudentAffairsOfficer')||this.auth.hasRole('SuperAdmin'));}
  readonly data=signal<DutyDismissalStatistics|null>(null);readonly todayCount=signal<number|null>(null);readonly loading=signal(false);readonly error=signal('');
  open=false;from:Date|null=null;to:Date|null=null;termId:number|null=null;classroomId:number|null=null;page=1;classes:DutyClass[]=[];terms:DutyTerm[]=[];private sequence=0;
  get dir(){return this.translate.currentLang==='en'?'ltr':'rtl';}
  get termOptions(){return this.terms.map(t=>({id:t.id,label:`${t.year} · ${this.translate.instant('DUTY_EXEC.TERM')} ${t.semester}`}));}
  ngOnInit(){this.refreshToday();interval(15000).pipe(takeUntilDestroyed(this.destroy)).subscribe(()=>{if(!document.hidden&&!this.open)this.refreshToday();});}
  refreshToday(){this.api.statistics({page:1}).subscribe({next:r=>{if(r.data)this.todayCount.set(r.data.uniqueStudents);},error:()=>{}});}
  show(){this.open=true;this.search();this.duty.context().subscribe({next:r=>{if(!r.data)return;this.terms=r.data.terms;const day=r.data.days.find(x=>x.date===r.data?.today)??r.data.days[0];if(day)this.api.roster(day.id,'',null,null,1).subscribe({next:roster=>this.classes=roster.data?.classes??[],error:()=>{}});},error:()=>{}});}
  iso(d:Date|null){return d?`${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`:undefined;}
  search(reset=true){if(reset)this.page=1;this.loading.set(true);this.error.set('');const sequence=++this.sequence;this.api.statistics({from:this.iso(this.from),to:this.iso(this.to),termId:this.termId,classroomId:this.classroomId,page:this.page}).subscribe({next:r=>{if(sequence!==this.sequence)return;this.loading.set(false);if(r.data&&r.isSuccess)this.data.set(r.data);else this.error.set(r.message||this.translate.instant('DUTY_EXEC.LOAD_ERROR'));},error:e=>{if(sequence!==this.sequence)return;this.loading.set(false);this.error.set(extractHttpErrorMessage(e)||this.translate.instant('DUTY_EXEC.LOAD_ERROR'));}});}
  clear(){this.from=null;this.to=null;this.termId=null;this.classroomId=null;this.search();}
}
