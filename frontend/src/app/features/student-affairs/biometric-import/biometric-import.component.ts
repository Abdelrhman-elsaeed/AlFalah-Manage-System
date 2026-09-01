import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { ProgressBarModule } from 'primeng/progressbar';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';
import {
  BiometricImportIssueDto,
  BiometricImportResultDto
} from '../../../core/models/daily-operations.models';
import { DailyOperationsService } from '../../../core/services/daily-operations.service';
import { downloadBlob } from '../../../core/utils/browser-download';

const MAX_ZAJEL_BYTES = 20 * 1024 * 1024;

@Component({
  selector: 'app-biometric-import',
  standalone: true,
  imports: [CommonModule, ButtonModule, CardModule, ProgressBarModule, TableModule, TagModule],
  templateUrl: './biometric-import.component.html',
  styleUrl: './biometric-import.component.css'
})
export class BiometricImportComponent {
  private readonly api = inject(DailyOperationsService);
  private readonly messages = inject(MessageService);

  readonly selectedFile = signal<File | null>(null);
  readonly result = signal<BiometricImportResultDto | null>(null);
  readonly uploading = signal(false);
  readonly dragging = signal(false);

  selectFromInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    input.value = '';
    if (file) this.acceptFile(file);
  }

  dragOver(event: DragEvent): void {
    event.preventDefault();
    if (!this.uploading()) this.dragging.set(true);
  }

  dragLeave(event: DragEvent): void {
    event.preventDefault();
    this.dragging.set(false);
  }

  drop(event: DragEvent): void {
    event.preventDefault();
    this.dragging.set(false);
    const files = event.dataTransfer?.files;
    if (!files?.length) return;
    if (files.length !== 1) {
      this.showValidation('اختر ملف Excel واحدًا فقط.');
      return;
    }
    this.acceptFile(files[0]);
  }

  clearFile(): void {
    if (this.uploading()) return;
    this.selectedFile.set(null);
    this.result.set(null);
  }

  upload(): void {
    const file = this.selectedFile();
    if (!file || this.uploading()) return;
    this.uploading.set(true);
    this.result.set(null);
    this.api.importZajel(file).subscribe({
      next: response => {
        this.uploading.set(false);
        if (!response.isSuccess || !response.data) {
          this.messages.add({
            severity: 'error',
            summary: 'تعذر الاستيراد',
            detail: response.errors[0] ?? response.message ?? 'لم يعالج الخادم ملف زاجل.'
          });
          return;
        }
        this.result.set(response.data);
        this.messages.add({
          severity: 'success',
          summary: 'اكتمل استيراد زاجل',
          detail: `تم تسجيل ${response.data.importedDelays} حالة تأخر، ووجدت ${response.data.unmatchedRows} صفوف غير مطابقة.`
        });
      },
      error: (error: HttpErrorResponse) => {
        this.uploading.set(false);
        this.messages.add({
          severity: 'error',
          summary: 'تعذر الاستيراد',
          detail: extractHttpErrorMessage(error) ?? 'تحقق من رؤوس الأعمدة والتواريخ وإعدادات وقت الحضور.'
        });
      }
    });
  }

  issueLabel(code: string): string {
    const labels: Record<string, string> = {
      MissingNationalId: 'رقم الهوية مفقود',
      StudentNotFound: 'لا يوجد طالب نشط مطابق',
      EnrollmentNotFound: 'لا يوجد تسجيل نشط في تاريخ البصمة'
    };
    return labels[code] ?? code;
  }

  async copyIssues(): Promise<void> {
    const text = this.result()?.issues
      .map(issue => `${issue.rowNumber}\t${this.issueLabel(issue.code)}\t${issue.message}`)
      .join('\n') ?? '';
    if (!text) return;
    try {
      await navigator.clipboard.writeText(`رقم الصف\tالرمز\tالرسالة\n${text}`);
      this.messages.add({ severity: 'success', summary: 'تم النسخ', detail: 'نُسخت تفاصيل الملاحظات.' });
    } catch {
      this.messages.add({ severity: 'warn', summary: 'تعذر النسخ', detail: 'لا يسمح المتصفح بالوصول إلى الحافظة.' });
    }
  }

  downloadIssues(): void {
    const issues = this.result()?.issues ?? [];
    if (!issues.length) return;
    const rows = [
      ['Excel row', 'Code', 'Localized code', 'Message'],
      ...issues.map(issue => [String(issue.rowNumber), issue.code, this.issueLabel(issue.code), issue.message])
    ];
    const csv = '\uFEFF' + rows.map(row => row.map(this.csvCell).join(',')).join('\r\n');
    downloadBlob(new Blob([csv], { type: 'text/csv;charset=utf-8' }), 'zajel-import-issues.csv');
  }

  formatSize(bytes: number): string {
    return bytes >= 1024 * 1024
      ? `${(bytes / (1024 * 1024)).toFixed(2)} MB`
      : `${Math.max(1, Math.round(bytes / 1024))} KB`;
  }

  private acceptFile(file: File): void {
    const extensionIsValid = file.name.toLocaleLowerCase('en').endsWith('.xlsx');
    if (!extensionIsValid) {
      this.showValidation('صيغة الملف غير صحيحة. المطلوب ملف .xlsx فقط.');
      return;
    }
    if (file.size === 0) {
      this.showValidation('الملف فارغ. اختر مصنفًا يحتوي على بيانات.');
      return;
    }
    if (file.size > MAX_ZAJEL_BYTES) {
      this.showValidation('حجم الملف يتجاوز الحد الأقصى 20 MB.');
      return;
    }
    this.selectedFile.set(file);
    this.result.set(null);
  }

  private showValidation(detail: string): void {
    this.messages.add({ severity: 'warn', summary: 'ملف غير صالح', detail });
  }

  private readonly csvCell = (value: string): string => {
    const formulaSafe = /^[=+\-@]/.test(value) ? `'${value}` : value;
    return `"${formulaSafe.replace(/"/g, '""')}"`;
  };
}
