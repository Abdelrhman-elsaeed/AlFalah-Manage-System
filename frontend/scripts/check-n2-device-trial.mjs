// Synthetic proxy boundaries only: no SQL, provider, tunnel or actual-phone claim.
import { strict as assert } from 'node:assert';
import { createServer } from 'node:http';
import { spawn } from 'node:child_process';
import { mkdtemp, mkdir, writeFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { once } from 'node:events';

const directory = await mkdtemp(join(tmpdir(), 'alfalah-n2-proxy-check-'));
let proxy;
const requests = [];
const api = createServer((req, res) => {
  requests.push({ path: req.url, authorization: req.headers.authorization, forwarded: req.headers['x-forwarded-for'] });
  res.writeHead(req.headers.authorization === 'Bearer synthetic' ? 200 : 401, { 'Content-Type': 'application/json' });
  res.end(JSON.stringify({ synthetic: true }));
});
try {
  await mkdir(join(directory, 'public'));
  await writeFile(join(directory, 'public', 'index.html'), '<main>synthetic public shell</main>');
  await writeFile(join(directory, 'public', 'ngsw.json'), JSON.stringify({ assetGroups: [{ name: 'public-shell' }] }));
  await writeFile(join(directory, 'public', 'ngsw-worker.js'), '// synthetic worker artifact');
  await writeFile(join(directory, 'public', 'manifest.webmanifest'), '{"start_url":"/"}');
  const settingsPath = join(directory, 'settings.json');
  // Windows PowerShell writes UTF-8 BOM: exercise the real operator configuration format.
  await writeFile(settingsPath, '\uFEFF' + JSON.stringify({ apiOrigin: 'http://127.0.0.1:5488', databaseName: 'AlFalahN2DeviceE2E_abcdef',
    assetRoot: join(directory, 'public'), expiresAtUtc: new Date(Date.now() + 60_000).toISOString(), tracePushRegistration: true }));
  api.listen(5488, '127.0.0.1');
  await once(api, 'listening');
  proxy = spawn(process.execPath, [new URL('./serve-n2-device-trial.mjs', import.meta.url).pathname.replace(/^\/([A-Z]:)/, '$1')],
    { env: { ...process.env, ALFALAH_N2_TRIAL_SETTINGS: settingsPath }, stdio: ['ignore', 'pipe', 'pipe'] });
  let error = '';
  let output = '';
  proxy.stdout.on('data', chunk => { output += chunk; });
  proxy.stderr.on('data', chunk => { error += chunk; });
  await Promise.race([once(proxy.stdout, 'data'), once(proxy, 'exit').then(() => { throw new Error(error || 'Proxy exited'); }),
    new Promise((_, reject) => { const timeout = setTimeout(() => reject(new Error('Proxy startup timed out')), 10_000); timeout.unref(); })]);
  const base = 'http://127.0.0.1:4488';
  assert.match(await (await fetch(base + '/notification-entry?target=synthetic')).text(), /synthetic public shell/);
  const manifest = await fetch(base + '/manifest.webmanifest');
  assert.match(manifest.headers.get('content-type'), /application\/manifest\+json/);
  const worker = await fetch(base + '/ngsw-worker.js');
  assert.match(worker.headers.get('content-type'), /javascript/);
  assert.equal(worker.headers.get('service-worker-allowed'), '/');
  assert.equal((await fetch(base + '/missing-worker.js')).status, 404);
  for (const path of ['/swagger/index.html', '/hubs/inbox', '/uploads/private.pdf', '/files/private.pdf', '/appsettings.json',
    '/api/v1/auth/main-manager-login', '/api/v1/auth/forgot-password', '/api/v1/auth/reset-password']) {
    assert.equal((await fetch(base + path)).status, 404, path);
  }
  assert.equal(requests.length, 0, 'Denied paths never reach the upstream');
  assert.equal((await fetch(base + '/api/v1/notification-center/state')).status, 401);
  const authenticated = await fetch(base + '/api/v1/notification-center/state', {
    headers: { Authorization: 'Bearer synthetic', 'X-Forwarded-For': 'synthetic-untrusted' }
  });
  assert.equal(authenticated.status, 200);
  assert.equal(authenticated.headers.get('cache-control'), 'private, no-store');
  assert.equal(requests.at(-1).authorization, 'Bearer synthetic');
  assert.equal(requests.at(-1).forwarded, undefined);
  const registration = await fetch(base + '/api/v1/push/subscriptions', {
    method: 'POST', headers: { Authorization: 'Bearer synthetic', 'Content-Type': 'application/json' },
    body: JSON.stringify({ subscription: 'synthetic-private-registration-material' })
  });
  assert.equal(registration.status, 200);
  await new Promise(resolve => setTimeout(resolve, 30));
  const trace = output.split('\n').filter(line => line.startsWith('{')).map(line => JSON.parse(line));
  assert.equal(trace.length, 1);
  assert.equal(trace[0].event, 'n2-push-registration-result');
  assert.equal(trace[0].status, 200);
  assert.deepEqual(Object.keys(trace[0]).sort(), ['atUtc', 'event', 'status']);
  assert.equal(output.includes('synthetic-private-registration-material'), false);
  assert.equal(output.includes('Bearer'), false);
  assert.equal((await fetch(base + '/', { method: 'POST' })).status, 405);
  process.stdout.write('PASS: synthetic proxy boundaries, Windows settings, API authentication forwarding, private cache headers and PWA asset routing. No provider/phone evidence.\n');
} finally {
  if (proxy && proxy.exitCode === null) { proxy.kill(); await once(proxy, 'exit'); }
  api.closeAllConnections();
  await new Promise(resolve => api.close(resolve));
  await rm(directory, { recursive: true, force: true });
}
