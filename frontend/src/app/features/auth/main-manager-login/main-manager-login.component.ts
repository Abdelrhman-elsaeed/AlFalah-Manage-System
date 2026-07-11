import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-main-manager-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, TranslateModule, RouterLink],
  templateUrl: './main-manager-login.component.html',
  styleUrls: ['./main-manager-login.component.css']
})
export class MainManagerLoginComponent {
  loginForm: FormGroup;
  loading = signal(false);
  errorMessage = signal('');

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private router: Router,
    private translate: TranslateService
  ) {
    this.loginForm = this.fb.group({
      username: ['', [Validators.required, Validators.minLength(3)]],
      password: ['', [Validators.required, Validators.minLength(6)]]
    });
  }

  onSubmit(): void {
    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.errorMessage.set('');

    const { username, password } = this.loginForm.value;

    this.authService.mainManagerLogin({ username, password }).subscribe({
      next: response => {
        if (response.isSuccess && response.data) {
          const roles = response.data.user.roles;
          if (roles.includes('SuperAdmin') || roles.includes('MainManager')) {
            this.router.navigate(['/main-manager/dashboard']);
          } else {
            this.errorMessage.set('هذا الحساب لا يملك صلاحية الوصول إلى لوحة المدير العام.');
          }
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

  get usernameControl() { return this.loginForm.get('username'); }
  get passwordControl() { return this.loginForm.get('password'); }
}
