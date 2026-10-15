import { CommonModule } from '@angular/common';
import { Component, DestroyRef, ElementRef, ViewChild, inject, signal } from '@angular/core';
import { HttpClient, HttpContext, HttpParams, HttpEventType } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../../core/models/api-response.model';
import { SUPPRESS_ERROR_TOAST, SUPPRESS_FORBIDDEN_REDIRECT } from '../../core/http/http-context.tokens';
import { extractHttpErrorMessage } from '../../core/http/http-error-message';
import { DutyPdfPreviewComponent } from '../timetables/duty/duty-pdf-preview.component';
import { StorageApiService } from './storage-api.service';
import { downloadBlob } from '../../core/utils/browser-download';
import { StorageFileInspectorComponent } from './storage-file-inspector.component';

interface DriveItem {handle:string;name:string;isFolder:boolean;size:number|null;mimeType:string;modifiedAt:string|null;canModify:boolean;protection:string|null;storedFileId?:number;folderId?:number;}
interface Crumb {handle:string;name:string;}
interface DrivePage {name:string;handle:string|null;breadcrumbs:Crumb[];items:DriveItem[];nextPageToken:string|null;canManage:boolean;writableFolderId?:number|null;}
interface Impact {ticket:string;name:string;action:string;files:number;folders:number;linkedTo:string[];linkedCount:number;signedDocuments:number;warning:string;}
interface Operation {id:number;status:string;action:string;name:string;errorCode:string|null;}
@Component({selector:'app-storage-drive-browser',standalone:true,imports:[CommonModule,FormsModule,DutyPdfPreviewComponent,StorageFileInspectorComponent],
 templateUrl:'./storage-drive-browser.component.html',styleUrls:['./storage-drive-browser.component.css']})
