import { provideRouter, Router } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { CustomerList } from './customer-list';
import { CustomersService } from './customers.service';
import { DepartmentsService } from '../departments/departments.service';
import { BranchesService } from '../branches/branches.service';
import {
  AppConfigStore,
  provideAppConfig,
  provideTranslations,
  validateAppConfig,
} from '@squad-crm/platform';
import { COMMON_TRANSLATIONS } from '@squad-crm/shared-ui';
import { CUSTOMER_TRANSLATIONS } from './customer-translations';
import { AuthorizationState } from '../auth/authorization.state';

describe('CustomerList', () => {
  const customerA = {
    id: 'customer-a',
    customerNumber: 'CUS-AAA111',
    firstName: 'Sara',
    lastName: 'Ahmed',
    preferredLanguage: null,
    departmentId: null,
    branchId: null,
    status: 'Active' as const,
    createdAtUtc: '2026-09-02T00:00:00Z',
    updatedAtUtc: '2026-09-02T00:00:00Z',
  };
  const customerB = { ...customerA, id: 'customer-b', customerNumber: 'CUS-BBB222', firstName: 'Omar' };

  let customersService: jasmine.SpyObj<CustomersService>;
  let router: Router;

  beforeEach(() => {
    customersService = jasmine.createSpyObj<CustomersService>('CustomersService', ['list']);
    customersService.list.and.resolveTo({
      items: [customerA, customerB],
      page: 1,
      pageSize: 20,
      totalCount: 2,
    });
    const departmentsService = jasmine.createSpyObj<DepartmentsService>('DepartmentsService', ['list']);
    departmentsService.list.and.resolveTo({ items: [], page: 1, pageSize: 200, totalCount: 0 });
    const branchesService = jasmine.createSpyObj<BranchesService>('BranchesService', ['list']);
    branchesService.list.and.resolveTo({ items: [], page: 1, pageSize: 200, totalCount: 0 });

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideAppConfig(),
        provideTranslations(COMMON_TRANSLATIONS),
        provideTranslations(CUSTOMER_TRANSLATIONS),
        { provide: CustomersService, useValue: customersService },
        { provide: DepartmentsService, useValue: departmentsService },
        { provide: BranchesService, useValue: branchesService },
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
    router = TestBed.inject(Router);
  });

  it('loads and renders rows from a mocked CustomersService', async () => {
    const fixture = TestBed.createComponent(CustomerList);
    await fixture.componentInstance.load();
    fixture.detectChanges();

    expect(fixture.componentInstance.customers().length).toBe(2);
    expect(customersService.list).toHaveBeenCalledWith({ search: undefined, departmentIds: undefined, branchIds: undefined }, 1, 20);
  });

  it('applies the search term on the next load', async () => {
    const fixture = TestBed.createComponent(CustomerList);
    fixture.componentInstance.search.set('sara');

    fixture.componentInstance.onFilter();
    await fixture.whenStable();

    expect(customersService.list).toHaveBeenCalledWith(
      { search: 'sara', departmentIds: undefined, branchIds: undefined },
      1,
      20,
    );
  });

  it('navigates to the customer detail route on row selection', async () => {
    const navigateSpy = spyOn(router, 'navigate');
    const fixture = TestBed.createComponent(CustomerList);
    await fixture.componentInstance.load();

    fixture.componentInstance.openCustomer(customerA);

    expect(navigateSpy).toHaveBeenCalledWith(['/customers', 'customer-a']);
  });

  it('hides the new-customer action until customers.manage is granted', async () => {
    const fixture = TestBed.createComponent(CustomerList);
    await fixture.componentInstance.load();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('New customer');

    TestBed.inject(AuthorizationState).set(['customers.view', 'customers.manage']);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('New customer');
  });
});
