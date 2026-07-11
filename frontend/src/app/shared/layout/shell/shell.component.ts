import { Component, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { TooltipModule } from 'primeng/tooltip';
import { AuthService } from '../../../core/services/auth.service';

interface NavItem {
  labelKey: string;
  icon: string;
  route: string;
  /** Roles allowed to see this item. SuperAdmin is always allowed. */
  roles?: string[];
  /** Permissions required to see this item. Any of the listed permissions suffices. */
  permissions?: string[];
  /** Optional tooltip key (defaults to labelKey). */
  tooltipKey?: string;
}

/**
 * Resolves the user's primary dashboard. Only ONE الرئيسية is shown in the sidebar.
 * Priority: SuperAdmin (sees all) → first role in: MainManager, SchoolManager, Moderator, Instructor.
 */
function dashboardRouteForRoles(roles: readonly string[]): { route: string; labelKey: string } | null {
  if (roles.includes('SuperAdmin')) return { route: '/main-manager/dashboard', labelKey: 'DASHBOARD.MAIN_MANAGER' };
  if (roles.includes('MainManager')) return { route: '/main-manager/dashboard', labelKey: 'DASHBOARD.MAIN_MANAGER' };
  if (roles.includes('SchoolManager')) return { route: '/school-manager/dashboard', labelKey: 'DASHBOARD.SCHOOL_MANAGER' };
  if (roles.includes('Moderator')) return { route: '/moderator/dashboard', labelKey: 'DASHBOARD.MODERATOR' };
  if (roles.includes('Instructor')) return { route: '/instructor/dashboard', labelKey: 'DASHBOARD.INSTRUCTOR' };
  return null;
}

/**
 * Authenticated shell layout: topbar (RTL) + sidebar (right, RTL) + content area.
 * Sidebar shows EXACTLY ONE dashboard entry (resolved by the user's primary role),
 * then permission-gated admin items (Schools / Users / Assignments / Rubric).
 */
@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive, TranslateModule, TooltipModule],
  templateUrl: './shell.component.html',
  styleUrls: ['./shell.component.css']
})
export class ShellComponent {
  private readonly authService = inject(AuthService);

  readonly currentUser = this.authService.currentUser;

  /** Admin / domain items (no dashboards — those are dynamic below). */
  private readonly adminItems: NavItem[] = [
    { labelKey: 'NAV.SCHOOLS', icon: 'pi pi-building', route: '/schools', permissions: ['School.View'] },
    { labelKey: 'NAV.USERS', icon: 'pi pi-users', route: '/users', permissions: ['User.View'] },
    { labelKey: 'NAV.USER_SCHOOL_ROLES', icon: 'pi pi-sitemap', route: '/user-school-roles', permissions: ['User.Edit'] },
    { labelKey: 'NAV.VISITS', icon: 'pi pi-clipboard', route: '/visits', permissions: ['Visit.View'] },
    { labelKey: 'NAV.RUBRIC', icon: 'pi pi-list-check', route: '/rubric', permissions: ['Rubric.View'] }
  ];

  /**
   * Dashboard item is dynamic — exactly ONE entry, resolved by the current user's primary role.
   * If SuperAdmin, the MainManager dashboard is shown (admins can navigate via the topbar
   * user-info menu if they want to swap roles; out of scope here).
   */
  readonly visibleItems = computed<NavItem[]>(() => {
    const roles = this.authService.roles();
    const permissions = this.authService.permissions();

    const out: NavItem[] = [];
    const dashboard = dashboardRouteForRoles(roles);
    if (dashboard) {
      out.push({
        labelKey: 'NAV.DASHBOARD',
        icon: 'pi pi-home',
        route: dashboard.route
      });
    }

    // Task 1: Add Account Settings link for all authenticated users
    out.push({
      labelKey: 'ACCOUNT.TITLE',
      icon: 'pi pi-user-edit',
      route: '/account/settings'
    });

    const isSuperAdmin = roles.includes('SuperAdmin');

    for (const item of this.adminItems) {
      if (isSuperAdmin) {
        out.push(item);
        continue;
      }
      if (item.permissions && item.permissions.length > 0) {
        if (item.permissions.some(p => permissions.includes(p))) out.push(item);
      } else {
        out.push(item);
      }
    }
    return out;
  });

  readonly activeSchoolName = computed(() =>
    this.currentUser()?.activeSchoolName ?? null
  );

  /** First role displayed in the topbar — drives the user-info badge label. */
  readonly primaryRoleLabel = computed<string | null>(() => {
    const roles = this.authService.roles();
    if (roles.length === 0) return null;
    return roles[0];
  });

  logout(): void {
    this.authService.logout();
  }
}