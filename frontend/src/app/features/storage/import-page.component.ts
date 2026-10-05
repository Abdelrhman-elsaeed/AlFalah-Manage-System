import { CommonModule } from '@angular/common';
import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { HttpClient, HttpContext } from '@angular/common/http';
import { forkJoin, Observable } from 'rxjs';
import { DropdownModule } from 'primeng/dropdown';
import { PaginatorModule } from 'primeng/paginator';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../../core/models/api-response.model';
import { SUPPRESS_ERROR_TOAST, SUPPRESS_FORBIDDEN_REDIRECT } from '../../core/http/http-context.tokens';
import { ImportApiService, ImportBatch, ImportRow } from './import-api.service';

@Component({selector: 'app-import-page', standalone: true, imports: [CommonModule, FormsModule, RouterLink, DropdownModule, PaginatorModule],
  templateUrl: './import-page.component.html', styleUrls: ['./import-page.component.css']})
export class ImportPageComponent implements OnInit {
  private readonly api = inject(ImportApiService); private readonly http = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef); private readonly route = inject(ActivatedRoute); private readonly router = inject(Router);
  batch?: ImportBatch; batches: ImportBatch[] = []; rows: ImportRow[] = []; total = 0; page = 1;
  years: {id: number; nameAr: string}[] = []; templates: {version: number; name: string}[] = [];
  members: {userId: string; name: string}[] = []; requirements: {id: number; displayName: string}[] = [];
  year?: number; version = 1; sourceVersion = '1'; file?: File; reason = ''; acknowledged = false; busy = false; error = ''; disabled = false; filter = '';
  filters = [{label:'كل الصفوف', value:''}, {label:'مطابق', value:'Matched'}, {label:'جديد', value:'New'}, {label:'ناقص', value:'Missing'}, {label:'متعارض', value:'Conflict'}];
  ngOnInit() {
    this.run(this.api.list(), batches => { this.batches = batches; const id = Number(this.route.snapshot.queryParamMap.get('batch')); if (id) this.open(id); });
    const base = `${environment.apiUrl}/api/v1/storage`; const context = new HttpContext().set(SUPPRESS_ERROR_TOAST, true).set(SUPPRESS_FORBIDDEN_REDIRECT, true);
    forkJoin({years: this.http.get<ApiResponse<typeof this.years>>(`${base}/academic-years`, {context}),
      templates: this.http.get<ApiResponse<typeof this.templates>>(`${base}/templates`, {context}), members: this.http.get<ApiResponse<typeof this.members>>(`${base}/evaluation-members`, {context})})
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({next: r => {this.years=r.years.data || []; this.templates=r.templates.data || []; this.members=r.members.data || [];}, error: e => this.fail(e)});
  }
  choose(event: Event) { const input = event.target as HTMLInputElement; const file = input.files?.[0]; input.value=''; if (!file) return;
    if (file.size > 16777216 || !/\.(json|csv)$/i.test(file.name)) { this.error='اختر JSON أو CSV بحد أقصى 16 MiB.'; return; } this.file=file;
  }
  preview() { if (this.file && this.year && this.sourceVersion.trim()) this.run(this.api.preview(this.file, this.year, this.version, this.sourceVersion), b => {this.batches=[b,...this.batches.filter(x=>x.id!==b.id)]; this.accept(b);}); }
  open(id: number) { this.run(this.api.get(id), b => this.accept(b)); }
  private accept(batch: ImportBatch) { this.batch=batch; this.year=batch.academicYearId; this.version=batch.templateVersion; this.acknowledged=false; this.page=1;
    this.router.navigate([], {relativeTo: this.route, replaceUrl:true, queryParams:{batch:batch.id}}); this.loadRows(); this.loadRequirements(); }
  private loadRequirements() { if (!this.year) return; const context=new HttpContext().set(SUPPRESS_FORBIDDEN_REDIRECT,true).set(SUPPRESS_ERROR_TOAST,true);
    this.http.get<ApiResponse<typeof this.requirements>>(`${environment.apiUrl}/api/v1/storage/requirement-catalog`, {params:{academicYearId:this.year},context}).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({next:r=>this.requirements=r.data||[],error:e=>this.fail(e)}); }
  loadRows() { if(this.batch) this.run(this.api.rows(this.batch.id,this.page,this.filter),r=>{this.rows=r.items;this.total=r.total;}); }
  resolve(row: ImportRow) { if(this.batch && this.reason.trim()) this.run(this.api.resolve(this.batch,row,this.reason),b=>this.accept(b)); }
  decide(commit: boolean) { if(this.batch && this.acknowledged && this.reason.trim()) this.run(this.api.decide(this.batch,commit,this.reason),b=>this.accept(b)); }
  upload(row: ImportRow, event: Event) { const input=event.target as HTMLInputElement;const file=input.files?.[0];input.value='';if(!file || !this.batch)return;
    if(!this.reason.trim()) {this.error='اكتب سبب مطابقة الأصل قبل الرفع.';return;}
    this.run(this.api.bytes(this.batch,row,file,this.reason),()=>this.open(this.batch!.id)); }
  reconcile(row: ImportRow) {if(this.batch)this.run(this.api.reconcile(this.batch,row),()=>this.open(this.batch!.id));}
  export() {if(this.batch)this.run(this.api.export(this.batch.id),blob=>{const url=URL.createObjectURL(blob);const a=document.createElement('a');a.href=url;a.download=`import-${this.batch!.id}-exceptions.csv`;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);});}
  label(value: string) {return ({Matched:'مطابق',Missing:'ناقص',Conflict:'متعارض',New:'جديد',ReferenceOnly:'مرجع فقط',NeedsReview:'يحتاج مراجعة',Imported:'أصل مرفوع — مراجعة الشاهد مستقلة'} as Record<string,string>)[value] || value;}
  private run<T>(call:Observable<T>, done:(value:T)=>void) {this.busy=true;this.error='';call.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({next:r=>{this.busy=false;done(r);},error:e=>{this.busy=false;this.fail(e);}});}
  private fail(e:any) {this.disabled=e.status===404;this.error=e.error?.message||e.message||'تعذر إتمام الاستيراد.';if(e.status===403 || e.status===401){this.batch=undefined;this.rows=[];this.batches=[];this.members=[];this.requirements=[];this.file=undefined;}}
}
