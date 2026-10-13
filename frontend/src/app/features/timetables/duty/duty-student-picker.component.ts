import { CommonModule } from '@angular/common';
import { Component, DestroyRef, EventEmitter, Input, Output, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { DialogModule } from 'primeng/dialog';
import { Subject, debounceTime, switchMap, catchError, of } from 'rxjs';
import { DutyExecutionService } from '../../../core/services/duty-execution.service';
import { DutyRoster, DutyStudent } from '../../../core/models/duty-execution.models';
import { ClearableSelectComponent } from '../../../shared/components/clearable-select/clearable-select.component';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';

@Component({ selector: 'app-duty-student-picker', standalone: true,
  imports: [CommonModule, FormsModule, TranslateModule, DialogModule, ClearableSelectComponent],
  templateUrl: './duty-student-picker.component.html', styleUrl: './duty-student-picker.component.css' })
export class DutyStudentPickerComponent {
  @Input({ required: true }) assignmentId = 0;
  @Input() school = '';
  @Input() recordedIds: number[] = [];
  @Output() chosen = new EventEmitter<DutyStudent>();
  readonly api = inject(DutyExecutionService); readonly translate = inject(TranslateService); readonly destroy = inject(DestroyRef);
  visible = false; search = ''; grade: number | null = null; classroom: number | null = null; page = 1;
  readonly roster = signal<DutyRoster | null>(null); readonly loading = signal(false); readonly error = signal('');
  readonly selected = signal<DutyStudent | null>(null); private readonly queries = new Subject<number>(); private sequence = 0;
  private returnFocus: HTMLElement | null = null; private confirming = false;
  readonly groups = computed(() => [...new Set(this.roster()?.students.items.map(x => x.classroomId) ?? [])]
    .map(id => ({ label: this.roster()?.students.items.find(x => x.classroomId === id)?.classroom, items: this.roster()?.students.items.filter(x => x.classroomId === id) ?? [] })));
  get dir() { return this.translate.currentLang === 'en' ? 'ltr' : 'rtl'; }
  get grades() { return [...new Set(this.roster()?.classes.map(x => x.grade) ?? [])].sort((a,b) => a-b).map(id => ({ id, label: `${this.translate.instant('DUTY_EXEC.GRADE')} ${id}` })); }
  get classes() { return this.roster()?.classes.filter(x => this.grade === null || x.grade === this.grade) ?? []; }
  constructor() {
    this.queries.pipe(debounceTime(240), switchMap(sequence => this.api.roster(this.assignmentId, this.search, this.grade, this.classroom, this.page)
      .pipe(catchError(error => { if (sequence === this.sequence) this.error.set(extractHttpErrorMessage(error) || this.translate.instant('DUTY_EXEC.SEARCH_ERROR')); return of(null); }),
        switchMap(response => of({ response, sequence })))), takeUntilDestroyed(this.destroy)).subscribe(({ response, sequence }) => {
        if (sequence !== this.sequence) return; this.loading.set(false);
        if (response?.isSuccess && response.data) this.roster.set(response.data);
        else if (response) this.error.set(response.message || this.translate.instant('DUTY_EXEC.SEARCH_ERROR'));
      });
    const viewport = window.visualViewport;
    const resize = () => document.documentElement.style.setProperty('--duty-picker-height', `${viewport?.height ?? window.innerHeight}px`);
    viewport?.addEventListener('resize', resize); resize();
    this.destroy.onDestroy(() => viewport?.removeEventListener('resize', resize));
  }
  open() { this.returnFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null; this.confirming = false; this.selected.set(null); this.visible = true; this.refresh(false); }
  refresh(reset = true) { if (reset) this.page = 1; this.loading.set(true); this.error.set(''); this.queries.next(++this.sequence); }
  selectClass(id: number | null) { this.classroom = id; this.refresh(); }
  confirm() { const student = this.selected(); if (!student || this.loading()) return; this.confirming = true; this.visible = false; this.chosen.emit(student); }
  focusSearch() { setTimeout(() => document.getElementById('duty-student-search')?.focus(), 30); }
  close() { this.visible = false; ++this.sequence; if (!this.confirming) requestAnimationFrame(() => { if (this.returnFocus?.isConnected) this.returnFocus.focus(); }); }
  escape(event: KeyboardEvent) { event.preventDefault(); event.stopPropagation(); this.visible = false; }
  track(_: number, student: DutyStudent) { return student.enrollmentId; }
}
