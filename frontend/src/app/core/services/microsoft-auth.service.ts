import { Injectable, inject } from '@angular/core';
import { MsalService } from '@azure/msal-angular';
import { InteractionRequiredAuthError, AccountInfo } from '@azure/msal-browser';

interface EntraRuntimeConfig {
  clientId?: string;
  tenantId?: string;
  apiScope?: string;
}

export class EntraConfigurationError extends Error {
  constructor() {
    super('Microsoft Entra is not configured.');
    this.name = 'EntraConfigurationError';
  }
}

function config(): EntraRuntimeConfig {
  return (globalThis as typeof globalThis & { __alfalahEntra?: EntraRuntimeConfig }).__alfalahEntra ?? {};
}

@Injectable({ providedIn: 'root' })
export class MicrosoftAuthService {
  private readonly msal = inject(MsalService);

  isConfigured(): boolean {
    return !!config().clientId && !!config().tenantId && !!config().apiScope;
  }

  async getApiToken(interactive = false): Promise<string> {
    if (!this.isConfigured()) throw new EntraConfigurationError();
    const scopes = [config().apiScope!];
    let account: AccountInfo | null = this.msal.instance.getActiveAccount() ?? this.msal.instance.getAllAccounts()[0] ?? null;
    if (!account && interactive) {
      const login = await this.msal.loginPopup({ scopes }).toPromise();
      account = login?.account ?? null;
    }
    if (!account) throw new InteractionRequiredAuthError();
    try {
      const result = await this.msal.acquireTokenSilent({ scopes, account: account as AccountInfo }).toPromise();
      return result!.accessToken;
    } catch (error) {
      if (!interactive || !(error instanceof InteractionRequiredAuthError)) throw error;
      const result = await this.msal.acquireTokenPopup({ scopes, account: account as AccountInfo }).toPromise();
      return result!.accessToken;
    }
  }
}
