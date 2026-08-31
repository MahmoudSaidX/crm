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
import { forbiddenInterceptor } from './auth/forbidden.interceptor';
import { AUDIT_TRANSLATIONS } from './audit/audit-translations';
import { AGENT_TRANSLATIONS } from './i18n/agent-translations';
import { ROLE_TRANSLATIONS } from './roles/role-translations';
import { STAFF_USER_TRANSLATIONS } from './staff-users/staff-user-translations';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes, withComponentInputBinding()),
    // Runtime configuration + HTTP + locale/direction foundation.
    providePlatform({
      appSurface: 'agent-crm',
      httpInterceptors: [authInterceptor, forbiddenInterceptor],
    }),
    provideTranslations(COMMON_TRANSLATIONS),
    provideTranslations(AGENT_TRANSLATIONS),
    provideTranslations(ROLE_TRANSLATIONS),
    provideTranslations(STAFF_USER_TRANSLATIONS),
    provideTranslations(AUDIT_TRANSLATIONS),
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
