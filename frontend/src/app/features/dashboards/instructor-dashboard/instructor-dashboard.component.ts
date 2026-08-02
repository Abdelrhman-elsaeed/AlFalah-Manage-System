import { Component } from '@angular/core';
import { DashboardLiveComponent } from '../../../shared/components/dashboard-live/dashboard-live.component';
import { SchoolTimetableComponent } from '../../timetables/school-timetable.component';

@Component({
  selector: 'app-instructor-dashboard',
  standalone: true,
  imports: [DashboardLiveComponent, SchoolTimetableComponent],
  template: `
    <app-dashboard-live role="instructor" />
    <section class="teacher-timetable-dashboard">
      <app-school-timetable />
    </section>
  `,
  styles: [`
    :host { display: grid; gap: var(--space-5); }
    .teacher-timetable-dashboard {
      overflow: hidden;
      border: 1px solid var(--border);
      border-radius: var(--radius-lg);
      background: var(--bg-surface);
      box-shadow: var(--shadow-sm);
    }
  `]
})
export class InstructorDashboardComponent {}
