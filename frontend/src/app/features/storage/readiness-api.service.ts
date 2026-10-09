import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../../core/models/api-response.model';
import { SUPPRESS_FORBIDDEN_REDIRECT, SUPPRESS_ERROR_TOAST } from '../../core/http/http-context.tokens';
import { StoragePage } from './storage.models';
import { AuthService } from '../../core/services/auth.service';
import { StorageContext } from './storage.models';
import { EvaluationFilter, EvaluationTemplate, ReadinessSummary, EvaluationRequirement, ManualEvaluation, EvaluationMember, EvaluationVersion, DigitalIndexFile } from './readiness.models';

export interface ReadinessViewSnapshot {
  filter: EvaluationFilter;
  template: EvaluationTemplate;
  summary: ReadinessSummary;
  rows: StoragePage<EvaluationRequirement>;
  critical: StoragePage<EvaluationRequirement>;
  manuals: ManualEvaluation[];
  files: StoragePage<DigitalIndexFile>;
}
export interface ReadinessBootstrap {
  context: StorageContext;
  years: {id:number;nameAr:string}[];
  members: EvaluationMember[];
  versions: EvaluationVersion[];
}

@Injectable({providedIn:'root'})
export class ReadinessApiService {
  private readonly http=inject(HttpClient); private readonly base=`${environment.apiUrl}/api/v1/storage`;
  private readonly auth=inject(AuthService);
  private readonly viewCache=new Map<string,ReadinessViewSnapshot>();
  private readonly bootstrapCache=new Map<string,ReadinessBootstrap>();
  private readonly summaryCache=new Map<string,ReadinessSummary>();
  private userScope() { const user=this.auth.currentUser();return `${user?.userId || ''}:${user?.activeSchoolId || ''}`; }
  private scope(mode:string) { const user=this.auth.currentUser();return `${user?.userId || ''}:${user?.activeSchoolId || ''}:${mode}`; }
  private viewKey(mode:string,filter:EvaluationFilter) { return `${this.scope(mode)}:${JSON.stringify(filter)}`; }
  private summaryKey(filter:EvaluationFilter) {
    return `${this.userScope()}:${JSON.stringify({year:filter.academicYearId,version:filter.templateVersion,domain:filter.domainCode,
      standard:filter.standardCode,responsible:filter.responsibleUserId,importance:filter.importance,status:filter.status,
      search:filter.search,critical:filter.criticalOnly,hideCompleted:filter.hideCompleted})}`;
  }
  peekSummary(filter:EvaluationFilter) { return this.summaryCache.get(this.summaryKey(filter)); }
  rememberSummary(filter:EvaluationFilter,summary:ReadinessSummary) { this.summaryCache.set(this.summaryKey(filter),summary); }
  peekView(mode:string,filter:EvaluationFilter) { return this.viewCache.get(this.viewKey(mode,filter)); }
  peekLastView(mode:string,year?:number) { const prefix=this.scope(mode)+':';return Array.from(this.viewCache.entries()).reverse().find(([key,value])=>key.startsWith(prefix) && (!year || value.filter.academicYearId===year))?.[1]; }
  peekSharedView(filter:EvaluationFilter) {
    const prefix=this.userScope()+':';
    return Array.from(this.viewCache.entries()).reverse().find(([key,value])=>key.startsWith(prefix) &&
      value.filter.academicYearId===filter.academicYearId && value.filter.templateVersion===filter.templateVersion &&
      ['domainCode','standardCode','responsibleUserId','importance','status','search','criticalOnly','hideCompleted']
        .every(field=>String(value.filter[field as keyof EvaluationFilter] ?? '')===String(filter[field as keyof EvaluationFilter] ?? '')))?.[1];
  }
  peekBootstrap() { return this.bootstrapCache.get(this.userScope()); }
  rememberBootstrap(value:ReadinessBootstrap) { this.bootstrapCache.set(this.userScope(),value); }
  rememberView(mode:string,snapshot:ReadinessViewSnapshot) { this.viewCache.set(this.viewKey(mode,snapshot.filter),snapshot); if(this.viewCache.size>12) this.viewCache.delete(this.viewCache.keys().next().value!); }
  forgetViews() { this.viewCache.clear(); this.bootstrapCache.clear(); this.summaryCache.clear(); }
  private context() { return new HttpContext().set(SUPPRESS_FORBIDDEN_REDIRECT,true).set(SUPPRESS_ERROR_TOAST,true); }
  private data<T>(r:ApiResponse<T>) { if(!r.isSuccess || r.data==null) throw new Error(r.message);return r.data; }
  private params(filter:EvaluationFilter) { let params=new HttpParams();for(const [key,value] of Object.entries(filter)) if(value!=null && value!=='') params=params.set(key,value);return params; }
  private get<T>(path:string, params:HttpParams=new HttpParams()) {return this.http.get<ApiResponse<T>>(`${this.base}/${path}`,{params,context:this.context()}).pipe(map(r=>this.data(r)));}
  template(version:number) {return this.get<EvaluationTemplate>(`templates/${version}`);}
  versions() {return this.get<EvaluationVersion[]>('templates');}
  members() {return this.get<EvaluationMember[]>('evaluation-members');}
  readiness(filter:EvaluationFilter) {return this.get<ReadinessSummary>('readiness',this.params(filter));}
  requirements(filter:EvaluationFilter, gaps=false) {return this.get<StoragePage<EvaluationRequirement>>(gaps?'gaps':'requirements',this.params(filter));}
  index(filter:EvaluationFilter) {return this.get<StoragePage<DigitalIndexFile>>('digital-index',this.params(filter));}
  manuals(year:number,version:number) {return this.get<ManualEvaluation[]>('manual-evaluations',new HttpParams().set('academicYearId',year).set('templateVersion',version));}
  manualHistory(id:number) {return this.get<{revision:number; snapshotJson:string; createdAtUtc:string}[]>(`manual-evaluations/${id}/history`);}
  followUpHistory(id:number) {return this.get<{actorName:string;reason:string;oldValuesJson:string;newValuesJson:string;createdAtUtc:string}[]>(`requirements/${id}/follow-up-history`);}
  initialize(year:number,version:number) {return this.http.post<ApiResponse<unknown>>(`${this.base}/self-evaluation/initialize`,{academicYearId:year,templateVersion:version},{context:this.context()}).pipe(map(r=>this.data(r)));}
  configure(id:number,body:unknown) {return this.http.patch<ApiResponse<EvaluationRequirement>>(`${this.base}/requirements/${id}/follow-up`,body,{context:this.context()}).pipe(map(r=>this.data(r)));}
  saveManual(body:unknown) {return this.http.post<ApiResponse<ManualEvaluation>>(`${this.base}/manual-evaluations`,body,{context:this.context()}).pipe(map(r=>this.data(r)));}
  export(format:string,filter:EvaluationFilter) {return this.http.get(`${this.base}/exports/${format}`,{params:this.params(filter),responseType:'blob',context:this.context()});}
}
