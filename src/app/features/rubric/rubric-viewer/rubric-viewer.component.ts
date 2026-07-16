import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { ButtonModule } from 'primeng/button';
import { Router } from '@angular/router';
import { RubricService } from '../../../core/services/rubric.service';
import { RubricVersionDto, RubricDomainDto, RubricStandardDto } from '../../../core/models/rubric.models';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-rubric-viewer',
  standalone: true,
  imports: [CommonModule, TranslateModule, ProgressSpinnerModule, ButtonModule],
  templateUrl: './rubric-viewer.component.html',
  styleUrls: ['./rubric-viewer.component.css']
})
export class RubricViewerComponent implements OnInit {
  private readonly rubricService = inject(RubricService);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  readonly loading = signal(true);
  readonly version = signal<RubricVersionDto | null>(null);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.rubricService.getActive().subscribe({
      next: (res) => {
        if (res.isSuccess && res.data) {
          this.version.set(res.data);
        } else {
          this.version.set(null);
          this.toast.error(this.translate.instant('RUBRIC.LOAD_FAILED'), res.message || this.translate.instant('ERRORS.SERVER_ERROR'));
        }
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.error(this.translate.instant('RUBRIC.LOAD_FAILED'), this.translate.instant('ERRORS.NETWORK_ERROR'));
      }
    });
  }

  canManage(): boolean {
    return this.auth.hasPermission('Rubric.Manage');
  }

  goEdit(): void {
    this.router.navigate(['/rubric/edit']);
  }

  trackDomain(_idx: number, d: RubricDomainDto): number { return d.id; }
  trackStandard(_idx: number, s: RubricStandardDto): number { return s.id; }
}