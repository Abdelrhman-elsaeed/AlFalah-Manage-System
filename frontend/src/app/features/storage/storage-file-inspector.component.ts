import { CommonModule } from '@angular/common';
import { Component, DestroyRef, EventEmitter, Input, OnChanges, Output, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslateService } from '@ngx-translate/core';
import { firstValueFrom, forkJoin } from 'rxjs';
import { StorageApiService } from './storage-api.service';
import { StorageEvidenceApiService } from './evidence-api.service';
import { EvidenceWorkspaceComponent } from './evidence-workspace.component';
import { StorageContext, StorageDetails } from './storage.models';
import { downloadBlob } from '../../core/utils/browser-download';
import { extractHttpErrorMessage } from '../../core/http/http-error-message';

/** One inspector in the native school browser, using the existing ledger/workflows. */
@Component({selector:'app-storage-file-inspector',standalone:true,imports:[CommonModule,FormsModule,EvidenceWorkspaceComponent],templateUrl:'./storage-file-inspector.component.html',styleUrls:['./storage-file-inspector.component.css']})
export class StorageFileInspectorComponent implements OnChanges {
 private readonly api=inject(StorageApiService);private readonly evidence=inject(StorageEvidenceApiService);private readonly translate=inject(TranslateService);private readonly destroy=inject(DestroyRef);
 @Input({required:true}) fileId!:number;@Input({required:true}) folderId!:number;@Input() year?:number;
 @Output() changed=new EventEmitter<void>();@Output() denied=new EventEmitter<void>();@Output() failure=new EventEmitter<string>();
 details?:StorageDetails;context?:StorageContext;tab:'info'|'links'|'changes'='info';linksVisited=false;changesVisited=false;busy=false;loading=false;error='';renameName='';private sequence=0;private alive=true;
 constructor(){this.destroy.onDestroy(()=>{this.alive=false;this.sequence++;});}
 t(ar:string,en:string){return this.translate.currentLang==='en'?en:ar;}
 get historical(){return !!this.year&&!!this.context?.academicYearId&&this.year!==this.context.academicYearId;}
 get manage(){return !!this.context?.canManage&&!this.historical&&this.details?.file.state!=='Withdrawn';}
 get review(){return !!this.context?.canReviewEvidence&&!this.historical;}
 ngOnChanges(){void this.load();}
 async load(){const sequence=++this.sequence;this.details=undefined;this.context=undefined;this.loading=true;this.error='';try{
  const [details,context]=await firstValueFrom(forkJoin([this.api.details(this.fileId,this.year,this.folderId,false),this.api.contextInfo(false)]));
  if(!this.alive||sequence!==this.sequence)return;this.details=details;this.context=context;this.renameName=details.file.displayName;
 }catch(e){if(!this.alive||sequence!==this.sequence)return;this.fail(e);}finally{if(sequence===this.sequence)this.loading=false;}}
 select(tab:'info'|'links'|'changes'){this.tab=tab;if(tab==='links')this.linksVisited=true;if(tab==='changes')this.changesVisited=true;}
 private fail(e:unknown){const status=(e as {status?:number})?.status;this.error=extractHttpErrorMessage(e)||(status==null&&e instanceof Error?e.message:this.t('تعذر تحميل تفاصيل الملف. أعد المحاولة.','File details could not be loaded. Try again.'));if(status===401||status===403){this.details=undefined;this.context=undefined;this.denied.emit();}else this.failure.emit(this.error);}
 async download(version:number){if(this.busy||!this.details)return;this.busy=true;try{const file=this.details.file;const blob=await firstValueFrom(this.evidence.versionContent(file.storedFileId,version));if(this.alive)downloadBlob(blob,file.displayName);}catch(e){this.fail(e);}finally{this.busy=false;}}
 async rename(){if(this.busy||!this.details||!this.manage||!this.renameName.trim())return;this.busy=true;try{const result=await firstValueFrom(this.api.rename(this.details.file,this.renameName.trim()));if(!result.isSuccess)throw new Error(result.message);this.api.invalidateDetails();this.changed.emit();}catch(e){this.fail(e);}finally{this.busy=false;}}
 updated(){this.api.invalidateDetails();this.changed.emit();}
}
