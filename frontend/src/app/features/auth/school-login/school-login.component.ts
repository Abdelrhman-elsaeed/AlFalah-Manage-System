import { Component, OnInit, computed, signal, NO_ERRORS_SCHEMA } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { AuthService } from '../../../core/services/auth.service';
import { SchoolLookup } from '../../../core/models/auth.models';
import { ClearableSelectComponent } from '../../../shared/components/clearable-select/clearable-select.component';
import { AchievementsShowcaseComponent } from '../../../shared/components/achievements-showcase/achievements-showcase.component';

@Component({
  selector: 'app-school-login',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslateModule,
    RouterLink,
    ClearableSelectComponent,
    AchievementsShowcaseComponent
  ],
  templateUrl: './school-login.component.html',
  styleUrls: ['./school-login.component.css'],
  schemas: [NO_ERRORS_SCHEMA]
})
export class SchoolLoginComponent implements OnInit {
  loginForm: FormGroup;
  schools = signal<SchoolLookup[]>([]);
  loading = signal(false);
  schoolsLoading = signal(true);
  errorMessage = signal('');
  readonly schoolOptions = computed(() => this.schools().map(school => ({
    id: school.id,
    label: `${school.name} — ${school.city} (${this.translate.instant(`SCHOOLS.STAGE.${school.stage.toUpperCase()}`)})`
  })));

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private router: Router,
    private translate: TranslateService
  ) {
    this.loginForm = this.fb.group({
      schoolId: ['', Validators.required],
      username: ['', [Validators.required, Validators.minLength(3)]],
      password: ['', [Validators.required, Validators.minLength(6)]]
    });
  }

  ngOnInit(): void {
    this.loadSchools();
  }

  loadSchools(): void {
    this.schoolsLoading.set(true);
    this.authService.getSchools().subscribe({
      next: response => {
        if (response.isSuccess && response.data) {
          this.schools.set(response.data);
        }
        this.schoolsLoading.set(false);
      },
      error: () => {
        this.schoolsLoading.set(false);
        this.errorMessage.set(this.translate.instant('ERRORS.NETWORK_ERROR'));
      }
    });
  }

  onSubmit(): void {
    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.errorMessage.set('');

    const { schoolId, username, password } = this.loginForm.value;

    this.authService.schoolLogin({
      schoolId: Number(schoolId),
      username,
      password
    }).subscribe({
      next: response => {
        if (response.isSuccess && response.data) {
          this.redirectByRole(response.data.user.roles);
        } else {
          this.errorMessage.set(response.message || this.translate.instant('AUTH.ERROR_INVALID_CREDENTIALS'));
        }
        this.loading.set(false);
      },
      error: err => {
        const msg = err?.error?.message || this.translate.instant('AUTH.ERROR_INVALID_CREDENTIALS');
        this.errorMessage.set(msg);
        this.loading.set(false);
      }
    });
  }

  private redirectByRole(roles: string[]): void {
    if (roles.includes('SuperAdmin') || roles.includes('MainManager')) {
      this.router.navigate(['/main-manager/dashboard']);
    } else if (roles.includes('SchoolManager')) {
      this.router.navigate(['/school-manager/dashboard']);
    } else if (roles.includes('Secretary')) {
      this.router.navigate(['/attendance']);
    } else if (roles.includes('Moderator')) {
      this.router.navigate(['/moderator/dashboard']);
    } else if (roles.includes('Instructor')) {
      this.router.navigate(['/instructor/dashboard']);
    } else if (roles.includes('StudentAffairsOfficer')) {
      this.router.navigate(['/student-affairs/settings']);
    } else {
      this.router.navigate(['/dashboard']);
    }
  }

  get schoolIdControl() { return this.loginForm.get('schoolId'); }
  get usernameControl() { return this.loginForm.get('username'); }
  get passwordControl() { return this.loginForm.get('password'); }
}
