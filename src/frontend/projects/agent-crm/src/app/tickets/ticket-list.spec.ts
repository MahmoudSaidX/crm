import { provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { TicketList } from './ticket-list';
import { TicketsService } from './tickets.service';
import { TicketCategoriesService } from '../ticket-categories/ticket-categories.service';
import { TicketPrioritiesService } from '../ticket-priorities/ticket-priorities.service';
import { DepartmentsService } from '../departments/departments.service';
import { BranchesService } from '../branches/branches.service';
import {
  AppConfigStore,
  provideAppConfig,
  provideTranslations,
  validateAppConfig,
} from '@squad-crm/platform';
import { COMMON_TRANSLATIONS } from '@squad-crm/shared-ui';
import { TICKET_TRANSLATIONS } from './ticket-translations';

describe('TicketList', () => {
  const ticketA = {
    id: 'ticket-a',
    ticketNumber: 'TKT-AAA111',
    customerId: 'customer-a',
    subject: 'Cannot log in',
    description: 'Cannot log in to the portal.',
    categoryId: 'category-a',
    subcategoryId: null,
    priorityId: 'priority-a',
    departmentId: 'department-a',
    branchId: 'branch-a',
    status: 'Open' as const,
    channel: 'Agent' as const,
    assignedAgentId: null,
    createdAtUtc: '2026-09-02T00:00:00Z',
  };
  const ticketB = { ...ticketA, id: 'ticket-b', ticketNumber: 'TKT-BBB222', subject: 'Billing issue' };

  let ticketsService: jasmine.SpyObj<TicketsService>;

  beforeEach(() => {
    ticketsService = jasmine.createSpyObj<TicketsService>('TicketsService', ['list']);
    ticketsService.list.and.resolveTo({
      items: [ticketA, ticketB],
      page: 1,
      pageSize: 20,
      totalCount: 2,
    });
    const ticketCategoriesService = jasmine.createSpyObj<TicketCategoriesService>(
      'TicketCategoriesService',
      ['list'],
    );
    ticketCategoriesService.list.and.resolveTo({ items: [], page: 1, pageSize: 200, totalCount: 0 });
    const ticketPrioritiesService = jasmine.createSpyObj<TicketPrioritiesService>(
      'TicketPrioritiesService',
      ['list'],
    );
    ticketPrioritiesService.list.and.resolveTo({ items: [], page: 1, pageSize: 200, totalCount: 0 });
    const departmentsService = jasmine.createSpyObj<DepartmentsService>('DepartmentsService', [
      'list',
    ]);
    departmentsService.list.and.resolveTo({ items: [], page: 1, pageSize: 200, totalCount: 0 });
    const branchesService = jasmine.createSpyObj<BranchesService>('BranchesService', ['list']);
    branchesService.list.and.resolveTo({ items: [], page: 1, pageSize: 200, totalCount: 0 });

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideAppConfig(),
        provideTranslations(COMMON_TRANSLATIONS),
        provideTranslations(TICKET_TRANSLATIONS),
        { provide: TicketsService, useValue: ticketsService },
        { provide: TicketCategoriesService, useValue: ticketCategoriesService },
        { provide: TicketPrioritiesService, useValue: ticketPrioritiesService },
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
  });

  it('loads page 1 on init', async () => {
    const fixture = TestBed.createComponent(TicketList);
    await fixture.componentInstance.load();
    fixture.detectChanges();

    expect(fixture.componentInstance.tickets().length).toBe(2);
    expect(ticketsService.list).toHaveBeenCalledWith(
      {
        search: undefined,
        categoryIds: undefined,
        priorityIds: undefined,
        departmentIds: undefined,
        branchIds: undefined,
        channels: undefined,
      },
      1,
      20,
    );
  });

  it('resets to page 1 and reloads when a filter changes', async () => {
    const fixture = TestBed.createComponent(TicketList);
    fixture.componentInstance.search.set('billing');

    fixture.componentInstance.onFilter();
    await fixture.whenStable();

    expect(ticketsService.list).toHaveBeenCalledWith(
      {
        search: 'billing',
        categoryIds: undefined,
        priorityIds: undefined,
        departmentIds: undefined,
        branchIds: undefined,
        channels: undefined,
      },
      1,
      20,
    );
  });

  it('renders zero results as an empty ticket list', async () => {
    ticketsService.list.and.resolveTo({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    const fixture = TestBed.createComponent(TicketList);
    await fixture.componentInstance.load();
    fixture.detectChanges();

    expect(fixture.componentInstance.tickets().length).toBe(0);
    expect(fixture.componentInstance.totalRecords()).toBe(0);
  });

  it('toggles loading around the service call', async () => {
    const fixture = TestBed.createComponent(TicketList);
    const loadPromise = fixture.componentInstance.load();
    expect(fixture.componentInstance.loading()).toBe(true);

    await loadPromise;

    expect(fixture.componentInstance.loading()).toBe(false);
  });
});
