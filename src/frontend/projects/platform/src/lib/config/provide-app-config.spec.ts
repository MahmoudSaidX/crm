import { TestBed } from '@angular/core/testing';
import { APP_CONFIG } from './app-config';
import { AppConfigStore } from './app-config.store';
import { provideAppConfig } from './provide-app-config';
import { validateAppConfig } from './validate-app-config';

describe('provideAppConfig', () => {
  it('exposes the loaded configuration through APP_CONFIG', () => {
    TestBed.configureTestingModule({ providers: [provideAppConfig()] });
    TestBed.inject(AppConfigStore).set(
      validateAppConfig({
        apiBaseUrl: 'https://api.example.test',
        defaultLocale: 'ar',
        supportedLocales: ['en', 'ar'],
        appSurface: 'customer-portal',
      }),
    );

    expect(TestBed.inject(APP_CONFIG).appSurface).toBe('customer-portal');
  });

  it('throws when APP_CONFIG is read before configuration has loaded', () => {
    TestBed.configureTestingModule({ providers: [provideAppConfig()] });

    expect(() => TestBed.inject(APP_CONFIG)).toThrow();
  });
});
