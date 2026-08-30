import { TestBed } from '@angular/core/testing';
import { AppConfigStore } from '../config/app-config.store';
import { provideAppConfig } from '../config/provide-app-config';
import { validateAppConfig } from '../config/validate-app-config';
import { LocaleService, LOCALE_STORAGE_KEY } from './locale.service';
import { provideTranslations } from './localization';
import { LocalizationService } from './localization.service';

const resources = {
  en: { greeting: 'Hello', englishOnly: 'Fallback' },
  ar: { greeting: 'مرحباً' },
} as const;

function configure(...translationProviders: ReturnType<typeof provideTranslations>[]): void {
  TestBed.configureTestingModule({
    providers: [provideAppConfig(), ...translationProviders],
  });
  TestBed.inject(AppConfigStore).set(
    validateAppConfig({
      apiBaseUrl: 'http://localhost:5080',
      defaultLocale: 'en',
      supportedLocales: ['en', 'ar'],
      appSurface: 'agent-crm',
    }),
  );
  TestBed.inject(LocaleService).initialize();
}

describe('LocalizationService', () => {
  beforeEach(() => localStorage.removeItem(LOCALE_STORAGE_KEY));

  afterEach(() => {
    localStorage.removeItem(LOCALE_STORAGE_KEY);
    document.documentElement.setAttribute('lang', 'en');
    document.documentElement.setAttribute('dir', 'ltr');
  });

  it('reads the active locale and falls back from Arabic to English', () => {
    configure(provideTranslations(resources));
    const locale = TestBed.inject(LocaleService);
    const localization = TestBed.inject(LocalizationService);

    expect(localization.translate('greeting')).toBe('Hello');
    locale.setLocale('ar');
    expect(localization.translate('greeting')).toBe('مرحباً');
    expect(localization.translate('englishOnly')).toBe('Fallback');
  });

  it('returns and reports a fully missing stable key once', () => {
    configure(provideTranslations(resources));
    const warn = spyOn(console, 'warn');
    const localization = TestBed.inject(LocalizationService);

    expect(localization.translate('missing.key')).toBe('missing.key');
    expect(localization.translate('missing.key')).toBe('missing.key');
    expect(warn).toHaveBeenCalledTimes(1);
  });

  it('rejects conflicting resource ownership', () => {
    configure(
      provideTranslations(resources),
      provideTranslations({ en: { greeting: 'Different' }, ar: { greeting: 'مختلف' } }),
    );

    expect(() => TestBed.inject(LocalizationService)).toThrowError(
      '[squad-crm] Conflicting en translation for "greeting".',
    );
  });

  it('formats dates and numbers with the active locale', () => {
    configure(provideTranslations(resources));
    const locale = TestBed.inject(LocaleService);
    const localization = TestBed.inject(LocalizationService);
    const date = new Date('2026-08-30T00:00:00Z');
    const dateOptions: Intl.DateTimeFormatOptions = { timeZone: 'UTC', dateStyle: 'medium' };

    expect(localization.formatDate(date, dateOptions)).toBe(
      new Intl.DateTimeFormat('en', dateOptions).format(date),
    );
    locale.setLocale('ar');
    expect(localization.formatNumber(1234.5)).toBe(new Intl.NumberFormat('ar').format(1234.5));
  });
});
