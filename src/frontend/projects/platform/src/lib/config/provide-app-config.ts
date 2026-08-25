import { EnvironmentProviders, Provider, inject, makeEnvironmentProviders } from '@angular/core';
import { APP_CONFIG } from './app-config';
import { AppConfigStore } from './app-config.store';

/**
 * Exposes the validated runtime configuration through `APP_CONFIG`.
 *
 * The value is populated by the bootstrap initializer in `providePlatform()`; this
 * factory only reads it, and throws if something injects it too early.
 */
export function provideAppConfig(): EnvironmentProviders {
  const providers: Provider[] = [
    {
      provide: APP_CONFIG,
      useFactory: () => inject(AppConfigStore).require(),
    },
  ];
  return makeEnvironmentProviders(providers);
}
