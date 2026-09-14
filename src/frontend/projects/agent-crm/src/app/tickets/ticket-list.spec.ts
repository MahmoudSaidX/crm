import { Params, provideRouter, Router } from '@angular/router';
import { Location } from '@angular/common';
import { provideLocationMocks } from '@angular/common/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
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
  const CATEGORY_ID = '2f1a4c9e-7b3d-4a6f-8e12-0b9d5c3a7e41';

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
    escalationLevel: 0,
    escalationTargetType: null,
    escalationTargetId: null,
    escalatedAtUtc: null,
    createdAtUtc: '2026-09-02T00:00:00Z',
    updatedAtUtc: null,
  };
  const ticketB = {
    ...ticketA,
    id: 'ticket-b',
    ticketNumber: 'TKT-BBB222',
    subject: 'Billing issue',
    // Escalated, so the list's escalation column is exercised independently of
    // the status column (CRM-138).
    escalationLevel: 2,
    escalationTargetType: 'Department' as const,
    escalationTargetId: 'department-a',
    escalatedAtUtc: '2026-09-03T00:00:00Z',
  };

  let ticketsService: jasmine.SpyObj<TicketsService>;

  let router: Router;
  let location: Location;

  const lastCall = () => ticketsService.list.calls.mostRecent().args;

  async function openAt(queryParams: Params): Promise<ComponentFixture<TicketList>> {
    await router.navigate([], { queryParams });
    const fixture = TestBed.createComponent(TicketList);
    fixture.detectChanges();
    await fixture.whenStable();
    return fixture;
  }

  async function settle(fixture: ComponentFixture<TicketList>): Promise<void> {
    await fixture.whenStable();
    // A history navigation (Back/Forward) completes on a macrotask, which
    // whenStable alone does not await.
    await new Promise((resolve) => setTimeout(resolve));
    fixture.detectChanges();
    await fixture.whenStable();
  }

  beforeEach(() => {
    ticketsService = jasmine.createSpyObj<TicketsService>('TicketsService', ['list']);
    ticketsService.list.and.resolveTo({
      items: [ticketA, ticketB],
      page: 1,
      pageSize: 20,
      // Spans several pages: a single-page total makes the paginator clamp an
      // out-of-range page back to 1, which would mask the paging assertions.
      totalCount: 42,
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
    router = TestBed.inject(Router);
    // No app bootstrap happens in a TestBed component test, so the router is
    // not listening to history events by default — without this, Back/Forward
    // would change the URL and navigate nothing.
    router.setUpLocationChangeListener();
    location = TestBed.inject(Location);
  });

  it('loads page 1 on init', async () => {
    const fixture = await openAt({});

    expect(fixture.componentInstance.tickets().length).toBe(2);
    const [query, page, pageSize] = lastCall();
    expect(page).toBe(1);
    expect(pageSize).toBe(20);
    expect(query.search).toBeUndefined();
  });

  it('resets to page 1 and reloads when a filter changes', async () => {
    const fixture = await openAt({ page: '3' });
    fixture.componentInstance.search.set('billing');

    fixture.componentInstance.onFilter();
    await settle(fixture);

    expect(location.path()).toContain('search=billing');
    const [query, page] = lastCall();
    expect(query.search).toBe('billing');
    expect(page).toBe(1);
  });

  it('keeps every filter in the URL and forwards them as the API arrays', async () => {
    await openAt({ search: 'billing', channel: 'Email', categoryId: CATEGORY_ID });

    const [query] = lastCall();
    expect(query.search).toBe('billing');
    expect(query.channels).toEqual(['Email']);
    expect(query.categoryIds).toEqual([CATEGORY_ID]);
  });

  it('preserves filters when paging', async () => {
    const fixture = await openAt({ search: 'billing', channel: 'Email' });

    fixture.componentInstance.onPage({ first: 20, rows: 20 });
    await settle(fixture);

    const [query, page] = lastCall();
    expect(page).toBe(2);
    expect(query.search).toBe('billing');
    expect(query.channels).toEqual(['Email']);
  });

  it('ignores an unknown channel and an unknown sort field from the URL', async () => {
    await openAt({ channel: 'Telepathy', sort: 'ticket_number;--', dir: 'up' });

    const [query] = lastCall();
    expect(query.channels).toBeUndefined();
    expect(query.sortBy).toBe('TicketNumber');
    expect(query.sortDirection).toBe('Asc');
  });

  it('restores state on Back', async () => {
    const fixture = await openAt({ search: 'billing' });
    fixture.componentInstance.onPage({ first: 20, rows: 20 });
    await settle(fixture);
    expect(lastCall()[1]).toBe(2);

    location.back();
    await settle(fixture);

    expect(lastCall()[1]).toBe(1);
    expect(fixture.componentInstance.search()).toBe('billing');
  });

  it('renders zero results as an empty ticket list', async () => {
    ticketsService.list.and.resolveTo({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    const fixture = await openAt({});

    expect(fixture.componentInstance.tickets().length).toBe(0);
    expect(fixture.componentInstance.totalRecords()).toBe(0);
  });

  it('toggles loading around the service call', async () => {
    const fixture = await openAt({});
    const loadPromise = fixture.componentInstance.load({
      page: 1,
      pageSize: 20,
      search: '',
      categoryId: null,
      priorityId: null,
      departmentId: null,
      branchId: null,
      channel: null,
      sort: 'TicketNumber',
      dir: 'Asc',
    });
    expect(fixture.componentInstance.loading()).toBe(true);

    await loadPromise;

    expect(fixture.componentInstance.loading()).toBe(false);
  });
});
