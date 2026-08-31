import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import {
  AppConfigStore,
  LOCALE_STORAGE_KEY,
  LocaleService,
  provideAppConfig,
  provideTranslations,
  validateAppConfig,
} from '@squad-crm/platform';
import { providePrimeNG } from 'primeng/config';
import { Home } from './home';
import { COMMON_TRANSLATIONS } from '@squad-crm/shared-ui';
import { AGENT_TRANSLATIONS } from '../i18n/agent-translations';

describe('Agent CRM — Home smoke', () => {
  let fixture: ComponentFixture<Home>;

  beforeEach(async () => {
    localStorage.removeItem(LOCALE_STORAGE_KEY);

    await TestBed.configureTestingModule({
      imports: [Home],
      providers: [
        provideAppConfig(),
        provideHttpClient(),
        provideRouter([]),
        provideNoopAnimations(),
        providePrimeNG({}),
        provideTranslations(COMMON_TRANSLATIONS),
        provideTranslations(AGENT_TRANSLATIONS),
      ],
    }).compileComponents();

    TestBed.inject(AppConfigStore).set(
      validateAppConfig({
        apiBaseUrl: 'http://localhost:5080',
        defaultLocale: 'en',
        supportedLocales: ['en', 'ar'],
        appSurface: 'agent-crm',
      }),
    );
    TestBed.inject(LocaleService).initialize();

    fixture = TestBed.createComponent(Home);
    fixture.detectChanges();
  });

  afterEach(() => {
    localStorage.removeItem(LOCALE_STORAGE_KEY);
    document.documentElement.setAttribute('lang', 'en');
    document.documentElement.setAttribute('dir', 'ltr');
  });

  it('runs on the agent-crm surface with the default locale applied to <html>', () => {
    expect(TestBed.inject(AppConfigStore).require().appSurface).toBe('agent-crm');
    expect(document.documentElement.getAttribute('lang')).toBe('en');
    expect(document.documentElement.getAttribute('dir')).toBe('ltr');
  });

  it('switches the document direction to rtl when the locale toggles to ar', () => {
    TestBed.inject(LocaleService).setLocale('ar');
    fixture.detectChanges();

    expect(TestBed.inject(LocaleService).locale()).toBe('ar');
    expect(document.documentElement.getAttribute('dir')).toBe('rtl');
    expect(fixture.nativeElement.querySelector('h1').textContent).toContain(
      'سكواد لإدارة علاقات العملاء',
    );
  });
});
