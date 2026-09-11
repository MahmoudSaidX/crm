import { provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
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
      totalCount: 2,
    });

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
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
  });

  it('loads page 1 on init', async () => {
    const fixture = TestBed.createComponent(TaskList);
    await fixture.componentInstance.load();
    fixture.detectChanges();

    expect(fixture.componentInstance.tasks().length).toBe(2);
    expect(tasksService.list).toHaveBeenCalledWith(
      { search: undefined, statuses: undefined, dueBefore: undefined, dueAfter: undefined },
      1,
      20,
    );
  });

  it('resets to page 1 and reloads when a filter changes', async () => {
    const fixture = TestBed.createComponent(TaskList);
    fixture.componentInstance.search.set('invoice');
    fixture.componentInstance.status.set('Completed');

    fixture.componentInstance.onFilter();
    await fixture.whenStable();

    const [query, page] = tasksService.list.calls.mostRecent().args;
    expect(query.search).toBe('invoice');
    expect(query.statuses).toEqual(['Completed']);
    expect(page).toBe(1);
  });

  it('requests the selected page on lazy load', async () => {
    const fixture = TestBed.createComponent(TaskList);

    fixture.componentInstance.onLazyLoad({ first: 20, rows: 20 });
    await fixture.whenStable();

    expect(tasksService.list.calls.mostRecent().args[1]).toBe(2);
  });

  it('renders zero results as an empty task list', async () => {
    tasksService.list.and.resolveTo({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    const fixture = TestBed.createComponent(TaskList);
    await fixture.componentInstance.load();
    fixture.detectChanges();

    expect(fixture.componentInstance.tasks().length).toBe(0);
    expect(fixture.componentInstance.totalRecords()).toBe(0);
  });

  it('toggles loading around the service call', async () => {
    const fixture = TestBed.createComponent(TaskList);
    const loadPromise = fixture.componentInstance.load();
    expect(fixture.componentInstance.loading()).toBe(true);

    await loadPromise;

    expect(fixture.componentInstance.loading()).toBe(false);
  });
});
