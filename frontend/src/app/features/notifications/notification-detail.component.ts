import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, effect, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../../core/models/api-response.model';
import { CenterTarget, NotificationCenterService } from '../../core/services/notification-center.service';
import { AuthService } from '../../core/services/auth.service';

interface Detail { title: string; preview: string; state: string; sourceType: string; sourceId: number; rows: {label: string; value: string}[]; action?: CenterTarget; }
@Component({ selector: 'app-notification-detail', standalone: true, imports: [CommonModule, RouterLink],
  template: `<section dir="rtl" class="notification-detail"><a routerLink="/notifications" class="back"><i class="pi pi-angle-right" aria-hidden="true"></i> سجل الإشعارات</a>@if (detail(); as item) { <header><span class="detail-icon"><i class="pi pi-file" aria-hidden="true"></i></span><div><h1>{{ item.title }}</h1><p>{{ item.preview }}</p></div></header><div class="source-line"><span>رقم السجل {{ item.sourceId }}</span><span>{{ state(item.state) }}</span></div><dl>@for (row of item.rows; track $index) { <div><dt>{{ row.label }}</dt><dd dir="auto">{{ row.value }}</dd></div> }</dl><footer><button type="button" [disabled]="reading()" (click)="read()">{{ readDone() ? 'تم تحديده كمقروء' : 'تحديد كمقروء' }}</button>@if (item.action?.route) { <a [routerLink]="item.action!.route" [queryParams]="item.action!.query">فتح العنصر في مساحة العمل <i class="pi pi-angle-left" aria-hidden="true"></i></a> }</footer><p class="decision-note">تتم الموافقة أو الإقرار أو التنفيذ من مساحة العمل بصلاحياتها الحالية.</p> } @else { <p role="status">{{ error() || 'جارٍ تحميل التفاصيل…' }}</p> }</section>`,
  styles: [`.notification-detail{max-width:800px;margin:20px auto;padding:28px;background:white;border:1px solid #d7e7de;border-radius:22px;color:#183c32;line-height:1.9}.back{display:inline-flex;gap:6px;align-items:center;color:#62776e;font-size:.8rem;text-decoration:none;margin-bottom:28px}header{display:flex;gap:16px;align-items:flex-start}h1{font-size:1.4rem;margin:0 0 8px}header p{font-size:.9rem;color:#526e60;margin:0;white-space:pre-wrap;overflow-wrap:anywhere}.detail-icon{display:grid;place-items:center;min-width:48px;height:48px;background:#eaf5ef;color:#145c43;border-radius:15px;font-size:1.3rem}.source-line{display:flex;justify-content:space-between;margin:22px 0;padding:12px 0;border-block:1px solid #e6eee8;color:#62776e;font-size:.8rem}dl>div{display:grid;grid-template-columns:minmax(90px,1fr) 2fr;gap:12px;padding:13px 0;border-bottom:1px solid #edf2ee}dt{font-weight:700;font-size:.85rem}dd{margin:0;white-space:pre-wrap;font-size:.85rem;overflow-wrap:anywhere}footer{display:flex;justify-content:space-between;gap:12px;flex-wrap:wrap;margin-top:26px}footer button,footer a{border:1px solid #c4dbcd;border-radius:10px;background:white;padding:10px 16px;color:#145c43;font:inherit;font-size:.8rem;text-decoration:none;cursor:pointer}footer a{background:#145c43;color:white}.decision-note{font-size:.75rem;color:#62776e;margin-top:20px}a:focus-visible,button:focus-visible{outline:3px solid #b78231;outline-offset:3px}@media(max-width:600px){.notification-detail{padding:20px 16px;margin:0;border-radius:16px}h1{font-size:1.15rem}dl>div{grid-template-columns:1fr}header{gap:10px}}`]
})
export class NotificationDetailComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly http = inject(HttpClient); private readonly route = inject(ActivatedRoute); private readonly center = inject(NotificationCenterService);
  readonly detail = signal<Detail | null>(null); readonly error = signal(''); readonly reading = signal(false); readonly readDone = signal(false);
  private id = '';
  private identity = '';
  private key(): string { const u = this.auth.currentUser(); return u ? `${u.userId}:${u.activeSchoolId}:${u.roles.join(',')}:${u.permissions.join(',')}` : ''; }
  constructor() {
    this.identity = this.key();
    effect(() => { const identity = this.key(); if (identity !== this.identity) { this.identity = identity; this.detail.set(null); this.error.set('تحقق من الحساب والمدرسة النشطة ثم افتح الإشعار مجددًا.'); } }, { allowSignalWrites: true });
  }
  async ngOnInit(): Promise<void> {
    this.id = this.route.snapshot.paramMap.get('id') ?? '';
    if (!/^[0-9a-f-]{36}$/i.test(this.id)) { this.error.set('هذا الإشعار غير متاح حاليًا.'); return; }
    try {
      const identity = this.key();
      const response = await firstValueFrom(this.http.get<ApiResponse<Detail>>(`${environment.apiUrl}/api/v1/notification-center/${this.id}/detail`));
      if (identity !== this.key()) return;
      if (response.isSuccess && response.data) this.detail.set(response.data); else this.error.set('هذا الإشعار غير متاح حاليًا.');
    } catch { this.error.set('تعذر تحميل التفاصيل. تحقق من الاتصال والصلاحيات.'); }
  }
  state(value: string): string { return ({ pending: 'بانتظار الإجراء', resolved: 'تمت المعالجة', cancelled: 'ملغى', correction: 'قيد التصحيح' } as Record<string, string>)[value] ?? 'للاطلاع'; }
  async read(): Promise<void> {
    this.reading.set(true);
    try { await this.center.read(this.id); this.readDone.set(true); }
    catch { this.detail.set(null); this.error.set('تعذر حفظ حالة القراءة. أعد فتح الإشعار للمحاولة مجددًا.'); }
    finally { this.reading.set(false); }
  }
}
