import { Component } from '@angular/core';
import { DashboardLiveComponent } from '../../../shared/components/dashboard-live/dashboard-live.component';

@Component({ selector: 'app-instructor-dashboard', standalone: true, imports: [DashboardLiveComponent], template: '<app-dashboard-live role="instructor" />' })
export class InstructorDashboardComponent {}