export class StorageDriveBrowserComponent {
 private readonly http=inject(HttpClient);private readonly router=inject(Router);private readonly route=inject(ActivatedRoute);
 private readonly translate=inject(TranslateService);private readonly destroy=inject(DestroyRef);
 private readonly library=inject(StorageApiService);
 private readonly base=`${environment.apiUrl}/api/v1/storage/drive`;
 private readonly context=new HttpContext().set(SUPPRESS_ERROR_TOAST,true).set(SUPPRESS_FORBIDDEN_REDIRECT,true);
 private alive=true;
 @ViewChild('actionDialog') dialog?:ElementRef<HTMLDialogElement>;
 @ViewChild('previewDialog') previewDialog?:ElementRef<HTMLDialogElement>;
 @ViewChild('errorDialog') errorDialog?:ElementRef<HTMLDialogElement>;
 @ViewChild('deletedDialog') deletedDialog?:ElementRef<HTMLDialogElement>;
 @ViewChild('createDialog') createDialog?:ElementRef<HTMLDialogElement>;
 @ViewChild('uploadInput') uploadInput?:ElementRef<HTMLInputElement>;
 readonly data=signal<DrivePage|null>(null);readonly loading=signal(false);readonly busy=signal(false);readonly operations=signal<Operation[]>([]);
 readonly error=signal('');readonly notice=signal('');readonly preview=signal<Blob|null>(null);
 readonly deleted=signal<Operation[]>([]);
 readonly progress=signal(0);readonly uploading=signal(false);
 fileInfo:DriveItem|null=null;previewUrl='';folderName='';createError='';
 inspectorOpen=false;inspectorVisited=false;
 readonlyYear=false;
 private uploadKey='';private pendingUpload:File|null=null;private uploadFolder=0;
 search='';selected:DriveItem|null=null;action='Delete';name='';impact:Impact|null=null;actionError='';previewName='';
 destinations:DrivePage|null=null;destination:Crumb|null=null;private origin:HTMLElement|null=null;private request=0;private key='';
 get en(){return this.translate.currentLang==='en';}
 get rows(){const q=this.search.trim().toLocaleLowerCase();return this.data()?.items.filter(x=>!q||x.name.toLocaleLowerCase().includes(q))??[];}
 t(ar:string,en:string){return this.en?en:ar;}
 constructor(){this.destroy.onDestroy(()=>{this.alive=false;if(this.previewUrl)URL.revokeObjectURL(this.previewUrl);});this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroy)).subscribe(()=>{this.search='';void this.load();});}
 private async get<T>(path:string,params=new HttpParams()):Promise<T>{const r=await firstValueFrom(this.http.get<ApiResponse<T>>(this.base+path,{params,context:this.context}));if(!r.isSuccess||r.data==null)throw new Error(r.message||this.t('تعذّر إتمام الطلب.','The request could not be completed.'));return r.data;}
 private async post<T>(path:string,body:unknown):Promise<T>{const r=await firstValueFrom(this.http.post<ApiResponse<T>>(this.base+path,body,{context:this.context}));if(!r.isSuccess||r.data==null)throw new Error(r.message||this.t('تعذّر إتمام الطلب.','The request could not be completed.'));return r.data;}
 private message(e:unknown){return extractHttpErrorMessage(e)||(e instanceof Error?e.message:this.t('تعذّر الاتصال. أعد المحاولة.','Connection unavailable. Try again.'));}
 async load(more=false){const sequence=++this.request;this.loading.set(true);this.error.set('');
  let params=new HttpParams();const handle=this.route.snapshot.queryParamMap.get('drive');if(handle)params=params.set('handle',handle);if(more&&this.data()?.nextPageToken)params=params.set('pageToken',this.data()!.nextPageToken!);
  try{let selected:DriveItem|null=null;const query=this.route.snapshot.queryParamMap;
   if(query.has('folder')||query.has('file')){let lookup=new HttpParams();if(query.has('folder'))lookup=lookup.set('folderId',query.get('folder')!);if(query.has('file'))lookup=lookup.set('fileId',query.get('file')!);
    const resolved=await this.get<{handle:string;openFile:DriveItem|null}>('/resolve',lookup);if(sequence!==this.request)return;params=params.set('handle',resolved.handle);selected=resolved.openFile;}
   let page=await this.get<DrivePage>('',params);if(sequence!==this.request)return;this.readonlyYear=false;
   if(this.selectedYear){const context=await firstValueFrom(this.library.contextInfo(false));if(sequence!==this.request)return;this.readonlyYear=!!context.academicYearId&&context.academicYearId!==this.selectedYear;if(this.readonlyYear)page={...page,writableFolderId:null};}
   this.data.set(more?{...page,items:[...(this.data()?.items??[]),...page.items]}:page);
   if(page.canManage)this.operations.set(await this.get<Operation[]>('/operations'));else this.operations.set([]);
   if(selected&&!more&&sequence===this.request&&!this.previewDialog?.nativeElement.open)await this.file(selected,false);
  }catch(e){if(sequence!==this.request)return;if(!more)this.data.set(null);this.error.set(this.message(e));}finally{if(sequence===this.request)this.loading.set(false);}}
 open(item:DriveItem){if(this.busy())return;if(item.isFolder)this.go(item.handle);else void this.file(item,false);}
 go(handle:string|null){void this.router.navigate([],{relativeTo:this.route,queryParams:{drive:handle,display:null,folder:null,file:null,search:null},queryParamsHandling:'merge'});}
 size(value:number|null){if(value==null)return '—';return value<1024?value+' B':value<1048576?(value/1024).toFixed(1)+' KB':(value/1048576).toFixed(1)+' MB';}
 fileType(item:DriveItem){if(item.mimeType==='application/pdf')return 'PDF';if(item.mimeType.startsWith('image/'))return this.t('صورة','Image');return item.name.split('.').length>1?item.name.split('.').pop()!.toUpperCase():this.t('ملف','File');}
 async file(item:DriveItem,download:boolean){if(this.busy())return;const folder=this.data()?.handle;this.busy.set(true);this.error.set('');try{
   if(!download){this.fileInfo=item;this.previewName=item.name;this.origin=document.activeElement as HTMLElement;this.previewDialog?.nativeElement.showModal();
    if(item.mimeType!=='application/pdf'&&!/^image\/(png|jpeg|webp|gif)$/.test(item.mimeType))return;}
   const blob=await firstValueFrom(this.http.get(this.base+'/content',{params:{handle:item.handle},responseType:'blob',context:this.context}));
   if(!this.alive||folder!==this.data()?.handle)return;
   if(!download){this.preview.set(blob);if(item.mimeType!=='application/pdf')this.previewUrl=URL.createObjectURL(blob);}
   else downloadBlob(blob,item.name);
  }catch(e){this.showError(this.message(e));}finally{this.busy.set(false);}}
 closePreview(){if(this.busy())return;this.previewDialog?.nativeElement.close();this.preview.set(null);if(this.previewUrl)URL.revokeObjectURL(this.previewUrl);this.previewUrl='';this.fileInfo=null;this.inspectorOpen=false;this.inspectorVisited=false;this.origin?.focus();}
 downloadPreview(){const blob=this.preview();if(blob)downloadBlob(blob,this.previewName);else if(this.fileInfo)void this.file(this.fileInfo,true);}
 details(){if(!this.fileInfo?.storedFileId)return;this.inspectorOpen=true;this.inspectorVisited=true;}
 get selectedYear(){return Number(this.route.snapshot.queryParamMap.get('academicYearId'))||undefined;}
 inspectorDenied(){this.closePreview();this.showError(this.t('لم تعد لديك صلاحية عرض هذا الملف.','You no longer have access to this file.'));}
 inspectorChanged(){this.closePreview();this.notice.set(this.t('تم تحديث سجل الملف.','The file record has been updated.'));void this.load();}
 openCreate(){if(!this.data()?.writableFolderId||this.busy())return;this.origin=document.activeElement as HTMLElement;this.folderName='';this.createError='';this.createDialog?.nativeElement.showModal();}
 closeCreate(){if(this.busy())return;this.createDialog?.nativeElement.close();this.origin?.focus();}
 async create(){const folder=this.data()?.writableFolderId;if(!folder||!this.folderName.trim()||this.busy())return;this.busy.set(true);this.createError='';try{await firstValueFrom(this.library.createFolder(folder,this.folderName.trim()));this.busy.set(false);this.closeCreate();this.notice.set(this.t('تم إنشاء المجلد.','Folder created.'));await this.load();}catch(e){this.createError=this.message(e);}finally{this.busy.set(false);}}
 chooseUpload(){if(this.busy()||!this.data()?.writableFolderId)return;if(this.pendingUpload){this.showError(this.t('تحقق من نتيجة الرفع السابق قبل بدء ملف جديد.','Verify the previous upload before starting a new file.'));return;}this.uploadInput?.nativeElement.click();}
 selectedUpload(event:Event){const input=event.target as HTMLInputElement;const file=input.files?.[0];input.value='';const folder=this.data()?.writableFolderId;if(!file||!folder||this.busy())return;this.pendingUpload=file;this.uploadFolder=folder;this.uploadKey=crypto.randomUUID();this.sendUpload();}
 retryUpload(){if(this.pendingUpload&&!this.busy()&&this.uploadFolder===this.data()?.writableFolderId)this.sendUpload();}
 private sendUpload(){const file=this.pendingUpload;if(!file)return;this.busy.set(true);this.uploading.set(true);this.progress.set(0);this.library.upload(false,this.uploadFolder,file,this.uploadKey).pipe(takeUntilDestroyed(this.destroy)).subscribe({next:event=>{
   if(event.type===HttpEventType.UploadProgress)this.progress.set(Math.min(99,Math.round(100*event.loaded/(event.total||file.size))));
   if(event.type===HttpEventType.Response){this.busy.set(false);this.uploading.set(false);const result=event.body?.data;if(!event.body?.isSuccess||result?.status!=='Completed'){this.showError(this.t('لم يتأكد اكتمال الرفع. أعد المحاولة للتحقق من نفس العملية.','Upload completion is not verified. Retry to check the same operation.'));return;}this.pendingUpload=null;this.library.invalidateContext();this.notice.set(this.t('تم رفع الملف وحفظه في سجل المدرسة.','File uploaded and recorded in school storage.'));void this.load();}
  },error:e=>{this.busy.set(false);this.uploading.set(false);this.showError(this.message(e));}});}
 get canRetryUpload(){return !!this.pendingUpload&&!this.busy()&&this.uploadFolder===this.data()?.writableFolderId;}
 get pendingFileName(){return this.pendingUpload?.name;}
 showError(message:string){this.error.set(message);this.origin=document.activeElement as HTMLElement;if(!this.errorDialog?.nativeElement.open)this.errorDialog?.nativeElement.showModal();}
 closeError(){this.errorDialog?.nativeElement.close();this.error.set('');this.origin?.focus();}
 async start(item:DriveItem,action:string){if(this.busy())return;this.origin=document.activeElement as HTMLElement;this.selected=item;this.action=action;this.name=item.name;this.impact=null;this.actionError='';this.destination=null;this.destinations=null;this.key=crypto.randomUUID();this.dialog?.nativeElement.showModal();
  if(action==='Delete')await this.inspect();if(action==='Move')await this.browseDestination(null);}
 async browseDestination(handle:string|null){this.busy.set(true);this.actionError='';try{const params=handle?new HttpParams().set('handle',handle):new HttpParams();this.destinations=await this.get<DrivePage>('',params);this.destination=handle?{handle,name:this.destinations.name}:null;}catch(e){this.actionError=this.message(e);}finally{this.busy.set(false);}}
 async inspect(){if(!this.selected)return;this.busy.set(true);this.actionError='';try{this.impact=await this.post<Impact>('/impact',{handle:this.selected.handle,action:this.action,name:this.name,destination:this.destination?.handle});}catch(e){this.actionError=this.message(e);}finally{this.busy.set(false);}}
 async confirm(){if(this.busy()||!this.selected)return;if(!this.impact){await this.inspect();if(!this.impact)return;}
  this.busy.set(true);try{const r=await this.post<Operation>('/mutations',{ticket:this.impact.ticket,requestKey:this.key});this.notice.set(r.status==='Completed'?this.t('تم تنفيذ الإجراء وتحديث روابط الملف.','Done. File links have been updated.'):this.t('نتحقق من نتيجة العملية. استخدم «التحقق من النتيجة» دون تكرار الحذف.','We are verifying the result. Use “Verify result” without repeating the deletion.'));this.busy.set(false);this.close();await this.load();}catch(e){this.actionError=this.message(e);}finally{this.busy.set(false);}}
 close(){if(this.busy())return;this.dialog?.nativeElement.close();this.selected=null;this.impact=null;this.origin?.focus();}
 async reconcile(op:Operation){if(this.busy())return;this.busy.set(true);try{const r=await this.post<Operation>(`/operations/${op.id}/reconcile`,{});this.notice.set(r.status==='Completed'?this.t('تم التحقق وتحديث النظام.','Verified and synchronized.'):this.t('العملية تحتاج مراجعة. الملف لم يُحذف مرة أخرى.','Review is required. The file was not deleted again.'));await this.load();}catch(e){this.showError(this.message(e));}finally{this.busy.set(false);}}
 async showDeleted(){if(this.busy())return;this.origin=document.activeElement as HTMLElement;this.deletedDialog?.nativeElement.showModal();this.busy.set(true);try{this.deleted.set(await this.get<Operation[]>('/deleted'));}catch(e){this.showError(this.message(e));}finally{this.busy.set(false);}}
 closeDeleted(){if(this.busy())return;this.deletedDialog?.nativeElement.close();this.origin?.focus();}
 async restore(op:Operation){if(this.busy())return;this.busy.set(true);try{const r=await this.post<Operation>(`/deleted/${op.id}/restore`,{});this.notice.set(r.status==='Completed'?this.t('استُعيدت الملفات؛ الشواهد المسحوبة تحتاج ربطًا ومراجعة جديدين.','Files restored. Withdrawn evidence needs linking and review again.'):this.t('نتحقق من الاستعادة؛ راجع نتيجة العملية.','Restore verification is pending. Check the operation result.'));this.deleted.set(await this.get<Operation[]>('/deleted'));await this.load();}catch(e){this.showError(this.message(e));}finally{this.busy.set(false);}}
 get actionTitle(){return this.action==='Delete'?this.t('حذف من Drive','Delete from Drive'):this.action==='Rename'?this.t('تعديل الاسم','Rename'):this.t('نقل إلى مجلد','Move to folder');}
 track(_:number,item:DriveItem){return item.handle;}
}
