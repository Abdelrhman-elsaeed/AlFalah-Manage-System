import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../../core/models/api-response.model';
import { SUPPRESS_FORBIDDEN_REDIRECT, SUPPRESS_ERROR_TOAST } from '../../core/http/http-context.tokens';
import { StoragePage } from './storage.models';
import { EvaluationFilter, EvaluationTemplate, ReadinessSummary, EvaluationRequirement, ManualEvaluation, EvaluationMember, EvaluationVersion, DigitalIndexFile } from './readiness.models';

@Injectable({providedIn:'root'})
export class ReadinessApiService {
  private readonly http=inject(HttpClient); private readonly base=`${environment.apiUrl}/api/v1/storage`;
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
