import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Router } from '@angular/router';
import {
  AppConfigStore,
  LOCALE_STORAGE_KEY,
  LocaleService,
  provideAppConfig,
  provideTranslations,
  validateAppConfig,
} from '@squad-crm/platform';
import { COMMON_TRANSLATIONS } from '@squad-crm/shared-ui';
import { providePrimeNG } from 'primeng/config';
import { AGENT_TRANSLATIONS } from '../i18n/agent-translations';
import { AuthService } from './auth.service';
import { Login } from './login';

describe('Login localization', () => {
  let auth: jasmine.SpyObj<AuthService>;

  beforeEach(() => {
    localStorage.removeItem(LOCALE_STORAGE_KEY);
    auth = jasmine.createSpyObj<AuthService>('AuthService', ['signIn']);
    TestBed.configureTestingModule({
      imports: [Login],
      providers: [
        provideAppConfig(),
        provideNoopAnimations(),
        providePrimeNG({}),
        provideTranslations(COMMON_TRANSLATIONS),
        provideTranslations(AGENT_TRANSLATIONS),
        { provide: AuthService, useValue: auth },
        { provide: Router, useValue: jasmine.createSpyObj<Router>('Router', ['navigateByUrl']) },
      ],
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
  });

  afterEach(() => {
    localStorage.removeItem(LOCALE_STORAGE_KEY);
    document.documentElement.setAttribute('lang', 'en');
    document.documentElement.setAttribute('dir', 'ltr');
  });

  it('renders English then Arabic from stable resources', () => {
    const fixture = TestBed.createComponent(Login);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('h1').textContent).toContain('Sign in to Agent CRM');

    fixture.nativeElement.querySelector('[data-testid="locale-toggle"] button').click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('h1').textContent).toContain(
      'تسجيل الدخول إلى نظام الموظفين',
    );
    expect(document.documentElement.getAttribute('dir')).toBe('rtl');
  });

  it('shows a localized security-safe rejection without backend text', async () => {
    auth.signIn.and.rejectWith(new Error('backend detail must not render'));
    const fixture = TestBed.createComponent(Login);
    fixture.componentInstance.form.setValue({
      email: 'agent@example.test',
      password: 'SyntheticPassword!42',
      rememberSession: false,
    });

    await fixture.componentInstance.submit();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain(
      'Email or password is incorrect, or the account is inactive.',
    );
    expect(fixture.nativeElement.textContent).not.toContain('backend detail');
  });
});
