import { Component, ViewChild, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { SchoolTimetableComponent } from './school-timetable.component';

@Component({
  selector: 'app-timetable-workspace', standalone: true,
  imports: [SchoolTimetableComponent],
  template: '<app-school-timetable />',
  styles: [':host{display:block;min-width:0}']
})
export class TimetableWorkspaceComponent {
  private readonly translate = inject(TranslateService);
  @ViewChild(SchoolTimetableComponent) lessons?: SchoolTimetableComponent;
  canLeave() {
    return !(this.lessons?.dirty() || this.lessons?.inlineSwap.active())
      || window.confirm(this.translate.instant('DUTY.LEAVE_DRAFT'));
  }
}
