import { Component } from '@angular/core';
import { DashboardLiveComponent } from '../../../shared/components/dashboard-live/dashboard-live.component';
import { CoverageDashboardTasksComponent } from '../../timetables/standby/coverage-dashboard-tasks.component';

@Component({ selector: 'app-moderator-dashboard', standalone: true, imports: [DashboardLiveComponent, CoverageDashboardTasksComponent], template: '<app-dashboard-live role="moderator" /><app-coverage-dashboard-tasks />' })
export class ModeratorDashboardComponent {}
