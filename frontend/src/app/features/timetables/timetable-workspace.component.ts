import { Component, ViewChild, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { ActivatedRoute } from '@angular/router';
import { SchoolTimetableComponent } from './school-timetable.component';

@Component({
  selector: 'app-timetable-workspace', standalone: true,
  imports: [SchoolTimetableComponent],
  template: '<app-school-timetable [standbyOnly]="standbyOnly" />',
  styles: [':host{display:block;min-width:0}']
})
export class TimetableWorkspaceComponent {
  readonly standbyOnly = inject(ActivatedRoute).snapshot.data['standbyOnly'] === true;
  private readonly translate = inject(TranslateService);
  @ViewChild(SchoolTimetableComponent) lessons?: SchoolTimetableComponent;
  canLeave() {
    if (this.lessons?.standbyBusy()) return false;
    return !this.lessons?.hasPendingChanges()
      || window.confirm(this.translate.instant('STANDBY.LEAVE'));
  }
}
