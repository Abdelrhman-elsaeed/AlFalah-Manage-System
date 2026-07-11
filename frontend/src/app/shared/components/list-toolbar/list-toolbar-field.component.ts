import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';

@Component({
  selector: 'app-list-toolbar-field',
  standalone: true,
  imports: [CommonModule, TranslateModule],
  template: `
    <div class="toolbar-field" [class.toolbar-field--grow]="grow" [class.toolbar-field--actions]="actions">
      <label *ngIf="labelKey && !actions" class="toolbar-field__label">{{ labelKey | translate }}</label>
      <div class="toolbar-field__control">
        <ng-content></ng-content>
      </div>
    </div>
  `
})
export class ListToolbarFieldComponent {
  @Input() labelKey = '';
  @Input() grow = false;
  @Input() actions = false;
}