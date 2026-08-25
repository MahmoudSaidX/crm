/**
 * Locale + direction primitives.
 *
 * Deliberately framework- and presentation-free: no Angular, no PrimeNG, no
 * application imports. They live here rather than in a separate library because
 * `LocaleService` is their only consumer today (see `src/frontend/README.md`).
 */

/** Locales the Squad CRM foundation supports. Content itself is CRM-116. */
export const SUPPORTED_LOCALES = ['en', 'ar'] as const;

export type SupportedLocale = (typeof SUPPORTED_LOCALES)[number];

export type Direction = 'ltr' | 'rtl';

const RTL_LOCALES: ReadonlySet<string> = new Set<string>(['ar']);

export function isSupportedLocale(value: unknown): value is SupportedLocale {
  return typeof value === 'string' && (SUPPORTED_LOCALES as readonly string[]).includes(value);
}

export function directionForLocale(locale: SupportedLocale): Direction {
  return RTL_LOCALES.has(locale) ? 'rtl' : 'ltr';
}
