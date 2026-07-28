import { HttpContextToken } from '@angular/common/http';

/**
 * Suppresses the full-page Unauthorized redirect for a background request
 * whose caller has a component-level fallback. It does not alter the HTTP
 * response or any server-side authorization decision.
 */
export const SUPPRESS_FORBIDDEN_REDIRECT = new HttpContextToken<boolean>(() => false);

/** Prevents the local-session interceptor from replacing an Entra API token. */
export const SKIP_LOCAL_AUTH = new HttpContextToken<boolean>(() => false);

/**
 * Suppresses the global error toast because the caller shows its own.
 *
 * Needed by the file-download endpoints (visit PDF, visits ZIP, attendance
 * sheet, evidence export). Those use `responseType: 'blob'`, so on failure
 * Angular hands back a `Blob` in `error.error` instead of the parsed
 * `ApiResponse`. The interceptor could not read it, fell back to the generic
 * "خطأ في الخادم", and the component then raised its own toast — so one failed
 * download produced two messages, the louder of which was the wrong one.
 *
 * Callers that set this MUST report the failure themselves; use
 * `extractHttpErrorMessage()` so the server's Arabic reason survives.
 */
export const SUPPRESS_ERROR_TOAST = new HttpContextToken<boolean>(() => false);
