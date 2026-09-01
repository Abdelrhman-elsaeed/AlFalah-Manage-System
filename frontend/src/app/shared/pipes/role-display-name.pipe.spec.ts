import { RoleDisplayNamePipe, getRoleDisplayName } from './role-display-name.pipe';

describe('RoleDisplayNamePipe', () => {
  const pipe = new RoleDisplayNamePipe();

  it('translates PascalCase backend role names into Arabic display strings', () => {
    expect(pipe.transform('SchoolManager')).toBe('مدير المدرسة');
    expect(pipe.transform('StudentAffairsOfficer')).toBe('وكيل شؤون الطلاب');
    expect(pipe.transform('SocialWorker')).toBe('الموجه الطلابي');
    expect(pipe.transform('Secretary')).toBe('السكرتير');
    expect(pipe.transform('SecurityGuard')).toBe('حارس الأمن');
    expect(pipe.transform('Instructor')).toBe('المعلم');
    expect(pipe.transform('Guardian')).toBe('ولي الأمر');
    expect(pipe.transform('Moderator')).toBe('المشرف التربوي');
    expect(pipe.transform('SuperAdmin')).toBe('المشرف العام');
    expect(pipe.transform('MainManager')).toBe('المدير العام');
  });

  it('translates ROLES. prefixed keys into Arabic display strings', () => {
    expect(pipe.transform('ROLES.STUDENT_AFFAIRS_OFFICER')).toBe('وكيل شؤون الطلاب');
    expect(pipe.transform('ROLES.SCHOOL_MANAGER')).toBe('مدير المدرسة');
    expect(pipe.transform('ROLES.SOCIAL_WORKER')).toBe('الموجه الطلابي');
    expect(pipe.transform('ROLES.SECRETARY')).toBe('السكرتير');
    expect(pipe.transform('ROLES.SECURITY_GUARD')).toBe('حارس الأمن');
    expect(pipe.transform('ROLES.INSTRUCTOR')).toBe('المعلم');
    expect(pipe.transform('ROLES.GUARDIAN')).toBe('ولي الأمر');
  });

  it('translates UPPER_SNAKE_CASE role keys into Arabic display strings', () => {
    expect(pipe.transform('STUDENT_AFFAIRS_OFFICER')).toBe('وكيل شؤون الطلاب');
    expect(pipe.transform('SCHOOL_MANAGER')).toBe('مدير المدرسة');
    expect(pipe.transform('SOCIAL_WORKER')).toBe('الموجه الطلابي');
  });

  it('handles null, undefined, or empty values gracefully', () => {
    expect(pipe.transform(null)).toBe('');
    expect(pipe.transform(undefined)).toBe('');
    expect(pipe.transform('')).toBe('');
    expect(pipe.transform('   ')).toBe('');
  });

  it('returns fallback string for unknown role safely without crashing', () => {
    expect(pipe.transform('CustomUnknownRole')).toBe('CustomUnknownRole');
  });
});
