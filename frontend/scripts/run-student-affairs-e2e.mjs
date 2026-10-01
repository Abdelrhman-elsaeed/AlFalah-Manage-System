import { randomBytes } from 'node:crypto';
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const frontendDirectory = resolve(scriptDirectory, '..');
const repositoryDirectory = resolve(frontendDirectory, '..');
const resultsDirectory = resolve(frontendDirectory, 'test-results');
const clockFile = resolve(resultsDirectory, 'e2e-clock.txt');
const fixtureFile = resolve(resultsDirectory, 'e2e-fixtures.json');
const databaseName = 'AlFalahW10E2E';
const apiPort = process.env.E2E_API_PORT ?? '5264';
const webPort = process.env.E2E_WEB_PORT ?? '4200';
const apiUrl = `http://127.0.0.1:${apiPort}`;
const webUrl = `http://127.0.0.1:${webPort}`;
const connectionString = `Server=(localdb)\\MSSQLLocalDB;Database=${databaseName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true`;
const fixturePassword = `E2e!${randomBytes(24).toString('base64url')}aA1`;

if (!databaseName.toUpperCase().includes('E2E')) {
  throw new Error('Refusing to reset a database whose name does not contain E2E.');
}

mkdirSync(resultsDirectory, { recursive: true });
writeFileSync(clockFile, '2026-09-30T03:50:00.000Z\n', 'utf8');

const environment = {
  ...process.env,
  ASPNETCORE_ENVIRONMENT: 'E2E',
  ASPNETCORE_URLS: apiUrl,
  E2E_API_URL: apiUrl,
  E2E_WEB_URL: webUrl,
  Cors__AllowedOrigins__0: webUrl,
  ConnectionStrings__DefaultConnection: connectionString,
  ALFALAH_MIGRATIONS_CONNECTION: connectionString,
  E2E__ClockFile: clockFile,
  E2E__FixtureFile: fixtureFile,
  Jwt__Secret: randomBytes(48).toString('base64url'),
  ALFALAH_E2E_PASSWORD: fixturePassword
};

const efArguments = [
  'ef', 'database', 'drop',
  '--project', 'backend/AlFalah.Infrastructure',
  '--startup-project', 'backend/AlFalah.Api',
  '--context', 'AlFalahDbContext',
  '--configuration', 'Release',
  '--no-build'
];
const dryRun = spawnSync(
  'dotnet',
  [...efArguments, '--dry-run'],
  { cwd: repositoryDirectory, env: environment, encoding: 'utf8', shell: false }
);

const dryRunOutput = `${dryRun.stdout ?? ''}\n${dryRun.stderr ?? ''}`;
if (dryRun.status !== 0 || !dryRunOutput.includes(`'${databaseName}'`)) {
  process.stderr.write(dryRunOutput);
  throw new Error(`Refusing database reset because EF did not resolve exactly ${databaseName}.`);
}

const ef = spawnSync(
  'dotnet',
  [...efArguments, '--force'],
  { cwd: repositoryDirectory, env: environment, stdio: 'inherit', shell: false }
);
if (ef.status !== 0) process.exit(ef.status ?? 1);

const playwright = spawnSync(
  process.execPath,
  [resolve(frontendDirectory, 'node_modules', '@playwright', 'test', 'cli.js'), 'test', ...process.argv.slice(2)],
  { cwd: frontendDirectory, env: environment, stdio: 'inherit', shell: false }
);

if (playwright.error) throw playwright.error;
process.exit(playwright.status ?? 1);
