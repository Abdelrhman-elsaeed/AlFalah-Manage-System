import { Route } from '@angular/router';
import { routes } from './app.routes';

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

function flatten(items: Route[]): Route[] {
  return items.flatMap(item => [item, ...flatten(item.children ?? [])]);
}
