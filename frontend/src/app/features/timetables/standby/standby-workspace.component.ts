import { CommonModule } from '@angular/common';
import { Component, HostListener, ViewChild, inject } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { SchoolTimetableComponent } from '../school-timetable.component';
import { DailyCoverageLedgerComponent } from '../../intelligent-timetable/substitutions/daily-coverage-ledger.component';

@Component({
  selector: 'app-standby-workspace', standalone: true,
  imports: [CommonModule, RouterLink, TranslateModule, SchoolTimetableComponent, DailyCoverageLedgerComponent],
  templateUrl: './standby-workspace.component.html', styleUrl: './standby-workspace.component.css'
})
export class StandbyWorkspaceComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  @ViewChild(SchoolTimetableComponent) weekly?: SchoolTimetableComponent;
  @ViewChild(DailyCoverageLedgerComponent) daily?: DailyCoverageLedgerComponent;
  tab: 'weekly' | 'daily' = 'weekly';
  weeklyVisited = false;
  dailyVisited = false;
  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe(params => {
      const next = params.get('tab') === 'daily' || params.get('register') === 'true' ? 'daily' : 'weekly';
      if (next !== this.tab && this.busy) { void this.updateUrl(this.tab); return; }
      this.tab = next;
      if (next === 'weekly') this.weeklyVisited = true;
      else this.dailyVisited = true;
      if (params.get('register') === 'true' && this.daily) this.daily.registerOpen = true;
    });
  }
  get busy() { return !!this.weekly?.standbyBusy() || !!this.weekly?.saving() || (this.daily ? !this.daily.canLeave() : false); }
  select(tab: 'weekly' | 'daily') { if (!this.busy && tab !== this.tab) void this.updateUrl(tab); }
  private updateUrl(tab: 'weekly' | 'daily') {
    return this.router.navigate([], { relativeTo: this.route, queryParams: { tab: tab === 'daily' ? 'daily' : null, register: null }, queryParamsHandling: 'merge' });
  }
  openRegister() {
    if (this.busy) return;
    if (this.daily) { this.daily.registerOpen = true; this.select('daily'); }
    else void this.router.navigate([], { relativeTo: this.route, queryParams: { tab: 'daily', register: 'true' }, queryParamsHandling: 'merge' });
  }
  canLeave() {
    if (this.busy) return false;
    return !this.weekly?.hasPendingChanges() || window.confirm(this.translate.instant('STANDBY.LEAVE'));
  }
  @HostListener('window:beforeunload', ['$event']) protectInFlight(event: BeforeUnloadEvent) {
    if (this.busy) { event.preventDefault(); event.returnValue = ''; }
  }
}
