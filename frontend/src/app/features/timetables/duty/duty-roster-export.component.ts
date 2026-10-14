import { Component, Input, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { DutyDocumentsService } from '../../../core/services/duty-documents.service';
import { filenameFromContentDisposition } from '../../../core/services/visits.service';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';
import { DutyErrorDirective } from './duty-error.directive';
import { isDutyPdf } from './duty-pdf-content';

@Component({ selector: 'app-duty-roster-export', standalone: true, imports: [CommonModule, TranslateModule, DutyErrorDirective],
  template: `<button (click)="download()" [disabled]="busy()" [attr.aria-busy]="busy()"><i class="pi" [ngClass]="busy()?'pi-spin pi-spinner':'pi-file-pdf'" aria-hidden="true"></i>{{ 'DUTY_DOC.ROSTER' | translate }}</button><span hidden [dutyError]="error()" (dutyErrorDismissed)="error.set('')"></span>`,
  styles: [':host{display:block}button{font:inherit;font-size:13px;min-height:46px;display:flex;align-items:center;gap:8px;padding:10px 14px;border:1px solid #cbd8cc;border-radius:12px;color:#245b43;background:#fff;cursor:pointer}button:focus-visible{outline:3px solid #b49449;outline-offset:3px}button:disabled{opacity:.6}p{font-size:12px;max-width:230px;color:#8b302d;line-height:1.8}@media(prefers-reduced-motion:reduce){.pi-spin{animation:none}}'] })
export class DutyRosterExportComponent {
  @Input({required:true}) termId = 0; @Input({required:true}) revision = 0;
  private readonly api = inject(DutyDocumentsService);
  private readonly translate = inject(TranslateService);
  readonly busy = signal(false); readonly error = signal('');
  async download() {
    if (this.busy()) return;
    const term = this.termId; this.busy.set(true); this.error.set('');
    try {
      const response = await firstValueFrom(this.api.roster(term, this.revision));
      if (term !== this.termId) return;
      if (!await isDutyPdf(response.body)) throw new Error(this.translate.instant('DUTY_DOC.UNAVAILABLE'));
      if (term !== this.termId) return;
      const url = URL.createObjectURL(response.body!); const a = document.createElement('a'); a.href = url;
      a.download = filenameFromContentDisposition(response.headers.get('Content-Disposition')) || 'school-duty.pdf';
      document.body.append(a); a.click(); a.remove(); setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch (e:any) {
      if (e.error instanceof Blob) { try { this.error.set(JSON.parse(await e.error.text()).message); } catch { this.error.set(this.translate.instant('DUTY_DOC.UNAVAILABLE')); } }
      else this.error.set(extractHttpErrorMessage(e) || this.translate.instant('DUTY_DOC.UNAVAILABLE'));
    } finally { this.busy.set(false); }
  }
}
