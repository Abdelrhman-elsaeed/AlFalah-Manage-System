import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { ClearableSelectComponent } from '../../../shared/components/clearable-select/clearable-select.component';
import { ToastService } from '../../../core/services/toast.service';
import { SchoolsService } from '../../../core/services/schools.service';
import { UsersService } from '../../../core/services/users.service';
import { SchoolDetail, SchoolStage } from '../../../core/models/phase2.models';

@Component({
  selector: 'app-school-form',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, RouterLink, TranslateModule,
    ButtonModule, InputTextModule, ClearableSelectComponent
  ],
  templateUrl: './school-form.component.html',
  styleUrls: ['./school-form.component.css']
})
export class SchoolFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly schoolsService = inject(SchoolsService);
  private readonly usersService = inject(UsersService);
  private readonly toast = inject(ToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);

  form: FormGroup = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    stage: ['Primary', Validators.required],
    city: ['', [Validators.required, Validators.maxLength(100)]],
    locationDetails: ['', Validators.maxLength(500)],
    logoUrl: ['', Validators.maxLength(1000)],
    managerUserId: ['']
  });

  isEdit = signal(false);
  loading = signal(false);
  saving = signal(false);
  schoolId = signal<number | null>(null);

  readonly stageOptions = [
    { label: this.translate.instant('SCHOOLS.STAGE.PRIMARY'), value: 'Primary' },
    { label: this.translate.instant('SCHOOLS.STAGE.INTERMEDIATE'), value: 'Intermediate' },
    { label: this.translate.instant('SCHOOLS.STAGE.SECONDARY'), value: 'Secondary' }
  ];

  availableManagers = signal<{ userId: string; fullName: string }[]>([]);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    this.loadManagers();
    if (id) {
      this.isEdit.set(true);
      this.schoolId.set(Number(id));
      this.loadSchool(Number(id));
    }
  }

  loadSchool(id: number): void {
    this.loading.set(true);
    this.schoolsService.getById(id).subscribe({
      next: (response) => {
        if (response.isSuccess && response.data) {
          this.applyForm(response.data);
        }
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  applyForm(school: SchoolDetail): void {
    this.form.patchValue({
      name: school.name,
      stage: school.stage,
      city: school.city,
      locationDetails: school.locationDetails ?? '',
      logoUrl: school.logoUrl ?? '',
      managerUserId: school.managerUserId ?? ''
    });
  }

  loadManagers(): void {
    this.usersService.list({ role: 'SchoolManager', isActive: true, page: 1, pageSize: 100 }).subscribe({
      next: (response) => {
        if (response.isSuccess && response.data) {
          this.availableManagers.set(
            response.data.items.map(u => ({ userId: u.userId, fullName: `${u.fullName} (${u.username})` }))
          );
        }
      }
    });
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.value;
    const body = {
      name: v.name!.trim(),
      stage: v.stage as SchoolStage,
      city: v.city!.trim(),
      locationDetails: v.locationDetails?.trim() || undefined,
      logoUrl: v.logoUrl?.trim() || undefined,
      managerUserId: v.managerUserId || undefined
    };

    this.saving.set(true);

    const obs = this.isEdit()
      ? this.schoolsService.update(this.schoolId()!, body)
      : this.schoolsService.create({ ...body, isActive: false });

    obs.subscribe({
      next: (response) => {
        if (response.isSuccess) {
          // FIX 2 — newly created schools are inactive by default. Surface the
          // next-step guidance as an info toast so the user knows the school
          // won't appear in the login dropdown until a manager is assigned
          // and the school is activated.
          if (!this.isEdit()) {
            this.toast.info(
              this.translate.instant('SCHOOLS.AFTER_CREATE_TITLE'),
              this.translate.instant('SCHOOLS.AFTER_CREATE_DESC')
            );
          } else {
            this.toast.success(
              this.translate.instant('COMMON.SUCCESS'),
              response.message || this.translate.instant('SCHOOLS.SAVE_SUCCESS'));
          }
          this.router.navigate(['/schools']);
        }
        this.saving.set(false);
      },
      error: () => this.saving.set(false)
    });
  }

  get name() { return this.form.get('name'); }
  get stage() { return this.form.get('stage'); }
  get city() { return this.form.get('city'); }
}
