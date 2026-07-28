import { SHELL_NAV_CATEGORIES } from './shell.component';

describe('shell evaluation navigation roles', () => {
  const evaluation = SHELL_NAV_CATEGORIES.find(category => category.id === 'evaluation');

  it('shows visits to operational visit roles', () => {
    const visits = evaluation?.items.find(item => item.route === '/visits');

    expect(visits?.roles).toEqual(['SchoolManager', 'Moderator', 'MainManager', 'SuperAdmin']);
  });

  it('limits the rubric tool to main managers and super admins', () => {
    const rubric = evaluation?.items.find(item => item.route === '/rubric');

    expect(rubric?.roles).toEqual(['MainManager', 'SuperAdmin']);
  });
});

describe('shell people navigation', () => {
  const people = SHELL_NAV_CATEGORIES.find(category => category.id === 'people');

  it('splits people per role, school manager first and everyone last', () => {
    expect(people?.items.map(item => item.route)).toEqual([
      '/users/school-managers',
      '/teachers',
      '/users/moderators',
      '/users/secretaries',
      '/users',
      '/user-school-roles'
    ]);
  });

  it('matches the all-people tab exactly so role tabs do not light it up', () => {
    const all = people?.items.find(item => item.route === '/users');

    expect(all?.exact).toBe(true);
  });
});
