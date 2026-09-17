import { Component, EventEmitter, Output } from '@angular/core';
import { ButtonModule } from 'primeng/button';

@Component({
  selector: 'app-swap-analysis-overlay',
  standalone: true,
  imports: [ButtonModule],
  template: `
    <div class="overlay" role="status" aria-live="polite" aria-label="جارٍ تحليل بدائل التبديل">
      <div class="mini-grid" aria-hidden="true"><i></i><i></i><i class="source"></i><i></i><i></i><i></i><i></i><i></i><i></i></div>
      <h3>نبحث عن أفضل تبديل آمن…</h3>
      <p>فحص توافر المعلمين · مراجعة تعارضات الفصول والقاعات · التحقق من الحصص المزدوجة</p>
      <button pButton type="button" class="p-button-outlined" icon="pi pi-times" label="إلغاء البحث" (click)="cancel.emit()"></button>
    </div>`,
  styles: [`
    .overlay{position:absolute;inset:39px 0 0;z-index:30;display:grid;place-content:center;justify-items:center;gap:.75rem;padding:1.5rem;text-align:center;background:rgb(248 252 249 / 94%);backdrop-filter:blur(3px)}h3{margin:0;color:#0b5b31}p{max-width:620px;margin:0;color:#64786f;font-size:.82rem;line-height:1.7}.mini-grid{position:relative;display:grid;grid-template-columns:repeat(3,38px);gap:6px;padding:8px;overflow:hidden;border:1px solid #bad7c5;border-radius:12px;background:#fff}.mini-grid::after{position:absolute;inset:-20% -60%;content:'';background:linear-gradient(100deg,transparent 38%,rgb(202 164 42 / 30%),transparent 62%);animation:scan 1.5s infinite}.mini-grid i{height:26px;border-radius:5px;background:#e3f1e8}.mini-grid .source{background:#147147;box-shadow:0 0 0 2px #a8d3bd}@keyframes scan{to{transform:translateX(-45%)}}@media(prefers-reduced-motion:reduce){.mini-grid::after{animation:none;inset:0;background:rgb(202 164 42 / 8%)}}
  `]
})
export class SwapAnalysisOverlayComponent {
  @Output() cancel = new EventEmitter<void>();
}
