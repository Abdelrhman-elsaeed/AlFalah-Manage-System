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
