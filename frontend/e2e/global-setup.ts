import { request, type FullConfig } from '@playwright/test';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';

const roleAccounts = {
  manager: { username: 'admin.test', school: 'Al-Falah E2E Test School', role: 'SchoolManager', permissions: ['StudentAffairsDashboard.SchoolOversight'] },
  officer: { username: 'officer.test', school: 'Al-Falah E2E Test School', role: 'StudentAffairsOfficer', permissions: ['Attendance.ReviewExcuse', 'GatePass.Approve'] },
  socialWorker: { username: 'socialworker.test', school: 'Al-Falah E2E Test School', role: 'SocialWorker', permissions: ['Referral.Manage', 'Summon.Schedule'] },
  otherSocialWorker: { username: 'socialworker.other.test', school: 'Al-Falah E2E Test School', role: 'SocialWorker', permissions: ['Referral.Manage'] },
  secretary: { username: 'secretary.test', school: 'Al-Falah E2E Test School', role: 'Secretary', permissions: ['Attendance.ManageStudents'] },
  security: { username: 'guard.test', school: 'Al-Falah E2E Test School', role: 'SecurityGuard', permissions: ['GatePass.Execute'] },
  instructor: { username: 'teacher.test', school: 'Al-Falah E2E Test School', role: 'Instructor', permissions: ['TeacherQuickAction.View'] },
  substituteInstructor: { username: 'substitute.teacher.test', school: 'Al-Falah E2E Test School', role: 'Instructor', permissions: ['TeacherQuickAction.View'] },
  guardian: { username: 'parent.test', school: 'Al-Falah E2E Test School', role: 'Guardian', permissions: ['Guardian.ViewLinkedStudents'] },
  crossOfficer: { username: 'cross.officer.test', school: 'Al-Falah E2E Isolation School', role: 'StudentAffairsOfficer', permissions: ['Student.View'] },
  matrixInstructor: { username: 'matrix.instructor.test', school: 'Al-Falah E2E Test School', role: 'Instructor', permissions: ['TeacherQuickAction.View'] },
  inactiveProfileGuardian: { username: 'inactive.profile.guardian.test', school: 'Al-Falah E2E Test School', role: 'Guardian', permissions: ['Guardian.ViewLinkedStudents'] },
  inactiveRelationshipGuardian: { username: 'inactive.relationship.guardian.test', school: 'Al-Falah E2E Test School', role: 'Guardian', permissions: ['Guardian.ViewLinkedStudents'] }
} as const;

export default async function globalSetup(_config: FullConfig): Promise<void> {
  const password = process.env['ALFALAH_E2E_PASSWORD'];
  if (!password) throw new Error('ALFALAH_E2E_PASSWORD is required.');

  const api = await request.newContext({ baseURL: process.env['E2E_API_URL'] ?? 'http://127.0.0.1:5264' });
  const schoolsResponse = await api.get('/api/v1/auth/schools');
  if (!schoolsResponse.ok()) throw new Error(`School lookup failed with ${schoolsResponse.status()}.`);

  const schoolsEnvelope = await schoolsResponse.json() as { data?: Array<{ id: number; name: string }> };
  const schools = schoolsEnvelope.data ?? [];
  for (const requiredSchool of new Set(Object.values(roleAccounts).map(account => account.school))) {
    if (!schools.some(candidate => candidate.name === requiredSchool))
      throw new Error(`The isolated E2E school '${requiredSchool}' was not seeded.`);
  }

  const authDirectory = resolve('test-results', '.auth');
  await mkdir(authDirectory, { recursive: true });
  const fixtureMetadata = JSON.parse(
    await readFile(resolve('test-results', 'e2e-fixtures.json'), 'utf8')
  ) as { inactiveSchoolId: number };

  const schoolIds: Record<string, number> = {};
  for (const [role, account] of Object.entries(roleAccounts)) {
    const school = schools.find(candidate => candidate.name === account.school)!;
    schoolIds[account.school] = school.id;
    const response = await api.post('/api/v1/auth/school-login', {
      data: { schoolId: school.id, username: account.username, password }
    });
    if (!response.ok()) throw new Error(`E2E login failed for ${role} with ${response.status()}.`);

    const envelope = await response.json() as {
      isSuccess: boolean;
      data?: {
        accessToken: string;
        refreshToken: string;
        user: { activeSchoolId: number; roles: string[]; permissions: string[] };
      };
    };
    if (!envelope.isSuccess || !envelope.data) throw new Error(`E2E login returned no session for ${role}.`);
    if (envelope.data.user.activeSchoolId !== school.id
        || envelope.data.user.roles.length !== 1
        || envelope.data.user.roles[0] !== account.role
        || account.permissions.some(permission => !envelope.data!.user.permissions.includes(permission))) {
      throw new Error(`E2E role, permission, or active-school contract failed for ${role}.`);
    }

    await writeFile(
      resolve(authDirectory, `${role}.json`),
      JSON.stringify(envelope.data),
      { encoding: 'utf8', mode: 0o600 }
    );
  }

  await writeFile(
    resolve(authDirectory, 'manifest.json'),
    JSON.stringify({
      primarySchoolId: schoolIds['Al-Falah E2E Test School'],
      isolationSchoolId: schoolIds['Al-Falah E2E Isolation School'],
      inactiveSchoolId: fixtureMetadata.inactiveSchoolId
    }),
    { encoding: 'utf8', mode: 0o600 }
  );
  await api.dispose();
}
