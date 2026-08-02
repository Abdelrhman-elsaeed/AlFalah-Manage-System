import { CommonModule } from '@angular/common';
import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { TooltipModule } from 'primeng/tooltip';
import { filter } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';

interface NavItem {
  labelKey: string;
  icon: string;
  route: string;
  roles?: string[];
  permissions?: string[];
  tooltipKey?: string;
  /** Highlight only on an exact URL match — for parents of deeper routes (e.g. /users). */
  exact?: boolean;
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
      }
    ]
  },
  {
    id: 'settings',
    labelKey: 'NAV.CATEGORIES.SETTINGS',
    icon: 'pi pi-cog',
    items: [
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
  private readonly sidebarStorageKey = 'alfalah-shell-sidebar-collapsed';

  private lastContentScrollTop = 0;
  private pendingContentScrollTop = 0;
  private scrollAnimationFrame: number | null = null;

  /** Stable references so `routerLinkActiveOptions` isn't a fresh object per CD pass. */
  readonly exactMatchOptions = { exact: true };
  readonly prefixMatchOptions = { exact: false };

  readonly currentUser = this.authService.currentUser;
  readonly expandedCategoryIds = signal<ReadonlySet<string>>(new Set<string>());
  readonly isSidebarCollapsed = signal(this.getInitialSidebarState());
  readonly isTopbarHidden = signal(false);

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
        { labelKey: 'ACCOUNT.TITLE', icon: 'pi pi-pen-to-square', route: '/account/settings' }
      );
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
    this.expandActiveCategory(this.router.url);
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(event => {
      this.expandActiveCategory(event.urlAfterRedirects);
      this.showTopbar();
      this.lastContentScrollTop = 0;
    });

    this.destroyRef.onDestroy(() => {
      if (this.scrollAnimationFrame !== null && typeof cancelAnimationFrame === 'function') {
        cancelAnimationFrame(this.scrollAnimationFrame);
      }
    });
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

  onContentScroll(event: Event): void {
    const target = event.currentTarget as HTMLElement | null;
    if (!target) return;

    this.pendingContentScrollTop = Math.max(0, target.scrollTop);
    if (this.scrollAnimationFrame !== null) return;

    if (typeof requestAnimationFrame !== 'function') {
      this.updateTopbarVisibility(this.pendingContentScrollTop);
      return;
    }

    this.scrollAnimationFrame = requestAnimationFrame(() => {
      this.updateTopbarVisibility(this.pendingContentScrollTop);
      this.scrollAnimationFrame = null;
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

  private updateTopbarVisibility(scrollTop: number): void {
    const delta = scrollTop - this.lastContentScrollTop;

    if (scrollTop <= 24) {
      this.showTopbar();
    } else if (delta > 7 && scrollTop > 88) {
      this.isTopbarHidden.set(true);
    } else if (delta < -5) {
      this.showTopbar();
    }

    this.lastContentScrollTop = scrollTop;
  }

  private showTopbar(): void {
    if (this.isTopbarHidden()) this.isTopbarHidden.set(false);
  }

  private canSee(item: NavItem): boolean {
    const roles = this.authService.roles();
    const permissions = this.authService.permissions();
    if (item.roles && !item.roles.some(role => roles.includes(role))) return false;
    if (roles.includes('SuperAdmin')) return true;
    return !item.permissions || item.permissions.some(permission => permissions.includes(permission));
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
