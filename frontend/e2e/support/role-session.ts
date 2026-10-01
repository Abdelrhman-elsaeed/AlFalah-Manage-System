import { type Browser, type BrowserContext, type Page } from '@playwright/test';
import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';

export type E2ERole =
  | 'manager'
  | 'officer'
  | 'socialWorker'
  | 'otherSocialWorker'
  | 'secretary'
  | 'security'
  | 'instructor'
  | 'substituteInstructor'
  | 'guardian'
  | 'crossOfficer'
  | 'matrixInstructor'
  | 'inactiveProfileGuardian'
  | 'inactiveRelationshipGuardian';

interface AuthArtifact {
  accessToken: string;
  refreshToken: string;
  user: unknown;
}

export interface RoleSession {
  context: BrowserContext;
  page: Page;
}

export async function openRoleSession(
  browser: Browser,
  role: E2ERole,
  viewport?: { width: number; height: number }
): Promise<RoleSession> {
  const artifact = JSON.parse(
    await readFile(resolve('test-results', '.auth', `${role}.json`), 'utf8')
  ) as AuthArtifact;

  const context = await browser.newContext({
    baseURL: process.env['E2E_WEB_URL'] ?? 'http://127.0.0.1:4200',
    locale: 'ar-SA',
    timezoneId: 'Africa/Cairo',
    viewport
  });
  const isolatedApiUrl = process.env['E2E_API_URL'];
  if (isolatedApiUrl && isolatedApiUrl !== 'http://localhost:5264') {
    await context.route('http://localhost:5264/**', route => {
      const rewritten = route.request().url().replace('http://localhost:5264', isolatedApiUrl);
      return route.continue({ url: rewritten });
    });
  }
  await context.addInitScript(session => {
    window.sessionStorage.setItem('alfalah_access_token', session.accessToken);
    window.sessionStorage.setItem('alfalah_refresh_token', session.refreshToken);
    window.sessionStorage.setItem('alfalah_user', JSON.stringify(session.user));
  }, artifact);

  return { context, page: await context.newPage() };
}

export async function setE2EClock(instant: string): Promise<void> {
  const { rename, writeFile } = await import('node:fs/promises');
  const clockPath = resolve('test-results', 'e2e-clock.txt');
  const nextPath = `${clockPath}.next`;
  await writeFile(nextPath, `${instant}\n`, 'utf8');
  await rename(nextPath, clockPath);
}
