import { routes } from '../../app.routes';
import { authGuard } from '../../core/guards/auth.guard';

describe('Public landing routes', () => {
  it('lazy loads the public homepage at the exact root', async () => {
    const route = routes[0];
    expect(route.path).toBe('');
    expect(route.pathMatch).toBe('full');
    expect(route.canActivate).toBeUndefined();
    const component = await (route.loadComponent!() as Promise<unknown>);
    expect((component as { name: string }).name).toBe('LandingPageComponent');
  });
  it('retains both public login routes and the authenticated shell guard', () => {
    const auth = routes.find((route) => route.path === 'auth')!;
    expect(auth.canActivate).toBeUndefined();
    expect(auth.children?.some((route) => route.path === 'school-login')).toBeTrue();
    expect(auth.children?.some((route) => route.path === 'main-manager-login')).toBeTrue();
    expect(routes.some((route) => route.path === '' && route.canActivate?.includes(authGuard))).toBeTrue();
    expect(routes.at(-1)?.redirectTo).toBe('/');
  });
});
