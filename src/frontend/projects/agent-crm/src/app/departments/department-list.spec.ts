import { provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { DepartmentList } from './department-list';
import { DepartmentsService } from './departments.service';
import {
  AppConfigStore,
  LocaleService,
  provideAppConfig,
  provideTranslations,
  validateAppConfig,
} from '@squad-crm/platform';
import { COMMON_TRANSLATIONS } from '@squad-crm/shared-ui';
import { DEPARTMENT_TRANSLATIONS } from './department-translations';
import { Paginator } from 'primeng/paginator';
import { AuthorizationState } from '../auth/authorization.state';

describe('DepartmentList', () => {
  const departmentA = {
    id: 'department-a',
    code: 'SALES',
    arabicName: 'المبيعات',
    englishName: 'Sales',
    description: null,
    isActive: true,
    createdAtUtc: '2026-08-29T00:00:00Z',
    updatedAtUtc: '2026-08-29T00:00:00Z',
  };
  const departmentB = { ...departmentA, id: 'department-b', englishName: 'Support', isActive: false };

  let list: jasmine.SpyObj<DepartmentsService>;

  beforeEach(() => {
    localStorage.removeItem('sc.locale');
    list = jasmine.createSpyObj<DepartmentsService>('DepartmentsService', [
      'list',
      'activate',
      'deactivate',
    ]);
    list.list.and.resolveTo({ items: [departmentA, departmentB], page: 1, pageSize: 20, totalCount: 2 });
    list.activate.and.resolveTo({ ...departmentB, isActive: true });
    list.deactivate.and.resolveTo({ ...departmentA, isActive: false });

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideAppConfig(),
        provideTranslations(COMMON_TRANSLATIONS),
        provideTranslations(DEPARTMENT_TRANSLATIONS),
        { provide: DepartmentsService, useValue: list },
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

  it('renders rows from a mocked DepartmentsService', async () => {
    const fixture = TestBed.createComponent(DepartmentList);
    await fixture.componentInstance.load();
    fixture.detectChanges();

    expect(fixture.componentInstance.departments().length).toBe(2);
    expect(list.list).toHaveBeenCalledWith(1, 20);
  });

  it('activating an inactive row calls activate and refreshes', async () => {
    const fixture = TestBed.createComponent(DepartmentList);
    await fixture.componentInstance.load();

    await fixture.componentInstance.toggleActive(departmentB);

    expect(list.activate).toHaveBeenCalledWith('department-b');
    expect(list.list).toHaveBeenCalledTimes(2);
  });

  it('deactivating an active row calls deactivate and refreshes', async () => {
    const fixture = TestBed.createComponent(DepartmentList);
    await fixture.componentInstance.load();

    await fixture.componentInstance.toggleActive(departmentA);

    expect(list.deactivate).toHaveBeenCalledWith('department-a');
    expect(list.list).toHaveBeenCalledTimes(2);
  });

  it('localizes feature chrome without translating department values', async () => {
    TestBed.inject(LocaleService).setLocale('ar');
    const fixture = TestBed.createComponent(DepartmentList);
    await fixture.componentInstance.load();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('الأقسام');
    expect(fixture.nativeElement.textContent).toContain('Sales');
    const paginator = fixture.debugElement.query(By.directive(Paginator))
      .componentInstance as Paginator;
    expect(paginator.locale).toBe('ar');
  });

  it('hides management actions until departments.manage is granted', async () => {
    const fixture = TestBed.createComponent(DepartmentList);
    await fixture.componentInstance.load();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('Edit');

    TestBed.inject(AuthorizationState).set(['departments.view', 'departments.manage']);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Edit');
  });
});
