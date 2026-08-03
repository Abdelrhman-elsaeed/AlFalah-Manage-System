import { CommonModule } from '@angular/common';
import { Component, ElementRef, OnDestroy, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Chart, ChartConfiguration, registerables } from 'chart.js';
import html2pdf from 'html2pdf.js';
import { finalize, forkJoin } from 'rxjs';
import { ToastService } from '../../core/services/toast.service';
import { StudentAnalyzerService } from '../../core/services/student-analyzer.service';
import {
  AnalyzeStudentRequest,
  StudentAnalyzerAnalysis,
  StudentAnalyzerDataPoint,
  StudentAnalyzerFile,
  StudentAnalyzerFileKind,
  StudentAnalyzerProvider,
  StudentAnalyzerReportListItem
} from '../../core/models/student-analyzer.models';
import {
  ColumnClassification,
  ParsedStudentData,
  StudentRecord,
  StudentValue,
  classifyAnalyzerColumns,
  parseManualStudentData,
  parseStudentFile
} from './student-data-parser';
import { renderStudentAnalysisMarkdown } from './student-analysis-markdown';

Chart.register(...registerables);

type AnalyzerView = 'library' | 'parsing' | 'columns' | 'students' | 'analyzing' | 'report';
type UnknownChoice = '' | 'grant' | 'deduction' | 'skip';

interface StudentWithStats extends StudentRecord {
  __grantTotal__: number;
  __deductionTotal__: number;
  __netScore__: number;
}

interface AnalysisSection {
  title: string;
  icon: string;
  body: string;
  bodyHtml: string;
  tone: string;
}

@Component({
  selector: 'app-student-analyzer',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './student-analyzer.component.html',
  styleUrls: ['./student-analyzer.component.css']
})
export class StudentAnalyzerComponent implements OnInit, OnDestroy {
  private readonly api = inject(StudentAnalyzerService);
  private readonly toast = inject(ToastService);
  private charts: Chart[] = [];

  @ViewChild('reportDocument') reportDocument?: ElementRef<HTMLElement>;
  @ViewChild('barChart') barCanvas?: ElementRef<HTMLCanvasElement>;
  @ViewChild('doughnutChart') doughnutCanvas?: ElementRef<HTMLCanvasElement>;

  readonly view = signal<AnalyzerView>('library');
  readonly capabilities = signal({ canAccess: true, canDelegate: false, canManageSettings: true, schoolId: null as number | null, schoolName: null as string | null });
  readonly parsingStatus = signal('');
  readonly busy = signal(false);
  readonly sourceFile = signal<StudentAnalyzerFile | null>(null);
  readonly parsedData = signal<ParsedStudentData | null>(null);
  readonly classification = signal<ColumnClassification>({ grants: [], deductions: [], unknown: [] });
  readonly selectedGrants = signal<ReadonlySet<string>>(new Set());
  readonly selectedDeductions = signal<ReadonlySet<string>>(new Set());
  readonly unknownChoices = signal<Record<string, UnknownChoice>>({});
  readonly students = signal<StudentWithStats[]>([]);
  readonly selectedStudent = signal<StudentWithStats | null>(null);
  readonly analysis = signal<StudentAnalyzerAnalysis | null>(null);
  readonly studentSearch = signal('');
  readonly studentSort = signal('name');

  readonly files = signal<StudentAnalyzerFile[]>([]);
  readonly fileTotal = signal(0);
  readonly filePage = signal(1);
  readonly filePageSize = 10;
  readonly fileSearch = signal('');
  readonly fileKind = signal<StudentAnalyzerFileKind | null>(null);
  readonly filesLoading = signal(false);

  readonly reports = signal<StudentAnalyzerReportListItem[]>([]);
  readonly reportTotal = signal(0);
  readonly reportPage = signal(1);
  readonly reportPageSize = 10;
  readonly reportSearch = signal('');
  readonly reportProvider = signal<StudentAnalyzerProvider | null>(null);
  readonly reportsLoading = signal(false);

  manualOpen = false;
  manualFormat: 'json' | 'csv' = 'json';
  manualData = '';
  manualError = '';

