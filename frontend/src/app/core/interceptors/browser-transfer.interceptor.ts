import { HttpInterceptorFn, HttpResponse } from '@angular/common/http';
import { map } from 'rxjs';
import { environment } from '../../../environments/environment';

export const BROWSER_TRANSFER_TYPE = 'application/vnd.alfalah.transfer';

/** Receive bytes before offering the browser a download; never suppress failures. */
export const browserTransferInterceptor: HttpInterceptorFn = (request, next) => {
  if (request.responseType !== 'blob' || !request.url.startsWith(`${environment.apiUrl}/api/`)) return next(request);
  // Legacy export paths stay available for direct callers. Application requests
  // use an extension-free alias so extensions cannot preempt by URL suffix.
  const url = request.url.replace('/school-duty/plans/roster.pdf', '/school-duty/plans/roster/content');
  return next(request.clone({ url, setHeaders: { Accept: BROWSER_TRANSFER_TYPE } })).pipe(map(event => {
    if (!(event instanceof HttpResponse) || !(event.body instanceof Blob) ||
        event.headers.get('Content-Type')?.split(';')[0] !== BROWSER_TRANSFER_TYPE) return event;
    const type = event.headers.get('X-AlFalah-Content-Type') || 'application/octet-stream';
    let headers = event.headers.set('Content-Type', type);
    const name = event.headers.get('X-AlFalah-File-Name');
    if (name) headers = headers.set('Content-Disposition', `attachment; filename*=UTF-8''${name}`);
    return event.clone({ body: event.body.slice(0, event.body.size, type), headers });
  }));
};
