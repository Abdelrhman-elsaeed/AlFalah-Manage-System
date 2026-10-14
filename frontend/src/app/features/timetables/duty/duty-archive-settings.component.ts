import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { DutyDocumentCollection, DutyDocumentsService } from '../../../core/services/duty-documents.service';
import { extractHttpErrorMessage } from '../../../core/http/http-error-message';
import { DutyFeedbackService } from './duty-feedback.service';
import { DutyErrorDialogComponent } from './duty-error-dialog.component';
import { DutyErrorDirective } from './duty-error.directive';

@Component({selector:'app-duty-archive-settings', standalone:true,
  imports:[CommonModule, RouterLink, TranslateModule, DutyErrorDialogComponent, DutyErrorDirective],
  providers:[DutyFeedbackService],
  template:`<app-duty-error-dialog /><span hidden [dutyError]="error()" (dutyErrorDismissed)="error.set('')"></span>
    <article class="duty-archive-settings" [dir]="dir" [attr.aria-busy]="busy()">
      <header><span class="archive-icon"><i class="pi pi-cloud-upload" aria-hidden="true"></i></span><div><small>{{ 'DUTY_ARCHIVE_SETTINGS.EYEBROW' | translate }}</small><h3>{{ 'DUTY_ARCHIVE_SETTINGS.TITLE' | translate }}</h3></div><span class="enabled-state" *ngIf="ready()?.enabled">{{ 'DUTY_ARCHIVE_SETTINGS.ENABLED' | translate }}</span></header>
      <p>{{ 'DUTY_ARCHIVE_SETTINGS.COPY' | translate }}</p>
      <p *ngIf="busy()" role="status"><i class="pi pi-spin pi-spinner" aria-hidden="true"></i> {{ 'DUTY_DOC.LOADING' | translate }}</p>
      <div class="archive-location"><i class="pi pi-folder" aria-hidden="true"></i>{{ 'DUTY_ARCHIVE_SETTINGS.HIERARCHY' | translate }}</div>
      <p class="archive-warning" *ngIf="ready() && ready()?.connectionState !== 'Connected'">{{ 'DUTY_ARCHIVE_SETTINGS.DISCONNECTED' | translate }}</p>
      <p class="archive-warning" *ngIf="ready() && !ready()?.processingEnabled">{{ 'DUTY_ARCHIVE_SETTINGS.PROCESSING_PAUSED' | translate }}</p>
      <p class="archive-success" *ngIf="message()" role="status">{{ message() }}</p>
      <footer><button type="button" class="archive-primary" *ngIf="!ready()?.enabled" [disabled]="busy() || !ready() || ready()?.connectionState !== 'Connected' || !ready()?.processingEnabled" (click)="setEnabled(true)"><i class="pi pi-cloud-upload" aria-hidden="true"></i>{{ 'DUTY_ARCHIVE_SETTINGS.ENABLE' | translate }}</button><button type="button" *ngIf="ready()?.enabled" [disabled]="busy()" (click)="setEnabled(false)">{{ 'DUTY_ARCHIVE_SETTINGS.DISABLE' | translate }}</button><button type="button" [disabled]="busy()" (click)="load()"><i class="pi pi-refresh" aria-hidden="true"></i>{{ 'DUTY_ARCHIVE_SETTINGS.REFRESH' | translate }}</button><a *ngIf="ready()?.connectionState !== 'Connected'" routerLink="/school-manager/evidence-settings">{{ 'DUTY_ARCHIVE_SETTINGS.CONNECT' | translate }}</a></footer>
      <small class="archive-note">{{ 'DUTY_ARCHIVE_SETTINGS.RETAINED' | translate }}</small>
    </article>`,
  styles:[`:host{display:block;margin-top:22px}.duty-archive-settings{padding:26px;border:1px solid var(--border);border-top:3px solid var(--brand-700);border-radius:18px;background:var(--bg-surface);color:var(--text-strong);min-width:0}header{display:flex;align-items:center;gap:14px}header h3{margin:4px 0;font-size:20px}header small{font-size:11px;color:var(--text-muted)}.archive-icon{display:grid;place-items:center;width:48px;height:48px;border-radius:13px;background:var(--brand-50);color:var(--brand-800);font-size:23px;flex-shrink:0}p{font-size:14px;line-height:1.9;color:var(--text-muted);max-width:800px}.archive-location{padding:14px 16px;display:flex;gap:10px;border:1px dashed var(--border);border-radius:10px;font-size:13px;line-height:1.9;overflow-wrap:anywhere}.archive-warning{padding:12px 16px;border:1px solid #e8cd88;border-radius:10px;color:#705012;background:#fff8df}.archive-success{color:var(--brand-800)}footer{display:flex;align-items:center;gap:10px;flex-wrap:wrap;margin:20px 0 14px}button,a{font:inherit;font-size:13px;font-weight:700;min-height:46px;padding:11px 18px;border:1px solid var(--border);background:var(--bg-surface);color:var(--brand-800);border-radius:10px;display:flex;align-items:center;justify-content:center;gap:8px;cursor:pointer;text-decoration:none}.archive-primary{background:var(--brand-800);border-color:var(--brand-800);color:white}button:disabled{opacity:.5;cursor:default}button:focus-visible,a:focus-visible{outline:3px solid var(--gold-600);outline-offset:3px}.archive-note{font-size:11px;color:var(--text-muted);line-height:1.9;display:block}.enabled-state{margin-inline-start:auto;padding:7px 12px;border-radius:8px;background:var(--brand-50);color:var(--brand-800);font-size:11px;font-weight:700}@media(max-width:600px){.duty-archive-settings{padding:20px}header{flex-wrap:wrap}header h3{font-size:18px}.enabled-state{margin-inline-start:62px}footer{align-items:stretch}footer>*{flex:1;min-width:130px}.archive-location{font-size:12px}}`]
})
export class DutyArchiveSettingsComponent implements OnInit {
  private readonly api=inject(DutyDocumentsService);
  private readonly translate=inject(TranslateService);
  readonly ready=signal<DutyDocumentCollection['readiness']|null>(null);
  readonly busy=signal(false); readonly error=signal(''); readonly message=signal('');
  get dir(){return this.translate.currentLang==='en'?'ltr':'rtl';}
  ngOnInit(){void this.load();}
  async load(){
    if(this.busy())return;this.busy.set(true);this.error.set('');
    try{const r=await firstValueFrom(this.api.readiness());if(!r.isSuccess || !r.data)throw new Error(r.message);this.ready.set(r.data);}
    catch(e){this.ready.set(null);this.error.set(extractHttpErrorMessage(e)||this.translate.instant('DUTY_DOC.ERROR'));}
    finally{this.busy.set(false);}
  }
  async setEnabled(enabled:boolean){
    if(this.busy())return;this.busy.set(true);this.error.set('');this.message.set('');
    try{const r=await firstValueFrom(this.api.enable(enabled));if(!r.isSuccess || !r.data)throw new Error(r.message);this.ready.set(r.data);this.message.set(this.translate.instant('DUTY_ARCHIVE_SETTINGS.'+(enabled?'ACTIVATED':'PAUSED')));}
    catch(e){this.error.set(extractHttpErrorMessage(e)||this.translate.instant('DUTY_DOC.ERROR'));}
    finally{this.busy.set(false);}
  }
}
