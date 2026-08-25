import { isSupportedLocale, SupportedLocale } from '../i18n/locale';
import { AppConfig, AppSurface } from './app-config';

/** Placeholder values a deployment pipeline is expected to substitute. */
export const CONFIG_SENTINELS: readonly string[] = ['REPLACE_ME', '__REPLACE_ME__', 'CHANGEME'];

const APP_SURFACES: readonly AppSurface[] = ['agent-crm', 'customer-portal'];

export class AppConfigError extends Error {
  constructor(message: string) {
    super(`[squad-crm] Invalid runtime configuration: ${message}`);
    this.name = 'AppConfigError';
  }
}

/**
 * Validates raw runtime configuration and fails startup loudly when a deployment
 * forgot to substitute a value. A frontend pointed at `REPLACE_ME` must not boot.
 */
export function validateAppConfig(raw: unknown): AppConfig {
  if (raw === null || typeof raw !== 'object') {
    throw new AppConfigError('expected a JSON object.');
  }

  const candidate = raw as Record<string, unknown>;
  const apiBaseUrl = candidate['apiBaseUrl'];

  if (typeof apiBaseUrl !== 'string' || apiBaseUrl.trim().length === 0) {
    throw new AppConfigError('`apiBaseUrl` is required and must be a non-empty string.');
  }
  if (CONFIG_SENTINELS.includes(apiBaseUrl.trim())) {
    throw new AppConfigError(
      `\`apiBaseUrl\` is still the placeholder "${apiBaseUrl}". Substitute it from AGENT_CRM_API_BASE_URL / CUSTOMER_PORTAL_API_BASE_URL.`,
    );
  }

  const supportedLocalesRaw = candidate['supportedLocales'];
  if (!Array.isArray(supportedLocalesRaw) || supportedLocalesRaw.length === 0) {
    throw new AppConfigError('`supportedLocales` must be a non-empty array.');
  }
  const supportedLocales: SupportedLocale[] = [];
  for (const locale of supportedLocalesRaw) {
    if (!isSupportedLocale(locale)) {
      throw new AppConfigError(`\`supportedLocales\` contains unsupported locale "${locale}".`);
    }
    supportedLocales.push(locale);
  }

  const defaultLocale = candidate['defaultLocale'];
  if (!isSupportedLocale(defaultLocale)) {
    throw new AppConfigError(`\`defaultLocale\` "${defaultLocale}" is not a supported locale.`);
  }
  if (!supportedLocales.includes(defaultLocale)) {
    throw new AppConfigError('`defaultLocale` must be listed in `supportedLocales`.');
  }

  const appSurface = candidate['appSurface'];
  if (typeof appSurface !== 'string' || !APP_SURFACES.includes(appSurface as AppSurface)) {
    throw new AppConfigError(
      `\`appSurface\` "${appSurface}" must be one of ${APP_SURFACES.join(', ')}.`,
    );
  }

  return {
    apiBaseUrl: apiBaseUrl.trim().replace(/\/+$/, ''),
    defaultLocale,
    supportedLocales,
    appSurface: appSurface as AppSurface,
  };
}
