import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-list-toolbar',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section class="list-toolbar" [class.list-toolbar--compact]="compact" dir="rtl">
      <ng-content></ng-content>
    </section>
  `
})
export class ListToolbarComponent {
  @Input() compact = false;
}