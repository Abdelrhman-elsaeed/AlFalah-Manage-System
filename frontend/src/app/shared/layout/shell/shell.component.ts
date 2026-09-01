import { CommonModule } from '@angular/common';
import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { TooltipModule } from 'primeng/tooltip';
import { filter } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { StudentAnalyzerService } from '../../../core/services/student-analyzer.service';

interface NavItem {
  labelKey: string;
  icon: string;
  route: string;
  roles?: string[];
  permissions?: string[];
  requireAllPermissions?: boolean;
  tooltipKey?: string;
  /** Highlight only on an exact URL match — for parents of deeper routes (e.g. /users). */
  exact?: boolean;
  requiresStudentAnalyzerAccess?: boolean;
}

interface NavCategory {
  id: 'evaluation' | 'people' | 'administration' | 'settings';
  labelKey: string;
  icon: string;
  items: NavItem[];
}

function dashboardRouteForRoles(roles: readonly string[]): string | null {
  if (roles.includes('SuperAdmin') || roles.includes('MainManager')) return '/main-manager/dashboard';
  if (roles.includes('SchoolManager')) return '/school-manager/dashboard';
  if (roles.includes('Moderator')) return '/moderator/dashboard';
  if (roles.includes('Instructor')) return '/instructor/dashboard';
  if (roles.includes('StudentAffairsOfficer')) return '/student-affairs/settings';
  return null;
}

