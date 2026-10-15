import { CommonModule } from '@angular/common';
import { Component, DestroyRef, ElementRef, ViewChild, inject, signal } from '@angular/core';
import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
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

interface DriveItem {handle:string;name:string;isFolder:boolean;size:number|null;mimeType:string;modifiedAt:string|null;canModify:boolean;protection:string|null;}
interface Crumb {handle:string;name:string;}
interface DrivePage {name:string;handle:string|null;breadcrumbs:Crumb[];items:DriveItem[];nextPageToken:string|null;canManage:boolean;}
interface Impact {ticket:string;name:string;action:string;files:number;folders:number;linkedTo:string[];linkedCount:number;signedDocuments:number;warning:string;}
interface Operation {id:number;status:string;action:string;name:string;errorCode:string|null;}
@Component({selector:'app-storage-drive-browser',standalone:true,imports:[CommonModule,FormsModule,DutyPdfPreviewComponent],
 templateUrl:'./storage-drive-browser.component.html',styleUrls:['./storage-drive-browser.component.css']})
export class StorageDriveBrowserComponent {
 private readonly http=inject(HttpClient);private readonly router=inject(Router);private readonly route=inject(ActivatedRoute);
 private readonly translate=inject(TranslateService);private readonly destroy=inject(DestroyRef);
 private readonly base=`${environment.apiUrl}/api/v1/storage/drive`;
 private readonly context=new HttpContext().set(SUPPRESS_ERROR_TOAST,true).set(SUPPRESS_FORBIDDEN_REDIRECT,true);
 @ViewChild('actionDialog') dialog?:ElementRef<HTMLDialogElement>;
 @ViewChild('previewDialog') previewDialog?:ElementRef<HTMLDialogElement>;
 @ViewChild('errorDialog') errorDialog?:ElementRef<HTMLDialogElement>;
 @ViewChild('deletedDialog') deletedDialog?:ElementRef<HTMLDialogElement>;
 readonly data=signal<DrivePage|null>(null);readonly loading=signal(false);readonly busy=signal(false);readonly operations=signal<Operation[]>([]);
 readonly error=signal('');readonly notice=signal('');readonly preview=signal<Blob|null>(null);
 readonly deleted=signal<Operation[]>([]);
 search='';selected:DriveItem|null=null;action='Delete';name='';impact:Impact|null=null;actionError='';previewName='';
 destinations:DrivePage|null=null;destination:Crumb|null=null;private origin:HTMLElement|null=null;private request=0;private key='';
 get en(){return this.translate.currentLang==='en';}
 get rows(){const q=this.search.trim().toLocaleLowerCase();return this.data()?.items.filter(x=>!q||x.name.toLocaleLowerCase().includes(q))??[];}
 t(ar:string,en:string){return this.en?en:ar;}
 constructor(){this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroy)).subscribe(()=>{this.search='';void this.load();});}
 private async get<T>(path:string,params=new HttpParams()):Promise<T>{const r=await firstValueFrom(this.http.get<ApiResponse<T>>(this.base+path,{params,context:this.context}));if(!r.isSuccess||r.data==null)throw new Error(r.message||this.t('تعذّر إتمام الطلب.','The request could not be completed.'));return r.data;}
 private async post<T>(path:string,body:unknown):Promise<T>{const r=await firstValueFrom(this.http.post<ApiResponse<T>>(this.base+path,body,{context:this.context}));if(!r.isSuccess||r.data==null)throw new Error(r.message||this.t('تعذّر إتمام الطلب.','The request could not be completed.'));return r.data;}
 private message(e:unknown){return extractHttpErrorMessage(e)||(e instanceof Error?e.message:this.t('تعذّر الاتصال. أعد المحاولة.','Connection unavailable. Try again.'));}
 async load(more=false){const sequence=++this.request;this.loading.set(true);this.error.set('');
  let params=new HttpParams();const handle=this.route.snapshot.queryParamMap.get('drive');if(handle)params=params.set('handle',handle);if(more&&this.data()?.nextPageToken)params=params.set('pageToken',this.data()!.nextPageToken!);
  try{const page=await this.get<DrivePage>('',params);if(sequence!==this.request)return;this.data.set(more?{...page,items:[...(this.data()?.items??[]),...page.items]}:page);
   if(page.canManage)this.operations.set(await this.get<Operation[]>('/operations'));else this.operations.set([]);
  }catch(e){if(sequence!==this.request)return;if(!more)this.data.set(null);this.error.set(this.message(e));}finally{if(sequence===this.request)this.loading.set(false);}}
 open(item:DriveItem){if(this.busy())return;if(item.isFolder)this.go(item.handle);else void this.file(item,false);}
 go(handle:string|null){void this.router.navigate([],{relativeTo:this.route,queryParams:{drive:handle,display:null},queryParamsHandling:'merge'});}
 size(value:number|null){if(value==null)return '—';return value<1024?value+' B':value<1048576?(value/1024).toFixed(1)+' KB':(value/1048576).toFixed(1)+' MB';}
 async file(item:DriveItem,download:boolean){if(this.busy())return;this.busy.set(true);this.error.set('');try{
   const blob=await firstValueFrom(this.http.get(this.base+'/content',{params:{handle:item.handle},responseType:'blob',context:this.context}));
   if(!download&&item.mimeType==='application/pdf'){this.previewName=item.name;this.preview.set(blob);this.origin=document.activeElement as HTMLElement;this.previewDialog?.nativeElement.showModal();}
   else{const url=URL.createObjectURL(blob);const a=document.createElement('a');a.href=url;a.download=item.name;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);}
  }catch(e){this.showError(this.message(e));}finally{this.busy.set(false);}}
 closePreview(){this.previewDialog?.nativeElement.close();this.preview.set(null);this.origin?.focus();}
 downloadPreview(){const blob=this.preview();if(!blob)return;const url=URL.createObjectURL(blob);const a=document.createElement('a');a.href=url;a.download=this.previewName;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);}
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
