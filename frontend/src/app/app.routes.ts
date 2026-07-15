import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';
import { permissionGuard } from './core/guards/permission.guard';

export const routes: Routes = [
  // Default redirect
  { path: '', redirectTo: '/auth/school-login', pathMatch: 'full' },

  // Auth routes (no guard needed)
  {
    path: 'auth',
    children: [
      {
        path: 'school-login',
        loadComponent: () => import('./features/auth/school-login/school-login.component')
          .then(m => m.SchoolLoginComponent),
        title: 'تسجيل الدخول — مدارس الفلاح'
      },
      {
        path: 'main-manager-login',
        loadComponent: () => import('./features/auth/main-manager-login/main-manager-login.component')
          .then(m => m.MainManagerLoginComponent),
        title: 'دخول المدير العام — مدارس الفلاح'
      },
      { path: '', redirectTo: 'school-login', pathMatch: 'full' }
    ]
  },

  // ─── Authenticated area: wrapped in the shell layout ────────────────────
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./shared/layout/shell/shell.component')
      .then(m => m.ShellComponent),
    children: [
      // Main Manager routes
      {
        path: 'main-manager',
        canActivate: [roleGuard],
        data: { roles: ['MainManager', 'SuperAdmin'] },
        children: [
          {
            path: 'dashboard',
            loadComponent: () => import('./features/dashboards/main-manager-dashboard/main-manager-dashboard.component')
              .then(m => m.MainManagerDashboardComponent),
            title: 'لوحة المدير العام'
          },
          { path: '', redirectTo: 'dashboard', pathMatch: 'full' }
        ]
      },

      // School Manager routes
      {
        path: 'school-manager',
        canActivate: [roleGuard],
        data: { roles: ['SchoolManager', 'SuperAdmin'] },
        children: [
          {
            path: 'dashboard',
            loadComponent: () => import('./features/dashboards/school-manager-dashboard/school-manager-dashboard.component')
              .then(m => m.SchoolManagerDashboardComponent),
            title: 'لوحة مدير المدرسة'
          },
          { path: '', redirectTo: 'dashboard', pathMatch: 'full' }
        ]
      },

      // Moderator routes
      {
        path: 'moderator',
        canActivate: [roleGuard],
        data: { roles: ['Moderator', 'SuperAdmin'] },
        children: [
          {
            path: 'dashboard',
            loadComponent: () => import('./features/dashboards/moderator-dashboard/moderator-dashboard.component')
              .then(m => m.ModeratorDashboardComponent),
            title: 'لوحة المشرف'
          },
          { path: '', redirectTo: 'dashboard', pathMatch: 'full' }
        ]
      },

      // Instructor routes
      {
        path: 'instructor',
        canActivate: [roleGuard],
        data: { roles: ['Instructor'] },
        children: [
          {
            path: 'dashboard',
            loadComponent: () => import('./features/dashboards/instructor-dashboard/instructor-dashboard.component')
              .then(m => m.InstructorDashboardComponent),
            title: 'لوحة المعلم'
          },
          {
            path: 'reports',
            loadComponent: () => import('./features/visits/instructor-reports/instructor-reports.component')
              .then(m => m.InstructorReportsComponent),
            title: 'تقاريري — مدارس الفلاح'
          },
          {
            path: 'reports/:id',
            loadComponent: () => import('./features/visits/visit-detail/visit-detail.component')
              .then(m => m.VisitDetailComponent),
            title: 'تقرير الزيارة — مدارس الفلاح'
          },
          { path: '', redirectTo: 'dashboard', pathMatch: 'full' }
        ]
      },

      // ─── Phase 2: Schools (permission-gated) ────────────────────────────
      {
        path: 'schools',
        canActivate: [permissionGuard],
        data: { permissions: ['School.View'] },
        children: [
          {
            path: '',
            loadComponent: () => import('./features/schools/schools-list/schools-list.component')
              .then(m => m.SchoolsListComponent),
            title: 'المدارس — مدارس الفلاح'
          },
          {
            path: 'new',
            canActivate: [permissionGuard],
            data: { permissions: ['School.Create'] },
            loadComponent: () => import('./features/schools/school-form/school-form.component')
              .then(m => m.SchoolFormComponent),
            title: 'مدرسة جديدة — مدارس الفلاح'
          },
          {
            path: ':id/edit',
            canActivate: [permissionGuard],
            data: { permissions: ['School.Edit'] },
            loadComponent: () => import('./features/schools/school-form/school-form.component')
              .then(m => m.SchoolFormComponent),
            title: 'تعديل المدرسة — مدارس الفلاح'
          }
        ]
      },

      // ─── Teachers: manager-facing list and profile ─────────────────────
      {
        path: 'teachers',
        canActivate: [roleGuard, permissionGuard],
        data: {
          roles: ['SchoolManager', 'MainManager', 'SuperAdmin'],
          permissions: ['User.View']
        },
        children: [
          {
            path: '',
            loadComponent: () => import('./features/teachers/teachers-list/teachers-list.component')
              .then(m => m.TeachersListComponent),
            title: 'المعلمون — مدارس الفلاح'
          },
          {
            path: ':userId',
            loadComponent: () => import('./features/teachers/teacher-profile/teacher-profile.component')
              .then(m => m.TeacherProfileComponent),
            title: 'ملف المعلم — مدارس الفلاح'
          }
        ]
      },

      // ─── Phase 2: Users (permission-gated) ───────────────────────────────
      {
        path: 'users',
        canActivate: [permissionGuard],
        data: { permissions: ['User.View'] },
        children: [
          {
            path: '',
            loadComponent: () => import('./features/users/users-list/users-list.component')
              .then(m => m.UsersListComponent),
            title: 'المستخدمون — مدارس الفلاح'
          },
          {
            path: 'new',
            canActivate: [permissionGuard],
            data: { permissions: ['User.Create'] },
            loadComponent: () => import('./features/users/user-form/user-form.component')
              .then(m => m.UserFormComponent),
            title: 'مستخدم جديد — مدارس الفلاح'
          },
          {
            path: ':id/edit',
            canActivate: [permissionGuard],
            data: { permissions: ['User.Edit'] },
            loadComponent: () => import('./features/users/user-form/user-form.component')
              .then(m => m.UserFormComponent),
            title: 'تعديل المستخدم — مدارس الفلاح'
          }
        ]
      },

      // ─── Phase 2: User-School Roles (permission-gated) ───────────────────
      {
        path: 'user-school-roles',
        canActivate: [permissionGuard],
        data: { permissions: ['User.Edit'] },
        loadComponent: () => import('./features/user-school-roles/user-school-roles-list/user-school-roles-list.component')
          .then(m => m.UserSchoolRolesListComponent),
        title: 'تعيينات المستخدمين — مدارس الفلاح'
      },

      // ─── Phase 3: Rubric (permission-gated) ──────────────────────────────
      {
        path: 'rubric',
        canActivate: [permissionGuard],
        data: { permissions: ['Rubric.View'] },
        children: [
          {
            path: '',
            loadComponent: () => import('./features/rubric/rubric-viewer/rubric-viewer.component')
              .then(m => m.RubricViewerComponent),
            title: 'أداة التقييم — مدارس الفلاح'
          },
          {
            path: 'edit',
            canActivate: [permissionGuard],
            data: { permissions: ['Rubric.Manage'] },
            loadComponent: () => import('./features/rubric/rubric-editor/rubric-editor.component')
              .then(m => m.RubricEditorComponent),
            title: 'تعديل أداة التقييم — مدارس الفلاح'
          }
        ]
      },

      // ─── Phase 4: Visits (permission-gated) ────────────────────────────
      {
        path: 'visits',
        canActivate: [roleGuard, permissionGuard],
        data: {
          roles: ['SchoolManager', 'Moderator', 'MainManager', 'SuperAdmin'],
          permissions: ['Visit.View']
        },
        children: [
          {
            path: '',
            loadComponent: () => import('./features/visits/visits-list/visits-list.component')
              .then(m => m.VisitsListComponent),
            title: 'الزيارات — مدارس الفلاح'
          },
          {
            path: 'new',
            canActivate: [permissionGuard],
            data: { permissions: ['Visit.Create'] },
            loadComponent: () => import('./features/visits/visit-form/visit-form.component')
              .then(m => m.VisitFormComponent),
            title: 'زيارة جديدة — مدارس الفلاح'
          },
          {
            path: ':id',
            canActivate: [permissionGuard],
            data: { permissions: ['Visit.View'] },
            loadComponent: () => import('./features/visits/visit-detail/visit-detail.component')
              .then(m => m.VisitDetailComponent),
            title: 'تفاصيل الزيارة — مدارس الفلاح'
          },
          {
            path: ':id/edit',
            canActivate: [permissionGuard],
            data: { permissions: ['Visit.Edit'] },
            loadComponent: () => import('./features/visits/visit-form/visit-form.component')
              .then(m => m.VisitFormComponent),
            title: 'تعديل الزيارة — مدارس الفلاح'
          }
        ]
      },

      // ─── Phase 7: Improvement Plans (permission-gated) ──────────────────
      {
        path: 'visits/:visitId/improvement-plans',
        canActivate: [permissionGuard],
        data: { permissions: ['Plan.View'] },
        loadComponent: () => import('./features/improvement-plans/plan-list/plan-list.component')
          .then(m => m.PlanListComponent),
        title: 'خطط التحسين — مدارس الفلاح'
      },
      {
        path: 'improvement-plans/:id',
        canActivate: [permissionGuard],
        data: { permissions: ['Plan.View'] },
        loadComponent: () => import('./features/improvement-plans/plan-detail/plan-detail.component')
          .then(m => m.PlanDetailComponent),
        title: 'تفاصيل خطة التحسين — مدارس الفلاح'
      },

      {
        path: 'complaints',
        canActivate: [roleGuard, permissionGuard],
        data: { roles: ['SchoolManager', 'SuperAdmin'], permissions: ['Complaint.View'] },
        loadComponent: () => import('./features/complaints/complaints-list/complaints-list.component')
          .then(m => m.ComplaintsListComponent),
        title: 'الشكاوى والمراجعات — مدارس الفلاح'
      },

      // ─── Account Settings (Any authenticated user) ──────────────────────
      {
        path: 'account',
        children: [
          {
            path: 'settings',
            loadComponent: () => import('./features/account/account-settings/account-settings.component')
              .then(m => m.AccountSettingsComponent),
            title: 'إعدادات الحساب — مدارس الفلاح'
          },
          { path: '', redirectTo: 'settings', pathMatch: 'full' }
        ]
      },

      // General dashboard fallback (used when role can't be inferred at login)
      {
        path: 'dashboard',
        loadComponent: () => import('./features/dashboards/main-manager-dashboard/main-manager-dashboard.component')
          .then(m => m.MainManagerDashboardComponent)
      }
    ]
  },

  // Unauthorized (kept OUTSIDE the shell so it renders full-page)
  {
    path: 'unauthorized',
    loadComponent: () => import('./features/errors/unauthorized/unauthorized.component')
      .then(m => m.UnauthorizedComponent),
    title: 'غير مصرح'
  },

  // Wildcard — 404
  { path: '**', redirectTo: '/auth/school-login' }
];
