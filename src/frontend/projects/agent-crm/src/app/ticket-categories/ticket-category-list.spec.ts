import { provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { TicketCategoryList } from './ticket-category-list';
import { TicketCategoriesService } from './ticket-categories.service';
import { DepartmentsService } from '../departments/departments.service';
import {
  AppConfigStore,
  LocaleService,
  provideAppConfig,
  provideTranslations,
  validateAppConfig,
} from '@squad-crm/platform';
import { COMMON_TRANSLATIONS } from '@squad-crm/shared-ui';
import { TICKET_CATEGORY_TRANSLATIONS } from './ticket-category-translations';
import { Paginator } from 'primeng/paginator';
import { AuthorizationState } from '../auth/authorization.state';

describe('TicketCategoryList', () => {
  const categoryA = {
    id: 'category-a',
    code: 'BILLING',
    arabicName: 'الفواتير',
    englishName: 'Billing',
    defaultDepartmentId: 'department-a',
    sortOrder: 1,
    isActive: true,
    createdAtUtc: '2026-08-29T00:00:00Z',
    updatedAtUtc: '2026-08-29T00:00:00Z',
  };
  const categoryB = {
    ...categoryA,
    id: 'category-b',
    englishName: 'Technical Support',
    defaultDepartmentId: null,
    isActive: false,
  };

  let list: jasmine.SpyObj<TicketCategoriesService>;
  let departments: jasmine.SpyObj<DepartmentsService>;

  beforeEach(() => {
    localStorage.removeItem('sc.locale');
    list = jasmine.createSpyObj<TicketCategoriesService>('TicketCategoriesService', [
      'list',
      'activate',
      'deactivate',
    ]);
    list.list.and.resolveTo({
      items: [categoryA, categoryB],
      page: 1,
      pageSize: 20,
      totalCount: 2,
    });
    list.activate.and.resolveTo({ ...categoryB, isActive: true });
    list.deactivate.and.resolveTo({ ...categoryA, isActive: false });

    departments = jasmine.createSpyObj<DepartmentsService>('DepartmentsService', ['list']);
    departments.list.and.resolveTo({
      items: [
        {
          id: 'department-a',
          code: 'SALES',
          arabicName: 'المبيعات',
          englishName: 'Sales',
          description: null,
          isActive: true,
          createdAtUtc: '2026-08-29T00:00:00Z',
          updatedAtUtc: '2026-08-29T00:00:00Z',
        },
      ],
      page: 1,
      pageSize: 200,
      totalCount: 1,
    });

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideAppConfig(),
        provideTranslations(COMMON_TRANSLATIONS),
        provideTranslations(TICKET_CATEGORY_TRANSLATIONS),
        { provide: TicketCategoriesService, useValue: list },
        { provide: DepartmentsService, useValue: departments },
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

  it('renders rows from a mocked TicketCategoriesService', async () => {
    const fixture = TestBed.createComponent(TicketCategoryList);
    await fixture.componentInstance.load();
    fixture.detectChanges();

    expect(fixture.componentInstance.categories().length).toBe(2);
    expect(list.list).toHaveBeenCalledWith(1, 20);
  });

  it('resolves the default department name from the loaded department list', async () => {
    const fixture = TestBed.createComponent(TicketCategoryList);
    await fixture.componentInstance.load();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Sales');
  });

  it('activating an inactive row calls activate and refreshes', async () => {
    const fixture = TestBed.createComponent(TicketCategoryList);
    await fixture.componentInstance.load();

    await fixture.componentInstance.toggleActive(categoryB);

    expect(list.activate).toHaveBeenCalledWith('category-b');
    expect(list.list).toHaveBeenCalledTimes(2);
  });

  it('deactivating an active row calls deactivate and refreshes', async () => {
    const fixture = TestBed.createComponent(TicketCategoryList);
    await fixture.componentInstance.load();

    await fixture.componentInstance.toggleActive(categoryA);

    expect(list.deactivate).toHaveBeenCalledWith('category-a');
    expect(list.list).toHaveBeenCalledTimes(2);
  });

  it('localizes feature chrome without translating category values', async () => {
    TestBed.inject(LocaleService).setLocale('ar');
    const fixture = TestBed.createComponent(TicketCategoryList);
    await fixture.componentInstance.load();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('فئات التذاكر');
    expect(fixture.nativeElement.textContent).toContain('Billing');
    const paginator = fixture.debugElement.query(By.directive(Paginator))
      .componentInstance as Paginator;
    expect(paginator.locale).toBe('ar');
  });

  it('hides management actions until ticketcategories.manage is granted', async () => {
    const fixture = TestBed.createComponent(TicketCategoryList);
    await fixture.componentInstance.load();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('Edit');

    TestBed.inject(AuthorizationState).set(['ticketcategories.view', 'ticketcategories.manage']);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Edit');
  });
});
