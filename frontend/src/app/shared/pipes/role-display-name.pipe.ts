import { Pipe, PipeTransform } from '@angular/core';

export const ROLE_DISPLAY_NAMES_AR: Readonly<Record<string, string>> = {
  SchoolManager: 'مدير المدرسة',
  StudentAffairsOfficer: 'وكيل شؤون الطلاب',
  SocialWorker: 'الموجه الطلابي',
  Secretary: 'السكرتير',
  SecurityGuard: 'حارس الأمن',
  Instructor: 'المعلم',
  Teacher: 'المعلم',
  Moderator: 'المشرف التربوي',
  Guardian: 'ولي الأمر',
  SuperAdmin: 'المشرف العام',
  MainManager: 'المدير العام',

  // Keys with ROLES. prefix
  'ROLES.SCHOOL_MANAGER': 'مدير المدرسة',
  'ROLES.STUDENT_AFFAIRS_OFFICER': 'وكيل شؤون الطلاب',
  'ROLES.SOCIAL_WORKER': 'الموجه الطلابي',
  'ROLES.SECRETARY': 'السكرتير',
  'ROLES.SECURITY_GUARD': 'حارس الأمن',
  'ROLES.INSTRUCTOR': 'المعلم',
  'ROLES.TEACHER': 'المعلم',
  'ROLES.MODERATOR': 'المشرف التربوي',
  'ROLES.GUARDIAN': 'ولي الأمر',
  'ROLES.SUPER_ADMIN': 'المشرف العام',
  'ROLES.MAIN_MANAGER': 'المدير العام',

  // UPPER_SNAKE_CASE formats
  SCHOOL_MANAGER: 'مدير المدرسة',
  STUDENT_AFFAIRS_OFFICER: 'وكيل شؤون الطلاب',
  SOCIAL_WORKER: 'الموجه الطلابي',
  SECRETARY_ROLE: 'السكرتير',
  SECURITY_GUARD: 'حارس الأمن',
  SUPER_ADMIN: 'المشرف العام',
  MAIN_MANAGER: 'المدير العام'
};

export function getRoleDisplayName(role: string | null | undefined): string {
  if (!role || typeof role !== 'string') return '';
  const trimmed = role.trim();
  if (!trimmed) return '';

  // 1. Direct match in dictionary
  if (ROLE_DISPLAY_NAMES_AR[trimmed]) {
    return ROLE_DISPLAY_NAMES_AR[trimmed];
  }

  // 2. Strip 'ROLES.' prefix if present
  const withoutPrefix = trimmed.startsWith('ROLES.') ? trimmed.substring(6) : trimmed;
  if (ROLE_DISPLAY_NAMES_AR[withoutPrefix]) {
    return ROLE_DISPLAY_NAMES_AR[withoutPrefix];
  }

  // 3. Convert camelCase / PascalCase to UPPER_SNAKE_CASE (e.g. StudentAffairsOfficer -> STUDENT_AFFAIRS_OFFICER)
  const snakeKey = withoutPrefix.replace(/([a-z])([A-Z])/g, '$1_$2').toUpperCase();
  if (ROLE_DISPLAY_NAMES_AR[snakeKey]) {
    return ROLE_DISPLAY_NAMES_AR[snakeKey];
  }

  // 4. Case-insensitive normalization
  const normalizedSearch = withoutPrefix.toLowerCase().replace(/[._\-\s]/g, '');
  const matchedKey = Object.keys(ROLE_DISPLAY_NAMES_AR).find(
    key => key.toLowerCase().replace(/[._\-\s]/g, '') === normalizedSearch
  );
  if (matchedKey) {
    return ROLE_DISPLAY_NAMES_AR[matchedKey];
  }

  return withoutPrefix;
}

@Pipe({
  name: 'roleDisplayName',
  standalone: true
})
export class RoleDisplayNamePipe implements PipeTransform {
  transform(role: string | null | undefined): string {
    return getRoleDisplayName(role);
  }
}
