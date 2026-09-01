import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CalendarModule } from 'primeng/calendar';
import { CardModule } from 'primeng/card';
import { TagModule } from 'primeng/tag';
import { extractHttpErrorMessage, readHttpErrorBody } from '../../../core/http/http-error-message';
import { DailyOperationsService } from '../../../core/services/daily-operations.service';
import { downloadBlob, fileNameFromResponse } from '../../../core/utils/browser-download';

const XLSX_CONTENT_TYPE = 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';

@Component({
  selector: 'app-noor-export',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, ButtonModule, CalendarModule, CardModule, TagModule],
  templateUrl: './noor-export.component.html',
  styleUrl: './noor-export.component.css'
})
export class NoorExportComponent {
  private readonly api = inject(DailyOperationsService);
  private readonly messages = inject(MessageService);
  private readonly destroyRef = inject(DestroyRef);

  readonly weekControl = new FormControl<Date>(this.currentWeekStart(), { nonNullable: true });
  readonly generating = signal(false);
  readonly batchId = signal<string | null>(null);
  readonly rowCount = signal<number | null>(null);
  private idempotencyKey: string | null = null;

  constructor() {
    this.weekControl.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.idempotencyKey = null;
      this.batchId.set(null);
      this.rowCount.set(null);
    });
  }

  get rangeLabel(): string {
    const start = this.weekControl.value;
    const end = new Date(start);
    end.setDate(end.getDate() + 6);
    const formatter = new Intl.DateTimeFormat('ar-SA', { dateStyle: 'medium' });
    return `${formatter.format(start)} — ${formatter.format(end)}`;
  }

  generate(): void {
    if (this.generating()) return;
    const weekStartsOn = this.toDateOnly(this.weekControl.value);
    const key = this.idempotencyKey ?? this.api.createIdempotencyKey();
    this.idempotencyKey = key;
    this.generating.set(true);

    this.api.exportNoor(weekStartsOn, key).subscribe({
      next: response => {
        this.generating.set(false);
        const blob = response.body;
        const contentType = (response.headers.get('Content-Type') ?? blob?.type ?? '').split(';')[0].trim().toLocaleLowerCase('en');
        if (!blob || contentType !== XLSX_CONTENT_TYPE) {
          void this.reportUnexpectedBlob(blob);
          return;
        }

        const batchId = response.headers.get('X-Noor-Batch-Id');
        const parsedCount = Number(response.headers.get('X-Noor-Row-Count'));
        const rowCount = Number.isFinite(parsedCount) ? parsedCount : 0;
        const fileName = fileNameFromResponse(response, `noor-absence-corrections-${weekStartsOn}.xlsx`);
        downloadBlob(blob, fileName);
        this.batchId.set(batchId);
        this.rowCount.set(rowCount);
        this.messages.add({
          severity: 'success',
          summary: 'تم إنشاء ملف نور',
          detail: `تم إنشاء ملف نور — ${rowCount} سجل${rowCount === 0 ? ' (ملف فارغ صالح)' : ''}`
        });
      },
      error: async (error: HttpErrorResponse) => {
        this.generating.set(false);
        await readHttpErrorBody(error);
        this.messages.add({
          severity: 'error',
          summary: 'تعذر إنشاء ملف نور',
          detail: extractHttpErrorMessage(error) ?? 'راجع أرقام الهوية في إدارة الطلاب ثم أعد المحاولة.'
        });
      }
    });
  }

  private async reportUnexpectedBlob(blob: Blob | null): Promise<void> {
    let detail = 'أعاد الخادم محتوى غير متوقع، لذلك لم يُنزّل كملف Excel.';
    if (blob) {
      try {
        const parsed = JSON.parse(await blob.text()) as { message?: string; errors?: string[] };
        detail = parsed.errors?.[0] ?? parsed.message ?? detail;
      } catch {
        // Keep the safe fallback; an unexpected body must never become an XLSX download.
      }
    }
    this.messages.add({ severity: 'error', summary: 'لم يتم تنزيل الملف', detail });
  }

  private currentWeekStart(): Date {
    const date = new Date();
    date.setHours(0, 0, 0, 0);
    date.setDate(date.getDate() - date.getDay());
    return date;
  }

  private toDateOnly(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
