import { createHmac } from 'node:crypto';
import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { expect, request, test, type APIRequestContext } from '@playwright/test';
import { apiForRole, successful, type ApiEnvelope } from '../support/api-session';

const apiBaseUrl = process.env['E2E_API_URL'] ?? 'http://127.0.0.1:5264';

interface AuthArtifact {
  accessToken: string;
  refreshToken: string;
  user: {
    userId: string;
    username: string;
    roles: string[];
    permissions: string[];
  };
}

interface Manifest {
  primarySchoolId: number;
  isolationSchoolId: number;
  inactiveSchoolId: number;
}

interface LoginSession {
  accessToken: string;
  refreshToken: string;
  user: {
    userId: string;
    roles: string[];
    permissions: string[];
  };
}

async function authArtifact(role: string): Promise<AuthArtifact> {
  return JSON.parse(
    await readFile(resolve('test-results', '.auth', `${role}.json`), 'utf8')
  ) as AuthArtifact;
}

async function manifest(): Promise<Manifest> {
  return JSON.parse(
    await readFile(resolve('test-results', '.auth', 'manifest.json'), 'utf8')
  ) as Manifest;
}

function mutateAndSignJwt(
  token: string,
  mutate: (payload: Record<string, unknown>) => void
): string {
  const secret = process.env['Jwt__Secret'];
  if (!secret) throw new Error('Jwt__Secret is required for the isolated E2E security matrix.');

  const [encodedHeader, encodedPayload] = token.split('.');
  if (!encodedHeader || !encodedPayload) throw new Error('The fixture access token is malformed.');
  const payload = JSON.parse(Buffer.from(encodedPayload, 'base64url').toString('utf8')) as Record<string, unknown>;
  mutate(payload);
  const nextPayload = Buffer.from(JSON.stringify(payload), 'utf8').toString('base64url');
  const unsigned = `${encodedHeader}.${nextPayload}`;
  const signature = createHmac('sha256', secret).update(unsigned).digest('base64url');
  return `${unsigned}.${signature}`;
}

async function apiForToken(accessToken: string): Promise<APIRequestContext> {
  return request.newContext({
    baseURL: apiBaseUrl,
    extraHTTPHeaders: {
      Authorization: `Bearer ${accessToken}`,
      Accept: 'application/json'
    }
  });
}

