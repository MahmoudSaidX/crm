import { provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { MyTickets } from './my-tickets';
import { TicketsService } from './tickets.service';
import { TicketCategoriesService } from '../ticket-categories/ticket-categories.service';
import { TicketPrioritiesService } from '../ticket-priorities/ticket-priorities.service';
import {
  AppConfigStore,
  provideAppConfig,
  provideTranslations,
  validateAppConfig,
} from '@squad-crm/platform';
import { COMMON_TRANSLATIONS } from '@squad-crm/shared-ui';
import { TICKET_TRANSLATIONS } from './ticket-translations';

describe('MyTickets', () => {
  const assignedTicket = {
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
    assignedAgentId: 'agent-a',
    escalationLevel: 0,
    escalationTargetType: null,
    escalationTargetId: null,
    escalatedAtUtc: null,
    createdAtUtc: '2026-09-02T00:00:00Z',
    updatedAtUtc: '2026-09-04T00:00:00Z',
  };

  let ticketsService: jasmine.SpyObj<TicketsService>;

  beforeEach(() => {
    ticketsService = jasmine.createSpyObj<TicketsService>('TicketsService', ['list']);
    ticketsService.list.and.resolveTo({
      items: [assignedTicket],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    });
    const ticketCategoriesService = jasmine.createSpyObj<TicketCategoriesService>(
      'TicketCategoriesService',
      ['list'],
    );
    ticketCategoriesService.list.and.resolveTo({
      items: [],
      page: 1,
      pageSize: 200,
      totalCount: 0,
    });
    const ticketPrioritiesService = jasmine.createSpyObj<TicketPrioritiesService>(
      'TicketPrioritiesService',
      ['list'],
    );
    ticketPrioritiesService.list.and.resolveTo({
      items: [],
      page: 1,
      pageSize: 200,
      totalCount: 0,
    });

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideAppConfig(),
        provideTranslations(COMMON_TRANSLATIONS),
        provideTranslations(TICKET_TRANSLATIONS),
        { provide: TicketsService, useValue: ticketsService },
        { provide: TicketCategoriesService, useValue: ticketCategoriesService },
        { provide: TicketPrioritiesService, useValue: ticketPrioritiesService },
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

  it('requests only the caller queue and never sends an agent id', async () => {
    const fixture = TestBed.createComponent(MyTickets);
    await fixture.componentInstance.load();
    fixture.detectChanges();

    expect(fixture.componentInstance.tickets().length).toBe(1);
    const [query, page, pageSize] = ticketsService.list.calls.mostRecent().args;
    expect(query.assignedToMe).toBe(true);
    expect(query.assigneeIds).toBeUndefined();
    expect(query.sortBy).toBe('UpdatedAtUtc');
    expect(query.sortDirection).toBe('Desc');
    expect(page).toBe(1);
    expect(pageSize).toBe(20);
  });

  it('resets to page 1 and reloads when a filter changes', async () => {
    const fixture = TestBed.createComponent(MyTickets);
    fixture.componentInstance.search.set('billing');
    fixture.componentInstance.status.set('InProgress');

    fixture.componentInstance.onFilter();
    await fixture.whenStable();

    const [query, page] = ticketsService.list.calls.mostRecent().args;
    expect(query.search).toBe('billing');
    expect(query.statuses).toEqual(['InProgress']);
    expect(query.assignedToMe).toBe(true);
    expect(page).toBe(1);
  });

  it('requests the selected page on lazy load', async () => {
    const fixture = TestBed.createComponent(MyTickets);

    fixture.componentInstance.onLazyLoad({ first: 20, rows: 20 });
    await fixture.whenStable();

    expect(ticketsService.list.calls.mostRecent().args[1]).toBe(2);
  });

  it('reloads the current page on refresh', async () => {
    const fixture = TestBed.createComponent(MyTickets);
    await fixture.componentInstance.load(3);

    fixture.componentInstance.onRefresh();
    await fixture.whenStable();

    expect(ticketsService.list.calls.mostRecent().args[1]).toBe(3);
  });

  it('renders an empty queue without an error', async () => {
    ticketsService.list.and.resolveTo({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    const fixture = TestBed.createComponent(MyTickets);
    await fixture.componentInstance.load();
    fixture.detectChanges();

    expect(fixture.componentInstance.tickets().length).toBe(0);
    expect(fixture.componentInstance.loadFailed()).toBe(false);
  });

  it('surfaces a failed load as an error state rather than an empty queue', async () => {
    ticketsService.list.and.rejectWith(new Error('network'));
    const fixture = TestBed.createComponent(MyTickets);
    await fixture.componentInstance.load();
    fixture.detectChanges();

    expect(fixture.componentInstance.loadFailed()).toBe(true);
    expect(
      (fixture.nativeElement as HTMLElement).querySelector('.my-tickets-error')?.textContent,
    ).toContain('could not be loaded');
  });

  it('clears the error state once a later load succeeds', async () => {
    ticketsService.list.and.rejectWith(new Error('network'));
    const fixture = TestBed.createComponent(MyTickets);
    await fixture.componentInstance.load();
    ticketsService.list.and.resolveTo({
      items: [assignedTicket],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    });

    await fixture.componentInstance.load();

    expect(fixture.componentInstance.loadFailed()).toBe(false);
    expect(fixture.componentInstance.tickets().length).toBe(1);
  });
});
