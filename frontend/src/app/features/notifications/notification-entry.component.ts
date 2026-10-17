import { Component, OnInit, inject, signal } from '@angular/core';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { NotificationCenterService } from '../../core/services/notification-center.service';

@Component({ selector: 'app-notification-entry', standalone: true, imports: [RouterLink],
  template: `<section dir="rtl" class="entry-result" role="status"><i class="pi pi-bell" aria-hidden="true"></i><h1>{{ loading() ? 'جارٍ فتح الإشعار…' : 'هذا الإشعار غير متاح حاليًا' }}</h1>@if (!loading()) { <p>قد تكون حالة العنصر أو صلاحيات الوصول قد تغيّرت. تحقق من الحساب والمدرسة النشطة.</p><a routerLink="/notifications">العودة إلى الإشعارات</a> }</section>`,
  styles: [`.entry-result{text-align:center;max-width:480px;margin:12vh auto;padding:30px;line-height:1.9;color:#345a49}.entry-result>i{font-size:2rem;color:#145c43}.entry-result h1{font-size:1.3rem}.entry-result a{color:#145c43;text-decoration:underline}`]
})
export class NotificationEntryComponent implements OnInit {
  private readonly route = inject(ActivatedRoute); private readonly router = inject(Router); private readonly center = inject(NotificationCenterService);
  readonly loading = signal(true);
  async ngOnInit(): Promise<void> {
    const id = this.route.snapshot.queryParamMap.get('target') ?? '';
    if (!/^[0-9a-f-]{36}$/i.test(id)) { this.loading.set(false); return; }
    sessionStorage.setItem('alfalah_notification_target', id);
    try {
      const target = await this.center.target(id);
      if (target.available && target.route && /^\/(student-affairs\/messages|complaints|visits|school-duty\/(execution|documents)|notifications\/(detail|sheet)\/[0-9a-f-]{36})$/i.test(target.route)) {
        sessionStorage.removeItem('alfalah_notification_target');
        await this.router.navigate([target.route], { queryParams: target.query, replaceUrl: true });
      }
    } catch { /* Keep the neutral unavailable state; never follow a stale or unverified target. */ }
    finally { this.loading.set(false); }
  }
}
