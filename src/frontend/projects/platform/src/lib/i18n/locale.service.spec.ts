import { TestBed } from '@angular/core/testing';
import { AppConfigStore } from '../config/app-config.store';
import { validateAppConfig } from '../config/validate-app-config';
import { LOCALE_STORAGE_KEY, LocaleService } from './locale.service';

function configure(supportedLocales: string[] = ['en', 'ar'], defaultLocale = 'en'): LocaleService {
  TestBed.configureTestingModule({});
  TestBed.inject(AppConfigStore).set(
    validateAppConfig({
      apiBaseUrl: 'https://api.example.test',
      defaultLocale,
      supportedLocales,
      appSurface: 'agent-crm',
    }),
  );
  return TestBed.inject(LocaleService);
}

describe('LocaleService', () => {
  beforeEach(() => localStorage.removeItem(LOCALE_STORAGE_KEY));
  afterEach(() => {
    localStorage.removeItem(LOCALE_STORAGE_KEY);
    document.documentElement.setAttribute('lang', 'en');
    document.documentElement.setAttribute('dir', 'ltr');
  });

  it('derives rtl from ar and applies it to the document', () => {
    const service = configure();

    service.setLocale('ar');

    expect(service.direction()).toBe('rtl');
    expect(document.documentElement.getAttribute('dir')).toBe('rtl');
    expect(document.documentElement.getAttribute('lang')).toBe('ar');
  });

  it('derives ltr from en and restores it on the document', () => {
    const service = configure();

    service.setLocale('ar');
    service.setLocale('en');

    expect(service.direction()).toBe('ltr');
    expect(document.documentElement.getAttribute('dir')).toBe('ltr');
    expect(document.documentElement.getAttribute('lang')).toBe('en');
  });

  it('persists the selected locale', () => {
    configure().setLocale('ar');

    expect(localStorage.getItem(LOCALE_STORAGE_KEY)).toBe('ar');
  });

  it('rejects a locale this deployment does not support', () => {
    const service = configure(['en']);

    expect(() => service.setLocale('ar')).toThrowError(/not supported/);
  });

  describe('initialize()', () => {
    it('restores a supported persisted locale', () => {
      localStorage.setItem(LOCALE_STORAGE_KEY, 'ar');

      const service = configure();
      service.initialize();

      expect(service.locale()).toBe('ar');
      expect(document.documentElement.getAttribute('dir')).toBe('rtl');
    });

    it('falls back to the runtime default when nothing is persisted', () => {
      const service = configure(['en', 'ar'], 'ar');
      service.initialize();

      expect(service.locale()).toBe('ar');
    });

    it('falls back to the runtime default when the persisted value is unsupported', () => {
      localStorage.setItem(LOCALE_STORAGE_KEY, 'fr');

      const service = configure(['en', 'ar'], 'en');
      service.initialize();

      expect(service.locale()).toBe('en');
      expect(document.documentElement.getAttribute('dir')).toBe('ltr');
    });

    it('falls back to the runtime default when the persisted locale is no longer deployed', () => {
      localStorage.setItem(LOCALE_STORAGE_KEY, 'ar');

      const service = configure(['en'], 'en');
      service.initialize();

      expect(service.locale()).toBe('en');
    });
  });
});
