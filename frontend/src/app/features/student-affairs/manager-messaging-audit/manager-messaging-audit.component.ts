import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { finalize } from 'rxjs';
import { MessagingAuditThreadDto } from '../../../core/models/phase5.models';
import { Phase5Service } from '../../../core/services/phase5.service';

@Component({
  selector: 'app-manager-messaging-audit', standalone: true, imports: [CommonModule, ButtonModule],
  template: `<main dir="rtl" class="audit-page"><header><div><span>بيانات وصفية فقط</span><h1>تدقيق المراسلات</h1><p>لا تعرض هذه الشاشة عنوان المحادثة أو محتواها أو هوية المشاركين.</p></div><button pButton type="button" class="p-button-outlined" icon="pi pi-refresh" label="تحديث" [loading]="loading()" (click)="load()"></button></header>
  <p class="state" role="status" aria-live="polite" *ngIf="loading()">جارٍ تحميل سجل التدقيق…</p><p class="state error" role="alert" *ngIf="errorMessage()">{{ errorMessage() }}</p>
  <section class="table-wrap" *ngIf="!loading() && !errorMessage()"><table><thead><tr><th>المعرّف</th><th>نوع المسار</th><th>الحالة</th><th>أدوار المشاركين</th><th>آخر نشاط</th><th>الرسائل</th><th>قيد الإرسال</th><th>تم التسليم</th><th>فشل</th></tr></thead><tbody><tr *ngFor="let row of rows()"><td>#{{ row.threadId }}</td><td>{{ typeLabel(row.threadType) }}</td><td>{{ row.status === 'Open' ? 'مفتوح' : row.status === 'Closed' ? 'مغلق' : 'مؤرشف' }}</td><td>{{ row.participantRoles.join('، ') }}</td><td>{{ row.lastActivityAt | date:'medium':'':'ar-EG' }}</td><td>{{ row.messageCount }}</td><td>{{ row.pendingDeliveryCount }}</td><td>{{ row.deliveredCount }}</td><td>{{ row.failedCount }}</td></tr><tr *ngIf="rows().length === 0"><td colspan="9">لا توجد سجلات مراسلات.</td></tr></tbody></table></section>
  <nav aria-label="صفحات سجل المراسلات"><button type="button" [disabled]="page() === 1 || loading()" (click)="previous()">السابق</button><span>صفحة {{ page() }} من {{ pages() }}</span><button type="button" [disabled]="page() >= pages() || loading()" (click)="next()">التالي</button></nav></main>`,
  styles: [`.audit-page{display:grid;gap:1rem;padding:1.25rem}header{display:flex;justify-content:space-between;gap:1rem;align-items:center}h1{margin:.25rem 0}.state,.table-wrap{background:var(--surface-card,#fff);border:1px solid var(--surface-border,#ddd);border-radius:14px;padding:1rem}.table-wrap{overflow:auto}table{width:100%;border-collapse:collapse;min-width:850px}th,td{text-align:right;padding:.75rem;border-bottom:1px solid var(--surface-border,#ddd)}nav{display:flex;justify-content:center;align-items:center;gap:1rem}nav button{padding:.65rem 1rem}.error{border-color:#b91c1c}@media(max-width:600px){header{align-items:stretch;flex-direction:column}}`]
})
export class ManagerMessagingAuditComponent {
  private readonly api = inject(Phase5Service); readonly rows = signal<readonly MessagingAuditThreadDto[]>([]); readonly loading = signal(true); readonly errorMessage = signal(''); readonly page = signal(1); readonly total = signal(0); readonly pageSize = 20;
  constructor(){ this.load(); } pages(): number { return Math.max(1, Math.ceil(this.total()/this.pageSize)); }
  load(): void { this.loading.set(true); this.errorMessage.set(''); this.api.getMessagingAudit({pageNumber:this.page(),pageSize:this.pageSize,sortDirection:'desc'}).pipe(finalize(()=>this.loading.set(false))).subscribe({next:r=>{if(r.isSuccess&&r.data){this.rows.set(r.data.items);this.total.set(r.data.totalCount);}else this.errorMessage.set(r.errors[0]??r.message);},error:(e:HttpErrorResponse)=>this.errorMessage.set(e.status===403?'لا تملك صلاحية تدقيق المراسلات.':'تعذر تحميل سجل التدقيق.')}); }
  previous():void{if(this.page()>1){this.page.update(x=>x-1);this.load();}} next():void{if(this.page()<this.pages()){this.page.update(x=>x+1);this.load();}}
  typeLabel(type: MessagingAuditThreadDto['threadType']):string{return ({GuardianTeacher:'ولي أمر ومعلم',GuardianStudentAffairs:'ولي أمر وشؤون الطلاب',GuardianSocialWorker:'ولي أمر وأخصائي اجتماعي'})[type];}
}
