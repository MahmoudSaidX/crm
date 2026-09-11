import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { TaskCreateForm } from './task-create-form';
import { TaskCreateService } from './task-create.service';
import { CustomersService } from '../customers/customers.service';
import { TicketsService } from '../tickets/tickets.service';
import { provideTranslations } from '@squad-crm/platform';
import { COMMON_TRANSLATIONS } from '@squad-crm/shared-ui';
import { TASK_CREATE_TRANSLATIONS } from './task-create-translations';

describe('TaskCreateForm', () => {
  let taskCreateService: jasmine.SpyObj<TaskCreateService>;
  let customersService: jasmine.SpyObj<CustomersService>;
  let ticketsService: jasmine.SpyObj<TicketsService>;
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

  const ticket = {
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
    status: 'Open' as const,
    channel: 'Agent' as const,
    assignedAgentId: null,
    escalationLevel: 0,
    escalationTargetType: null,
    escalationTargetId: null,
    escalatedAtUtc: null,
    createdAtUtc: '2026-08-29T00:00:00Z',
    updatedAtUtc: null,
  };

  function configure(): void {
    taskCreateService = jasmine.createSpyObj<TaskCreateService>('TaskCreateService', ['create']);
    customersService = jasmine.createSpyObj<CustomersService>('CustomersService', ['list']);
    customersService.list.and.resolveTo({
      items: [customer],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    });
    ticketsService = jasmine.createSpyObj<TicketsService>('TicketsService', ['list']);
    ticketsService.list.and.resolveTo({
      items: [ticket],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    });
    router = jasmine.createSpyObj<Router>('Router', ['navigateByUrl']);

    TestBed.configureTestingModule({
      providers: [
        provideTranslations(COMMON_TRANSLATIONS),
        provideTranslations(TASK_CREATE_TRANSLATIONS),
        { provide: TaskCreateService, useValue: taskCreateService },
        { provide: CustomersService, useValue: customersService },
        { provide: TicketsService, useValue: ticketsService },
        { provide: Router, useValue: router },
      ],
    });
  }

  it('blocks submit when required fields are missing', async () => {
    configure();
    const fixture = TestBed.createComponent(TaskCreateForm);
    fixture.detectChanges();

    await fixture.componentInstance.submit();

    expect(taskCreateService.create).not.toHaveBeenCalled();
    expect(fixture.componentInstance.form.controls.title.touched).toBeTrue();
  });

  it('submits the create request with no link and navigates to my tasks on success', async () => {
    configure();
    taskCreateService.create.and.resolveTo({
      id: 'task-1',
      title: 'Call customer back',
      details: null,
      ownerUserId: 'agent-1',
      ticketId: null,
      customerId: null,
      dueAtUtc: null,
      status: 'Open',
      completedAtUtc: null,
      createdAtUtc: '2026-09-11T00:00:00Z',
      updatedAtUtc: null,
      version: 1,
    });
    const fixture = TestBed.createComponent(TaskCreateForm);
    fixture.detectChanges();
    await fixture.whenStable();

    fixture.componentInstance.form.setValue({
      title: 'Call customer back',
      details: '',
      ticket: null,
      customer: null,
      dueAtUtc: null,
    });

    await fixture.componentInstance.submit();

    expect(taskCreateService.create).toHaveBeenCalledWith({
      title: 'Call customer back',
      details: null,
      ownerUserId: null,
      ticketId: null,
      customerId: null,
      dueAtUtc: null,
    });
    expect(router.navigateByUrl).toHaveBeenCalledWith('/my-tasks');
  });

  it('links the selected ticket and customer when both are chosen', async () => {
    configure();
    taskCreateService.create.and.resolveTo({
      id: 'task-1',
      title: 'Follow up',
      details: null,
      ownerUserId: 'agent-1',
      ticketId: ticket.id,
      customerId: customer.id,
      dueAtUtc: null,
      status: 'Open',
      completedAtUtc: null,
      createdAtUtc: '2026-09-11T00:00:00Z',
      updatedAtUtc: null,
      version: 1,
    });
    const fixture = TestBed.createComponent(TaskCreateForm);
    fixture.detectChanges();
    await fixture.whenStable();

    fixture.componentInstance.form.setValue({
      title: 'Follow up',
      details: '',
      ticket,
      customer,
      dueAtUtc: null,
    });

    await fixture.componentInstance.submit();

    expect(taskCreateService.create).toHaveBeenCalledWith({
      title: 'Follow up',
      details: null,
      ownerUserId: null,
      ticketId: ticket.id,
      customerId: customer.id,
      dueAtUtc: null,
    });
  });

  it('surfaces an invalid-ticket error from a mocked 422 response', async () => {
    configure();
    taskCreateService.create.and.rejectWith(
      new HttpErrorResponse({ status: 422, error: { code: 'tasks.invalid_ticket' } }),
    );
    const fixture = TestBed.createComponent(TaskCreateForm);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.componentInstance.form.setValue({
      title: 'Follow up',
      details: '',
      ticket,
      customer: null,
      dueAtUtc: null,
    });

    await fixture.componentInstance.submit();

    expect(fixture.componentInstance.errorKey()).toBe('taskCreate.errors.invalidTicket');
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });

  it('populates customer and ticket suggestions from their search endpoints', async () => {
    configure();
    const fixture = TestBed.createComponent(TaskCreateForm);
    fixture.detectChanges();

    await fixture.componentInstance.searchCustomers({ query: 'sara' } as never);
    await fixture.componentInstance.searchTickets({ query: 'abc' } as never);

    expect(customersService.list).toHaveBeenCalledWith({ search: 'sara' }, 1, 20);
    expect(fixture.componentInstance.customerSuggestions()).toEqual([customer]);
    expect(ticketsService.list).toHaveBeenCalledWith({ search: 'abc' }, 1, 20);
    expect(fixture.componentInstance.ticketSuggestions()).toEqual([ticket]);
  });
});
