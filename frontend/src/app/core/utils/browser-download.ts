import { HttpResponse } from '@angular/common/http';

export function downloadBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  anchor.rel = 'noopener';
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  setTimeout(() => URL.revokeObjectURL(url), 0);
}

export function fileNameFromResponse(response: HttpResponse<Blob>, fallback: string): string {
  const disposition = response.headers.get('Content-Disposition');
  if (!disposition) return fallback;

  const encoded = /filename\*=UTF-8''([^;]+)/i.exec(disposition)?.[1];
  if (encoded) {
    try {
      return sanitizeFileName(decodeURIComponent(encoded));
    } catch {
      return sanitizeFileName(encoded);
    }
  }

  const quoted = /filename="([^"]+)"/i.exec(disposition)?.[1];
  const plain = /filename=([^;]+)/i.exec(disposition)?.[1];
  return sanitizeFileName(quoted ?? plain ?? fallback);
}

function sanitizeFileName(value: string): string {
  const safe = value.trim().replace(/[\\/:*?"<>|\u0000-\u001f]/g, '_');
  return safe || 'download';
}