test.describe.serial('W10 security, privacy, and negative matrix', () => {
  test('Security matrix: exact officer role and permission succeeds while missing or injected claims fail closed', async () => {
    const officer = await authArtifact('officer');
    const guardian = await authArtifact('guardian');
    const validApi = await apiForRole('officer');
    const withoutPermissionApi = await apiForToken(mutateAndSignJwt(officer.accessToken, payload => {
      delete payload['permission'];
    }));
    const injectedPermissionApi = await apiForToken(mutateAndSignJwt(guardian.accessToken, payload => {
      payload['permission'] = ['Attendance.ViewStudents', 'Attendance.ReviewExcuse'];
    }));

    try {
      const valid = await validApi.get('/api/v1/student-attendance/excuses/pending?page=1&pageSize=5');
      expect(valid.status()).toBe(200);

      const missingPermission = await withoutPermissionApi.get('/api/v1/student-attendance/excuses/pending?page=1&pageSize=5');
      expect(missingPermission.status()).toBe(403);

      const wrongRole = await injectedPermissionApi.get('/api/v1/student-attendance/excuses/pending?page=1&pageSize=5');
      expect(wrongRole.status()).toBe(403);
      const wrongRoleEnvelope = await wrongRole.json() as ApiEnvelope<unknown>;
      expect(wrongRoleEnvelope.isSuccess).toBeFalsy();
    } finally {
      await Promise.all([validApi.dispose(), withoutPermissionApi.dispose(), injectedPermissionApi.dispose()]);
    }
  });

  test('Security matrix: inactive user and school cannot authenticate; inactive profile and relationship expose no student', async () => {
    const ids = await manifest();
    const password = process.env['ALFALAH_E2E_PASSWORD'];
    if (!password) throw new Error('ALFALAH_E2E_PASSWORD is required.');
    const anonymousApi = await request.newContext({ baseURL: apiBaseUrl });
    const inactiveProfileApi = await apiForRole('inactiveProfileGuardian');
    const inactiveRelationshipApi = await apiForRole('inactiveRelationshipGuardian');

    try {
      const inactiveUserLogin = await anonymousApi.post('/api/v1/auth/school-login', {
        data: { schoolId: ids.primarySchoolId, username: 'inactive.user.test', password }
      });
      expect(inactiveUserLogin.status()).toBe(401);

      const inactiveSchoolLogin = await anonymousApi.post('/api/v1/auth/school-login', {
        data: { schoolId: ids.inactiveSchoolId, username: 'inactive.school.user.test', password }
      });
      expect(inactiveSchoolLogin.status()).toBe(401);

      const inactiveProfileStudents = await inactiveProfileApi.get('/api/v1/guardian/students');
      expect(inactiveProfileStudents.status()).toBe(404);
      const inactiveProfileEnvelope = await inactiveProfileStudents.json() as ApiEnvelope<unknown>;
      expect(inactiveProfileEnvelope.isSuccess).toBeFalsy();
      expect(inactiveProfileEnvelope.data).toBeNull();

      const inactiveRelationshipStudents = await successful<unknown[]>(
        await inactiveRelationshipApi.get('/api/v1/guardian/students')
      );
      expect(inactiveRelationshipStudents).toEqual([]);
    } finally {
      await Promise.all([anonymousApi.dispose(), inactiveProfileApi.dispose(), inactiveRelationshipApi.dispose()]);
    }
  });

  test('Security matrix: production role change invalidates stale access and refresh; fresh login has only new claims', async () => {
    const ids = await manifest();
    const matrix = await authArtifact('matrixInstructor');
    const password = process.env['ALFALAH_E2E_PASSWORD'];
    if (!password) throw new Error('ALFALAH_E2E_PASSWORD is required.');
    const managerApi = await apiForRole('manager');
    const staleAccessApi = await apiForToken(matrix.accessToken);
    const anonymousApi = await request.newContext({ baseURL: apiBaseUrl });

    try {
      expect((await staleAccessApi.get('/api/v1/auth/me')).status()).toBe(200);

      const update = await managerApi.put(`/api/v1/users/${matrix.user.userId}`, {
        data: {
          firstName: 'Matrix',
          lastName: 'Moderator',
          fullName: 'Matrix Moderator',
          email: 'matrix.instructor.test@alfalah.test',
          preferredLanguage: 'ar',
          role: 'Moderator',
          schoolId: ids.primarySchoolId
        }
      });
      const updated = await successful<{ roles: string[] }>(update);
      expect(updated.roles).toEqual(['Moderator']);

      expect((await staleAccessApi.get('/api/v1/auth/me')).status()).toBe(401);
      const revokedByRoleChange = await anonymousApi.post('/api/v1/auth/refresh', {
        data: { refreshToken: matrix.refreshToken }
      });
      expect(revokedByRoleChange.status()).toBe(401);

      const freshLoginResponse = await anonymousApi.post('/api/v1/auth/school-login', {
        data: { schoolId: ids.primarySchoolId, username: matrix.user.username, password }
      });
      const freshLogin = await successful<LoginSession>(freshLoginResponse);
      expect(freshLogin.user.roles).toEqual(['Moderator']);
      expect(freshLogin.user.permissions).toContain('Dashboard.Moderator');
      expect(freshLogin.user.permissions).toContain('Instructor.View');
      expect(freshLogin.user.permissions).not.toContain('TeacherQuickAction.View');

      const logout = await anonymousApi.post('/api/v1/auth/logout', {
        data: { refreshToken: freshLogin.refreshToken }
      });
      expect(logout.status()).toBe(200);
      const explicitlyRevoked = await anonymousApi.post('/api/v1/auth/refresh', {
        data: { refreshToken: freshLogin.refreshToken }
      });
      expect(explicitlyRevoked.status()).toBe(401);
    } finally {
      await Promise.all([managerApi.dispose(), staleAccessApi.dispose(), anonymousApi.dispose()]);
    }
  });
});
