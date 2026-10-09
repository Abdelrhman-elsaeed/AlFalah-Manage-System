import { CommonModule } from '@angular/common';
import { HttpClient, HttpContext } from '@angular/common/http';
import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { ApiResponse } from '../../core/models/api-response.model';
import { SUPPRESS_ERROR_TOAST, SUPPRESS_FORBIDDEN_REDIRECT } from '../../core/http/http-context.tokens';
import { environment } from '../../../environments/environment';
import { ImportPageComponent } from './import-page.component';
import { StorageApiService } from './storage-api.service';
import { StorageContext } from './storage.models';
import { ArchiveOperations, VisitArchiveApiService } from './visit-archive-api.service';

interface Delegation { id:number; granteeUserId:string; startsAt:string; expiresAt?:string; revokedAt?:string; reason:string; rowVersion:string; }

@Component({selector:'app-storage-admin',standalone:true,imports:[CommonModule,FormsModule,RouterLink,ImportPageComponent],templateUrl:'./storage-admin-page.component.html',styleUrls:['./storage-admin-page.component.css']})
export class StorageAdminPageComponent implements OnInit,OnDestroy {
  private readonly api=inject(StorageApiService); private readonly archive=inject(VisitArchiveApiService); private readonly http=inject(HttpClient);
  private readonly route=inject(ActivatedRoute); private readonly router=inject(Router); private readonly destroyed=new Subject<void>();
  readonly tabs=[{id:'drive',label:'Drive والجاهزية',icon:'pi-cloud'},{id:'teachers',label:'مجلدات المعلمين',icon:'pi-users'},{id:'delegations',label:'التفويض',icon:'pi-shield'},{id:'imports',label:'الاستيراد التاريخي',icon:'pi-history'}];
  readonly openedTabs=new Set<string>(['drive']);
  tab='drive'; context?:StorageContext; operations?:ArchiveOperations; operationsError=''; error=''; loading=false;
  delegations:Delegation[]=[]; delegationsError=''; delegationsLoading=false; delegationsLoaded=false; canDelegate=false; saving=false; granteeUserId=''; expiresAt=''; reason=''; revokeTarget?:Delegation; revokeReason='';
  get connectionText(){return this.context?.connectionState==='Connected'?'المكتبة متصلة بحساب المدرسة وفق صلاحية الخادم.':'تحتاج مكتبة المدرسة إلى متابعة حالة الاتصال أو التهيئة.';}
  ngOnInit(){this.route.queryParamMap.pipe(takeUntil(this.destroyed)).subscribe(params=>{const value=params.get('tab');this.tab=this.tabs.some(item=>item.id===value)?value!:'drive';this.openedTabs.add(this.tab);if(this.tab==='delegations'&&!this.delegationsLoaded)this.loadDelegations();});this.load();}
  load(refresh=false){this.error='';this.loading=!this.context;this.api.contextInfo(false,refresh,true).pipe(takeUntil(this.destroyed)).subscribe({next:context=>{this.loading=false;this.context=context;if(!context.canManage){this.router.navigateByUrl('/unauthorized');return;}this.loadOperations();},error:e=>{this.loading=false;this.error=e?.error?.message||'تعذّر تحميل إعدادات المساحة.';}});}
  loadOperations(){this.operationsError='';this.archive.operationsStatus().pipe(takeUntil(this.destroyed)).subscribe({next:value=>this.operations=value,error:()=>{this.operationsError='تعذّر قراءة حالة أرشفة الزيارات.';}});}
  private delegationUrl(){return `${environment.apiUrl}/api/v1/storage/delegations`;}
  private options(){return {context:new HttpContext().set(SUPPRESS_ERROR_TOAST,true).set(SUPPRESS_FORBIDDEN_REDIRECT,true)};}
  loadDelegations(refresh=false){if(this.delegationsLoaded&&!refresh)return;this.delegationsLoading=true;this.delegationsError='';this.http.get<ApiResponse<Delegation[]>>(this.delegationUrl(),this.options()).pipe(takeUntil(this.destroyed)).subscribe({next:response=>{this.delegationsLoading=false;this.delegationsLoaded=true;this.delegations=response.data||[];this.canDelegate=true;},error:e=>{this.delegationsLoading=false;if(e?.status===401||e?.status===403){this.delegations=[];this.canDelegate=false;this.delegationsLoaded=false;}this.delegationsError=e?.status===403?'إدارة التفويضات متاحة لمدير المدرسة المخوّل فقط.':e?.error?.message||'تعذّر قراءة التفويضات.';}});}
  grant(){if(this.saving||!this.granteeUserId.trim()||!this.reason.trim())return;this.saving=true;this.delegationsError='';const body={granteeUserId:this.granteeUserId.trim(),startsAt:new Date().toISOString(),expiresAt:this.expiresAt?new Date(this.expiresAt).toISOString():null,reason:this.reason.trim()};this.http.post<ApiResponse<Delegation>>(this.delegationUrl(),body,this.options()).pipe(takeUntil(this.destroyed)).subscribe({next:()=>{this.saving=false;this.granteeUserId='';this.expiresAt='';this.reason='';this.loadDelegations(true);},error:e=>{this.saving=false;this.delegationsError=e?.error?.message||'تعذّر منح التفويض.';}});}
  revoke(){const item=this.revokeTarget;if(!item||!this.revokeReason.trim()||this.saving)return;this.saving=true;this.delegationsError='';this.http.request<ApiResponse<Delegation>>('DELETE',`${this.delegationUrl()}/${item.id}`,{body:{reason:this.revokeReason.trim(),rowVersion:item.rowVersion},...this.options()}).pipe(takeUntil(this.destroyed)).subscribe({next:()=>{this.saving=false;this.revokeTarget=undefined;this.revokeReason='';this.loadDelegations(true);},error:e=>{this.saving=false;this.delegationsError=e?.error?.message||'تعذّر سحب التفويض.';}});}
  ngOnDestroy(){this.destroyed.next();this.destroyed.complete();}
}
