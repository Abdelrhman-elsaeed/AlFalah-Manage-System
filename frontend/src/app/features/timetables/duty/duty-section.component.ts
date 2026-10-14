import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { DUTY_WORKSPACE_NAVIGATION, DutySection } from './duty-workspace-navigation';

/** Route views share the parent's context/draft without duplicating duty submission state. */
@Component({ selector: 'app-duty-section', standalone: true, imports: [CommonModule],
  template: '<ng-container [ngTemplateOutlet]="workspace.pageTemplate(section)" />' })
export class DutySectionComponent implements OnInit {
  readonly workspace = inject(DUTY_WORKSPACE_NAVIGATION);
  readonly section = inject(ActivatedRoute).snapshot.data['section'] as DutySection;
  ngOnInit() { this.workspace.activateSection(this.section); }
  canLeave() { return this.workspace.canLeaveSection(); }
}
