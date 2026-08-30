import {
  ApplicationConfig,
  ENVIRONMENT_INITIALIZER,
  effect,
  inject,
  provideBrowserGlobalErrorListeners,
  provideZoneChangeDetection,
} from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { LocaleService, providePlatform, provideTranslations } from '@squad-crm/platform';
import {
  COMMON_TRANSLATIONS,
  PrimeNgLocaleAdapter,
  providePrimeNgPlatform,
} from '@squad-crm/shared-ui';

import { routes } from './app.routes';
import { authInterceptor } from './auth/auth.interceptor';
import { AGENT_TRANSLATIONS } from './i18n/agent-translations';
import { ROLE_TRANSLATIONS } from './roles/role-translations';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes, withComponentInputBinding()),
    // Runtime configuration + HTTP + locale/direction foundation.
    providePlatform({ appSurface: 'agent-crm', httpInterceptors: [authInterceptor] }),
    provideTranslations(COMMON_TRANSLATIONS),
    provideTranslations(AGENT_TRANSLATIONS),
    provideTranslations(ROLE_TRANSLATIONS),
    providePrimeNgPlatform(),
    {
      provide: ENVIRONMENT_INITIALIZER,
      multi: true,
      useValue: () => {
        const locale = inject(LocaleService).locale;
        const primeNgLocale = inject(PrimeNgLocaleAdapter);
        effect(() => primeNgLocale.setLocale(locale()));
      },
    },
  ],
};
