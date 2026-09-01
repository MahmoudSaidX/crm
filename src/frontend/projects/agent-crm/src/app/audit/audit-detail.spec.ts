import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { AuditDetail } from './audit-detail';
import { AuditService } from './audit.service';
import {
  AppConfigStore,
  LocaleService,
  provideAppConfig,
  provideTranslations,
  validateAppConfig,
} from '@squad-crm/platform';
import { COMMON_TRANSLATIONS } from '@squad-crm/shared-ui';
import { AUDIT_TRANSLATIONS } from './audit-translations';

describe('AuditDetail', () => {
  const record = {
    id: 1,
    actorHandle: 'bootstrap-tool',
    action: 'role_assigned',
    entityType: 'StaffSubjectRole',
    entityId: 'subject-1:role-1',
    metadata: { roleCode: 'ADMIN' },
    occurredAtUtc: '2026-08-31T00:00:00Z',
  };

  function configure(get: jasmine.Spy): void {
    const auditService = jasmine.createSpyObj<AuditService>('AuditService', ['list', 'get']);
    auditService.get = get;

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideAppConfig(),
        provideTranslations(COMMON_TRANSLATIONS),
        provideTranslations(AUDIT_TRANSLATIONS),
        { provide: AuditService, useValue: auditService },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: '1' }) } },
        },
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
  }

  afterEach(() => {
    localStorage.removeItem('sc.locale');
    document.documentElement.setAttribute('lang', 'en');
    document.documentElement.setAttribute('dir', 'ltr');
  });

  it('renders the loaded audit record', async () => {
    configure(jasmine.createSpy().and.resolveTo(record));
    const fixture = TestBed.createComponent(AuditDetail);
    await Promise.resolve();
    await Promise.resolve();
    fixture.detectChanges();

    expect(fixture.componentInstance.auditRecord()).toEqual(record);
    expect(fixture.componentInstance.notFound()).toBeFalse();
  });

  it('sets notFound when the audit record cannot be loaded', async () => {
    configure(jasmine.createSpy().and.rejectWith(new Error('not found')));
    const fixture = TestBed.createComponent(AuditDetail);
    await Promise.resolve();
    await Promise.resolve();
    fixture.detectChanges();

    expect(fixture.componentInstance.notFound()).toBeTrue();
  });
});
