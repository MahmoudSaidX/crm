import { InjectionToken } from '@angular/core';
import { SupportedLocale } from '../i18n/locale';

/** Application surfaces hosted by the Squad CRM Angular workspace. */
export type AppSurface = 'agent-crm' | 'customer-portal';

/**
 * Validated runtime configuration.
 *
 * Loaded from a static file that is deployed *next to* the built artifact, so the
 * same production bundle can be promoted Dev -> Test -> UAT -> Prod by swapping the
 * file only. It is publicly readable by design — never put secrets here.
 */
export interface AppConfig {
  readonly apiBaseUrl: string;
  readonly defaultLocale: SupportedLocale;
  readonly supportedLocales: readonly SupportedLocale[];
  readonly appSurface: AppSurface;
}

/** Inject the validated runtime configuration. Available after bootstrap. */
export const APP_CONFIG = new InjectionToken<AppConfig>('APP_CONFIG');
