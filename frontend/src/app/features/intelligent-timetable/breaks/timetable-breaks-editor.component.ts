import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { SaveBellSchedule, ScheduleBreak, effectivePeriods } from '../../../core/models/bell-schedule.models';
import { breakCategories, breakErrors, effectiveBreaks, scheduleBreakIssues } from '../../../core/models/schedule-break.models';
import { ClearableSelectComponent } from '../../../shared/components/clearable-select/clearable-select.component';

@Component({
  selector: 'app-timetable-breaks-editor', standalone: true,
  imports: [CommonModule, FormsModule, ButtonModule, InputTextModule, ClearableSelectComponent],
  templateUrl: './timetable-breaks-editor.component.html',
  styleUrls: ['../timings/timetable-timings.component.css', './timetable-breaks-editor.component.css']
})
export class TimetableBreaksEditorComponent {
  @Input({ required: true }) draft!: SaveBellSchedule;
  @Input() activeDay = 0;
  @Input() disabled = false;
  @Input() selectedDays = new Set<number>();
  @Input() dayNames: string[] = [];
  readonly categories = breakCategories;
  error = '';
  get rows() { return effectiveBreaks(this.draft, this.activeDay); }
  get orderedRows() { return this.rows.map((row, index) => ({ row, index })).sort((a, b) => a.row.startLocalTime.localeCompare(b.row.startLocalTime)); }
  get periods() { return this.activeDay === 0 ? this.draft.defaultPeriods : effectivePeriods(this.draft, this.activeDay); }
  get validation() { return breakErrors(this.rows, this.periods); }
  get inherited() { return this.activeDay !== 0 && this.draft.days.find(x => x.day === this.activeDay)?.usesDefaultBreaks !== false; }
  get periodOptions() { return this.periods.map(x => ({ label: `الحصة ${x.sequence} · ${x.endLocalTime.slice(0, 5)}`, value: x.sequence })); }
  get appliedDays() { return this.activeDay ? [this.dayNames[this.activeDay]] : this.draft.days.filter(x => x.isStudyDay && x.usesDefaultBreaks !== false).map(x => this.dayNames[x.day]); }
  trackRow(_: number, item: { index: number }) { return item.index; }
  private editable() {
    if (!this.activeDay) return this.draft.defaultBreaks ??= [];
    const day = this.draft.days.find(x => x.day === this.activeDay)!;
    if (day.usesDefaultBreaks !== false) { day.breaks = structuredClone(this.draft.defaultBreaks ?? []); day.usesDefaultBreaks = false; }
    return day.breaks ??= [];
  }
  edit(index: number, field: keyof ScheduleBreak, value: string | null) {
    if (this.disabled) return;
    const row = this.editable()[index];
    if (field === 'category') row.category = value;
    else row[field] = value && field !== 'name' && value.length === 5 ? value + ':00' : value ?? '';
    this.error = '';
  }
  afterPeriod(index: number, sequence: number | null) {
    const period = this.periods.find(x => x.sequence === sequence);
    if (period) this.edit(index, 'startLocalTime', period.endLocalTime);
  }
  resize(count: number) {
    if (this.disabled || !Number.isInteger(count) || count < 0 || count === this.rows.length) return;
    if (count < this.rows.length && !window.confirm('هل تريد حذف الاستراحات الأخيرة من هذا التوقيت؟ ستبقى النسخ السابقة محفوظة.')) return;
    const rows = this.editable();
    rows.sort((a, b) => a.startLocalTime.localeCompare(b.startLocalTime));
    while (rows.length > count) rows.pop();
    while (rows.length < count) {
      const start = [...this.periods, ...rows].map(x => x.endLocalTime).sort().at(-1) ?? '07:00:00';
      const minutes = Number(start.slice(0, 2)) * 60 + Number(start.slice(3, 5)) + 15;
      rows.push({ name: '', category: null, startLocalTime: start, endLocalTime: minutes < 1440 ? `${String(Math.floor(minutes / 60)).padStart(2, '0')}:${String(minutes % 60).padStart(2, '0')}:00` : '' });
    }
  }
  remove(index: number) {
    if (this.disabled || !window.confirm(`هل تريد حذف الاستراحة «${this.rows[index].name}»؟ ستبقى النسخ السابقة محفوظة.`)) return;
    this.editable().splice(index, 1);
  }
  inherit() {
    if (this.disabled || !window.confirm('هل تريد استبدال استراحات هذا اليوم بالاستراحات الافتراضية؟')) return;
    const day = this.draft.days.find(x => x.day === this.activeDay)!;
    day.usesDefaultBreaks = true; day.breaks = [];
  }
  applyToDays() {
    if (this.disabled || !this.selectedDays.size) return;
    const candidate = structuredClone(this.draft);
    for (const day of candidate.days.filter(x => this.selectedDays.has(x.day) && x.isStudyDay)) {
      day.usesDefaultBreaks = false; day.breaks = structuredClone(this.rows);
    }
    const issues = scheduleBreakIssues(candidate).filter(x => this.selectedDays.has(x.day));
    if (issues.length) {
      const days = [...new Set(issues.map(x => this.dayNames[x.day]))];
      this.error = `تعذر التطبيق. يرجى مراجعة الأوقات في: ${days.join('، ')}.`;
      return;
    }
    if (!window.confirm(`سيتم استبدال الاستراحات في: ${[...this.selectedDays].map(x => this.dayNames[x]).join('، ')}. هل تريد التطبيق؟`)) return;
    for (const day of candidate.days.filter(x => this.selectedDays.has(x.day) && x.isStudyDay)) {
      const target = this.draft.days.find(x => x.day === day.day)!;
      target.usesDefaultBreaks = false; target.breaks = day.breaks;
    }
    this.error = ''; this.selectedDays.clear();
  }
}
