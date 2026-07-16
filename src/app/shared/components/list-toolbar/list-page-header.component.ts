import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';

@Component({
  selector: 'app-list-page-header',
  standalone: true,
  imports: [CommonModule, TranslateModule],
  template: `
    <header class="list-page-header" dir="rtl">
      <div class="list-page-header__titles">
        <h1 class="list-page-header__title">{{ titleKey | translate }}</h1>
        <p class="list-page-header__subtitle">{{ subtitleKey | translate }}</p>
      </div>
      <div class="list-page-header__action">
        <ng-content select="[slot=action]"></ng-content>
      </div>
    </header>
  `
})
export class ListPageHeaderComponent {
  @Input() titleKey = '';
  @Input() subtitleKey = '';
}