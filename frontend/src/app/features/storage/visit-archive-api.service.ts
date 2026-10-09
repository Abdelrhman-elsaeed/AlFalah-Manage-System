import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { map, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../../core/models/api-response.model';
import { SUPPRESS_FORBIDDEN_REDIRECT, SUPPRESS_ERROR_TOAST } from '../../core/http/http-context.tokens';
import { AuthService } from '../../core/services/auth.service';

export interface ArchiveVersion { versionId:number;versionNumber:number;uploadedAtUtc:string;availability:string;size:number; }
export interface ArchiveRevision { approvalRevision:number;status:string;approvalSource:string;approvedAtUtc:string;attempts:number;lastAttemptAtUtc?:string;completedAtUtc?:string;nextAttemptAtUtc?:string;errorCode?:string;isCurrent:boolean;canRetry:boolean;versions:ArchiveVersion[]; }
export interface VisitArchive { visitId:number;instructorName:string;approvalRevision:number;isApproved:boolean;canManage:boolean;revisions:ArchiveRevision[];subject?:string;visitDate?:string;category?:string; }
export interface ArchivePage { items:VisitArchive[];total:number;page:number;pageSize:number; }
export interface ArchiveOperations { workerEnabled:boolean;externalWritesEnabled:boolean;ready:boolean; }
export interface ArchiveFilter { from?:string;to?:string;teacherId?:string;status?:string;page:number;pageSize:number; }
@Injectable({providedIn:'root'})
export class VisitArchiveApiService {
  private readonly http=inject(HttpClient);
  private readonly auth=inject(AuthService);
  private readonly base=environment.apiUrl+'/api/v1/storage/visits';
  private readonly pageCache=new Map<string,ArchivePage>();
  private pageKey(filter:ArchiveFilter){const user=this.auth.currentUser();return `${user?.userId || ''}:${user?.activeSchoolId || ''}:${this.auth.getAccessToken() || ''}:${JSON.stringify(filter)}`;}
  peekList(filter:ArchiveFilter){return this.pageCache.get(this.pageKey(filter));}
  forgetList(filter:ArchiveFilter){this.pageCache.delete(this.pageKey(filter));}
  private context() {return new HttpContext().set(SUPPRESS_FORBIDDEN_REDIRECT,true).set(SUPPRESS_ERROR_TOAST,true);}
  private data<T>(response:ApiResponse<T>):T {if(!response.isSuccess || response.data==null)throw new Error(response.message || 'Archive unavailable');return response.data;}
  operationsStatus() {return this.http.get<ApiResponse<ArchiveOperations>>(this.base+'/operations-status',{context:this.context()}).pipe(map(r=>this.data(r)));}
  list(filter:ArchiveFilter) {let params=new HttpParams();for(const [key,value] of Object.entries(filter))if(value!=null && value!=='')params=params.set(key,String(value));return this.http.get<ApiResponse<ArchivePage>>(this.base,{params,context:this.context()}).pipe(map(r=>this.data(r)),tap(page=>{this.pageCache.set(this.pageKey(filter),page);if(this.pageCache.size>8)this.pageCache.delete(this.pageCache.keys().next().value!);}));}
  teachers() {return this.http.get<ApiResponse<{userId:string;name:string}[]>>(this.base+'/teachers',{context:this.context()}).pipe(map(r=>this.data(r)));}
  get(id:number) {return this.http.get<ApiResponse<VisitArchive>>(`${this.base}/${id}/archive`,{context:this.context()}).pipe(map(r=>this.data(r)));}
  retry(id:number,approvalRevision:number,recreateMissing=false,reason?:string) {return this.http.post<ApiResponse<VisitArchive>>(`${this.base}/${id}/archive/retry`,{approvalRevision,recreateMissing,reason},{context:this.context()}).pipe(map(r=>this.data(r)));}
  content(id:number,revision:number,versionId?:number) {return this.http.get(`${this.base}/${id}/archive/${revision}/content`,{params:versionId?{versionId}:{},responseType:'blob',context:this.context()});}
}
