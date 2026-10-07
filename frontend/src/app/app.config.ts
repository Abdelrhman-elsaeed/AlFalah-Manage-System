import { APP_INITIALIZER, ApplicationConfig, Injectable, inject, importProvidersFrom } from '@angular/core';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { PreloadingStrategy, Route, provideRouter, withPreloading } from '@angular/router';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { TranslateHttpLoader } from '@ngx-translate/http-loader';
import { TranslateLoader, TranslateModule, TranslateService } from '@ngx-translate/core';
import { MessageService } from 'primeng/api';
import { Observable, firstValueFrom, of } from 'rxjs';
import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { AuthService } from './core/services/auth.service';

export function HttpLoaderFactory(http: HttpClient): TranslateHttpLoader {
  return new TranslateHttpLoader(http, './assets/i18n/', '.json');
}

function initTranslations(translate: TranslateService) {
  return (): Promise<void> => {
    translate.addLangs(['ar', 'en']);
    translate.setDefaultLang('ar');
    return firstValueFrom(translate.use('ar')).then(
      () => undefined,
      (error: unknown) => console.error('[i18n] Arabic translations failed to load.', error)
    );
  };
}

function initAuthSession(auth: AuthService) {
  return (): Promise<void> => firstValueFrom(auth.bootstrapSession());
}

@Injectable({ providedIn: 'root' })
export class StoragePreloadingStrategy implements PreloadingStrategy {
  private readonly auth = inject(AuthService);
  preload(route: Route, load: () => Observable<unknown>): Observable<unknown> {
    return this.auth.isAuthenticated() && (route.path?.startsWith('school-manager/storage') || route.path === 'my-files')
      ? load() : of(null);
  }
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes, withPreloading(StoragePreloadingStrategy)),
    provideHttpClient(withInterceptors([authInterceptor])),
    MessageService,
    importProvidersFrom(BrowserAnimationsModule),
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
    {
      provide: APP_INITIALIZER,
      useFactory: initTranslations,
      deps: [TranslateService],
      multi: true
    },
    {
      provide: APP_INITIALIZER,
      useFactory: initAuthSession,
      deps: [AuthService],
      multi: true
    }
  ]
};
