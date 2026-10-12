import { PhaseTwoRole } from './phase2.models';

export const SCHOOL_PEOPLE_ROLES: ReadonlyArray<{ role: PhaseTwoRole; path: string; icon: string; labelKey: string }> = [
  { role: 'StudentAffairsOfficer', path: 'student-affairs-officers', icon: 'pi-briefcase', labelKey: 'USERS.ROLE_STUDENT_AFFAIRS_OFFICER' },
  { role: 'SocialWorker', path: 'social-workers', icon: 'pi-heart', labelKey: 'USERS.ROLE_SOCIAL_WORKER' },
  { role: 'SecurityGuard', path: 'security-guards', icon: 'pi-shield', labelKey: 'USERS.ROLE_SECURITY_GUARD' },
  { role: 'Guardian', path: 'guardians', icon: 'pi-user', labelKey: 'USERS.ROLE_GUARDIAN' }
];

export const canRevealUserPasswords = (roles: readonly string[], permissions: readonly string[]) =>
  permissions.includes('User.Edit') && roles.some(role => ['SchoolManager', 'SuperAdmin', 'MainManager'].includes(role));
