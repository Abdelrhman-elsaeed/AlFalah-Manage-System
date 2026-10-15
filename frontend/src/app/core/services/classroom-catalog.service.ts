import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { EMPTY, expand, map, reduce } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { SUPPRESS_ERROR_TOAST } from '../http/http-context.tokens';
export interface CatalogYear { id:number; name:string; isActive:boolean; }
export interface CatalogContext { stage:'Primary'|'Intermediate'|'Secondary'; isManager:boolean; isMember:boolean; years:CatalogYear[]; }
export interface ClassroomOption { id:number; label:string; stage:string; gradeLevel:number; section:string; sectionNumber:number|null; academicYearId:number; isActive:boolean; physicalLocation:string; revision:string; }
export interface ClassroomCatalog { context:CatalogContext; items:ClassroomOption[]; hasMore:boolean; }
@Injectable({providedIn:'root'})
export class ClassroomCatalogService {
 private readonly http=inject(HttpClient);private readonly base=environment.apiUrl+'/api/v1/classroom-catalog';
 private readonly context=new HttpContext().set(SUPPRESS_ERROR_TOAST,true);
 page(year?:number,page=1,includeInactive=true){return this.http.get<ApiResponse<ClassroomCatalog>>(this.base,{params:{...(year?{academicYearId:year}:{}),page,includeInactive},context:this.context}).pipe(map(r=>{if(!r.isSuccess||!r.data)throw new Error(r.message||'تعذر تحميل الفصول.');return r.data;}));}
 all(year?:number){return this.page(year).pipe(expand((r,index)=>r.hasMore?this.page(year,index+2):EMPTY),reduce((all,r)=>({...r,items:[...(all?.items??[]),...r.items]}),null as ClassroomCatalog|null),map(r=>r!));}
 create(year:number,grade:number,sections:string[],physicalLocation:string){return this.http.post<ApiResponse<ClassroomOption[]>>(this.base+'/batch',{academicYearId:year,gradeLevel:grade,sections,physicalLocation},{context:this.context});}
 edit(row:ClassroomOption,section:string,isActive:boolean,physicalLocation:string){return this.http.patch<ApiResponse<ClassroomOption>>(this.base+'/'+row.id,{section,isActive,physicalLocation,revision:row.revision},{context:this.context});}
 gradeLabel(stage:string,grade:number){const names=['الأول','الثاني','الثالث','الرابع','الخامس','السادس'];return (names[grade-1]||grade)+' '+(({Primary:'الابتدائي',Intermediate:'المتوسط',Secondary:'الثانوي'} as Record<string,string>)[stage]||'');}
}
