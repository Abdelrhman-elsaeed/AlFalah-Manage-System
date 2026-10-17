import { CommonModule } from '@angular/common';
import { Component, DestroyRef, EventEmitter, HostListener, Input, OnInit, Output, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { DialogModule } from 'primeng/dialog';
import { Observable, firstValueFrom } from 'rxjs';
import { CoverageRegisterService } from '../../../core/services/coverage-register.service';
import { CoverageRegisterContext, CoverageRegisterInfo } from '../../../core/models/coverage-register.models';
import { ApiResponse } from '../../../core/models/api-response.model';
import { extractHttpErrorMessage, readHttpErrorBody } from '../../../core/http/http-error-message';
import { downloadBlob } from '../../../core/utils/browser-download';
import { DutyPdfPreviewComponent } from '../../timetables/duty/duty-pdf-preview.component';
import { isDutyPdf } from '../../timetables/duty/duty-pdf-content';

@Component({selector:'app-coverage-register',standalone:true,imports:[CommonModule,FormsModule,TranslateModule,DialogModule,DutyPdfPreviewComponent],
  templateUrl:'./coverage-register.component.html',styleUrl:'./coverage-register.component.css'})
export class CoverageRegisterComponent implements OnInit {
  @Input({required:true}) date!:string;
  @Output() closed=new EventEmitter<void>();
  private readonly api=inject(CoverageRegisterService);private readonly destroy=inject(DestroyRef);private readonly translate=inject(TranslateService);
  context:CoverageRegisterContext|null=null;selected:CoverageRegisterInfo|null=null;blob:Blob|null=null;
  busy=false;issuing=false;error='';historyWarning='';page=1;private pendingSource:string|null=null;
  ngOnInit(){void this.load();}
  canLeave(){return !this.issuing;}
  @HostListener('window:beforeunload',['$event']) beforeUnload(event:BeforeUnloadEvent){if(this.issuing){event.preventDefault();event.returnValue='';}}
  close(){if(!this.busy)this.closed.emit();}
  private async read<T>(request:Observable<ApiResponse<T>>){const r=await firstValueFrom(request.pipe(takeUntilDestroyed(this.destroy)));if(!r.isSuccess||r.data==null)throw Error(r.errors?.[0]||r.message);return r.data;}
  private async run(action:()=>Promise<void>){if(this.busy)return;this.busy=true;this.error='';try{await action();}catch(e){await readHttpErrorBody(e);this.error=extractHttpErrorMessage(e)||(e instanceof Error && e.name==='Error'?e.message:null)||this.translate.instant('COVERAGE_REGISTER.FAILED');}finally{this.busy=false;this.issuing=false;}}
  private async refreshHistory(page:number){const context=await this.read(this.api.context(this.date,page));this.context=context;this.page=page;this.historyWarning='';}
  async load(){await this.run(async()=>{await this.refreshHistory(this.page);this.pendingSource=null;});}
  async changeDate(){this.blob=null;this.selected=null;this.context=null;this.pendingSource=null;this.page=1;await this.load();}
  async changePage(delta:number){await this.run(async()=>{await this.refreshHistory(this.page+delta);this.pendingSource=null;});}
  async prepare(){if(!this.context?.sourceRevision)return;await this.run(async()=>{
    this.issuing=true;this.pendingSource??=this.context!.sourceRevision!;
    const issued=await this.read(this.api.issue(this.date,this.pendingSource));this.selected=issued;this.blob=null;
    await this.fetchContent(issued);
    // A failed secondary refresh must neither mix history pages nor invalidate the retained preview.
    try{await this.refreshHistory(1);}catch{this.historyWarning=this.translate.instant('COVERAGE_REGISTER.HISTORY_REFRESH_FAILED');}
  });}
  async open(info:CoverageRegisterInfo){await this.run(async()=>{this.blob=null;this.selected=info;await this.fetchContent(info);});}
  private async fetchContent(info:CoverageRegisterInfo){
    const response=await firstValueFrom(this.api.content(info.id).pipe(takeUntilDestroyed(this.destroy)));
    if(!await isDutyPdf(response.body))throw Error(this.translate.instant('COVERAGE_REGISTER.INVALID_PDF'));
    const hash=[...new Uint8Array(await crypto.subtle.digest('SHA-256',await response.body!.arrayBuffer()))].map(n=>n.toString(16).padStart(2,'0')).join('').toUpperCase();
    if(hash!==info.sha256.toUpperCase())throw Error(this.translate.instant('COVERAGE_REGISTER.INTEGRITY'));
    this.blob=response.body;
  }
  download(){if(this.blob&&this.selected)downloadBlob(this.blob,this.selected.fileName);}
}