  readonly provider = StudentAnalyzerProvider;
  readonly fileKinds = StudentAnalyzerFileKind;

  readonly filteredStudents = computed(() => {
    const query = this.studentSearch().trim().toLowerCase();
    let list = query ? this.students().filter(student => this.studentName(student).toLowerCase().includes(query)) : [...this.students()];
    switch (this.studentSort()) {
      case 'deductions-desc': list.sort((a, b) => b.__deductionTotal__ - a.__deductionTotal__); break;
      case 'grants-desc': list.sort((a, b) => b.__grantTotal__ - a.__grantTotal__); break;
      case 'net-asc': list.sort((a, b) => a.__netScore__ - b.__netScore__); break;
      default: list.sort((a, b) => this.studentName(a).localeCompare(this.studentName(b), 'ar'));
    }
    return list;
  });

  readonly studentsWithIssues = computed(() => this.students().filter(student => student.__deductionTotal__ > 0).length);
  readonly fileTotalPages = computed(() => Math.max(1, Math.ceil(this.fileTotal() / this.filePageSize)));
  readonly reportTotalPages = computed(() => Math.max(1, Math.ceil(this.reportTotal() / this.reportPageSize)));
  readonly analysisSections = computed(() => this.splitAnalysis(this.analysis()?.analysisText ?? ''));

  ngOnInit(): void {
    forkJoin({ capabilities: this.api.capabilities(), files: this.loadFilesRequest(), reports: this.loadReportsRequest() }).subscribe({
      next: ({ capabilities, files, reports }) => {
        if (capabilities.data) this.capabilities.set(capabilities.data);
        if (files.data) { this.files.set(files.data.items); this.fileTotal.set(files.data.totalCount); }
        if (reports.data) { this.reports.set(reports.data.items); this.reportTotal.set(reports.data.totalCount); }
      }
    });
  }

  ngOnDestroy(): void { this.destroyCharts(); }

  onFileInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (file) this.acceptFile(file);
    input.value = '';
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    const file = event.dataTransfer?.files[0];
    if (file) this.acceptFile(file);
  }

  acceptFile(file: File): void {
    const extension = file.name.split('.').pop()?.toLowerCase();
    if (!extension || !['pdf', 'xlsx', 'xls', 'ods', 'csv'].includes(extension)) {
      this.toast.error('ملف غير مدعوم', 'استخدم PDF أو Excel أو CSV.');
      return;
    }
    if (file.size > 50 * 1024 * 1024) {
      this.toast.error('حجم الملف كبير', 'الحد الأقصى 50 ميجابايت.');
      return;
    }
    this.busy.set(true);
    this.api.upload(file).pipe(finalize(() => this.busy.set(false))).subscribe({
      next: response => {
        if (!response.data) return;
        this.sourceFile.set(response.data);
        this.parse(file);
        this.loadFiles();
      }
    });
  }

  openStoredFile(file: StudentAnalyzerFile): void {
    this.busy.set(true);
    this.api.fileContent(file.id).pipe(finalize(() => this.busy.set(false))).subscribe({
      next: blob => {
        this.sourceFile.set(file);
        this.parse(new File([blob], file.originalFileName, { type: file.contentType }));
      }
    });
  }

  private parse(file: File): void {
    this.view.set('parsing');
    this.parsingStatus.set('جاري تهيئة الملف...');
    parseStudentFile(file, message => this.parsingStatus.set(message)).then(data => {
      this.prepareParsedData(data);
    }).catch((error: unknown) => {
      this.view.set('library');
      const message = error instanceof Error ? error.message : 'حدث خطأ أثناء قراءة الملف.';
      this.toast.error('تعذر قراءة الملف', `${message} جرّب Excel أو الإدخال اليدوي إذا كان PDF صورة.`);
    });
  }

  private prepareParsedData(data: ParsedStudentData): void {
    this.parsedData.set(data);
    const classification = classifyAnalyzerColumns(data.headers);
    this.classification.set(classification);
    const saved = this.readSelections();
    this.selectedGrants.set(new Set(classification.grants.filter(column => !saved.grants || saved.grants.includes(column))));
    this.selectedDeductions.set(new Set(classification.deductions.filter(column => !saved.deductions || saved.deductions.includes(column))));
    this.unknownChoices.set({});
    this.view.set('columns');
  }

  submitManual(): void {
    this.manualError = '';
    if (!this.manualData.trim()) { this.manualError = 'الرجاء إدخال البيانات أولاً.'; return; }
    try {
      const data = parseManualStudentData(this.manualData.trim(), this.manualFormat);
      const csv = this.toCsv(data);
      const file = new File([`\uFEFF${csv}`], `إدخال_يدوي_${new Date().toISOString().slice(0, 10)}.csv`, { type: 'text/csv;charset=utf-8' });
      this.busy.set(true);
      this.api.upload(file).pipe(finalize(() => this.busy.set(false))).subscribe({
        next: response => {
          if (!response.data) return;
          this.sourceFile.set(response.data);
          this.manualOpen = false;
          this.prepareParsedData(data);
          this.loadFiles();
          this.toast.success('تم تحميل البيانات', `تم حفظ بيانات ${data.students.length} طالب.`);
        }
      });
    } catch (error: unknown) {
      this.manualError = error instanceof Error ? error.message : 'تنسيق البيانات غير صالح.';
    }
  }

  loadDemo(): void {
    const data = DEMO_DATA;
    const file = new File([`\uFEFF${this.toCsv(data)}`], 'بيانات_تجريبية.csv', { type: 'text/csv;charset=utf-8' });
    this.busy.set(true);
    this.api.upload(file).pipe(finalize(() => this.busy.set(false))).subscribe({
      next: response => {
        if (!response.data) return;
        this.sourceFile.set(response.data);
        this.prepareParsedData(data);
        this.loadFiles();
        this.toast.info('الوضع التجريبي', 'تم تحميل وحفظ البيانات التجريبية.');
      }
    });
  }

  toggleColumn(column: string, type: 'grant' | 'deduction', checked: boolean): void {
    const target = new Set(type === 'grant' ? this.selectedGrants() : this.selectedDeductions());
    checked ? target.add(column) : target.delete(column);
    if (type === 'grant') this.selectedGrants.set(target); else this.selectedDeductions.set(target);
    this.saveSelections();
  }

  setAll(type: 'grant' | 'deduction', selected: boolean): void {
    const columns = type === 'grant' ? this.classification().grants : this.classification().deductions;
    if (type === 'grant') this.selectedGrants.set(new Set(selected ? columns : []));
    else this.selectedDeductions.set(new Set(selected ? columns : []));
    this.saveSelections();
  }

  classifyUnknown(column: string, choice: UnknownChoice): void {
    this.unknownChoices.update(current => ({ ...current, [column]: choice }));
  }

  proceedToStudents(): void {
    const grants = [...this.selectedGrants()];
    const deductions = [...this.selectedDeductions()];
    for (const [column, choice] of Object.entries(this.unknownChoices())) {
      if (choice === 'grant') grants.push(column);
      if (choice === 'deduction') deductions.push(column);
    }
    if (!grants.length && !deductions.length) { this.toast.warn('اختيار الأعمدة', 'يجب اختيار عمود واحد على الأقل.'); return; }
    this.selectedGrants.set(new Set(grants));
    this.selectedDeductions.set(new Set(deductions));
    const data = this.parsedData();
    if (!data) return;
    this.students.set(data.students.map(student => this.withStats(student)));
    this.view.set('students');
  }

  analyzeStudent(student: StudentWithStats): void {
    const source = this.sourceFile();
    if (!source) return;
    this.selectedStudent.set(student);
    this.view.set('analyzing');
    const request: AnalyzeStudentRequest = {
      sourceFileId: source.id,
      studentName: this.studentName(student),
      grants: [...this.selectedGrants()].map(column => this.dataPoint(column, student[column])),
      deductions: [...this.selectedDeductions()].map(column => this.dataPoint(column, student[column]))
    };
    this.api.analyze(request).subscribe({
      next: response => {
        if (!response.data) return;
        this.showReport(response.data);
        this.loadReports();
        this.loadFiles();
      },
      error: () => this.view.set('students')
    });
  }

  openReport(item: StudentAnalyzerReportListItem): void {
    this.busy.set(true);
    this.api.report(item.id).pipe(finalize(() => this.busy.set(false))).subscribe({ next: response => response.data && this.showReport(response.data) });
  }

  private showReport(report: StudentAnalyzerAnalysis): void {
    this.analysis.set(report);
    this.view.set('report');
    setTimeout(() => this.renderCharts(report), 0);
  }

  back(): void {
    if (this.view() === 'report') this.view.set(this.students().length ? 'students' : 'library');
    else if (this.view() === 'students') this.view.set('columns');
    else if (this.view() === 'columns') this.view.set('library');
  }

  resetFlow(): void {
    this.destroyCharts();
    this.sourceFile.set(null); this.parsedData.set(null); this.students.set([]); this.analysis.set(null);
    this.view.set('library');
  }

  loadFiles(reset = false): void {
    if (reset) this.filePage.set(1);
    this.filesLoading.set(true);
    this.loadFilesRequest().pipe(finalize(() => this.filesLoading.set(false))).subscribe({ next: response => {
      if (response.data) { this.files.set(response.data.items); this.fileTotal.set(response.data.totalCount); }
    }});
  }

  loadReports(reset = false): void {
    if (reset) this.reportPage.set(1);
    this.reportsLoading.set(true);
    this.loadReportsRequest().pipe(finalize(() => this.reportsLoading.set(false))).subscribe({ next: response => {
      if (response.data) { this.reports.set(response.data.items); this.reportTotal.set(response.data.totalCount); }
    }});
  }

  changeFilePage(delta: number): void { this.filePage.set(Math.min(this.fileTotalPages(), Math.max(1, this.filePage() + delta))); this.loadFiles(); }
  changeReportPage(delta: number): void { this.reportPage.set(Math.min(this.reportTotalPages(), Math.max(1, this.reportPage() + delta))); this.loadReports(); }

  deleteFile(file: StudentAnalyzerFile): void {
    if (!confirm(`حذف الملف «${file.originalFileName}» وكل تقاريره؟`)) return;
    this.api.deleteFile(file.id).subscribe({ next: () => { this.toast.success('تم الحذف', 'تم حذف الملف وتقاريره.'); this.loadFiles(); this.loadReports(); } });
  }

  deleteReport(item: StudentAnalyzerReportListItem): void {
    if (!confirm(`حذف تقرير ${item.studentName}؟`)) return;
    this.api.deleteReport(item.id).subscribe({ next: () => { this.toast.success('تم الحذف', 'تم حذف تقرير التحليل.'); this.loadReports(); } });
  }

  async copyReport(): Promise<void> {
    const text = this.reportDocument?.nativeElement.innerText;
    if (!text) return;
    await navigator.clipboard.writeText(text);
    this.toast.success('تم النسخ', 'تم نسخ نص التقرير.');
  }

  async exportReport(): Promise<void> {
    const element = this.reportDocument?.nativeElement;
    const report = this.analysis();
    if (!element || !report) return;
    const safeName = report.studentName.replace(/[^\u0600-\u06FF\w\s]/g, '').trim().replace(/\s+/g, '_');
    this.toast.info('جاري التصدير', 'يتم الآن إنشاء ملف PDF...');
    await html2pdf().set({
      margin: [10, 12, 10, 12], filename: `تقرير_${safeName}_${new Date().toISOString().slice(0, 10)}.pdf`,
      image: { type: 'jpeg', quality: .97 }, html2canvas: { scale: 2, useCORS: true, letterRendering: true, logging: false, scrollX: 0, scrollY: 0 },
      jsPDF: { unit: 'mm', format: 'a4', orientation: 'portrait' }, pagebreak: { mode: ['avoid-all', 'css', 'legacy'], avoid: '.analysis-section' }
    } as any).from(element).save();
    this.toast.success('تم التصدير', 'تم إنشاء ملف PDF بنجاح.');
  }

  studentName(student: StudentRecord): string { return String(student['__name__'] || 'طالب'); }
  initials(name: string): string { return name.trim().split(/\s+/).filter(Boolean).slice(0, 2).map(part => part[0]).join('') || '؟'; }
  providerName(provider: StudentAnalyzerProvider): string { return provider === 1 ? 'Groq' : provider === 2 ? 'Gemini' : 'OpenRouter'; }
  kindName(kind: StudentAnalyzerFileKind): string { return kind === 1 ? 'PDF' : kind === 2 ? 'Excel' : 'CSV'; }
  formatBytes(size: number): string { return size < 1024 * 1024 ? `${(size / 1024).toFixed(1)} KB` : `${(size / 1024 / 1024).toFixed(1)} MB`; }
  numeric(value: StudentValue | undefined): number { const number = Number(value); return Number.isFinite(number) ? number : 0; }
  topIssues(student: StudentWithStats): { column: string; value: number }[] {
    return [...this.selectedDeductions()].map(column => ({ column, value: this.numeric(student[column]) })).filter(item => item.value > 0).sort((a, b) => b.value - a.value).slice(0, 3);
  }

  private loadFilesRequest() { return this.api.files({ page: this.filePage(), pageSize: this.filePageSize, search: this.fileSearch() || undefined, fileKind: this.fileKind() ?? undefined }); }
  private loadReportsRequest() { return this.api.reports({ page: this.reportPage(), pageSize: this.reportPageSize, search: this.reportSearch() || undefined, provider: this.reportProvider() ?? undefined }); }
  private withStats(student: StudentRecord): StudentWithStats {
    const grantTotal = [...this.selectedGrants()].reduce((sum, column) => sum + Math.max(0, this.numeric(student[column])), 0);
    const deductionTotal = [...this.selectedDeductions()].reduce((sum, column) => sum + Math.max(0, this.numeric(student[column])), 0);
    return { ...student, __grantTotal__: grantTotal, __deductionTotal__: deductionTotal, __netScore__: grantTotal - deductionTotal };
  }
  private dataPoint(column: string, value: StudentValue | undefined): StudentAnalyzerDataPoint {
    const text = value === undefined || value === null ? '0' : String(value);
    const numeric = Number(text);
    return { column, value: text, numericValue: Number.isFinite(numeric) ? numeric : null };
  }
  private readSelections(): { grants?: string[]; deductions?: string[] } {
    try { return JSON.parse(localStorage.getItem('studentAnalyzer.selectedColumns') || '{}') as { grants?: string[]; deductions?: string[] }; } catch { return {}; }
  }
  private saveSelections(): void {
    try { localStorage.setItem('studentAnalyzer.selectedColumns', JSON.stringify({ grants: [...this.selectedGrants()], deductions: [...this.selectedDeductions()] })); } catch { /* Restricted storage. */ }
  }
  private toCsv(data: ParsedStudentData): string {
    const escape = (value: StudentValue | undefined) => `"${String(value ?? '').replace(/"/g, '""')}"`;
    return [data.headers.map(escape).join(','), ...data.students.map(student => data.headers.map(header => escape(student[header])).join(','))].join('\r\n');
  }
  private splitAnalysis(text: string): AnalysisSection[] {
    const configs = [
      ['ملخص تنفيذي', '📋', 'summary'], ['التشخيص النفسي والسلوكي', '🧠', 'diagnosis'], ['المشكلات المحددة', '⚠️', 'problems'],
      ['خطة التدخل', '🎯', 'intervention'], ['أساليب التعلم', '📚', 'learning'], ['توصيات للأسرة', '👨‍👩‍👧', 'family'],
      ['توصيات للمعلمين', '👩‍🏫', 'teachers'], ['الخلاصة', '🏁', 'conclusion']
    ] as const;
    const matches = [...text.matchAll(/^(?:#{1,3}\s+(.+)|\*\*(.+?)\*\*)\s*$/gm)]
      .map(match => {
        const heading = (match[1] || match[2]).replace(/\*\*/g, '').trim();
        const normalizedHeading = heading.replace(/\s/g, '');
        const config = configs.find(item => normalizedHeading.includes(item[0].replace(/\s/g, '')));
        return config ? { match, heading, config } : null;
      })
      .filter((item): item is NonNullable<typeof item> => !!item);
    if (!matches.length) {
      return [{
        title: 'التحليل النفسي والتربوي',
        icon: '🤖',
        body: text,
        bodyHtml: renderStudentAnalysisMarkdown(text),
        tone: 'summary'
      }];
    }
    return matches.map((match, index) => {
      const start = (match.match.index ?? 0) + match.match[0].length;
      const end = matches[index + 1]?.match.index ?? text.length;
      const body = text.slice(start, end).trim();
      return {
        title: match.heading,
        icon: match.config[1],
        body,
        bodyHtml: renderStudentAnalysisMarkdown(body),
        tone: match.config[2]
      };
    });
  }

  private renderCharts(report: StudentAnalyzerAnalysis): void {
    this.destroyCharts();
    const grants = report.selectedData.grants.filter(item => (item.numericValue ?? 0) > 0).sort((a, b) => (b.numericValue ?? 0) - (a.numericValue ?? 0)).slice(0, 6);
    const deductions = report.selectedData.deductions.filter(item => (item.numericValue ?? 0) > 0).sort((a, b) => (b.numericValue ?? 0) - (a.numericValue ?? 0)).slice(0, 8);
    if (this.barCanvas) {
      const labels = [...grants.map(item => item.column), ...deductions.slice(0, 6).map(item => item.column)];
      const config: ChartConfiguration = { type: 'bar', data: { labels, datasets: [
        { label: 'منح', data: [...grants.map(item => item.numericValue ?? 0), ...deductions.slice(0, 6).map(() => 0)], backgroundColor: 'rgba(16,185,129,.72)', borderColor: '#10b981', borderWidth: 1, borderRadius: 5, barPercentage: .78, categoryPercentage: .82 },
        { label: 'خصم', data: [...grants.map(() => 0), ...deductions.slice(0, 6).map(item => item.numericValue ?? 0)], backgroundColor: 'rgba(239,68,68,.72)', borderColor: '#ef4444', borderWidth: 1, borderRadius: 5, barPercentage: .78, categoryPercentage: .82 }
      ]}, options: {
        indexAxis: 'y', responsive: true, maintainAspectRatio: false, locale: 'ar-EG',
        layout: { padding: { top: 4, right: 8, bottom: 4, left: 8 } },
        plugins: {
          legend: { position: 'top', align: 'center', rtl: true, textDirection: 'rtl', labels: { boxWidth: 12, boxHeight: 10, padding: 14, usePointStyle: true, pointStyle: 'rectRounded', font: { size: 11 } } },
          tooltip: { rtl: true, textDirection: 'rtl' }
        },
        scales: {
          x: { beginAtZero: true, ticks: { precision: 0, padding: 6, font: { size: 11 } }, grid: { color: 'rgba(100,116,139,.14)' } },
          y: { grid: { display: false }, ticks: { autoSkip: false, padding: 7, font: { size: 11 }, callback(value) { return wrapChartLabel(this.getLabelForValue(Number(value)), 16); } } }
        }
      } };
      this.charts.push(new Chart(this.barCanvas.nativeElement, config));
    }
    if (this.doughnutCanvas && deductions.length) this.charts.push(new Chart(this.doughnutCanvas.nativeElement, {
      type: 'doughnut',
      data: { labels: deductions.map(item => item.column), datasets: [{ data: deductions.map(item => item.numericValue ?? 0), backgroundColor: ['#ef4444', '#f97316', '#eab308', '#dc2626', '#b91c1c', '#fb923c', '#fbbf24', '#f87171'], borderColor: '#fff', borderWidth: 2, hoverOffset: 4 }] },
      options: {
        responsive: true, maintainAspectRatio: false, locale: 'ar-EG', cutout: '58%', radius: '86%',
        layout: { padding: { top: 2, right: 8, bottom: 4, left: 8 } },
        plugins: {
          legend: { position: 'bottom', align: 'center', rtl: true, textDirection: 'rtl', labels: { boxWidth: 10, boxHeight: 10, padding: 12, usePointStyle: true, pointStyle: 'circle', font: { size: 11 } } },
          tooltip: { rtl: true, textDirection: 'rtl' }
        }
      }
    }));
  }
  private destroyCharts(): void { this.charts.forEach(chart => chart.destroy()); this.charts = []; }
}

function wrapChartLabel(label: string, maxLineLength: number): string[] {
  const words = label.trim().split(/\s+/).filter(Boolean);
  if (words.length < 2 || label.length <= maxLineLength) return [label];

  const lines: string[] = [];
  for (const word of words) {
    const current = lines.at(-1);
    if (!current || `${current} ${word}`.length > maxLineLength) lines.push(word);
    else lines[lines.length - 1] = `${current} ${word}`;
  }
  return lines.slice(0, 2);
}

const DEMO_DATA: ParsedStudentData = {
  headers: ['اسم الطالب', 'المشاركة الفعالة', 'التعاون', 'الإبداع', 'انضباط الصلاة', 'العمل التطوعي', 'مشارك في أنشطة', 'عدم التفاعل', 'إثارة الفوضى', 'التأخر عن الحصص', 'عدم حل الواجب كلي', 'التأخر الصباحي', 'الهروب من الحصة', 'التحدث في الحصة', 'مخالفة الزي'],
  students: [
    { 'اسم الطالب': 'أحمد بن محمد العمري', 'المشاركة الفعالة': 3, 'التعاون': 2, 'الإبداع': 1, 'انضباط الصلاة': 0, 'العمل التطوعي': 1, 'مشارك في أنشطة': 2, 'عدم التفاعل': 5, 'إثارة الفوضى': 3, 'التأخر عن الحصص': 7, 'عدم حل الواجب كلي': 4, 'التأخر الصباحي': 8, 'الهروب من الحصة': 2, 'التحدث في الحصة': 6, 'مخالفة الزي': 1, '__name__': 'أحمد بن محمد العمري' },
    { 'اسم الطالب': 'خالد عبدالله السعيد', 'المشاركة الفعالة': 8, 'التعاون': 7, 'الإبداع': 5, 'انضباط الصلاة': 6, 'العمل التطوعي': 4, 'مشارك في أنشطة': 9, 'عدم التفاعل': 0, 'إثارة الفوضى': 0, 'التأخر عن الحصص': 1, 'عدم حل الواجب كلي': 0, 'التأخر الصباحي': 0, 'الهروب من الحصة': 0, 'التحدث في الحصة': 1, 'مخالفة الزي': 0, '__name__': 'خالد عبدالله السعيد' },
    { 'اسم الطالب': 'عمر فيصل النجدي', 'المشاركة الفعالة': 1, 'التعاون': 0, 'الإبداع': 0, 'انضباط الصلاة': 2, 'العمل التطوعي': 0, 'مشارك في أنشطة': 0, 'عدم التفاعل': 9, 'إثارة الفوضى': 7, 'التأخر عن الحصص': 12, 'عدم حل الواجب كلي': 11, 'التأخر الصباحي': 15, 'الهروب من الحصة': 5, 'التحدث في الحصة': 10, 'مخالفة الزي': 3, '__name__': 'عمر فيصل النجدي' },
    { 'اسم الطالب': 'محمد ناصر الغامدي', 'المشاركة الفعالة': 5, 'التعاون': 4, 'الإبداع': 3, 'انضباط الصلاة': 4, 'العمل التطوعي': 2, 'مشارك في أنشطة': 5, 'عدم التفاعل': 2, 'إثارة الفوضى': 1, 'التأخر عن الحصص': 3, 'عدم حل الواجب كلي': 2, 'التأخر الصباحي': 2, 'الهروب من الحصة': 0, 'التحدث في الحصة': 3, 'مخالفة الزي': 0, '__name__': 'محمد ناصر الغامدي' },
    { 'اسم الطالب': 'عبدالرحمن سعد القحطاني', 'المشاركة الفعالة': 10, 'التعاون': 9, 'الإبداع': 8, 'انضباط الصلاة': 10, 'العمل التطوعي': 7, 'مشارك في أنشطة': 11, 'عدم التفاعل': 0, 'إثارة الفوضى': 0, 'التأخر عن الحصص': 0, 'عدم حل الواجب كلي': 0, 'التأخر الصباحي': 0, 'الهروب من الحصة': 0, 'التحدث في الحصة': 0, 'مخالفة الزي': 0, '__name__': 'عبدالرحمن سعد القحطاني' }
  ]
};
