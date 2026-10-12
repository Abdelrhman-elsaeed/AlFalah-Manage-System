import { Component } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { SignatureEditorComponent } from '../../../shared/components/signature-editor/signature-editor.component';

@Component({ selector: 'app-signature', standalone: true, imports: [TranslateModule, SignatureEditorComponent],
  template: `<div class="signature-page" dir="rtl"><header><h1>{{ 'ACCOUNT.SIGNATURE.TITLE' | translate }}</h1><p>{{ 'ACCOUNT.SIGNATURE.DESCRIPTION' | translate }}</p></header><section class="surface-card"><app-signature-editor /></section></div>`,
  styles: [`.signature-page{max-width:760px;margin:auto}header{margin-bottom:20px}h1{font-size:24px;color:var(--text-strong)}p{color:var(--text-muted);line-height:1.7}`] })
export class SignatureComponent {}
