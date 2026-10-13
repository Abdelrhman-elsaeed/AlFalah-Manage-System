import { Component } from '@angular/core';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { SignatureEditorComponent } from '../../../shared/components/signature-editor/signature-editor.component';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { inject } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({ selector: 'app-signature', standalone: true, imports: [CommonModule, RouterLink, TranslateModule, SignatureEditorComponent],
  template: `<div class="signature-page" [dir]="translate.currentLang==='en'?'ltr':'rtl'"><header><h1>{{ 'ACCOUNT.SIGNATURE.TITLE' | translate }}</h1><p>{{ 'ACCOUNT.SIGNATURE.DESCRIPTION' | translate }}</p><a *ngIf="returnUrl" [href]="returnUrl" (click)="returnToDuty($event)" style="display:inline-flex;align-items:center;min-height:44px;color:var(--brand-700,#176647)">{{ 'DUTY_EXEC.RETURN_DRAFT' | translate }}</a></header><section class="surface-card"><app-signature-editor /></section></div>`,
  styles: [`:host ::ng-deep .signature-editor .actions button{min-height:44px}.signature-page{max-width:760px;margin:auto}header{margin-bottom:20px}h1{font-size:24px;color:var(--text-strong)}p{color:var(--text-muted);line-height:1.7}`] })
export class SignatureComponent {
  readonly translate=inject(TranslateService);
  private readonly router=inject(Router);
  private readonly requested = inject(ActivatedRoute).snapshot.queryParamMap.get('returnUrl');
  readonly returnUrl = this.requested?.startsWith('/timetable?tab=duty') ? this.requested : null;
  returnToDuty(event:Event){event.preventDefault();if(this.returnUrl)void this.router.navigateByUrl(this.returnUrl);}
}
