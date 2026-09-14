import { Location } from '@angular/common';
import { provideLocationMocks } from '@angular/common/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Params, provideRouter, Router } from '@angular/router';
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

/**
 * Customer list — the reference implementation of URL-as-list-state.
 *
 * Every test here drives the component the way a user or a pasted link does:
 * by navigating. Nothing calls `load()` directly, because a test that reached
 * past the URL would pass even if the URL were wired up wrong, which is the
 * exact defect these tests exist to prevent.
 */
describe('CustomerList', () => {
  const DEPARTMENT_ID = '2f1a4c9e-7b3d-4a6f-8e12-0b9d5c3a7e41';

  const customerA = {
    id: 'customer-a',
    customerNumber: 'CUS-AAA111',
    firstName: 'Sara',
    lastName: 'Ahmed',
    preferredLanguage: null,
    departmentId: null,
    branchId: null,
    status: 'Active' as const,
    version: 1,
    createdAtUtc: '2026-09-02T00:00:00Z',
    updatedAtUtc: '2026-09-02T00:00:00Z',
  };
  const customerB = {
    ...customerA,
    id: 'customer-b',
    customerNumber: 'CUS-BBB222',
    firstName: 'Omar',
  };

  let customersService: jasmine.SpyObj<CustomersService>;
  let router: Router;
  let location: Location;

  /** The query the component sent on its most recent load. */
  const lastCall = () => customersService.list.calls.mostRecent().args;

  async function openAt(queryParams: Params): Promise<ComponentFixture<CustomerList>> {
    await router.navigate([], { queryParams });
    const fixture = TestBed.createComponent(CustomerList);
    fixture.detectChanges();
    await fixture.whenStable();
    return fixture;
  }

  async function settle(fixture: ComponentFixture<CustomerList>): Promise<void> {
    await fixture.whenStable();
    // A history navigation (Back/Forward) completes on a macrotask, which
    // whenStable alone does not await.
    await new Promise((resolve) => setTimeout(resolve));
    fixture.detectChanges();
    await fixture.whenStable();
  }

  beforeEach(async () => {
    customersService = jasmine.createSpyObj<CustomersService>('CustomersService', ['list']);
    // totalCount deliberately spans several pages: with a single-page total the
    // paginator correctly clamps an out-of-range page back to 1, which would
    // make every paging assertion below test the clamp instead of the URL.
    customersService.list.and.resolveTo({
      items: [customerA, customerB],
      page: 1,
      pageSize: 20,
      totalCount: 42,
    });
    const departmentsService = jasmine.createSpyObj<DepartmentsService>('DepartmentsService', [
      'list',
    ]);
    departmentsService.list.and.resolveTo({ items: [], page: 1, pageSize: 200, totalCount: 0 });
    const branchesService = jasmine.createSpyObj<BranchesService>('BranchesService', ['list']);
    branchesService.list.and.resolveTo({ items: [], page: 1, pageSize: 200, totalCount: 0 });

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideLocationMocks(),
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
    // No app bootstrap happens in a TestBed component test, so the router is
    // not listening to history events by default — without this, Back/Forward
    // would change the URL and navigate nothing.
    router.setUpLocationChangeListener();
    location = TestBed.inject(Location);
  });

  it('loads and renders rows with the default state when the URL carries none', async () => {
    const fixture = await openAt({});

    expect(fixture.componentInstance.customers().length).toBe(2);
    const [query, page, pageSize] = lastCall();
    expect(page).toBe(1);
    expect(pageSize).toBe(20);
    expect(query.search).toBeUndefined();
  });

  it('derives the API request from the URL query parameters', async () => {
    await openAt({ page: '3', pageSize: '50', search: 'sara', departmentId: DEPARTMENT_ID });

    const [query, page, pageSize] = lastCall();
    expect(page).toBe(3);
    expect(pageSize).toBe(50);
    expect(query.search).toBe('sara');
    expect(query.departmentIds).toEqual([DEPARTMENT_ID]);
  });

  it('reconstructs the inputs from a deep link, not only the results', async () => {
    const fixture = await openAt({ search: 'sara', departmentId: DEPARTMENT_ID });

    expect(fixture.componentInstance.search()).toBe('sara');
    expect(fixture.componentInstance.departmentId()).toBe(DEPARTMENT_ID);
  });

  it('puts a page change in the URL and preserves search and filters', async () => {
    const fixture = await openAt({ search: 'sara', departmentId: DEPARTMENT_ID });

    fixture.componentInstance.onPage({ first: 20, rows: 20 });
    await settle(fixture);

    expect(location.path()).toContain('page=2');
    expect(location.path()).toContain('search=sara');
    const [query, page] = lastCall();
    expect(page).toBe(2);
    expect(query.search).toBe('sara');
    expect(query.departmentIds).toEqual([DEPARTMENT_ID]);
  });

  it('resets to page 1 when the search changes', async () => {
    const fixture = await openAt({ page: '3', search: 'sara' });

    fixture.componentInstance.search.set('omar');
    fixture.componentInstance.onFilter();
    await settle(fixture);

    expect(location.path()).not.toContain('page=3');
    expect(location.path()).toContain('search=omar');
    const [query, page] = lastCall();
    expect(page).toBe(1);
    expect(query.search).toBe('omar');
  });

  it('resets to page 1 when a filter changes', async () => {
    const fixture = await openAt({ page: '4' });

    fixture.componentInstance.departmentId.set(DEPARTMENT_ID);
    fixture.componentInstance.onFilter();
    await settle(fixture);

    expect(lastCall()[1]).toBe(1);
    expect(location.path()).toContain(`departmentId=${DEPARTMENT_ID}`);
  });

  it('omits defaults and cleared filters from the URL', async () => {
    const fixture = await openAt({ search: 'sara' });

    fixture.componentInstance.search.set('');
    fixture.componentInstance.onFilter();
    await settle(fixture);

    expect(location.path()).not.toContain('search=');
    expect(location.path()).not.toContain('page=1');
    expect(location.path()).not.toContain('pageSize=20');
  });

  it('restores state and results on Back and Forward', async () => {
    const fixture = await openAt({ search: 'sara' });

    fixture.componentInstance.onPage({ first: 20, rows: 20 });
    await settle(fixture);
    expect(lastCall()[1]).toBe(2);

    location.back();
    await settle(fixture);
    expect(lastCall()[1]).toBe(1);
    expect(fixture.componentInstance.search()).toBe('sara');

    location.forward();
    await settle(fixture);
    expect(lastCall()[1]).toBe(2);
  });

  // Malformed URL state must normalize to a working screen. The backend
  // independently rejects anything out of bounds; this is the UX half.
  it('normalizes malformed pagination instead of failing', async () => {
    await openAt({ page: 'abc', pageSize: '100000' });

    const [, page, pageSize] = lastCall();
    expect(page).toBe(1);
    expect(pageSize).toBe(200);
  });

  it('drops an unknown sort field and a malformed id filter', async () => {
    await openAt({ sort: 'DROP TABLE', dir: 'sideways', departmentId: 'not-a-guid' });

    const [query] = lastCall();
    expect(query.sortBy).toBe('CustomerNumber');
    expect(query.sortDirection).toBe('Asc');
    expect(query.departmentIds).toBeUndefined();
  });

  it('forwards an allow-listed sort from the URL', async () => {
    await openAt({ sort: 'CreatedAtUtc', dir: 'Desc' });

    const [query] = lastCall();
    expect(query.sortBy).toBe('CreatedAtUtc');
    expect(query.sortDirection).toBe('Desc');
  });

  // Arabic and Unicode search terms are ordinary business data and must reach
  // the API byte-for-byte.
  for (const term of ['محمود', 'عبد الله', "O'Brien", 'Ünicode ✨']) {
    it(`round-trips ${term} through the URL to the API unchanged`, async () => {
      const fixture = await openAt({});
      fixture.componentInstance.search.set(term);
      fixture.componentInstance.onFilter();
      await settle(fixture);

      expect(lastCall()[0].search).toBe(term);
      expect(fixture.componentInstance.search()).toBe(term);
    });
  }

  it('does not issue a second request when a navigation changes nothing', async () => {
    const fixture = await openAt({ search: 'sara' });
    const before = customersService.list.calls.count();

    fixture.componentInstance.onFilter();
    await settle(fixture);

    expect(customersService.list.calls.count()).toBe(before);
  });

  it('navigates to the customer detail route on row selection', async () => {
    const fixture = await openAt({});
    const navigateSpy = spyOn(router, 'navigate');

    fixture.componentInstance.openCustomer(customerA);

    expect(navigateSpy).toHaveBeenCalledWith(['/customers', 'customer-a']);
  });

  it('hides the new-customer action until customers.manage is granted', async () => {
    const fixture = await openAt({});
    expect(fixture.nativeElement.textContent).not.toContain('New customer');

    TestBed.inject(AuthorizationState).set(['customers.view', 'customers.manage']);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('New customer');
  });
});
