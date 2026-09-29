import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { PaginatorModule } from 'primeng/paginator';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { forkJoin } from 'rxjs';
import { GuardianEntryPermitDto, GuardianSummonDto } from '../../../core/models/guardian-self-service.models';
import { GuardianSelfServiceService } from '../../../core/services/guardian-self-service.service';

@Component({
  selector: 'app-guardian-activity',
  standalone: true,
  imports: [CommonModule, ButtonModule, PaginatorModule, ProgressSpinnerModule, TableModule, TagModule],
  templateUrl: './guardian-activity.component.html',
  styleUrl: './guardian-activity.component.css'
})
export class GuardianActivityComponent {
  private readonly api = inject(GuardianSelfServiceService);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly permits = signal<readonly GuardianEntryPermitDto[]>([]);
  readonly summons = signal<readonly GuardianSummonDto[]>([]);
  readonly selectedSummon = signal<GuardianSummonDto | null>(null);
  readonly pageSize = 10;
  readonly permitPage = signal(1);
  readonly permitTotal = signal(0);
  readonly summonPage = signal(1);
  readonly summonTotal = signal(0);

  constructor() { this.load(); }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    forkJoin({
      permits: this.api.listEntryPermits(this.permitPage(), this.pageSize),
      summons: this.api.listSummons(this.summonPage(), this.pageSize)
    }).subscribe({
      next: result => {
        this.loading.set(false);
        if (!result.permits.isSuccess || !result.summons.isSuccess) {
          this.error.set(result.permits.errors[0] ?? result.summons.errors[0] ?? 'تعذر تحميل السجل.');
          return;
        }
        this.permits.set(result.permits.data?.items ?? []);
        this.summons.set(result.summons.data?.items ?? []);
        this.permitTotal.set(result.permits.data?.totalCount ?? 0);
        this.summonTotal.set(result.summons.data?.totalCount ?? 0);
      },
      error: () => { this.loading.set(false); this.error.set('تعذر تحميل التصاريح والاستدعاءات.'); }
    });
  }

  changePermitPage(event: { first?: number }): void {
    this.permitPage.set(Math.floor((event.first ?? 0) / this.pageSize) + 1);
    this.load();
  }

  changeSummonPage(event: { first?: number }): void {
    this.summonPage.set(Math.floor((event.first ?? 0) / this.pageSize) + 1);
    this.load();
  }

  showSummon(id: number): void {
    this.api.getSummon(id).subscribe(response => {
      if (response.isSuccess && response.data) this.selectedSummon.set(response.data);
    });
  }
}
