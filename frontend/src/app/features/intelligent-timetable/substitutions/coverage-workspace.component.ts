import { CommonModule } from '@angular/common';
import { Component, ViewChild, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { DailySubstitutionsComponent } from './daily-substitutions.component';
import { DailyCoverageLedgerComponent } from './daily-coverage-ledger.component';

@Component({ selector: 'app-coverage-workspace', standalone: true, imports: [CommonModule, TranslateModule, DailySubstitutionsComponent, DailyCoverageLedgerComponent],
  template: `<nav class="coverage-tabs"><button type="button" [class.active]="tab === 'coverage'" [attr.aria-pressed]="tab === 'coverage'" (click)="select('coverage')"><i class="pi pi-users"></i>{{ 'COVERAGE.TAB' | translate }}</button><button type="button" [class.active]="tab === 'swaps'" [attr.aria-pressed]="tab === 'swaps'" (click)="select('swaps')"><i class="pi pi-sync"></i>{{ 'COVERAGE.SWAPS' | translate }}</button></nav><app-daily-coverage-ledger *ngIf="tab === 'coverage'"/><app-daily-substitutions *ngIf="tab === 'swaps'"/>`,
  styles: [':host{display:block;min-width:0}.coverage-tabs{display:flex;gap:10px;flex-wrap:wrap;margin:16px 0}.coverage-tabs button{font:inherit;font-weight:700;display:flex;align-items:center;gap:10px;padding:12px 20px;background:#fff;color:#51665a;border:1px solid #d3dfd6;border-radius:11px;cursor:pointer}.coverage-tabs .active{background:#edf7ef;border-color:#187344;color:#14583e;box-shadow:inset 0 -3px #187344}.coverage-tabs button:focus-visible{outline:3px solid #bc962f;outline-offset:3px}'] })
export class CoverageWorkspaceComponent {
  @ViewChild(DailyCoverageLedgerComponent) ledger?: DailyCoverageLedgerComponent;
  canLeave(){return this.ledger?.canLeave() ?? true;}
  select(tab:'coverage'|'swaps'){if(this.canLeave())this.tab=tab;}
  tab: 'coverage' | 'swaps' = inject(ActivatedRoute).snapshot.queryParamMap.has('sourceEntryId') ? 'swaps' : 'coverage';
}
