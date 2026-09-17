import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { SwapSearchScope } from '../../../core/models/timetable-substitution.models';
import { InlineSwapSource } from './inline-swap-session.store';

@Component({
  selector: 'app-swap-mode-bar', standalone: true, imports: [CommonModule, ButtonModule],
  template: `<div class="bar" *ngIf="source">
    <div class="summary"><span>أنت تبدّل</span><strong>{{source.entry.subject}} · {{source.entry.classLabel}}</strong><small>{{source.teacherName}} · {{source.dayLabel}} · {{source.periodLabel}}</small></div>
    <span class="scope">{{scope === 'SameDay' ? 'داخل اليوم نفسه' : 'كل أيام الجدول'}}</span>
    <div class="counts"><b class="green">{{green}} متاح</b><b class="yellow">{{yellow}} بتنبيه</b><b class="red">{{red}} غير متاح</b></div>
    <span class="expiry" *ngIf="expiresAt">صالحة حتى {{expiresAt | date:'HH:mm'}}</span>
    <button pButton type="button" class="p-button-outlined" icon="pi pi-times" label="إلغاء التبديل" (click)="cancel.emit()"></button>
  </div>`,
  styles: [`.bar{position:sticky;top:0;z-index:20;display:flex;align-items:center;gap:.8rem;min-height:64px;padding:.55rem .7rem;border-bottom:1px solid #c5d9cd;background:#f4faf6;box-shadow:0 4px 12px rgb(18 68 43 / 8%)}.summary{display:grid;gap:.08rem;min-width:210px}.summary span,.summary small,.expiry{color:#64786f;font-size:.68rem}.summary strong{color:#0a4f2b;font-size:.78rem}.scope{padding:.3rem .55rem;border-radius:999px;background:#dcefe4;color:#145c39;font-size:.7rem;font-weight:800}.counts{display:flex;gap:.35rem;flex-wrap:wrap}.counts b{padding:.27rem .45rem;border-radius:7px;font-size:.67rem}.green{background:#def3e5;color:#17683e}.yellow{background:#fff0c0;color:#76570a}.red{background:#fde3e5;color:#9d2935}.expiry{margin-inline-start:auto;white-space:nowrap}@media(max-width:780px){.bar{align-items:flex-start;flex-wrap:wrap}.summary{width:100%}.expiry{margin-inline-start:0}}
  `]
})
export class SwapModeBarComponent {
  @Input({ required: true }) source: InlineSwapSource | null = null;
  @Input() scope: SwapSearchScope = 'SameDay';
  @Input() green = 0; @Input() yellow = 0; @Input() red = 0;
  @Input() expiresAt: string | null = null;
  @Output() cancel = new EventEmitter<void>();
}
