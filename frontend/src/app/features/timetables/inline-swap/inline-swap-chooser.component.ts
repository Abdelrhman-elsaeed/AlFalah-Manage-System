import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { SwapSearchScope } from '../../../core/models/timetable-substitution.models';
import { InlineSwapSource } from './inline-swap-session.store';

@Component({
  selector: 'app-inline-swap-chooser',
  standalone: true,
  imports: [CommonModule, ButtonModule],
  template: `
    <section class="chooser" *ngIf="source">
      <div class="source-summary">
        <i class="pi pi-sync"></i>
        <div><strong>{{ source.entry.subject }} · {{ source.entry.classLabel }}</strong>
          <span>{{ source.teacherName }} · {{ source.dayLabel }} · {{ source.periodLabel }}</span></div>
      </div>
      <div class="blocked" *ngIf="dirty">
        <i class="pi pi-save"></i><span>احفظ الجدول أولًا حتى نفحص أحدث نسخة محفوظة.</span>
        <button pButton type="button" label="حفظ الآن" icon="pi pi-save" (click)="saveRequested.emit()"></button>
      </div>
      <div class="scope-grid" [class.scope-grid--blocked]="dirty">
        <button type="button" class="scope-card" [disabled]="dirty" (click)="search.emit('SameDay')">
          <i class="pi pi-calendar-clock"></i><strong>داخل اليوم نفسه</strong>
          <span>ابحث عن تبديل آمن مع الحفاظ على الحصص المزدوجة والقيود.</span>
          <b>ابدأ البحث</b>
        </button>
        <button type="button" class="scope-card scope-card--future" [disabled]="dirty || !wholeTimetableEnabled" (click)="search.emit('WholeTimetable')">
          <i class="pi pi-calendar"></i><strong>كل أيام الجدول</strong>
          <span>ابحث في جميع أيام الأسبوع عن أفضل البدائل الممكنة.</span>
          <b>{{ wholeTimetableEnabled ? 'ابدأ البحث' : 'يُفعّل بعد اعتماد سياسة الاحتياطي' }}</b>
        </button>
      </div>
    </section>`,
  styles: [`
    .chooser{display:grid;gap:1rem}.source-summary,.blocked{display:flex;align-items:center;gap:.75rem;padding:.8rem;border-radius:12px;background:#edf7f1;color:#174c35}.source-summary i{font-size:1.35rem}.source-summary div{display:grid;gap:.2rem}.source-summary span{font-size:.78rem;color:#60756b}
    .blocked{background:#fff7df;border:1px solid #ead28a;color:#674f0d}.blocked span{flex:1}.scope-grid{display:grid;grid-template-columns:1fr 1fr;gap:.8rem}.scope-card{display:grid;gap:.45rem;min-height:170px;padding:1rem;text-align:right;border:1px solid #bcd7c7;border-radius:14px;background:#fff;color:#183d2d;cursor:pointer;transition:.15s}.scope-card:hover:not(:disabled){transform:translateY(-2px);border-color:#177443;box-shadow:0 8px 22px rgb(15 113 50 / 12%)}.scope-card i{font-size:1.6rem;color:#0f7132}.scope-card span{font-size:.78rem;line-height:1.7;color:#60756b}.scope-card b{margin-top:auto;color:#0f7132;font-size:.78rem}.scope-card--future{border-style:dashed}.scope-card:disabled{cursor:not-allowed;opacity:.65}.scope-grid--blocked{opacity:.7}@media(max-width:620px){.scope-grid{grid-template-columns:1fr}.blocked{align-items:flex-start;flex-wrap:wrap}}
  `]
})
export class InlineSwapChooserComponent {
  @Input({ required: true }) source: InlineSwapSource | null = null;
  @Input() dirty = false;
  @Input() wholeTimetableEnabled = false;
  @Output() search = new EventEmitter<SwapSearchScope>();
  @Output() saveRequested = new EventEmitter<void>();
}
