import { Component } from '@angular/core';

@Component({
  selector: 'app-swap-grid-legend', standalone: true,
  template: `<div class="legend" aria-label="دليل حالات التبديل"><span class="source">الحصة المختارة</span><span class="green">متاح</span><span class="yellow">بتنبيه</span><span class="red">غير متاح</span></div>`,
  styles: [`.legend{display:flex;flex-wrap:wrap;gap:.45rem;padding:.45rem .7rem;border-bottom:1px solid #e0e9e3;background:#fff;font-size:.68rem;font-weight:800}.legend span{display:flex;align-items:center;gap:.3rem}.legend span::before{width:10px;height:10px;border-radius:3px;content:''}.source::before{background:#16708a}.green::before{background:#2f9b59}.yellow::before{background:repeating-linear-gradient(135deg,#d6a918 0 3px,#fff1bd 3px 6px)}.red::before{background:#c94652}`]
})
export class SwapGridLegendComponent {}
