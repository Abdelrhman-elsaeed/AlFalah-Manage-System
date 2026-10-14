import { Directive, EventEmitter, Input, OnChanges, OnDestroy, Output, inject } from '@angular/core';
import { DutyFeedbackService } from './duty-feedback.service';

@Directive({ selector: '[dutyError]', standalone: true })
export class DutyErrorDirective implements OnChanges, OnDestroy {
  @Input('dutyError') message = '';
  @Output() dutyErrorDismissed = new EventEmitter<void>();
  private readonly feedback = inject(DutyFeedbackService);
  ngOnChanges() { if (this.message) this.feedback.show(this.message, () => this.dutyErrorDismissed.emit(), this); }
  ngOnDestroy() { this.feedback.forget(this); }
}
