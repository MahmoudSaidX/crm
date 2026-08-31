import { provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { RoleList } from './role-list';
import { RolesService } from './roles.service';
import {
  AppConfigStore,
  LocaleService,
  provideAppConfig,
  provideTranslations,
  validateAppConfig,
} from '@squad-crm/platform';
import { COMMON_TRANSLATIONS } from '@squad-crm/shared-ui';
import { ROLE_TRANSLATIONS } from './role-translations';
import { Paginator } from 'primeng/paginator';
import { AuthorizationState } from '../auth/authorization.state';

describe('RoleList', () => {
  const roleA = {
    id: 'role-a',
    name: 'Sales Manager',
    code: 'SALES_MANAGER',
    description: null,
    isActive: true,
    createdAtUtc: '2026-08-29T00:00:00Z',
    updatedAtUtc: '2026-08-29T00:00:00Z',
  };
  const roleB = { ...roleA, id: 'role-b', name: 'Support Agent', isActive: false };

  let list: jasmine.SpyObj<RolesService>;

  beforeEach(() => {
    localStorage.removeItem('sc.locale');
    list = jasmine.createSpyObj<RolesService>('RolesService', ['list', 'activate', 'deactivate']);
    list.list.and.resolveTo({ items: [roleA, roleB], page: 1, pageSize: 20, totalCount: 2 });
    list.activate.and.resolveTo({ ...roleB, isActive: true });
    list.deactivate.and.resolveTo({ ...roleA, isActive: false });

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideAppConfig(),
        provideTranslations(COMMON_TRANSLATIONS),
        provideTranslations(ROLE_TRANSLATIONS),
        { provide: RolesService, useValue: list },
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

  it('renders rows from a mocked RolesService', async () => {
    const fixture = TestBed.createComponent(RoleList);
    await fixture.componentInstance.load();
    fixture.detectChanges();

    expect(fixture.componentInstance.roles().length).toBe(2);
    expect(list.list).toHaveBeenCalledWith(1, 20);
  });

  it('activating an inactive row calls activate and refreshes', async () => {
    const fixture = TestBed.createComponent(RoleList);
    await fixture.componentInstance.load();

    await fixture.componentInstance.toggleActive(roleB);

    expect(list.activate).toHaveBeenCalledWith('role-b');
    expect(list.list).toHaveBeenCalledTimes(2);
  });

  it('deactivating an active row calls deactivate and refreshes', async () => {
    const fixture = TestBed.createComponent(RoleList);
    await fixture.componentInstance.load();

    await fixture.componentInstance.toggleActive(roleA);

    expect(list.deactivate).toHaveBeenCalledWith('role-a');
    expect(list.list).toHaveBeenCalledTimes(2);
  });

  it('localizes feature chrome without translating role values', async () => {
    TestBed.inject(LocaleService).setLocale('ar');
    const fixture = TestBed.createComponent(RoleList);
    await fixture.componentInstance.load();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('الأدوار');
    expect(fixture.nativeElement.textContent).toContain('Sales Manager');
    const paginator = fixture.debugElement.query(By.directive(Paginator))
      .componentInstance as Paginator;
    expect(paginator.locale).toBe('ar');
  });

  it('hides management actions until roles.manage is granted', async () => {
    const fixture = TestBed.createComponent(RoleList);
    await fixture.componentInstance.load();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('Permissions');

    TestBed.inject(AuthorizationState).set(['roles.view', 'roles.manage']);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Permissions');
  });
});
