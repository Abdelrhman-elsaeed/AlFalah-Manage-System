import { Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';

@Component({
  selector: 'app-timetable-module-placeholder',
  standalone: true,
  imports: [RouterLink, ButtonModule],
  template: `
    <main class="placeholder tt-workspace" dir="rtl">
      <span class="icon"><i [class]="iconClass"></i></span>
      <p>الجدول الذكي</p>
      <h1>{{ title }}</h1>
      <div class="badge">ضمن المرحلة {{ phase }}</div>
      <p class="description">{{ description }}</p>
      <a pButton routerLink="/intelligent-timetable/settings" icon="pi pi-cog" label="العودة إلى إعدادات الجدول"></a>
    </main>
  `,
  styles: [`
    :host { display: block; min-height: 100%; background: #f8f7f2; }
    .placeholder { min-height: calc(100dvh - 8rem); margin: 0; padding: clamp(2.5rem, 7vw, 6rem); border: 1px solid rgba(17, 86, 55, .15); border-top: 3px solid #c9a227; border-radius: 1rem; background: radial-gradient(circle at 12% 8%, rgba(201,162,39,.12), transparent 25%), linear-gradient(135deg, rgba(255,255,255,.98), rgba(238,248,242,.96)); text-align: center; box-shadow: 0 10px 28px rgba(17, 62, 40, .07); }
    .icon { display: grid; width: 4.5rem; height: 4.5rem; margin: auto; place-items: center; border-radius: 1.35rem; background: linear-gradient(145deg, #0f7132, #084824); color: #fff; font-size: 2rem; box-shadow: 0 10px 22px rgba(15,113,50,.18); }
    p { color: #64748b; }
    h1 { margin: .35rem 0 1rem; color: #0f172a; font-size: clamp(1.8rem, 4vw, 2.8rem); }
    .badge { display: inline-block; padding: .35rem .8rem; border-radius: 999px; background: rgba(201,162,39,.14); color: #765b0c; font-size: .8rem; font-weight: 800; }
    .description { max-width: 520px; margin: 1.25rem auto 2rem; line-height: 1.8; }
    a { text-decoration: none; }
  `]
})
export class TimetableModulePlaceholderComponent {
  private readonly data = inject(ActivatedRoute).snapshot.data;
  readonly title = this.data['moduleTitle'] as string;
  readonly description = this.data['description'] as string;
  readonly phase = this.data['phase'] as string;
  readonly iconClass = `pi ${this.data['icon'] as string}`;
}
