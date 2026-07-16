import { Component } from '@angular/core';
import { DashboardLiveComponent } from '../../../shared/components/dashboard-live/dashboard-live.component';

@Component({ selector: 'app-school-manager-dashboard', standalone: true, imports: [DashboardLiveComponent], template: '<app-dashboard-live role="school-manager" />' })
export class SchoolManagerDashboardComponent {}
