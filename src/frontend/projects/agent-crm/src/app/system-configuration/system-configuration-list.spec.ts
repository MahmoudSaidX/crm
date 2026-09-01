import { provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { SystemConfigurationList } from './system-configuration-list';
import { ConfigurationValue, SystemConfigurationService } from './system-configuration.service';
import {
  AppConfigStore,
  LocaleService,
  provideAppConfig,
  provideTranslations,
  validateAppConfig,
} from '@squad-crm/platform';
import { COMMON_TRANSLATIONS } from '@squad-crm/shared-ui';
import { SYSTEM_CONFIGURATION_TRANSLATIONS } from './system-configuration-translations';
import { AuthorizationState } from '../auth/authorization.state';

describe('SystemConfigurationList', () => {
  const companyName: ConfigurationValue = {
    key: 'general.company_display_name',
    valueType: 'String',
    displayNameEn: 'Company display name',
    displayNameAr: 'اسم الشركة المعروض',
    descriptionEn: null,
    descriptionAr: null,
    value: 'Squad CRM',
    hasValue: false,
    defaultValue: 'Squad CRM',
    isSensitive: false,
    requiresRestart: false,
    isEditable: true,
    minNumber: null,
    maxNumber: null,
    updatedByHandle: null,
    updatedAtUtc: null,
  };
  const smtpPassword: ConfigurationValue = {
    key: 'integrations.smtp_password',
    valueType: 'String',
    displayNameEn: 'SMTP password',
    displayNameAr: 'كلمة مرور SMTP',
    descriptionEn: null,
    descriptionAr: null,
    value: null,
    hasValue: false,
    defaultValue: '',
    isSensitive: true,
    requiresRestart: true,
    isEditable: true,
    minNumber: null,
    maxNumber: null,
    updatedByHandle: null,
    updatedAtUtc: null,
  };

  let service: jasmine.SpyObj<SystemConfigurationService>;

  beforeEach(() => {
    localStorage.removeItem('sc.locale');
    service = jasmine.createSpyObj<SystemConfigurationService>('SystemConfigurationService', [
      'list',
      'update',
    ]);
    service.list.and.resolveTo([companyName, smtpPassword]);
    service.update.and.resolveTo({ ...companyName, value: 'Contoso CRM', hasValue: true });

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideAppConfig(),
        provideTranslations(COMMON_TRANSLATIONS),
        provideTranslations(SYSTEM_CONFIGURATION_TRANSLATIONS),
        { provide: SystemConfigurationService, useValue: service },
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
    localStorage.removeItem('sc.locale');
    document.documentElement.setAttribute('lang', 'en');
    document.documentElement.setAttribute('dir', 'ltr');
  });

  it('renders rows from a mocked SystemConfigurationService', async () => {
    const fixture = TestBed.createComponent(SystemConfigurationList);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(fixture.componentInstance.values().length).toBe(2);
    expect(service.list).toHaveBeenCalled();
  });

  it('never renders the raw value of a sensitive key', async () => {
    const fixture = TestBed.createComponent(SystemConfigurationList);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).not.toContain('super-secret');
    expect(fixture.nativeElement.textContent).toContain('Not set');
  });

  it('saving an edited value calls update with the new value and refreshes the row', async () => {
    const fixture = TestBed.createComponent(SystemConfigurationList);
    fixture.detectChanges();
    await fixture.whenStable();

    fixture.componentInstance.startEdit(companyName);
    fixture.componentInstance.editText.set('Contoso CRM');
    await fixture.componentInstance.save(companyName);

    expect(service.update).toHaveBeenCalledWith('general.company_display_name', 'Contoso CRM');
    expect(fixture.componentInstance.values().find((v) => v.key === companyName.key)?.value).toBe(
      'Contoso CRM',
    );
  });

  it('hides edit actions until configuration.manage is granted', async () => {
    const fixture = TestBed.createComponent(SystemConfigurationList);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('Edit');

    TestBed.inject(AuthorizationState).set(['configuration.view', 'configuration.manage']);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Edit');
  });
});
