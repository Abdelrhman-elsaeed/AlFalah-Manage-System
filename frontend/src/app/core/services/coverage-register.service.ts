import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { CoverageRegisterContext, CoverageRegisterInfo, CoverageRegisterScope } from '../models/coverage-register.models';
import { SUPPRESS_ERROR_TOAST } from '../http/http-context.tokens';

@Injectable({providedIn:'root'})
export class CoverageRegisterService {
  private readonly http=inject(HttpClient);
  private readonly base=`${environment.apiUrl}/api/v1/timetable-coverage/register`;
  private readonly options={context:new HttpContext().set(SUPPRESS_ERROR_TOAST,true)};
  context(date:string,page=1,scope:CoverageRegisterScope='Day'){return this.http.get<ApiResponse<CoverageRegisterContext>>(this.base,{...this.options,params:{date,page,pageSize:10,scope}});}
  issue(date:string,sourceRevision:string,scope:CoverageRegisterScope='Day'){return this.http.post<ApiResponse<CoverageRegisterInfo>>(this.base,{date,sourceRevision,scope},this.options);}
  content(id:number){return this.http.get(`${this.base}/${id}/content`,{...this.options,responseType:'blob',observe:'response'});}
}
