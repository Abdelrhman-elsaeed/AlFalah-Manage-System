import { Component, OnInit } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ToastModule } from 'primeng/toast';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, ToastModule],
  template: `
    <!-- D-33: top-right in RTL keeps toasts away from the page-header
         action button (which sits on the visual-start / left in RTL). -->
    <p-toast position="top-right"></p-toast>
    <router-outlet></router-outlet>
  `
})
export class AppComponent implements OnInit {
  ngOnInit(): void {
    // RTL + Arabic lang attribute on the HTML element.
    // Translation loading is handled by APP_INITIALIZER in app.config.ts
    // (translate.use('ar') is awaited there before bootstrap).
    document.documentElement.setAttribute('dir', 'rtl');
    document.documentElement.setAttribute('lang', 'ar-SA');
  }
}
