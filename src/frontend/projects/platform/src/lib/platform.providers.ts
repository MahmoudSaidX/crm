import {
  EnvironmentProviders,
  inject,
  makeEnvironmentProviders,
  provideAppInitializer,
} from '@angular/core';
import { HttpInterceptorFn } from '@angular/common/http';
import { AppSurface } from './config/app-config';
import { AppConfigStore } from './config/app-config.store';
import { provideAppConfig } from './config/provide-app-config';
import { RUNTIME_CONFIG_URL, loadRuntimeConfig } from './config/runtime-config-loader';
import { AppConfigError } from './config/validate-app-config';
import { provideHttpPlatform } from './http/provide-http-platform';
import { LocaleService } from './i18n/locale.service';

export interface PlatformOptions {
  /** The surface the bundle was built for; cross-checked against the runtime file. */
  readonly appSurface: AppSurface;
  /** Override the runtime configuration location. Defaults to `config.json`. */
  readonly configUrl?: string;
  /** Application-specific interceptors appended after the shared API URL interceptor. */
  readonly httpInterceptors?: readonly HttpInterceptorFn[];
}

/**
 * The Squad CRM frontend bootstrap: runtime configuration, HTTP and locale/direction.
 *
 * A single initializer loads the configuration and then applies the locale, so the
 * document direction is correct before the first render — ordering that separate
 * initializers could not guarantee, because Angular runs them concurrently.
 */
export function providePlatform(options: PlatformOptions): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideAppConfig(),
    provideHttpPlatform(options.httpInterceptors),
    options.configUrl ? [{ provide: RUNTIME_CONFIG_URL, useValue: options.configUrl }] : [],
    provideAppInitializer(async () => {
      const store = inject(AppConfigStore);
      const locale = inject(LocaleService);

      const config = await loadRuntimeConfig(inject(RUNTIME_CONFIG_URL));
      if (config.appSurface !== options.appSurface) {
        throw new AppConfigError(
          `\`appSurface\` is "${config.appSurface}" but this bundle is "${options.appSurface}".`,
        );
      }

      store.set(config);
      locale.initialize();
    }),
  ]);
}
