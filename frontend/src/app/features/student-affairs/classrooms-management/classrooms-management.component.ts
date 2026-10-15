import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { TableModule } from 'primeng/table';
import { finalize, map } from 'rxjs';
import { ClassroomCatalogService, ClassroomCatalog, ClassroomOption } from '../../../core/services/classroom-catalog.service';
import { AuthService } from '../../../core/services/auth.service';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';

@Component({
  selector: 'app-classrooms-management', standalone: true,
  imports: [CommonModule, FormsModule, ButtonModule, DialogModule, TableModule],
  templateUrl: './classrooms-management.component.html', styleUrl: './classrooms-management.component.css'
})
export class ClassroomsManagementComponent {
  readonly api = inject(ClassroomCatalogService);
  private readonly auth = inject(AuthService);
  readonly catalog = signal<ClassroomCatalog | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly success = signal('');
  readonly search = signal('');
  readonly grade = signal(1);
  readonly stage = signal<string>('Primary');
  readonly year = signal<number | undefined>(undefined);
  readonly editing = signal<ClassroomOption | null>(null);
  readonly dialog = signal(false);
  readonly formError = signal('');
  firstSection = 1;
  lastSection = 3;
  location = '';
  section = '';
  active = true;
  readonly stages = computed(() => [...new Set([this.catalog()?.context.stage ?? 'Primary', ...(this.catalog()?.items.map(c => c.stage) ?? [])])]);
  readonly grades = computed(() => Array.from({length: this.stage() === 'Primary' ? 6 : 3}, (_, i) => i + 1));
  readonly canManage = computed(() => !!this.catalog() && (this.catalog()!.context.isManager || this.auth.hasRole('SuperAdmin') || this.auth.hasRole('MainManager') ||
    this.auth.hasPermission('Classroom.Manage') || this.auth.hasPermission('StudentEnrollment.Manage')));
  readonly currentYear = computed(() => this.catalog()?.context.years.find(y => y.id === this.year())?.isActive === true);
  readonly rows = computed(() => (this.catalog()?.items ?? []).filter(c => c.stage === this.stage() && c.gradeLevel === this.grade() &&
    `${c.label} ${c.section} ${c.physicalLocation}`.toLocaleLowerCase('ar').includes(this.search().trim().toLocaleLowerCase('ar'))));
  readonly legacyCount = computed(() => this.catalog()?.items.filter(c => c.sectionNumber == null).length ?? 0);
  constructor() { this.load(); }
  count(grade: number): number { return this.catalog()?.items.filter(c => c.stage === this.stage() && c.gradeLevel === grade).length ?? 0; }
  load(): void {
    this.loading.set(true); this.error.set('');
    this.api.all(this.year()).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: data => { const initial = !this.catalog(); this.catalog.set(data); if (initial) this.stage.set(data.context.stage); if (!this.year()) this.year.set(data.context.years.find(y => y.isActive)?.id); },
      error: e => this.error.set(extractHttpErrorMessage(e) || 'تعذر تحميل الفصول. أعد المحاولة.')
    });
  }
  openCreate(): void {
    this.editing.set(null); this.formError.set(''); this.location = ''; this.firstSection = 1; this.lastSection = 3; this.dialog.set(true);
  }
  openEdit(row: ClassroomOption): void {
    this.editing.set(row); this.formError.set(''); this.location = row.physicalLocation; this.section = row.section;
    this.active = row.isActive; this.dialog.set(true);
  }
  save(): void {
    if (this.saving() || !this.canManage() || !this.currentYear()) return;
    if (!this.editing() && this.stage() !== this.catalog()?.context.stage) { this.formError.set('إضافة الفصول متاحة لمرحلة المدرسة الحالية فقط.'); return; }
    if (!this.location.trim()) { this.formError.set('حدّد الموقع الفعلي للفصول.'); return; }
    if (!this.editing() && (!Number.isInteger(this.firstSection) || !Number.isInteger(this.lastSection) ||
      this.firstSection < 1 || this.lastSection < this.firstSection || this.lastSection - this.firstSection >= 100)) {
      this.formError.set('اختر نطاقًا صحيحًا من شعبة إلى ١٠٠ شعبة.'); return;
    }
    this.saving.set(true); this.formError.set('');
    const row = this.editing();
    const request = row ? this.api.edit(row, this.section, this.active, this.location).pipe(map(r => ({isSuccess:r.isSuccess, message:r.message}))) :
      this.api.create(this.year()!, this.grade(), Array.from({length: this.lastSection - this.firstSection + 1}, (_, i) => `${this.firstSection + i}`), this.location).pipe(map(r => ({isSuccess:r.isSuccess, message:r.message})));
    request.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: result => {
        if (!result.isSuccess) { this.formError.set(result.message || 'تعذر حفظ الفصول.'); return; }
        this.dialog.set(false); this.success.set(row ? 'تم تحديث الفصل مع الاحتفاظ بارتباطاته.' : 'تمت إضافة الشعب. أصبحت متاحة للمعلمين مباشرة.'); this.load();
      }, error: e => this.formError.set(extractHttpErrorMessage(e) || 'تعذر الحفظ. حدّث البيانات ثم أعد المحاولة.')
    });
  }
}
