import { InjectionToken } from '@angular/core';
import { AppConfig } from './app-config';
import { AppConfigError, validateAppConfig } from './validate-app-config';

/**
 * Location of the static runtime configuration file, relative to the deployed
 * application root. Each surface owns its own file because Agent CRM and
 * Customer Portal are deployed independently; only the contract is shared.
 */
export const RUNTIME_CONFIG_URL = new InjectionToken<string>('RUNTIME_CONFIG_URL', {
  providedIn: 'root',
  factory: () => 'config.json',
});

export async function loadRuntimeConfig(url: string): Promise<AppConfig> {
  let response: Response;
  try {
    response = await fetch(url, { cache: 'no-cache' });
  } catch (cause) {
    throw new AppConfigError(`could not fetch "${url}" (${String(cause)}).`);
  }
  if (!response.ok) {
    throw new AppConfigError(`fetching "${url}" returned HTTP ${response.status}.`);
  }
  return validateAppConfig(await response.json());
}
