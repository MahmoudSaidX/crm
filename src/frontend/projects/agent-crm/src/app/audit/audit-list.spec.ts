import { Params, provideRouter, Router } from '@angular/router';
import { provideLocationMocks } from '@angular/common/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AuditList } from './audit-list';
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

describe('AuditList', () => {
  const recordA = {
    id: 1,
    actorHandle: 'bootstrap-tool',
    action: 'role_assigned',
    entityType: 'StaffSubjectRole',
    entityId: 'subject-1:role-1',
    metadata: { roleCode: 'ADMIN' },
    occurredAtUtc: '2026-08-31T00:00:00Z',
  };
  const recordB = { ...recordA, id: 2, entityId: 'subject-2:role-1' };

  let list: jasmine.SpyObj<AuditService>;

  beforeEach(() => {
    localStorage.removeItem('sc.locale');
    list = jasmine.createSpyObj<AuditService>('AuditService', ['list', 'get']);
    list.list.and.resolveTo({ items: [recordA, recordB], page: 1, pageSize: 20, totalCount: 2 });

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideLocationMocks(),
        provideAppConfig(),
        provideTranslations(COMMON_TRANSLATIONS),
        provideTranslations(AUDIT_TRANSLATIONS),
        { provide: AuditService, useValue: list },
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

  async function openAt(queryParams: Params): Promise<ComponentFixture<AuditList>> {
    await TestBed.inject(Router).navigate([], { queryParams });
    const fixture = TestBed.createComponent(AuditList);
    fixture.detectChanges();
    await fixture.whenStable();
    return fixture;
  }

  async function settle(fixture: ComponentFixture<AuditList>): Promise<void> {
    await fixture.whenStable();
    // A history navigation (Back/Forward) completes on a macrotask, which
    // whenStable alone does not await.
    await new Promise((resolve) => setTimeout(resolve));
    fixture.detectChanges();
    await fixture.whenStable();
  }

  it('renders rows from a mocked AuditService', async () => {
    const fixture = await openAt({});

    expect(fixture.componentInstance.auditRecords().length).toBe(2);
    expect(list.list).toHaveBeenCalledWith(1, 20, {
      entityType: undefined,
      action: undefined,
      actorHandle: undefined,
    });
  });

  it('filtering re-lists from page 1 with the entered filter values', async () => {
    const fixture = await openAt({ page: '2' });
    fixture.componentInstance.entityType.set('StaffSubjectRole');

    fixture.componentInstance.onFilter();
    await settle(fixture);

    expect(list.list).toHaveBeenCalledWith(1, 20, {
      entityType: 'StaffSubjectRole',
      action: undefined,
      actorHandle: undefined,
    });
  });

  it('derives the request from the URL and restores the filter inputs', async () => {
    const fixture = await openAt({ entityType: 'Role', action: 'granted', page: '2' });

    expect(list.list).toHaveBeenCalledWith(2, 20, {
      entityType: 'Role',
      action: 'granted',
      actorHandle: undefined,
    });
    expect(fixture.componentInstance.entityType()).toBe('Role');
    expect(fixture.componentInstance.action()).toBe('granted');
  });
});
