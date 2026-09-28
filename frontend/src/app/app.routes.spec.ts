import { Route } from '@angular/router';
import { routes } from './app.routes';
import { permissionGuard } from './core/guards/permission.guard';
import { roleGuard } from './core/guards/role.guard';

describe('Classroom Visits V2 cutover routes', () => {
  const allRoutes = flatten(routes);

  it('uses the V2 workspace as the primary /visits entry', () => {
    const route = allRoutes.find(item => item.path === 'visits' && !!item.loadComponent);

    expect(route).toBeTruthy();
    expect(route?.data?.['roles']).toContain('Instructor');
  });

  it('keeps /visits-v2 as a compatibility alias', () => {
    const alias = allRoutes.find(item => item.path === 'visits-v2');

    expect(alias?.redirectTo).toBe('visits');
    expect(alias?.pathMatch).toBe('full');
  });

  it('exposes legacy visits without create or edit child routes', () => {
    const legacy = allRoutes.find(item => item.path === 'visits-legacy');
    const childPaths = legacy?.children?.map(child => child.path) ?? [];

    expect(childPaths).toContain('');
    expect(childPaths).toContain(':id');
    expect(childPaths).not.toContain('new');
    expect(childPaths).not.toContain(':id/edit');
  });
});

describe('Student Affairs role workspace routes', () => {
  const allRoutes = flatten(routes);
  const matrix = [
    ['student-affairs/officer', 'StudentAffairsOfficer', 'StudentAffairsDashboard.Officer'],
    ['student-affairs/guardian', 'Guardian', 'StudentAffairsDashboard.Guardian'],
    ['student-affairs/security', 'SecurityGuard', 'StudentAffairsDashboard.Security'],
    ['student-affairs/social-worker', 'SocialWorker', 'Referral.View'],
    ['student-affairs/teacher', 'Instructor', 'StudentAffairsDashboard.Teacher'],
    ['student-affairs/attendance/sheet', 'Secretary', 'Attendance.ViewStudents'],
    ['student-affairs/oversight', 'SchoolManager', 'StudentAffairsDashboard.SchoolOversight']
  ] as const;

  for (const [path, role, permission] of matrix) {
    it(`guards ${path} for ${role}`, () => {
      const route = allRoutes.find(item => item.path === path);

      expect(route).toBeTruthy();
      expect(route?.canActivate).toContain(roleGuard);
      expect(route?.canActivate).toContain(permissionGuard);
      expect(route?.data?.['roles']).toEqual([role]);
      expect(route?.data?.['permissions']).toContain(permission);
    });
  }

  it('keeps School Manager on the general dashboard with oversight as a separate workspace', () => {
    const oversight = allRoutes.find(item => item.path === 'student-affairs/oversight');

    expect(oversight?.data?.['roles']).toEqual(['SchoolManager']);
  });
});

function flatten(items: Route[]): Route[] {
  return items.flatMap(item => [item, ...flatten(item.children ?? [])]);
}
