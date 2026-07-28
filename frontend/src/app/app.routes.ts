import { inject } from '@angular/core';
import { ResolveFn, Routes } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';
import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';
import { permissionGuard } from './core/guards/permission.guard';

function translatedTitle(key: string): ResolveFn<string> {
  return () => inject(TranslateService).get(key);
}

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
        title: translatedTitle('ROUTE_TITLES.SCHOOL_LOGIN')
      },
      {
        path: 'main-manager-login',
        loadComponent: () => import('./features/auth/main-manager-login/main-manager-login.component')
          .then(m => m.MainManagerLoginComponent),
        title: translatedTitle('ROUTE_TITLES.MAIN_MANAGER_LOGIN')
      },
      { path: '', redirectTo: 'school-login', pathMatch: 'full' }
    ]
  },

  // ─── Authenticated area: wrapped in the shell layout ────────────────────
  {
    path: 'parent-survey/:token',
    loadComponent: () => import('./features/parent-surveys/public-parent-survey.component')
      .then(m => m.PublicParentSurveyComponent),
    title: 'استبيان أولياء الأمور'
  },

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
            title: translatedTitle('ROUTE_TITLES.MAIN_MANAGER_DASHBOARD')
          },
          {
            path: 'evidence-matrix',
            loadComponent: () => import('./features/evidence-matrix/evidence-matrix-page.component')
              .then(m => m.EvidenceMatrixPageComponent),
            title: 'مصفوفة متابعة الأدلة'
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
            title: translatedTitle('ROUTE_TITLES.SCHOOL_MANAGER_DASHBOARD')
          },
          {
            path: 'evidence-matrix',
            loadComponent: () => import('./features/evidence-matrix/evidence-matrix-page.component')
              .then(m => m.EvidenceMatrixPageComponent),
            title: 'مصفوفة متابعة الأدلة'
          },
          {
            path: 'evidence-settings',
            canActivate: [permissionGuard],
            data: { permissions: ['Settings.Manage'] },
            loadComponent: () => import('./features/schools/school-microsoft-drive-settings/school-microsoft-drive-settings.component').then(m => m.SchoolMicrosoftDriveSettingsComponent),
            title: 'إعدادات ملفات الإنجاز'
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
            title: translatedTitle('ROUTE_TITLES.MODERATOR_DASHBOARD')
          },
          {
            path: 'evidence-matrix',
            loadComponent: () => import('./features/evidence-matrix/evidence-matrix-page.component')
              .then(m => m.EvidenceMatrixPageComponent),
            title: 'مصفوفة متابعة الأدلة'
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
            title: translatedTitle('ROUTE_TITLES.INSTRUCTOR_DASHBOARD')
          },
          {
            path: 'reports',
            loadComponent: () => import('./features/visits/instructor-reports/instructor-reports.component')
              .then(m => m.InstructorReportsComponent),
            title: translatedTitle('ROUTE_TITLES.MY_REPORTS')
          },
          {
            path: 'reports/:id',
            loadComponent: () => import('./features/visits/visit-detail/visit-detail.component')
              .then(m => m.VisitDetailComponent),
            title: translatedTitle('ROUTE_TITLES.VISIT_REPORT')
          },
          {
            path: 'evidence-files',
            loadComponent: () => import('./features/teacher-evidence-files/pages/teacher-evidence-files-page/teacher-evidence-files-page.component')
              .then(m => m.TeacherEvidenceFilesPageComponent),
            title: 'ملفات الإنجاز'
          },
          { path: '', redirectTo: 'dashboard', pathMatch: 'full' }
        ]
      },

      // ─── Phase 2: Schools (permission-gated) ────────────────────────────
      {
        path: 'visit-reports/:id/preview',
        canActivate: [roleGuard, permissionGuard],
        data: {
          roles: ['Instructor', 'SchoolManager', 'Moderator', 'MainManager', 'SuperAdmin'],
          permissions: ['Visit.View']
        },
        loadComponent: () => import('./features/visits/report-preview/report-preview.component')
          .then(m => m.ReportPreviewComponent),
        title: translatedTitle('ROUTE_TITLES.VISIT_REPORT_PREVIEW')
      },
      {
        path: 'attendance',
        canActivate: [roleGuard, permissionGuard],
        data: {
          roles: ['Secretary', 'SchoolManager', 'Moderator', 'Instructor'],
          permissions: ['Attendance.View']
        },
        loadComponent: () => import('./features/attendance/attendance/attendance.component')
          .then(m => m.AttendanceComponent),
        title: 'الحضور والانصراف'
      },
      {
        path: 'parent-surveys',
        canActivate: [roleGuard, permissionGuard],
        data: {
          roles: ['SchoolManager', 'Moderator', 'SuperAdmin'],
          permissions: ['ParentSurvey.Manage']
        },
        loadComponent: () => import('./features/parent-surveys/parent-survey-admin.component')
          .then(m => m.ParentSurveyAdminComponent),
        title: 'نماذج أولياء الأمور'
      },
      {
        path: 'schools',
        canActivate: [permissionGuard],
        data: { permissions: ['School.View'] },
        children: [
          {
            path: '',
            loadComponent: () => import('./features/schools/schools-list/schools-list.component')
              .then(m => m.SchoolsListComponent),
            title: translatedTitle('ROUTE_TITLES.SCHOOLS')
          },
          {
            path: 'new',
            canActivate: [permissionGuard],
            data: { permissions: ['School.Create'] },
            loadComponent: () => import('./features/schools/school-form/school-form.component')
              .then(m => m.SchoolFormComponent),
            title: translatedTitle('ROUTE_TITLES.NEW_SCHOOL')
          },
          {
            path: ':id/edit',
            canActivate: [permissionGuard],
            data: { permissions: ['School.Edit'] },
            loadComponent: () => import('./features/schools/school-form/school-form.component')
              .then(m => m.SchoolFormComponent),
            title: translatedTitle('ROUTE_TITLES.EDIT_SCHOOL')
          }
        ]
      },

      // ─── Teachers: school-scoped list and profile ──────────────────────
      {
        path: 'teachers',
        canActivate: [roleGuard, permissionGuard],
        data: {
          roles: ['SchoolManager', 'Moderator', 'MainManager', 'SuperAdmin'],
          permissions: ['Instructor.View']
        },
        children: [
          {
            path: '',
            loadComponent: () => import('./features/teachers/teachers-list/teachers-list.component')
              .then(m => m.TeachersListComponent),
            title: translatedTitle('ROUTE_TITLES.TEACHERS')
          },
          {
            path: ':userId',
            loadComponent: () => import('./features/teachers/teacher-profile/teacher-profile.component')
              .then(m => m.TeacherProfileComponent),
            title: translatedTitle('ROUTE_TITLES.TEACHER_PROFILE')
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
            title: translatedTitle('ROUTE_TITLES.USERS')
          },

          // The "الأشخاص" sidebar category is split into one sub-tab per role so
          // each list only ever shows the people named in its title. Same
          // component — `scopedRole` pins its role filter and hides the dropdown.
          {
            path: 'school-managers',
            data: { scopedRole: 'SchoolManager' },
            loadComponent: () => import('./features/users/users-list/users-list.component')
              .then(m => m.UsersListComponent),
            title: translatedTitle('ROUTE_TITLES.SCHOOL_MANAGERS')
          },
          {
            path: 'moderators',
            data: { scopedRole: 'Moderator' },
            loadComponent: () => import('./features/users/users-list/users-list.component')
              .then(m => m.UsersListComponent),
            title: translatedTitle('ROUTE_TITLES.MODERATORS')
          },
          {
            path: 'secretaries',
            data: { scopedRole: 'Secretary' },
            loadComponent: () => import('./features/users/users-list/users-list.component')
              .then(m => m.UsersListComponent),
            title: translatedTitle('ROUTE_TITLES.SECRETARIES')
          },

          {
            path: 'new',
            canActivate: [permissionGuard],
            data: { permissions: ['User.Create'] },
            loadComponent: () => import('./features/users/user-form/user-form.component')
              .then(m => m.UserFormComponent),
            title: translatedTitle('ROUTE_TITLES.NEW_USER')
          },
          {
            path: ':id/edit',
            canActivate: [permissionGuard],
            data: { permissions: ['User.Edit'] },
            loadComponent: () => import('./features/users/user-form/user-form.component')
              .then(m => m.UserFormComponent),
            title: translatedTitle('ROUTE_TITLES.EDIT_USER')
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
        title: translatedTitle('ROUTE_TITLES.USER_SCHOOL_ROLES')
      },

      // ─── Phase 3: Rubric (permission-gated) ──────────────────────────────
      {
        path: 'rubric',
        canActivate: [roleGuard, permissionGuard],
        data: { roles: ['MainManager', 'SuperAdmin'], permissions: ['Rubric.View'] },
        children: [
          {
            path: '',
            loadComponent: () => import('./features/rubric/rubric-viewer/rubric-viewer.component')
              .then(m => m.RubricViewerComponent),
            title: translatedTitle('ROUTE_TITLES.RUBRIC')
          },
          {
            path: 'edit',
            canActivate: [permissionGuard],
            data: { permissions: ['Rubric.Manage'] },
            loadComponent: () => import('./features/rubric/rubric-editor/rubric-editor.component')
              .then(m => m.RubricEditorComponent),
            title: translatedTitle('ROUTE_TITLES.EDIT_RUBRIC')
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
            title: translatedTitle('ROUTE_TITLES.VISITS')
          },
          {
            path: 'new',
            canActivate: [permissionGuard],
            data: { permissions: ['Visit.Create'] },
            loadComponent: () => import('./features/visits/visit-form/visit-form.component')
              .then(m => m.VisitFormComponent),
            title: translatedTitle('ROUTE_TITLES.NEW_VISIT')
          },
          {
            path: ':id',
            canActivate: [permissionGuard],
            data: { permissions: ['Visit.View'] },
            loadComponent: () => import('./features/visits/visit-detail/visit-detail.component')
              .then(m => m.VisitDetailComponent),
            title: translatedTitle('ROUTE_TITLES.VISIT_DETAILS')
          },
          {
            path: ':id/edit',
            canActivate: [permissionGuard],
            data: { permissions: ['Visit.Edit'] },
            loadComponent: () => import('./features/visits/visit-form/visit-form.component')
              .then(m => m.VisitFormComponent),
            title: translatedTitle('ROUTE_TITLES.EDIT_VISIT')
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
        title: translatedTitle('ROUTE_TITLES.IMPROVEMENT_PLANS')
      },
      {
        path: 'improvement-plans/:id',
        canActivate: [permissionGuard],
        data: { permissions: ['Plan.View'] },
        loadComponent: () => import('./features/improvement-plans/plan-detail/plan-detail.component')
          .then(m => m.PlanDetailComponent),
        title: translatedTitle('ROUTE_TITLES.IMPROVEMENT_PLAN_DETAILS')
      },

      {
        path: 'complaints',
        canActivate: [roleGuard, permissionGuard],
        data: { roles: ['SchoolManager', 'Instructor', 'SuperAdmin'], permissions: ['Complaint.View'] },
        loadComponent: () => import('./features/complaints/complaints-list/complaints-list.component')
          .then(m => m.ComplaintsListComponent),
        title: translatedTitle('ROUTE_TITLES.COMPLAINTS')
      },

      // ─── Account Settings (Any authenticated user) ──────────────────────
      {
        path: 'account',
        children: [
          {
            path: 'settings',
            loadComponent: () => import('./features/account/account-settings/account-settings.component')
              .then(m => m.AccountSettingsComponent),
            title: translatedTitle('ROUTE_TITLES.ACCOUNT_SETTINGS')
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
    title: translatedTitle('ROUTE_TITLES.UNAUTHORIZED')
  },

  // Wildcard — 404
  { path: '**', redirectTo: '/auth/school-login' }
];
