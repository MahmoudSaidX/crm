import { ComponentFixture, TestBed } from '@angular/core/testing';
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
import { PORTAL_TRANSLATIONS } from '../i18n/portal-translations';

describe('Customer Portal — Home smoke', () => {
  let fixture: ComponentFixture<Home>;

  beforeEach(async () => {
    localStorage.removeItem(LOCALE_STORAGE_KEY);

    await TestBed.configureTestingModule({
      imports: [Home],
      providers: [
        provideAppConfig(),
        provideNoopAnimations(),
        providePrimeNG({}),
        provideTranslations(COMMON_TRANSLATIONS),
        provideTranslations(PORTAL_TRANSLATIONS),
      ],
    }).compileComponents();

    TestBed.inject(AppConfigStore).set(
      validateAppConfig({
        apiBaseUrl: 'http://localhost:5080',
        defaultLocale: 'en',
        supportedLocales: ['en', 'ar'],
        appSurface: 'customer-portal',
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

  it('renders a PrimeNG button with a PrimeIcon', () => {
    const button: HTMLElement | null = fixture.nativeElement.querySelector('p-button button');

    expect(button).not.toBeNull();
    expect(button?.querySelector('.pi.pi-globe')).not.toBeNull();
  });

  it('runs on the customer-portal surface with the default locale applied to <html>', () => {
    expect(TestBed.inject(AppConfigStore).require().appSurface).toBe('customer-portal');
    expect(document.documentElement.getAttribute('lang')).toBe('en');
    expect(document.documentElement.getAttribute('dir')).toBe('ltr');
  });

  it('switches the document direction to rtl when the locale toggles to ar', () => {
    fixture.nativeElement.querySelector('[data-testid="locale-toggle"] button').click();
    fixture.detectChanges();

    expect(TestBed.inject(LocaleService).locale()).toBe('ar');
    expect(document.documentElement.getAttribute('dir')).toBe('rtl');
    expect(fixture.nativeElement.querySelector('h1').textContent).toContain('بوابة العملاء');
  });
});
