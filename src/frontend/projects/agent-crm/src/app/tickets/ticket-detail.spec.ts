import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { TicketDetail } from './ticket-detail';
import { TicketDetail as TicketDetailModel, TicketsService } from './tickets.service';
import { CustomersService } from '../customers/customers.service';
import { DepartmentsService } from '../departments/departments.service';
import { BranchesService } from '../branches/branches.service';
import { StaffUsersService } from '../staff-users/staff-users.service';
import { AuthorizationState } from '../auth/authorization.state';
import {
  AppConfigStore,
  LocaleService,
  provideAppConfig,
  provideTranslations,
  validateAppConfig,
} from '@squad-crm/platform';
import { COMMON_TRANSLATIONS } from '@squad-crm/shared-ui';
import { TICKET_TRANSLATIONS } from './ticket-translations';

describe('TicketDetail', () => {
  const ticket: TicketDetailModel = {
    id: 'ticket-1',
    ticketNumber: 'TKT-000001',
    customerId: 'customer-1',
    subject: 'Cannot log in',
    description: 'The customer cannot log in to the portal.',
    categoryId: 'category-1',
    categoryArabicName: 'فئة',
    categoryEnglishName: 'Category',
    categoryIsActive: true,
    subcategoryId: null,
    priorityId: 'priority-1',
    priorityArabicName: 'أولوية',
    priorityEnglishName: 'Priority',
    priorityIsActive: true,
    priorityRank: 1,
    departmentId: 'department-1',
    branchId: 'branch-1',
    status: 'Open',
    channel: 'Agent',
    assignedAgentId: null,
    escalationLevel: 0,
    escalationTargetType: null,
    escalationTargetId: null,
    escalatedAtUtc: null,
    createdAtUtc: '2026-09-11T00:00:00Z',
    updatedAtUtc: null,
    version: 1,
    allowedStatusTransitions: ['InProgress', 'PendingCustomer', 'PendingInternal', 'Resolved'],
  };

  const historyEntry = {
    eventId: 'event-1',
    eventType: 'TicketCreated',
    occurredAtUtc: '2026-09-11T00:00:00Z',
    sequence: 1,
    actorType: 'User' as const,
    actorId: null,
    summary: 'Ticket TKT-000001 created with status Open via Agent',
    reason: null,
    visibility: 'Customer' as const,
  };

  const emptyHistoryPage = { items: [], page: 1, pageSize: 10, totalCount: 0 };

  function configure(options: {
    get?: jasmine.Spy;
    customerGet?: jasmine.Spy;
    customerListContacts?: jasmine.Spy;
    customerGetTimeline?: jasmine.Spy;
    departmentGet?: jasmine.Spy;
    branchGet?: jasmine.Spy;
    assign?: jasmine.Spy;
    changeStatus?: jasmine.Spy;
    escalate?: jasmine.Spy;
    history?: jasmine.Spy;
    staffList?: jasmine.Spy;
    departmentList?: jasmine.Spy;
    permissions?: readonly string[];
  }): void {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        // PrimeNG's p-message relies on Angular animations; the real app
        // provides them in its bootstrap config, so the test does the same.
        provideNoopAnimations(),
        provideAppConfig(),
        provideTranslations(COMMON_TRANSLATIONS),
        provideTranslations(TICKET_TRANSLATIONS),
        {
          provide: TicketsService,
          useValue: {
            get: options.get ?? jasmine.createSpy().and.resolveTo(ticket),
            assign: options.assign ?? jasmine.createSpy().and.resolveTo({}),
            changeStatus: options.changeStatus ?? jasmine.createSpy().and.resolveTo({}),
            escalate: options.escalate ?? jasmine.createSpy().and.resolveTo({}),
            history: options.history ?? jasmine.createSpy().and.resolveTo(emptyHistoryPage),
          },
        },
        {
          provide: CustomersService,
          useValue: {
            get:
              options.customerGet ??
              jasmine.createSpy().and.resolveTo({
                id: 'customer-1',
                customerNumber: 'CUST-000001',
                firstName: 'Sara',
                lastName: 'Ali',
                preferredLanguage: null,
                departmentId: null,
                branchId: null,
                status: 'Active',
                version: 1,
                createdAtUtc: '2026-09-01T00:00:00Z',
                updatedAtUtc: '2026-09-01T00:00:00Z',
              }),
            listContacts: options.customerListContacts ?? jasmine.createSpy().and.resolveTo([]),
            getTimeline: options.customerGetTimeline ?? jasmine.createSpy().and.resolveTo([]),
          },
        },
        {
          provide: DepartmentsService,
          useValue: {
            get:
              options.departmentGet ??
              jasmine.createSpy().and.resolveTo({ arabicName: 'الدعم', englishName: 'Support' }),
            list:
              options.departmentList ??
              jasmine.createSpy().and.resolveTo({
                items: [
                  {
                    id: 'department-1',
                    arabicName: 'الدعم',
                    englishName: 'Support',
                    isActive: true,
                  },
                  {
                    id: 'department-2',
                    arabicName: 'مغلق',
                    englishName: 'Closed team',
                    isActive: false,
                  },
                ],
                page: 1,
                pageSize: 100,
                totalCount: 2,
              }),
          },
        },
        {
          provide: BranchesService,
          useValue: {
            get:
              options.branchGet ??
              jasmine.createSpy().and.resolveTo({ arabicName: 'الرياض', englishName: 'Riyadh' }),
          },
        },
        {
          provide: StaffUsersService,
          useValue: {
            list:
              options.staffList ??
              jasmine.createSpy().and.resolveTo({
                items: [
                  {
                    id: 'agent-1',
                    email: 'agent@example.test',
                    displayName: 'Agent',
                    isActive: true,
                  },
                  {
                    id: 'agent-2',
                    email: 'left@example.test',
                    displayName: 'Left',
                    isActive: false,
                  },
                ],
                page: 1,
                pageSize: 100,
                totalCount: 2,
              }),
          },
        },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: 'ticket-1' }) } },
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
    TestBed.inject(LocaleService).initialize();
    TestBed.inject(AuthorizationState).set(options.permissions ?? []);
  }

  async function createComponent() {
    const fixture = TestBed.createComponent(TicketDetail);
    for (let tick = 0; tick < 6; tick += 1) {
      await Promise.resolve();
    }
    fixture.detectChanges();
    return fixture;
  }

  afterEach(() => {
    TestBed.inject(AuthorizationState).clear();
    localStorage.removeItem('sc.locale');
    document.documentElement.setAttribute('lang', 'en');
    document.documentElement.setAttribute('dir', 'ltr');
  });

  it('loads the ticket and resolves reference labels from their owning modules', async () => {
    configure({});
    const fixture = await createComponent();

    expect(fixture.componentInstance.ticket()).toEqual(ticket);
    expect(fixture.componentInstance.notFound()).toBeFalse();
    expect(fixture.componentInstance.customerName()).toBe('Sara Ali');
    expect(fixture.componentInstance.departmentName()).toBe('Support');
    expect(fixture.componentInstance.branchName()).toBe('Riyadh');
    expect(fixture.componentInstance.categoryName()).toBe('Category');
    expect(fixture.componentInstance.priorityName()).toBe('Priority');
  });

  it('sets notFound when the ticket cannot be loaded', async () => {
    configure({ get: jasmine.createSpy().and.rejectWith(new Error('not found')) });
    const fixture = await createComponent();

    expect(fixture.componentInstance.notFound()).toBeTrue();
    expect(fixture.componentInstance.ticket()).toBeNull();
  });

  it('leaves a reference label unresolved when its owning module denies the read', async () => {
    configure({ customerGet: jasmine.createSpy().and.rejectWith(new Error('forbidden')) });
    const fixture = await createComponent();

    // The template falls back to the raw id, and the rest of the ticket still
    // renders — a missing permission must not blank out the whole screen.
    expect(fixture.componentInstance.customerName()).toBeNull();
    expect(fixture.componentInstance.ticket()).toEqual(ticket);
    expect(fixture.componentInstance.departmentName()).toBe('Support');
  });

  it('hides the assignment action without the tickets.assign permission', async () => {
    configure({});
    const fixture = await createComponent();

    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Assign to agent');
  });

  it('offers only active agents once the assignment form is opened', async () => {
    configure({ permissions: ['tickets.assign'] });
    const fixture = await createComponent();

    await fixture.componentInstance.startAssignment();

    expect(fixture.componentInstance.agentOptions()).toEqual([
      { label: 'Agent', value: 'agent-1' },
    ]);
    expect(fixture.componentInstance.agentsUnavailable()).toBeFalse();
  });

  it('reports that agents could not be loaded when the staff lookup is denied', async () => {
    configure({
      permissions: ['tickets.assign'],
      staffList: jasmine.createSpy().and.rejectWith(new Error('forbidden')),
    });
    const fixture = await createComponent();

    await fixture.componentInstance.startAssignment();

    expect(fixture.componentInstance.agentOptions()).toEqual([]);
    expect(fixture.componentInstance.agentsUnavailable()).toBeTrue();
  });

  it('submits the selected agent with the version it last read', async () => {
    const assign = jasmine.createSpy().and.resolveTo({});
    configure({ permissions: ['tickets.assign'], assign });
    const fixture = await createComponent();

    await fixture.componentInstance.startAssignment();
    fixture.componentInstance.assignForm.setValue({ targetAgentId: 'agent-1', reason: 'Owner' });
    await fixture.componentInstance.submitAssignment();

    expect(assign).toHaveBeenCalledWith('ticket-1', {
      targetAgentId: 'agent-1',
      reason: 'Owner',
      version: 1,
    });
    expect(fixture.componentInstance.assigning()).toBeFalse();
  });

  it('requires a reason when the ticket already has an owner', async () => {
    const assign = jasmine.createSpy().and.resolveTo({});
    configure({
      permissions: ['tickets.assign'],
      assign,
      get: jasmine.createSpy().and.resolveTo({ ...ticket, assignedAgentId: 'agent-2' }),
    });
    const fixture = await createComponent();

    await fixture.componentInstance.startAssignment();
    fixture.componentInstance.assignForm.setValue({ targetAgentId: 'agent-1', reason: '   ' });
    await fixture.componentInstance.submitAssignment();

    expect(assign).not.toHaveBeenCalled();
    expect(fixture.componentInstance.assignmentErrorKey()).toBe('tickets.assign.validation.reason');
  });

  it('surfaces the stale-version message when the ticket changed meanwhile', async () => {
    configure({
      permissions: ['tickets.assign'],
      assign: jasmine
        .createSpy()
        .and.rejectWith(new HttpErrorResponse({ status: 409, statusText: 'Conflict' })),
    });
    const fixture = await createComponent();

    await fixture.componentInstance.startAssignment();
    fixture.componentInstance.assignForm.setValue({ targetAgentId: 'agent-1', reason: '' });
    await fixture.componentInstance.submitAssignment();

    expect(fixture.componentInstance.assignmentErrorKey()).toBe(
      'tickets.assign.errors.staleVersion',
    );
    expect(fixture.componentInstance.assigning()).toBeTrue();
  });

  it('hides the status action without the tickets.changestatus permission', async () => {
    configure({});
    const fixture = await createComponent();

    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Change status');
  });

  it('offers only the transitions the backend reported as allowed', async () => {
    configure({
      permissions: ['tickets.changestatus'],
      get: jasmine
        .createSpy()
        .and.resolveTo({ ...ticket, status: 'Resolved', allowedStatusTransitions: ['Closed'] }),
    });
    const fixture = await createComponent();

    fixture.componentInstance.startStatusChange();

    expect(fixture.componentInstance.statusOptions()).toEqual([
      { label: 'Closed', value: 'Closed' },
    ]);
  });

  it('submits the selected status with the version it last read', async () => {
    const changeStatus = jasmine.createSpy().and.resolveTo({});
    configure({ permissions: ['tickets.changestatus'], changeStatus });
    const fixture = await createComponent();

    fixture.componentInstance.startStatusChange();
    fixture.componentInstance.statusForm.setValue({ targetStatus: 'InProgress', reason: '' });
    fixture.componentInstance.onStatusSelected('InProgress');
    await fixture.componentInstance.submitStatusChange();

    expect(changeStatus).toHaveBeenCalledWith('ticket-1', {
      targetStatus: 'InProgress',
      reason: null,
      version: 1,
    });
    expect(fixture.componentInstance.changingStatus()).toBeFalse();
  });

  it('requires a reason before closing a ticket', async () => {
    const changeStatus = jasmine.createSpy().and.resolveTo({});
    configure({
      permissions: ['tickets.changestatus'],
      changeStatus,
      get: jasmine
        .createSpy()
        .and.resolveTo({ ...ticket, status: 'Resolved', allowedStatusTransitions: ['Closed'] }),
    });
    const fixture = await createComponent();

    fixture.componentInstance.startStatusChange();
    fixture.componentInstance.statusForm.setValue({ targetStatus: 'Closed', reason: '   ' });
    fixture.componentInstance.onStatusSelected('Closed');
    await fixture.componentInstance.submitStatusChange();

    expect(changeStatus).not.toHaveBeenCalled();
    expect(fixture.componentInstance.statusErrorKey()).toBe('tickets.status.validation.reason');
  });

  it('surfaces the stale-version message when the status changed meanwhile', async () => {
    configure({
      permissions: ['tickets.changestatus'],
      changeStatus: jasmine
        .createSpy()
        .and.rejectWith(new HttpErrorResponse({ status: 409, statusText: 'Conflict' })),
    });
    const fixture = await createComponent();

    fixture.componentInstance.startStatusChange();
    fixture.componentInstance.statusForm.setValue({ targetStatus: 'InProgress', reason: '' });
    fixture.componentInstance.onStatusSelected('InProgress');
    await fixture.componentInstance.submitStatusChange();

    expect(fixture.componentInstance.statusErrorKey()).toBe('tickets.status.errors.staleVersion');
    expect(fixture.componentInstance.changingStatus()).toBeTrue();
  });

  it('hides the escalation action without the tickets.escalate permission', async () => {
    configure({});
    const fixture = await createComponent();

    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Escalate ticket');
  });

  it('offers only active agents as escalation targets by default', async () => {
    configure({ permissions: ['tickets.escalate'] });
    const fixture = await createComponent();

    await fixture.componentInstance.startEscalation();

    expect(fixture.componentInstance.escalationTargetOptions()).toEqual([
      { label: 'Agent', value: 'agent-1' },
    ]);
    expect(fixture.componentInstance.escalationTargetsUnavailable()).toBeFalse();
  });

  it('reloads active departments when the escalation target kind changes', async () => {
    configure({ permissions: ['tickets.escalate'] });
    const fixture = await createComponent();

    await fixture.componentInstance.startEscalation();
    await fixture.componentInstance.onEscalationTargetTypeSelected('Department');

    expect(fixture.componentInstance.escalationTargetOptions()).toEqual([
      { label: 'Support', value: 'department-1' },
    ]);
    expect(fixture.componentInstance.escalationForm.controls.targetId.value).toBe('');
  });

  it('submits the escalation target and reason without a level', async () => {
    const escalate = jasmine.createSpy().and.resolveTo({});
    configure({ permissions: ['tickets.escalate'], escalate });
    const fixture = await createComponent();

    await fixture.componentInstance.startEscalation();
    fixture.componentInstance.escalationForm.setValue({
      targetType: 'Agent',
      targetId: 'agent-1',
      reason: 'Needs a senior agent.',
    });
    await fixture.componentInstance.submitEscalation();

    expect(escalate).toHaveBeenCalledWith('ticket-1', {
      targetType: 'Agent',
      targetId: 'agent-1',
      reason: 'Needs a senior agent.',
      version: 1,
    });
    expect(fixture.componentInstance.escalating()).toBeFalse();
  });

  it('requires a reason before escalating', async () => {
    const escalate = jasmine.createSpy().and.resolveTo({});
    configure({ permissions: ['tickets.escalate'], escalate });
    const fixture = await createComponent();

    await fixture.componentInstance.startEscalation();
    fixture.componentInstance.escalationForm.setValue({
      targetType: 'Agent',
      targetId: 'agent-1',
      reason: '   ',
    });
    await fixture.componentInstance.submitEscalation();

    expect(escalate).not.toHaveBeenCalled();
    expect(fixture.componentInstance.escalationErrorKey()).toBe(
      'tickets.escalation.validation.reason',
    );
  });

  it('surfaces the stale-version message when the ticket changed before the escalation', async () => {
    configure({
      permissions: ['tickets.escalate'],
      escalate: jasmine
        .createSpy()
        .and.rejectWith(new HttpErrorResponse({ status: 409, statusText: 'Conflict' })),
    });
    const fixture = await createComponent();

    await fixture.componentInstance.startEscalation();
    fixture.componentInstance.escalationForm.setValue({
      targetType: 'Agent',
      targetId: 'agent-1',
      reason: 'Needs a senior agent.',
    });
    await fixture.componentInstance.submitEscalation();

    expect(fixture.componentInstance.escalationErrorKey()).toBe(
      'tickets.escalation.errors.staleVersion',
    );
    expect(fixture.componentInstance.escalating()).toBeTrue();
  });

  it('surfaces the not-escalatable message when the backend rejects a finished ticket', async () => {
    configure({
      permissions: ['tickets.escalate'],
      escalate: jasmine.createSpy().and.rejectWith(
        new HttpErrorResponse({
          status: 422,
          statusText: 'Unprocessable',
          error: { code: 'tickets.not_escalatable' },
        }),
      ),
    });
    const fixture = await createComponent();

    await fixture.componentInstance.startEscalation();
    fixture.componentInstance.escalationForm.setValue({
      targetType: 'Agent',
      targetId: 'agent-1',
      reason: 'Too late.',
    });
    await fixture.componentInstance.submitEscalation();

    expect(fixture.componentInstance.escalationErrorKey()).toBe(
      'tickets.escalation.errors.notEscalatable',
    );
  });

  it('surfaces the invalid-transition message when the backend rejects the target status', async () => {
    configure({
      permissions: ['tickets.changestatus'],
      changeStatus: jasmine
        .createSpy()
        .and.rejectWith(new HttpErrorResponse({ status: 422, statusText: 'Unprocessable' })),
    });
    const fixture = await createComponent();

    fixture.componentInstance.startStatusChange();
    fixture.componentInstance.statusForm.setValue({ targetStatus: 'InProgress', reason: '' });
    fixture.componentInstance.onStatusSelected('InProgress');
    await fixture.componentInstance.submitStatusChange();

    expect(fixture.componentInstance.statusErrorKey()).toBe(
      'tickets.status.errors.invalidTransition',
    );
  });

  it('loads the ticket history timeline on load', async () => {
    const history = jasmine.createSpy().and.resolveTo({
      items: [historyEntry],
      page: 1,
      pageSize: 10,
      totalCount: 1,
    });
    configure({ history });
    const fixture = await createComponent();

    expect(history).toHaveBeenCalledWith('ticket-1', 1, 10);
    expect(fixture.componentInstance.history().length).toBe(1);
    expect(fixture.componentInstance.historyUnavailable()).toBeFalse();
    expect(fixture.nativeElement.textContent).toContain('Ticket created');
    expect(fixture.nativeElement.textContent).toContain(historyEntry.summary);
  });

  it('requests the next history page without reloading the ticket', async () => {
    const history = jasmine.createSpy().and.resolveTo({
      items: [historyEntry],
      page: 1,
      pageSize: 10,
      totalCount: 25,
    });
    const get = jasmine.createSpy().and.resolveTo(ticket);
    configure({ history, get });
    const fixture = await createComponent();

    await fixture.componentInstance.onHistoryPageChange(10);

    expect(history).toHaveBeenCalledWith('ticket-1', 2, 10);
    expect(get).toHaveBeenCalledTimes(1);
  });

  it('keeps rendering the ticket when the history cannot be loaded', async () => {
    const history = jasmine.createSpy().and.rejectWith(new Error('boom'));
    configure({ history });
    const fixture = await createComponent();

    expect(fixture.componentInstance.ticket()).toEqual(ticket);
    expect(fixture.componentInstance.historyUnavailable()).toBeTrue();
    expect(fixture.nativeElement.textContent).toContain('The ticket history could not be loaded.');
  });

  it('renders the customer context panel with number, name, status, contacts and recent activity', async () => {
    configure({
      customerListContacts: jasmine.createSpy().and.resolveTo([
        {
          id: 'contact-1',
          customerId: 'customer-1',
          type: 'Email',
          value: 'sara@example.test',
          label: null,
          isPrimary: true,
          isActive: true,
          createdAtUtc: '2026-09-01T00:00:00Z',
          updatedAtUtc: '2026-09-01T00:00:00Z',
        },
        {
          id: 'contact-2',
          customerId: 'customer-1',
          type: 'Phone',
          value: '+966500000000',
          label: null,
          isPrimary: true,
          isActive: true,
          createdAtUtc: '2026-09-01T00:00:00Z',
          updatedAtUtc: '2026-09-01T00:00:00Z',
        },
      ]),
      customerGetTimeline: jasmine.createSpy().and.resolveTo([
        {
          eventType: 'TicketCreated',
          occurredAtUtc: '2026-09-10T00:00:00Z',
          actorDisplay: null,
          relatedEntityType: 'Ticket',
          relatedEntityId: 'ticket-1',
          summary: 'Ticket TKT-000001 created',
          visibility: 'Internal',
        },
      ]),
    });
    const fixture = await createComponent();

    expect(fixture.componentInstance.customer()?.customerNumber).toBe('CUST-000001');
    expect(fixture.componentInstance.customerPrimaryEmail()).toBe('sara@example.test');
    expect(fixture.componentInstance.customerPrimaryPhone()).toBe('+966500000000');
    expect(fixture.componentInstance.customerRecentActivity().length).toBe(1);
    expect(fixture.nativeElement.textContent).toContain('CUST-000001');
    expect(fixture.nativeElement.textContent).toContain('sara@example.test');
    expect(fixture.nativeElement.textContent).toContain('View full profile');
  });

  it('renders no customer context panel when the customer read is forbidden', async () => {
    const customerListContacts = jasmine.createSpy().and.resolveTo([]);
    configure({
      customerGet: jasmine.createSpy().and.rejectWith(new HttpErrorResponse({ status: 403 })),
      customerListContacts,
    });
    const fixture = await createComponent();

    expect(fixture.componentInstance.customer()).toBeNull();
    expect(fixture.componentInstance.customerContacts()).toEqual([]);
    expect(fixture.componentInstance.customerRecentActivity()).toEqual([]);
    expect(customerListContacts).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).not.toContain('View full profile');
  });
});
