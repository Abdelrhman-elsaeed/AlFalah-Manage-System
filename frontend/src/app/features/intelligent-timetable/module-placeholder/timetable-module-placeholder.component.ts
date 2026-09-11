import { Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';

@Component({
  selector: 'app-timetable-module-placeholder',
  standalone: true,
  imports: [RouterLink, ButtonModule],
  template: `
    <main class="placeholder" dir="rtl">
      <span class="icon"><i [class]="iconClass"></i></span>
      <p>الجدول الذكي</p>
      <h1>{{ title }}</h1>
      <div class="badge">ضمن المرحلة {{ phase }}</div>
      <p class="description">{{ description }}</p>
      <a pButton routerLink="/intelligent-timetable/settings" icon="pi pi-cog" label="العودة إلى إعدادات الجدول"></a>
    </main>
  `,
  styles: [`
    :host { display: block; min-height: 100%; padding: clamp(1rem, 3vw, 3rem); background: #f8fafc; }
    .placeholder { max-width: 760px; margin: 7vh auto 0; padding: clamp(2rem, 6vw, 5rem); border: 1px solid #dbe4ea; border-radius: 2rem; background: #fff; text-align: center; box-shadow: 0 20px 55px rgba(15, 23, 42, .08); }
    .icon { display: grid; width: 4.5rem; height: 4.5rem; margin: auto; place-items: center; border-radius: 1.5rem; background: #d1fae5; color: #047857; font-size: 2rem; }
    p { color: #64748b; }
    h1 { margin: .35rem 0 1rem; color: #0f172a; font-size: clamp(1.8rem, 4vw, 2.8rem); }
    .badge { display: inline-block; padding: .35rem .8rem; border-radius: 999px; background: #ecfdf5; color: #047857; font-size: .8rem; font-weight: 800; }
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
