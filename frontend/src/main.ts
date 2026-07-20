import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { AppComponent } from './app/app.component';
import { environment } from './environments/environment';

interface EntraRuntimeConfig {
  clientId?: string;
  tenantId?: string;
  apiScope?: string;
  redirectUri?: string;
}

async function loadEntraRuntimeConfig(): Promise<void> {
  try {
    const response = await fetch(`${environment.apiUrl}/api/v1/auth/entra-config`);
    if (!response.ok) return;

    const config = await response.json() as EntraRuntimeConfig & { isConfigured?: boolean };
    if (!config.isConfigured) return;

    (globalThis as typeof globalThis & { __alfalahEntra?: EntraRuntimeConfig }).__alfalahEntra = {
      clientId: config.clientId,
      tenantId: config.tenantId,
      apiScope: config.apiScope,
      redirectUri: globalThis.location.origin
    };
  } catch {
    // The app remains usable without the optional OneDrive integration.
  }
}

function checkI18nDuplicateKeys(lang: string): void {
  // Lightweight runtime guard (development only): fetch each i18n file as
  // text and detect duplicate TOP-LEVEL namespace keys only.
  // We track brace depth so nested keys like SCHOOLS.TITLE vs USERS.TITLE
  // are NOT counted as duplicates of each other.
  fetch(`./assets/i18n/${lang}.json`)
    .then(r => r.text())
    .then(raw => {
      const seen = new Set<string>();
      const dupes: string[] = [];
      let depth = 0;
      // Match either a brace OR a key pattern
      const re = /(\{|\})|"([A-Z][A-Z0-9_]*)"\s*:/g;
      let m: RegExpExecArray | null;
      while ((m = re.exec(raw)) !== null) {
        if (m[1] === '{') { depth++; continue; }
        if (m[1] === '}') { depth--; continue; }
        // Only check true top-level keys (depth 1 = inside the root object)
        if (depth === 1 && m[2]) {
          const key = m[2];
          if (seen.has(key)) dupes.push(key);
          else seen.add(key);
        }
      }
      if (dupes.length) {
        // eslint-disable-next-line no-console
        console.error(
          `[i18n] DUPLICATE top-level keys detected in ${lang}.json: ` +
          dupes.join(', ') +
          '. JSON.parse keeps only the last occurrence — fix the merge.'
        );
      }
    })
    .catch(() => {/* ignore in production / offline */});
}

async function bootstrap(): Promise<void> {
  await loadEntraRuntimeConfig();
  ['ar', 'en'].forEach(checkI18nDuplicateKeys);
  await bootstrapApplication(AppComponent, appConfig);
}

void bootstrap().catch((err) => console.error(err));
