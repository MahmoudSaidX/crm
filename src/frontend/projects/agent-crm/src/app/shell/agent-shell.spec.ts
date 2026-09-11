import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import {
  AppConfigStore,
  BrandingService,
  DEFAULT_BRANDING,
  provideAppConfig,
  provideTranslations,
  validateAppConfig,
} from '@squad-crm/platform';
import { COMMON_TRANSLATIONS } from '@squad-crm/shared-ui';
import { AgentShell } from './agent-shell';
import { AuthService } from '../auth/auth.service';
import { AuthorizationService } from '../auth/authorization.service';
import { AuthorizationState } from '../auth/authorization.state';
import { AGENT_TRANSLATIONS } from '../i18n/agent-translations';

describe('AgentShell', () => {
  let authorization: AuthorizationState;

  async function setup(permissions: readonly string[]): Promise<HTMLElement> {
    const branding = {
      value: signal(DEFAULT_BRANDING),
      load: () => Promise.resolve(),
    } as unknown as BrandingService;

    await TestBed.configureTestingModule({
      imports: [AgentShell],
      providers: [
        provideRouter([]),
        provideAppConfig(),
        provideTranslations(COMMON_TRANSLATIONS),
        provideTranslations(AGENT_TRANSLATIONS),
        { provide: BrandingService, useValue: branding },
        {
          provide: AuthorizationService,
          useFactory: () => ({
            load: () => Promise.resolve(),
            state: TestBed.inject(AuthorizationState),
          }),
        },
        { provide: AuthService, useValue: jasmine.createSpyObj('AuthService', ['signOut']) },
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
    authorization = TestBed.inject(AuthorizationState);
    authorization.set(permissions);

    const fixture = TestBed.createComponent(AgentShell);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('renders only the groups the agent has permissions for', async () => {
    const element = await setup(['tickets.view']);
    const groups = Array.from(element.querySelectorAll('.layout-menuitem-root-text')).map(
      (heading) => heading.textContent?.trim(),
    );

    expect(groups).toContain('Overview');
    expect(groups).toContain('Tickets');
    expect(groups).not.toContain('Administration');
    expect(groups).not.toContain('Customers');
    expect(element.textContent).toContain('My tickets');
  });

  it('shows administration entries for the permissions the agent holds', async () => {
    const element = await setup(['audit.view', 'roles.view']);
    const links = Array.from(element.querySelectorAll('.layout-menu a')).map((link) =>
      link.textContent?.trim(),
    );

    expect(links).toContain('Audit');
    expect(links).toContain('Roles');
    expect(links).not.toContain('Branding');
  });

  it('signs the agent out from the topbar action', async () => {
    const element = await setup(['tickets.view']);
    const auth = TestBed.inject(AuthService) as jasmine.SpyObj<AuthService>;
    auth.signOut.and.resolveTo();

    element.querySelector<HTMLButtonElement>('[data-testid="sign-out"] button')!.click();

    expect(auth.signOut).toHaveBeenCalled();
  });
});
