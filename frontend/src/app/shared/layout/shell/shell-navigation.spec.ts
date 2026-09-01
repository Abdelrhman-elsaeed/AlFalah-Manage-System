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

describe('shell Phase 5 navigation', () => {
  const administration = SHELL_NAV_CATEGORIES.find(category => category.id === 'administration');

  it('exposes confidential case work only to social workers', () => {
    const cases = administration?.items.find(item => item.route === '/student-affairs/cases');
    const summons = administration?.items.find(item => item.route === '/student-affairs/summons');

    expect(cases?.roles).toEqual(['SocialWorker']);
    expect(cases?.permissions).toEqual(['Referral.View']);
    expect(summons?.roles).toEqual(['SocialWorker']);
  });

  it('does not expose the participant chat route to school managers', () => {
    const messages = administration?.items.find(item => item.route === '/student-affairs/messages');

    expect(messages?.roles).toEqual(['Guardian', 'StudentAffairsOfficer', 'SocialWorker']);
    expect(messages?.roles).not.toContain('SchoolManager');
  });
});

describe('shell classroom master data navigation', () => {
  const administration = SHELL_NAV_CATEGORIES.find(category => category.id === 'administration');

  it('exposes classroom management only to secretaries with its narrow permission', () => {
    const classrooms = administration?.items.find(item => item.route === '/student-affairs/classrooms');

    expect(classrooms?.roles).toEqual(['Secretary']);
    expect(classrooms?.permissions).toEqual(['Classroom.Manage']);
  });

  it('places student management directly after classroom management for secretaries', () => {
    const routes = administration?.items.map(item => item.route) ?? [];
    const classroomIndex = routes.indexOf('/student-affairs/classrooms');
    const students = administration?.items[classroomIndex + 1];

    expect(students?.route).toBe('/student-affairs/students');
    expect(students?.roles).toEqual(['Secretary']);
    expect(students?.permissions).toEqual(['Student.Manage']);
  });
});
