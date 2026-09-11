import { provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { MyTasks } from './my-tasks';
import { TasksService } from './tasks.service';
import {
  AppConfigStore,
  provideAppConfig,
  provideTranslations,
  validateAppConfig,
} from '@squad-crm/platform';
import { COMMON_TRANSLATIONS } from '@squad-crm/shared-ui';
import { TASK_TRANSLATIONS } from './task-translations';

describe('MyTasks', () => {
  const ownedTask = {
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
  };

  let tasksService: jasmine.SpyObj<TasksService>;

  beforeEach(() => {
    tasksService = jasmine.createSpyObj<TasksService>('TasksService', ['list']);
    tasksService.list.and.resolveTo({
      items: [ownedTask],
      page: 1,
      pageSize: 20,
      totalCount: 1,
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

  it('requests only the caller task list and never sends an owner id', async () => {
    const fixture = TestBed.createComponent(MyTasks);
    await fixture.componentInstance.load();
    fixture.detectChanges();

    expect(fixture.componentInstance.tasks().length).toBe(1);
    const [query, page, pageSize] = tasksService.list.calls.mostRecent().args;
    expect(query.myTasksOnly).toBe(true);
    expect(query.sortBy).toBe('DueAtUtc');
    expect(query.sortDirection).toBe('Asc');
    expect(page).toBe(1);
    expect(pageSize).toBe(20);
  });

  it('resets to page 1 and reloads when a filter changes', async () => {
    const fixture = TestBed.createComponent(MyTasks);
    fixture.componentInstance.search.set('invoice');
    fixture.componentInstance.status.set('Open');

    fixture.componentInstance.onFilter();
    await fixture.whenStable();

    const [query, page] = tasksService.list.calls.mostRecent().args;
    expect(query.search).toBe('invoice');
    expect(query.statuses).toEqual(['Open']);
    expect(query.myTasksOnly).toBe(true);
    expect(page).toBe(1);
  });

  it('requests the selected page on lazy load', async () => {
    const fixture = TestBed.createComponent(MyTasks);

    fixture.componentInstance.onLazyLoad({ first: 20, rows: 20 });
    await fixture.whenStable();

    expect(tasksService.list.calls.mostRecent().args[1]).toBe(2);
  });

  it('reloads the current page on refresh', async () => {
    const fixture = TestBed.createComponent(MyTasks);
    await fixture.componentInstance.load(3);

    fixture.componentInstance.onRefresh();
    await fixture.whenStable();

    expect(tasksService.list.calls.mostRecent().args[1]).toBe(3);
  });

  it('renders an empty list without an error', async () => {
    tasksService.list.and.resolveTo({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    const fixture = TestBed.createComponent(MyTasks);
    await fixture.componentInstance.load();
    fixture.detectChanges();

    expect(fixture.componentInstance.tasks().length).toBe(0);
    expect(fixture.componentInstance.loadFailed()).toBe(false);
  });

  it('surfaces a failed load as an error state rather than an empty list', async () => {
    tasksService.list.and.rejectWith(new Error('network'));
    const fixture = TestBed.createComponent(MyTasks);
    await fixture.componentInstance.load();
    fixture.detectChanges();

    expect(fixture.componentInstance.loadFailed()).toBe(true);
    expect(
      (fixture.nativeElement as HTMLElement).querySelector('.my-tasks-error')?.textContent,
    ).toContain('could not be loaded');
  });

  it('clears the error state once a later load succeeds', async () => {
    tasksService.list.and.rejectWith(new Error('network'));
    const fixture = TestBed.createComponent(MyTasks);
    await fixture.componentInstance.load();
    tasksService.list.and.resolveTo({
      items: [ownedTask],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    });

    await fixture.componentInstance.load();

    expect(fixture.componentInstance.loadFailed()).toBe(false);
    expect(fixture.componentInstance.tasks().length).toBe(1);
  });
});
