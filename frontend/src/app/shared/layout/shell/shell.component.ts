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
    id: 'people',
    labelKey: 'NAV.CATEGORIES.PEOPLE',
    icon: 'pi pi-users',
    items: [
      {
        labelKey: 'NAV.TEACHERS',
        icon: 'pi pi-id-card',
        route: '/teachers',
        roles: ['SchoolManager', 'Moderator', 'MainManager', 'SuperAdmin'],
        permissions: ['Instructor.View']
      },
      { labelKey: 'NAV.USERS', icon: 'pi pi-users', route: '/users', permissions: ['User.View'] },
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
        roles: ['SchoolManager', 'SuperAdmin'],
        permissions: ['Complaint.View']
      }
    ]
  },
  {
    id: 'settings',
    labelKey: 'NAV.CATEGORIES.SETTINGS',
    icon: 'pi pi-cog',
    items: [
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

  readonly currentUser = this.authService.currentUser;
  readonly expandedCategoryIds = signal<ReadonlySet<string>>(new Set<string>());

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

    // D-36/D-73: exactly the minimal Instructor navigation surface.
    if (this.isInstructorOnly()) {
      items.push(
        { labelKey: 'NAV.MY_REPORTS', icon: 'pi pi-file', route: '/instructor/reports' },
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
  readonly primaryRoleLabel = computed<string | null>(() => this.authService.roles()[0] ?? null);

  ngOnInit(): void {
    this.expandActiveCategory(this.router.url);
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(event => this.expandActiveCategory(event.urlAfterRedirects));
  }

  toggleCategory(category: NavCategory): void {
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

  private canSee(item: NavItem): boolean {
    const roles = this.authService.roles();
    const permissions = this.authService.permissions();
    if (roles.includes('SuperAdmin')) return true;
    if (item.roles && !item.roles.some(role => roles.includes(role))) return false;
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
