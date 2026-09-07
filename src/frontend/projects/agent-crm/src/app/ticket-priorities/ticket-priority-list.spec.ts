import { provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { TicketPriorityList } from './ticket-priority-list';
import { TicketPrioritiesService } from './ticket-priorities.service';
import {
  AppConfigStore,
  LocaleService,
  provideAppConfig,
  provideTranslations,
  validateAppConfig,
} from '@squad-crm/platform';
import { COMMON_TRANSLATIONS } from '@squad-crm/shared-ui';
import { TICKET_PRIORITY_TRANSLATIONS } from './ticket-priority-translations';
import { Paginator } from 'primeng/paginator';
import { AuthorizationState } from '../auth/authorization.state';

describe('TicketPriorityList', () => {
  const priorityA = {
    id: 'priority-a',
    code: 'URGENT',
    arabicName: 'عاجل',
    englishName: 'Urgent',
    rank: 1,
    description: null,
    isActive: true,
    createdAtUtc: '2026-08-29T00:00:00Z',
    updatedAtUtc: '2026-08-29T00:00:00Z',
  };
  const priorityB = {
    ...priorityA,
    id: 'priority-b',
    englishName: 'Low',
    rank: 4,
    isActive: false,
  };

  let list: jasmine.SpyObj<TicketPrioritiesService>;

  beforeEach(() => {
    localStorage.removeItem('sc.locale');
    list = jasmine.createSpyObj<TicketPrioritiesService>('TicketPrioritiesService', [
      'list',
      'activate',
      'deactivate',
    ]);
    list.list.and.resolveTo({
      items: [priorityA, priorityB],
      page: 1,
      pageSize: 20,
      totalCount: 2,
    });
    list.activate.and.resolveTo({ ...priorityB, isActive: true });
    list.deactivate.and.resolveTo({ ...priorityA, isActive: false });

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideAppConfig(),
        provideTranslations(COMMON_TRANSLATIONS),
        provideTranslations(TICKET_PRIORITY_TRANSLATIONS),
        { provide: TicketPrioritiesService, useValue: list },
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
  });

  afterEach(() => {
    localStorage.removeItem('sc.locale');
    document.documentElement.setAttribute('lang', 'en');
    document.documentElement.setAttribute('dir', 'ltr');
  });

  it('renders rows from a mocked TicketPrioritiesService', async () => {
    const fixture = TestBed.createComponent(TicketPriorityList);
    await fixture.componentInstance.load();
    fixture.detectChanges();

    expect(fixture.componentInstance.priorities().length).toBe(2);
    expect(list.list).toHaveBeenCalledWith(1, 20);
  });

  it('activating an inactive row calls activate and refreshes', async () => {
    const fixture = TestBed.createComponent(TicketPriorityList);
    await fixture.componentInstance.load();

    await fixture.componentInstance.toggleActive(priorityB);

    expect(list.activate).toHaveBeenCalledWith('priority-b');
    expect(list.list).toHaveBeenCalledTimes(2);
  });

  it('deactivating an active row calls deactivate and refreshes', async () => {
    const fixture = TestBed.createComponent(TicketPriorityList);
    await fixture.componentInstance.load();

    await fixture.componentInstance.toggleActive(priorityA);

    expect(list.deactivate).toHaveBeenCalledWith('priority-a');
    expect(list.list).toHaveBeenCalledTimes(2);
  });

  it('localizes feature chrome without translating priority values', async () => {
    TestBed.inject(LocaleService).setLocale('ar');
    const fixture = TestBed.createComponent(TicketPriorityList);
    await fixture.componentInstance.load();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('أولويات التذاكر');
    expect(fixture.nativeElement.textContent).toContain('Urgent');
    const paginator = fixture.debugElement.query(By.directive(Paginator))
      .componentInstance as Paginator;
    expect(paginator.locale).toBe('ar');
  });

  it('hides management actions until ticketpriorities.manage is granted', async () => {
    const fixture = TestBed.createComponent(TicketPriorityList);
    await fixture.componentInstance.load();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('Edit');

    TestBed.inject(AuthorizationState).set(['ticketpriorities.view', 'ticketpriorities.manage']);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Edit');
  });
});