export const SHELL_NAV_CATEGORIES: NavCategory[] = [
  {
    id: 'evaluation',
    labelKey: 'NAV.CATEGORIES.EVALUATION',
    icon: 'pi pi-chart-line',
    items: [
      {
        labelKey: 'NAV.VISITS',
        icon: 'pi pi-clipboard',
        route: '/visits',
        roles: ['SchoolManager', 'Moderator', 'MainManager', 'SuperAdmin'],
        permissions: ['Visit.View']
      },
      {
        labelKey: 'NAV.RUBRIC',
        icon: 'pi pi-list-check',
        route: '/rubric',
        roles: ['MainManager', 'SuperAdmin'],
        permissions: ['Rubric.View']
      }
    ]
  },
  {
    // One sub-tab per role: each list shows only the people its title names,
    // with the school manager first and an "everyone" tab at the end.
    id: 'people',
    labelKey: 'NAV.CATEGORIES.PEOPLE',
    icon: 'pi pi-users',
    items: [
      { labelKey: 'NAV.SCHOOL_MANAGERS', icon: 'pi pi-briefcase', route: '/users/school-managers', permissions: ['User.View'] },
      {
        labelKey: 'NAV.TEACHERS',
        icon: 'pi pi-id-card',
        route: '/teachers',
        roles: ['SchoolManager', 'Moderator', 'MainManager', 'SuperAdmin'],
        permissions: ['Instructor.View']
      },
      { labelKey: 'NAV.MODERATORS', icon: 'pi pi-user-edit', route: '/users/moderators', permissions: ['User.View'] },
      { labelKey: 'NAV.SECRETARIES', icon: 'pi pi-inbox', route: '/users/secretaries', permissions: ['User.View'] },
      // Exact match: otherwise /users/moderators would light this up too.
      { labelKey: 'NAV.ALL_PEOPLE', icon: 'pi pi-users', route: '/users', permissions: ['User.View'], exact: true },
      { labelKey: 'NAV.USER_SCHOOL_ROLES', icon: 'pi pi-sitemap', route: '/user-school-roles', permissions: ['User.Edit'] }
    ]
  },
  {
    id: 'administration',
    labelKey: 'NAV.CATEGORIES.ADMINISTRATION',
    icon: 'pi pi-building',
    items: [
      { labelKey: 'NAV.SCHOOLS', icon: 'pi pi-building', route: '/schools', permissions: ['School.View'] },
      {
        labelKey: 'NAV.COMPLAINTS',
        icon: 'pi pi-flag',
        route: '/complaints',
        roles: ['SchoolManager', 'Instructor', 'SuperAdmin'],
        permissions: ['Complaint.View']
      },
      {
        labelKey: 'نماذج أولياء الأمور',
        icon: 'pi pi-file-edit',
        route: '/parent-surveys',
        roles: ['SchoolManager', 'Moderator', 'SuperAdmin'],
        permissions: ['ParentSurvey.Manage']
      },
      {
        labelKey: 'الجدول المدرسي',
        icon: 'pi pi-calendar-plus',
        route: '/timetable',
        roles: ['SchoolManager', 'Moderator'],
        permissions: ['Timetable.View']
      },
      {
        labelKey: 'الحضور والانصراف',
        icon: 'pi pi-calendar',
        route: '/attendance',
        roles: ['Secretary', 'SchoolManager', 'Moderator', 'Instructor'],
        permissions: ['Attendance.View']
      },
      {
        labelKey: 'رصد الغياب اليومي',
        icon: 'pi pi-list-check',
        route: '/student-affairs/attendance/sheet',
        roles: ['Secretary'],
        permissions: ['Attendance.ViewStudents', 'Attendance.ManageStudents'],
        requireAllPermissions: true
      },
      {
        labelKey: 'استيراد سجل زاجل',
        icon: 'pi pi-file-import',
        route: '/student-affairs/biometrics/zajel',
        roles: ['Secretary', 'StudentAffairsOfficer'],
        permissions: ['Biometric.Import']
      },
      {
        labelKey: 'تصدير نور الأسبوعي',
        icon: 'pi pi-file-export',
        route: '/student-affairs/noor-export',
        roles: ['StudentAffairsOfficer'],
        permissions: ['Noor.Export']
      },
      {
        labelKey: 'رفع عذر غياب',
        icon: 'pi pi-paperclip',
        route: '/student-affairs/guardian/excuses',
        roles: ['Guardian'],
        permissions: ['Attendance.SubmitExcuse']
      },
      {
        labelKey: 'مراجعة الأعذار',
        icon: 'pi pi-verified',
        route: '/student-affairs/officer/excuses',
        roles: ['StudentAffairsOfficer'],
        permissions: ['Attendance.ViewStudents', 'Attendance.ReviewExcuse'],
        requireAllPermissions: true
      },
      {
        labelKey: 'طلب استئذان خروج',
        icon: 'pi pi-send',
        route: '/student-affairs/gate-passes/mine/new',
        roles: ['Guardian'],
        permissions: ['GatePass.Request']
      },
      {
        labelKey: 'مراجعة استئذانات الخروج',
        icon: 'pi pi-check-square',
        route: '/student-affairs/gate-passes',
        roles: ['StudentAffairsOfficer'],
        permissions: ['GatePass.View', 'GatePass.Approve', 'GatePass.Reject'],
        requireAllPermissions: true,
        exact: true
      },
      {
        labelKey: 'تنفيذ استئذانات الخروج',
        icon: 'pi pi-sign-out',
        route: '/student-affairs/gate-passes/security',
        roles: ['SecurityGuard'],
        permissions: ['GatePass.AcknowledgeSecurity', 'GatePass.Execute'],
        requireAllPermissions: true
      },
      {
        labelKey: 'إحالات الموجه الطلابي',
        icon: 'pi pi-briefcase',
        route: '/student-affairs/cases',
        roles: ['SocialWorker'],
        permissions: ['Referral.View']
      },
      {
        labelKey: 'استدعاءات أولياء الأمور',
        icon: 'pi pi-calendar-clock',
        route: '/student-affairs/summons',
        roles: ['SocialWorker'],
        permissions: ['Summon.View']
      },
      {
        labelKey: 'اعتماد إشعارات أولياء الأمور',
        icon: 'pi pi-send',
        route: '/student-affairs/notification-approvals',
        roles: ['StudentAffairsOfficer'],
        permissions: ['Notification.ApproveDispatch', 'Notification.SuppressDispatch'],
        requireAllPermissions: true
      },
      {
        labelKey: 'الرسائل',
        icon: 'pi pi-comments',
        route: '/student-affairs/messages',
        roles: ['Guardian', 'StudentAffairsOfficer', 'SocialWorker'],
        permissions: ['Messaging.ViewOwn']
      }
    ]
  },
  {
    id: 'settings',
    labelKey: 'NAV.CATEGORIES.SETTINGS',
    icon: 'pi pi-cog',
    items: [
      {
        labelKey: 'إعدادات شؤون الطلاب',
        icon: 'pi pi-sliders-h',
        route: '/student-affairs/settings',
        roles: ['StudentAffairsOfficer', 'SchoolManager'],
        permissions: ['StudentAffairsSettings.View']
      },
      { labelKey: 'إعدادات ملفات الإنجاز', icon: 'pi pi-folder-open', route: '/school-manager/evidence-settings', roles: ['SchoolManager'], permissions: ['Settings.Manage'] },
      { labelKey: 'ACCOUNT.TITLE', icon: 'pi pi-pen-to-square', route: '/account/settings' }
    ]
  }
];

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive, TranslateModule, TooltipModule],
  templateUrl: './shell.component.html',
  styleUrls: ['./shell.component.css']
})
export class ShellComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly studentAnalyzer = inject(StudentAnalyzerService);
  private readonly sidebarStorageKey = 'alfalah-shell-sidebar-collapsed';

  /** Stable references so `routerLinkActiveOptions` isn't a fresh object per CD pass. */
  readonly exactMatchOptions = { exact: true };
  readonly prefixMatchOptions = { exact: false };

  readonly currentUser = this.authService.currentUser;
  readonly expandedCategoryIds = signal<ReadonlySet<string>>(new Set<string>());
  readonly isSidebarCollapsed = signal(this.getInitialSidebarState());
  readonly hasStudentAnalyzerAccess = signal(false);
  readonly ksaTime = signal<string>('');
  private clockInterval: ReturnType<typeof setInterval> | null = null;

  private readonly categories = SHELL_NAV_CATEGORIES;

  readonly isInstructorOnly = computed(() => {
    const roles = this.authService.roles();
    return roles.includes('Instructor')
      && !roles.some(role => ['SchoolManager', 'Moderator', 'MainManager', 'SuperAdmin'].includes(role));
  });

  /** Top-level links stay outside accordion categories by design. */
  readonly topItems = computed<NavItem[]>(() => {
    const dashboardRoute = dashboardRouteForRoles(this.authService.roles());
    const items: NavItem[] = dashboardRoute
      ? [{ labelKey: 'NAV.DASHBOARD', icon: 'pi pi-home', route: dashboardRoute }]
      : [];

    const roles = this.authService.roles();
    if (roles.includes('SchoolManager'))
      items.push({ labelKey: 'مصفوفة متابعة الأدلة', icon: 'pi pi-table', route: '/school-manager/evidence-matrix' });
    else if (roles.includes('Moderator'))
      items.push({ labelKey: 'مصفوفة متابعة الأدلة', icon: 'pi pi-table', route: '/moderator/evidence-matrix' });
    else if (roles.includes('MainManager') || roles.includes('SuperAdmin'))
      items.push({ labelKey: 'مصفوفة متابعة الأدلة', icon: 'pi pi-table', route: '/main-manager/evidence-matrix' });

    // D-36/D-73: exactly the minimal Instructor navigation surface.
    if (this.isInstructorOnly()) {
      items.push(
        { labelKey: 'الجدول المدرسي', icon: 'pi pi-calendar-plus', route: '/timetable', permissions: ['Timetable.View'] },
        { labelKey: 'الحضور والانصراف', icon: 'pi pi-calendar', route: '/attendance', permissions: ['Attendance.View'] },
        { labelKey: 'NAV.MY_REPORTS', icon: 'pi pi-file', route: '/instructor/reports' },
        { labelKey: 'ملفات الإنجاز', icon: 'pi pi-folder-open', route: '/instructor/evidence-files' },
        { labelKey: 'NAV.COMPLAINT_RESULTS', icon: 'pi pi-flag', route: '/complaints', permissions: ['Complaint.View'] },
        { labelKey: 'الساعات المكتبية', icon: 'pi pi-clock', route: '/student-affairs/office-hours', permissions: ['OfficeHours.ManageOwn'] },
        { labelKey: 'الرسائل', icon: 'pi pi-comments', route: '/student-affairs/messages', permissions: ['Messaging.ViewOwn'] },
        { labelKey: 'ACCOUNT.TITLE', icon: 'pi pi-pen-to-square', route: '/account/settings' }
      );
    }
    if (this.hasStudentAnalyzerAccess()) {
      items.push({
        labelKey: 'محلل تقارير الطلاب',
        icon: 'pi pi-sparkles',
        route: '/student-analyzer',
        requiresStudentAnalyzerAccess: true
      });
    }
    return items;
  });

  readonly visibleCategories = computed<NavCategory[]>(() => {
    if (this.isInstructorOnly()) return [];
    return this.categories
      .map(category => ({
        ...category,
        items: category.items.filter(item => this.canSee(item))
      }))
      .filter(category => category.items.length > 0);
  });

  readonly activeSchoolName = computed(() => this.currentUser()?.activeSchoolName ?? null);
  readonly primaryRoleKey = computed<string | null>(() => {
    const role = this.authService.roles()[0];
    if (!role) return null;

    if (role === 'Secretary') return 'السكرتير';

    const key = role.replace(/([a-z])([A-Z])/g, '$1_$2').toUpperCase();
    return `ROLES.${key}`;
  });
  readonly userInitials = computed(() => {
    const name = this.currentUser()?.fullName?.trim();
    if (!name) return '';

    return name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map(part => part.charAt(0))
      .join('')
      .toUpperCase();
  });

  ngOnInit(): void {
    this.updateKsaTime();
    if (typeof window !== 'undefined') {
      this.clockInterval = setInterval(() => this.updateKsaTime(), 1000);
      this.destroyRef.onDestroy(() => {
        if (this.clockInterval) clearInterval(this.clockInterval);
      });
    }

    this.studentAnalyzer.capabilities().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: response => this.hasStudentAnalyzerAccess.set(!!response.data?.canAccess),
      error: () => this.hasStudentAnalyzerAccess.set(false)
    });
    this.expandActiveCategory(this.router.url);
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(event => {
      this.expandActiveCategory(event.urlAfterRedirects);
    });
  }

  private updateKsaTime(): void {
    try {
      const now = new Date();
      const formatter = new Intl.DateTimeFormat('ar-SA', {
        timeZone: 'Asia/Riyadh',
        hour: 'numeric',
        minute: '2-digit',
        hour12: true
      });
      this.ksaTime.set(formatter.format(now));
    } catch {
      this.ksaTime.set(new Date().toLocaleTimeString('ar-SA', { hour: 'numeric', minute: '2-digit' }));
    }
  }

  toggleSidebar(): void {
    this.setSidebarCollapsed(!this.isSidebarCollapsed());
  }

  toggleCategory(category: NavCategory): void {
    if (this.isSidebarCollapsed()) {
      this.setSidebarCollapsed(false);
      this.expandedCategoryIds.update(current => new Set([...current, category.id]));
      return;
    }

    this.expandedCategoryIds.update(current => {
      const next = new Set(current);
      if (next.has(category.id)) next.delete(category.id);
      else next.add(category.id);
      return next;
    });
  }

  isCategoryExpanded(category: NavCategory): boolean {
    return this.expandedCategoryIds().has(category.id);
  }

  categoryContainsActiveRoute(category: NavCategory): boolean {
    return category.items.some(item => this.routeMatches(item.route, this.router.url));
  }

  logout(): void {
    this.authService.logout();
  }

  private setSidebarCollapsed(collapsed: boolean): void {
    this.isSidebarCollapsed.set(collapsed);

    if (typeof window !== 'undefined') {
      try {
        window.localStorage.setItem(this.sidebarStorageKey, collapsed ? '1' : '0');
      } catch {
        // Storage can be unavailable in private or restricted browser contexts.
      }
    }
  }

  private getInitialSidebarState(): boolean {
    if (typeof window === 'undefined') return false;

    try {
      const stored = window.localStorage.getItem(this.sidebarStorageKey);
      if (stored !== null) return stored === '1';
    } catch {
      // Fall through to viewport-based default.
    }

    return window.matchMedia?.('(max-width: 1024px)').matches ?? false;
  }

  private canSee(item: NavItem): boolean {
    if (item.requiresStudentAnalyzerAccess && !this.hasStudentAnalyzerAccess()) return false;
    const roles = this.authService.roles();
    const permissions = this.authService.permissions();
    if (item.roles && !item.roles.some(role => roles.includes(role))) return false;
    if (roles.includes('SuperAdmin')) return true;
    if (!item.permissions) return true;
    return item.requireAllPermissions
      ? item.permissions.every(permission => permissions.includes(permission))
      : item.permissions.some(permission => permissions.includes(permission));
  }

  private expandActiveCategory(url: string): void {
    const active = this.visibleCategories().find(category =>
      category.items.some(item => this.routeMatches(item.route, url)));
    if (!active) return;
    this.expandedCategoryIds.update(current => {
      if (current.has(active.id)) return current;
      return new Set([...current, active.id]);
    });
  }

  private routeMatches(route: string, url: string): boolean {
    const cleanUrl = url.split('?')[0].split('#')[0];
    return cleanUrl === route || cleanUrl.startsWith(`${route}/`);
  }
}
