import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextModule } from 'primeng/inputtext';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { finalize } from 'rxjs';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';
import { ClassroomDto, StudentStatsDto } from '../../../core/models/daily-operations.models';
import { DailyOperationsService } from '../../../core/services/daily-operations.service';

interface DropdownOption<T> {
  label: string;
  value: T;
}

@Component({
  selector: 'app-student-records',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ButtonModule,
    CardModule,
    DropdownModule,
    InputTextModule,
    ProgressSpinnerModule,
    TableModule,
    TagModule,
    TooltipModule
  ],
  templateUrl: './student-records.component.html',
  styleUrl: './student-records.component.css'
})
export class StudentRecordsComponent implements OnInit {
  private readonly api = inject(DailyOperationsService);
  private readonly router = inject(Router);

  readonly students = signal<readonly StudentStatsDto[]>([]);
  readonly classrooms = signal<readonly ClassroomDto[]>([]);
  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);

  readonly search = signal('');
  readonly selectedClassroomId = signal<number | null>(null);
  readonly selectedStatus = signal<boolean | null>(null);

  readonly classroomOptions = computed<DropdownOption<number | null>[]>(() => [
    { label: 'جميع الفصول', value: null },
    ...this.classrooms().map(c => ({ label: c.label, value: c.id }))
  ]);

  readonly statusOptions: DropdownOption<boolean | null>[] = [
    { label: 'جميع الحالات', value: null },
    { label: 'الطلاب النشطون فقط', value: true },
    { label: 'الطلاب غير النشطين', value: false }
  ];

  readonly filteredStudents = computed(() => {
    const q = this.search().trim().toLowerCase();
    const classId = this.selectedClassroomId();
    const status = this.selectedStatus();

    return this.students().filter(s => {
      if (status !== null && s.isActive !== status) return false;
      if (classId !== null && s.classroomId !== classId) return false;
      if (!q) return true;

      return (
        s.name.toLowerCase().includes(q) ||
        s.studentNumber.toLowerCase().includes(q) ||
        (s.identityNumber && s.identityNumber.toLowerCase().includes(q)) ||
        (s.classroomName && s.classroomName.toLowerCase().includes(q))
      );
    });
  });

  readonly totalClassrooms = signal(0);

  readonly kpiSummary = computed(() => {
    const list = this.filteredStudents();
    const totalStudents = list.length;
    const totalClassrooms = this.totalClassrooms() || this.classrooms().length;
    const totalAbsences = list.reduce((sum, s) => sum + s.totalAbsences, 0);
    const totalDelays = list.reduce((sum, s) => sum + s.totalDelays, 0);
    const totalExcuses = list.reduce((sum, s) => sum + s.totalExcuses, 0);
    const totalReferrals = list.reduce((sum, s) => sum + s.totalReferrals, 0);

    return {
      totalStudents,
      totalClassrooms,
      totalAbsences,
      totalDelays,
      totalExcuses,
      totalReferrals
    };
  });

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    this.api.getClassrooms().subscribe({
      next: res => {
        if (res.data?.items) {
          this.classrooms.set(res.data.items);
          if (!this.totalClassrooms()) {
            this.totalClassrooms.set(res.data.items.length);
          }
        }
      }
    });

    this.api.getStudentsStats({ pageSize: 500 })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: res => {
          if (res.isSuccess && res.data) {
            this.students.set(res.data.items);
            if (res.data.totalClassrooms) {
              this.totalClassrooms.set(res.data.totalClassrooms);
            }
          } else {
            this.errorMessage.set(res.message || 'تعذر تحميل سجل متابعة الطلاب');
          }
        },
        error: err => {
          this.errorMessage.set(extractHttpErrorMessage(err) ?? 'حدث خطأ أثناء تحميل سجل متابعة الطلاب');
        }
      });
  }

  setSearch(event: Event): void {
    const target = event.target as HTMLInputElement;
    this.search.set(target.value ?? '');
  }

  viewProfile(studentId: number): void {
    this.router.navigate(['/student-affairs/records', studentId]);
  }
}
