import { ApplicationConfig, importProvidersFrom, APP_INITIALIZER } from '@angular/core';
import { provideRouter } from '@angular/router';
import {
  provideHttpClient,
  withInterceptorsFromDi,
  HTTP_INTERCEPTORS,
  HttpClient
} from '@angular/common/http';
import { TranslateModule, TranslateLoader, TranslateService } from '@ngx-translate/core';
import { TranslateHttpLoader } from '@ngx-translate/http-loader';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { firstValueFrom } from 'rxjs';

import { routes } from './app.routes';
import { AuthInterceptor } from './core/interceptors/auth.interceptor';
import { ErrorInterceptor } from './core/interceptors/error.interceptor';

export function HttpLoaderFactory(http: HttpClient): TranslateHttpLoader {
  return new TranslateHttpLoader(http, './assets/i18n/', '.json');
}

function initTranslations(translate: TranslateService) {
  return (): Promise<void> => {
    translate.addLangs(['ar', 'en']);
    translate.setDefaultLang('ar');
    // Await the ar.json fetch so | translate never shows raw keys on first render.
    // The .catch() ensures a network failure never blocks bootstrap.
    return firstValueFrom(translate.use('ar')).catch(() => {});
  };
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideHttpClient(withInterceptorsFromDi()),
    // Order matters: AuthInterceptor runs first on the request side
    // (attaches the Bearer token), and on the response side it gets the
    // 401 LAST — so its refresh-retry runs before ErrorInterceptor sees
    // the 401. If refresh fails, ErrorInterceptor handles the bounce.
    {
      provide: HTTP_INTERCEPTORS,
      useClass: AuthInterceptor,
      multi: true
    },
    {
      provide: HTTP_INTERCEPTORS,
      useClass: ErrorInterceptor,
      multi: true
    },
    // PrimeNG message service (toasts)
    MessageService,
    importProvidersFrom(BrowserAnimationsModule),
    // ngx-translate v15 (Angular 17 compatible): TranslateModule.forRoot + HttpLoader
    importProvidersFrom(
      TranslateModule.forRoot({
        defaultLanguage: 'ar',
        loader: {
          provide: TranslateLoader,
          useFactory: HttpLoaderFactory,
          deps: [HttpClient]
        }
      })
    ),
    // Pre-load translations before bootstrap so | translate never returns raw keys.
    // The factory is declared outside providers so TranslateService is available via DI.
    {
      provide: APP_INITIALIZER,
      useFactory: initTranslations,
      deps: [TranslateService],
      multi: true
    }
  ]
};