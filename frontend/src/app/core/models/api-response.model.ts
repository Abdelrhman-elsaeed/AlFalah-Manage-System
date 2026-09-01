/** Exact camel-cased shape emitted by AlFalah.Shared.Models.ApiResponse<T>. */
export interface ApiResponse<T = void> {
  readonly isSuccess: boolean;
  readonly message: string;
  readonly data?: T | null;
  readonly errors: readonly string[];
}

/** Exact camel-cased shape emitted by AlFalah.Shared.Models.PagedResult<T>. */
export interface PagedResult<T> {
  readonly items: readonly T[];
  readonly totalCount: number;
  readonly page: number;
  readonly pageSize: number;
  readonly totalPages: number;
  readonly hasNext: boolean;
  readonly hasPrevious: boolean;
}

export type ApiOutcome<T> =
  | { readonly kind: 'success'; readonly data: T; readonly message: string }
  | { readonly kind: 'businessFailure'; readonly errors: readonly string[]; readonly message: string }
  | { readonly kind: 'conflict'; readonly errors: readonly string[]; readonly message: string };

const CONCURRENCY_MARKERS = ['row version', 'rowversion', 'concurrency', 'مستخدم آخر'];

/**
 * Normalizes the mandatory 2xx envelope branch in one place. HTTP failures
 * remain HttpErrorResponses and are handled separately by the interceptor/UI.
 */
export function normalizeApiResponse<T>(response: ApiResponse<T>): ApiOutcome<T> {
  if (response.isSuccess && response.data !== null && response.data !== undefined) {
    return { kind: 'success', data: response.data, message: response.message };
  }

  const errors = [...new Set(response.errors.filter(Boolean))];
  const searchable = `${response.message} ${errors.join(' ')}`.toLocaleLowerCase('en');
  const isCompatibilityConflict = CONCURRENCY_MARKERS.some(marker => searchable.includes(marker));

  return {
    kind: isCompatibilityConflict ? 'conflict' : 'businessFailure',
    errors,
    message: response.message
  };
}
