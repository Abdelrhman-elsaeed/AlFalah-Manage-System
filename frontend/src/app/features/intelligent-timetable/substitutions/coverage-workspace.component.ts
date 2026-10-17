import { Component, ViewChild } from '@angular/core';
import { DailySubstitutionsComponent } from './daily-substitutions.component';

@Component({
  selector: 'app-coverage-workspace', standalone: true, imports: [DailySubstitutionsComponent],
  template: '<app-daily-substitutions />', styles: [':host{display:block;min-width:0}']
})
export class CoverageWorkspaceComponent {
  @ViewChild(DailySubstitutionsComponent) swaps?: DailySubstitutionsComponent;
  canLeave() { return !this.swaps?.busy; }
}
