import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { TicketCreateForm } from './ticket-create-form';
import { TicketCreateService } from './ticket-create.service';
import { CustomersService } from '../customers/customers.service';
import { TicketCategoriesService } from '../ticket-categories/ticket-categories.service';
import { TicketPrioritiesService } from '../ticket-priorities/ticket-priorities.service';
import { DepartmentsService } from '../departments/departments.service';
import { BranchesService } from '../branches/branches.service';
import { provideTranslations } from '@squad-crm/platform';
import { COMMON_TRANSLATIONS } from '@squad-crm/shared-ui';
import { TICKET_CREATE_TRANSLATIONS } from './ticket-create-translations';

describe('TicketCreateForm', () => {
  let ticketCreateService: jasmine.SpyObj<TicketCreateService>;
  let customersService: jasmine.SpyObj<CustomersService>;
  let ticketCategoriesService: jasmine.SpyObj<TicketCategoriesService>;
  let ticketPrioritiesService: jasmine.SpyObj<TicketPrioritiesService>;
  let departmentsService: jasmine.SpyObj<DepartmentsService>;
  let branchesService: jasmine.SpyObj<BranchesService>;
  let router: jasmine.SpyObj<Router>;

  const customer = {
    id: 'customer-1',
    customerNumber: 'CUS-0001',
    firstName: 'Sara',
    lastName: 'Ahmed',
    preferredLanguage: null,
    departmentId: null,
    branchId: null,
    status: 'Active' as const,
    version: 0,
    createdAtUtc: '2026-08-29T00:00:00Z',
    updatedAtUtc: '2026-08-29T00:00:00Z',
  };

  function configure(): void {
    ticketCreateService = jasmine.createSpyObj<TicketCreateService>('TicketCreateService', [
      'create',
    ]);
    customersService = jasmine.createSpyObj<CustomersService>('CustomersService', ['list']);
    customersService.list.and.resolveTo({
      items: [customer],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    });
    ticketCategoriesService = jasmine.createSpyObj<TicketCategoriesService>(
      'TicketCategoriesService',
      ['list'],
    );
    ticketCategoriesService.list.and.resolveTo({
      items: [
        {
          id: 'category-1',
          code: 'BILLING',
          arabicName: 'الفواتير',
          englishName: 'Billing',
          defaultDepartmentId: null,
          sortOrder: 0,
          isActive: true,
          createdAtUtc: '2026-08-29T00:00:00Z',
          updatedAtUtc: '2026-08-29T00:00:00Z',
        },
      ],
      page: 1,
      pageSize: 200,
      totalCount: 1,
    });
    ticketPrioritiesService = jasmine.createSpyObj<TicketPrioritiesService>(
      'TicketPrioritiesService',
      ['list'],
    );
    ticketPrioritiesService.list.and.resolveTo({
      items: [
        {
          id: 'priority-1',
          code: 'URGENT',
          arabicName: 'عاجل',
          englishName: 'Urgent',
          rank: 1,
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
    departmentsService = jasmine.createSpyObj<DepartmentsService>('DepartmentsService', ['list']);
    departmentsService.list.and.resolveTo({
      items: [
        {
          id: 'department-1',
          code: 'SUPPORT',
          arabicName: 'الدعم',
          englishName: 'Support',
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
    branchesService = jasmine.createSpyObj<BranchesService>('BranchesService', ['list']);
    branchesService.list.and.resolveTo({
      items: [
        {
          id: 'branch-1',
          code: 'MAIN',
          arabicName: 'الرئيسي',
          englishName: 'Main',
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
    router = jasmine.createSpyObj<Router>('Router', ['navigateByUrl']);

    TestBed.configureTestingModule({
      providers: [
        provideTranslations(COMMON_TRANSLATIONS),
        provideTranslations(TICKET_CREATE_TRANSLATIONS),
        { provide: TicketCreateService, useValue: ticketCreateService },
        { provide: CustomersService, useValue: customersService },
        { provide: TicketCategoriesService, useValue: ticketCategoriesService },
        { provide: TicketPrioritiesService, useValue: ticketPrioritiesService },
        { provide: DepartmentsService, useValue: departmentsService },
        { provide: BranchesService, useValue: branchesService },
        { provide: Router, useValue: router },
      ],
    });
  }

  it('blocks submit when required fields are missing', async () => {
    configure();
    const fixture = TestBed.createComponent(TicketCreateForm);
    fixture.detectChanges();

    await fixture.componentInstance.submit();

    expect(ticketCreateService.create).not.toHaveBeenCalled();
    expect(fixture.componentInstance.form.controls.customer.touched).toBeTrue();
  });

  it('submits the create request and navigates home on success', async () => {
    configure();
    ticketCreateService.create.and.resolveTo({
      id: 'ticket-1',
      ticketNumber: 'TKT-ABC12345',
      customerId: customer.id,
      subject: 'Cannot log in',
      description: 'The customer cannot log in.',
      categoryId: 'category-1',
      subcategoryId: null,
      priorityId: 'priority-1',
      departmentId: 'department-1',
      branchId: 'branch-1',
      status: 'Open',
      channel: 'Agent',
      assignedAgentId: null,
      escalationLevel: 0,
      escalationTargetType: null,
      escalationTargetId: null,
      escalatedAtUtc: null,
      createdAtUtc: '2026-08-29T00:00:00Z',
    });
    const fixture = TestBed.createComponent(TicketCreateForm);
    fixture.detectChanges();
    await fixture.whenStable();

    fixture.componentInstance.form.setValue({
      customer,
      subject: 'Cannot log in',
      description: 'The customer cannot log in.',
      categoryId: 'category-1',
      priorityId: 'priority-1',
      departmentId: 'department-1',
      branchId: 'branch-1',
      channel: 'Agent',
    });

    await fixture.componentInstance.submit();

    expect(ticketCreateService.create).toHaveBeenCalledWith({
      customerId: customer.id,
      subject: 'Cannot log in',
      description: 'The customer cannot log in.',
      categoryId: 'category-1',
      subcategoryId: null,
      priorityId: 'priority-1',
      departmentId: 'department-1',
      branchId: 'branch-1',
      channel: 'Agent',
      assignedAgentId: null,
    });
    expect(router.navigateByUrl).toHaveBeenCalledWith('/');
  });

  it('surfaces an inactive-category error from a mocked 422 response', async () => {
    configure();
    ticketCreateService.create.and.rejectWith(
      new HttpErrorResponse({ status: 422, error: { code: 'tickets.inactive_category' } }),
    );
    const fixture = TestBed.createComponent(TicketCreateForm);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.componentInstance.form.setValue({
      customer,
      subject: 'Cannot log in',
      description: 'The customer cannot log in.',
      categoryId: 'category-1',
      priorityId: 'priority-1',
      departmentId: 'department-1',
      branchId: 'branch-1',
      channel: 'Agent',
    });

    await fixture.componentInstance.submit();

    expect(fixture.componentInstance.errorKey()).toBe('ticketCreate.errors.inactiveCategory');
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });

  it('populates customer suggestions from the customers search endpoint', async () => {
    configure();
    const fixture = TestBed.createComponent(TicketCreateForm);
    fixture.detectChanges();

    await fixture.componentInstance.searchCustomers({ query: 'sara' } as never);

    expect(customersService.list).toHaveBeenCalledWith({ search: 'sara' }, 1, 20);
    expect(fixture.componentInstance.customerSuggestions()).toEqual([customer]);
  });
});
