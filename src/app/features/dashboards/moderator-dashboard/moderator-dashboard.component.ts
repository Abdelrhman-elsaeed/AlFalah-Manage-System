import { Component } from '@angular/core';
import { DashboardLiveComponent } from '../../../shared/components/dashboard-live/dashboard-live.component';

@Component({ selector: 'app-moderator-dashboard', standalone: true, imports: [DashboardLiveComponent], template: '<app-dashboard-live role="moderator" />' })
export class ModeratorDashboardComponent {}
