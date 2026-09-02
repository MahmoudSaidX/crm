import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { CustomerForm } from './customer-form';
import { CustomersService } from './customers.service';
import { DepartmentsService } from '../departments/departments.service';
import { BranchesService } from '../branches/branches.service';
import { provideTranslations } from '@squad-crm/platform';
import { COMMON_TRANSLATIONS } from '@squad-crm/shared-ui';
import { CUSTOMER_TRANSLATIONS } from './customer-translations';

describe('CustomerForm', () => {
  let customersService: jasmine.SpyObj<CustomersService>;
  let departmentsService: jasmine.SpyObj<DepartmentsService>;
  let branchesService: jasmine.SpyObj<BranchesService>;
  let router: jasmine.SpyObj<Router>;

  function configure(): void {
    customersService = jasmine.createSpyObj<CustomersService>('CustomersService', ['create']);
    departmentsService = jasmine.createSpyObj<DepartmentsService>('DepartmentsService', ['list']);
    departmentsService.list.and.resolveTo({ items: [], page: 1, pageSize: 200, totalCount: 0 });
    branchesService = jasmine.createSpyObj<BranchesService>('BranchesService', ['list']);
    branchesService.list.and.resolveTo({ items: [], page: 1, pageSize: 200, totalCount: 0 });
    router = jasmine.createSpyObj<Router>('Router', ['navigateByUrl']);

    TestBed.configureTestingModule({
      providers: [
        provideTranslations(COMMON_TRANSLATIONS),
        provideTranslations(CUSTOMER_TRANSLATIONS),
        { provide: CustomersService, useValue: customersService },
        { provide: DepartmentsService, useValue: departmentsService },
        { provide: BranchesService, useValue: branchesService },
        { provide: Router, useValue: router },
      ],
    });
  }

  it('blocks submit when required fields are missing', async () => {
    configure();
    const fixture = TestBed.createComponent(CustomerForm);
    fixture.detectChanges();

    await fixture.componentInstance.submit();

    expect(customersService.create).not.toHaveBeenCalled();
    expect(fixture.componentInstance.form.controls.firstName.touched).toBeTrue();
  });

  it('surfaces a duplicate-customer error from a mocked 409 response', async () => {
    configure();
    customersService.create.and.rejectWith(
      new HttpErrorResponse({ status: 409, error: { code: 'customers.duplicate_customer' } }),
    );
    const fixture = TestBed.createComponent(CustomerForm);
    fixture.detectChanges();
    fixture.componentInstance.form.patchValue({ firstName: 'Sara', lastName: 'Ahmed' });

    await fixture.componentInstance.submit();

    expect(fixture.componentInstance.errorKey()).toBe('customers.errors.duplicateCustomer');
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });

  it('surfaces an inactive-department error from a mocked 422 response', async () => {
    configure();
    customersService.create.and.rejectWith(
      new HttpErrorResponse({ status: 422, error: { code: 'customers.inactive_department' } }),
    );
    const fixture = TestBed.createComponent(CustomerForm);
    fixture.detectChanges();
    fixture.componentInstance.form.patchValue({ firstName: 'Sara', lastName: 'Ahmed' });

    await fixture.componentInstance.submit();

    expect(fixture.componentInstance.errorKey()).toBe('customers.errors.inactiveDepartment');
  });

  it('navigates to the home page on successful submit', async () => {
    configure();
    customersService.create.and.resolveTo({
      id: 'customer-1',
      customerNumber: 'CUS-ABC12345',
      firstName: 'Sara',
      lastName: 'Ahmed',
      preferredLanguage: null,
      departmentId: null,
      branchId: null,
      status: 'Active',
      createdAtUtc: '2026-09-02T00:00:00Z',
      updatedAtUtc: '2026-09-02T00:00:00Z',
    });
    const fixture = TestBed.createComponent(CustomerForm);
    fixture.detectChanges();
    fixture.componentInstance.form.patchValue({ firstName: 'Sara', lastName: 'Ahmed' });

    await fixture.componentInstance.submit();

    expect(router.navigateByUrl).toHaveBeenCalledWith('/');
  });
});
