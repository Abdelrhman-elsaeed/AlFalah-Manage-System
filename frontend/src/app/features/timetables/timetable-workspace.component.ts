import { CommonModule } from '@angular/common';
import { Component, ViewChild, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { AuthService } from '../../core/services/auth.service';
import { SchoolTimetableComponent } from './school-timetable.component';
import { SchoolDutyComponent } from './duty/school-duty.component';

@Component({
  selector: 'app-timetable-workspace', standalone: true,
  imports: [CommonModule, TranslateModule, SchoolTimetableComponent, SchoolDutyComponent],
  template: `
    <nav class="workspace-tabs" role="tablist" [attr.aria-label]="'DUTY.WORKSPACE' | translate">
      <button id="lessons-tab" role="tab" aria-controls="lessons-panel" [attr.aria-selected]="tab() === 'lessons'" [attr.tabindex]="tab() === 'lessons' ? 0 : -1" (click)="select('lessons')" (keydown)="onKey($event)"><i class="pi pi-table" aria-hidden="true"></i>{{ 'DUTY.LESSONS' | translate }}</button>
      <button *ngIf="showDuty" id="duty-tab" role="tab" aria-controls="duty-panel" [attr.aria-selected]="tab() === 'duty'" [attr.tabindex]="tab() === 'duty' ? 0 : -1" (click)="select('duty')" (keydown)="onKey($event)"><i class="pi pi-sun" aria-hidden="true"></i>{{ 'DUTY.TITLE' | translate }}<span class="tab-dot"></span></button>
    </nav>
    <div id="lessons-panel" role="tabpanel" aria-labelledby="lessons-tab" [hidden]="tab() !== 'lessons'"><app-school-timetable /></div>
    <div id="duty-panel" role="tabpanel" aria-labelledby="duty-tab" [hidden]="tab() !== 'duty'"><app-school-duty *ngIf="dutyOpened()" /></div>`,
  styles: [`:host{display:block;min-width:0}.workspace-tabs{display:flex;gap:8px;border-bottom:1px solid var(--border);margin-bottom:24px}.workspace-tabs button{min-height:50px;padding:12px 20px;display:flex;gap:10px;align-items:center;border:0;border-bottom:3px solid transparent;background:transparent;color:var(--text-muted);font:inherit;font-weight:700;cursor:pointer}.workspace-tabs button[aria-selected=true]{color:var(--brand-800);border-color:var(--brand-600)}.workspace-tabs button:focus-visible{outline:2px solid var(--brand-600);outline-offset:-3px}.tab-dot{width:6px;height:6px;background:var(--gold-600);border-radius:50%}[hidden]{display:none!important}@media(max-width:600px){.workspace-tabs{margin-bottom:16px}.workspace-tabs button{padding:10px 14px}}`]
})
export class TimetableWorkspaceComponent {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly translate = inject(TranslateService);
  readonly showDuty = inject(AuthService).roles().some(r => ['SchoolManager', 'Instructor'].includes(r));
  readonly tab = signal<'lessons' | 'duty'>(this.showDuty && this.route.snapshot.queryParamMap.get('tab') === 'duty' ? 'duty' : 'lessons');
  readonly dutyOpened = signal(this.tab() === 'duty');
  @ViewChild(SchoolTimetableComponent) lessons?: SchoolTimetableComponent;
  @ViewChild(SchoolDutyComponent) duty?: SchoolDutyComponent;
  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe(params => {
      const tab = this.showDuty && params.get('tab') === 'duty' ? 'duty' : 'lessons';
      this.tab.set(tab); if (tab === 'duty') this.dutyOpened.set(true);
    });
  }
  canLeave() { return !(this.lessons?.dirty() || this.lessons?.inlineSwap.active() || this.duty?.hasDraft()) || window.confirm(this.translate.instant('DUTY.LEAVE_DRAFT')); }
  select(tab: 'lessons' | 'duty') {
    if (tab === this.tab()) return;
    if (!this.canLeave()) return;
    this.tab.set(tab); if (tab === 'duty') this.dutyOpened.set(true);
    void this.router.navigate([], { relativeTo: this.route, queryParams: { tab: tab === 'duty' ? 'duty' : null }, queryParamsHandling: 'merge' });
  }
  onKey(event: KeyboardEvent) {
    if (!this.showDuty || !['ArrowRight','ArrowLeft','Home','End'].includes(event.key)) return;
    event.preventDefault(); this.select(event.key === 'Home' ? 'lessons' : event.key === 'End' ? 'duty' : this.tab() === 'duty' ? 'lessons' : 'duty');
    document.getElementById(this.tab() === 'duty' ? 'duty-tab' : 'lessons-tab')?.focus();
  }
}
