// Temporary synthetic-device trial only. Never proxies the owner's operational API.
import { createServer, request as proxyRequest } from 'node:http';
import { createReadStream, readFileSync, realpathSync, statSync } from 'node:fs';
import { extname, resolve, sep, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const settingsPath = process.env.ALFALAH_N2_TRIAL_SETTINGS;
if (!settingsPath) throw new Error('ALFALAH_N2_TRIAL_SETTINGS must point to the private trial settings.');
const settings = JSON.parse(readFileSync(settingsPath, 'utf8').replace(/^\uFEFF/, ''));
const api = new URL(settings.apiOrigin);
const repo = realpathSync(resolve(dirname(fileURLToPath(import.meta.url)), '../..'));
const privateDirectory = realpathSync(dirname(settingsPath));
if (privateDirectory === repo || privateDirectory.startsWith(repo + sep)) throw new Error('Trial secrets must stay outside the repository.');
// Keep regression fixtures off the active phone trial. Neither pair can target the main API.
if (api.protocol !== 'http:' || api.hostname !== '127.0.0.1' || !['5487', '5488'].includes(api.port)
    || !/^AlFalahN2DeviceE2E_[a-f0-9]+$/.test(settings.databaseName)) throw new Error('Only the isolated loopback trial API is allowed.');
const root = realpathSync(settings.assetRoot);
const worker = JSON.parse(readFileSync(resolve(root, 'ngsw.json'), 'utf8'));
if (!worker.assetGroups.some(group => group.name === 'public-shell')) throw new Error('Build the optimized N2 worker artifact first.');
const expiry = Date.parse(settings.expiresAtUtc);
if (!Number.isFinite(expiry) || expiry <= Date.now() || expiry > Date.now() + 4 * 3600_000) throw new Error('Trial expiry must be within four hours.');
const mime = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.mjs': 'text/javascript', '.css': 'text/css',
  '.json': 'application/json', '.webmanifest': 'application/manifest+json', '.png': 'image/png', '.ico': 'image/x-icon',
  '.woff2': 'font/woff2', '.svg': 'image/svg+xml', '.webp': 'image/webp', '.jpg': 'image/jpeg' };
const server = createServer((req, res) => {
  const reject = status => { res.writeHead(status, { 'Cache-Control': 'no-store' }); res.end(); };
  if (Date.now() >= expiry) return reject(410);
  let path;
  try { path = decodeURIComponent(new URL(req.url, 'http://localhost').pathname); } catch { return reject(400); }
  // The trial exposes the real school APIs only to their normal authenticated callers.
  // No Swagger, token-bearing hub query strings, fixture controls, files or server config.
  if (/^\/(swagger|hubs|uploads|files)(\/|$)/i.test(path)) return reject(404);
  if (path.startsWith('/api/')) {
    if (/\/auth\/(main-manager-login|forgot-password|reset-password)/i.test(path)) return reject(404);
    const traceRegistration = settings.tracePushRegistration === true && req.method === 'POST' && path === '/api/v1/push/subscriptions';
    const trace = status => {
      if (traceRegistration) process.stdout.write(JSON.stringify({ event: 'n2-push-registration-result', status, atUtc: new Date().toISOString() }) + '\n');
    };
    const headers = { ...req.headers, host: api.host };
    delete headers['x-forwarded-for']; delete headers['x-forwarded-host']; delete headers['x-forwarded-proto'];
    const upstream = proxyRequest({ hostname: api.hostname, port: api.port, method: req.method, path: req.url, headers }, response => {
      trace(response.statusCode ?? 502);
      res.writeHead(response.statusCode ?? 502, { ...response.headers, 'Cache-Control': 'private, no-store' }); response.pipe(res);
    });
    upstream.setTimeout(30_000, () => upstream.destroy());
    upstream.on('error', () => { if (!res.headersSent) { trace(502); reject(502); } else res.destroy(); });
    req.on('aborted', () => upstream.destroy()); req.pipe(upstream); return;
  }
  if (!['GET', 'HEAD'].includes(req.method)) return reject(405);
  let file = resolve(root, '.' + path);
  if (file !== root && !file.startsWith(root + sep)) return reject(404);
  try {
    if (!statSync(file).isFile()) file = resolve(root, 'index.html');
  } catch {
    if (extname(path)) return reject(404); // Real missing worker/manifest must never become SPA HTML.
    file = resolve(root, 'index.html');
  }
  try { if (!realpathSync(file).startsWith(root + sep)) return reject(404); } catch { return reject(404); }
  res.writeHead(200, { 'Content-Type': mime[extname(file)] ?? 'application/octet-stream', 'Cache-Control': 'no-cache',
    'Service-Worker-Allowed': '/', 'X-Content-Type-Options': 'nosniff' });
  if (req.method === 'HEAD') res.end(); else createReadStream(file).on('error', () => res.destroy()).pipe(res);
});
const proxyPort = api.port === '5488' ? 4488 : 4487;
server.listen(proxyPort, '127.0.0.1', () => process.stdout.write(`Isolated N2 device fixture listening on loopback port ${proxyPort}.\n`));
const stop = () => server.close(() => process.exit(0));
setTimeout(stop, Math.max(1, expiry - Date.now())).unref();
process.on('SIGINT', stop); process.on('SIGTERM', stop);
