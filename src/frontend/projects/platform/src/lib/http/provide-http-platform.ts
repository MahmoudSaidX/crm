import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { EnvironmentProviders } from '@angular/core';
import { apiBaseUrlInterceptor } from './api-base-url.interceptor';

/**
 * The single canonical HTTP bootstrap for Squad CRM frontends.
 *
 * Applications call this instead of `provideHttpClient(...)` directly, so that every
 * surface gets the same interceptor chain.
 */
export function provideHttpPlatform(): EnvironmentProviders {
  return provideHttpClient(withInterceptors([apiBaseUrlInterceptor]));
}
