import {
  ApplicationConfig,
  provideBrowserGlobalErrorListeners,
  provideZoneChangeDetection,
} from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { providePlatform } from '@squad-crm/platform';
import { providePrimeNgPlatform } from '@squad-crm/shared-ui';

import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes, withComponentInputBinding()),
    // Runtime configuration + HTTP + locale/direction foundation.
    providePlatform({ appSurface: 'agent-crm' }),
    providePrimeNgPlatform(),
  ],
};
