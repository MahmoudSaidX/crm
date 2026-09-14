import { Params, provideRouter, Router } from '@angular/router';
import { provideLocationMocks } from '@angular/common/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TaskList } from './task-list';
import { TasksService } from './tasks.service';
import {
  AppConfigStore,
  provideAppConfig,
  provideTranslations,
  validateAppConfig,
} from '@squad-crm/platform';
import { COMMON_TRANSLATIONS } from '@squad-crm/shared-ui';
import { TASK_TRANSLATIONS } from './task-translations';

describe('TaskList', () => {
  let router: Router;

  const taskA = {
    id: 'task-a',
    title: 'Call customer back',
    details: null,
    ownerUserId: 'agent-a',
    ticketId: null,
    customerId: null,
    dueAtUtc: '2026-09-15T00:00:00Z',
    status: 'Open' as const,
    completedAtUtc: null,
    createdAtUtc: '2026-09-10T00:00:00Z',
    updatedAtUtc: null,
    version: 1,
    reminderAtUtc: null,
    reminderStatus: 'None' as const,
    reminderTriggeredAtUtc: null,
  };
  const taskB = {
    ...taskA,
    id: 'task-b',
    title: 'Follow up on invoice',
    status: 'Completed' as const,
    completedAtUtc: '2026-09-11T00:00:00Z',
  };

  let tasksService: jasmine.SpyObj<TasksService>;

  beforeEach(() => {
    tasksService = jasmine.createSpyObj<TasksService>('TasksService', ['list']);
    tasksService.list.and.resolveTo({
      items: [taskA, taskB],
      page: 1,
      pageSize: 20,
      // Spans several pages: a single-page total makes the paginator clamp an
      // out-of-range page back to 1, which would mask the paging assertions.
      totalCount: 42,
    });

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideLocationMocks(),
        provideAppConfig(),
        provideTranslations(COMMON_TRANSLATIONS),
        provideTranslations(TASK_TRANSLATIONS),
        { provide: TasksService, useValue: tasksService },
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
  });

  async function openAt(queryParams: Params): Promise<ComponentFixture<TaskList>> {
    await router.navigate([], { queryParams });
    const fixture = TestBed.createComponent(TaskList);
    fixture.detectChanges();
    await fixture.whenStable();
    return fixture;
  }

  async function settle(fixture: ComponentFixture<TaskList>): Promise<void> {
    await fixture.whenStable();
    // A history navigation (Back/Forward) completes on a macrotask, which
    // whenStable alone does not await.
    await new Promise((resolve) => setTimeout(resolve));
    fixture.detectChanges();
    await fixture.whenStable();
  }

  it('loads page 1 on init', async () => {
    const fixture = await openAt({});

    expect(fixture.componentInstance.tasks().length).toBe(2);
    expect(tasksService.list).toHaveBeenCalledWith(
      { search: undefined, statuses: undefined, dueBefore: undefined, dueAfter: undefined },
      1,
      20,
    );
  });

  it('resets to page 1 and reloads when a filter changes', async () => {
    const fixture = await openAt({ page: '3' });
    fixture.componentInstance.search.set('invoice');
    fixture.componentInstance.status.set('Completed');

    fixture.componentInstance.onFilter();
    await settle(fixture);

    const [query, page] = tasksService.list.calls.mostRecent().args;
    expect(query.search).toBe('invoice');
    expect(query.statuses).toEqual(['Completed']);
    expect(page).toBe(1);
  });

  it('requests the selected page when the paginator moves', async () => {
    const fixture = await openAt({});

    fixture.componentInstance.onPage({ first: 20, rows: 20 });
    await settle(fixture);

    expect(tasksService.list.calls.mostRecent().args[1]).toBe(2);
  });

  it('carries the due preset by NAME so a shared link stays meaningful later', async () => {
    const fixture = await openAt({ due: 'overdue' });

    expect(fixture.componentInstance.dueFilter()).toBe('overdue');
    const [query] = tasksService.list.calls.mostRecent().args;
    expect(query.dueBefore).toBeDefined();
    expect(query.dueAfter).toBeUndefined();
  });

  it('ignores an unknown status or due preset in the URL', async () => {
    await openAt({ status: 'Deleted', due: 'someday' });

    const [query] = tasksService.list.calls.mostRecent().args;
    expect(query.statuses).toBeUndefined();
    expect(query.dueBefore).toBeUndefined();
    expect(query.dueAfter).toBeUndefined();
  });

  it('renders zero results as an empty task list', async () => {
    tasksService.list.and.resolveTo({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    const fixture = await openAt({});

    expect(fixture.componentInstance.tasks().length).toBe(0);
    expect(fixture.componentInstance.totalRecords()).toBe(0);
  });

  it('toggles loading around the service call', async () => {
    const fixture = await openAt({});
    const loadPromise = fixture.componentInstance.load({
      page: 1,
      pageSize: 20,
      search: '',
      status: null,
      due: null,
    });
    expect(fixture.componentInstance.loading()).toBe(true);

    await loadPromise;

    expect(fixture.componentInstance.loading()).toBe(false);
  });
});
