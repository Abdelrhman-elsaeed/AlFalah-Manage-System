import { expect, request, type APIRequestContext, type APIResponse } from '@playwright/test';
import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import type { E2ERole } from './role-session';

interface AuthArtifact {
  accessToken: string;
}

export interface ApiEnvelope<T> {
  isSuccess: boolean;
  data: T | null;
  message?: string | null;
  errors?: string[];
}

export interface PageResult<T> {
  items: T[];
  totalCount: number;
  pageNumber?: number;
  page?: number;
  pageSize: number;
}

export async function apiForRole(role: E2ERole): Promise<APIRequestContext> {
  const artifact = JSON.parse(
    await readFile(resolve('test-results', '.auth', `${role}.json`), 'utf8')
  ) as AuthArtifact;

  return request.newContext({
    baseURL: process.env['E2E_API_URL'] ?? 'http://127.0.0.1:5264',
    extraHTTPHeaders: {
      Authorization: `Bearer ${artifact.accessToken}`,
      Accept: 'application/json'
    }
  });
}

export async function successful<T>(response: APIResponse, expectedStatus?: number): Promise<T> {
  const envelope = await response.json() as ApiEnvelope<T>;
  const diagnostic = `HTTP ${response.status()}: ${JSON.stringify(envelope)}`;
  if (expectedStatus !== undefined) expect(response.status(), diagnostic).toBe(expectedStatus);
  else expect(response.ok(), diagnostic).toBeTruthy();

  expect(envelope.isSuccess, JSON.stringify(envelope.errors ?? envelope.message)).toBeTruthy();
  expect(envelope.data).not.toBeNull();
  return envelope.data as T;
}
