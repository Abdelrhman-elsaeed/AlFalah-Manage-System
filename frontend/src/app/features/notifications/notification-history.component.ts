import { Component } from '@angular/core';
import { NotificationCenterComponent } from '../../shared/components/notification-center/notification-center.component';
@Component({ selector: 'app-notification-history', standalone: true, imports: [NotificationCenterComponent], template: `<app-notification-center [history]="true" />` })
export class NotificationHistoryComponent {}
