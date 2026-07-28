import { HttpErrorResponse } from '@angular/common/http';

/**
 * One place that turns a failed HTTP call into the message a user should read.
 *
 * The backend always answers a failure with
 * `{ isSuccess: false, message: "<Arabic>", errors: [...] }`, so the Arabic
 * reason is available — but only if the body can be read. Three shapes reach
 * the client and each was being handled differently (or not at all):
 *
 *  1. **object** — the normal JSON path.
 *  2. **string** — a raw text body, or JSON that Angular did not parse because
 *     the request declared `responseType: 'text'`.
 *  3. **`Blob`** — every file download (`responseType: 'blob'`). A `Blob` is an
 *     object, so code that tested `typeof body === 'object'` accepted it, found
 *     no `.message`, and silently fell back to a generic server error. The real
 *     reason — e.g. "لا يمكن للمعلم تحميل تقرير الزيارة قبل اعتماد مدير
 *     المدرسة." — was sitting unread inside the blob.
 *
 * `Blob.text()` is async, so blob bodies are read up-front by
 * {@link readHttpErrorBody} and cached back onto the error before the message is
 * extracted. Call that first for any blob request.
 */

/** Marker for the parsed body cached onto an error by readHttpErrorBody(). */
const PARSED_BODY = '__parsedErrorBody';

/**
 * Reads a blob error body into text and caches the parsed result on the error,
 * so the synchronous extractor below can see it. Resolves with the same error
 * object; never rejects — a body that cannot be read simply leaves the error
 * unchanged and the caller falls back to its own message.
 */
export async function readHttpErrorBody(error: unknown): Promise<unknown> {
  const body = (error as HttpErrorResponse | undefined)?.error;
  if (!(body instanceof Blob)) return error;

  try {
    const text = await body.text();
    (error as Record<string, unknown>)[PARSED_BODY] = parseBody(text);
  } catch {
    // An unreadable body is not itself an error worth reporting.
  }
  return error;
}

/**
 * Extracts the server's message from a failed request, or `null` when the
 * response carried none. Callers supply their own fallback so the wording can
 * match the action that failed.
 */
export function extractHttpErrorMessage(error: unknown): string | null {
  if (!error || typeof error !== 'object') return null;

  const cached = (error as Record<string, unknown>)[PARSED_BODY];
  if (cached !== undefined) {
    const fromCache = messageFrom(cached);
    if (fromCache) return fromCache;
  }

  return messageFrom((error as HttpErrorResponse).error);
}

/** True when the body still needs an async read before it can be inspected. */
export function isUnreadBlobError(error: unknown): boolean {
  if (!error || typeof error !== 'object') return false;
  if ((error as Record<string, unknown>)[PARSED_BODY] !== undefined) return false;
  return (error as HttpErrorResponse).error instanceof Blob;
}

function parseBody(text: string): unknown {
  const trimmed = text?.trim();
  if (!trimmed) return null;
  try {
    return JSON.parse(trimmed);
  } catch {
    // A plain-text body is a usable message on its own.
    return trimmed;
  }
}

function messageFrom(body: unknown): string | null {
  if (!body) return null;

  if (typeof body === 'string') {
    const parsed = parseBody(body);
    // parseBody returns the same string when it is not JSON.
    return typeof parsed === 'string' ? (parsed || null) : messageFrom(parsed);
  }

  if (typeof body !== 'object') return null;

  const record = body as Record<string, unknown>;
  if (typeof record['message'] === 'string' && record['message'].trim()) {
    return record['message'].trim();
  }
  if (typeof record['error'] === 'string' && record['error'].trim()) {
    return record['error'].trim();
  }
  if (Array.isArray(record['errors'])) {
    const joined = record['errors']
      .filter((entry): entry is string => typeof entry === 'string' && entry.trim().length > 0)
      .join(' — ');
    if (joined) return joined;
  }
  // ASP.NET ProblemDetails / ModelState: { errors: { field: ["msg", …] } }
  if (record['errors'] && typeof record['errors'] === 'object') {
    const joined = Object.values(record['errors'] as Record<string, unknown>)
      .flatMap(value => (Array.isArray(value) ? value : [value]))
      .filter((entry): entry is string => typeof entry === 'string' && entry.trim().length > 0)
      .join(' — ');
    if (joined) return joined;
  }
  if (typeof record['title'] === 'string' && record['title'].trim()) {
    return record['title'].trim();
  }
  return null;
}
