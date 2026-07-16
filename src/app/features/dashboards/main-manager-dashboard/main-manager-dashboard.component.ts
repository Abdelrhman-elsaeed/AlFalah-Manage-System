import { Component } from '@angular/core';
import { DashboardLiveComponent } from '../../../shared/components/dashboard-live/dashboard-live.component';

@Component({ selector: 'app-main-manager-dashboard', standalone: true, imports: [DashboardLiveComponent], template: '<app-dashboard-live role="main-manager" />' })
export class MainManagerDashboardComponent {}
