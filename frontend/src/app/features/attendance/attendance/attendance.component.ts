import { CommonModule, DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { InputTextModule } from 'primeng/inputtext';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastService } from '../../../core/services/toast.service';
import { AuthService } from '../../../core/services/auth.service';
import { AttendanceService } from '../../../core/services/attendance.service';
import { AttendanceRecordItem, AttendanceSheetRow, AttendanceStatus, MyAttendanceItem } from '../../../core/models/attendance.models';

@Component({
  selector: 'app-attendance',
  standalone: true,
  imports: [CommonModule, DatePipe, FormsModule, ButtonModule, CheckboxModule, InputTextModule, TableModule, TagModule],
  templateUrl: './attendance.component.html',
  styleUrls: ['./attendance.component.css']
})
export class AttendanceComponent implements OnInit {
  private readonly attendance = inject(AttendanceService);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);

  readonly canManage = computed(() => this.auth.hasPermission('Attendance.Manage'));
  readonly selectedDate = signal(this.today());
  readonly sheetRows = signal<AttendanceSheetRow[]>([]);
  readonly myAttendance = signal<MyAttendanceItem[]>([]);
  readonly attendanceRecords = signal<AttendanceRecordItem[]>([]);
  readonly recordName = signal('');
  readonly recordFromDate = signal('');
  readonly recordToDate = signal('');
  readonly sheetLoading = signal(false);
  readonly historyLoading = signal(false);
  readonly saving = signal(false);

  readonly statusOptions = [
    { label: 'حاضر', value: 1 as AttendanceStatus },
    { label: 'غائب', value: 2 as AttendanceStatus },
    { label: 'غائب بعذر', value: 3 as AttendanceStatus }
  ];

  ngOnInit(): void {
    if (this.canManage()) {
      this.loadSheet();
      this.loadRecords();
    } else {
      this.loadHistory();
    }
  }

  changeDate(value: string): void {
    this.selectedDate.set(value);
    if (this.canManage()) this.loadSheet();
  }

  loadSheet(): void {
    this.sheetLoading.set(true);
    this.attendance.getSheet(this.selectedDate()).subscribe({
      next: response => {
        if (response.isSuccess && response.data) {
          this.sheetRows.set(response.data.rows.map(row => ({ ...row, notes: row.notes ?? '' })));
        } else {
          this.toast.error('تعذر تحميل كشف الحضور', response.message || '');
        }
        this.sheetLoading.set(false);
      },
      error: error => {
        this.sheetLoading.set(false);
        this.toast.error('تعذر تحميل كشف الحضور', error?.error?.message || '');
      }
    });
  }

  saveSheet(): void {
    const rows = this.sheetRows();
    if (!rows.length || rows.some(row => row.status === null)) {
      this.toast.warn('أكمل حالة الحضور لجميع الموظفين', '');
      return;
    }

    this.saving.set(true);
    this.attendance.saveSheet({
      date: this.selectedDate(),
      entries: rows.map(row => ({
        userId: row.userId,
        status: row.status!,
        notes: row.notes?.trim() || null
      }))
    }).subscribe({
      next: response => {
        this.saving.set(false);
        if (response.isSuccess && response.data) {
          this.sheetRows.set(response.data.rows);
          this.toast.success('تم حفظ سجل الحضور', response.message || '');
          if (this.canManage()) this.loadRecords();
          else this.loadHistory();
        } else {
          this.toast.error('تعذر حفظ سجل الحضور', response.message || '');
        }
      },
      error: error => {
        this.saving.set(false);
        this.toast.error('تعذر حفظ سجل الحضور', error?.error?.message || '');
      }
    });
  }

  markEveryonePresent(): void {
    this.sheetRows.update(rows => rows.map(row => ({ ...row, status: 1 })));
  }

  setCheckpoint(row: AttendanceSheetRow, status: AttendanceStatus, checked: boolean): void {
    if (checked) {
      row.status = status;
    } else if (row.status === status) {
      row.status = null;
    }
  }

  loadHistory(): void {
    this.historyLoading.set(true);
    this.attendance.getMine().subscribe({
      next: response => {
        if (response.isSuccess && response.data) this.myAttendance.set(response.data);
        this.historyLoading.set(false);
      },
      error: error => {
        this.historyLoading.set(false);
        this.toast.error('تعذر تحميل سجل حضوري', error?.error?.message || '');
      }
    });
  }

  loadRecords(): void {
    let fromDate = this.recordFromDate();
    let toDate = this.recordToDate();
    // Date inputs are displayed in locale order under RTL. Normalize a
    // reversed range so Search never sends an invalid request.
    if (fromDate && toDate && fromDate > toDate) {
      [fromDate, toDate] = [toDate, fromDate];
      this.recordFromDate.set(fromDate);
      this.recordToDate.set(toDate);
    }
    this.historyLoading.set(true);
    this.attendance.getRecords(fromDate, toDate, this.recordName()).subscribe({
      next: response => {
        if (response.isSuccess && response.data) this.attendanceRecords.set(response.data);
        this.historyLoading.set(false);
      },
      error: error => {
        this.historyLoading.set(false);
        this.attendanceRecords.set([]);
        this.toast.error('ØªØ¹Ø°Ø± ØªØ­Ù…ÙŠÙ„ Ø³Ø¬Ù„ Ø§Ù„Ø­Ø¶ÙˆØ±', error?.error?.message || '');
      }
    });
  }

  clearRecordFilters(): void {
    this.recordName.set('');
    this.recordFromDate.set('');
    this.recordToDate.set('');
    this.loadRecords();
  }

  setDatePreset(preset: 'today' | 'week' | 'month'): void {
    const now = new Date();
    const format = (date: Date) => {
      const offset = date.getTimezoneOffset() * 60_000;
      return new Date(date.getTime() - offset).toISOString().slice(0, 10);
    };
    if (preset === 'today') {
      const value = format(now);
      this.recordFromDate.set(value); this.recordToDate.set(value);
    } else if (preset === 'month') {
      this.recordFromDate.set(format(new Date(now.getFullYear(), now.getMonth(), 1)));
      this.recordToDate.set(format(new Date(now.getFullYear(), now.getMonth() + 1, 0)));
    } else {
      const day = now.getDay();
      const sunday = new Date(now); sunday.setDate(now.getDate() - day);
      const thursday = new Date(sunday); thursday.setDate(sunday.getDate() + 4);
      this.recordFromDate.set(format(sunday)); this.recordToDate.set(format(thursday));
    }
    this.loadRecords();
  }

  exportRecords(): void {
    this.attendance.downloadRecordsPdf(this.recordFromDate(), this.recordToDate(), this.recordName()).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = `attendance-records-${new Date().toISOString().slice(0, 10)}.pdf`;
        anchor.click();
        URL.revokeObjectURL(url);
      },
      error: error => this.toast.error('تعذر تنزيل ملف PDF', error?.error?.message || '')
    });
  }

  private openPdfWindow(title: string, headers: string[], rows: string[][], fileName: string): void {
    const popup = window.open('', '_blank', 'width=1100,height=800');
    if (!popup) {
      this.toast.warn('يرجى السماح بالنوافذ المنبثقة لتصدير PDF', '');
      return;
    }
    const escape = (value: string) => value.replace(/[&<>"']/g, character => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;' }[character]!));
    const headerHtml = headers.map(header => `<th>${escape(header)}</th>`).join('');
    const bodyHtml = rows.map(row => `<tr>${row.map(cell => `<td>${escape(String(cell))}</td>`).join('')}</tr>`).join('');
    popup.document.write(`<!doctype html><html dir="rtl"><head><meta charset="utf-8"><title>${escape(fileName)}</title>
      <style>@page{size:A4 landscape;margin:14mm}*{box-sizing:border-box}body{font-family:Arial,"Tahoma",sans-serif;color:#173b35;margin:0;background:#fff}header{display:flex;align-items:center;gap:18px;border-bottom:3px solid #0f7f4f;padding-bottom:12px;margin-bottom:18px}header img{width:68px;height:68px;object-fit:contain}h1{font-size:22px;margin:0 0 5px;color:#075e54}p{margin:0;color:#66756f;font-size:12px}table{width:100%;border-collapse:collapse;font-size:12px}th{background:#dff1e9;color:#075e54;font-weight:700;padding:10px 8px;border:1px solid #b8d6c9}td{padding:9px 8px;border:1px solid #d5e1dc}tbody tr:nth-child(even){background:#f5faf7}.footer{margin-top:14px;color:#71827b;font-size:10px;text-align:left}@media print{.no-print{display:none}}</style></head><body>
      <header><img src="${window.location.origin}/assets/Logo.png" alt="شعار مدارس الفلاح"><div><h1>${escape(title)}</h1><p>مدارس الفلاح النموذجية • صادر من نظام إدارة الحضور • ${new Date().toLocaleDateString('ar-SA')}</p></div></header>
      <table><thead><tr>${headerHtml}</tr></thead><tbody>${bodyHtml || `<tr><td colspan="${headers.length}">لا توجد سجلات</td></tr>`}</tbody></table><div class="footer">عدد السجلات: ${rows.length}</div>
      <script>window.onload=()=>{window.focus();window.print();}</script></body></html>`);
    popup.document.close();
  }

  statusLabel(status: AttendanceStatus | null): string {
    return this.statusOptions.find(option => option.value === status)?.label ?? 'غير مسجل';
  }

  weekdayLabel(date: string): string {
    return new Intl.DateTimeFormat('ar-SA', { weekday: 'long' })
      .format(new Date(`${date}T12:00:00`));
  }

  statusSeverity(status: AttendanceStatus | null): 'success' | 'danger' | 'warning' | 'info' | 'secondary' {
    if (status === 1) return 'success';
    if (status === 2) return 'danger';
    if (status === 3) return 'info';
    return 'secondary';
  }

  private today(): string {
    const now = new Date();
    // The school week is Sunday–Thursday. On Friday/Saturday start on the
    // next Sunday so the secretary can use the page immediately.
    if (now.getDay() === 5) now.setDate(now.getDate() + 2);
    if (now.getDay() === 6) now.setDate(now.getDate() + 1);
    const offset = now.getTimezoneOffset() * 60_000;
    return new Date(now.getTime() - offset).toISOString().slice(0, 10);
  }
}
