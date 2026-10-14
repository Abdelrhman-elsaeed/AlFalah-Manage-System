import { AfterViewInit, Component, DestroyRef, ElementRef, Input, OnChanges, ViewChild, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { DutyErrorDirective } from './duty-error.directive';
import type { PDFDocumentLoadingTask, PDFDocumentProxy, RenderTask } from 'pdfjs-dist';

@Component({ selector: 'app-duty-pdf-preview', standalone: true, imports: [CommonModule, TranslateModule, DutyErrorDirective],
  template: `<div class="preview-toolbar"><span>{{ 'DUTY_DOC.PAGES' | translate:{count:pages()} }}</span><div><button (click)="zoom(-.25)" [disabled]="scale()<=1 || loading()" [attr.aria-label]="'DUTY_DOC.ZOOM_OUT' | translate"><i class="pi pi-minus" aria-hidden="true"></i></button><bdi>{{ scale()*100 }}%</bdi><button (click)="zoom(.25)" [disabled]="scale()>=2 || loading()" [attr.aria-label]="'DUTY_DOC.ZOOM_IN' | translate"><i class="pi pi-plus" aria-hidden="true"></i></button></div></div><p class="preview-message" *ngIf="loading()" role="status"><i class="pi pi-spin pi-spinner" aria-hidden="true"></i>{{ 'DUTY_DOC.PREPARING_PREVIEW' | translate }}</p><span hidden [dutyError]="error() ? ('DUTY_DOC.PREVIEW_ERROR' | translate) : ''" (dutyErrorDismissed)="error.set(false)"></span><div #stage class="pdf-pages" tabindex="0" [attr.aria-label]="'DUTY_DOC.PREVIEW_PAGES' | translate"></div>`,
  styles: [':host{display:block}.preview-toolbar{display:flex;justify-content:space-between;align-items:center;gap:12px;color:var(--text-muted,#67756d);font-size:12px;margin-bottom:12px}.preview-toolbar div{display:flex;align-items:center;gap:12px}.preview-toolbar button{width:44px;height:44px;border-radius:10px;border:1px solid var(--border-strong,#cbd2c9);background:white;color:var(--brand-900,#12563a);cursor:pointer}.preview-toolbar button:disabled{opacity:.4}.preview-toolbar button:focus-visible,.pdf-pages:focus-visible{outline:3px solid var(--gold-700,#946800);outline-offset:3px}.pdf-pages{max-height:62vh;min-height:280px;overflow:auto;overscroll-behavior:contain;background:#e9eee8;border:1px solid var(--border,#e2e5df);border-radius:12px;padding:16px;direction:ltr}.preview-message{font-size:13px;display:flex;gap:8px;align-items:center;color:var(--text-muted,#67756d)}.error{color:#8b302d}@media(max-width:600px){.pdf-pages{max-height:57vh;padding:10px}}@media(prefers-reduced-motion:reduce){.pi-spin{animation:none}}'] })
export class DutyPdfPreviewComponent implements AfterViewInit, OnChanges {
  @Input({required:true}) blob!: Blob;
  @ViewChild('stage',{static:true}) stage!: ElementRef<HTMLDivElement>;
  private readonly destroy = inject(DestroyRef); private readonly translate = inject(TranslateService);
  readonly loading = signal(true); readonly error = signal(false); readonly pages = signal(0); readonly scale = signal(1);
  private pdf: PDFDocumentProxy | null = null; private task: PDFDocumentLoadingTask | null = null;
  private renders: RenderTask[] = []; private sequence=0; private ready=false; private resize: ResizeObserver | null=null;
  private resizeTimer: ReturnType<typeof setTimeout> | null=null; private width=0;
  constructor() { this.destroy.onDestroy(()=>{this.sequence++;this.resize?.disconnect();if(this.resizeTimer)clearTimeout(this.resizeTimer);this.cancel();void this.task?.destroy();}); }
  ngAfterViewInit() {
    this.ready=true;void this.load();
    this.resize=new ResizeObserver(entries=>{const width=entries[0].contentRect.width;if(Math.abs(width-this.width)<8)return;this.width=width;if(this.resizeTimer)clearTimeout(this.resizeTimer);this.resizeTimer=setTimeout(()=>{if(this.pdf)void this.render();},180);});
    this.resize.observe(this.stage.nativeElement);
  }
  ngOnChanges() { if(this.ready)void this.load(); }
  private cancel() { for(const task of this.renders)task.cancel();this.renders=[]; }
  private async load() {
    const sequence=++this.sequence;this.cancel();this.loading.set(true);this.error.set(false);
    try {
      if(this.task)await this.task.destroy();this.pdf=null;
      const pdfjs=await import('pdfjs-dist');if(sequence!==this.sequence)return;
      pdfjs.GlobalWorkerOptions.workerSrc=new URL('assets/pdfjs/pdf.worker.min.mjs',document.baseURI).href;
      this.task=pdfjs.getDocument({data:new Uint8Array(await this.blob.arrayBuffer()),isEvalSupported:false});
      const pdf=await this.task.promise;if(sequence!==this.sequence){await pdf.destroy();return;}
      this.pdf=pdf;this.pages.set(pdf.numPages);await this.render();
    } catch { if(sequence===this.sequence){this.error.set(true);this.loading.set(false);} }
  }
  zoom(change:number) {this.scale.set(Math.max(1,Math.min(2,this.scale()+change)));void this.render();}
  private async render() {
    if(!this.pdf)return;const sequence=++this.sequence;const pdf=this.pdf;this.cancel();this.loading.set(true);this.error.set(false);
    const stage=this.stage.nativeElement;stage.replaceChildren();
    const available=Math.max(220,stage.clientWidth-34);const ratio=Math.min(2,window.devicePixelRatio||1);
    try {
      for(let number=1;number<=pdf.numPages;number++) {
        const page=await pdf.getPage(number);if(sequence!==this.sequence)return;
        const view=page.getViewport({scale:available/page.getViewport({scale:1}).width*this.scale()});
        const sheet=document.createElement('div');sheet.style.cssText='margin:0 auto 18px;width:max-content;max-width:none;';
        const caption=document.createElement('p');caption.textContent=this.translate.instant('DUTY_DOC.PAGE',{number});caption.style.cssText='font-size:11px;color:#607368;text-align:center;margin:8px 0;';
        const canvas=document.createElement('canvas');canvas.width=Math.round(view.width*ratio);canvas.height=Math.round(view.height*ratio);
        canvas.style.cssText=`display:block;width:${view.width}px;height:${view.height}px;background:white;box-shadow:0 3px 14px #1739271a;`;
        canvas.setAttribute('role','img');canvas.setAttribute('aria-label',caption.textContent!);sheet.append(caption,canvas);stage.append(sheet);
        const task=page.render({canvasContext:canvas.getContext('2d')!,viewport:view,transform:ratio===1?undefined:[ratio,0,0,ratio,0,0]});
        this.renders.push(task);await task.promise;if(sequence!==this.sequence)return;
      }
    } catch {if(sequence===this.sequence)this.error.set(true);}
    finally {if(sequence===this.sequence){this.loading.set(false);this.renders=[];}}
  }
}
