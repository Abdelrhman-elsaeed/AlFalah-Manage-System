import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-main-manager-dashboard',
  standalone: true,
  imports: [CommonModule, TranslateModule],
  templateUrl: './main-manager-dashboard.component.html',
  styleUrls: ['./dashboard-card.css']
})
export class MainManagerDashboardComponent {
  authService = inject(AuthService);
}