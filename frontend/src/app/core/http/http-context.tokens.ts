import { HttpContextToken } from '@angular/common/http';

/**
 * Suppresses the full-page Unauthorized redirect for a background request
 * whose caller has a component-level fallback. It does not alter the HTTP
 * response or any server-side authorization decision.
 */
export const SUPPRESS_FORBIDDEN_REDIRECT = new HttpContextToken<boolean>(() => false);

/** Prevents the local-session interceptor from replacing an Entra API token. */
export const SKIP_LOCAL_AUTH = new HttpContextToken<boolean>(() => false);
