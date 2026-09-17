import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { TagModule } from 'primeng/tag';
import { SwapCandidate } from '../../../core/models/timetable-substitution.models';
import { TimetableDay } from '../../../core/models/timetable.models';

@Component({
  selector: 'app-swap-review-dialog', standalone: true,
  imports: [CommonModule, FormsModule, ButtonModule, DialogModule, InputTextareaModule, TagModule],
  template: `<p-dialog [visible]="visible" (visibleChange)="!$event && back.emit()" [modal]="true" [closable]="!busy" [closeOnEscape]="false" [draggable]="false" [style]="{width:'900px',maxWidth:'96vw'}" header="مراجعة التبديل" dir="rtl">
    <div *ngIf="proposal as item" class="review">
      <p-tag [severity]="severity[item.color]" [icon]="icon[item.color]" [value]="label[item.color]"></p-tag>
      <p class="ready" *ngIf="item.color === 'Green'">لا توجد مشاكل — جاهز للتبديل.</p>
      <ul class="issues" *ngIf="item.errors.length || item.warnings.length"><li *ngFor="let issue of item.errors">{{issue}}</li><li *ngFor="let issue of item.warnings">{{issue}}</li></ul>
      <div class="alternatives" *ngIf="item.color === 'Red' && alternatives.length"><strong>يتوفر حل ثلاثي آمن:</strong><button *ngFor="let alternative of alternatives" type="button" (click)="selectProposal.emit(alternative.id)">{{alternative.label}}</button></div>
      <p *ngIf="item.kind === 'ThreeWaySwap'">ستُنفذ الدورة كاملة كعملية واحدة؛ الأرقام التالية تعرض كل حركة صراحة.</p>
      <div class="preview-grid"><article *ngFor="let preview of item.preview; let index = index"><span class="step" *ngIf="item.kind === 'ThreeWaySwap'">{{index + 1}}</span><h3>{{preview.classroom}} · {{preview.subject}}</h3>
        <div class="before-after"><div><b>قبل</b><span>{{preview.teacherName}}</span><small>{{dayLabel(preview.fromDay)}} · الحصة {{preview.fromPeriod}} · {{preview.fromTime}}</small></div><i class="pi pi-arrow-left"></i><div><b>بعد</b><span>{{preview.toTeacherName}}</span><small>{{dayLabel(preview.toDay)}} · الحصة {{preview.toPeriod}} · {{preview.toTime}}</small></div></div></article></div>
      <label class="reason" *ngIf="item.color === 'Yellow'"><span>سبب التجاوز (إلزامي ومسجل)</span><textarea pInputTextarea [(ngModel)]="reason" maxlength="1000" rows="3" [disabled]="busy"></textarea><small>{{reason.trim().length}} / 1000</small></label>
      <p class="error" role="alert" *ngIf="expired">انتهت صلاحية الاقتراح. أعد فحص البدائل قبل التأكيد.</p><p class="error" role="alert" *ngIf="error">{{error}}</p>
      <div class="actions"><button pButton type="button" class="p-button-outlined" label="العودة للجدول" [disabled]="busy" (click)="back.emit()"></button><button *ngIf="stale || expired" pButton type="button" class="p-button-outlined" icon="pi pi-refresh" label="إعادة فحص البدائل" [disabled]="busy" (click)="reanalyze.emit()"></button><button *ngIf="item.color !== 'Red' && (item.color !== 'Yellow' || canOverride)" pButton type="button" icon="pi pi-check" label="تأكيد التبديل" [loading]="busy" [disabled]="busy || expired || item.color === 'Yellow' && !reason.trim()" (click)="confirm.emit(reason.trim() || null)"></button></div>
      <p class="permission" *ngIf="item.color === 'Yellow' && !canOverride">يتطلب التنفيذ مستخدمًا مخولًا بالتجاوز؛ يمكنك مراجعة التحذيرات فقط.</p>
    </div>
  </p-dialog>`,
  styles: [`.review{display:grid;gap:1rem}.ready{margin:0;color:#17683e}.issues{margin:0;padding-inline-start:1.2rem;color:#7c3b21;line-height:1.7}.preview-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(230px,1fr));gap:.7rem}.preview-grid article{position:relative;padding:.8rem;border:1px solid #dce7e0;border-radius:12px}.preview-grid h3{margin:0 0 .7rem;font-size:.84rem;color:#174b34}.step{position:absolute;top:.5rem;left:.5rem;width:24px;height:24px;display:grid;place-items:center;border-radius:50%;background:#0f7132;color:#fff;font-weight:900}.before-after{display:grid;grid-template-columns:1fr auto 1fr;align-items:center;gap:.55rem}.before-after div{display:grid;gap:.2rem}.before-after span{font-size:.76rem}.before-after small{color:#687b72;font-size:.68rem}.alternatives{display:grid;gap:.45rem;padding:.75rem;border:1px solid #ecd58e;border-radius:10px;background:#fff9e7}.alternatives button{text-align:right;border:0;background:transparent;color:#12623d;font:inherit;font-size:.78rem;font-weight:800;cursor:pointer}.reason{display:grid;gap:.35rem}.reason small{text-align:left;color:#6b7d75}.actions{display:flex;justify-content:flex-end;gap:.5rem;flex-wrap:wrap}.error{margin:0;color:#a32d38}.permission{margin:0;color:#76570a}`]
})
export class SwapReviewDialogComponent implements OnChanges {
  @Input() visible = false;
  @Input() proposal: SwapCandidate | null = null;
  @Input() alternatives: SwapCandidate[] = [];
  @Input() canOverride = false;
  @Input() expired = false;
  @Input() busy = false;
  @Input() stale = false;
  @Input() error = '';
  @Output() back = new EventEmitter<void>();
  @Output() confirm = new EventEmitter<string | null>();
  @Output() reanalyze = new EventEmitter<void>();
  @Output() selectProposal = new EventEmitter<string>();
  reason = '';
  readonly label = { Green: 'متاح', Yellow: 'متاح بتنبيه', Red: 'غير متاح' } as const;
  readonly severity = { Green: 'success', Yellow: 'warning', Red: 'danger' } as const;
  readonly icon = { Green: 'pi pi-check-circle', Yellow: 'pi pi-exclamation-triangle', Red: 'pi pi-ban' } as const;
  dayLabel(day: TimetableDay): string {
    return ({ 1: 'السبت', 2: 'الأحد', 3: 'الاثنين', 4: 'الثلاثاء', 5: 'الأربعاء', 6: 'الخميس', 7: 'الجمعة' })[day];
  }
  ngOnChanges(changes: SimpleChanges): void {
    if (changes['proposal'] && changes['proposal'].previousValue?.id !== changes['proposal'].currentValue?.id) this.reason = '';
  }
}
