import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { TaskDetail } from './task-detail';
import { TasksService } from './tasks.service';
import { AgentTask } from '../task-create/task-create.service';
import { AuthorizationState } from '../auth/authorization.state';
import {
  AppConfigStore,
  LocaleService,
  provideAppConfig,
  provideTranslations,
  validateAppConfig,
} from '@squad-crm/platform';
import { COMMON_TRANSLATIONS } from '@squad-crm/shared-ui';
import { TASK_TRANSLATIONS } from './task-translations';

describe('TaskDetail', () => {
  const task: AgentTask = {
    id: 'task-1',
    title: 'Call customer back',
    details: 'They asked about the invoice.',
    ownerUserId: 'agent-1',
    ticketId: 'ticket-1',
    customerId: 'customer-1',
    dueAtUtc: '2026-09-15T00:00:00Z',
    status: 'Open',
    completedAtUtc: null,
    createdAtUtc: '2026-09-10T00:00:00Z',
    updatedAtUtc: null,
    version: 1,
  };

  function configure(options: {
    get?: jasmine.Spy;
    complete?: jasmine.Spy;
    reopen?: jasmine.Spy;
    permissions?: readonly string[];
  }): void {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        provideAppConfig(),
        provideTranslations(COMMON_TRANSLATIONS),
        provideTranslations(TASK_TRANSLATIONS),
        {
          provide: TasksService,
          useValue: {
            get: options.get ?? jasmine.createSpy().and.resolveTo(task),
            complete: options.complete ?? jasmine.createSpy().and.resolveTo({}),
            reopen: options.reopen ?? jasmine.createSpy().and.resolveTo({}),
          },
        },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: 'task-1' }) } },
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
    const fixture = TestBed.createComponent(TaskDetail);
    for (let tick = 0; tick < 4; tick += 1) {
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

  it('loads the task', async () => {
    configure({});
    const fixture = await createComponent();

    expect(fixture.componentInstance.task()).toEqual(task);
    expect(fixture.componentInstance.notFound()).toBeFalse();
  });

  it('sets notFound when the task cannot be loaded', async () => {
    configure({ get: jasmine.createSpy().and.rejectWith(new Error('not found')) });
    const fixture = await createComponent();

    expect(fixture.componentInstance.notFound()).toBeTrue();
    expect(fixture.componentInstance.task()).toBeNull();
  });

  it('renders the linked ticket and customer only when present', async () => {
    configure({});
    const fixture = await createComponent();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('ticket-1');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('customer-1');
  });

  it('shows no link placeholder when the task has no linked ticket or customer', async () => {
    configure({
      get: jasmine.createSpy().and.resolveTo({ ...task, ticketId: null, customerId: null }),
    });
    const fixture = await createComponent();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Not linked');
  });

  it('hides Complete/Reopen without the tasks.complete permission', async () => {
    configure({});
    const fixture = await createComponent();

    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Complete');
  });

  it('offers Complete for an open task', async () => {
    configure({ permissions: ['tasks.complete'] });
    const openFixture = await createComponent();
    expect((openFixture.nativeElement as HTMLElement).textContent).toContain('Complete');
  });

  it('offers Reopen for a completed task', async () => {
    configure({
      permissions: ['tasks.complete'],
      get: jasmine
        .createSpy()
        .and.resolveTo({ ...task, status: 'Completed', completedAtUtc: '2026-09-12T00:00:00Z' }),
    });
    const completedFixture = await createComponent();
    expect((completedFixture.nativeElement as HTMLElement).textContent).toContain('Reopen');
  });

  it('submits the version it last read when completing', async () => {
    const complete = jasmine.createSpy().and.resolveTo({ ...task, status: 'Completed' });
    configure({ permissions: ['tasks.complete'], complete });
    const fixture = await createComponent();

    await fixture.componentInstance.complete();

    expect(complete).toHaveBeenCalledWith('task-1', { version: 1 });
  });

  it('surfaces the stale-version message and does not retry on 409', async () => {
    const complete = jasmine
      .createSpy()
      .and.rejectWith(new HttpErrorResponse({ status: 409, statusText: 'Conflict' }));
    configure({ permissions: ['tasks.complete'], complete });
    const fixture = await createComponent();

    await fixture.componentInstance.complete();

    expect(fixture.componentInstance.actionErrorKey()).toBe('tasks.detail.errors.staleVersion');
    expect(complete).toHaveBeenCalledTimes(1);
  });

  it('surfaces a forbidden message on 403', async () => {
    const complete = jasmine
      .createSpy()
      .and.rejectWith(new HttpErrorResponse({ status: 403, statusText: 'Forbidden' }));
    configure({ permissions: ['tasks.complete'], complete });
    const fixture = await createComponent();

    await fixture.componentInstance.complete();

    expect(fixture.componentInstance.actionErrorKey()).toBe('tasks.detail.errors.forbidden');
  });

  it('reopens a completed task with the version it last read', async () => {
    const reopen = jasmine.createSpy().and.resolveTo({ ...task, status: 'Open' });
    configure({
      permissions: ['tasks.complete'],
      get: jasmine.createSpy().and.resolveTo({ ...task, status: 'Completed', version: 2 }),
      reopen,
    });
    const fixture = await createComponent();

    await fixture.componentInstance.reopen();

    expect(reopen).toHaveBeenCalledWith('task-1', { version: 2 });
  });
});
