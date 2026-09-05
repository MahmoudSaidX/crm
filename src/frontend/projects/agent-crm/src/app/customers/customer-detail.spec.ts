import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { CustomerDetail } from './customer-detail';
import { CustomerContact, CustomersService } from './customers.service';
import { DepartmentsService } from '../departments/departments.service';
import { BranchesService } from '../branches/branches.service';
import {
  provideAppConfig,
  provideTranslations,
  validateAppConfig,
  AppConfigStore,
} from '@squad-crm/platform';
import { COMMON_TRANSLATIONS } from '@squad-crm/shared-ui';
import { CUSTOMER_TRANSLATIONS } from './customer-translations';
import { AuthorizationState } from '../auth/authorization.state';

describe('CustomerDetail', () => {
  let customersService: jasmine.SpyObj<CustomersService>;
  let departmentsService: jasmine.SpyObj<DepartmentsService>;
  let branchesService: jasmine.SpyObj<BranchesService>;
  let authorization: AuthorizationState;

  function configure(id: string | null): void {
    customersService = jasmine.createSpyObj<CustomersService>('CustomersService', [
      'get',
      'update',
      'listContacts',
      'addContact',
      'updateContact',
      'deactivateContact',
    ]);
    customersService.listContacts.and.resolveTo([]);
    departmentsService = jasmine.createSpyObj<DepartmentsService>('DepartmentsService', ['list']);
    departmentsService.list.and.resolveTo({ items: [], page: 1, pageSize: 200, totalCount: 0 });
    branchesService = jasmine.createSpyObj<BranchesService>('BranchesService', ['list']);
    branchesService.list.and.resolveTo({ items: [], page: 1, pageSize: 200, totalCount: 0 });

    TestBed.configureTestingModule({
      providers: [
        provideAppConfig(),
        provideTranslations(COMMON_TRANSLATIONS),
        provideTranslations(CUSTOMER_TRANSLATIONS),
        { provide: CustomersService, useValue: customersService },
        { provide: DepartmentsService, useValue: departmentsService },
        { provide: BranchesService, useValue: branchesService },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap(id ? { id } : {}) } },
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
    authorization = TestBed.inject(AuthorizationState);
  }

  const customer = {
    id: 'customer-a',
    customerNumber: 'CUS-AAA111',
    firstName: 'Sara',
    lastName: 'Ahmed',
    preferredLanguage: 'Arabic' as const,
    departmentId: null,
    branchId: null,
    status: 'Active' as const,
    version: 1,
    createdAtUtc: '2026-09-02T00:00:00Z',
    updatedAtUtc: '2026-09-02T00:00:00Z',
  };

  const emailContact: CustomerContact = {
    id: 'contact-1',
    customerId: 'customer-a',
    type: 'Email',
    value: 'sara@example.test',
    label: null,
    isPrimary: true,
    isActive: true,
    createdAtUtc: '2026-09-02T00:00:00Z',
    updatedAtUtc: '2026-09-02T00:00:00Z',
  };

  it('renders customer fields on successful load', async () => {
    configure('customer-a');
    customersService.get.and.resolveTo(customer);
    const fixture = TestBed.createComponent(CustomerDetail);

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(customersService.get).toHaveBeenCalledWith('customer-a');
    expect(fixture.nativeElement.textContent).toContain('Sara Ahmed');
    expect(fixture.nativeElement.textContent).toContain('CUS-AAA111');
  });

  it('shows a not-found message on a 404 response', async () => {
    configure('missing-id');
    customersService.get.and.rejectWith(new HttpErrorResponse({ status: 404 }));
    const fixture = TestBed.createComponent(CustomerDetail);

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.componentInstance.notFound()).toBeTrue();
  });

  it('shows an empty state when the customer has no contacts', async () => {
    configure('customer-a');
    customersService.get.and.resolveTo(customer);
    const fixture = TestBed.createComponent(CustomerDetail);

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No contacts yet.');
  });

  it('renders existing contacts and hides mutation actions without permission', async () => {
    configure('customer-a');
    customersService.get.and.resolveTo(customer);
    customersService.listContacts.and.resolveTo([emailContact]);
    const fixture = TestBed.createComponent(CustomerDetail);

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('sara@example.test');
    expect(fixture.nativeElement.textContent).toContain('Primary');
    expect(fixture.nativeElement.querySelector('p-button[label="Add contact"]')).toBeNull();
  });

  it('adds a contact through the form when permitted', async () => {
    configure('customer-a');
    authorization.set(['customers.manage']);
    customersService.get.and.resolveTo(customer);
    customersService.listContacts.and.resolveTo([]);
    customersService.addContact.and.resolveTo(emailContact);
    const fixture = TestBed.createComponent(CustomerDetail);
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance;
    component.startAddContact();
    component.contactForm.setValue({
      type: 'Email',
      value: 'sara@example.test',
      label: null,
      isPrimary: true,
    });
    await component.submitContact();

    expect(customersService.addContact).toHaveBeenCalledWith('customer-a', {
      type: 'Email',
      value: 'sara@example.test',
      label: null,
      isPrimary: true,
    });
    expect(component.showContactForm()).toBeFalse();
  });

  it('requires selecting a new primary before deactivating the only primary with others active', async () => {
    configure('customer-a');
    authorization.set(['customers.manage']);
    const secondary: CustomerContact = { ...emailContact, id: 'contact-2', isPrimary: false };
    customersService.get.and.resolveTo(customer);
    customersService.listContacts.and.resolveTo([emailContact, secondary]);
    const fixture = TestBed.createComponent(CustomerDetail);
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance;
    component.requestDeactivateContact(emailContact);

    expect(component.deactivatingContactId()).toBe('contact-1');
    expect(customersService.deactivateContact).not.toHaveBeenCalled();
  });

  it('hides the edit action without permission', async () => {
    configure('customer-a');
    customersService.get.and.resolveTo(customer);
    const fixture = TestBed.createComponent(CustomerDetail);

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('p-button[label="Edit"]')).toBeNull();
  });

  it('submits an edit with the current version and reloads the customer', async () => {
    configure('customer-a');
    authorization.set(['customers.manage']);
    customersService.get.and.resolveTo(customer);
    const updated = { ...customer, firstName: 'Sara2', status: 'Inactive' as const, version: 2 };
    customersService.update.and.resolveTo(updated);
    const fixture = TestBed.createComponent(CustomerDetail);
    fixture.detectChanges();
    await fixture.whenStable();

    const component = fixture.componentInstance;
    component.startEdit();
    component.editForm.patchValue({ firstName: 'Sara2', status: 'Inactive' });
    await component.submitEdit();

    expect(customersService.update).toHaveBeenCalledWith('customer-a', {
      firstName: 'Sara2',
      lastName: 'Ahmed',
      preferredLanguage: 'Arabic',
      departmentId: null,
      branchId: null,
      status: 'Inactive',
      version: 1,
    });
    expect(component.editing()).toBeFalse();
    expect(component.customer()).toEqual(updated);
  });

  it('reloads the customer and reports a conflict on a stale-version update', async () => {
    configure('customer-a');
    authorization.set(['customers.manage']);
    customersService.get.and.resolveTo(customer);
    const conflictError = new HttpErrorResponse({ status: 409, error: { code: 'customers.update_conflict' } });
    customersService.update.and.rejectWith(conflictError);
    const fixture = TestBed.createComponent(CustomerDetail);
    fixture.detectChanges();
    await fixture.whenStable();

    const refreshed = { ...customer, version: 2 };
    customersService.get.and.resolveTo(refreshed);
    const component = fixture.componentInstance;
    component.startEdit();
    await component.submitEdit();

    expect(component.editErrorKey()).toBe('customers.errors.updateConflict');
    expect(component.customer()).toEqual(refreshed);
  });
});
