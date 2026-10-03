export function roleLandingFor(roles: readonly string[]): string {
  if (roles.includes('SuperAdmin') || roles.includes('MainManager')) return '/main-manager/dashboard';
  if (roles.includes('SchoolManager')) return '/school-manager/dashboard';
  if (roles.includes('Moderator')) return '/moderator/dashboard';
  if (roles.includes('Instructor')) return '/instructor/dashboard';
  if (roles.includes('StudentAffairsOfficer')) return '/student-affairs/officer';
  if (roles.includes('Secretary')) return '/student-affairs/attendance/sheet';
  if (roles.includes('Guardian')) return '/student-affairs/guardian/dashboard';
  if (roles.includes('SecurityGuard')) return '/student-affairs/security';
  if (roles.includes('SocialWorker')) return '/student-affairs/social-worker';
  return '/dashboard';
}
